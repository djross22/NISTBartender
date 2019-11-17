using System;
using BarcodeParser;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace BarcodeClusterer
{
    public class Clusterer
    {
        IDisplaysOutputText outputReceiver;

        //********************************************************************************
        // Fields that need to be set before running clusterer:
        public string inputFile, outputPrefix;

        public int clusterCutoffFrequency, maxClusterDistance, clusterSeedLength, clusterSeedStep;

        public double clusterMergeThreshold;

        public int threadsForClustering;

        public int lintagLength;

        public double[] inDelProbArr;

        //********************************************************************************

        //private Process clusterProcess;

        public Clusterer(BarcodeParser.IDisplaysOutputText receiver)
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

        public void ClusterBarcodes()
        {
            //Set up log file to keep record of output text from clustering
            TextWriter logFileWriter = TextWriter.Synchronized(new StreamWriter($"{outputPrefix}.clustering.log"));

            DateTime startTime = DateTime.Now;
            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, "*********************************************");
            SendOutputText(logFileWriter, $"Running Barcode Clustering.");
            SendOutputText(logFileWriter, $"Number of threads used for clustering: {threadsForClustering}.");
            SendOutputText(logFileWriter, $"Clustering started: {startTime}.");
            SendOutputText(logFileWriter, "");

            //TODO: if seed_len < step: print("The default or specified step is larger than the seed length, reset the step be the seed length!") step = seed_len
            string bartenderArgStr = "bartender_single_com";

            // path string have to use .Replace("\\", "/")..Replace("C:/", "/mnt/c/") because the call to wsl uses Unix syntax
            bartenderArgStr += $" -f {inputFile.Replace("\\", "/").Replace("C:/", "/mnt/c/").Replace("E:/", "/mnt/e/")}"; //.Replace("\\", "/").Replace("C:/", "/mnt/c/")
            bartenderArgStr += $" -o {outputPrefix.Replace("\\", "/").Replace("C:/", "/mnt/c/").Replace("E:/", "/mnt/e/")}"; //.Replace("\\", "/").Replace("C:/", "/mnt/c/")

            bartenderArgStr += $" -c {clusterCutoffFrequency}";
            bartenderArgStr += $" -z {clusterMergeThreshold}";
            bartenderArgStr += $" -l {clusterSeedLength}";
            bartenderArgStr += $" -s {clusterSeedStep}";
            bartenderArgStr += $" -t {threadsForClustering}";
            bartenderArgStr += $" -d {maxClusterDistance}";
            //bartenderArgStr += $" 1"; //strand direction parameter possibly needed for direct call to bartender_single

            SendOutputText(logFileWriter, $"Running bartender with command string: {bartenderArgStr}.");
            SendOutputText(logFileWriter, "");

            //This part starts the wsl Bartender process (Bartender running on the Unix shell in Windows)
            using (Process clusterProcess = new Process())
            {
                clusterProcess.StartInfo.FileName = "wsl";

                clusterProcess.StartInfo.Arguments = bartenderArgStr;

                //clusterProcess.StartInfo.ArgumentList.Add("bartender_single");// -c";
                //clusterProcess.StartInfo.ArgumentList.Add("/mnt/c/Users/djross/Documents/temp/csharp_test/barcode_analysis_forward_lintags.txt");// -c";
                //clusterProcess.StartInfo.ArgumentList.Add(@"-f C:\Users\djross\Documents\temp\csharp_test\barcode_analysis_forward_lintags.txt");// -c";

                clusterProcess.StartInfo.CreateNoWindow = true;
                clusterProcess.StartInfo.UseShellExecute = false;
                clusterProcess.StartInfo.RedirectStandardOutput = true;
                clusterProcess.StartInfo.RedirectStandardError = true;

                clusterProcess.Start();

                while (!clusterProcess.HasExited)
                {
                    SendOutputText(logFileWriter, clusterProcess.StandardOutput.ReadToEnd());
                    Thread.Sleep(100);
                }

                clusterProcess.WaitForExit();
            }

            DateTime endTime = DateTime.Now;
            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, $"{endTime}: Bartender finished.");
            SendOutputText(logFileWriter, $"Elapsed time: {endTime - startTime}.");
            SendOutputText(logFileWriter, "*********************************************");
            SendOutputText(logFileWriter);


            logFileWriter.Close();
        }

        public void MergeDifferentLengths()
        {
            using (StreamWriter logFileWriter = File.AppendText($"{outputPrefix}.clustering.log"))
            {
                DateTime startTime = DateTime.Now;
                SendOutputText(logFileWriter);
                SendOutputText(logFileWriter, $"{startTime}: Beginning merge barcodes of different lengths.");

                //Load in "_cluster.csv" and "_barcode.csv" files as dictionaries
                //    "_cluster.csv" = clusterId, clusterCenter, clusterScore, clusterCount 
                //        -> clusterCenterDictList List<Dictionary<int clusterId, string clusterCenter>>; one entry in list for each barcode length
                //        -> barcodeLengthList List<int>; list of barcode lengths
                //        -> clusterScoreDict Dictionary<int clusterId, string clusterScore>; 
                //        -> clusterCountDict Dictionary<int clusterId, int clusterCount>; 
                //        
                List<Dictionary<int, string>> clusterCenterDictList = new List<Dictionary<int, string>>();
                List<int> barcodeLengthList = new List<int>();
                Dictionary<int, string> clusterScoreDict = new Dictionary<int, string>();
                Dictionary<int, int> clusterCountDict = new Dictionary<int, int>();

                string clusterFile = $"{outputPrefix}_cluster.csv";
                string clusterFileHeader;
                string line;
                string[] splitLine;
                SendOutputText(logFileWriter, $"{DateTime.Now}: Loading cluster file: {clusterFile}");
                using (StreamReader reader = new StreamReader(clusterFile))
                {
                    clusterFileHeader = reader.ReadLine(); //first line of file is header.

                    Dictionary<int, string> clusterCenterDict = new Dictionary<int, string>();

                    while ((line = reader.ReadLine()) != null)
                    {
                        splitLine = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        splitLine = splitLine.Select(s => s.Trim()).ToArray();
                        int id = int.Parse(splitLine[0]);
                        string center = splitLine[1];
                        string score = splitLine[2];
                        int count = int.Parse(splitLine[3]);

                        int length = center.Length;
                        if (barcodeLengthList.Count == 0 || (length != barcodeLengthList.Last()))
                        {
                            barcodeLengthList.Add(length);
                            clusterCenterDict = new Dictionary<int, string>();
                            clusterCenterDictList.Add(clusterCenterDict);
                        }

                        clusterCenterDict[id] = center;
                        clusterScoreDict[id] = score;
                        clusterCountDict[id] = count;
                    }
                }

                //Load in "_barcode.csv" = barcodeSequence, frequency, clusterId 
                //        -> barcodeFreqDict Dictionary<string barcodeSequence, int frequency>
                //        -> barcodeClusterIdDict Dictionary<string barcodeSequence, int clusterId>
                Dictionary<string, int> barcodeFreqDict = new Dictionary<string, int>();
                Dictionary<string, int> barcodeClusterIdDict = new Dictionary<string, int>();
                string barcodeFile = $"{outputPrefix}_barcode.csv";
                string barcodeFileHeader;
                SendOutputText(logFileWriter, $"{DateTime.Now}: Loading barcode file: {barcodeFile}");
                using (StreamReader reader = new StreamReader(barcodeFile))
                {
                    barcodeFileHeader = reader.ReadLine(); //first line of file is header.
                    while ((line = reader.ReadLine()) != null)
                    {
                        splitLine = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        splitLine = splitLine.Select(s => s.Trim()).ToArray();
                        string seq = splitLine[0];
                        int freq = int.Parse(splitLine[1]);
                        int id = int.Parse(splitLine[2]);

                        barcodeFreqDict[seq] = freq;
                        barcodeClusterIdDict[seq] = id;
                    }
                }

                //Merge toward nominal barcode length

                //Add all nominal length barcodes to outputClusterDictionary
                int nominalIndex = barcodeLengthList.IndexOf(lintagLength);
                if (nominalIndex == -1)
                {
                    throw new InvalidOperationException("Zero barcode clusters with the nominal length.");
                }
                SendOutputText(logFileWriter, $"");
                SendOutputText(logFileWriter, $"Nominal barcode length: {lintagLength}");
                SendOutputText(logFileWriter, $"{DateTime.Now}: Adding barcodes to cluster list with length {lintagLength}");
                Dictionary<int, string> outputClusterDictionary = new Dictionary<int, string>(clusterCenterDictList[nominalIndex]);

                //test length+1, length+2, length+3...
                //    then test length-1, test length-2...
                //make list of index values for clusterCenterDictList/barcodeLengthList to set the order in which testing/merging is done
                List<int> indexList = new List<int>();
                if (barcodeLengthList.Count > nominalIndex + 1)
                {
                    for (int i = nominalIndex + 1; i < clusterCenterDictList.Count; i++)
                    {
                        indexList.Add(i);
                    }
                }
                if (nominalIndex > 0)
                {
                    for (int i = 0; i < nominalIndex; i++)
                    {
                        indexList.Add(i);
                    }
                }

                foreach (int i in indexList)
                {
                    int barcodeLength = barcodeLengthList[i];
                    SendOutputText(logFileWriter, $"{DateTime.Now}: Testing barcodes with length {barcodeLength} for merging with barcodes in cluster list");
                    var compDict = new Dictionary<int, string>(outputClusterDictionary); //copy of the current outputClusterDictionary to use for iteration
                                                                                         //Dictionary<int, string> dict = longBarcodeList[i - nominalIndex - 1];
                    Dictionary<int, string> dict = clusterCenterDictList[i]; //dictionary of cluster centers with length = barcodeLengthList[i] 
                    SendOutputText(logFileWriter, $"Comparing {dict.Count} x {compDict.Count} = {dict.Count * compDict.Count} barcode pairs");
                    foreach (var entry in dict)
                    {
                        string s1 = entry.Value;
                        int n1 = clusterCountDict[entry.Key];
                        bool wasMerged = false;
                        foreach (var compEntry in compDict)
                        {
                            string s2 = compEntry.Value;
                            int n2 = clusterCountDict[compEntry.Key];

                            int distance = Parser.LevenshteinDistance(s1, s2);
                            distance = Math.Abs(distance);
                            //SendOutputText(logFileWriter, $"distance: {distance}");
                            double indelProb;
                            //If there is not an entry in the inDelProbArr that corresponds to this Levenschtein difference,
                            //    then don't consider merging.
                            if (inDelProbArr.Length >= distance)
                            {
                                indelProb = inDelProbArr[distance - 1];

                                int N1, N2;
                                if (n1 > n2)
                                {
                                    N1 = n1;
                                    N2 = n2;
                                }
                                else
                                {
                                    N1 = n2;
                                    N2 = n1;
                                }
                                SendOutputText(logFileWriter);
                                SendOutputText(logFileWriter, $"    BayesMergeRatio({indelProb}, {N1}, {N2}) = {BayesMergeRatio(indelProb, N1, N2)}");
                                if (BayesMergeRatio(indelProb, N1, N2) > 0)
                                {
                                    SendOutputText(logFileWriter, $"    Merging {entry.Key}, {entry.Value} into {compEntry.Key}, {compEntry.Value}.");
                                    SendOutputText(logFileWriter, $"    {n1} + {n2} = {n1 + n2}");
                                    //if merge:
                                    //    add clusterCount to merge target in clusterCountDict
                                    //    change clusterIds in barcodeDictionary (barcodeClusterIdDict?)
                                    //barcodeClusterIdDict
                                    clusterCountDict[compEntry.Key] = n1 + n2;
                                    //not sure if I need this: clusterCountDict[entry.Key] = 0;
                                    var result = barcodeClusterIdDict.Where(x => x.Value == entry.Key).ToList();
                                    foreach (KeyValuePair<string, int> keyValuePair in result)
                                    {
                                        barcodeClusterIdDict[keyValuePair.Key] = compEntry.Key;
                                    }
                                    wasMerged = true;
                                    break; // Only allow merge into one target cluster
                                }
                            }

                        }
                        if (!wasMerged)
                        {
                            //If the cluster doesn't get merged, then add it to outputClusterDictionary
                            outputClusterDictionary[entry.Key] = entry.Value;
                        }
                    }
                }

                //Save outputClusterDictionary, clusterScoreDict, clusterCountDict -> "_merged_cluster.csv"
                string mergedClusterFile = $"{outputPrefix}_merged_cluster.csv";
                using (StreamWriter outFileWriter = new StreamWriter(mergedClusterFile))
                {
                    //write header line (copied from "_cluster.csv" file
                    outFileWriter.WriteLine(clusterFileHeader);
                    foreach (var entry in outputClusterDictionary)
                    {
                        int bcId = entry.Key;
                        string outStr = $"{bcId},{entry.Value},{clusterScoreDict[bcId]},{clusterCountDict[bcId]}";
                        outFileWriter.WriteLine(outStr);
                    }
                }
                //Save barcodeFreqDict, barcodeClusterIdDict -> "_merged_barcode.csv"
                string mergedBarcodeFile = $"{outputPrefix}_merged_barcode.csv";
                using (StreamWriter outFileWriter = new StreamWriter(mergedBarcodeFile))
                {
                    //write header line (copied from "_barcode.csv" file
                    outFileWriter.WriteLine(barcodeFileHeader);
                    foreach (var entry in barcodeClusterIdDict)
                    {
                        int bcId = entry.Value;
                        string bcSeq = entry.Key;
                        int bcFreq = barcodeFreqDict[bcSeq];
                        string outStr = $"{bcSeq},{bcFreq},{bcId}";
                        outFileWriter.WriteLine(outStr);
                    }
                }


                DateTime endTime = DateTime.Now;
                SendOutputText(logFileWriter);
                SendOutputText(logFileWriter, $"{DateTime.Now}: Finished merge barcodes of different lengths.");
                SendOutputText(logFileWriter, $"Elapsed time: {endTime - startTime}.");
                SendOutputText(logFileWriter, "*********************************************");
                SendOutputText(logFileWriter);
            }

        }

        public double BayesMergeRatio(double p, int N1, int N2)
        {
            //p is the probability of seeing a read error with a given distance from the correct sequence
            //N1 is the number of reads in the largere cluster
            //N2 is the number of reads in the smaller cluster
            if (p == 0)
            {
                return 0;
            }
            else
            {
                double logLikeRatioAprox = 1 + Math.Log(N1 + N2) + Math.Log(p) - Math.Log(N2) - 1 / 2 * Math.Log(N2 * 6.28) / N2;
                return logLikeRatioAprox;
            }
        }

    }
}
