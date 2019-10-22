using System;
using BarcodeParser;
using System.IO;

namespace BarcodeClusterer
{
    public class Clusterer
    {
        IDisplaysOutputText outputReceiver;

        public string inputFile, outputPrefix;

        public int clusterCutoffFrequency, maxClusterDistance, clusterSeedLength, clusterSeedStep;

        public double clusterMergeThreshold;

        public int clusteringThreads;

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
        }
    }
}
