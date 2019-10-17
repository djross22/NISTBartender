using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace BarcodeParser
{
    public class Parser
    {
        IDisplaysOutputText outputReceiver;
        public string write_directory; //directory where files are read and saved
        public string read_directory; //directory where files are read and saved

        public string f_gzipped_fastqfile; //The forward reads, gzipped fastq file
        public string r_gzipped_fastqfile; //The reverse reads, gzipped fastq file

        public List<string> forMultiTagList; //List of forward multiplexing tags

        //Length arrays
        public int[] forUmiTagLen, revUmiTagLen; //range of possible UMI tag lengths
        public int[] forMultiTagLen, revMultiTagLen; //range of possible Multi-tag lengths
        public int[] forSpacerLength, revSpacerLength; //range of possible spacer lengths
        public int[] forLinTagLength, revLinTagLength; //range of possible lineage tag lengths
        public int multiFlankLength; //length of flanking regions around multi-tags
        public int linTagFlankLength; //length of flanking regions around lineage tags

        //lock for multi-thread file reading
        private static readonly Object file_lock = new Object();

        
        public int ParsingThreads { get; set; } //number of threads to use for parsing


        static double min_qs = 30; //the minimum avareage quality score for both lineage tags


        static string lintag_grep_filter1 = @"(.ACC|T.CC|TA.C|TAC.).{4,7}?AA.{4,7}?TT.{4,7}?TT.{4,7}?(.TAA|A.AA|AT.A|ATA.)"; //first barcode
        static string lintag_grep_filter2 = @"(.ACC|T.CC|TA.C|TAC.).{4,7}?AA.{4,7}?AA.{4,7}?TT.{4,7}?(.TAC|T.AC|TT.C|TTA.)"; //second barcode
        Regex forwardLinTagRegex;
        Regex reverseLinTagRegex;


        //Consider changing the way these are used/defined
        static int f_seqtag_length = 8; //the length of the sequencing tag on the first read (UMI1)
        static int r_seqtag_length = 8; //the length of the sequencing tag on the second read (UMI2)

        static int f_multitag_length = 6; //the length of the multiplexing tag on the first read
        static int f_multitag_short = 0; //the length of the shorter multiplexing tags on the first read
        static int r_multitag_length = 6; //the length of the multiplexing tag on the second read

        static int f_lintag_length = 38; //the length of the lineage tag on the first read (first barcode)
        static int r_lintag_length = 38; //the length of the lineage tag on the second read (second barcode)

        static int f_spacer_length = 43; //distance to first barcode in forward read, not including the multitag and the seqtag
        static int r_spacer_length = 29; //distance second barcode in reverse read, not including the multitag and the seqtag

        static bool clip_ends = true; //logical of whether or not to clip the front and back ends off of lintag1 and lintag2
        static int clipper_length = 4; //length of clippers

        static string lintag1_front_clipper = "(.ACC|T.CC|TA.C|TAC.)"; //only report lintag1 after this sequence
        static string lintag2_front_clipper = "(.ACC|T.CC|TA.C|TAC.)"; //only report lintag2 after this sequence
        static string lintag1_rear_clipper = "(.ATA|A.TA|AA.A|AAT.)"; //only report lintag1 before this sequence, this must be the REVERSE of the true sequence
        static string lintag2_rear_clipper = "(.ATT|C.TT|CA.T|CAT.)"; //only report lintag2 before this sequence, this must be the REVERSE of the true sequence

        Regex forwardFrontClipperRegex;
        Regex reverseFrontClipperRegex;
        Regex forwardRearClipperRegex;
        Regex reverseRearClipperRegex;

        static string[] multitags = new string[2] { "TTCGGTGCTTAA", "CGCATACACCGA" }; //concatenated multiplexing tags from the first and second reads that uniquely identify a sample, currently must have 2 or more multitags

        static List<string> short_f_list;
        static List<string> long_f_list;

        public Parser(IDisplaysOutputText receiver)
        {
            outputReceiver = receiver;

            forwardLinTagRegex = new Regex(lintag_grep_filter1, RegexOptions.Compiled);
            reverseLinTagRegex = new Regex(lintag_grep_filter2, RegexOptions.Compiled);

            forwardFrontClipperRegex = new Regex(lintag1_front_clipper, RegexOptions.Compiled);
            reverseFrontClipperRegex = new Regex(lintag2_front_clipper, RegexOptions.Compiled);
            forwardRearClipperRegex = new Regex(lintag1_rear_clipper, RegexOptions.Compiled);
            reverseRearClipperRegex = new Regex(lintag2_rear_clipper, RegexOptions.Compiled);

            short_f_list = new List<string>();
            long_f_list = new List<string>();
        }

        private void SendOutputText(string text, bool newLine = true)
        {
            outputReceiver.DisplayOutput(text, newLine);
        }

        private void SendOutputText()
        {
            SendOutputText("");
        }

        public void ParseDoubleBarcodes()
        {
            DateTime startTime = DateTime.Now;
            SendOutputText();
            SendOutputText("*********************************************");
            SendOutputText("Running Parser for Double Barcodes.");
            SendOutputText($"Parser started: {startTime}.");
            SendOutputText("    Forward Multiplexing Tags:");
            foreach (string i in forMultiTagList)
            {
                SendOutputText($"        {i}, ");
            }
            SendOutputText("    Reverse Multiplexing Tags:");
            foreach (string i in revMultiTagList)
            {
                SendOutputText($"        {i}, ");
            }
            SendOutputText();
            
            //open files for writing
            //  lineage tags for reads that sort to a multiplexing tag, these files are for input into clustering method
            TextWriter forwardWriter = TextWriter.Synchronized(new StreamWriter($"forward_lintags.txt"));
            TextWriter reverseWriter = TextWriter.Synchronized(new StreamWriter($"reverse_lintags.txt"));
            //  actual multi-plexing tag sequencess for reads that sort to a multiplexing tag, this files is for debugging
            TextWriter multiTagWriter = TextWriter.Synchronized(new StreamWriter($"multiplexing_tags.txt"));
            //   reads that don't sort to a multiplexing tag or don't match the lineage tag RegEx, this file for debugging 
            TextWriter unmatchedWriter = TextWriter.Synchronized(new StreamWriter($"unmatched_sequences.txt"));

            //Maximum useful sequence read length based on input settings
            int maxForSeqLength = forUmiTagLen.Last() + forMultiTagLen.Last() + forSpacerLength.Last() + forLinTagLength.Last() + linTagFlankLength;

            //Minimum length of sequence before Lineage tag flanking sequence
            int minPreLinFlankLength = forUmiTagLen.First() + forMultiTagLen.First() + forSpacerLength.First() - linTagFlankLength;

            // Keep track of how many reads pass each check
            int quality_reads = 0;
            int total_reads = 0;
            int grep_matching_quality_reads = 0;
            int passing_reads_that_dont_match_a_multitag = 0;
            int grep_failures = 0;



            //while ( ((f_id = f_file.ReadLine()) != null) & ((r_id = r_file.ReadLine()) != null) )
            //foreach (string[] stringArr in GetNextSequences(f_fastqfile, r_fastqfile))
            //Parallel.ForEach(GetNextSequences(f_fastqfile, r_fastqfile), stringArr =>
            //foreach (string[] stringArr in GetNextSequencesFromGZip($"{inDir}{f_gzipped_fastqfile}", $"{inDir}{r_gzipped_fastqfile}"))
            //Parallel.ForEach(GetNextSequencesFromGZip($"{inDir}{f_gzipped_fastqfile}", $"{inDir}{r_gzipped_fastqfile}"), stringArr =>
            Parallel.ForEach(GetNextSequencesFromGZip($"{read_directory}\\{f_gzipped_fastqfile}", $"{read_directory}\\{r_gzipped_fastqfile}"), new ParallelOptions { MaxDegreeOfParallelism = 1 }, stringArr =>
            {
                string counter = stringArr[4];

                int quality_readsAdd = 0;
                
                //string f_line, r_line;
                string f_seq, f_qual, f_tag;
                string r_seq, r_qual, r_tag;
                string best;
                int num_mis;
                int fstart, rstart;
                int fend, rend;

                string combinedMultiTag; //concatintated multiplexing tag

                bool use_short_tag = false;
                string f_tag_seq, f_tag_best;
                int f_tag_mismatches;
                f_seq = stringArr[0];
                r_seq = stringArr[1];
                f_qual = stringArr[2];
                r_qual = stringArr[3];


                //first check whether the forward multitag is a short multitag or a regular length multitag
                if (f_multitag_short > 0)
                {
                    f_tag_seq = f_seq.Substring(f_boundries[1], f_multitag_length);
                    //set max: 3 in the next line so that if there are >= 3 mismatches, f_tag_best will be returned as ""
                    //(f_tag_best, f_tag_mismatches) = MatchMultiTag(f_tag_seq, f_tag_arr, max: 3, ignoreN: true);
                    (f_tag_best, f_tag_mismatches) = BestMatchMultiTag(f_tag_seq, f_tag_arr, ignoreN: true);
                    if (f_tag_best != "")
                    {
                        use_short_tag = (f_tag_best[f_tag_best.Length - 1] == 'N');
                    }
                }

                if (use_short_tag)
                {
                    f_boundries_to_use = f_boundries_short;
                    f_multitag_length_to_use = f_multitag_short;
                    total_multitag_length = f_multitag_short + r_multitag_length;
                }
                else
                {
                    f_boundries_to_use = f_boundries;
                    f_multitag_length_to_use = f_multitag_length;
                    total_multitag_length = f_multitag_length + r_multitag_length;
                }

                max_multitag_mismatch = (total_multitag_length + 1) / 3;

                //checks that the quality scores of forward and reverse lintags are OK
                //foreach (int i in Quality(f_qual.Substring(f_boundries_to_use[3], f_lintag_length)))
                //{
                //    SendOutputText($"{i}, ", false);
                //}
                //SendOutputText();
                //foreach (int i in Quality(r_qual.Substring(r_boundries[3], r_lintag_length)))
                //{
                //    SendOutputText($"{i}, ", false);
                //}
                //SendOutputText();


                if ((MeanQuality(f_qual.Substring(f_boundries_to_use[3], f_lintag_length)) > min_qs) && (MeanQuality(r_qual.Substring(r_boundries[3], r_lintag_length)) > min_qs))
                {
                    quality_readsAdd = 1;

                    //SendOutputText($"{forwardLinTagRegex.IsMatch(f_seq.Substring(f_boundries_to_use[3], f_lintag_length))}");
                    //SendOutputText($"{reverseLinTagRegex.IsMatch(r_seq.Substring(r_boundries[3], r_lintag_length))}");
                    //SendOutputText();
                    //Console.ReadLine();


                    //checks that both lineage tags meet the regular expression filter
                    if (forwardLinTagRegex.IsMatch(f_seq.Substring(f_boundries_to_use[3], f_lintag_length)) && reverseLinTagRegex.IsMatch(r_seq.Substring(r_boundries[3], r_lintag_length)))
                    {
                        grep_matching_quality_reads += 1;

                        //next, find the closest matching multitag
                        combinedMultiTag = f_seq.Substring(f_boundries_to_use[1], f_multitag_length_to_use) + r_seq.Substring(r_boundries[1], r_multitag_length);
                        (best, num_mis) = BestMatchMultiTag(combinedMultiTag, multitags, max: max_multitag_mismatch, ignoreN: true);

                        if (best.Length != total_multitag_length)
                        {
                            num_mis = 1000;
                            if (best != "")
                            {
                                if (!use_short_tag)
                                {
                                    SendOutputText($"long forward tag, {combinedMultiTag}, matched to short tag sequence, {best}");
                                }
                                else
                                {
                                    //SendOutputText($"short forward tag, {combinedMultiTag}, matched to long tag sequence, {best}");
                                }
                            }
                        }

                        f_tag = f_seq.Substring(f_boundries_to_use[3], f_lintag_length);
                        r_tag = r_seq.Substring(r_boundries[3], r_lintag_length);
                        if (clip_ends)
                        {
                            fstart = forwardFrontClipperRegex.Match(f_tag).Index + clipper_length;
                            fend = f_tag.Length - forwardRearClipperRegex.Match(Reverse(f_tag)).Index - clipper_length;
                            rstart = reverseFrontClipperRegex.Match(r_tag).Index + clipper_length;
                            rend = r_tag.Length - reverseRearClipperRegex.Match(Reverse(r_tag)).Index - clipper_length;

                            f_tag = f_tag.Substring(fstart, fend - fstart);
                            r_tag = r_tag.Substring(rstart, rend - rstart);
                        }

                        //SendOutputText($"{num_mis}");
                        //SendOutputText();
                        //Console.ReadLine();
                        max_multitag_mismatch_2 = (total_multitag_length + 1) / 4;

                        lock (file_lock)
                        {
                            quality_reads += quality_readsAdd;
                            grep_failures += 1 - quality_readsAdd;

                            if (num_mis < max_multitag_mismatch_2)
                            {
                                //A multitag match has been found, so write to the appropriate multitag file
                                seq_tag_files[best].Write($"{f_seq.Substring(0, f_seqtag_length)}{r_seq.Substring(0, r_seqtag_length)}\n");
                                lin_tag_1_files[best].Write($"{f_tag}\n");
                                lin_tag_2_files[best].Write($"{r_tag}\n");
                                multi_tag_files[best].Write($"{combinedMultiTag}\n");

                                //seq_tag_files[best].Write($"{counter}, {f_seq.Substring(0, f_seqtag_length)}{r_seq.Substring(0, r_seqtag_length)}\n");
                                //lin_tag_1_files[best].Write($"{counter}, {f_tag}\n");
                                //lin_tag_2_files[best].Write($"{counter}, {r_tag}\n");
                                //multi_tag_files[best].Write($"{counter}, {combinedMultiTag}\n");
                            }
                            else
                            {
                                //Sequence did not match a multitag, so write to unmatched output files
                                passing_reads_that_dont_match_a_multitag += 1;

                                unmatched_seqtag.Write($"{f_seq.Substring(0, f_seqtag_length)}{r_seq.Substring(0, r_seqtag_length)}\n");
                                unmatched_lintag1.Write($"{f_tag}\n");
                                unmatched_lintag2.Write($"{r_tag}\n");
                                unmatched_multitag.Write($"{combinedMultiTag}\n");

                                //unmatched_seqtag.Write($"{counter}, {f_seq.Substring(0, f_seqtag_length)}{r_seq.Substring(0, r_seqtag_length)}\n");
                                //unmatched_lintag1.Write($"{counter}, {f_tag}\n");
                                //unmatched_lintag2.Write($"{counter}, {r_tag}\n");
                                //unmatched_multitag.Write($"{counter}, {combinedMultiTag}\n");
                            }

                            total_reads += 1;
                        }

                    }
                    //else
                    //{
                    //}
                }

                //test write
                //lin_tag_1_files[multitags[0]].Write($"{f_seq}\n");
                //lin_tag_2_files[multitags[0]].Write($"{r_seq}\n");
                //lin_tag_1_files[multitags[0]].Write($"{f_qual}\n");
                //lin_tag_2_files[multitags[0]].Write($"{r_qual}\n");
                //lin_tag_1_files[multitags[0]].Write($"\n");
                //lin_tag_2_files[multitags[0]].Write($"\n");

                //counter++;
                //if (num_reads > 0)
                //{
                //    //if (counter > num_reads - 1)
                //    //{
                //    //    break;
                //    //}
                //}
            });


            //Summary output messages
            SendOutputText($"{quality_reads} out of {total_reads} reads passed quality filters");
            SendOutputText($"{grep_failures} out of {total_reads} did not match the barcode pattern");
            SendOutputText($"{grep_matching_quality_reads} out of {total_reads} reads passed grep and quality filters");
            SendOutputText($"{passing_reads_that_dont_match_a_multitag} out of {total_reads} reads passed grep and quality filters but did not match a multitag");

            //Close output files
            foreach (string tag in multitags)
            {
                seq_tag_files[tag].Close();
                lin_tag_1_files[tag].Close();
                lin_tag_2_files[tag].Close();
                multi_tag_files[tag].Close();
            }
            unmatched_seqtag.Close();
            unmatched_lintag1.Close();
            unmatched_lintag2.Close();
            unmatched_multitag.Close();


            DateTime endTime = DateTime.Now;
            SendOutputText();
            SendOutputText($"Parser finished: {endTime}.");
            SendOutputText($"Elapsed time: {endTime - startTime}.");
            SendOutputText("*********************************************");
            SendOutputText();
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

        public static string RegExStrWithTwoSnips(string seq)
        {
            string regExStr = "(";

            char[] seqChars = seq.ToCharArray();
            for (int i = 0; i < seqChars.Length; i++)
            {
                for (int k = i+1; k < seqChars.Length; k++)
                {
                    for (int j = 0; j < seqChars.Length; j++)
                    {
                        if (i == j || k == j)
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

        public static int HammingDistance(string seq1, string seq2, int max = int.MaxValue, bool ignoreN = false)
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

        public static (string, int) BestMatchMultiTag(string m, string[] tags, int max = int.MaxValue, bool ignoreN = false, bool useHamming = true)
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

        public static IEnumerable<string[]> GetNextSequencesFromGZip(string f_gzipped_fastqfile, string r_gzipped_fastqfile)
        {
            int count = 0;

            //for testing, loop over the files ten times
            for (int i = 0; i < 1; i++)
            {
                FileInfo f_fileToDecompress = new FileInfo(f_gzipped_fastqfile);
                FileInfo r_fileToDecompress = new FileInfo(r_gzipped_fastqfile);

                //Create StreamReaders from both forward and reverse read files
                using (FileStream forwardFileStream = f_fileToDecompress.OpenRead(), reverseFileStream = r_fileToDecompress.OpenRead())
                using (GZipStream f_gzip = new GZipStream(forwardFileStream, CompressionMode.Decompress), r_gzip = new GZipStream(reverseFileStream, CompressionMode.Decompress))
                using (StreamReader f_file = new StreamReader(f_gzip), r_file = new StreamReader(r_gzip))
                {
                    //check to be sure there are more lines first
                    //while ((f_file.ReadLine() != null) & (r_file.ReadLine() != null))
                    while ((f_file.ReadLine() != null) & (r_file.ReadLine() != null))
                    {
                        count += 1;

                        //Returns an array of 4 strings: f_seq, r_seq, f_qual, r_qual, in that order
                        string[] retString = new string[5];

                        //parse fastq here, four lines per sequence
                        //First line is identifier, already read it, and don't need it
                        //f_file.ReadLine();
                        //r_file.ReadLine();

                        //2nd line is sequence
                        retString[0] = f_file.ReadLine(); //f_seq
                        retString[1] = r_file.ReadLine(); //r_seq


                        //3rd line is nothing, "+" 
                        f_file.ReadLine();  //f_qual
                        r_file.ReadLine();  //r_qual


                        //4th line is quality 
                        retString[2] = f_file.ReadLine();
                        retString[3] = r_file.ReadLine();

                        retString[4] = $"{count:000000}";

                        yield return retString;
                    }
                }
            }
        }

        static int[] Quality(string s)
        {
            int[] int_qual = new int[s.Length];
            int i = -1;
            foreach (char c in s)
            {
                i++;
                int_qual[i] = (int)c - 33;
            }
            return int_qual;
        }

        static double MeanQuality(string s)
        {
            int sum = 0;
            foreach (int i in Quality(s))
            {
                sum += i;
            }
            return (double)(sum / ((double)s.Length));
        }

        static string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }


    }
}
