using System;
using BarcodeParser;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace BarcodeClusterer
{
    public class Clusterer
    {
        IDisplaysOutputText outputReceiver;

        public string inputFile, outputPrefix;

        public int clusterCutoffFrequency, maxClusterDistance, clusterSeedLength, clusterSeedStep;

        public double clusterMergeThreshold;

        public int threadsForClustering;

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


            //ProcessStartInfo startInfo = new ProcessStartInfo();

            //startInfo.FileName = "wsl";

            //startInfo.Arguments = "bartender_single";// -c";

            //startInfo.UseShellExecute = false;
            //startInfo.RedirectStandardOutput = true;

            //clusterProcess = Process.Start(startInfo);

            //while (!clusterProcess.HasExited)
            //{
            //    SendOutputText(clusterProcess.StandardOutput.ReadToEnd());
            //    Thread.Sleep(100);
            //}



            DateTime endTime = DateTime.Now;
            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, $"Clustering finished: {endTime}.");
            SendOutputText(logFileWriter, $"Elapsed time: {endTime - startTime}.");
            SendOutputText(logFileWriter, "*********************************************");
            SendOutputText(logFileWriter);


            logFileWriter.Close();
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
