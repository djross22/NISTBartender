using System;
using BarcodeParser;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;
using static BarcodeParser.Parser;
using System.Collections;
using System.Collections.Frozen;
using System.Data;
using System.Xml;
using System.Diagnostics.Metrics;

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

        private void AddClusterCountForId(DataFrame editDF, long bcId, long addBCCount)
        {
            int foundIdCount = 0;

            foreach (var changeRow in editDF.Rows)
            {
                if ((long)changeRow["Cluster.ID"] == bcId)
                {
                    changeRow["time_point_1"] = (long)changeRow["time_point_1"] + addBCCount;
                    foundIdCount++;
                }
            }

            if (foundIdCount > 1)
            {
                throw new Exception($"editDF has more than one row with Cluster.ID {bcId}");
            }
            else if (foundIdCount < 1)
            {
                throw new Exception($"editDF has zero rows with Cluster.ID {bcId}");
            }
        }
        public void MergeDifferentLengths()
        {
            using (StreamWriter logFileWriter = new StreamWriter($"{outputPrefix}.cluster_merging.log"))
            {
                DateTime startTime = DateTime.Now;
                SendOutputText(logFileWriter);
                SendOutputText(logFileWriter, $"{startTime}: Beginning merge barcodes of different lengths.");

                //Load in "_cluster.csv" as DataFrame; which contains the set of barcode centers/clusters
                //    "_cluster.csv" columns:
                //        Cluster.ID - long, the cluster id assigned by Bartender
                //        Center - string, the sequence of the cluster center
                //        Cluster.Score - float, the score assigned by Bartender
                //        time_point_1 - long, the read count for the cluster
                //        
                string clusterFile = $"{outputPrefix}_cluster.csv";
                SendOutputText(logFileWriter, $"{DateTime.Now}: Loading cluster file: {clusterFile}");
                //Type[] columnTypes = new Type[] { typeof(long), typeof(string), typeof(float), typeof(long) };
                Type[] columnTypes = new Type[] { typeof(long), typeof(string), typeof(string), typeof(long) };
                DataFrame clusterDF = DataFrame.LoadCsv(clusterFile, dataTypes: columnTypes); // DataFrame with all barcode centers

                List<int> barcodeLengths = new List<int>();
                foreach (string? entry in clusterDF["Center"])
                {
                    barcodeLengths.Add(entry?.Length ?? 0); // Handle null entries by assigning a length of 0
                }
                PrimitiveDataFrameColumn<int> lenCol = new("barcodeLength", barcodeLengths);
                clusterDF.Columns.Add(lenCol);

                // group clusterDF by barcodeLength
                var clusterDfGroups = clusterDF.GroupBy<int>("barcodeLength");
                Dictionary<int, DataFrame> clusterCenterDfDict = new Dictionary<int, DataFrame>();

                foreach (var group in clusterDfGroups.Groupings)
                {
                    clusterCenterDfDict[group.Key] = clusterDF.Filter(clusterDF["barcodeLength"].ElementwiseEquals(group.Key));
                }

                //int minBCLength = barcodeLengths.Min();
                int minBCLength = barcodeLengths.Where(x => x != 0).Min();
                int maxBCLength = barcodeLengths.Max();


                //Load in "_barcode.csv" = barcodeSequence, frequency, Cluster.ID; which contains the set of unique barcode reads
                //    "_barcode.csv" = Unique.reads, Frequency, Cluster.ID
                string barcodeFile = $"{outputPrefix}_barcode.csv";
                SendOutputText(logFileWriter, $"{DateTime.Now}: Loading barcode file: {barcodeFile}");
                Type[] barcodeColumnTypes = new Type[] { typeof(string), typeof(long), typeof(long) };
                DataFrame barcodeDF = DataFrame.LoadCsv(barcodeFile, dataTypes:barcodeColumnTypes);


                //Merge toward nominal barcode length

                //First, add all nominal length barcodes to outputClusterDictionary
                DataFrame outputClusterDF = clusterCenterDfDict[lintagLength].Clone();
                if (outputClusterDF.Rows.Count == 0)
                {
                    throw new InvalidOperationException("Zero barcode clusters with the nominal length.");
                }
                SendOutputText(logFileWriter);
                SendOutputText(logFileWriter, $"Nominal barcode length: {lintagLength}");
                SendOutputText(logFileWriter);
                SendOutputText(logFileWriter, $"{DateTime.Now}: Adding barcodes to cluster list with length {lintagLength}");

                //Test for merging in this order: lintagLength+1, lintagLength+2, lintagLength+3...
                //    then test lintagLength-1, lintagLength-2...
                //To set that up, make list of barcode lengths (!= lintagLength) in the order that they should be merged
                //    into the larger set of barcodes:
                List<int> orderedLengthList = Enumerable.Range(lintagLength + 1, maxBCLength - lintagLength + 2).ToList();
                List<int> orderedLengthListLo = Enumerable.Range(minBCLength, lintagLength - minBCLength).ToList();
                orderedLengthList.AddRange(orderedLengthListLo);

                long countsMerged = 0;
                long totalNumMerged = 0;
                foreach (int barcodeLength in orderedLengthList)
                {
                    if (clusterCenterDfDict.ContainsKey(barcodeLength))
                    {
                        SendOutputText(logFileWriter);
                        SendOutputText(logFileWriter, $"{DateTime.Now}: Testing barcodes with length {barcodeLength} for merging with barcodes in cluster list");

                        //copy of the current outputClusterDF to use for iteration while adding new elements to outputClusterDF
                        //var compDF = outputClusterDF.Clone();

                        DataFrame df = clusterCenterDfDict[barcodeLength]; //dataframe of cluster centers with length = barcodeLength
                        SendOutputText(logFileWriter, $"    Comparing {df.Rows.Count:N0} x {outputClusterDF.Rows.Count:N0} = {df.Rows.Count * outputClusterDF.Rows.Count:N0} barcode pairs");
                        int numMerged = 0;
                        int numAdded = 0;

                        foreach (var row in df.Rows)
                        {
                            // colums in the dataframe: Cluster.ID, Center, Cluster.Score, time_point_1, barcodeLength
                            string s1 = (string)row["Center"];
                            long n1 = (long)row["time_point_1"];
                            long id1 = (long)row["Cluster.ID"];
                            bool shouldMerge = false;
                            foreach (var compRow in outputClusterDF.Rows)
                            {
                                string s2 = (string)compRow["Center"];
                                long n2 = (long)compRow["time_point_1"];
                                long id2 = (long)compRow["Cluster.ID"];

                                long N1, N2;
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
                                            SendOutputText(logFileWriter, $"    Substring: {id1}, {s1} -> {id2}, {s2}");
                                            SendOutputText(logFileWriter, $"        Merging, {n1:N0} + {n2:N0} = {n1 + n2:N0}");
                                            shouldMerge = true;
                                        }
                                    }
                                    else
                                    {
                                        if (s1.Contains(s2))
                                        {
                                            SendOutputText(logFileWriter, $"    Substring: {id1}, {s1} <- {id2}, {s2}");
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
                                        SendOutputText(logFileWriter, $"    Distance {distance}: {id1}, {s1} -> {id2}, {s2}, count ratio: {N2:N0}/{N1 + N2:N0} = {(double)N2 / (N1 + N2)}, expected error probability: {indelProb}");
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
                                    //    add time_point_1 (n1) to merge target in outputClusterDF
                                    //    change clusterIds in barcodeDF
                                    AddClusterCountForId(outputClusterDF, id2, n1);

                                    var changebarcodeIDRows = barcodeDF.Filter(barcodeDF["Cluster.ID"].ElementwiseEquals(id1));
                                    foreach (var changeRow in changebarcodeIDRows.Rows)
                                    {
                                        changeRow["Cluster.ID"] = id2;
                                    }
                                    numMerged++;
                                    countsMerged += N2;
                                    break; // Only allow merge into one target cluster
                                }

                            }
                            if (!shouldMerge)
                            {
                                //If the cluster doesn't get merged, then add its row to outputClusterDF
                                outputClusterDF.Append(row);
                                numAdded++;
                            }
                        }
                        SendOutputText(logFileWriter, $"    Merged {numMerged:N0} barcode clusters of length {barcodeLength} into larger clusters.");
                        SendOutputText(logFileWriter, $"    Added {numAdded:N0} barcode clusters of length {barcodeLength} to output without merging.");
                        totalNumMerged += numMerged;

                    }
                    
                }
                SendOutputText(logFileWriter);
                SendOutputText(logFileWriter, $"A total of {totalNumMerged:N0} barcode clusters with {countsMerged:N0} read counts were merged into larger clusters.");

                //Next, identify spike-in barcode clusters and merge clusters that are near them
                if (spikeinMergeDistance>0)
                {
                    SendOutputText(logFileWriter);
                    SendOutputText(logFileWriter, $"{DateTime.Now}: Testing all barcodes for merging with spike-in barcodes in cluster list");

                    //Get rows of outputClusterDF with spike-in barcodes: with time_point_1 > spikeinMergeThreshold
                    var spikeInDF = outputClusterDF.Filter(outputClusterDF["time_point_1"].ElementwiseGreaterThan(spikeinMergeThreshold));
                    var nonSpikeInDF = outputClusterDF.Filter(outputClusterDF["time_point_1"].ElementwiseLessThanOrEqual(spikeinMergeThreshold));

                    //if (spikeInDF.Rows.Count>0)
                    //{
                    //    // TODO: re-code this section to be more like the previous merging;
                    //    //     or just move the spike-in merging into that section
                    //    foreach (var spikeinRow in spikeInDF.Rows)
                    //    {
                    //        // Output the set of spike-ins that were found.
                    //        string spikeinCenter = (string)spikeinRow["Center"];
                    //        long spikeinId = (long)spikeinRow["Cluster.ID"];
                    //        SendOutputText(logFileWriter, $"{DateTime.Now}: Spike-In ID: {spikeinId}; Spike-In Center: {spikeinCenter}");
                    //    }

                    //    //Make new DataFrame to add clusters to if they don't get merged into a spike-in cluster
                    //    // Start with spike-ins already included
                    //    DataFrame newOutputClusterDF = spikeInDF.Clone();

                    //    int numMerged = 0;
                    //    countsMerged = 0;
                    //    foreach (var row in nonSpikeInDF.Rows)
                    //    {
                    //        long bcId = (long)row["Cluster.ID"];
                    //        string bcCenter = (string)row["Center"];
                    //        long bcCount = (long)row["time_point_1"]; //counts for cluster that might be merged into spike-in cluster

                    //        foreach (var spikeinRow in spikeInDF.Rows)
                    //        {
                    //            string spikeinCenter = (string)spikeinRow["Center"];
                    //            long spikeinCount = (long)spikeinRow["time_point_1"];
                    //            long spikeinId = (long)spikeinRow["Cluster.ID"];

                    //            int levDist = Parser.LevenshteinDistance(bcCenter, spikeinCenter);
                    //            if ((bcId != spikeinId) && (levDist <= spikeinMergeDistance))
                    //            {
                    //                //If cluster center is within threshold Levenshtein distance, merge it with spike-in cluster

                    //                SendOutputText(logFileWriter, $"    Spike-In Merge: levDist: {levDist}; {bcId}, {bcCenter} -> {spikeinId}, {spikeinCenter}");
                    //                SendOutputText(logFileWriter, $"        {bcCount:N0} + {spikeinCount:N0} = {bcCount + spikeinCount:N0}");

                    //                //add time_point_1 to merge target in outputClusterDF
                    //                AddClusterCountForId(newOutputClusterDF, spikeinId, bcCount);

                    //                //change clusterIds in barcodeDF
                    //                var changebarcodeIDRows = barcodeDF.Filter(barcodeDF["Cluster.ID"].ElementwiseEquals(bcId));
                    //                foreach (var changeRow in changebarcodeIDRows.Rows)
                    //                {
                    //                    changeRow["Cluster.ID"] = spikeinId;
                    //                }

                    //                numMerged++;
                    //                countsMerged += bcCount;
                    //            }
                    //            else
                    //            {
                    //                //If not merged, add the cluster to the newOutputClusterDF
                    //                newOutputClusterDF.Append(row);
                    //            }
                    //        }

                    //    }

                    //    outputClusterDF = newOutputClusterDF;
                    //}
                    //else
                    //{
                    //    SendOutputText(logFileWriter, $"{DateTime.Now}: No spike-in barcodes found with count greater than merge threshold ({spikeinMergeThreshold})");
                    //}
                }

                //Save outputClusterDF -> "_merged_cluster.csv"
                //    only save with original columns: Cluster.ID, Center, Cluster.Score, time_point_1
                List<DataFrameColumn> selectedColumns = new List<DataFrameColumn>();
                List<string> columnNamesToSelect = new List<string> { "Cluster.ID", "Center", "Cluster.Score", "time_point_1" }; // Replace with your desired column names

                foreach (string columnName in columnNamesToSelect)
                {
                    selectedColumns.Add(outputClusterDF.Columns[columnName]);
                }
                DataFrame saveClusterDF = new DataFrame(selectedColumns);

                string mergedClusterFile = $"{outputPrefix}_merged_cluster.csv";
                DataFrame.SaveCsv(saveClusterDF, mergedClusterFile);

                //Save barcodeDF -> "_merged_barcode.csv"
                string mergedBarcodeFile = $"{outputPrefix}_merged_barcode.csv";
                DataFrame.SaveCsv(barcodeDF, mergedBarcodeFile);

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
