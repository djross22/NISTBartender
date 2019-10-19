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
        private Dictionary<string, Regex> forMultiTagRegexDict; //Dictionary of Regex's for detecting forward multi-tags, keys are multi-tag sequences

        public List<string> revMultiTagList; //List of reverse multiplexing tags
        private Dictionary<string, Regex> revMultiTagRegexDict; //Dictionary of Regex's for detecting reverse multi-tags, keys are multi-tag sequences

        public string forMultiFlankStr, revMultiFlankStr; //string for flanking sequence after multi-tags

        public int[] forUmiTagLen, revUmiTagLen; //range of possible UMI tag lengths
        public int[] forMultiTagLen, revMultiTagLen; //range of possible Multi-tag lengths
        public int[] forSpacerLength, revSpacerLength; //range of possible spacer lengths
        public int[] forLinTagLength, revLinTagLength; //range of possible lineage tag lengths
        public int multiFlankLength; //length of flanking regions around multi-tags
        public int linTagFlankLength; //length of flanking regions around lineage tags

        //Regex's for matching to lineage tag patterns (with flanking sequences)
        public string forLintagRegexStr, revLintagRegexStr;
        private Regex forLinTagRegex, revLinTagRegex;

        public Dictionary<string, string> mutiTagIdDict;  //Dictionary for sample IDs, keys are: $"{forwardMultiTag}_{reverseMultiTag}"

        private static readonly Object fileLock = new Object(); //lock for multi-thread file writing
        private static readonly Object unmatchedFileLock = new Object(); //lock for multi-thread file writing
        private static readonly Object counterLock = new Object(); //lock for multi-thread counter updates


        public int parsingThreads; //number of threads to use for parsing

        static double min_qs = 30; //the minimum avareage quality score for both lineage tags

        public Parser(IDisplaysOutputText receiver)
        {
            outputReceiver = receiver;

            forMultiTagRegexDict = new Dictionary<string, Regex>();
            revMultiTagRegexDict = new Dictionary<string, Regex>();
            mutiTagIdDict = new Dictionary<string, string>();
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
            foreach (string tag in forMultiTagList)
            {
                SendOutputText($"        {tag}, ");
            }
            SendOutputText("    Reverse Multiplexing Tags:");
            foreach (string tag in revMultiTagList)
            {
                SendOutputText($"        {tag}, ");
            }
            SendOutputText();
            
            //open files for writing
            //  lineage tags for reads that sort to a multiplexing tag, these files are for input into clustering method
            TextWriter forwardWriter = TextWriter.Synchronized(new StreamWriter($"{write_directory}\\forward_lintags.txt"));
            TextWriter reverseWriter = TextWriter.Synchronized(new StreamWriter($"{write_directory}\\reverse_lintags.txt"));

            //  actual multi-plexing tag sequences for reads that sort to a multiplexing tag, this files is for debugging
            TextWriter multiTagWriter = TextWriter.Synchronized(new StreamWriter($"{write_directory}\\multiplexing_tags.txt"));

            //  reads that don't sort to a multiplexing tag or don't match the lineage tag RegEx, this file for debugging 
            TextWriter unmatchedWriter = TextWriter.Synchronized(new StreamWriter($"{write_directory}\\unmatched_sequences.txt"));

            //Maximum useful sequence read length based on input settings
            int maxForSeqLength = forUmiTagLen.Last() + forMultiTagLen.Last() + forSpacerLength.Last() + forLinTagLength.Last() + linTagFlankLength;
            int maxRevSeqLength = revUmiTagLen.Last() + revMultiTagLen.Last() + revSpacerLength.Last() + revLinTagLength.Last() + linTagFlankLength;
            SendOutputText($"maxForSeqLength: {maxForSeqLength}");
            SendOutputText($"maxRevSeqLength: {maxRevSeqLength}");

            //Minimum length of sequence before Lineage tag flanking sequence
            int minForPreLinFlankLength = forUmiTagLen.First() + forMultiTagLen.First() + forSpacerLength.First() - linTagFlankLength;
            int minRevPreLinFlankLength = revUmiTagLen.First() + revMultiTagLen.First() + revSpacerLength.First() - linTagFlankLength;
            SendOutputText($"minForPreLinFlankLength: {minForPreLinFlankLength}");
            SendOutputText($"minRevPreLinFlankLength: {minRevPreLinFlankLength}");

            //lengths to use for recording UMI tags (max of range)
            int forUmiTagLenUse = forUmiTagLen.Last();
            int revUmiTagLenUse = revUmiTagLen.Last(); 

            //Regex lists for detecting multi-tags
            foreach (string tag in forMultiTagList)
            {
                string regexStr = "^.{";
                if (forUmiTagLen.Length == 1) regexStr += $"{forUmiTagLen[0]}";
                else regexStr += $"{forUmiTagLen[0]},{forUmiTagLen[1]}";
                regexStr += "}";
                regexStr += RegExStrWithOneSnip(tag, includePerfectMatch:false);
                regexStr += RegExStrWithOneSnip(forMultiFlankStr, includePerfectMatch: false);
                SendOutputText($"Forward multi-tag RegEx: {regexStr}");
                forMultiTagRegexDict[tag] = new Regex(regexStr, RegexOptions.Compiled);
            }
            foreach (string tag in revMultiTagList)
            {
                string regexStr = "^.{";
                if (revUmiTagLen.Length == 1) regexStr += $"{revUmiTagLen[0]}";
                else regexStr += $"{revUmiTagLen[0]},{revUmiTagLen[1]}";
                regexStr += "}";
                regexStr += RegExStrWithOneSnip(tag, includePerfectMatch: false);
                regexStr += RegExStrWithOneSnip(revMultiFlankStr, includePerfectMatch: false);
                SendOutputText($"Reverse multi-tag RegEx: {regexStr}");
                revMultiTagRegexDict[tag] = new Regex(regexStr, RegexOptions.Compiled);
            }
            SendOutputText();

            //RegEx's for lineage tages.
            forLinTagRegex = new Regex(forLintagRegexStr, RegexOptions.Compiled);
            revLinTagRegex = new Regex(revLintagRegexStr, RegexOptions.Compiled);
            SendOutputText($"Forward lin-tag RegEx: {forLintagRegexStr}");
            SendOutputText($"Reverse lin-tag RegEx: {revLintagRegexStr}");


            //SendOutputText($"Multi-tag sample IDs: ");
            //foreach (string keys in mutiTagIdDict.Keys)
            //{
            //    SendOutputText($"keys: {keys}; value: {mutiTagIdDict[keys]}, ");
            //    string samp;
            //    bool test = mutiTagIdDict.TryGetValue(keys, out samp);
            //    SendOutputText($"test: {test}");
            //}
            //SendOutputText($"");



            // Keep track of how many reads pass each check
            int totalReads = 0;
            int multiTagMatchingReads = 0; //reads that match to both forward and reverse multi-tags, bool revMatchFound
            int validSampleReads = 0; //reads with multi-tags that mapped to a valid/defined sample ID (out of multi_tag_matching_reads), bool validSampleFound
            int qualityReads = 0; //reads that passed the quality check (out of valid_sample_reads), bool meanQualOk
            int lineageTagReads = 0; //reads that match lin-tag pattern (out of quality_reads), bool revLinTagMatchFound



            //while ( ((f_id = f_file.ReadLine()) != null) & ((r_id = r_file.ReadLine()) != null) )
            //foreach (string[] stringArr in GetNextSequences(f_fastqfile, r_fastqfile))
            //Parallel.ForEach(GetNextSequences(f_fastqfile, r_fastqfile), stringArr =>
            //foreach (string[] stringArr in GetNextSequencesFromGZip($"{inDir}{f_gzipped_fastqfile}", $"{inDir}{r_gzipped_fastqfile}"))
            //Parallel.ForEach(GetNextSequencesFromGZip($"{inDir}{f_gzipped_fastqfile}", $"{inDir}{r_gzipped_fastqfile}"), stringArr =>
            Parallel.ForEach(GetNextSequencesFromGZip($"{read_directory}\\{f_gzipped_fastqfile}", $"{read_directory}\\{r_gzipped_fastqfile}"), new ParallelOptions { MaxDegreeOfParallelism = 1 }, stringArr =>
            {
                string counter = stringArr[4]; //TODO: use this to display progress
                int count = 0;
                int.TryParse(counter, out count);

                string forRead = stringArr[0];
                string revRead = stringArr[1];
                string forQual = stringArr[2];
                string revQual = stringArr[3];

                bool forMatchFound = false;
                bool revMatchFound = false;
                bool validSampleFound = false;
                bool meanQualOk = false;
                bool forLinTagMatchFound = false;
                bool revLinTagMatchFound = false;

                string forMultiMatch = ""; //matching seqeunces from list of nominal multi-tags
                string revMultiMatch = ""; //matching seqeunces from list of nominal multi-tags
                string forMultiActual = ""; //actual sequence matched to multi-tag
                string revMultiActual = ""; //actual sequence matched to multi-tag
                string forUmi, revUmi; //UMI tag sequences
                string sampleId; //Identifier for sample (forward x reverse multi-tag pairs)


                //To make things run faster, work with just the substring of length maxForSeqLength/maxRevSeqLength
                //    TODO: check this assumption with performance comparison
                string forSeq = forRead.Substring(0, maxForSeqLength);
                string revSeq = revRead.Substring(0, maxRevSeqLength);

                forUmi = forSeq.Substring(0, forUmiTagLenUse);
                revUmi = revSeq.Substring(0, revUmiTagLenUse);

                //Find Match to multi-tag
                foreach (string tag in forMultiTagList)
                {
                    Match match = forMultiTagRegexDict[tag].Match(forSeq);
                    forMatchFound = match.Success;
                    if (forMatchFound)
                    {
                        forMultiMatch = tag;
                        string matchStr = match.Value;
                        forMultiActual = matchStr.Substring(matchStr.Length - multiFlankLength - tag.Length, tag.Length);
                        
                        break;
                    }
                }
                //TODO: code to allow for 2-base-pair missmatches (or greater)
                //Procede if a multi-tag match was found;
                if (forMatchFound)
                {
                    foreach (string tag in revMultiTagList)
                    {

                        Match match = revMultiTagRegexDict[tag].Match(revSeq);
                        revMatchFound = match.Success;
                        if (revMatchFound)
                        {
                            revMultiMatch = tag;
                            string matchStr = match.Value;
                            revMultiActual = matchStr.Substring(matchStr.Length - multiFlankLength - tag.Length, tag.Length);
                            
                            break;
                        }
                    }
                    //TODO: code to allow for 2-base-pair missmatches (or greater)
                    //Procede if a multi-tag match was found;
                    if (revMatchFound)
                    {
                        //Look up sample ID, if there is a match, procede
                        string keys = $"{forMultiMatch}_{revMultiMatch}";
                        //if (count < 10) SendOutputText($"keys: {keys[0]}, {keys[1]}; value: {mutiTagIdDict[keys]}, ");
                        validSampleFound = mutiTagIdDict.TryGetValue(keys, out sampleId);
                        //if (count < 10) SendOutputText($"validSampleFound: {validSampleFound}");
                        if (validSampleFound)
                        {
                            sampleId += $"_{forUmi}_{revUmi}";

                            //Check mean quality score for potential forward lin-tag sequence
                            string linTagQualStr = forQual.Substring(minForPreLinFlankLength);
                            meanQualOk = MeanQuality(linTagQualStr) > min_qs;
                            if (meanQualOk)
                            {
                                //If quality good on forward read, check reverse read
                                linTagQualStr = revQual.Substring(minRevPreLinFlankLength);
                                meanQualOk = MeanQuality(linTagQualStr) > min_qs;
                                if (meanQualOk)
                                {
                                    //if quality good, find match to lin-tag pattern, forwardLinTagRegex/reverseLinTagRegex
                                    if (count < 20) SendOutputText($"Forward string: {forSeq.Substring(minForPreLinFlankLength)}");
                                    Match forLinTagMatch = forLinTagRegex.Match(forSeq.Substring(minForPreLinFlankLength));
                                    forLinTagMatchFound = forLinTagMatch.Success;
                                    if (forLinTagMatchFound)
                                    {
                                        if (count < 20) SendOutputText($"Reverse string: {revSeq.Substring(minRevPreLinFlankLength)}");
                                        Match revLinTagMatch = revLinTagRegex.Match(revSeq.Substring(minRevPreLinFlankLength));
                                        revLinTagMatchFound = revLinTagMatch.Success;
                                        if (revLinTagMatchFound) {
                                            //If a match is found to both lin-tag patterns, then call it a lineage tag and write it to the output files
                                            string forLinTag = forLinTagMatch.Value;
                                            forLinTag = forLinTag.Substring(linTagFlankLength, forLinTag.Length - 2* linTagFlankLength);
                                            string revLinTag = revLinTagMatch.Value;
                                            revLinTag = revLinTag.Substring(linTagFlankLength, revLinTag.Length - 2 * linTagFlankLength);

                                            lock (fileLock)
                                            {
                                                //TODO: checked if clustering tool cares whether or not there is a space after the comma:
                                                forwardWriter.Write($"{forLinTag},{sampleId}\n");
                                                reverseWriter.Write($"{revLinTag},{sampleId}\n");

                                                multiTagWriter.Write($"{forMultiMatch}, {forMultiActual}\n{revMultiMatch}, {revMultiActual}\n\n");
                                            }
                                            //lock (counter_lock)
                                            //{
                                            //    grep_matching_quality_reads += 1;
                                            //    total_reads += 1;
                                            //}
                                        }
                                        else
                                        {
                                            //TODO: failed to find a lin-tag match
                                        }
                                    }
                                    else
                                    {
                                        //TODO: failed to find a lin-tag match
                                    }

                                }
                                else
                                {
                                    //TODO: fail quality check
                                }
                            }
                            else
                            {
                                //TODO: fail quality check
                            }
                        }
                        else
                        {
                            sampleId = $"unexpected_{forUmi}_{revUmi}";
                            //TODO: handle unexpected multi-tag pair 
                        }
                        
                    }
                    else
                    {
                        //TODO: failed to find a multi-tag match
                    }
                }
                else
                {
                    //TODO: failed to find a multi-tag match
                }

                //Check for false values to decide which bins to assign counts to
                //    revMatchFound indicates that a multi-tag match was found; 
                //    validSampleFound indicates that a valid sample match was found
                //    meanQualOk
                //    revLinTagMatchFound
                
                if (!revLinTagMatchFound)
                {
                    lock (unmatchedFileLock)
                    {
                        unmatchedWriter.Write($"{forRead}\n{revRead}\n\n");
                    }
                }

                lock (counterLock)
                {
                    totalReads += 1;
                    if (revMatchFound)
                    {
                        multiTagMatchingReads += 1;
                        if (validSampleFound)
                        {
                            validSampleReads += 1;
                            if (meanQualOk)
                            {
                                qualityReads += 1;
                                if (revLinTagMatchFound)
                                {
                                    lineageTagReads += 1;
                                }
                            }
                        }
                    }
                }


                /*


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


                */


            });


            //Summary output messages
            SendOutputText();
            string percentStr = $"{(double)multiTagMatchingReads / totalReads * 100:0.##}";
            SendOutputText($"{multiTagMatchingReads} out of {totalReads} reads match to both forward and reverse multi-tags ({percentStr}%).");
            percentStr = $"{(double)validSampleReads / multiTagMatchingReads * 100:0.##}";
            SendOutputText($"{validSampleReads} of those mapped to a valid/defined sample ID ({percentStr}%).");
            percentStr = $"{(double)qualityReads / validSampleReads * 100:0.##}";
            SendOutputText($"{qualityReads} of those passed the qualtity filter ({percentStr}%).");
            percentStr = $"{(double)lineageTagReads / qualityReads * 100:0.##}";
            SendOutputText($"{lineageTagReads} of those matched the lineage tag RegEx patttern and counted as valid barcode reads ({percentStr}%).");

            //Close output files
            forwardWriter.Close();
            reverseWriter.Close();
            multiTagWriter.Close();
            unmatchedWriter.Close();


            DateTime endTime = DateTime.Now;
            SendOutputText();
            SendOutputText($"Parser finished: {endTime}.");
            SendOutputText($"Elapsed time: {endTime - startTime}.");
            SendOutputText("*********************************************");
            SendOutputText();


            
        }

        public static string RegExStrWithOneSnip(string seq, bool includePerfectMatch = true)
        {
            string regExStr;

            if (includePerfectMatch) regExStr = $"({seq}|";
            else regExStr = "(";

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
