﻿using System;
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


            string bartenderArgStr = "bartender_single_com -f /mnt/c/Users/djross/Documents/temp/csharp_test/barcode_analysis_forward_lintags.txt";
            bartenderArgStr += " -o /mnt/c/Users/djross/Documents/temp/csharp_test/forward";

            //This part starts the wsl Bartender process (Bartender running on the Unix shell in Windows)
            using (Process clusterProcess = new Process())
            {
                clusterProcess.StartInfo.FileName = "wsl";

                clusterProcess.StartInfo.Arguments = bartenderArgStr;

                //clusterProcess.StartInfo.ArgumentList.Add("bartender_single");// -c";
                //clusterProcess.StartInfo.ArgumentList.Add("/mnt/c/Users/djross/Documents/temp/csharp_test/barcode_analysis_forward_lintags.txt");// -c";
                //clusterProcess.StartInfo.ArgumentList.Add(@"-f C:\Users\djross\Documents\temp\csharp_test\barcode_analysis_forward_lintags.txt");// -c";

                clusterProcess.StartInfo.UseShellExecute = false;
                clusterProcess.StartInfo.RedirectStandardOutput = true;
                clusterProcess.StartInfo.RedirectStandardError = true;

                clusterProcess.Start();

                while (!clusterProcess.HasExited)
                {
                    SendOutputText(clusterProcess.StandardOutput.ReadToEnd());
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
    }
}