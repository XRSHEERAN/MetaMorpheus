﻿using Proteomics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulProteomicsDatabases;

namespace EngineLayer.Indexing
{
    public class IndexingEngine : MetaMorpheusEngine
    {
        #region Private Fields

        private const int fragmentBinsPerDalton = 1000;
        private readonly List<Protein> proteinList;

        private readonly List<ModificationWithMass> fixedModifications;
        private readonly List<ModificationWithMass> variableModifications;
        private readonly List<ProductType> lp;
        private readonly int currentPartition;
        private readonly DecoyType decoyType;
        private readonly IEnumerable<DigestionParams> CollectionOfDigestionParams;
        private readonly int totalPartitions;
        private readonly int threadsToUse;

        #endregion Private Fields

        #region Public Constructors

        public IndexingEngine(List<Protein> proteinList, List<ModificationWithMass> variableModifications, List<ModificationWithMass> fixedModifications, List<ProductType> lp, int currentPartition, DecoyType decoyType, IEnumerable<DigestionParams> CollectionOfDigestionParams, int totalPartitions, List<string> nestedIds) : base(nestedIds)
        {
            this.proteinList = proteinList;
            this.variableModifications = variableModifications;
            this.fixedModifications = fixedModifications;
            this.lp = lp;
            this.currentPartition = currentPartition + 1;
            this.decoyType = decoyType;
            this.CollectionOfDigestionParams = CollectionOfDigestionParams;
            this.totalPartitions = totalPartitions;

            this.threadsToUse = Environment.ProcessorCount;
            if (threadsToUse > 1)
                threadsToUse--;
        }

        #endregion Public Constructors

        #region Public Methods

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Partitions: " + currentPartition + "/" + totalPartitions);
            sb.AppendLine("Search Decoys: " + decoyType);
            sb.AppendLine("Number of proteins: " + proteinList.Count);
            sb.AppendLine("Number of fixed mods: " + fixedModifications.Count);
            sb.AppendLine("Number of variable mods: " + variableModifications.Count);
            sb.AppendLine("lp: " + string.Join(",", lp));
            foreach (var digestionParams in CollectionOfDigestionParams)
            {
                sb.AppendLine("protease: " + digestionParams.Protease);
                sb.AppendLine("initiatorMethionineBehavior: " + digestionParams.InitiatorMethionineBehavior);
                sb.AppendLine("maximumMissedCleavages: " + digestionParams.MaxMissedCleavages);
                sb.AppendLine("minPeptideLength: " + digestionParams.MinPeptideLength);
                sb.AppendLine("maxPeptideLength: " + digestionParams.MaxPeptideLength);
                sb.AppendLine("maximumVariableModificationIsoforms: " + digestionParams.MaxModificationIsoforms);
            }
            sb.Append("Localizeable mods: " + proteinList.Select(b => b.OneBasedPossibleLocalizedModifications.Count).Sum());
            return sb.ToString();
        }

        #endregion Public Methods

        #region Protected Methods

        protected override MetaMorpheusEngineResults RunSpecific()
        {
            double progress = 0;
            int oldPercentProgress = 0;
            TerminusType terminusType = ProductTypeMethod.IdentifyTerminusType(lp);

            // digest database
            HashSet<CompactPeptide> peptideToId = new HashSet<CompactPeptide>();

            Parallel.ForEach(Partitioner.Create(0, proteinList.Count), new ParallelOptions { MaxDegreeOfParallelism = threadsToUse }, range =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    foreach (var digestionParams in CollectionOfDigestionParams)
                    {
                        var digestedList = proteinList[i].Digest(digestionParams, fixedModifications);
                        foreach (var peptide in digestedList)
                        {
                            var ListOfModifiedPeptides = peptide.GetPeptidesWithSetModifications(digestionParams, variableModifications);

                            foreach (PeptideWithSetModifications pepWithSetMods in ListOfModifiedPeptides)
                            {
                                CompactPeptide compactPeptide = pepWithSetMods.CompactPeptide(terminusType);

                                var observed = peptideToId.Contains(compactPeptide);
                                if (observed)
                                    continue;
                                lock (peptideToId)
                                {
                                    observed = peptideToId.Contains(compactPeptide);
                                    if (observed)
                                        continue;
                                    peptideToId.Add(compactPeptide);
                                }
                            }
                        }
                    }

                    progress++;
                    var percentProgress = (int)((progress / proteinList.Count) * 100);

                    if (percentProgress > oldPercentProgress)
                    {
                        oldPercentProgress = percentProgress;
                        ReportProgress(new ProgressEventArgs(percentProgress, "Digesting proteins...", nestedIds));
                    }
                }
            });

            // sort peptides by mass
            var peptidesSortedByMass = peptideToId.AsParallel().WithDegreeOfParallelism(threadsToUse).OrderBy(p => p.MonoisotopicMassIncludingFixedMods).ToList();
            peptideToId = null;

            // create fragment index 
            int maxFragmentMass = 0;
            for (int i = peptidesSortedByMass.Count - 1; i >= 0; i--)
            {
                if (!Double.IsNaN(peptidesSortedByMass[i].MonoisotopicMassIncludingFixedMods))
                {
                    maxFragmentMass = (int)Math.Ceiling(peptidesSortedByMass[i].MonoisotopicMassIncludingFixedMods);
                    break;
                }
            }

            List<int>[] fragmentIndex = new List<int>[0];

            try
            {
                fragmentIndex = new List<int>[maxFragmentMass * fragmentBinsPerDalton];
            }
            catch (OutOfMemoryException)
            {
                throw new MetaMorpheusException("Fragment mass too large for fragment indexing engine; try \"Classic Search\" mode and report this error to the MetaMorpheus developers");
            }

            // populate fragment index
            progress = 0;
            oldPercentProgress = 0;
            for (int i = 0; i < peptidesSortedByMass.Count; i++)
            {
                var validFragments = peptidesSortedByMass[i].ProductMassesMightHaveDuplicatesAndNaNs(lp).Distinct().Where(p => !Double.IsNaN(p));

                foreach (var theoreticalFragmentMass in validFragments)
                {
                    //double mz = Chemistry.ClassExtensions.ToMz(theoreticalFragmentMass, 1);
                    //int fragmentBin = (int)Math.Round(mz * fragmentBinsPerDalton);
                    int fragmentBin = (int)Math.Round(theoreticalFragmentMass * fragmentBinsPerDalton);

                    if (fragmentIndex[fragmentBin] == null)
                        fragmentIndex[fragmentBin] = new List<int> { i };
                    else
                        fragmentIndex[fragmentBin].Add(i);
                }

                progress++;
                var percentProgress = (int)((progress / peptidesSortedByMass.Count) * 100);

                if (percentProgress > oldPercentProgress)
                {
                    oldPercentProgress = percentProgress;
                    ReportProgress(new ProgressEventArgs(percentProgress, "Creating fragment index...", nestedIds));
                }
            }

            return new IndexingResults(peptidesSortedByMass, fragmentIndex, this);
        }

        #endregion Protected Methods
    }
}