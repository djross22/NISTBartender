using System;
using BarcodeParser;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace SampleSorter
{
    public class Sorter
    {
        //***********************************************************************************************
        //All public fields need to be set by controlling app before SortBarcodes() is called
        //    input file strings should be set to the full path for the file

        public string forLinTagFile, revLinTagFile; //lineage tag files ourput from Parser

        public string outputPrefix; //output prefix used to automatically create output files; directory plus partial filename

        public string forBarcodeFile, revBarcodeFile; // "..._barcode.csv" files output from Clusterer; 1st column is lin-tag sequence, third column is cluster ID




        //***********************************************************************************************

        private IDisplaysOutputText outputReceiver;

        private Dictionary<string, int> forBarcodeDict, revBarcodeDict; //Dictionaries for looking up barcode cluster IDs: key = lin-tag sequence, value = barcode ID.


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
                while ((forwardReader.ReadLine() != null) & (reverseReader.ReadLine() != null))
                {
                    count += 1;

                    //Returns an array of 4 strings: forwardTag, reverseTag, samplePlusUmi, count in that order
                    string[] retString = new string[4];

                    //each line from forward and reverse lin-tag files is the lin-tag sequence and sampleID+UMI, separated by a comma
                    string forLine = forwardReader.ReadLine();
                    string revLine = reverseReader.ReadLine();

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


                    retString[3] = $"{count}";

                    yield return retString;
                }
            }
        }

        private void MakeBarcodeDictionary(bool forward)
        {
            string inFile;
            Dictionary<string, int> dict;
            if (forward)
            {
                inFile = forBarcodeFile;
                dict = forBarcodeDict;
            }
            else
            {
                inFile = revBarcodeFile;
                dict = revBarcodeDict;
            }

            dict = new Dictionary<string, int>();

            string line;
            string[] splitLine;
            //each line of input should be in the form: "{lin-tag sequence},{frequence},{cluster ID}"
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

            MakeBarcodeDictionary(forward:true);
            foreach (string key in forBarcodeDict.Keys)
            {
                SendOutputText(key);
            }


            //foreach (string[] stringArr in GetNextLinTags(forLinTagFile, revLinTagFile))
            //{

            //}


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
