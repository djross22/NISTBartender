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
            bartenderArgStr += $" -f {inputFile.Replace("\\", "/").Replace("C:/", "/mnt/c/")}"; //.Replace("\\", "/").Replace("C:/", "/mnt/c/")
            bartenderArgStr += $" -o {outputPrefix.Replace("\\", "/").Replace("C:/", "/mnt/c/")}"; //.Replace("\\", "/").Replace("C:/", "/mnt/c/")

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
            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, $"{DateTime.Now}: Bartender finished.");


            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, $"{DateTime.Now}: Beginning merge barcodes of different lengths.");

            MergeDifferentLengths();

            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, $"{DateTime.Now}: Finished merge barcodes of different lengths.");


            DateTime endTime = DateTime.Now;
            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, $"Clustering finished: {endTime}.");
            SendOutputText(logFileWriter, $"Elapsed time: {endTime - startTime}.");
            SendOutputText(logFileWriter, "*********************************************");
            SendOutputText(logFileWriter);


            logFileWriter.Close();
        }

        private void MergeDifferentLengths()
        {
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
            string line;
            string[] splitLine;
            using (StreamReader reader = new StreamReader(clusterFile))
            {
                reader.ReadLine(); //first line of file is header, so read it but don't do anything with it.

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
                    if (barcodeLengthList.Count==0 || (length != barcodeLengthList.Last()))
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

            //Load in "_barcode.csv" = barcodeRead, frequency, clusterId 
            //        -> barcodeFreqDict Dictionary<string barcodeSequence, int frequency>
            //        -> barcodeClusterIdDict Dictionary<string barcodeSequence, int clusterId>
            Dictionary<string, int> barcodeFreqDict = new Dictionary<string, int>();
            Dictionary<string, int> barcodeClusterIdDict = new Dictionary<string, int>();
            string barcodeFile = $"{outputPrefix}_barcode.csv";
            using (StreamReader reader = new StreamReader(barcodeFile))
            {
                reader.ReadLine(); //first line of file is header, so read it but don't do anything with it.
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
            Dictionary<int, string> outputClusterDictionary = new Dictionary<int, string>(clusterCenterDictList[nominalIndex]);

            //test length+1, length+2, length+3...:
            if (barcodeLengthList.Count > nominalIndex + 1)
            {
                List<Dictionary<int, string>> longBarcodeList = clusterCenterDictList.GetRange(nominalIndex + 1, barcodeLengthList.Count - nominalIndex - 1);
                for (int i = nominalIndex + 1; i< clusterCenterDictList.Count; i++)
                //foreach (Dictionary<int, string> dict in longBarcodeList)
                {
                    var compDict = new Dictionary<int, string>(outputClusterDictionary);
                    Dictionary<int, string> dict = longBarcodeList[i];
                    foreach (var entry in dict)
                    {
                        string s1 = entry.Value;
                        int n1 = clusterCountDict[entry.Key];
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
                            if (BayesMergeRatio(double p, N1, N2)>1)
                            {
                                //if merge:
                                //    add clusterCount to merge target in clusterCountDict
                                //    change clusterIds in barcodeDictionary
                                //barcodeClusterIdDict
                                var result = barcodeClusterIdDict.Where(x => x.Value == entry.Key).ToList();
                                result.
                                //var result = Enumerable.Range(0, barcodeClusterIdDict.Count).Where(i => lst1[i] == "a").ToList();
                            }
                            else
                            {
                                //else:
                                //    add new item to outputClusterDictionary
                            }
                        }
                    }
                }
            }
            

            //test length-1, test length-2...

            //Save outputClusterDictionary, clusterScoreDict, clusterCountDict -> "_merged_cluster.csv"
            //Save barcodeFreqDict, barcodeClusterIdDict -> "_merged_barcode.csv"
        }

        public double BayesMergeRatio(double p, int N1, int N2)
        {
            //p is the probability of seeing a read error with a given distance from the correct sequence
            //N1 is the number of reads in the largere cluster
            //N2 is the number of reads in the smaller cluster
            double logLikeRatioAprox = 1 + Math.Log(N1 + N2) + Math.Log(p) - Math.Log(N2) - 1 / 2 * Math.Log(N2 * 6.28) / N2;
            return logLikeRatioAprox;
        }

    }
}
