using System;
using BarcodeParser;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace SampleSorter
{
    public class Sorter
    {
        IDisplaysOutputText outputReceiver;


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

        public static IEnumerable<string[]> GetNextLinTags(string forLinTagFile, string revLinTagFile)
        {
            Int64 count = 0;

            //Create StreamReaders from both forward and reverse lineage tag files
            using (StreamReader forwardReader = new StreamReader(forLinTagFile), reverseReader = new StreamReader(revLinTagFile))
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


    }
}
