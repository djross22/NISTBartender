﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.ComponentModel;
using Microsoft.Win32;
using System.Xml;
using BarcodeParser;

namespace BartenderWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, IDisplaysOutputText
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        //Window title, app name, plus file name, plus * to indicate unsaved changes
        private static string appName = "NIST Bartender";
        private string displayTitle = appName + " - ";
        private string paramsFilePath;
        private bool paramsChanged = false;
        private List<string> paramsList;
        //Fields for XML parameters file output
        private XmlDocument xmlDoc;
        private XmlNode rootNode;

        private string inputDirectory, outputDirectory, outputFileLabel, forwardGzFastQ, reverseGzFastQ;
        private string defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);//@"C:";

        //Length of read from sequencer, used for underlining sequence
        private string readLengthStr;
        private int readLength;

        //UMI tage lengths
        private string forUmiTagLenStr, revUmiTagLenStr;
        private int[] forUmiTagLen, revUmiTagLen;

        //Multiplexing tags
        private string fowardMultiTagText, reverseMultiTagText, extraMultiTagText;
        private List<string> forwardMultiTagList, reverseMultiTagList;
        private Dictionary<string, string> fowardIdDict, reverseIdDict;
        private Dictionary<string, string> mutiTagIdDict; //Dictionary for sample IDs, keys are: $"{forwardMultiTag}_{reverseMultiTag}"
        private int[] forMultiTagLen, revMultiTagLen;
        //Multi-tag flank sequences
        private string forwardMultiFlankStr, reverseMultiFlankStr;
        private string multiFlankLengthStr;
        private int multiFlankLength;

        //Spacer lengths
        private string forwardSpacerLengthStr, reverseSpacerLengthStr;
        private int[] forwardSpacerLength, reverseSpacerLength;
        private string spacerInsRateStr, spacerDelRateStr;
        private double spacerInsRate, spacerDelRate;

        //Lineage tags
        private string forwardLinTag, reverseLinTag;
        private string forwardLinTagLengthStr, reverseLinTagLengthStr;
        private int[] forwardLinTagLength, reverseLinTagLength;
        private string forLintagRegexStr, revLintagRegexStr;
        //Lin-tag flanking sequences
        private string[] forwardLinTagFlankStrs = new string[2];
        private string[] reverseLinTagFlankStrs = new string[2];
        private string linTagFlankLengthStr;
        private int linTagFlankLength;

        //Parameters for Auto-RegEx
        private string regexDelRateStr, regexInsRateStr;
        private double regexDelRate, regexInsRate;
        private bool ignoreSingleConst;


        private string outputText;

        //Highlight colors for sequence annotations
        private static Brush UmiTagHighlight = Brushes.Yellow;
        //private static Brush MultiTagHighlight = Brushes.LightGreen;
        //private static Brush LineageTagHighlight = Brushes.Thistle;
        private static Brush MultiTagHighlight = Brushes.Thistle;
        private static Brush LineageTagHighlight = Brushes.LightGreen;
        private static Brush FlankHighlight = Brushes.PowderBlue;

        //Parameters for sequence file parsing
        private int threadsForParsing;
        private string minQualityStr;
        private double minQuality;
        private Parser parser;

        private List<Control> inputControlsList;

        #region Properties Getters and Setters

        public string SpacerDelRateStr
        {
            get { return this.spacerDelRateStr; }
            set
            {
                if (this.spacerDelRateStr != value)
                {
                    this.spacerDelRateStr = value;
                    OnPropertyChanged("SpacerDelRateStr");
                    double.TryParse(spacerDelRateStr, out spacerDelRate);
                }
            }
        }

        public string SpacerInsRateStr
        {
            get { return this.spacerInsRateStr; }
            set
            {
                if (this.spacerInsRateStr != value)
                {
                    this.spacerInsRateStr = value;
                    OnPropertyChanged("SpacerInsRateStr");
                    double.TryParse(spacerInsRateStr, out spacerInsRate);
                }
            }
        }

        public bool IgnoreSingleConst
        {
            get { return this.ignoreSingleConst; }
            set
            {
                if (this.ignoreSingleConst != value)
                {
                    this.ignoreSingleConst = value;
                    OnPropertyChanged("IgnoreSingleConst");
                }
            }
        }

        public string RegexDelRateStr
        {
            get { return this.regexDelRateStr; }
            set
            {
                if (this.regexDelRateStr != value)
                {
                    this.regexDelRateStr = value;
                    OnPropertyChanged("RegexDelRateStr");
                    double.TryParse(regexDelRateStr, out regexDelRate);
                }
            }
        }

        public string RegexInsRateStr
        {
            get { return this.regexInsRateStr; }
            set
            {
                if (this.regexInsRateStr != value)
                {
                    this.regexInsRateStr = value;
                    OnPropertyChanged("RegexInsRateStr");
                    double.TryParse(regexInsRateStr, out regexInsRate);
                }
            }
        }

        public string MinQualityStr
        {
            get { return this.minQualityStr; }
            set
            {
                if (this.minQualityStr != value)
                {
                    this.minQualityStr = value;
                    OnPropertyChanged("MinQualityStr");
                    double.TryParse(minQualityStr, out minQuality);
                }
            }
        }

        public bool ParamsChanged
        {
            get { return this.paramsChanged; }
            set
            {
                this.paramsChanged = value;
                UpdateTitle();
                //OnPropertyChanged("ParamsChanged");
            }
        }

        public string ParamsFilePath
        {
            get { return this.paramsFilePath; }
            set
            {
                if (this.paramsFilePath != value)
                {
                    this.paramsFilePath = value;
                    UpdateTitle();
                    OnPropertyChanged("ParamsFileName");
                }
            }
        }

        public string DisplayTitle
        {
            get { return this.displayTitle; }
            set
            {
                if (this.displayTitle != value)
                {
                    this.displayTitle = value;
                    OnPropertyChanged("DisplayTitle");
                }
            }
        }

        public string ReverseGzFastQ
        {
            get { return this.reverseGzFastQ; }
            set
            {
                if (this.reverseGzFastQ != value)
                {
                    this.reverseGzFastQ = value;
                    OnPropertyChanged("ReverseGzFastQ");
                }
            }
        }
        

        public string ForwardGzFastQ
        {
            get { return this.forwardGzFastQ; }
            set
            {
                if (this.forwardGzFastQ != value)
                {
                    this.forwardGzFastQ = value;
                    OnPropertyChanged("ForwardGzFastQ");
                }
            }
        }

        public string OutputFileLabel
        {
            get { return this.outputFileLabel; }
            set
            {
                if (this.outputFileLabel != value)
                {
                    this.outputFileLabel = value;
                    OnPropertyChanged("OutputFileLabel");
                }
            }
        }

        public string OutputDirectory
        {
            get { return this.outputDirectory; }
            set
            {
                if (this.outputDirectory != value)
                {
                    this.outputDirectory = value;
                    OnPropertyChanged("OutputDirectory");
                }
            }
        }

        public string InputDirectory
        {
            get { return this.inputDirectory; }
            set
            {
                if (this.inputDirectory != value)
                {
                    this.inputDirectory = value;
                    OnPropertyChanged("InputDirectory");
                }
            }
        }

        public string RevLintagRegexStr
        {
            get { return this.revLintagRegexStr; }
            set
            {
                if (this.revLintagRegexStr != value)
                {
                    this.revLintagRegexStr = value;
                    OnPropertyChanged("RevLintagRegexStr");
                }
            }
        }

        public string ForLintagRegexStr
        {
            get { return this.forLintagRegexStr; }
            set
            {
                if (this.forLintagRegexStr != value)
                {
                    this.forLintagRegexStr = value;
                    OnPropertyChanged("ForLintagRegexStr");
                }
            }
        }

        public string ReverseLinTagLengthStr
        {
            get { return this.reverseLinTagLengthStr; }
            set
            {
                if (this.reverseLinTagLengthStr != value)
                {
                    this.reverseLinTagLengthStr = value;
                    OnPropertyChanged("ReverseLinTagLengthStr");
                    reverseLinTagLength = LengthRangeStringToArray(reverseLinTagLengthStr);
                    //int.TryParse(reverseLinTagLengthStr, out reverseLinTagLength);
                }
            }
        }

        public string ForwardLinTagLengthStr
        {
            get { return this.forwardLinTagLengthStr; }
            set
            {
                if (this.forwardLinTagLengthStr != value)
                {
                    this.forwardLinTagLengthStr = value;
                    OnPropertyChanged("ForwardLinTagLengthStr");
                    forwardLinTagLength = LengthRangeStringToArray(forwardLinTagLengthStr);
                    //int.TryParse(forwardLinTagLengthStr, out forwardLinTagLength);
                }
            }
        }

        public string ReverseSpacerLengthStr
        {
            get { return this.reverseSpacerLengthStr; }
            set
            {
                if (this.reverseSpacerLengthStr != value)
                {
                    this.reverseSpacerLengthStr = value;
                    OnPropertyChanged("ReverseSpacerLengthStr");
                    reverseSpacerLength = LengthRangeStringToArray(reverseSpacerLengthStr);
                    //int.TryParse(reverseSpacerLengthStr, out reverseSpacerLength);
                }
            }
        }

        public string ForwardSpacerLengthStr
        {
            get { return this.forwardSpacerLengthStr; }
            set
            {
                if (this.forwardSpacerLengthStr != value)
                {
                    this.forwardSpacerLengthStr = value;
                    OnPropertyChanged("ForwardSpacerLengthStr");
                    forwardSpacerLength = LengthRangeStringToArray(forwardSpacerLengthStr);
                    //int.TryParse(forwardSpacerLengthStr, out forwardSpacerLength);
                }
            }
        }

        public string LinTagFlankLengthStr
        {
            get { return this.linTagFlankLengthStr; }
            set
            {
                if (this.linTagFlankLengthStr != value)
                {
                    this.linTagFlankLengthStr = value;
                    OnPropertyChanged("LinTagFlankLengthStr");
                    int.TryParse(linTagFlankLengthStr, out linTagFlankLength);
                }
            }
        }

        public string MultiFlankLengthStr
        {
            get { return this.multiFlankLengthStr; }
            set
            {
                if (this.multiFlankLengthStr != value)
                {
                    this.multiFlankLengthStr = value;
                    OnPropertyChanged("MultiFlankLengthStr");
                    int.TryParse(multiFlankLengthStr, out multiFlankLength);
                }
            }
        }

        public string ExtraMultiTagText
        {
            get { return this.extraMultiTagText; }
            set
            {
                if (this.extraMultiTagText != value)
                {
                    this.extraMultiTagText = value;
                    OnPropertyChanged("ExtraMultiTagText");
                }
            }
        }

        public string ReverseMultiTagText
        {
            get { return this.reverseMultiTagText; }
            set
            {
                if (this.reverseMultiTagText != value)
                {
                    this.reverseMultiTagText = value;
                    OnPropertyChanged("ReverseMultiTagText");
                }
            }
        }

        public string FowardMultiTagText
        {
            get { return this.fowardMultiTagText; }
            set
            {
                if (this.fowardMultiTagText != value)
                {
                    this.fowardMultiTagText = value;
                    OnPropertyChanged("FowardMultiTagText");
                }
            }
        }

        public string ReadLengthStr
        {
            get { return this.readLengthStr; }
            set
            {
                if (this.readLengthStr != value)
                {
                    this.readLengthStr = value;
                    OnPropertyChanged("ReadLengthStr");
                    SetReadLength();
                }
            }
        }

        public string OutputText
        {
            get { return this.outputText; }
            set
            {
                if (this.outputText != value)
                {
                    this.outputText = value;
                    OnPropertyChanged("OutputText");
                }
            }
        }

        public string RevUmiTagLenStr
        {
            get { return this.revUmiTagLenStr; }
            set
            {
                if (this.revUmiTagLenStr != value)
                {
                    this.revUmiTagLenStr = value;
                    OnPropertyChanged("RevUmiTagLenStr");
                    SetUmiTagLength(forward: false);
                }
            }
        }

        public string ForUmiTagLenStr
        {
            get { return this.forUmiTagLenStr; }
            set
            {
                if (this.forUmiTagLenStr != value)
                {
                    this.forUmiTagLenStr = value;
                    OnPropertyChanged("ForUmiTagLenStr");
                    SetUmiTagLength(forward: true);
                }
            }
        }
        #endregion

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));

                if ((paramsList != null) && (paramsList.Contains(name)))
                {
                    ParamsChanged = true;
                }
                if (name == "IgnoreSingleConst") ParamsChanged = true;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            InitInputControlsList();

            parser = new Parser(this);

            DataContext = this;

            forUmiTagLenTextBox.Background = UmiTagHighlight;
            revUmiTagLenTextBox.Background = UmiTagHighlight;
            fowardMultiTagTextBox.Background = MultiTagHighlight;
            reverseMultiTagTextBox.Background = MultiTagHighlight;
            extraMultiTagTextBox.Background = MultiTagHighlight;
            multiFlankLengthTextBox.Background = FlankHighlight;
            linTagFlankLengthTextBox.Background = FlankHighlight;
            forRegExTextBox.Background = LineageTagHighlight;
            revRegExTextBox.Background = LineageTagHighlight;

            InputDirectory = defaultDirectory;
            OutputDirectory = defaultDirectory;

            ParamsFilePath = "";
            CreateParamsList();

            OutputFileLabel = "Barcoded_Samples";

            //CopyReverseComplement();

            //TextRange textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
            //textRange.Text = "ZZZZZZZZXXXXXXXXXXCATCGGTGAGCCCGGGCTGTCGGCGTNNTNNNANNTNNNANNTNNNANNTNNNANNTNNNANNATATGCCAGCAGGCCGGCCACGCTNNTNNNANNTNNNANNANNNANNTNNNANNTNNNANNCGGTGGCCCGGGCGGCCGCACGATGCGTCCGGCGTAGAGGXXXXXXXXXXZZZZZZZZ";

            ReadLengthStr = "150";
            //ForUmiTagLenStr = "";
            //RevUmiTagLenStr = "";

            LinTagFlankLengthStr = "4";
            MultiFlankLengthStr = "4";

            RegexDelRateStr = "20";
            RegexInsRateStr = "40";

            SpacerDelRateStr = "5";
            SpacerInsRateStr = "5";

            //FowardMultiTagText = "AGCTAGCTAG, A\n";
            //FowardMultiTagText += "CAATGCCTAG, B\n";
            //ReverseMultiTagText = "TAATGCCGTG, 1\n";
            //ReverseMultiTagText += "GGGCAATGCG, 2\n";
            //ExtraMultiTagText = "AGAAGGTAG, TAGTGTCGTG, S5\n";
            //ExtraMultiTagText = "AGAAGGTAG, GGGCAATGCG, S6\n";

            InitMultiTagLists();

            AddOutputText($"Number of Logical Processors: {Environment.ProcessorCount}");
            threadsForParsing = Environment.ProcessorCount / 2;
            AddOutputText($"Number of threads to use for sequence file parsing: {threadsForParsing}");

            MinQualityStr = "30";

            //"ParamsChanged = false" should be the last thing in the Constructor
            ParamsChanged = false;
        }

        private void InitMultiTagLists()
        {
            forwardMultiTagList = new List<string>();
            fowardIdDict = new Dictionary<string, string>();
            reverseMultiTagList = new List<string>();
            reverseIdDict = new Dictionary<string, string>();
            mutiTagIdDict = new Dictionary<string, string>();
        }

        private void InitInputControlsList()
        {
            inputControlsList = new List<Control>();
            inputControlsList.Add(mainMenu);
            inputControlsList.Add(mainToolBar);
            inputControlsList.Add(forwardRichTextBox);
            inputControlsList.Add(reverseRichTextBox);
            inputControlsList.Add(readLengthTextBox);
            inputControlsList.Add(fowardMultiTagTextBox);
            inputControlsList.Add(reverseMultiTagTextBox);
            inputControlsList.Add(extraMultiTagTextBox);
            inputControlsList.Add(forUmiTagLenTextBox);
            inputControlsList.Add(revUmiTagLenTextBox);
            inputControlsList.Add(forRegExTextBox);
            inputControlsList.Add(revRegExTextBox);
            inputControlsList.Add(multiFlankLengthTextBox);
            inputControlsList.Add(linTagFlankLengthTextBox);
            inputControlsList.Add(minQualityTextBox);

            inputControlsList.Add(regexDelRateTextBox);
            inputControlsList.Add(regexInsRateTextBox);
            inputControlsList.Add(ignoreSingleConstCheckBox);

            inputControlsList.Add(spacerDelRateTextBox);
            inputControlsList.Add(spacerInsRateTextBox);
        }

        private void CreateParamsList()
        {
            paramsList = new List<string>();
            paramsList.Add("ReverseGzFastQ");
            paramsList.Add("ForwardGzFastQ");
            paramsList.Add("OutputDirectory");
            paramsList.Add("OutputFileLabel");
            paramsList.Add("InputDirectory");
            paramsList.Add("ForwardReadSequence");
            paramsList.Add("ReverseReadSequence");
            paramsList.Add("RevLintagRegexStr");
            paramsList.Add("ForLintagRegexStr");
            paramsList.Add("LinTagFlankLengthStr");
            paramsList.Add("MultiFlankLengthStr");
            paramsList.Add("ExtraMultiTagText");
            paramsList.Add("ReverseMultiTagText");
            paramsList.Add("FowardMultiTagText");
            paramsList.Add("ReadLengthStr");
            paramsList.Add("RevUmiTagLenStr");
            paramsList.Add("ForUmiTagLenStr");
            paramsList.Add("MinQualityStr");

            //paramsList.Add("IgnoreSingleConst"); this is a bool Property so nas to be dealt with separately
            paramsList.Add("RegexDelRateStr");
            paramsList.Add("RegexInsRateStr");

            paramsList.Add("SpacerDelRateStr");
            paramsList.Add("SpacerInsRateStr");
            //paramsList.Add("");
        }

        private int[] LengthRangeStringToArray(string range)
        {
            string[] split = range.Split('-', StringSplitOptions.RemoveEmptyEntries);
            int[] lenArr = new int[split.Length];
            for (int i=0; i<split.Length; i++)
            {
                int.TryParse(split[i], out lenArr[i]);
            }

            return lenArr;
        }

        private void UpdateTitle()
        {
            string title = appName;
            if (!string.IsNullOrEmpty(ParamsFilePath))
            {
                title += " - " + ParamsFilePath;
            }
            if (ParamsChanged)
            {
                title += "*";
            }
            DisplayTitle = title;
        }

        private void MakeMultiTagLists()
        {
            InitMultiTagLists();

            //If multitag text boxes aren't properly populated, give warning message and return from method
            string invalidTagListMsg = "Please enter at least one valid set of Multiplex Tags, and try again.";
            bool validForRevTags = true, validForwardTags = true, validReverseTags = true, validExtraTags = true;
            if (String.IsNullOrEmpty(ExtraMultiTagText))
            {
                validExtraTags = false;
            }
            else
            {
                string cleanExtra = ExtraMultiTagText.Replace("\n", "").Replace("\r", "");
                if (String.IsNullOrEmpty(cleanExtra))
                {
                    validExtraTags = false;
                }
            }

            if (String.IsNullOrEmpty(FowardMultiTagText))
            {
                validForwardTags = false;
            }
            else
            {
                string cleanForward = FowardMultiTagText.Replace("\n", "").Replace("\r", "");
                if (String.IsNullOrEmpty(cleanForward))
                {
                    validForwardTags = false;
                }
            }

            if (String.IsNullOrEmpty(ReverseMultiTagText))
            {
                validReverseTags = false;
            }
            else
            {
                string cleanReverse = ReverseMultiTagText.Replace("\n", "").Replace("\r", "");
                if (String.IsNullOrEmpty(cleanReverse))
                {
                    validReverseTags = false;
                }
            }

            //Give warning if the forward or reverse multiplexing tag list is empty
            if (validForwardTags && !validReverseTags)
            {
                string msg = "Warning: Ignoring Forward Multiplexing Tag list since Reverse Multiplexing Tag list is empty.\n";
                msg += "Analysis will procede using only tags listed in Extra Multiplexing Tags list.";
                MessageBox.Show(msg, "Warning!");
            }
            if (!validForwardTags && validReverseTags)
            {
                string msg = "Warning: Ignoring Reverse Multiplexing Tag list since Forward Multiplexing Tag list is empty.\n";
                msg += "Analysis will procede using only tags listed in Extra Multiplexing Tags list.";
                MessageBox.Show(msg, "Warning!");
            }

            validForRevTags = validForwardTags & validReverseTags;

            if (!validForRevTags && !validExtraTags)
            {
                MessageBox.Show(invalidTagListMsg);
                return;
            }

            //First add to fowardMultiTagList and reverseMultiTagList from FowardMultiTagText and ReverseMultiTagText
            if (validForRevTags)
            {
                string[] forwardTagArr = FowardMultiTagText.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                string[] reverseTagArr = ReverseMultiTagText.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string tagPlusId in forwardTagArr)
                {
                    string[] splitTag = tagPlusId.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    forwardMultiTagList.Add(splitTag[0]);
                    if (splitTag.Length > 1)
                    {
                        //key is sequence of multi-tag; value is sampleID (if given)
                        fowardIdDict[splitTag[0]] = splitTag[1];
                    }
                    else
                    {
                        fowardIdDict[splitTag[0]] = $"{splitTag[0]}_";
                    }
                }

                foreach (string tagPlusId in reverseTagArr)
                {
                    string[] splitTag = tagPlusId.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    reverseMultiTagList.Add(splitTag[0]);
                    if (splitTag.Length > 1)
                    {
                        //key is sequence of multi-tag; value is sampleID (if given)
                        reverseIdDict[splitTag[0]] = splitTag[1];
                    }
                    else
                    {
                        reverseIdDict[splitTag[0]] = $"_{splitTag[0]}";
                    }
                }

                //Then combine Forward and Reverse tags in all possible ways and add IDs to mutiTagIDDict
                foreach (string forTag in forwardMultiTagList)
                {
                    foreach (string revTag in reverseMultiTagList)
                    {
                        string keys = $"{forTag}_{revTag}";
                        AddOutputText($"keys: {keys}");
                        string value = $"{fowardIdDict[forTag]}{reverseIdDict[revTag]}";
                        value = value.Replace("__", "_");
                        mutiTagIdDict[keys] = value;
                    }
                }
            }


            //Then add individual tags from ExtraMultiTagText - and add matching IDs to mutiTagIDDict
            if (validExtraTags)
            {
                string[] extraTagArr = ExtraMultiTagText.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string tagPlusId in extraTagArr)
                {
                    string[] splitTag = tagPlusId.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string forTag = splitTag[0];
                    string revTag = splitTag[1];
                    string keys = $"{forTag}_{revTag}";
                    AddOutputText($"keys: {keys}");
                    if (!forwardMultiTagList.Contains(forTag)) forwardMultiTagList.Add(forTag);
                    if (!reverseMultiTagList.Contains(revTag)) reverseMultiTagList.Add(revTag);

                    if (splitTag.Length > 2)
                    {
                        mutiTagIdDict[keys] = splitTag[2];
                    }
                    else
                    {
                        mutiTagIdDict[keys] = $"{forTag}_{revTag}";
                    }
                }
            }

            AddOutputText($"Multi-tag sample IDs: ", false);
            foreach (string keys in mutiTagIdDict.Keys)
            {
                AddOutputText($"{mutiTagIdDict[keys]}, ", false);
            }
            AddOutputText($"");
        }

        private void SetReadLength()
        {
            readLength = Int32.Parse(this.readLengthStr);
            //AddOutputText($"Read length: {readLength}.\n");

            UnderLineReadLength();
        }

        private void SetUmiTagLength(bool forward)
        {
            string umiLenStr;
            int[] umiLenArr;
            if (forward)
            {
                umiLenStr = forUmiTagLenStr;
                if (string.IsNullOrEmpty(umiLenStr))
                {
                    forUmiTagLen = null;
                    //AddOutputText($"Forward UMI tag length: )";
                    return;
                }
            }
            else
            {
                umiLenStr = revUmiTagLenStr;
                if (string.IsNullOrEmpty(umiLenStr))
                {
                    revUmiTagLen = null;
                    //AddOutputText($"Reverse UMI tag length: ");
                    return;
                }
            }


            string[] split = umiLenStr.Split('-');
            umiLenArr = new int[split.Length];
            for (int i=0; i<split.Length; i++)
            {
                umiLenArr[i] = Int32.Parse(split[i]);
            }
            if (split.Length == 2)
            {
                if (umiLenArr[1] <= umiLenArr[0])
                {
                    MessageBox.Show("Warning: Bad UMI length specification.");
                    return;
                }
            }

            if (forward)
            {
                forUmiTagLen = umiLenArr;
                //AddOutputText($"Forward UMI tag length: ", false);
            }
            else
            {
                revUmiTagLen = umiLenArr;
                //AddOutputText($"Reverse UMI tag length: ", false);
            }

            foreach (int i in umiLenArr) {
                //AddOutputText($"{i}, ", false);
            }
            //AddOutputText("");
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Save();
            outputTextBox.Focus();
        }

        private bool Save()
        {
            bool didSave;
            if (!string.IsNullOrEmpty(ParamsFilePath))
            {
                try
                {
                    //Populate the XML document
                    CreateParamsXml();
                    //Save the XML document
                    xmlDoc.Save(ParamsFilePath);

                    ParamsChanged = false;
                    didSave = true;
                }
                catch (UnauthorizedAccessException e)
                {
                    MessageBox.Show($"{e.Message}. Try saving with a temporary file name, then restart {appName}");
                    didSave = SaveAs();
                }
            }
            else
            {
                didSave = SaveAs();
            }

            return didSave;
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveAs();
            outputTextBox.Focus();
        }

        private bool SaveAs()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XML file (*.xml)|*.xml";
            if (!string.IsNullOrEmpty(ParamsFilePath))
            {
                saveFileDialog.FileName = System.IO.Path.GetFileName(ParamsFilePath);
            }
            bool didSave;
            if (saveFileDialog.ShowDialog() == true)
            {
                ParamsFilePath = saveFileDialog.FileName;

                //Populate the XML document
                CreateParamsXml();
                //Save the XML document
                xmlDoc.Save(ParamsFilePath);

                ParamsChanged = false;
                didSave = true;
            }
            else
            {
                didSave = false;
            }

            return didSave;
        }

        private void CreateParamsXml()
        {
            xmlDoc = new XmlDocument();
            rootNode = xmlDoc.CreateElement("parameters");
            XmlAttribute sourceAtt = xmlDoc.CreateAttribute("source");
            sourceAtt.Value = appName;
            rootNode.Attributes.Append(sourceAtt);
            //add the root node to the document
            xmlDoc.AppendChild(rootNode);

            //handle the Forward and Reverse Read Sequences separately, since propery binding to RichTextDocuments is wierd.
            TextRange textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
            string value = textRange.Text;
            XmlNode paramNode = xmlDoc.CreateElement("ForwardReadSequence");
            paramNode.InnerText = value;
            rootNode.AppendChild(paramNode);

            textRange = new TextRange(reverseRichTextBox.Document.ContentStart, reverseRichTextBox.Document.ContentEnd);
            value = textRange.Text;
            paramNode = xmlDoc.CreateElement("ReverseReadSequence");
            paramNode.InnerText = value;
            rootNode.AppendChild(paramNode);

            //add all the parameters values to the XML document
            PropertyInfo propInfo;
            foreach (string param in paramsList)
            {
                propInfo = this.GetType().GetProperty(param);
                if (propInfo != null)
                {
                    value = $"{propInfo.GetValue(this)}";
                    paramNode = xmlDoc.CreateElement(param);
                    paramNode.InnerText = value;
                    rootNode.AppendChild(paramNode);
                }
                
            }
            //handle bool Property as special case
            string boolParam = "IgnoreSingleConst";
            propInfo = this.GetType().GetProperty(boolParam);
            if (propInfo != null)
            {
                value = $"{propInfo.GetValue(this)}";
                paramNode = xmlDoc.CreateElement(boolParam);
                paramNode.InnerText = value;
                rootNode.AppendChild(paramNode);
            }

        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            outputTextBox.Focus();
        }

        private void reverseComplementButton_Click(object sender, RoutedEventArgs e)
        {
            CopyReverseComplement();
        }
        private bool SaveFirstQuery()
        {
            //SaveFirstQuery returns true unless the user chooses 'Cancel'
            //    - either directly in response to the 1st Message Box
            //    - or in in the Select File Save Dialog box-
            string messageBoxText = "Do you want to save changes first?";
            string caption = "Save File?";
            MessageBoxButton button = MessageBoxButton.YesNoCancel;
            MessageBoxImage icon = MessageBoxImage.Warning;

            bool okToGo = false;

            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon);
            switch (result)
            {
                case MessageBoxResult.Yes:
                    // User pressed Yes button
                    //Save();
                    //okToGo = true;
                    okToGo = Save();
                    break;
                case MessageBoxResult.No:
                    // User pressed No button
                    // do nothing (go ahead without saving)
                    okToGo = true;
                    break;
                case MessageBoxResult.Cancel:
                    // User pressed Cancel button
                    okToGo = false;
                    break;
            }
            return okToGo;
        }

        private void loadMenuItme_Click(object sender, RoutedEventArgs e)
        {
            if (!ParamsChanged || SaveFirstQuery())
            {
                LoadParams();
            }
            outputTextBox.Focus();
        }

        private void LoadParamsFile(string file)
        {
            if (!ParamsChanged || SaveFirstQuery())
            {
                LoadParams(file);
            }
            outputTextBox.Focus();
        }

        private void LoadParams(string file)
        {
            if (file.EndsWith(".xml", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    ParamsFilePath = file;

                    ReadParamsXml(ParamsFilePath);

                    ParamsChanged = false;
                }
                catch
                {
                    MessageBox.Show($"Failed to open file, {file}");
                }
            }
            else
            {
                MessageBox.Show($"{file} is not an parameters file in XML format (*.xml)");
            }
        }

        private void ReadParamsXml(string filePath)
        {
            xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            rootNode = xmlDoc.SelectSingleNode("descendant::parameters");

            //handle the Forward and Reverse Read Sequences separately, since propery binding to RichTextDocuments is wierd.
            TextRange textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
            XmlNode paramNode = rootNode.SelectSingleNode($"descendant::ForwardReadSequence");
            textRange.Text = paramNode.InnerText;

            textRange = new TextRange(reverseRichTextBox.Document.ContentStart, reverseRichTextBox.Document.ContentEnd);
            paramNode = rootNode.SelectSingleNode($"descendant::ReverseReadSequence");
            textRange.Text = paramNode.InnerText;

            //Read all the paramsList parameter values from the XML document
            PropertyInfo propInfo;
            foreach (string param in paramsList)
            {
                paramNode = rootNode.SelectSingleNode($"descendant::{param}");
                propInfo = this.GetType().GetProperty(param);

                if (propInfo != null && paramNode != null)
                {
                    string value = paramNode.InnerText;
                    propInfo.SetValue(this, value);
                }

            }
            //handle bool Property as special case
            string boolParam = "IgnoreSingleConst";
            paramNode = rootNode.SelectSingleNode($"descendant::{boolParam}");
            propInfo = this.GetType().GetProperty(boolParam);

            if (propInfo != null && paramNode != null)
            {
                string value = paramNode.InnerText;
                propInfo.SetValue(this, (value=="True" || value=="true"));
            }
        }

        private void LoadParams()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "XML file (*.xml)|*.xml";
            openFileDialog.Title = "Select Parameters File to Load";
            if (openFileDialog.ShowDialog() == true)
            {
                ParamsFilePath = openFileDialog.FileName;
                ReadParamsXml(ParamsFilePath);
                ParamsChanged = false;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ParamsChanged && !SaveFirstQuery())
            {
                //Input trext has changed, and the user selected 'Cancel'
                //    so do not close
                e.Cancel = true;
            }
            else
            {
                var messageBoxResult = MessageBox.Show($"Are you sure you want to exit {appName}?\nClick 'Yes' to abort or 'No' to continue.", $"Exit {appName}?", MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    AddOutputText($"Closing {appName}.");
                }
                else
                {
                    // If user doesn't want to close, cancel closure
                    e.Cancel = true;
                }
            }
            outputTextBox.Focus();
        }

        private void AddOutputText(string txt, bool newLine = true)
        {
            if (newLine)
            {
                OutputText += $"{txt}\n";
            }
            else
            {
                OutputText += txt;
            }

            //TODO: set up log file
            ////Add to log file
            //if (logFilePath != null)
            //{
            //    if (newLine)
            //    {
            //        string timeStr = DateTime.Now.ToString("yyyy-MM-dd.HH:mm:ss.fff");
            //        File.AppendAllText(logFilePath, $"\n{timeStr},\t {txt}");
            //    }
            //    else
            //    {
            //        File.AppendAllText(logFilePath, txt);
            //    }
            //}
        }

        public void DisplayOutput(string text, bool newLine = true)
        {
            //this has to be delegated becasue it interacts with the GUI by sending text to the outputTextBox
            this.Dispatcher.Invoke(() => {
                AddOutputText(text, newLine);
            });
        }

        private void inputDirMenuItme_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            // Set validate names and check file exists to false otherwise windows will
            // not let you select "Folder Selection."
            dialog.ValidateNames = false;
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = true;
            // Always default to Folder Selection.
            dialog.FileName = "_";

            dialog.Title = "Select Directory Containing Input Sequencee Files";
            if (InputDirectory != defaultDirectory) dialog.InitialDirectory = InputDirectory;

            if (dialog.ShowDialog() == true)
            {
                InputDirectory = System.IO.Path.GetDirectoryName(dialog.FileName);
            }
        }

        private void outputDirMenuItme_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            // Set validate names and check file exists to false otherwise windows will
            // not let you select "Folder Selection."
            dialog.ValidateNames = false;
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = true;
            // Always default to Folder Selection.
            dialog.FileName = "_";

            dialog.Title = "Select Directory For Saving Output Files";
            if (OutputDirectory != defaultDirectory) dialog.InitialDirectory = OutputDirectory;

            if (dialog.ShowDialog() == true)
            {
                OutputDirectory = System.IO.Path.GetDirectoryName(dialog.FileName);
            }
        }

        private void forFastqMenuItme_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            //openFileDialog.Filter = "XML file (*.xml)|*.xml";
            openFileDialog.Title = "Select GZipped Fastq file with Forward Reads";
            if (InputDirectory != defaultDirectory) openFileDialog.InitialDirectory = InputDirectory;
            if (openFileDialog.ShowDialog() == true)
            {
                string filePathStr = openFileDialog.FileName;
                string FileDir = System.IO.Path.GetDirectoryName(filePathStr);
                //If file is in the InputDirectory, then just display the filename, without the directory info
                if (FileDir == InputDirectory)
                {
                    ForwardGzFastQ = System.IO.Path.GetFileName(filePathStr);
                }
                else
                {
                    ForwardGzFastQ = filePathStr;
                }
            }
        }

        private void revFastqMenuItme_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            //openFileDialog.Filter = "XML file (*.xml)|*.xml";
            openFileDialog.Title = "Select GZipped Fastq file with Reverse Reads";
            if (InputDirectory != defaultDirectory) openFileDialog.InitialDirectory = InputDirectory;
            if (openFileDialog.ShowDialog() == true)
            {
                string filePathStr = openFileDialog.FileName;
                string FileDir = System.IO.Path.GetDirectoryName(filePathStr);
                //If file is in the InputDirectory, then just display the filename, without the directory info
                if (FileDir == InputDirectory)
                {
                    ReverseGzFastQ = System.IO.Path.GetFileName(filePathStr);
                }
                else
                {
                    ReverseGzFastQ = filePathStr;
                }
            }
        }

        private void forwardRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ParamsChanged = true;
        }

        private void reverseRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ParamsChanged = true;
        }

        private void analyzeMultiButton_Click(object sender, RoutedEventArgs e)
        {
            MakeMultiTagLists();

            AddOutputText("");
            AddOutputText("Nearest neighbor and minimum distances for Forward Multiplex Tags:");
            int minLength = int.MaxValue;
            int maxLength = 0;
            foreach (string tag in forwardMultiTagList)
            {
                List<string> compList = new List<string>(forwardMultiTagList);
                compList.Remove(tag);
                AddOutputText($"{tag}: Hamming", false);
                AddOutputText($"{Parser.BestMatchMultiTag(tag, compList.ToArray())}, Levenshtein: ", false);
                AddOutputText($"{Parser.BestMatchMultiTag(tag, compList.ToArray(), useHamming:false)}.");
                foreach (string s in compList)
                {
                    if (s.Contains(tag)) AddOutputText($"!!Warning: {tag} is a substring of {s}.");
                }
                if (tag.Length < minLength) minLength = tag.Length;
                if (tag.Length > maxLength) maxLength = tag.Length;
            }
            //Set forMultiTagLen[]
            if (minLength == maxLength) forMultiTagLen = new int[1] { minLength };
            else forMultiTagLen = new int[2] { minLength, maxLength };

            AddOutputText("");
            AddOutputText("Nearest neighbor and minimum distances for Reverse Multiplex Tags:");
            minLength = int.MaxValue;
            maxLength = 0;
            foreach (string tag in reverseMultiTagList)
            {
                List<string> compList = new List<string>(reverseMultiTagList);
                compList.Remove(tag);
                AddOutputText($"{tag}: Hamming", false);
                AddOutputText($"{Parser.BestMatchMultiTag(tag, compList.ToArray())}, Levenshtein: ", false);
                AddOutputText($"{Parser.BestMatchMultiTag(tag, compList.ToArray(), useHamming:false)}.");
                foreach (string s in compList)
                {
                    if (s.Contains(tag)) AddOutputText($"!!Warning: {tag} is a substring of {s}.");
                }
                if (tag.Length < minLength) minLength = tag.Length;
                if (tag.Length > maxLength) maxLength = tag.Length;
            }
            //Set forMultiTagLen[]
            if (minLength == maxLength) revMultiTagLen = new int[1] { minLength };
            else revMultiTagLen = new int[2] { minLength, maxLength };
            AddOutputText("");
        }

        private void parseButton_Click(object sender, RoutedEventArgs e)
        {
            RunParser();

        }

        private void DisableInputControls()
        {
            foreach (Control cont in inputControlsList)
            {
                cont.IsEnabled = false;
            }
        }

        private void EnableInputControls()
        {
            foreach (Control cont in inputControlsList)
            {
                cont.IsEnabled = true;
            }
        }

        private void RunParser()
        {
            DisableInputControls();

            ParamsFilePath = System.IO.Path.Combine(outputDirectory, $"{outputFileLabel}.xml");
            Save();

            parser.write_directory = OutputDirectory;
            parser.outputFileLabel = outputFileLabel;
            parser.read_directory = InputDirectory;
            parser.f_gzipped_fastqfile = ForwardGzFastQ;
            parser.r_gzipped_fastqfile = ReverseGzFastQ;

            parser.forUmiTagLen = forUmiTagLen;
            parser.revUmiTagLen = revUmiTagLen;

            parser.forMultiTagList = forwardMultiTagList;
            parser.revMultiTagList = reverseMultiTagList;
            parser.forMultiTagLen = forMultiTagLen;
            parser.revMultiTagLen = revMultiTagLen;

            parser.mutiTagIdDict = mutiTagIdDict;

            parser.forSpacerLength = forwardSpacerLength;
            parser.revSpacerLength = reverseSpacerLength;

            parser.forLinTagLength = forwardLinTagLength;
            parser.revLinTagLength = reverseLinTagLength;

            parser.forMultiFlankStr = forwardMultiFlankStr;
            parser.revMultiFlankStr = reverseMultiFlankStr;

            parser.linTagFlankLength = linTagFlankLength;
            parser.multiFlankLength = multiFlankLength;

            parser.forLintagRegexStr = forLintagRegexStr;
            parser.revLintagRegexStr = revLintagRegexStr;

            parser.parsingThreads = threadsForParsing;

            BackgroundWorker parserWorker = new BackgroundWorker();
            parserWorker.WorkerReportsProgress = false;
            parserWorker.DoWork += parserWorker_DoWork;
            parserWorker.RunWorkerCompleted += parserWorker_RunWorkerCompleted;

            parserWorker.RunWorkerAsync();
        }

        void parserWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                parser.ParseDoubleBarcodes();
            }
            catch (Exception ex)
            {
                //this has to be delegated becasue it interacts with the GUI by sending text to the outputTextBox
                this.Dispatcher.Invoke(() => {
                    AddOutputText($"Excpetion in Parser.ParseDoubleBarcodes(): {ex})");
                });
            }
        }

        void parserWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            EnableInputControls();
        }

        private void CopyReverseComplement()
        {
            clearWhiteSpaces();

            TextRange textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
            textRange.Text = textRange.Text.ToUpper();
            string forwardSequence = textRange.Text;

            string reverseSequence = Parser.ReverseComplement(forwardSequence);

            textRange = new TextRange(reverseRichTextBox.Document.ContentStart, reverseRichTextBox.Document.ContentEnd);
            textRange.Text = reverseSequence;
        }

        private void forUmiTagLenTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                forUmiTagLenTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }

        private void revUmiTagLenTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                revUmiTagLenTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }

        private void forRegExTextBox_KeyUp(object sender, KeyEventArgs e)
        {

        }

        private void revRegExTextBox_KeyUp(object sender, KeyEventArgs e)
        {

        }

        private string CondenseRepeatedNs(Match match)
        {
            //The code in this method assumes that the match.Value is a repeated string of N's
            string matchStr = match.Value;
            int matchLen = matchStr.Length;
            if (matchLen == 1)
            {
                return ".";
            }
            else
            {
                double dMin = matchLen * (1 - regexDelRate/100.0);
                int min = (int)Math.Round(dMin);
                double dMax = matchLen * (1 + regexInsRate/100.0);
                int max = (int)Math.Round(dMax);

                if (max == min)
                {
                    if (max == matchLen)
                    {
                        if (matchLen == 2)
                        {
                            return "..";
                        }
                        else
                        {
                            string ret = ".{";
                            ret += $"{matchLen}";
                            ret += "}?";
                            return ret;
                        }
                    }
                    else
                    {
                        string msg = "Something weird happened in the auto regex generation, CondenseRepeatedNs() method. ";
                        msg += $"matching string: \"{matchStr}\"";
                        MessageBox.Show(msg);
                        return matchStr;
                    }
                }
                else
                {
                    string ret = ".{";
                    ret += $"{min},{max}";
                    ret += "}?";
                    return ret;
                }
            }
        }
        
        private void autoRegexButton_Click(object sender, RoutedEventArgs e)
        {
            //First update everything that needs updating by runnint AnalyzeSequences()
            AnalyzeSequences();

            //Automatically generate RegEx's, and also calculate/update length range for Lin-tags 

            //Forward Lin-tag RegEx
            string regExStr = Parser.RegExStrWithOneSnip(forwardLinTagFlankStrs[0]);
            string linTag = forwardLinTag;
            int linTagLen = forwardLinTag.Length;
            //look for single constants and replace with Ns if IgnoreSingleConst
            if (IgnoreSingleConst) {
                linTag = Regex.Replace(linTag, "(?<=N).(?=N)", "N");
            }
            //find runs of N's and use them, along with spacerInsRate and spacerDelRate to calculate the min and max length
            int midLen = 0;
            MatchCollection matches = Regex.Matches(linTag, "N+");
            foreach (Match match in matches)
            {
                midLen += match.Value.Length;
            }
            double dMin = midLen * (1 - regexDelRate / 100.0);
            int min = (int)Math.Round(dMin);
            double dMax = midLen * (1 + regexInsRate / 100.0);
            int max = (int)Math.Round(dMax);
            min += linTagLen - midLen;
            max += linTagLen - midLen;
            // write result to ForwardLinTagLengthStr (which also assigns values to forwardLinTagLength[])
            string lengthStr = "";
            if (min != linTagLen) lengthStr += $"{min}-";
            lengthStr += $"{linTagLen}";
            if (max != linTagLen) lengthStr += $"-{max}";
            ForwardLinTagLengthStr = lengthStr;
            //Find runs of one or more N's (again) and replace them accordingly using CondenseRepeatedNs()
            MatchEvaluator evaluator = new MatchEvaluator(CondenseRepeatedNs);
            linTag = Regex.Replace(linTag, "N+", evaluator);
            //regExStr += linTag;
            regExStr += linTag.Replace('N', '.'); //include the Replace() here just in case
            regExStr += Parser.RegExStrWithOneSnip(forwardLinTagFlankStrs[1]);
            ForLintagRegexStr = regExStr;

            //Reverse Lin-tag RegEx
            regExStr = Parser.RegExStrWithOneSnip(reverseLinTagFlankStrs[0]);
            linTag = reverseLinTag;
            linTagLen = reverseLinTag.Length;
            //look for single constants and replace with Ns if IgnoreSingleConst
            if (IgnoreSingleConst)
            {
                linTag = Regex.Replace(linTag, "(?<=N).(?=N)", "N");
            }

            //find runs of N's and use them, along with spacerInsRate and spacerDelRate to calculate the min and max length
            midLen = 0;
            matches = Regex.Matches(linTag, "N+");
            foreach (Match match in matches)
            {
                midLen += match.Value.Length;
            }
            dMin = midLen * (1 - regexDelRate / 100.0);
            min = (int)Math.Round(dMin);
            dMax = midLen * (1 + regexInsRate / 100.0);
            max = (int)Math.Round(dMax);
            min += linTagLen - midLen;
            max += linTagLen - midLen;
            // write result to ForwardLinTagLengthStr (which also assigns values to forwardLinTagLength[])
            lengthStr = "";
            if (min != linTagLen) lengthStr += $"{min}-";
            lengthStr += $"{linTagLen}";
            if (max != linTagLen) lengthStr += $"-{max}";
            ReverseLinTagLengthStr = lengthStr;
            //find runs of one or more N's (again) and replace them accordingly using CondenseRepeatedNs()
            linTag = Regex.Replace(linTag, "N+", evaluator);
            //regExStr += linTag;
            regExStr += linTag.Replace('N', '.'); //include the Replace() here just in case
            regExStr += Parser.RegExStrWithOneSnip(reverseLinTagFlankStrs[1]);
            RevLintagRegexStr = regExStr;
        }

        private void readLengthTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                readLengthTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }

        private void clearWhiteSpaces()
        {
            TextRange textRange;

            textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
            textRange.Text = Parser.RemoveStringWhitespace(textRange.Text);

            textRange = new TextRange(reverseRichTextBox.Document.ContentStart, reverseRichTextBox.Document.ContentEnd);
            textRange.Text = Parser.RemoveStringWhitespace(textRange.Text);
        }

        private void analyzeButton_Click(object sender, RoutedEventArgs e)
        {
            AnalyzeSequences();
        }

        private void AnalyzeSequences()
        {
            clearWhiteSpaces();

            //reverseComplementButton_Click(sender, e);

            ClearSequenceFormatting();

            HighlightUmiTag();

            MakeMultiTagLists();

            HighlightMultiTag();

            HighlightLineageTag();

            HighlightMultiTagFlank();

            UnderLineReadLength();

            UnselectSequenceText();
        }

        private void ClearSequenceFormatting()
        {
            foreach (RichTextBox rtb in new RichTextBox[2] { forwardRichTextBox , reverseRichTextBox })
            {
                rtb.Selection.ClearAllProperties();
            }
        }

        private void UnselectSequenceText()
        {
            foreach (RichTextBox rtb in new RichTextBox[2] { forwardRichTextBox, reverseRichTextBox })
            {
                TextPointer endPointer = rtb.Document.ContentEnd.GetPositionAtOffset(0);
                rtb.Selection.Select(endPointer, endPointer);
            }
        }

        private void UnderLineReadLength()
        {
            foreach (RichTextBox rtb in new RichTextBox[2] { forwardRichTextBox , reverseRichTextBox })
            {
                TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                TextPointer startPointer = rtb.Document.ContentStart.GetPositionAtOffset(0);
                TextPointer endPointer = rtb.Document.ContentEnd.GetPositionAtOffset(0);
                rtb.Selection.Select(startPointer, endPointer);

                //rtb.Selection.ClearAllProperties();
                rtb.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);

                endPointer = GetTextPointerAtOffset(rtb, readLength);

                rtb.Selection.Select(startPointer, endPointer);
                rtb.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
            }
        }

        private void HighlightUmiTag()
        {
            foreach (RichTextBox rtb in new RichTextBox[2] { forwardRichTextBox, reverseRichTextBox })
            {
                TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                string read = textRange.Text;

                //The UMI tag is indicated by Z's at the start of each sequence.
                //    So, start by finding the first non-'Z' character
                Regex umiRegEx = new Regex("^Z*");
                string umiMatch = umiRegEx.Match(read).Value;
                //AddOutputText($"umiMatch: {umiMatch}");
                int firstNonZ = umiMatch.Length;

                TextPointer startPointer = rtb.Document.ContentStart.GetPositionAtOffset(0);
                TextPointer endPointer = GetTextPointerAtOffset(rtb, firstNonZ);

                rtb.Selection.Select(startPointer, endPointer);

                //If UMI tag length is set in the GUI then use that value - and replace/insert the appropriate number of Zs
                //    otherwise, use number of Z'z in sequence to set value for UMI tag length
                if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                {
                    //AddOutputText($"ForUmiTagLenStr: {ForUmiTagLenStr}");
                    if (string.IsNullOrEmpty(ForUmiTagLenStr))
                    {
                        ForUmiTagLenStr = $"{firstNonZ}";
                    }
                    else
                    {
                        rtb.Selection.Text = new String('Z', forUmiTagLen.Last());
                    }
                }
                if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                {
                    //AddOutputText($"RevUmiTagLenStr: {RevUmiTagLenStr}");
                    if (string.IsNullOrEmpty(RevUmiTagLenStr))
                    {
                        RevUmiTagLenStr = $"{firstNonZ}";
                    }
                    else
                    {
                        rtb.Selection.Text = new String('Z', revUmiTagLen.Last());
                    }
                }

                rtb.Selection.ApplyPropertyValue(Inline.BackgroundProperty, UmiTagHighlight);
            }
        }

        private void HighlightMultiTag()
        {
            foreach (RichTextBox rtb in new RichTextBox[2] { forwardRichTextBox, reverseRichTextBox })
            {
                TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                string read = textRange.Text;

                //The Multiplexing tag is indicated by X's after the UMI tag (Z's).
                //    So, start by finding the first non-'Z' character
                //        and the first non-'X' non'Z' character
                Regex umiRegEx = new Regex("^Z*");
                string umiMatch = umiRegEx.Match(read).Value;
                //AddOutputText($"umiMatch: {umiMatch}");
                int firstNonZ = umiMatch.Length;

                Regex multiRegEx = new Regex("^Z*X*");
                string multiMatch = multiRegEx.Match(read).Value;
                //AddOutputText($"multiMatch: {multiMatch}");
                int firstNonX = multiMatch.Length;

                //Check if X'x ans Z's are in expected order
                if (firstNonX < firstNonZ)
                {
                    string errorMsg = "Unexpected sequence in ";
                    if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                    {
                        errorMsg += "Forward Read Sequence. ";
                    }
                    if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                    {
                        errorMsg += "Reverse Read Sequence. ";
                    }
                    errorMsg += "Sequences should start with one or more Z's for UMI tags, then one or more X's for multiplexing tags.";
                    MessageBox.Show(errorMsg);
                    return;
                }

                //AddOutputText($"firstNonX: {firstNonX}");

                TextPointer startPointer = rtb.Document.ContentStart.GetPositionAtOffset(0);
                startPointer = GetTextPointerAtOffset(rtb, firstNonZ);
                TextPointer endPointer = GetTextPointerAtOffset(rtb, firstNonX);

                rtb.Selection.Select(startPointer, endPointer);

                //If multiplexing tags are set in the GUI then use the length from those strings - and replace/insert the appropriate number of X's
                if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                {
                    //if (!String.IsNullOrEmpty(FowardMultiTagText))
                    if (forwardMultiTagList.Count>0)
                    {
                        rtb.Selection.Text = new String('X', GetMaxMultiTagLength(forward: true));
                    }
                }
                if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                {
                    //if (!String.IsNullOrEmpty(ReverseMultiTagText))
                    if (reverseMultiTagList.Count>0)
                    {
                        rtb.Selection.Text = new String('X', GetMaxMultiTagLength(forward: false));
                    }
                }

                rtb.Selection.ApplyPropertyValue(Inline.BackgroundProperty, MultiTagHighlight);

            }
        }

        private void HighlightMultiTagFlank()
        {
            if (!string.IsNullOrEmpty(MultiFlankLengthStr))
            {
                foreach (RichTextBox rtb in new RichTextBox[2] { forwardRichTextBox, reverseRichTextBox })
                {
                    TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                    string read = textRange.Text;

                    //Find end of multi-tag
                    Regex multiRegEx = new Regex("^Z*X*");
                    string multiMatch = multiRegEx.Match(read).Value;
                    //AddOutputText($"multiMatch: {multiMatch}");
                    int firstNonX = multiMatch.Length;


                    TextPointer startPointer = GetTextPointerAtOffset(rtb, firstNonX);
                    TextPointer endPointer;
                    endPointer = GetTextPointerAtOffset(rtb, firstNonX + multiFlankLength);

                    rtb.Selection.Select(startPointer, endPointer);

                    rtb.Selection.ApplyPropertyValue(Inline.BackgroundProperty, FlankHighlight);

                    if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                    {
                        forwardMultiFlankStr = rtb.Selection.Text;
                        AddOutputText($"forwardMultiFlankStr: {forwardMultiFlankStr}");
                    }
                    if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                    {
                        reverseMultiFlankStr = rtb.Selection.Text;
                        AddOutputText($"reverseMultiFlankStr: {reverseMultiFlankStr}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Set Multiplex Tag Flanking Length and try again.");
            }

        }

        private void HighlightLineageTag()
        {
            foreach (RichTextBox rtb in new RichTextBox[2] { forwardRichTextBox, reverseRichTextBox })
            {
                TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                string read = textRange.Text;

                //The Lineage tag is indicated by N's but can have constant (non-N) bases in it.
                //    So, start by finding the first 'N' character
                //        and the first run of 5 non-N characters after that
                Regex tagStartRegEx = new Regex("^.+?N");
                string tagStartMatch = tagStartRegEx.Match(read).Value;
                //AddOutputText($"tagStartMatch: {tagStartMatch}");
                int tagStart = tagStartMatch.Length - 1;

                Regex tagEndRegEx = new Regex("^.+?N.+?N[^N]{5}");
                string tagEndMatch = tagEndRegEx.Match(read).Value;
                //AddOutputText($"tagEndMatch: {tagEndMatch}");
                int tagEnd = tagEndMatch.Length - 5;


                TextPointer startPointer = GetTextPointerAtOffset(rtb, tagStart);
                TextPointer endPointer = GetTextPointerAtOffset(rtb, tagEnd);

                rtb.Selection.Select(startPointer, endPointer);

                rtb.Selection.ApplyPropertyValue(Inline.BackgroundProperty, LineageTagHighlight);
                rtb.Selection.ApplyPropertyValue(Inline.FontWeightProperty, FontWeights.Bold);

                //Write lineage tag string to appropriate variable
                if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                {
                    forwardLinTag = rtb.Selection.Text;
                    ForwardLinTagLengthStr = $"{rtb.Selection.Text.Length}";
                    AddOutputText($"forwardLinTag: {forwardLinTag}");
                }
                if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                {
                    reverseLinTag = rtb.Selection.Text;
                    ReverseLinTagLengthStr = $"{rtb.Selection.Text.Length}";
                    AddOutputText($"reverseLinTag: {reverseLinTag}");
                }

                //Also highlight the flanking sequences used for matching
                if (!string.IsNullOrEmpty(LinTagFlankLengthStr))
                {
                    startPointer = GetTextPointerAtOffset(rtb, tagStart - linTagFlankLength);
                    endPointer = GetTextPointerAtOffset(rtb, tagStart);
                    rtb.Selection.Select(startPointer, endPointer);
                    rtb.Selection.ApplyPropertyValue(Inline.BackgroundProperty, FlankHighlight);
                    if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                    {
                        forwardLinTagFlankStrs[0] = rtb.Selection.Text;
                        //AddOutputText($"forwardLinTagFlankStrs[0]: {forwardLinTagFlankStrs[0]}");
                    }
                    if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                    {
                        reverseLinTagFlankStrs[0] = rtb.Selection.Text;
                        //AddOutputText($"reverseLinTagFlankStrs[0]: {reverseLinTagFlankStrs[0]}");
                    }

                    startPointer = GetTextPointerAtOffset(rtb, tagEnd);
                    endPointer = GetTextPointerAtOffset(rtb, tagEnd + linTagFlankLength);
                    rtb.Selection.Select(startPointer, endPointer);
                    rtb.Selection.ApplyPropertyValue(Inline.BackgroundProperty, FlankHighlight);
                    if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                    {
                        forwardLinTagFlankStrs[1] = rtb.Selection.Text;
                        //AddOutputText($"forwardLinTagFlankStrs[1]: {forwardLinTagFlankStrs[1]}");
                    }
                    if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                    {
                        reverseLinTagFlankStrs[1] = rtb.Selection.Text;
                        //AddOutputText($"reverseLinTagFlankStrs[1]: {reverseLinTagFlankStrs[1]}");
                    }
                }
                else
                {
                    MessageBox.Show("Set Lineage Tag Flanking Length and try again.");
                }
                
                //Also set spacer lengths in this method
                Regex multiRegEx = new Regex("^Z*X*");
                string multiMatch = multiRegEx.Match(read).Value;
                //AddOutputText($"multiMatch: {multiMatch}");
                int firstNonX = multiMatch.Length;

                int spacerLength = tagStart - firstNonX;
                double dMin = spacerLength * (1 - spacerDelRate / 100.0);
                int minLength = (int)Math.Round(dMin);
                double dMax = spacerLength * (1 + spacerInsRate / 100.0);
                int maxLength = (int)Math.Round(dMax);

                string spacerStr = "";
                if (minLength != spacerLength) spacerStr += $"{minLength}-";
                spacerStr += $"{spacerLength}";
                if (maxLength != spacerLength) spacerStr += $"-{maxLength}";

                if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                {
                    ForwardSpacerLengthStr = spacerStr;
                    //AddOutputText($"forwardSpacerLength: {forwardSpacerLength}");
                }
                if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                {
                    ReverseSpacerLengthStr = spacerStr;
                    //AddOutputText($"reverseSpacerLength: {reverseSpacerLength}");
                }
            }
        }

        private int GetMaxMultiTagLength(bool forward)
        {
            int maxLength = 0;
            List<string> tagList;
            if (forward)
            {
                tagList = forwardMultiTagList;
            }
            else
            {
                tagList = reverseMultiTagList;
            }

            foreach (string tag in tagList)
            {
                if (tag.Length > maxLength) maxLength = tag.Length;
            }

            return maxLength;
        }

        private static TextPointer GetTextPointerAtOffset(RichTextBox richTextBox, int offset)
        {
            //From: https://stackoverflow.com/questions/2565783/wpf-flowdocument-absolute-character-position
            var navigator = richTextBox.Document.ContentStart;
            int cnt = 0;

            while (navigator.CompareTo(richTextBox.Document.ContentEnd) < 0)
            {
                switch (navigator.GetPointerContext(LogicalDirection.Forward))
                {
                    case TextPointerContext.ElementStart:
                        break;
                    case TextPointerContext.ElementEnd:
                        if (navigator.GetAdjacentElement(LogicalDirection.Forward) is Paragraph)
                            cnt += 2;
                        break;
                    case TextPointerContext.EmbeddedElement:
                        // TODO: Find out what to do here?
                        cnt++;
                        break;
                    case TextPointerContext.Text:
                        int runLength = navigator.GetTextRunLength(LogicalDirection.Forward);

                        if (runLength > 0 && runLength + cnt < offset)
                        {
                            cnt += runLength;
                            navigator = navigator.GetPositionAtOffset(runLength);
                            if (cnt > offset)
                                break;
                            continue;
                        }
                        cnt++;
                        break;
                }

                if (cnt > offset)
                    break;

                navigator = navigator.GetPositionAtOffset(1, LogicalDirection.Forward);

            } // End while.

            return navigator;
        }

        private void outputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            outputTextBox.ScrollToEnd();
        }

    }
}
