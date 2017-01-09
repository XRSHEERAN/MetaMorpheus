﻿using IndexSearchAndAnalyze;
using IO.MzML;
using IO.Thermo;
using MassSpectrometry;
using MetaMorpheus;
using Spectra;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IndexSearchAndAnalyze
{
    public class MyCalibrateTask : MyTask
    {
        public MyCalibrateTask() : base(0)
        {
        }

        public string precursorMassToleranceTextBox { get; internal set; }
        public int precursorMassToleranceComboBox { get; internal set; }
        public string missedCleavagesTextBox { get; internal set; }
        public Protease protease { get; internal set; }
        public string maxModificationIsoformsTextBox { get; internal set; }
        public int initiatorMethionineBehaviorComboBox { get; internal set; }
        public string productMassToleranceTextBox { get; internal set; }
        public int productMassToleranceComboBox { get; internal set; }
        public bool? bCheckBox { get; internal set; }
        public bool? yCheckBox { get; internal set; }
        public bool? checkBoxDecoy { get; internal set; }
        public string acceptedPrecursorMassErrorsTextBox { get; internal set; }
        public bool? checkBoxMonoisotopic { get; internal set; }

        private ObservableCollection<ModListForSearch> modFileList;

        public override void DoTask(ObservableCollection<RawData> completeRawFileListCollection, ObservableCollection<XMLdb> completeXmlDbList, AllTasksParams po)
        {
            var currentRawFileList = completeRawFileListCollection.Where(b => b.Use).Select(b => b.FileName).ToList();
            string output_folder = Path.Combine(Path.GetDirectoryName(currentRawFileList[0]), DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture));

            if (!Directory.Exists(output_folder))
                Directory.CreateDirectory(output_folder);

            for (int spectraFileIndex = 0; spectraFileIndex < currentRawFileList.Count; spectraFileIndex++)
            {
                var origDataFile = currentRawFileList[spectraFileIndex];
                po.RTBoutput("Loading spectra file...");
                IMsDataFile<IMzSpectrum<MzPeak>> myMsDataFile;
                if (Path.GetExtension(origDataFile).Equals(".mzML"))
                    myMsDataFile = new Mzml(origDataFile, 400);
                else
                    myMsDataFile = new ThermoRawFile(origDataFile, 400);
                po.RTBoutput("Opening spectra file...");
                myMsDataFile.Open();
                po.RTBoutput("Finished opening spectra file " + Path.GetFileName(origDataFile));

                List<MorpheusModification> variableModifications = modFileList.Where(b => b.Variable).SelectMany(b => b.getMods()).ToList();
                List<MorpheusModification> fixedModifications = modFileList.Where(b => b.Fixed).SelectMany(b => b.getMods()).ToList();
                List<MorpheusModification> localizeableModifications = modFileList.Where(b => b.Localize).SelectMany(b => b.getMods()).ToList();
                Dictionary<string, List<MorpheusModification>> identifiedModsInXML;
                HashSet<string> unidentifiedModStrings;
                GenerateModsFromStrings(completeXmlDbList.Select(b => b.FileName).ToList(), localizeableModifications, out identifiedModsInXML, out unidentifiedModStrings);

                var proteinList = completeXmlDbList.SelectMany(b => b.getProteins(true, identifiedModsInXML)).ToList();

                List<SearchMode> searchModes = new List<SearchMode>();
                //searchModes.Add(new SearchMode("within7thousandthsOfZero", (double asdf) => { return asdf > -0.007 && asdf < 0.007; }));
                searchModes.Add(new FunctionSearchMode("withinHalfADaltonOfZero", (double asdf, double bb) => { return asdf - bb > -0.5 && asdf < 0.5; }));

                //ClassicSearchParams searchParams = new ClassicSearchParams(myMsDataFile, spectraFileIndex, variableModifications, fixedModifications, localizeableModifications, proteinList, double.Parse(productMassToleranceTextBox), protease, searchModes[0], po.RTBoutput, po.ReportProgress);
                //ClassicSearchEngine searchEngine = new ClassicSearchEngine(searchParams);
                //ClassicSearchResults searchResults = (ClassicSearchResults)searchEngine.Run();
                //po.RTBoutput(searchResults.ToString());

                //List<NewPsm> newPsms = searchResults.newPsms;

                //AnalysisParams analysisParams = new AnalysisParams(newPsms, new Dictionary<CompactPeptide, HashSet<PeptideWithSetModifications>>(), proteinList, variableModifications, fixedModifications, localizeableModifications, protease, searchModes[0], myMsDataFile, double.Parse(productMassToleranceTextBox), MainWindow.unimodDeserialized, MainWindow.uniprotDeseralized, (BinTreeStructure myTreeStructure, string s) => Writing.WriteTree(myTreeStructure, output_folder, Path.GetFileNameWithoutExtension(origDataFile) + s), (List<NewPsmWithFDR> h, string s) => Writing.WriteToTabDelimitedTextFileWithDecoys(h, output_folder, Path.GetFileNameWithoutExtension(origDataFile) + s), po.RTBoutput, po.ReportProgress);
                //AnalysisEngine analysisEngine = new AnalysisEngine(analysisParams);
                //AnalysisResults analysisResults = (AnalysisResults)analysisEngine.Run();

                //po.RTBoutput(analysisResults.ToString());

                //var identifications = analysisResults.allResultingIdentifications[0];

                //myMsDataFile.Close();
                //myMsDataFile = null;

                ////Now can calibrate!!!

                //SoftwareLockMassParams a = mzCalIO.mzCalIO.GetReady(origDataFile, identifications, double.Parse(productMassToleranceTextBox));

                //po.AttachoutRichTextBoxHandlerr(handler => a.outputHandler += handler);
                //po.AttachoSuccessfullyFinishedFileHandler(handler => a.finishedFileHandler += handler);
                //po.AttachoutProgressBarHandler(handler => a.progressHandler += handler);
                //po.AttachoutRichTextBoxHandlerr(handler => a.watchHandler += handler);

                //SoftwareLockMassRunner.Run(a);
            }
        }

        private static void GenerateModsFromStrings(List<string> listOfXMLdbs, List<MorpheusModification> modsKnown, out Dictionary<string, List<MorpheusModification>> modsToLocalize, out HashSet<string> modsInXMLtoTrim)
        {
            modsToLocalize = new Dictionary<string, List<MorpheusModification>>();
            var modsInXML = ProteomeDatabaseReader.ReadXMLmodifications(listOfXMLdbs);
            modsInXMLtoTrim = new HashSet<string>(modsInXML);
            foreach (var knownMod in modsKnown)
                if (modsInXML.Contains(knownMod.NameInXML))
                {
                    if (modsToLocalize.ContainsKey(knownMod.NameInXML))
                        modsToLocalize[knownMod.NameInXML].Add(knownMod);
                    else
                        modsToLocalize.Add(knownMod.NameInXML, new List<MorpheusModification>() { knownMod });
                    modsInXMLtoTrim.Remove(knownMod.NameInXML);
                }
        }
    }
}