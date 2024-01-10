# NISTBartender

NISTBartender is a Windows GUI interface for dual barcode parsing to prep files for input to bartender-1.1. NISTBartender is written in C#.

This software works in conjunction with the bartender-1.1 barcode clustering algorithm (Zhao, L., Liu, Z., Levy, S. F. & Wu, S. Bartender: a fast and accurate clustering algorithm to count barcode reads. Bioinformatics 34, 739–747 (2018)). 

In a typical workflow, the barcode parsing is run in the NISTBartender Windows GUI, and then clustering is run on a Linux system with bartender-1.1. Then, some additional barcode merging, cleanup, and sorting steps are run via the Windows GUI.

# Prerequisites

To install and run NISTBartender requires the following additional software:

- Visual Studio (C# editor and compiler): https://visualstudio.microsoft.com/, installed on a Windows computer

- bartender-1.1: https://github.com/LaoZZZZZ/bartender-1.1, installed on a Linux ort Mac system.

- With the changes made to the Windows Subsystem for Linux, I can no longer get bartender to run via Ubuntu on a Windows PC. So, the clustering step has to be run outside the GUI, and the "Cluster" and "Parse and Cluster" buttons in the GUI are disabled.

# Installation

1. Install bartender-1.1 (see instructions in the document, "bartender-1.1 installation and use on AWS.txt" - in this repository)
2. Use Visual Studio to open the NISTBartender.sln file, and build (i.e. compile) the release configuration of NISTBartender
3. Run the NISTBartender GUI by double clicking on the BartenderWindow.exe executable in the folder \NISTBartender\BartenderWindow\bin\Release\net6.0-windows



# Overview and Data Requirements

NISTBartender is written for a double barcode system, with the two barcode components read from the forward and reverse reads from an Illumina sequencing platform.

NISTBartender takes barcode amplicon sequencing data and first runs a parsing algorithm to extract the barcodes and label them with the appropriate sample information. It passes the barcode information to the bartender-1.1 program for barcode clustering. It then corrects for possible barcode sequencing in-del errors by merging different length barcode clusters based on a Levenshtein distance criteria. It then sorts the barcodes based on the sample multiplexing tags and corrects for PCR jackpotting. Finally, it sorts the barcodes from most abundant to least abundant and marks potential chimeric double barcodes.

Input data should be in g-zipped fastq files, with separate files for the forward and reverse reads. Multiple fastq files can be used for each direction (e.g. data from multiple HiSeq lanes).

The Parsing step produces two output files that are used as input to bartender-1.1: "output_file_label_forward_lintags.txt" and "output_file_label_reverse_lintags.txt"

For small datasets (e.g. data from a MiSeq run), the entire workflow can be run on a Windows computer. In this case, the NISTBartender GUI automatically handles calls to bartender-1.1 in the Ubuntu environment via the Windows Subsystem for Linux. For larger datasets, we recommend the use of a dedicated Linux computer with sufficient memory to run the bartender-1.1 clustering steps. For example, we used a high-end AWS Linux instance for clustering a dataset resulting from four lanes of HiSeq sequencing. In that case, clustering took approximately 6 hours to run. The bartender-1.1 command text can be copied from the output text field at the bottom of the NISTBartender GUI to facilitate this.

The bartender-1.1 algorithm is run separately for the forward and reverse barcodes. It produces four files that are used by additional steps controlled by the NISTBartender GUI: "output_file_label_forward_cluster.csv", "output_file_label_forward_barcode.csv", "output_file_label_reverse_cluster.csv", and "output_file_label_reverse_barcode.csv".

The NISTBartender GUI creates a single output file, "output_file_label.trimmed_sorted_counts.csv" - which has the resulting barcode count data sorted into columns for each sample (identified via sample multiplexing tags in the sequencing data).

# Getting Started

Once each component is installed, follow these steps to analyze the sample dataset:

1. Run the BartenderWindow.exe Windows GUI program.
2. Load the file "example.xml" (located in the \examples\parameters folder), which contains the settings for analyzing the sample dataset.
   - The Forward Read Sequence field should contain the full amplicon sequence in the forward direction; the Reverse Read Sequence field should contain the reverse complement of the amplicon sequence. The "Copy Reverse Complement" button can be used to automatically fill in the Reverse Read Sequence field.
3. Use the File menu to select the input and output data directories (e.g. "\examples\input" and "\examples\barcode_analysis"). 
4. Click the "Analyze Sequences" button. This automatically detects different sequence components and highlights them in Forward Read Sequence and Reverse Read Sequence fields.
5. Click the "Analyze Multi-Tags" button. This uses information from the Multiplexing Tags fields to calculate and display nearest neighbor information for the sample multiplexing tags in the output field at the bottom of the GUI. It also initializes parameters used by the parsing algorithm to find the multiplexing tag sequences:
   - It assigns forward-reverse multi-tag pairs using all possible combinations of the Forward and Reverse multi-tags entered in the Multiplexing Tags fields on the right side of the GUI. It also adds any extra forward and reverse tags from the “Extra Multiplexing tags” field.
   - The format for the Forward and Reverse Multi-tags is the multi-tag sequence optionally followed by a comma and an identifier.
   - The forward and reverse identifiers get combined to create a sample identifier – for the example data, the samples are the wells from a 96-well plate.
   - The format for the Extra Multi-Tags is forward-tag, reverse-tag, identifier (the identifier is again optional). forward-reverse pairs on the “Extra” list only get paired with themselves during parsing.
6. Click the "Auto Regex" button. This automatically generates the Regex that is used for matching to lineage tags. That Regex can be manually edited if needed. 
7. The Forward Read Sequence and Reverse Read Sequence fields should now show the amplicon sequence for barcode sequencing, with color codes to indicate various parts of the sequence.
   - Z's (yellow) indicate the UMI tags used for PCR jackpotting correction.
   - X's (light purple) indicate the sample multiplexing tags.
   - Light blue is used to highlight flanking sequences used for locating the multiplexing tags and the barcodes.
   - Green is used to highlight the barcode sequences, with '*' used to indicate random bases in the barcodes.

8. Make sure the filenames for the Forward and Reverse fastq files are correct (files should be g-zipped). The Forward and Reverse fastq files fields can each contain multiple filenames. They should be in matching order and separated by commas.
9. Click the "Parse" button. This will start the parsing algorithm. On a Windows PC with mid-level performance configuration, this takes about a minute for the example data, or approximately 2 hours for four lanes of HiSeq data.
   - Test Parsing and Clustering initially with the "Max Sequences to Parse" field set to a relatively low value (10,000 to 1,000,000); then set to zero, to run the full set of input sequences.
10. Click the "Cluster" button to run the bartender-1.1 clustering algorithm on the local PC, in the Windows Subsystem for Linux Ubuntu environment (see setup instructions in the file named "Ubuntu and Bartender setup on Windows 10 or AWS computer.docx")

   - The command line call needed to run the clustering on AWS or another Linux system is displayed at the bottom of the Windows GUI; after initial testing (with Max Sequences to Parse set to low value), copy the bartender command line string, move the parser output files to the AWS/Linux system and copy the bartender command string to the Linux command prompt to run the clustering algorithm for the full scale data.
   - On a high-end AWS machine, with four lanes of HiSeq data, the clustering algorithm takes about 6 hours to run.
11. If necessary, move clustering output files back to Windows machine
12. Click "Merge Lengths" to run the algorithm to merge barcodes of different lengths (from sequencing in-del errors); the algorithm uses a Levenshtein distance criteria to merge barcode clusters of different lengths to account for in-del read errors.

    - This step takes 20-30 minutes on a typical Windows PC for a 4-lane HiSeq dataset.
13. Click the "Sort" button. This sorts the double barcode reads by sample and saves to a new file for each sample; then corrects for PCR de-jackpotting and creates an output file with the read counts for each double barcode for every sample ("output_file_label.sorted_counts.csv").

    - This takes ~1 minute for the example data or ~3 hours for a HiSeq dataset.
14. Click the "Trim Sorted BCs" button. This runs saves barcodes to new file with only double barcodes above the Sorted Barcode Cutoff Frequency; also re-orders the output from most abundant to least abundant barcodes and marks possible chimeric barcodes; saves result with new filename: "output_file_label.trimmed_sorted_counts.csv"

    - This takes ~1 minute for the example data or ~6 minutes for a HiSeq dataset.
      	
