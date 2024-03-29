﻿using System;
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

        public bool autoMergeSubstrings;

        public int spikeinMergeThreshold, spikeinMergeDistance;

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
            using (StreamWriter logFileWriter = new StreamWriter($"{outputPrefix}.cluster_merging.log"))
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
                SendOutputText(logFileWriter);
                SendOutputText(logFileWriter, $"Nominal barcode length: {lintagLength}");
                SendOutputText(logFileWriter);
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
                    for (int i = nominalIndex-1; i >= 0; i--)
                    {
                        indexList.Add(i);
                    }
                }

                int countsMerged = 0;
                int totalNumMerged = 0;
                foreach (int i in indexList)
                {
                    int barcodeLength = barcodeLengthList[i];
                    SendOutputText(logFileWriter);
                    SendOutputText(logFileWriter, $"{DateTime.Now}: Testing barcodes with length {barcodeLength} for merging with barcodes in cluster list");
                    var compDict = new Dictionary<int, string>(outputClusterDictionary); //copy of the current outputClusterDictionary to use for iteration
                                                                                         //Dictionary<int, string> dict = longBarcodeList[i - nominalIndex - 1];
                    Dictionary<int, string> dict = clusterCenterDictList[i]; //dictionary of cluster centers with length = barcodeLengthList[i] 
                    SendOutputText(logFileWriter, $"    Comparing {dict.Count:N0} x {compDict.Count:N0} = {dict.Count * compDict.Count:N0} barcode pairs");
                    int numMerged = 0;
                    int numAdded = 0;
                    foreach (var entry in dict)
                    {
                        string s1 = entry.Value;
                        int n1 = clusterCountDict[entry.Key];
                        bool shouldMerge = false;
                        foreach (var compEntry in compDict)
                        {
                            string s2 = compEntry.Value;
                            int n2 = clusterCountDict[compEntry.Key];
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

                            if (autoMergeSubstrings)
                            {
                                //Test if one sequence is a substring of the other
                                if (s1.Length < s2.Length)
                                {
                                    if (s2.Contains(s1))
                                    {
                                        SendOutputText(logFileWriter, $"    Substring: {entry.Key}, {s1} -> {compEntry.Key}, {s2}");
                                        SendOutputText(logFileWriter, $"        Merging, {n1:N0} + {n2:N0} = {n1 + n2:N0}");
                                        shouldMerge = true;
                                    }
                                }
                                else
                                {
                                    if (s1.Contains(s2))
                                    {
                                        SendOutputText(logFileWriter, $"    Substring: {entry.Key}, {s1} <- {compEntry.Key}, {s2}");
                                        SendOutputText(logFileWriter, $"        Merging, {n1:N0} + {n2:N0} = {n1 + n2:N0}");
                                        shouldMerge = true;
                                    }
                                }
                            }

                            if (!shouldMerge)
                            {
                                int distance = Parser.LevenshteinDistance(s1, s2);
                                distance = Math.Abs(distance);
                                //SendOutputText(logFileWriter, $"distance: {distance}");
                                double indelProb;
                                //If there is not an entry in the inDelProbArr that corresponds to this Levenschtein difference,
                                //    then don't consider merging.
                                if (inDelProbArr.Length >= distance)
                                {
                                    indelProb = inDelProbArr[distance - 1];


                                    //Test ratio vs. expected probability for merge criteria
                                    SendOutputText(logFileWriter, $"    Distance {distance}: {entry.Key}, {s1} -> {compEntry.Key}, {s2}, count ratio: {N2:N0}/{N1 + N2:N0} = {(double)N2 / (N1 + N2)}, expected error probability: {indelProb}");
                                    if ((double)N2 / (N1 + N2) <= indelProb)
                                    {
                                        SendOutputText(logFileWriter, $"        Merging, {n1:N0} + {n2:N0} = {n1 + n2:N0}");

                                        shouldMerge = true;
                                    }
                                }
                            }

                            //Note: do NOT replace next line with else:
                            if (shouldMerge)
                            {
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
                                numMerged++;
                                countsMerged += N2;
                                break; // Only allow merge into one target cluster
                            }

                        }
                        if (!shouldMerge)
                        {
                            //If the cluster doesn't get merged, then add it to outputClusterDictionary
                            outputClusterDictionary[entry.Key] = entry.Value;
                            numAdded++;
                        }
                    }
                    SendOutputText(logFileWriter, $"    Merged {numMerged:N0} barcode clusters of length {barcodeLength} into larger clusters.");
                    SendOutputText(logFileWriter, $"    Added {numAdded:N0} barcode clusters of length {barcodeLength} to output without merging.");
                    totalNumMerged += numMerged;
                }
                SendOutputText(logFileWriter);
                SendOutputText(logFileWriter, $"A total of {totalNumMerged:N0} barcode clusters with {countsMerged:N0} read counts were merged into larger clusters.");

                //Next, identify spike-in barcode clusters and merge clusters that are near them
                if (spikeinMergeDistance>0)
                {
                    SendOutputText(logFileWriter);
                    SendOutputText(logFileWriter, $"{DateTime.Now}: Testing all barcodes for merging with spike-in barcodes in cluster list");

                    //Identify IDs for spike-in barcodes with count > threshold
                    List<int> spikeinIdList = new List<int>();
                    foreach (var entry in outputClusterDictionary)
                    {
                        int bcId = entry.Key;
                        int bcCount = clusterCountDict[bcId];
                        if (bcCount > spikeinMergeThreshold)
                        {
                            spikeinIdList.Add(bcId);
                        }
                    }

                    if (spikeinIdList.Count>0)
                    {
                        int numMerged = 0;
                        countsMerged = 0;
                        foreach (int spikeinId in spikeinIdList)
                        {
                            string spikeinCenter = outputClusterDictionary[spikeinId];

                            SendOutputText(logFileWriter, $"{DateTime.Now}: Spike-In ID: {spikeinId}; Spike-In Center: {spikeinCenter}");

                            //Make new dictionary to add clusters to if they don't get merged into a spike-in cluster
                            Dictionary<int, string> newOutputClusterDictionary = new Dictionary<int, string>();

                            foreach (var entry in outputClusterDictionary)
                            {
                                int bcId = entry.Key;
                                string bcCenter = entry.Value;
                                int levDist = Parser.LevenshteinDistance(bcCenter, spikeinCenter);
                                if ((bcId != spikeinId) && (levDist <= spikeinMergeDistance))
                                {
                                    //If cluster center is within threshold Levenshtein distance, merge it with spike-in cluster
                                    int n1 = clusterCountDict[bcId]; //counts for cluster getting merged into spike-in cluster
                                    int n2 = clusterCountDict[spikeinId]; //counts for spike-in cluster

                                    SendOutputText(logFileWriter, $"    Spike-In Merge: levDist: {levDist}; {bcId}, {bcCenter} -> {spikeinId}, {spikeinCenter}");
                                    SendOutputText(logFileWriter, $"        {n1:N0} + {n2:N0} = {n1 + n2:N0}");

                                    //add clusterCount to merge target in clusterCountDict
                                    clusterCountDict[spikeinId] = n1 + n2;

                                    //change clusterIds in barcodeDictionary barcodeClusterIdDict
                                    var result = barcodeClusterIdDict.Where(x => x.Value == bcId).ToList();
                                    foreach (KeyValuePair<string, int> keyValuePair in result)
                                    {
                                        barcodeClusterIdDict[keyValuePair.Key] = spikeinId;
                                    }
                                    numMerged++;
                                    countsMerged += n1;
                                }
                                else
                                {
                                    //If not merged, add the cluster to the newOutputClusterDictionary
                                    newOutputClusterDictionary[bcId] = bcCenter;
                                }
                            }

                            outputClusterDictionary = newOutputClusterDictionary;

                        }
                    }
                    else
                    {
                        SendOutputText(logFileWriter, $"{DateTime.Now}: No spike-in barcodes found with count greater than merge threshold ({spikeinMergeThreshold})");
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
