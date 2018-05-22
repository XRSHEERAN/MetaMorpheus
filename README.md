# MetaMorpheus: Free, Open-Source PTM Discovery <a href="https://twitter.com/intent/tweet?text=Discover PTMs in bottom-up data with MetaMorpheus https://tinyurl.com/y9an55ah"> <img src="https://img.shields.io/twitter/url/http/shields.io.svg?style=social" alt="Tweet"></a>

[![Build status](https://ci.appveyor.com/api/projects/status/0jt31252xny5aoxt/branch/master?svg=true)](https://ci.appveyor.com/project/smith-chem-wisc/metamorpheus/branch/master)
[![codecov](https://codecov.io/gh/smith-chem-wisc/MetaMorpheus/branch/master/graph/badge.svg)](https://codecov.io/gh/smith-chem-wisc/MetaMorpheus/branch/master)

[![](https://github.com/smith-chem-wisc/MetaMorpheus/blob/master/GUI/MMnice.ico)](https://smith-chem-wisc.github.io/MetaMorpheus/)

Download the current version [here](https://github.com/smith-chem-wisc/MetaMorpheus/releases/latest). For first-time Windows users, choose "MetaMorpheusInstaller.msi" and install MetaMorpheus. Check out our <img src ="https://user-images.githubusercontent.com/16841846/40379523-eb130166-5dbb-11e8-8a03-559599cdd560.png">[getting started video](https://www.youtube.com/watch?v=rNgCDzi7STs&list=PLVk5tTSZ1aWlhNPh7jxPQ8pc0ElyzSUQb) on YouTube 

MetaMorpheus is a bottom-up proteomics database search software with integrated post-translational modification (PTM) discovery capability.
This program combines features of [Morpheus](https://github.com/cwenger/Morpheus) and [G-PTM-D](https://github.com/smith-chem-wisc/gptmd) in a single tool.

Check out the [wiki page](https://github.com/smith-chem-wisc/MetaMorpheus/wiki) for software details!

## Major Features

* Database Search: A robust search algorithm that identifies peptides by their fragmentation spectra.
* Calibration: A calibration tool that uses peptides identified by a database search to calibrate the m/z values of all peaks in the spectra. This improves the quality of any subsequent search or analysis of the data.
* G-PTM-D: Post-translational modification (PTM) discovery, which expands the scope of peptide identifications to include both known and unknown PTMs.
* Quantification: Ultrafast label-free peptide quantification with FlashLFQ. MS2-identified peptides are used as "seeds" for peakfinding, including PTM-containing peptides.

## System Requirements

* Environment:
  * For .NET Core versions: .NET Core 2.0.0 runtime
  * For .NET Framework versions: .NET Framework 4.7.1
    * The .NET Framework versions have the ability to read Thermo .RAW files. Those require [Thermo MSFileReader](https://thermo.flexnetoperations.com/control/thmo/search?query=MSFileReader+3.0+SP2) installed (v3.0 SP2 is recommended), and at least an 64-bit Windows 7. 
  * Installation of the latest [Microsoft C++ Redistributable](https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads) is strongly recommended.
* At least 16 GB RAM recommended

## Spectra Requirements

* One of the following formats:
   * Thermo .raw
   * .mzML file in centroid mode
* MS1 and MS2 scans

## Database Requirements

UniProt .XML or .fasta format; may be used in compressed (.gz) format.

## Test Installation (Windows GUI)

1. Download the latest MetaMorpheusInstaller.msi [release](https://github.com/smith-chem-wisc/MetaMorpheus/releases), and install MetaMorpheus.
2. Download the example spectra and database files from [https://uwmadison.box.com/v/MetaMorpheusPublic](https://uwmadison.box.com/s/7rjo9sohki6xrgltr0r56fjpyb9ni8cp).
3. Open MetaMorpheusGUI.exe, and drag and drop the raw spectra files and the compressed Uniprot .xml database into MetaMorpheus.
4. Add a series of Tasks to make a workflow for MetaMorpheus to follow. Drag the .toml files (these files store MetaMorpheus's search parameters) **IN ORDER** (Task1 - Task5) into the application.
  * Task1SearchExample.toml - the standard search functionality.
  * Task2CalibrationExample.toml - will calibrate a .raw or .mzML file based on high scoring search results and write a new calibrated .mzML file. Calibration can take a long time depending on the file.
  * Task3SearchExample.toml - searches the newly calibrated data file, which demonstrates improved performance and allows for tighter search tolerances.
  * Task4GptmdExample.toml - searches the calibrated data file to find high-probability PTMs. This search task generates a new .xml protein database with annotated PTM possibilities discovered by G-PTM-D.
  * Task5SearchExample.toml - searches the calibrated input file against the G-PTM-D .xml database. This search result is the highest confidence in terms of total PSMs and modified peptides.
5. Click "Run All Tasks!"
6. As the third task completes, open the results.txt files for the first and third tasks (before and after calibration, respectively). Observe the increase in the number of confident PSMs and identified peptides due to calibration.
7. As the fifth task completes, open the results.txt files for the third and fifth tasks. Observe the increase in the number of confident PSMs identified due to discovered PTM-containing peptides.

## Typical Usage (Windows GUI)
1. Open MetaMorpheusGUI.exe, and drag and drop your raw spectra files and protein database into the GUI.
2. Select "New Calibrate Task" tab and enter appropriate search parameters, using slightly liberal mass tolerances. Then "Add the Calibration Task". Subsequent tasks (searches) will use suggested ppm tolerances automatically generated by the calibration task.
3. Select "New GPTMD Task" tab. Specify the G-PTM-D modifications that you think may be present in your sample. Many typical modifications are pre-selected. Then "Add the GPTMD Task".
4. Select "New Search Task" tab. Specify the Post-Search Parameters (e.g. protein parsimony, quantification). Then "Add the Search Task".
5. Select "Run all tasks!". This search automatically looks for PTMs uncovered in the G-PTM-D step.

## Test Installation (Windows Command Line)

1. Download the latest [release](https://github.com/smith-chem-wisc/MetaMorpheus/releases). Extract "MetaMorpheusDotNetFrameworkAppveyor.zip" using, for example, [7-Zip](http://www.7-zip.org/).
2. Download the example spectra and database files at [https://uwmadison.box.com/v/MetaMorpheusPublic](https://uwmadison.box.com/s/7rjo9sohki6xrgltr0r56fjpyb9ni8cp) to the folder with the CMD.exe executable.
3. Run the command:

```
CMD.exe -t Task1SearchExample.toml Task2CalibrationExample.toml Task3SearchExample.toml Task4GptmdExample.toml Task5SearchExample.toml -s 04-30-13_CAST_Frac4_6uL.raw 04-30-13_CAST_Frac5_4uL.raw -d uniprot-mouse-reviewed-3-9-2017.xml.gz
```
4. As the third task completes, open the results.txt files for the first and third tasks (before and after calibration). Observe the increase in the number of confident PSMs identified due to calibration.
5. As the fifth task completes, open the results.txt files for the third and fifth tasks. Observe the increase in the number of confident PSMs identified due to an addition of new plausible PTMs.

## Test Installation (.NET Core)

1. Download the latest [release](https://github.com/smith-chem-wisc/MetaMorpheus/releases). Extract files from "MetaMorpheusDotNetCoreAppveyor.zip".
2. Download the files at [https://uwmadison.box.com/v/MetaMorpheusPublic](https://uwmadison.box.com/s/7rjo9sohki6xrgltr0r56fjpyb9ni8cp) to the folder with the CMD.dll file.
3. Run the command:

```
dotnet CMD/bin/Release/netcoreapp2.0/CMD.dll -t Task1SearchExample.toml Task2CalibrationExample.toml Task3SearchExample.toml Task4GptmdExample.toml Task5SearchExample.toml -s 04-30-13_CAST_Frac4_6uL.raw 04-30-13_CAST_Frac5_4uL.raw -d uniprot-mouse-reviewed-3-9-2017.xml.gz
```

## mzLib


[mzLib](https://github.com/smith-chem-wisc/mzLib) is a [nuget](https://www.nuget.org/packages/mzLib/) package that we created as an all-purpose toolchest for mass-spec data analysis and many of its functions provide the tools for MetaMorpheus. mzLib is freely available for use in mass-spec applications. You do not need to download mzLib separately to run MetaMorpheus; it is already included.


## References
* [Enhanced Global Post-translational Modification Discovery with MetaMorpheus--J Proteome Res. 2018 May 4;17(5):1844-1851](https://pubs.acs.org/doi/10.1021/acs.jproteome.7b00873)
* [Global Post-translational Modification Discovery--J. Proteome Res., 2017, 16, 1383-1390](http://pubs.acs.org/doi/abs/10.1021/acs.jproteome.6b00034)

* [Global Identification of Protein Post-translational Modifications in a Single-Pass Database Search--J. Proteome Res., 2015, 14 (11), pp 4714–4720](http://pubs.acs.org/doi/abs/10.1021/acs.jproteome.5b00599)

* [A Proteomics Search Algorithm Specifically Designed for High-Resolution Tandem Mass Spectra--J. Proteome Res., 2013, 12 (3), pp 1377–1386](http://pubs.acs.org/doi/abs/10.1021/pr301024c)
