using System;

namespace BarcodeParser
{
    public class Parser
    {
        static string write_directory; //directory where files are read and saved
        static string read_directory; //directory where files are read and saved

        public string TestParser()
        {
            return "test output";
        }

        public static string RegExStrWithOneSnip(string seq)
        {
            string regExStr = $"({seq}|";
            char[] seqChars = seq.ToCharArray();
            for (int i = 0; i < seqChars.Length; i++)
            {
                for (int j = 0; j < seqChars.Length; j++)
                {
                    if (i == j)
                    {
                        regExStr += ".";
                    }
                    else
                    {
                        regExStr += seqChars[j];
                    }
                }
                regExStr += "|";
            }

            regExStr = regExStr.Remove(regExStr.Length - 1);
            regExStr += ")";

            return regExStr;
        }
    }
}
