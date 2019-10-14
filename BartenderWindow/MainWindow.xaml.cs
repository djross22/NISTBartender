using System;
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

namespace BartenderWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
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

        private string inputDirectory, outputDirectory, forwardGzFastQ, reverseGzFastQ;
        private string defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);//@"C:";

        private string forUmiTagLenStr, revUmiTagLenStr;
        private int[] forUmiTagLen, revUmiTagLen;
        private string readLengthStr;
        private int readLength;

        private string forwardLinTag, reverseLinTag;
        private string forwardLinTagLengthStr, reverseLinTagLengthStr;
        private int forwardLinTagLength, reverseLinTagLength;
        private string forLintagRegexStr, revLintagRegexStr;
        private string[] forwardLinTagFlankStrs = new string[2];
        private string[] reverseLinTagFlankStrs = new string[2];

        private string forwardMultiFlankStr, reverseMultiFlankStr;
        private string multiFlankLengthStr, linTagFlankLengthStr, forwardSpacerLengthStr, reverseSpacerLengthStr;
        private int multiFlankLength, linTagFlankLength, forwardSpacerLength, reverseSpacerLength;

        private string fowardMultiTagText, reverseMultiTagText, extraMultiTagText;
        private List<string> fowardMultiTagList, reverseMultiTagList;
        private Dictionary<string, string> fowardIdDict, reverseIdDict;
        private Dictionary<string[], string> mutiTagIdDict;

        private string outputText;

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

        #region Properties Getters and Setters

        public string MinQualityStr
        {
            get { return this.minQualityStr; }
            set
            {
                this.minQualityStr = value;
                OnPropertyChanged("MinQualityStr");
                double.TryParse(minQualityStr, out minQuality);
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
                this.paramsFilePath = value;
                UpdateTitle();
                OnPropertyChanged("ParamsFileName");
            }
        }

        public string DisplayTitle
        {
            get { return this.displayTitle; }
            set
            {
                this.displayTitle = value;
                OnPropertyChanged("DisplayTitle");
            }
        }

        public string ReverseGzFastQ
        {
            get { return this.reverseGzFastQ; }
            set
            {
                this.reverseGzFastQ = value;
                OnPropertyChanged("ReverseGzFastQ");
            }
        }
        

        public string ForwardGzFastQ
        {
            get { return this.forwardGzFastQ; }
            set
            {
                this.forwardGzFastQ = value;
                OnPropertyChanged("ForwardGzFastQ");
            }
        }

        public string OutputDirectory
        {
            get { return this.outputDirectory; }
            set
            {
                this.outputDirectory = value;
                OnPropertyChanged("OutputDirectory");
            }
        }

        public string InputDirectory
        {
            get { return this.inputDirectory; }
            set
            {
                this.inputDirectory = value;
                OnPropertyChanged("InputDirectory");
            }
        }

        public string RevLintagRegexStr
        {
            get { return this.revLintagRegexStr; }
            set
            {
                this.revLintagRegexStr = value;
                OnPropertyChanged("RevLintagRegexStr");
            }
        }

        public string ForLintagRegexStr
        {
            get { return this.forLintagRegexStr; }
            set
            {
                this.forLintagRegexStr = value;
                OnPropertyChanged("ForLintagRegexStr");
            }
        }

        public string ReverseLinTagLengthStr
        {
            get { return this.reverseLinTagLengthStr; }
            set
            {
                this.reverseLinTagLengthStr = value;
                OnPropertyChanged("ReverseLinTagLengthStr");
                int.TryParse(reverseLinTagLengthStr, out reverseLinTagLength);
            }
        }

        public string ForwardLinTagLengthStr
        {
            get { return this.forwardLinTagLengthStr; }
            set
            {
                this.forwardLinTagLengthStr = value;
                OnPropertyChanged("ForwardLinTagLengthStr");
                int.TryParse(forwardLinTagLengthStr, out forwardLinTagLength);
            }
        }

        public string ReverseSpacerLengthStr
        {
            get { return this.reverseSpacerLengthStr; }
            set
            {
                this.reverseSpacerLengthStr = value;
                OnPropertyChanged("ReverseSpacerLengthStr");
                int.TryParse(reverseSpacerLengthStr, out reverseSpacerLength);
            }
        }

        public string ForwardSpacerLengthStr
        {
            get { return this.forwardSpacerLengthStr; }
            set
            {
                this.forwardSpacerLengthStr = value;
                OnPropertyChanged("ForwardSpacerLengthStr");
                int.TryParse(forwardSpacerLengthStr, out forwardSpacerLength);
            }
        }

        public string LinTagFlankLengthStr
        {
            get { return this.linTagFlankLengthStr; }
            set
            {
                this.linTagFlankLengthStr = value;
                OnPropertyChanged("LinTagFlankLengthStr");
                int.TryParse(linTagFlankLengthStr, out linTagFlankLength);
            }
        }

        public string MultiFlankLengthStr
        {
            get { return this.multiFlankLengthStr; }
            set
            {
                this.multiFlankLengthStr = value;
                OnPropertyChanged("MultiFlankLengthStr");
                int.TryParse(multiFlankLengthStr, out multiFlankLength);
            }
        }

        public string ExtraMultiTagText
        {
            get { return this.extraMultiTagText; }
            set
            {
                this.extraMultiTagText = value;
                OnPropertyChanged("ExtraMultiTagText");
            }
        }

        public string ReverseMultiTagText
        {
            get { return this.reverseMultiTagText; }
            set
            {
                this.reverseMultiTagText = value;
                OnPropertyChanged("ReverseMultiTagText");
            }
        }

        public string FowardMultiTagText
        {
            get { return this.fowardMultiTagText; }
            set
            {
                this.fowardMultiTagText = value;
                OnPropertyChanged("FowardMultiTagText");
            }
        }

        public string ReadLengthStr
        {
            get { return this.readLengthStr; }
            set
            {
                this.readLengthStr = value;
                OnPropertyChanged("ReadLengthStr");
                SetReadLength();
            }
        }

        public string OutputText
        {
            get { return this.outputText; }
            set
            {
                this.outputText = value;
                OnPropertyChanged("OutputText");
            }
        }

        public string RevUmiTagLenStr
        {
            get { return this.revUmiTagLenStr; }
            set
            {
                this.revUmiTagLenStr = value;
                OnPropertyChanged("RevUmiTagLenStr");
                SetUmiTagLength(forward: false);
            }
        }

        public string ForUmiTagLenStr
        {
            get { return this.forUmiTagLenStr; }
            set
            {
                this.forUmiTagLenStr = value;
                OnPropertyChanged("ForUmiTagLenStr");
                SetUmiTagLength(forward: true);
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
            }
        }

        public MainWindow()
        {
            InitializeComponent();
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

            //CopyReverseComplement();

            //TextRange textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
            //textRange.Text = "ZZZZZZZZXXXXXXXXXXCATCGGTGAGCCCGGGCTGTCGGCGTNNTNNNANNTNNNANNTNNNANNTNNNANNTNNNANNATATGCCAGCAGGCCGGCCACGCTNNTNNNANNTNNNANNANNNANNTNNNANNTNNNANNCGGTGGCCCGGGCGGCCGCACGATGCGTCCGGCGTAGAGGXXXXXXXXXXZZZZZZZZ";

            ReadLengthStr = "150";
            //ForUmiTagLenStr = "";
            //RevUmiTagLenStr = "";

            LinTagFlankLengthStr = "4";
            MultiFlankLengthStr = "4";

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
            fowardMultiTagList = new List<string>();
            fowardIdDict = new Dictionary<string, string>();
            reverseMultiTagList = new List<string>();
            reverseIdDict = new Dictionary<string, string>();
            mutiTagIdDict = new Dictionary<string[], string>();
        }

        private void CreateParamsList()
        {
            paramsList = new List<string>();
            paramsList.Add("ReverseGzFastQ");
            paramsList.Add("ForwardGzFastQ");
            paramsList.Add("OutputDirectory");
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
            //paramsList.Add("");
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
                    fowardMultiTagList.Add(splitTag[0]);
                    if (splitTag.Length > 1)
                    {
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
                        reverseIdDict[splitTag[0]] = splitTag[1];
                    }
                    else
                    {
                        reverseIdDict[splitTag[0]] = $"_{splitTag[0]}";
                    }
                }

                //Then combine Forward and Reverse tags in all possible ways and add IDs to mutiTagIDDict
                foreach (string forTag in fowardMultiTagList)
                {
                    foreach (string revTag in reverseMultiTagList)
                    {
                        string[] keys = new string[2] { forTag, revTag };
                        string value = $"{fowardIdDict[forTag]}{reverseIdDict[revTag]}";
                        value = value.Replace("__", "_");
                        mutiTagIdDict[keys] = value;
                    }
                }
            }


            //Then add individual tags from ExtraMultiTagText - and add mathcing IDs to mutiTagIDDict
            if (validExtraTags)
            {
                string[] extraTagArr = ExtraMultiTagText.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string tagPlusId in extraTagArr)
                {
                    string[] splitTag = tagPlusId.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string forTag = splitTag[0];
                    string revTag = splitTag[1];
                    string[] keys = new string[2] { forTag, revTag };
                    fowardMultiTagList.Add(forTag);
                    reverseMultiTagList.Add(revTag);

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
            foreach (string[] keys in mutiTagIdDict.Keys)
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

        static string ReverseComplement(string inputSequence)
        {
            string outputString = inputSequence.TrimEnd('\r', '\n');
            outputString = RemoveStringWhitespace(outputString);

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

        static string RemoveStringWhitespace(string input)
        {
            string output = new string(input.ToCharArray()
                .Where(c => !Char.IsWhiteSpace(c))
                .ToArray());

            output = output.Replace("\n", "");
            output = output.Replace("\r", "");

            return output;
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
            foreach (string param in paramsList)
            {
                PropertyInfo propInfo = this.GetType().GetProperty(param);
                if (propInfo != null)
                {
                    value = $"{propInfo.GetValue(this)}";
                    paramNode = xmlDoc.CreateElement(param);
                    paramNode.InnerText = value;
                    rootNode.AppendChild(paramNode);
                }
                
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
            foreach (string param in paramsList)
            {
                paramNode = rootNode.SelectSingleNode($"descendant::{param}");
                PropertyInfo propInfo = this.GetType().GetProperty(param);

                if (propInfo != null && paramNode != null)
                {
                    string value = paramNode.InnerText;
                    propInfo.SetValue(this, value);
                }

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

        private void CopyReverseComplement()
        {
            clearWhiteSpaces();

            TextRange textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
            textRange.Text = textRange.Text.ToUpper();
            string forwardSequence = textRange.Text;

            string reverseSequence = ReverseComplement(forwardSequence);

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
        
        private void autoRegexButton_Click(object sender, RoutedEventArgs e)
        {
            string regExStr = RegExStrWithOneSnip(forwardLinTagFlankStrs[0]);
            regExStr += forwardLinTag.Replace('N', '.');
            regExStr += RegExStrWithOneSnip(forwardLinTagFlankStrs[1]);
            ForLintagRegexStr = regExStr;

            regExStr = RegExStrWithOneSnip(reverseLinTagFlankStrs[0]);
            regExStr += reverseLinTag.Replace('N', '.');
            regExStr += RegExStrWithOneSnip(reverseLinTagFlankStrs[1]);
            RevLintagRegexStr = regExStr;
        }


        private string RegExStrWithOneSnip(string seq)
        {
            string regExStr = $"({seq}|";
            char[] seqChars = seq.ToCharArray();
            for (int i=0; i<seqChars.Length; i++)
            {
                for (int j=0; j<seqChars.Length; j++)
                {
                    if (i==j)
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
            textRange.Text = RemoveStringWhitespace(textRange.Text);

            textRange = new TextRange(reverseRichTextBox.Document.ContentStart, reverseRichTextBox.Document.ContentEnd);
            textRange.Text = RemoveStringWhitespace(textRange.Text);
        }

        private void analyzeButton_Click(object sender, RoutedEventArgs e)
        {
            //If UMI tag length boxes are empty, auto-polulate them
            //If UMI tag length boxes have values, use those values to add appropriate number of Z's at beginning of each sequence
            TextRange textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
            textRange.Text = RemoveStringWhitespace(textRange.Text);

            reverseComplementButton_Click(sender, e);

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
                    if (fowardMultiTagList.Count>0)
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

                if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                {
                    ForwardSpacerLengthStr = $"{tagStart - firstNonX}";
                    //AddOutputText($"forwardSpacerLength: {forwardSpacerLength}");
                }
                if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                {
                    ReverseSpacerLengthStr = $"{tagStart - firstNonX}";
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
                tagList = fowardMultiTagList;
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
