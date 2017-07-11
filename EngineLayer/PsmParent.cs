﻿using Chemistry;
using MassSpectrometry;
using MzLibUtil;
using Proteomics;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EngineLayer
{
    public abstract class PsmParent
    {

        #region Protected Constructors

        protected PsmParent(int notch, double score, int scanIndex, Ms2ScanWithSpecificMass scan, double peptideMonoisotopicMass)
        {
            this.Notch = notch;
            this.Score = score;
            this.ScanIndex = scanIndex;
            this.FullFilePath = scan.FullFilePath;
            this.ScanNumber = scan.TheScan.OneBasedScanNumber;
            this.PrecursorScanNumber = scan.TheScan.OneBasedPrecursorScanNumber;
            this.ScanRetentionTime = scan.TheScan.RetentionTime;
            this.ScanExperimentalPeaks = scan.TheScan.MassSpectrum.Size;
            this.TotalIonCurrent = scan.TheScan.TotalIonCurrent;
            this.ScanPrecursorCharge = scan.PrecursorCharge;
            this.ScanPrecursorMonoisotopicPeak = scan.PrecursorMonoisotopicPeak;
            this.ScanPrecursorMass = scan.PrecursorMass;
            this.QuantIntensity = new double[1];
            this.PeptideMonoisotopicMass = peptideMonoisotopicMass;
        }

        #endregion Protected Constructors

        #region Public Properties

        public double[] QuantIntensity { get; set; }
        public double MostAbundantMass { get; set; }
        public int Notch { get; }
        public double Score { get; }
        public int ScanNumber { get; }
        public int PrecursorScanNumber { get; }
        public double ScanRetentionTime { get; }
        public int ScanExperimentalPeaks { get; }
        public double TotalIonCurrent { get; }
        public int ScanPrecursorCharge { get; }
        public IMzPeak ScanPrecursorMonoisotopicPeak { get; }
        public double ScanPrecursorMass { get; }
        public string FullFilePath { get; }
        public int ScanIndex { get; }
        public int NumAmbiguous { get; set; }
        public ProteinLinkedInfo Pli { get; private set; }
        public double PeptideMonoisotopicMass { get; internal set; }
        public FdrInfo FdrInfo { get; set; }
        public LocalizationResults LocalizationResults { get; internal set; }

        #endregion Public Properties

        #region Public Methods

        public static double MatchIons(IMsDataScan<IMzSpectrum<IMzPeak>> thisScan, Tolerance productMassTolerance, double[] sorted_theoretical_product_masses_for_this_peptide, double[] matchedIonMassesListPositiveIsMatch)
        {
            var TotalProductsHere = sorted_theoretical_product_masses_for_this_peptide.Length;
            if (TotalProductsHere == 0)
                return 0;
            int MatchingProductsHere = 0;
            double MatchingIntensityHere = 0;

            // speed optimizations
            double[] experimental_mzs = thisScan.MassSpectrum.XArray;
            double[] experimental_intensities = thisScan.MassSpectrum.YArray;
            int num_experimental_peaks = experimental_mzs.Length;

            int currentTheoreticalIndex = -1;
            double currentTheoreticalMass;
            do
            {
                currentTheoreticalIndex++;
                currentTheoreticalMass = sorted_theoretical_product_masses_for_this_peptide[currentTheoreticalIndex];
            } while (double.IsNaN(currentTheoreticalMass) && currentTheoreticalIndex < sorted_theoretical_product_masses_for_this_peptide.Length - 1);

            if (double.IsNaN(currentTheoreticalMass))
                return 0;

            double currentTheoreticalMz = currentTheoreticalMass + Constants.protonMass;

            int testTheoreticalIndex;
            double testTheoreticalMZ;
            double testTheoreticalMass;
            // Loop over all experimenal indices
            for (int experimentalIndex = 0; experimentalIndex < num_experimental_peaks; experimentalIndex++)
            {
                double currentExperimentalMZ = experimental_mzs[experimentalIndex];
                // If found match
                if (productMassTolerance.Within(currentExperimentalMZ, currentTheoreticalMz))
                {
                    MatchingProductsHere++;
                    MatchingIntensityHere += experimental_intensities[experimentalIndex];
                    matchedIonMassesListPositiveIsMatch[currentTheoreticalIndex] = currentTheoreticalMass;
                    currentTheoreticalIndex++;
                    if (currentTheoreticalIndex == TotalProductsHere)
                        break;
                    currentTheoreticalMass = sorted_theoretical_product_masses_for_this_peptide[currentTheoreticalIndex];
                    currentTheoreticalMz = currentTheoreticalMass + Constants.protonMass;
                }
                // Else if for sure did not reach the next theoretical yet, move to next experimental
                else if (currentExperimentalMZ < currentTheoreticalMz)
                    continue;
                // Else if for sure passed a theoretical
                else
                {
                    // Mark the theoretical as missed
                    matchedIonMassesListPositiveIsMatch[currentTheoreticalIndex] = -currentTheoreticalMass;

                    // Move on to next index and never come back!
                    currentTheoreticalIndex++;
                    if (currentTheoreticalIndex == TotalProductsHere)
                        break;
                    currentTheoreticalMass = sorted_theoretical_product_masses_for_this_peptide[currentTheoreticalIndex];
                    currentTheoreticalMz = currentTheoreticalMass + Constants.protonMass;

                    // Start with the current ones
                    testTheoreticalIndex = currentTheoreticalIndex;
                    testTheoreticalMZ = currentTheoreticalMz;
                    testTheoreticalMass = currentTheoreticalMass;
                    // Mark the skipped theoreticals as not found. The last one is not for sure, might be flipped!
                    while (currentExperimentalMZ > testTheoreticalMZ)
                    {
                        matchedIonMassesListPositiveIsMatch[testTheoreticalIndex] = -currentTheoreticalMass;
                        // Store old info for possible reuse
                        currentTheoreticalMass = testTheoreticalMass;
                        currentTheoreticalMz = testTheoreticalMZ;
                        currentTheoreticalIndex = testTheoreticalIndex;

                        // Update test stuff!
                        testTheoreticalIndex++;
                        if (testTheoreticalIndex == TotalProductsHere)
                            break;
                        testTheoreticalMass = sorted_theoretical_product_masses_for_this_peptide[testTheoreticalIndex];
                        testTheoreticalMZ = testTheoreticalMass + Constants.protonMass;
                    }

                    experimentalIndex--;
                }
            }
            return MatchingProductsHere + MatchingIntensityHere / thisScan.TotalIonCurrent;
        }

        public static string GetTabSeparatedHeader()
        {
            var sb = new StringBuilder();
            sb.Append("File Name" + '\t');
            sb.Append("Scan Number" + '\t');
            sb.Append("Scan Retention Time" + '\t');
            sb.Append("Num Experimental Peaks" + '\t');
            sb.Append("Total Ion Current" + '\t');
            sb.Append("Precursor Scan Number" + '\t');
            sb.Append("Precursor Charge" + '\t');
            sb.Append("Precursor MZ" + '\t');
            sb.Append("Precursor Intensity" + '\t');
            sb.Append("Precursor Mass" + '\t');
            sb.Append("Score" + '\t');
            sb.Append("Notch" + '\t');
            sb.Append("Quantification Intensity" + '\t');
            sb.Append("Ambiguous Matches" + '\t');

            sb.Append(ProteinLinkedInfo.GetTabSeparatedHeader() + '\t');
            sb.Append(LocalizationResults.GetTabSeparatedHeader() + '\t');

            // Need info from both current and from Pli
            sb.Append("Improvement Possible" + '\t');
            sb.Append("Mass Diff (Da)" + '\t');
            sb.Append("Mass Diff (ppm)");

            sb.Append("Cumulative Target" + '\t');
            sb.Append("Cumulative Decoy" + '\t');
            sb.Append("QValue" + '\t');
            sb.Append("Cumulative Target Notch" + '\t');
            sb.Append("Cumulative Decoy Notch" + '\t');
            sb.Append("QValue Notch");

            return sb.ToString();
        }

        public abstract CompactPeptide GetCompactPeptide(Dictionary<ModificationWithMass, ushort> modsDictionary);

        public void SetProteinLinkedInfo(Dictionary<CompactPeptide, HashSet<PeptideWithSetModifications>> matching, Dictionary<ModificationWithMass, ushort> modsDictionary)
        {
            Pli = new ProteinLinkedInfo(matching[GetCompactPeptide(modsDictionary)]);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append(Path.GetFileNameWithoutExtension(FullFilePath) + '\t');
            sb.Append(ScanNumber.ToString(CultureInfo.InvariantCulture) + '\t');
            sb.Append(ScanRetentionTime.ToString("F5", CultureInfo.InvariantCulture) + '\t');
            sb.Append(ScanExperimentalPeaks.ToString("F5", CultureInfo.InvariantCulture) + '\t');
            sb.Append(TotalIonCurrent.ToString("F5", CultureInfo.InvariantCulture) + '\t');
            sb.Append(PrecursorScanNumber.ToString(CultureInfo.InvariantCulture) + '\t');
            sb.Append(ScanPrecursorCharge.ToString("F5", CultureInfo.InvariantCulture) + '\t');
            sb.Append(ScanPrecursorMonoisotopicPeak.Mz.ToString("F5", CultureInfo.InvariantCulture) + '\t');
            sb.Append(ScanPrecursorMonoisotopicPeak.Intensity.ToString("F5", CultureInfo.InvariantCulture) + '\t');
            sb.Append(ScanPrecursorMass.ToString("F5", CultureInfo.InvariantCulture) + '\t');
            sb.Append(Score.ToString("F3", CultureInfo.InvariantCulture) + '\t');
            sb.Append(Notch.ToString("F3", CultureInfo.InvariantCulture) + '\t');
            sb.Append(string.Join("|", QuantIntensity) + '\t');
            sb.Append(NumAmbiguous.ToString("F5", CultureInfo.InvariantCulture) + '\t');

            sb.Append(Pli.ToString() + '\t');
            if (LocalizationResults != null)
            {
                sb.Append(LocalizationResults.ToString() + '\t');
                sb.Append((LocalizationResults.LocalizedScores.Max() - Score).ToString("F3", CultureInfo.InvariantCulture) + '\t');
            }
            sb.Append((ScanPrecursorMass - Pli.PeptideMonoisotopicMass).ToString("F5", CultureInfo.InvariantCulture) + '\t');
            sb.Append(((ScanPrecursorMass - Pli.PeptideMonoisotopicMass) / Pli.PeptideMonoisotopicMass * 1e6).ToString("F5", CultureInfo.InvariantCulture));

            sb.Append(FdrInfo.cumulativeTarget.ToString(CultureInfo.InvariantCulture) + '\t');
            sb.Append(FdrInfo.cumulativeDecoy.ToString(CultureInfo.InvariantCulture) + '\t');
            sb.Append(FdrInfo.QValue.ToString("F6", CultureInfo.InvariantCulture) + '\t');
            sb.Append(FdrInfo.cumulativeTargetNotch.ToString(CultureInfo.InvariantCulture) + '\t');
            sb.Append(FdrInfo.cumulativeDecoyNotch.ToString(CultureInfo.InvariantCulture) + '\t');
            sb.Append(FdrInfo.QValueNotch.ToString("F6", CultureInfo.InvariantCulture));

            return sb.ToString();
        }

        public void SetValues(int cumulativeTarget, int cumulativeDecoy, double tempQValue, int cumulativeTargetNotch, int cumulativeDecoyNotch, double tempQValueNotch)
        {
            FdrInfo = new FdrInfo()
            {
                cumulativeTarget = cumulativeTarget,
                cumulativeDecoy = cumulativeDecoy,
                QValue = tempQValue,
                cumulativeTargetNotch = cumulativeTargetNotch,
                cumulativeDecoyNotch = cumulativeDecoyNotch,
                QValueNotch = tempQValueNotch
            };
        }

        #endregion Public Methods

    }
}