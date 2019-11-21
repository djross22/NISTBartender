using System;
using BarcodeParser;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace BarcodeSorter
{
    public class Sorter
    {
        //***********************************************************************************************
        //All public fields need to be set by controlling app before SortBarcodes() is called
        //    input file strings should be set to the full path for the file

        public string forLinTagFile, revLinTagFile; //lineage tag files output from Parser

        public string outputPrefix; //output prefix used to automatically create output files; directory plus partial filename

        public string forBarcodeFile, revBarcodeFile; // "..._barcode.csv" files output from Clusterer; 1st column is lin-tag sequence, third column is cluster ID

        public string forClusterFile, revClusterFile; // "..._cluster.csv" files output from Clusterer; 1st column is cluster ID. 2nd column is cluster center

        public List<string> sampleIdList; //List of sample IDs; = MainWindow.mutiTagIdDict.Values

        public int sortedBarcodeThreshold; //threshold for trimming low-count double barcodes after sorting


        //***********************************************************************************************

        private IDisplaysOutputText outputReceiver;

        private Dictionary<string, int> forBarcodeDict, revBarcodeDict; //Dictionaries for looking up barcode cluster IDs: key = barcode sequence, value = barcode ID.
        private Dictionary<int, string> forCenterDict, revCenterDict; //Dictionaries for looking up barcode cluster centers: key = barcode ID, value = barcode center sequence.

        //private Dictionary<(int, int), HashSet<string>> barcodeSetDict; //Dictionary for storing a List of sampleID+UMI corresponding to each pair of forward, reverse barcodes; key = (forwardBarcodeID, reversebarcodeID); value = List of sampleID+UMI

        //Dictionary for storing output counts for each sample; key = (forwardBarcodeID, reversebarcodeID); value = Dictionary<sampleID, count>
        private Dictionary<(int, int), Dictionary<string, int>> outputCountDictionary;

        private string sampleFileDirectory; //path to directory for sample-specific output files used during sorting

        public Sorter(BarcodeParser.IDisplaysOutputText receiver)
        {
            outputReceiver = receiver;

        }

        private void SendOutputText(TextWriter logWriter, string text, bool newLine = true)
        {
            outputReceiver.DisplayOutput(text, newLine);

            if (newLine) logWriter.WriteLine(text);
            else logWriter.Write(text);
        }

        private void SendOutputText(TextWriter logWriter)
        {
            SendOutputText(logWriter, "");
        }

        private void SendOutputText(string text, bool newLine = true)
        {
            outputReceiver.DisplayOutput(text, newLine);
        }

        private static IEnumerable<string[]> GetNextLinTags(string forwardFile, string reverseFile)
        {
            Int64 count = 0;

            //Create StreamReaders from both forward and reverse lineage tag files
            using (StreamReader forwardReader = new StreamReader(forwardFile), reverseReader = new StreamReader(reverseFile))
            {
                //check to be sure there are more lines first
                while (true)
                {
                    count += 1;

                    //Returns an array of 3 strings: forwardTag, reverseTag, samplePlusUmi
                    string[] retString = new string[3];

                    //each line from forward and reverse lin-tag files is the lin-tag sequence and sampleID+UMI, separated by a comma
                    string forLine = forwardReader.ReadLine();
                    string revLine = reverseReader.ReadLine();

                    if ((forLine is null) || (revLine is null)) break;

                    string[] forwardSplitLine = forLine.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    //Then clean up forwardSplitLine by trimming white space from ends of each string, and removing empty strings.
                    forwardSplitLine = forwardSplitLine.Select(s => s.Trim()).ToArray();
                    forwardSplitLine = forwardSplitLine.Where(x => !string.IsNullOrEmpty(x)).ToArray();

                    string[] reverseSplitLine = revLine.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    //Then clean up reverseSplitLine by trimming white space from ends of each string, and removing empty strings.
                    reverseSplitLine = reverseSplitLine.Select(s => s.Trim()).ToArray();
                    reverseSplitLine = reverseSplitLine.Where(x => !string.IsNullOrEmpty(x)).ToArray();

                    //make sure that the sampleID+UMI is the same for forward and reverse, otherwise throw an error
                    if (forwardSplitLine[1] != reverseSplitLine[1])
                    {
                        string msg = $"Sample identifier plus UMI on line {count} does not match between forward and reverse lin-tag files.\n";
                        msg += $"Forward sample + UMI: {forwardSplitLine[1]}.\n";
                        msg += $"Reverse sample + UMI: {reverseSplitLine[1]}.\n";
                        throw new ArgumentException(msg);
                    }

                    retString[0] = forwardSplitLine[0]; //forward lin-tag
                    retString[1] = reverseSplitLine[0]; //reverse lin-tag
                    retString[2] = reverseSplitLine[1]; //sample ID plus UMI
                    
                    yield return retString;
                }
            }
        }

        private void MakeBarcodeDictionary(bool forward)
        {
            string inFile, backInFile;
            Dictionary<string, int> dict = new Dictionary<string, int>();
            Dictionary<int, string> backDict = new Dictionary<int, string>();
            if (forward)
            {
                inFile = forBarcodeFile;
                backInFile = forClusterFile;
            }
            else
            {
                inFile = revBarcodeFile;
                backInFile = revClusterFile;
            }

            string line;
            string[] splitLine;
            //"_barcode.csv" files: each line of input should be in the form: "{lin-tag sequence},{frequence},{cluster ID}"
            using (StreamReader reader = new StreamReader(inFile))
            {
                reader.ReadLine(); //first line of file is header, so read it but don't do anything with it.

                while ((line = reader.ReadLine()) != null)
                {
                    splitLine = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    splitLine = splitLine.Select(s => s.Trim()).ToArray();

                    dict[splitLine[0]] = int.Parse(splitLine[2]);
                }
            }

            //"_cluster.csv" files: each line of input should be in the form: "{cluster ID},{cluster center},..."
            using (StreamReader reader = new StreamReader(backInFile))
            {
                reader.ReadLine(); //first line of file is header, so read it but don't do anything with it.

                while ((line = reader.ReadLine()) != null)
                {
                    splitLine = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    splitLine = splitLine.Select(s => s.Trim()).ToArray();

                    backDict[int.Parse(splitLine[0])] = splitLine[1];
                }
            }

            if (forward)
            {
                forBarcodeDict = dict;
                forCenterDict = backDict;
            }
            else
            {
                revBarcodeDict = dict;
                revCenterDict = backDict;
            }

        }

        public void SortBarcodes()
        {
            //Set up log file to keep record of output text from clustering
            TextWriter logFileWriter = TextWriter.Synchronized(new StreamWriter($"{outputPrefix}.sorting.log"));

            DateTime startTime = DateTime.Now;
            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, "*********************************************");
            SendOutputText(logFileWriter, $"Running Barcode Sorting.");
            SendOutputText(logFileWriter, $"Sorting started: {startTime}.");
            SendOutputText(logFileWriter, "");

            SendOutputText(logFileWriter, $"{DateTime.Now}; Start building barcode look-up dictionaries.");
            //Create the dictionaries to look up barcode IDs and barcode centers
            MakeBarcodeDictionary(forward: true);
            MakeBarcodeDictionary(forward: false);
            SendOutputText(logFileWriter, $"{DateTime.Now}; Finished building barcode look-up dictionaries.");
            SendOutputText(logFileWriter);


            SendOutputText(logFileWriter, $"{DateTime.Now}; Start sorting barcodes from input files:");
            SendOutputText(logFileWriter, $"    {forLinTagFile},");
            SendOutputText(logFileWriter, $"    {revLinTagFile},");
            SendOutputText(logFileWriter, $"... to sample files.");

            //open a file for each sample to save list of barcode IDs and UMIs
            sampleFileDirectory = $"{outputPrefix}_samples";
            if (!Directory.Exists(sampleFileDirectory))
            {
                Directory.CreateDirectory(sampleFileDirectory);
            }
            Dictionary<string, TextWriter> sampleFileWriters = new Dictionary<string, TextWriter>();
            Dictionary<string, string> sampleFilePaths = new Dictionary<string, string>();
            foreach (string s in sampleIdList)
            {
                string filePath = $"{sampleFileDirectory}\\{s}.txt";
                sampleFileWriters[s] = new StreamWriter(filePath);
                sampleFilePaths[s] = filePath;
            }

            int count = 0;
            int notFoundCount = 0;
            //First, read each barcode from the input files and write info to sample-specific files:
            //    forward barcode ID, reverse barcode ID, sample ID plus UMI string
            foreach (string[] stringArr in GetNextLinTags(forLinTagFile, revLinTagFile))
            {
                //stringArr[0] = forward lin-tag
                //stringArr[1] = reverse lin-tag
                //stringArr[2] = sample ID plus UMI

                try
                {
                    int forBarcodeId = forBarcodeDict[stringArr[0]];
                    int revBarcodeId = revBarcodeDict[stringArr[1]];

                    string sampleIdPlusUmi = stringArr[2];
                    string sampleId = sampleIdPlusUmi.Remove(sampleIdPlusUmi.IndexOf('_'));

                    sampleFileWriters[sampleId].Write($"{forBarcodeId},{revBarcodeId},{sampleIdPlusUmi}\n");

                    count++;
                    if (count % 1000000 == 0) SendOutputText(".", newLine: false);
                    if (count % 10000000 == 0 && count > 0) SendOutputText($"{count}", newLine: false);
                }
                catch (KeyNotFoundException ex)
                {
                    //Don't need to do anything here except keep count.
                    //    If one of the lin-tags is not in the look-up dictionary, it is presumably becasue it belongs to a barcode below the cutoff frequency.
                    //Also, if the sample ID starts with "unexpected", then will also not be found as a key in the sampleFileWriters dictionary
                    notFoundCount++;
                }
            }
            //Close the sample-specific output files
            foreach (string s in sampleIdList)
            {
                sampleFileWriters[s].Close();
            }
            SendOutputText(logFileWriter, $"{DateTime.Now}; Finished sorting barcodes into sample-specific files.");
            SendOutputText(logFileWriter);

            //Next, for each sample-specific file, make the barcodeSetDict = Dictionary<(int, int), HashSet<string>>
            //    key = (forward barcode ID, reverse barcode ID); value = HashSet of sampleIdPlusUmiSet
            //Then, convert to counts for each file
            //    TODO?: replace with ParallelForEach?
            SendOutputText(logFileWriter, $"{DateTime.Now}; Start counting and de-jackpotting barcodes for each sample.");
            int deduplCount = 0;
            outputCountDictionary = new Dictionary<(int, int), Dictionary<string, int>>();
            foreach (string s in sampleIdList)
            {
                SendOutputText(s, newLine:false);

                Dictionary<(int, int), HashSet<string>> barcodeSetDict = new Dictionary<(int, int), HashSet<string>>();
                using (StreamReader sampleReader = new StreamReader(sampleFilePaths[s]))
                {
                    while (true)
                    {
                        string line = sampleReader.ReadLine();
                        if (line is null) break;

                        string[] splitLine = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        int forBarcodeId = int.Parse(splitLine[0]);
                        int revBarcodeId = int.Parse(splitLine[1]);

                        HashSet<string> sampleIdPlusUmiSet;
                        if (barcodeSetDict.TryGetValue((forBarcodeId, revBarcodeId), out sampleIdPlusUmiSet))
                        {
                            if (sampleIdPlusUmiSet.Add(splitLine[2])) deduplCount++;
                        }
                        else
                        {
                            sampleIdPlusUmiSet = new HashSet<string>();
                            sampleIdPlusUmiSet.Add(splitLine[2]);
                            barcodeSetDict[(forBarcodeId, revBarcodeId)] = sampleIdPlusUmiSet;
                            deduplCount++;
                        }
                    }
                }
                SendOutputText("... ", newLine: false);

                //Convert to counts and store in outputCountDictionary:
                foreach (var entry in barcodeSetDict)
                {
                    var key = entry.Key;
                    var list = entry.Value.ToList();

                    Dictionary<string, int> countDictionary;
                    if (outputCountDictionary.TryGetValue(key, out countDictionary))
                    {
                        countDictionary[s] = list.Count;
                    }
                    else
                    {
                        countDictionary = new Dictionary<string, int>();
                        countDictionary[s] = list.Count;
                        outputCountDictionary[key] = countDictionary;
                    }
                }
            }
            SendOutputText(logFileWriter, $"{DateTime.Now}; Finished counting and de-jackpotting barcodes for each sample.");
            SendOutputText(logFileWriter);


            SendOutputText(logFileWriter, $"Number of forward-reverse lineage tag pairs before PCR jackpot correction: {count}");
            double deduplePerc = 100.0 * ((double)deduplCount) / ((double)(count));
            SendOutputText(logFileWriter, $"Number of forward-reverse lineage tag pairs after PCR jackpot correction:  {deduplCount} ({deduplePerc:0.##}%)");
            SendOutputText(logFileWriter, $"Number of distinct double barcode cluster IDs: {outputCountDictionary.Count}");
            SendOutputText(logFileWriter, $"Number of forward-reverse lineage tag pairs not found in look-up dictionary (frequency below cutoff): {notFoundCount}");
            SendOutputText(logFileWriter);

            string outFileStr = $"{outputPrefix}.sorted_counts.csv";
            SendOutputText(logFileWriter, $"{DateTime.Now}; Writing barcode counts to file: {outFileStr}");
            using (StreamWriter outFileWriter = new StreamWriter(outFileStr))
            {
                // Write header to output .csv file
                string outStr = $"forward_BC, reverse_BC";
                foreach (string s in sampleIdList)
                {
                    outStr = $"{outStr}, {s}";
                }
                outStr = $"{outStr}, total_counts";
                outFileWriter.WriteLine(outStr);

                //Write line for each observed barcode pair
                //count = 0;
                foreach (var entry in outputCountDictionary)
                {
                    outStr = $"{forCenterDict[entry.Key.Item1]}, {revCenterDict[entry.Key.Item2]}";
                    var countDictionary = entry.Value;
                    int sum = 0;
                    foreach (string s in sampleIdList)
                    {
                        int num = 0;
                        countDictionary.TryGetValue(s, out num);

                        outStr = $"{outStr}, {num}";

                        sum += num;
                    }
                    outStr = $"{outStr}, {sum}";

                    outFileWriter.WriteLine(outStr);

                    //count++;

                    //if (count < 5) SendOutputText($"IDs: {entry.Key}");
                    //if (count < 5) SendOutputText($"    {outStr}");
                }
            }
                


            DateTime endTime = DateTime.Now;
            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, $"Sorting finished: {endTime}.");
            SendOutputText(logFileWriter, $"Elapsed time: {endTime - startTime}.");
            SendOutputText(logFileWriter, "*********************************************");
            SendOutputText(logFileWriter);


            logFileWriter.Close();
        }


    }
}
