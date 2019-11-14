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
        //***********************************************************************************************
        //All public fields need to be set by controlling app before ParseDoubleBarcodes() is called
        public string write_directory; //directory where files are read and saved
        public string read_directory; //directory where files are read and saved
        public string outputFileLabel; //text used at the start of output filenames

        public string forFastqFileList; //The forward reads, gzipped fastq file
        public string revFastqFileList; //The reverse reads, gzipped fastq file

        public string forLintagOutFile, revLintagOutFile;

        public List<string> forMultiTagList; //List of forward multiplexing tags

        public List<string> revMultiTagList; //List of reverse multiplexing tags

        public string forMultiFlankStr, revMultiFlankStr; //string for flanking sequence after multi-tags

        public int[] forUmiTagLen, revUmiTagLen; //range of possible UMI tag lengths
        public int[] forMultiTagLen, revMultiTagLen; //range of possible Multi-tag lengths
        public int[] forSpacerLength, revSpacerLength; //range of possible spacer lengths
        public int[] forLinTagLength, revLinTagLength; //range of possible lineage tag lengths
        public int multiFlankLength; //length of flanking regions around multi-tags
        public int linTagFlankLength; //length of flanking regions around lineage tags
        public int multiTagFlankErr; // number of allowed errors in multi-tag flanking sequence (zero or one)

        public string forLintagRegexStr, revLintagRegexStr; //Regex's for matching to lineage tag patterns (with flanking sequences)

        public Dictionary<string, string> mutiTagIdDict;  //Dictionary for sample IDs, keys are: $"{forwardMultiTag}_{reverseMultiTag}"

        public int parsingThreads; //number of threads to use for parsing

        public double min_qs = 30; //the minimum avareage quality score for both lineage tags

        public double multiTagErrorRate; //allowed error rate for matching sequences to multiplexing tags

        public NWeights nWeight;
        //***********************************************************************************************


        //enum for setting how to dwal with N's in multi-tag sequence; Ignore: N counts as zero mismathces, Full:N counts as one, Half:N  counts as 1/2 mismatch
        public enum NWeights { Ignore, Half, Full };

        private string[] forMultiTagArr; //Array of forward multiplexing tags, set from forMultiTagList before parsing to increase spped
        private Dictionary<string, Regex> forMultiTagRegexDict; //Dictionary of Regex's for detecting forward multi-tags, keys are multi-tag sequences

        private string[] revMultiTagArr; //Array of reverse multiplexing tags, set from revMultiTagList before parsing to increase spped
        private Dictionary<string, Regex> revMultiTagRegexDict; //Dictionary of Regex's for detecting reverse multi-tags, keys are multi-tag sequences

        //Regex's for matching to lineage tag patterns (with flanking sequences)
        private Regex forLinTagRegex, revLinTagRegex;

        IDisplaysOutputText outputReceiver; //the controlling app to send text output to (e.g. the Main Window object)


        //Locks used for synchronization of writing output to files from multiple parsing threads
        private static readonly Object fileLock = new Object(); //lock for multi-thread file writing
        private static readonly Object unmatchedFileLock = new Object(); //lock for multi-thread file writing
        private static readonly Object counterLock = new Object(); //lock for multi-thread counter updates

        public Parser(IDisplaysOutputText receiver)
        {
            outputReceiver = receiver;

            forMultiTagRegexDict = new Dictionary<string, Regex>();
            revMultiTagRegexDict = new Dictionary<string, Regex>();
            mutiTagIdDict = new Dictionary<string, string>();
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

        public void ParseDoubleBarcodes(Int64 num_reads = Int64.MaxValue)
        {
            //temp ********************** for testing: 
            parsingThreads = 3;
            //Set up log file to keep record of output text from parsing
            TextWriter logFileWriter = TextWriter.Synchronized(new StreamWriter($"{write_directory}\\{outputFileLabel}.parsing.log"));

            DateTime startTime = DateTime.Now;
            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, "*********************************************");
            SendOutputText(logFileWriter, $"Running Parser for Double Barcodes.");
            SendOutputText(logFileWriter, $"Number of threads used for parsing: {parsingThreads}.");
            SendOutputText(logFileWriter, $"Parser started: {startTime}.");
            SendOutputText(logFileWriter, "");

            //SendOutputText($"*** nWeight: {nWeight} ***");

            SendOutputText(logFileWriter, "Forward Multiplexing Tags:");
            foreach (string tag in forMultiTagList)
            {
                SendOutputText(logFileWriter, $"    {tag}, ");
            }
            SendOutputText(logFileWriter, "Reverse Multiplexing Tags:");
            foreach (string tag in revMultiTagList)
            {
                SendOutputText(logFileWriter, $"    {tag}, ");
            }
            SendOutputText(logFileWriter);

            //Set multi-plexing tag arrays for faster loop itteration
            //TODO: test this assumption
            forMultiTagArr = forMultiTagList.ToArray();
            revMultiTagArr = revMultiTagList.ToArray();

            //open files for writing
            //  lineage tags for reads that sort to a multiplexing tag, these files are for input into clustering method
            forLintagOutFile = $"{write_directory}\\{outputFileLabel}_forward_lintags.txt";
            TextWriter forwardWriter = TextWriter.Synchronized(new StreamWriter(forLintagOutFile));
            revLintagOutFile = $"{write_directory}\\{outputFileLabel}_reverse_lintags.txt";
            TextWriter reverseWriter = TextWriter.Synchronized(new StreamWriter(revLintagOutFile));

            //  actual multi-plexing tag sequences for reads that sort to a multiplexing tag, this files is for debugging
            TextWriter multiTagWriter = TextWriter.Synchronized(new StreamWriter($"{write_directory}\\{outputFileLabel}_multiplexing_tags.txt"));

            //  reads that don't sort to a multiplexing tag or don't match the lineage tag RegEx, this file for debugging 
            TextWriter unmatchedWriter = TextWriter.Synchronized(new StreamWriter($"{write_directory}\\{outputFileLabel}_unmatched_sequences.txt"));

            //Maximum useful sequence read length based on input settings
            int maxForSeqLength = forUmiTagLen.Last() + forMultiTagLen.Last() + forSpacerLength.Last() + forLinTagLength.Last() + linTagFlankLength;
            int maxRevSeqLength = revUmiTagLen.Last() + revMultiTagLen.Last() + revSpacerLength.Last() + revLinTagLength.Last() + linTagFlankLength;

            //Minimum length of sequences uncluding UMI tags, multi-tags, and multi-tag flanking sequences
            int minForMultiTestLength = forUmiTagLen.First() + forMultiTagLen.First() + multiFlankLength;
            int minRevMultiTestLength = revUmiTagLen.First() + revMultiTagLen.First() + multiFlankLength;

            //Maximum length of sequences uncluding UMI tags, multi-tags, and multi-tag flanking sequences
            int maxForMultiTestLength = forUmiTagLen.Last() + forMultiTagLen.Last() + multiFlankLength;
            int maxRevMultiTestLength = revUmiTagLen.Last() + revMultiTagLen.Last() + multiFlankLength;


            //Multi-tag flank sequence regex, used for finding matches to multi-tags if Regex search for single-mismatch fails
            //    Done as a look-ahead so that the multi-tag sequence will be at the end of the returned matching string 
            string multiFlankRegexStr;
            if (multiTagFlankErr == 0)
            {
                multiFlankRegexStr = forMultiFlankStr;
            }
            else
            {
                multiFlankRegexStr = RegExStrWithOneSnip(forMultiFlankStr, includePerfectMatch: false);
            }
            if (maxForMultiTestLength == minForMultiTestLength)
            {
                multiFlankRegexStr = $"^.*(?=({multiFlankRegexStr}$))";
            }
            else
            {
                multiFlankRegexStr = $"^.*(?=({multiFlankRegexStr}";
                multiFlankRegexStr += ".{";
                multiFlankRegexStr += $"0,{maxForMultiTestLength - minForMultiTestLength}";
                multiFlankRegexStr += "}$))";
            }
            Regex forMultiFlankRegex = new Regex(multiFlankRegexStr, RegexOptions.Compiled);

            if (multiTagFlankErr == 0)
            {
                multiFlankRegexStr = revMultiFlankStr;
            }
            else
            {
                multiFlankRegexStr = RegExStrWithOneSnip(revMultiFlankStr, includePerfectMatch: false);
            }
            if (maxRevMultiTestLength == minRevMultiTestLength)
            {
                multiFlankRegexStr = $"^.*(?=({multiFlankRegexStr}$))";
            }
            else
            {
                multiFlankRegexStr = $"^.*(?=({multiFlankRegexStr}";
                multiFlankRegexStr += ".{";
                multiFlankRegexStr += $"0,{maxRevMultiTestLength - minRevMultiTestLength}";
                multiFlankRegexStr += "}$))";
            }
            Regex revMultiFlankRegex = new Regex(multiFlankRegexStr, RegexOptions.Compiled);

            //If all UMI-tags and all multi-tags are the same length, potential multi-tage sequence can be identified strictly by position
            //Multi-tag flank sequence regex, used for finding matches to multi-tags if Regex search for single-mismatch fails
            //    Done without beginning/end anchors, since it will be compared agains strings with the exact flanking sequence length 
            if (multiTagFlankErr == 0)
            {
                multiFlankRegexStr = forMultiFlankStr;
            }
            else
            {
                multiFlankRegexStr = RegExStrWithOneSnip(forMultiFlankStr, includePerfectMatch: false);
            }
            Regex forFixedMultiFlankRegex = new Regex(multiFlankRegexStr, RegexOptions.Compiled);
            SendOutputText($"forMultiFlankRegex: {multiFlankRegexStr}");

            if (multiTagFlankErr == 0)
            {
                multiFlankRegexStr = revMultiFlankStr;
            }
            else
            {
                multiFlankRegexStr = RegExStrWithOneSnip(revMultiFlankStr, includePerfectMatch: false);
            }
            Regex revFixedMultiFlankRegex = new Regex(multiFlankRegexStr, RegexOptions.Compiled);
            SendOutputText($"revMultiFlankRegex: {multiFlankRegexStr}");


            //Minimum length of sequence before Lineage tag flanking sequence
            int minForPreLinFlankLength = forUmiTagLen.First() + forMultiTagLen.First() + forSpacerLength.First() - linTagFlankLength;
            int minRevPreLinFlankLength = revUmiTagLen.First() + revMultiTagLen.First() + revSpacerLength.First() - linTagFlankLength;

            //lengths to use for recording UMI tags (max of range)
            int forUmiTagLenUse = forUmiTagLen.Last();
            int revUmiTagLenUse = revUmiTagLen.Last();

            //Multi-tag lengths
            int forMultiLength = forMultiTagLen.Last();
            int revMultiLength = revMultiTagLen.Last();

            //Max errors allowed in Multi-tag matching
            int forMaxMultiErrors = (int)Math.Round(forMultiLength * multiTagErrorRate / 100.0);
            int revMaxMultiErrors = (int)Math.Round(revMultiLength * multiTagErrorRate / 100.0);

            //Maximum length of UMI tags + multi-tags
            int maxForUmiPlusMultiLength = forUmiTagLen.Last() + forMultiTagLen.Last();
            int maxRevUmiPlusMultiLength = revUmiTagLen.Last() + revMultiTagLen.Last();


            //Regex lists for detecting multi-tags, when all multi-tags are not the same length
            foreach (string tag in forMultiTagList)
            {
                string regexStr = "^.{";
                if (forUmiTagLen.Length == 1) regexStr += $"{forUmiTagLen[0]}";
                else regexStr += $"{forUmiTagLen[0]},{forUmiTagLen[1]}";
                regexStr += "}";
                regexStr += RegExStrWithOneSnip(tag, includePerfectMatch:false);
                if (multiTagFlankErr == 0)
                {
                    regexStr += forMultiFlankStr;
                }
                else
                {
                    regexStr += RegExStrWithOneSnip(forMultiFlankStr, includePerfectMatch: false);
                }
                SendOutputText(logFileWriter, $"Forward multi-tag RegEx: {regexStr}");
                forMultiTagRegexDict[tag] = new Regex(regexStr, RegexOptions.Compiled);
            }
            foreach (string tag in revMultiTagList)
            {
                string regexStr = "^.{";
                if (revUmiTagLen.Length == 1) regexStr += $"{revUmiTagLen[0]}";
                else regexStr += $"{revUmiTagLen[0]},{revUmiTagLen[1]}";
                regexStr += "}";
                regexStr += RegExStrWithOneSnip(tag, includePerfectMatch: false);
                if (multiTagFlankErr == 0)
                {
                    regexStr += revMultiFlankStr;
                }
                else
                {
                    regexStr += RegExStrWithOneSnip(revMultiFlankStr, includePerfectMatch: false);
                }
                SendOutputText(logFileWriter, $"Reverse multi-tag RegEx: {regexStr}");
                revMultiTagRegexDict[tag] = new Regex(regexStr, RegexOptions.Compiled);
            }
            SendOutputText(logFileWriter);

            //RegEx's for lineage tages.
            forLinTagRegex = new Regex(forLintagRegexStr, RegexOptions.Compiled);
            revLinTagRegex = new Regex(revLintagRegexStr, RegexOptions.Compiled);
            SendOutputText(logFileWriter, $"Forward lin-tag RegEx: {forLintagRegexStr}");
            SendOutputText(logFileWriter, $"Reverse lin-tag RegEx: {revLintagRegexStr}");
            SendOutputText("");

            // Keep track of how many reads pass each check
            int totalReads = 0;
            int multiTagMatchingReads = 0; //reads that match to both forward and reverse multi-tags, bool revMatchFound
            int validSampleReads = 0; //reads with multi-tags that mapped to a valid/defined sample ID (out of multi_tag_matching_reads), bool validSampleFound
            int qualityReads = 0; //reads that passed the quality check (out of valid_sample_reads), bool meanQualOk
            int lineageTagReads = 0; //reads that match lin-tag pattern (out of quality_reads), bool revLinTagMatchFound

            string[] forFiles = forFastqFileList.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            string[] revFiles = revFastqFileList.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            //Then clean up forwardSplitLine by trimming white space from ends of each string, and removing empty strings.
            forFiles = forFiles.Select(s => s.Trim()).ToArray();
            revFiles = revFiles.Select(s => s.Trim()).ToArray();
            //Then add full path info before passing to GetNextSequencesFromGZip() method
            if (forFiles.Length != revFiles.Length)
            {
                throw new ArgumentException("Forward and Reverse input file lists are not the same length");
            }
            string[] forFilePaths = new string[forFiles.Length];
            string[] revFilePaths = new string[revFiles.Length];
            for (int i=0; i< forFiles.Length; i++)
            {
                forFilePaths[i] = $"{read_directory}\\{forFiles[i]}";
                revFilePaths[i] = $"{read_directory}\\{revFiles[i]}";
            }

            //while ( ((f_id = f_file.ReadLine()) != null) & ((r_id = r_file.ReadLine()) != null) )
            //foreach (string[] stringArr in GetNextSequences(f_fastqfile, r_fastqfile))
            //Parallel.ForEach(GetNextSequences(f_fastqfile, r_fastqfile), stringArr =>
            //foreach (string[] stringArr in GetNextSequencesFromGZip($"{inDir}{f_gzipped_fastqfile}", $"{inDir}{r_gzipped_fastqfile}"))
            //Parallel.ForEach(GetNextSequencesFromGZip($"{inDir}{f_gzipped_fastqfile}", $"{inDir}{r_gzipped_fastqfile}"), stringArr =>
            if (forUmiTagLen.Length == 1 && revUmiTagLen.Length == 1 && forMultiTagLen.Length == 1 && revMultiTagLen.Length == 1)
            {
                //If the UMI and multi-tags have constant length, run the faster pasring loop that looks for the multi-tag in a defined position
                SendOutputText(logFileWriter, $"Parsing with LoopBodyConstantMultiTag().");
                SendOutputText(logFileWriter, "");
                Parallel.ForEach(GetNextSequencesFromGZip(forFilePaths, revFilePaths, num_reads: num_reads), new ParallelOptions { MaxDegreeOfParallelism = parsingThreads }, stringArr => LoopBodyConstantMultiTag(stringArr));
            }
            else
            {
                //If the UMI tags or multi-ags have different lengths, use the more general parsing loop that uses a Regex search to find/match the multi-tags
                SendOutputText(logFileWriter, $"Parsing with LoopBodyRegexMultiTag().");
                SendOutputText(logFileWriter, "");
                Parallel.ForEach(GetNextSequencesFromGZip(forFilePaths, revFilePaths, num_reads: num_reads), new ParallelOptions { MaxDegreeOfParallelism = parsingThreads }, stringArr => LoopBodyRegexMultiTag(stringArr));
            }


            //Summary output messages
            SendOutputText("");
            SendOutputText(logFileWriter);

            string percentStr = $"{(double)multiTagMatchingReads / totalReads * 100:0.##}";
            SendOutputText(logFileWriter, $"{multiTagMatchingReads} out of {totalReads} reads match to both forward and reverse multi-tags ({percentStr}%).");

            percentStr = $"{(double)validSampleReads / multiTagMatchingReads * 100:0.##}";
            SendOutputText(logFileWriter, $"{validSampleReads} of the multi-tag matching reads mapped to a valid/defined sample ID ({percentStr}%).");

            percentStr = $"{(double)qualityReads / multiTagMatchingReads * 100:0.##}";
            SendOutputText(logFileWriter, $"{qualityReads} of the multi-tag matching reads passed the quality filter ({percentStr}%).");

            percentStr = $"{(double)lineageTagReads / totalReads * 100:0.##}";
            SendOutputText(logFileWriter, $"{lineageTagReads} of the total reads passed all the quality and matching checks and counted as valid barcode reads ({percentStr}%).");

            //Close output files
            forwardWriter.Close();
            reverseWriter.Close();
            multiTagWriter.Close();
            unmatchedWriter.Close();


            DateTime endTime = DateTime.Now;
            SendOutputText(logFileWriter);
            SendOutputText(logFileWriter, $"Parser finished: {endTime}.");
            SendOutputText(logFileWriter, $"Elapsed time: {endTime - startTime}.");
            SendOutputText(logFileWriter, "*********************************************");
            SendOutputText(logFileWriter);


            logFileWriter.Close();


            //define loop body functions here:

            //If UMI tags and/or multi-tags have variable length, then must use a Regex to find multi-tag positions
            void LoopBodyRegexMultiTag(string[] stringArr)
            {
                string counter = stringArr[4]; //TODO: use this to display progress
                Int64 count = 0;
                Int64.TryParse(counter, out count);
                if (count % 1000000 == 0) SendOutputText(".", newLine: false);
                if (count % 10000000 == 0 && count > 0) SendOutputText($"{count}", newLine: false);

                //if (count < 15000000) return;

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

                //For quality checks, only use the relevant portions of the sequence (bases later in the read tend to have lower quality, but are not relevant) 
                forQual = forQual.Substring(0, maxForSeqLength);
                revQual = revQual.Substring(0, maxRevSeqLength);
                double meanForQuality = 0;
                double meanRevQuality = 0;

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
                if (!forMatchFound)
                {
                    //Check for 2-base-pair mismatches (could be adjusted to allow for a greater number of mismatches by incresing max parameter)
                    string matchSeq = forSeq.Substring(0, maxForMultiTestLength);
                    Match match = forMultiFlankRegex.Match(matchSeq);
                    if (match.Success)
                    {
                        int misMatches;
                        (forMultiMatch, misMatches) = BestMatchMultiTag(match.Value, forMultiTagArr, max: forMaxMultiErrors+1, trimUmi: true, nWeight: nWeight);
                        //(forMultiMatch, misMatches) = BestMatchMultiTag(match.Value, forMultiTagArr, max: 4, trimUmi: true, nWeight: nWeight);
                        forMatchFound = (forMultiMatch != "");
                        if (forMatchFound)
                        {
                            //because multi-tags are not all the same length, make sure the number of mismatches is ok.
                            int maxErrors = (int)Math.Round(forMultiMatch.Length * multiTagErrorRate / 100.0);
                            forMatchFound = (misMatches <= maxErrors);

                            forMultiActual = match.Value.Substring(match.Value.Length - forMultiMatch.Length);
                        }
                    }
                }
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
                    if (!revMatchFound)
                    {
                        //Check for 2-base-pair mismatches (could be adjusted to allow for a greater number of mismatches by incresing max parameter)
                        string matchSeq = revSeq.Substring(0, maxRevMultiTestLength);
                        Match match = revMultiFlankRegex.Match(matchSeq);
                        if (match.Success)
                        {
                            int misMatches;
                            //found match to flanking sequence, now test for match to multi-tag
                            (revMultiMatch, misMatches) = BestMatchMultiTag(match.Value, revMultiTagArr, max: revMaxMultiErrors+1, trimUmi: true, nWeight: nWeight);
                            //(revMultiMatch, i) = BestMatchMultiTag(match.Value, revMultiTagArr, max: 4, trimUmi: true, nWeight: nWeight);
                            revMatchFound = (revMultiMatch != "");
                            if (revMatchFound)
                            {
                                //because multi-tags are not all the same length, make sure the number of mismatches is ok.
                                int maxErrors = (int)Math.Round(revMultiMatch.Length * multiTagErrorRate / 100.0);
                                revMatchFound = (misMatches <= maxErrors);

                                revMultiActual = match.Value.Substring(match.Value.Length - revMultiMatch.Length);
                            }
                        }
                    }
                    //Procede if a multi-tag match was found;
                    if (revMatchFound)
                    {
                        //Look up sample ID, and label accordingly
                        string keys = $"{forMultiMatch}_{revMultiMatch}";
                        validSampleFound = mutiTagIdDict.TryGetValue(keys, out sampleId);
                        if (validSampleFound) sampleId += $"_{forUmi}_{revUmi}";
                        else sampleId = $"unexpected-F{forMultiMatch}-R{revMultiMatch}_{forUmi}_{revUmi}";

                        //Check mean quality score for potential forward lin-tag sequence
                        string linTagQualStr = forQual.Substring(minForPreLinFlankLength);
                        meanForQuality = MeanQuality(linTagQualStr);
                        meanQualOk = meanForQuality > min_qs;
                        if (meanQualOk)
                        {
                            //If quality good on forward read, check reverse read
                            linTagQualStr = revQual.Substring(minRevPreLinFlankLength);
                            meanRevQuality = MeanQuality(linTagQualStr);
                            meanQualOk = meanRevQuality > min_qs;
                            if (meanQualOk)
                            {
                                //if quality good, find match to lin-tag pattern, forwardLinTagRegex/reverseLinTagRegex
                                Match forLinTagMatch = forLinTagRegex.Match(forSeq.Substring(minForPreLinFlankLength));
                                forLinTagMatchFound = forLinTagMatch.Success;
                                if (forLinTagMatchFound)
                                {
                                    Match revLinTagMatch = revLinTagRegex.Match(revSeq.Substring(minRevPreLinFlankLength));
                                    revLinTagMatchFound = revLinTagMatch.Success;
                                    if (revLinTagMatchFound)
                                    {
                                        //If a match is found to both lin-tag patterns, then call it a lineage tag and write it to the output files
                                        string forLinTag = forLinTagMatch.Value;
                                        forLinTag = forLinTag.Substring(linTagFlankLength, forLinTag.Length - 2 * linTagFlankLength);
                                        string revLinTag = revLinTagMatch.Value;
                                        revLinTag = revLinTag.Substring(linTagFlankLength, revLinTag.Length - 2 * linTagFlankLength);

                                        lock (fileLock)
                                        {
                                            //TODO: checked if clustering tool cares whether or not there is a space after the comma:
                                            forwardWriter.Write($"{forLinTag},{sampleId}\n");
                                            reverseWriter.Write($"{revLinTag},{sampleId}\n");

                                            multiTagWriter.Write($"{forMultiMatch}, {forMultiActual}\n{revMultiMatch}, {revMultiActual}\n\n");
                                        }
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
                        //TODO: failed to find a multi-tag match
                    }
                }
                else
                {
                    //TODO: failed to find a multi-tag match
                }


                if (!revLinTagMatchFound)
                {
                    lock (unmatchedFileLock)
                    {
                        unmatchedWriter.Write($"{forRead}, {meanForQuality}\n{revRead}, {meanRevQuality}\n\n");
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
            }

            //If UMItags and multi-tags are constant length, then multi-tag position us constant, so loop can run faster with theis code:
            void LoopBodyConstantMultiTag(string[] stringArr)
            {
                string counter = stringArr[4]; //TODO: use this to display progress
                Int64 count = 0;
                Int64.TryParse(counter, out count);
                if (count % 1000000 == 0) SendOutputText(".", newLine: false);
                if (count % 10000000 == 0 && count > 0) SendOutputText($"{count}", newLine: false);

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

                //For quality checks, only use the relevant portions of the sequence (bases later in the read tend to have lower quality, but are not relevant) 
                forQual = forQual.Substring(0, maxForSeqLength);
                revQual = revQual.Substring(0, maxRevSeqLength);
                double meanForQuality = 0;
                double meanRevQuality = 0;

                forUmi = forSeq.Substring(0, forUmiTagLenUse);
                revUmi = revSeq.Substring(0, revUmiTagLenUse);


                //First, look for match to multi-tag flanking sequence
                string matchSeq = forSeq.Substring(maxForUmiPlusMultiLength, multiFlankLength);
                Match match = forFixedMultiFlankRegex.Match(matchSeq);
                ////***********************************
                //if (forSeq.StartsWith("ATTCGTTTTCCTTCATAGCA"))
                //{
                //    SendOutputText($"*** F matchSeq: {matchSeq} ***");
                //    SendOutputText($"*** F match.Success: {match.Success} ***");
                //}
                ////**************************************
                //If match to flanking sequence is found on forward, check reverse flanking sequence
                if (match.Success)
                {
                    matchSeq = revSeq.Substring(maxRevUmiPlusMultiLength, multiFlankLength);
                    match = revFixedMultiFlankRegex.Match(matchSeq);
                    ////***********************************
                    //if (forSeq.StartsWith("ATTCGTTTTCCTTCATAGCA"))
                    //{
                    //    SendOutputText($"*** R matchSeq: {matchSeq} ***");
                    //    SendOutputText($"*** R match.Success: {match.Success} ***");
                    //}
                    ////**************************************
                }

                //If match to both forward and reverse flanking sequences is found, go ahead and look for multi-tag matches 
                if (match.Success)
                {
                    //Find Best match to forward multi-tag
                    //   Allows for up to 2-base-pair mismatches (could be adjusted to allow for a greater number of mismatches by incresing max parameter)
                    int misMatches;
                    matchSeq = forSeq.Substring(forUmiTagLenUse, forMultiLength);
                    (forMultiMatch, misMatches) = BestMatchMultiTag(matchSeq, forMultiTagArr, max: forMaxMultiErrors+1, trimUmi: false, nWeight: nWeight);
                    forMatchFound = (forMultiMatch != "");
                    if (forMatchFound)
                    {
                        forMultiActual = matchSeq;
                    }
                    ////***********************************
                    //if (forSeq.StartsWith("ATTCGTTTTCCTTCATAGCA"))
                    //{
                    //    SendOutputText($"*** F revMatchFound: {forMatchFound} ***");
                    //    SendOutputText($"*** F revMultiMatch: {forMultiMatch} ***");
                    //}
                    ////**************************************
                }
                //Procede if a forward multi-tag match was found;
                if (forMatchFound)
                {
                    //Find Best match to reverse multi-tag
                    //   Allows for up to 2-base-pair mismatches (could be adjusted to allow for a greater number of mismatches by incresing max parameter)
                    int mismatches;
                    matchSeq = revSeq.Substring(revUmiTagLenUse, revMultiLength);
                    (revMultiMatch, mismatches) = BestMatchMultiTag(matchSeq, revMultiTagArr, max: revMaxMultiErrors+1, trimUmi: false, nWeight: nWeight);
                    revMatchFound = (revMultiMatch != "");
                    if (revMatchFound)
                    {
                        revMultiActual = matchSeq;
                    }
                    ////***********************************
                    //if (forSeq.StartsWith("ATTCGTTTTCCTTCATAGCA"))
                    //{
                    //    SendOutputText($"*** R revMatchFound: {revMatchFound} ***");
                    //    SendOutputText($"*** R revMultiMatch: {revMultiMatch} ***");
                    //}
                    ////**************************************

                    //Procede if a multi-tag match was found;
                    if (revMatchFound)
                    {
                        //Look up sample ID, and label accordingly
                        string keys = $"{forMultiMatch}_{revMultiMatch}";
                        validSampleFound = mutiTagIdDict.TryGetValue(keys, out sampleId);
                        if (validSampleFound) sampleId += $"_{forUmi}_{revUmi}";
                        else sampleId = $"unexpected-F{forMultiMatch}-R{revMultiMatch}_{forUmi}_{revUmi}";

                        //Check mean quality score for potential forward lin-tag sequence
                        string linTagQualStr = forQual.Substring(minForPreLinFlankLength);
                        meanForQuality = MeanQuality(linTagQualStr);
                        meanQualOk = meanForQuality > min_qs;
                        if (meanQualOk)
                        {
                            //If quality good on forward read, check reverse read
                            linTagQualStr = revQual.Substring(minRevPreLinFlankLength);
                            meanRevQuality = MeanQuality(linTagQualStr);
                            meanQualOk = meanRevQuality > min_qs;
                            if (meanQualOk)
                            {
                                //if quality good, find match to lin-tag pattern, forwardLinTagRegex/reverseLinTagRegex
                                Match forLinTagMatch = forLinTagRegex.Match(forSeq.Substring(minForPreLinFlankLength));
                                forLinTagMatchFound = forLinTagMatch.Success;
                                if (forLinTagMatchFound)
                                {
                                    Match revLinTagMatch = revLinTagRegex.Match(revSeq.Substring(minRevPreLinFlankLength));
                                    revLinTagMatchFound = revLinTagMatch.Success;
                                    if (revLinTagMatchFound)
                                    {
                                        //If a match is found to both lin-tag patterns, then call it a lineage tag and write it to the output files
                                        string forLinTag = forLinTagMatch.Value;
                                        forLinTag = forLinTag.Substring(linTagFlankLength, forLinTag.Length - 2 * linTagFlankLength);
                                        string revLinTag = revLinTagMatch.Value;
                                        revLinTag = revLinTag.Substring(linTagFlankLength, revLinTag.Length - 2 * linTagFlankLength);

                                        lock (fileLock)
                                        {
                                            //TODO: checked if clustering tool cares whether or not there is a space after the comma:
                                            forwardWriter.Write($"{forLinTag},{sampleId}\n");
                                            reverseWriter.Write($"{revLinTag},{sampleId}\n");

                                            multiTagWriter.Write($"{forMultiMatch}, {forMultiActual}\n{revMultiMatch}, {revMultiActual}\n\n");
                                        }
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
                        //TODO: failed to find a multi-tag match
                    }
                }
                else
                {
                    //TODO: failed to find a multi-tag match
                }


                if (!revLinTagMatchFound)
                {
                    lock (unmatchedFileLock)
                    {
                        unmatchedWriter.Write($"{forRead}, {meanForQuality}\n{revRead}, {meanRevQuality}\n\n");
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
            }

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

        //public static int HammingDistance(string seq1, string seq2, int max = int.MaxValue, bool ignoreN = false)
        public static int HammingDistance(string seq1, string seq2, int max = int.MaxValue, NWeights nWeight = NWeights.Full)
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
                    switch (nWeight)
                    {
                        case NWeights.Ignore:
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
                        case NWeights.Half:
                            double misMatchesDbl = 0;
                            for (int i = 0; i < seq1.Length; i++)
                            {
                                if ((seq1[i] != 'N') && (seq2[i] != 'N'))
                                {
                                    if (seq1[i] != seq2[i])
                                    {
                                        misMatchesDbl += 1;
                                    }
                                }
                                else
                                {
                                    misMatchesDbl += 0.5;
                                }
                                if (misMatchesDbl >= max)
                                {
                                    break;
                                }
                            }
                            mismatches = (int)(Math.Round(misMatchesDbl));
                            return mismatches;
                        case NWeights.Full:
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
                        default:
                            return max;
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

        public static (string, int) BestMatchMultiTag(string m, string[] tags, int max = int.MaxValue, NWeights nWeight = NWeights.Full, bool useHamming = true, bool trimUmi = false)
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
            //    trimUmi should be set to true if the string for testing (m) has a UMI seqeunce at the beginning that needs to be trimmed off
            string bestMatch = "";
            int leastMismatches = max;
            int mismatches;
            string mTest;
            //first search for exact matach
            foreach (string t in tags)
            {
                if (trimUmi) mTest = m.Substring(m.Length - t.Length);
                else mTest = m;

                if (mTest.Equals(t))
                {
                    return (t, 0);
                }
                else
                {
                    if (useHamming)
                    {
                        mismatches = HammingDistance(mTest, t, max, nWeight);
                    }
                    else
                    {
                        mismatches = LevenshteinDistance(mTest, t);
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

        public static IEnumerable<string[]> GetNextSequencesFromGZip(string f_gzipped_fastqfile, string r_gzipped_fastqfile, Int64 num_reads = Int64.MaxValue)
        {
            string[] forList = new string[] { f_gzipped_fastqfile };
            string[] revList = new string[] { r_gzipped_fastqfile };
            return GetNextSequencesFromGZip(forList, revList, num_reads);
        }

        public static IEnumerable<string[]> GetNextSequencesFromGZip(string[] forwardFileList, string[] reverseFileList, Int64 num_reads = Int64.MaxValue)
        {
            int forFileListLength = forwardFileList.Length;
            int revFileListLength = reverseFileList.Length;
            if (forFileListLength!=revFileListLength)
            {
                throw new ArgumentException("Forward and Reverse input file lists are not the same length");
            }

            Int64 count = 0;

            for (int i=0; i<forFileListLength; i++)
            {
                Int64 subCount = 0;

                FileInfo f_fileToDecompress = new FileInfo(forwardFileList[i]);
                FileInfo r_fileToDecompress = new FileInfo(reverseFileList[i]);

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
                        subCount += 1;
                        //if (count > num_reads) break;
                        if (subCount > num_reads) break;

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

                        retString[4] = $"{count}";

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
