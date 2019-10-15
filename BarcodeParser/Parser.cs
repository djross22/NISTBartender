using System;
using System.Linq;

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

        public static string RemoveStringWhitespace(string input)
        {
            string output = new string(input.ToCharArray()
                .Where(c => !Char.IsWhiteSpace(c))
                .ToArray());

            output = output.Replace("\n", "");
            output = output.Replace("\r", "");

            return output;
        }

        public static string ReverseComplement(string inputSequence)
        {
            string outputString = inputSequence.TrimEnd('\r', '\n');
            outputString = Parser.RemoveStringWhitespace(outputString);

            outputString = outputString.ToLower();

            outputString = outputString.Replace('a', 'T');
            outputString = outputString.Replace('t', 'A');
            outputString = outputString.Replace('g', 'C');
            outputString = outputString.Replace('c', 'G');

            outputString = outputString.ToUpper();

            char[] charArray = outputString.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }


        public static int HammingDistance(string seq1, string seq2, int max = Int32.MaxValue, bool ignoreN = false)
        {
            //This Hamming distance, from Levy lab (mostly), included max and ignoreN parameters
            //     It assumes that seq1.Length <= seq2.Length, otherwise it returns max
            if (seq1.Equals(seq2))
            {
                return 0;
            }
            else
            {
                if (seq1.Length > seq2.Length)
                {
                    return max;
                }
                else
                {
                    int mismatches = 0;
                    if (ignoreN)
                    {
                        for (int i = 0; i < seq1.Length; i++)
                        {
                            if ((seq1[i] != 'N') && (seq2[i] != 'N'))
                            {
                                if (seq1[i] != seq2[i])
                                {
                                    mismatches += 1;
                                }
                            }
                            if (mismatches >= max)
                            {
                                break;
                            }
                        }
                        return mismatches;
                    }
                    else
                    {
                        for (int i = 0; i < seq1.Length; i++)
                        {
                            if (seq1[i] != seq2[i])
                            {
                                mismatches += 1;
                            }
                            if (mismatches >= max)
                            {
                                break;
                            }
                        }
                        return mismatches;
                    }
                }
            }
        }

        //public static int HammingDistance(string s, string t)
        //{
        //    //HammingDistance() from: https://www.csharpstar.com/csharp-string-distance-algorithm/
        //    if (s.Length != t.Length)
        //    {
        //        throw new Exception("Strings must be equal length");
        //    }

        //    int distance =
        //        s.ToCharArray()
        //        .Zip(t.ToCharArray(), (c1, c2) => new { c1, c2 })
        //        .Count(m => m.c1 != m.c2);

        //    return distance;
        //}


        public static int LevenshteinDistance(string s, string t)
        {
            //LevenshteinDistance() from: https://www.csharpstar.com/csharp-string-distance-algorithm/
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // Step 1
            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            // Step 2
            for (int i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (int j = 0; j <= m; d[0, j] = j++)
            {
            }

            // Step 3
            for (int i = 1; i <= n; i++)
            {
                //Step 4
                for (int j = 1; j <= m; j++)
                {
                    // Step 5
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                    // Step 6
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            // Step 7
            return d[n, m];
        }


        public static (string, int) BestMatchMultiTag(string m, string[] tags, int max = Int32.MaxValue, bool ignoreN = false, bool useHamming = true)
        {
            //Combines the best_match and mismatches functions
            //returns the best matching multitag and the number of mismathces
            //    m is the sequence to test
            //    tags is the array of multitags; 
            //        Note that this method uses an array input instead of a list, since the foreach loop should run slightly faster that way
            //    max sets the maximum number of mismatches before it moves on.
            //        Lowering max increases performance.
            //        If the best match has >= max mismatches, the return value for the best match tag will be an empty string
            //    IGNORE_N = true will ignore mismatches with N.
            //    useHamming controls whether the distance metric is Hamming (true) or Levenshtein (false)
            string bestMatch = "";
            int leastMismatches = max;
            int mismatches;

            //first search for exact matach
            foreach (string t in tags)
            {
                if (m.Equals(t))
                {
                    return (t, 0);
                }
                else
                {
                    if (useHamming)
                    {
                        mismatches = HammingDistance(m, t, max, ignoreN);
                    }
                    else
                    {
                        mismatches = LevenshteinDistance(m, t);
                    }
                    
                    if (mismatches < leastMismatches)
                    {
                        bestMatch = t;
                        leastMismatches = mismatches;
                    }
                }
            }

            return (bestMatch, leastMismatches);
        }


    }
}
