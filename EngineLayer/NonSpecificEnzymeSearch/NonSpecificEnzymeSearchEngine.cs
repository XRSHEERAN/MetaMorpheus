﻿using Chemistry;
using MassSpectrometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EngineLayer.NonSpecificEnzymeSearch
{
    public class NonSpecificEnzymeSearchEngine : ModernSearch.ModernSearchEngine
    {
        #region Private Fields

        private static readonly double nitrogenAtomMonoisotopicMass = PeriodicTable.GetElement("N").PrincipalIsotope.AtomicMass;
        private static readonly double oxygenAtomMonoisotopicMass = PeriodicTable.GetElement("O").PrincipalIsotope.AtomicMass;
        private static readonly double hydrogenAtomMonoisotopicMass = PeriodicTable.GetElement("H").PrincipalIsotope.AtomicMass;
        private static readonly double waterMonoisotopicMass = PeriodicTable.GetElement("H").PrincipalIsotope.AtomicMass * 2 + PeriodicTable.GetElement("O").PrincipalIsotope.AtomicMass;

        #endregion Private Fields

        #region Public Constructors

        public NonSpecificEnzymeSearchEngine(Psm[] globalPsms, Ms2ScanWithSpecificMass[] listOfSortedms2Scans, List<CompactPeptide> peptideIndex, List<int>[] fragmentIndex, List<ProductType> lp, int currentPartition, CommonParameters CommonParameters, bool addCompIons, MassDiffAcceptor massDiffAcceptors, List<string> nestedIds) : base(globalPsms, listOfSortedms2Scans, peptideIndex, fragmentIndex, lp, currentPartition, CommonParameters, addCompIons, massDiffAcceptors, nestedIds)
        {
        }

        #endregion Public Constructors

        #region Protected Methods

        protected override MetaMorpheusEngineResults RunSpecific()
        {
            double progress = 0;
            int oldPercentProgress = 0;
            ReportProgress(new ProgressEventArgs(oldPercentProgress, "Performing nonspecific search... " + currentPartition + "/" + CommonParameters.TotalPartitions, nestedIds));
            TerminusType terminusType = ProductTypeMethod.IdentifyTerminusType(lp);

            int intScoreCutoff = (int)CommonParameters.ScoreCutoff;
            //var roundedPepMasses = peptideIndex.Select(p => (int)Math.Round(p.MonoisotopicMassIncludingFixedMods * 100, 0)).ToList();
            byte byteScoreCutoff = Convert.ToByte(intScoreCutoff);

            Parallel.ForEach(Partitioner.Create(0, listOfSortedms2Scans.Length), new ParallelOptions { MaxDegreeOfParallelism = CommonParameters.MaxThreadsToUse }, range =>
            {
                byte[] scoringTable = new byte[peptideIndex.Count];
                HashSet<int> idsOfPeptidesPossiblyObserved = new HashSet<int>();

                for (int i = range.Item1; i < range.Item2; i++)
                {
                    // empty the scoring table to score the new scan (conserves memory compared to allocating a new array)
                    Array.Clear(scoringTable, 0, scoringTable.Length);
                    idsOfPeptidesPossiblyObserved.Clear();
                    var scan = listOfSortedms2Scans[i];

                    // filter ms2 fragment peaks by intensity
                    int numFragmentsToUse = 0;
                    if (CommonParameters.TopNpeaks != null)
                        numFragmentsToUse = (int)CommonParameters.TopNpeaks;
                    else
                        numFragmentsToUse = scan.NumPeaks;

                    var peaks = scan.TheScan.MassSpectrum.FilterByNumberOfMostIntense(numFragmentsToUse).ToList();
                    double largestIntensity = scan.TheScan.MassSpectrum.YofPeakWithHighestY;

                    // get allowed precursor masses
                    var t = massDiffAcceptor.GetAllowedPrecursorMassIntervals(scan.PrecursorMass);
                    double lowestMassPeptideToLookFor = t.Min(p => p.allowedInterval.Minimum);
                    double highestMassPeptideToLookFor = t.Max(p => p.allowedInterval.Maximum);

                    int obsPreviousFragmentCeilingMz = 0;
                    int[] compPreviousFragmentFloorMz = new int[dissociationTypes.Count];
                    for (int j = 0; j < compPreviousFragmentFloorMz.Length; j++)
                        compPreviousFragmentFloorMz[j] = (int)Math.Ceiling(scan.PrecursorMass * fragmentBinsPerDalton);
                    // search peaks for matches
                    foreach (IMzPeak peak in peaks)
                    {
                        if (CommonParameters.MinRatio == null || (peak.Intensity / largestIntensity) >= CommonParameters.MinRatio)
                        {
                            // assume charge state 1 to calculate mz tolerance
                            var mzTolerance = (CommonParameters.ProductMassTolerance.Value / 1e6) * peak.Mz;
                            int obsFragmentFloorMz = (int)Math.Floor((peak.Mz - mzTolerance) * fragmentBinsPerDalton);
                            obsFragmentFloorMz = obsFragmentFloorMz > obsPreviousFragmentCeilingMz ? obsFragmentFloorMz : obsPreviousFragmentCeilingMz;
                            int obsFragmentCeilingMz = (int)Math.Ceiling((peak.Mz + mzTolerance) * fragmentBinsPerDalton);
                            obsPreviousFragmentCeilingMz = obsFragmentCeilingMz + 1;

                            // get all theoretical fragments this experimental fragment could be
                            for (int fragmentBin = obsFragmentFloorMz; fragmentBin <= obsFragmentCeilingMz; fragmentBin++)
                            {
                                if (fragmentIndex[fragmentBin] != null)
                                {
                                    List<int> peptideIdsInThisBin = fragmentIndex[fragmentBin];
                                    foreach (int id in peptideIdsInThisBin)
                                    {
                                        scoringTable[id]++;
                                        if (scoringTable[id] == byteScoreCutoff)
                                            idsOfPeptidesPossiblyObserved.Add(id);
                                    }
                                }
                            }
                            if (addCompIons)
                            {
                                //okay, we're not actually adding in complementary m/z peaks, we're doing a shortcut and just straight up adding the mass assuming that they're z=1
                                for(int j=0; j<dissociationTypes.Count; j++)
                                {
                                    double complementaryPeak = scan.PrecursorMass - peak.Mz + complementaryIonConversionDictionary[dissociationTypes[j]] + Constants.protonMass;
                                    int compFragmentFloorMz = (int)Math.Floor((complementaryPeak - mzTolerance) * fragmentBinsPerDalton);
                                    int compFragmentCeilingMz = (int)Math.Ceiling((complementaryPeak + mzTolerance) * fragmentBinsPerDalton);
                                    if (compFragmentCeilingMz > compPreviousFragmentFloorMz[j])
                                        compFragmentCeilingMz = compPreviousFragmentFloorMz[j];
                                    compPreviousFragmentFloorMz[j] = compFragmentFloorMz;
                                    // get all theoretical fragments this experimental fragment could be
                                    for (int fragmentBin = compFragmentFloorMz; fragmentBin <= compFragmentCeilingMz; fragmentBin++)
                                    {
                                        if (fragmentIndex[fragmentBin] != null)
                                        {
                                            List<int> peptideIdsInThisBin = fragmentIndex[fragmentBin];
                                            foreach (int id in peptideIdsInThisBin)
                                            {
                                                scoringTable[id]++;
                                                if (scoringTable[id] == byteScoreCutoff)
                                                    idsOfPeptidesPossiblyObserved.Add(id);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // done with initial scoring; refine scores and create PSMs
                    if (idsOfPeptidesPossiblyObserved.Any())
                    {
                        int maxInitialScore = idsOfPeptidesPossiblyObserved.Max(id => scoringTable[id]);
                        while (maxInitialScore >= intScoreCutoff)
                        {
                            foreach (var id in idsOfPeptidesPossiblyObserved.Where(id => scoringTable[id] == maxInitialScore))
                            {
                                var candidatePeptide = peptideIndex[id];
                                double[] fragmentMasses = candidatePeptide.ProductMassesMightHaveDuplicatesAndNaNs(lp).Distinct().Where(p => !Double.IsNaN(p)).OrderBy(p => p).ToArray();
                                double peptideScore = CalculatePeptideScore(scan.TheScan, CommonParameters.ProductMassTolerance, fragmentMasses, scan.PrecursorMass, dissociationTypes, addCompIons);
                                Tuple<int, double> notchAndPrecursor = Accepts(scan.PrecursorMass, candidatePeptide, terminusType, massDiffAcceptor);
                                if (notchAndPrecursor.Item1 >= 0)
                                {
                                    CompactPeptideWithModifiedMass cp = new CompactPeptideWithModifiedMass(candidatePeptide, notchAndPrecursor.Item2);

                                    if (globalPsms[i] == null)
                                        globalPsms[i] = new Psm(cp, notchAndPrecursor.Item1, peptideScore, i, scan);
                                    else
                                        globalPsms[i].AddOrReplace(cp, peptideScore, notchAndPrecursor.Item1, CommonParameters.ReportAllAmbiguity);
                                }
                            }
                            if (globalPsms[i] != null)
                                break;
                            maxInitialScore--;
                        }
                    }

                    // report search progress
                    progress++;
                    var percentProgress = (int)((progress / listOfSortedms2Scans.Length) * 100);

                    if (percentProgress > oldPercentProgress)
                    {
                        oldPercentProgress = percentProgress;
                        ReportProgress(new ProgressEventArgs(percentProgress, "Performing modern search... " + currentPartition + "/" + CommonParameters.TotalPartitions, nestedIds));
                    }
                }
            });
            return new MetaMorpheusEngineResults(this);
        }

        #endregion Protected Methods

        #region Private Methods

        private Tuple<int, double> Accepts(double scanPrecursorMass, CompactPeptide peptide, TerminusType terminusType, MassDiffAcceptor searchMode)
        {
            //all masses in N and CTerminalMasses are b-ion masses, which are one water away from a full peptide
            int localminPeptideLength = CommonParameters.DigestionParams.MinPeptideLength ?? 0;
            if (terminusType == TerminusType.N)
            {
                for (int i = localminPeptideLength; i < peptide.NTerminalMasses.Count(); i++)
                {
                    double theoMass = peptide.NTerminalMasses[i] + waterMonoisotopicMass;
                    int notch = searchMode.Accepts(scanPrecursorMass, theoMass);
                    if (notch >= 0)
                    {
                        return new Tuple<int, double>(notch, theoMass);
                    }
                    else if (theoMass > scanPrecursorMass)
                    {
                        break;
                    }
                }
                //if the theoretical and experimental have the same mass
                if (peptide.NTerminalMasses.Count() > localminPeptideLength)
                {
                    double totalMass = peptide.MonoisotopicMassIncludingFixedMods;// + Constants.protonMass;
                    int notch = searchMode.Accepts(scanPrecursorMass, totalMass);
                    if (notch >= 0)
                    {
                        return new Tuple<int, double>(notch, totalMass);
                    }
                }
            }
            else//if (terminusType==TerminusType.C)
            {
                for (int i = localminPeptideLength; i < peptide.CTerminalMasses.Count(); i++)
                {
                    double theoMass = peptide.CTerminalMasses[i] + waterMonoisotopicMass;
                    int notch = searchMode.Accepts(scanPrecursorMass, theoMass);
                    if (notch >= 0)
                    {
                        return new Tuple<int, double>(notch, theoMass);
                    }
                    else if (theoMass > scanPrecursorMass)
                    {
                        break;
                    }
                }
                //if the theoretical and experimental have the same mass
                if (peptide.CTerminalMasses.Count() > localminPeptideLength)
                {
                    double totalMass = peptide.MonoisotopicMassIncludingFixedMods;// + Constants.protonMass;
                    int notch = searchMode.Accepts(scanPrecursorMass, totalMass);
                    if (notch >= 0)
                    {
                        return new Tuple<int, double>(notch, totalMass);
                    }
                }
            }
            return new Tuple<int, double>(-1, -1);
        }

        #endregion Private Methods

    }
}