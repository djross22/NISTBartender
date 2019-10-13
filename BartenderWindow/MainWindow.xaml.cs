﻿using System;
using System.Collections.Generic;
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

namespace BartenderWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private string forUmiTagLenStr, revUmiTagLenStr;
        private int[] forUmiTagLen, revUmiTagLen;
        private string readLengthStr;
        private int readLength;

        private string fowardMultiTagText, reverseMultiTagText, extraMultiTagText;
        private List<string> fowardMultiTagList, reverseMultiTagList, extraMultiTagList, multiTagList;

        private string outputText;

        private static Brush UmiTagHighlight = Brushes.Yellow;
        private static Brush MultiTagHighlight = Brushes.LightGreen;

        #region Properties Getters and Setters
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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            CopyReverseComplement();
            ReadLengthStr = "150";
            ForUmiTagLenStr = "";
            RevUmiTagLenStr = "";

            FowardMultiTagText = "AGCTAGCTAG, A\n";
            FowardMultiTagText += "CAATGCCTAG, B\n";
            ReverseMultiTagText = "TAATGCCGTG, 1\n";
            ReverseMultiTagText += "GGGCAATGCG, 2\n";
            ExtraMultiTagText = "AGAAGGTAG, TAGTGTCGTG, S5\n";
            ExtraMultiTagText = "AGAAGGTAG, GGGCAATGCG, S6\n";

            fowardMultiTagList = new List<string>();
            reverseMultiTagList = new List<string>();
            extraMultiTagList = new List<string>();
            multiTagList = new List<string>();
        }

        private void MakeMultiTagLists()
        {
            //If multitag text boxes aren't properly populated
            //if ()
        }

        private void SetReadLength()
        {
            readLength = Int32.Parse(this.readLengthStr);
            OutputText += $"Read length: {readLength}.\n";

            UnderLineReadLength();
        }

        private void SetUmiTagLength(bool forward)
        {
            string umiLenStr;
            int[] umiLenArr;
            if (forward)
            {
                umiLenStr = forUmiTagLenStr;
                if (umiLenStr == "")
                {
                    forUmiTagLen = null;
                    OutputText += $"Forward UMI tag length: \n";
                    return;
                }
            }
            else
            {
                umiLenStr = revUmiTagLenStr;
                if (umiLenStr == "")
                {
                    revUmiTagLen = null;
                    OutputText += $"Reverse UMI tag length: \n";
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
                OutputText += $"Forward UMI tag length: ";
            }
            else
            {
                revUmiTagLen = umiLenArr;
                OutputText += $"Reverse UMI tag length: ";
            }

            foreach (int i in umiLenArr) {
                OutputText += $"{i}, ";
            }
            OutputText += "\n";
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
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

        private void NewMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OpenMenuItme_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void reverseComplementButton_Click(object sender, RoutedEventArgs e)
        {
            CopyReverseComplement();
        }

        private void CopyReverseComplement()
        {
            clearWhiteSpaces();

            TextRange textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
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

            HighlightMultiTag();

            UnderLineReadLength();

        }

        private void ClearSequenceFormatting()
        {
            foreach (RichTextBox rtb in new RichTextBox[2] { forwardRichTextBox , reverseRichTextBox })
            {
                rtb.Selection.ClearAllProperties();
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
                int firstNonZ = 0;
                foreach (char c in read.ToCharArray())
                {
                    if (c != 'Z')
                    {
                        break;
                    }
                    firstNonZ++;
                }

                OutputText += $"firstNonZ: {firstNonZ}\n";

                TextPointer startPointer = rtb.Document.ContentStart.GetPositionAtOffset(0);
                TextPointer endPointer = GetTextPointerAtOffset(rtb, firstNonZ);

                rtb.Selection.Select(startPointer, endPointer);

                //If UMI tag length is set in the GUI then use that value - and replace/insert the appropriate number of Zs
                //    otherwise, use number of Z'z in sequence to set value for UMI tag length
                if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                {
                    //OutputText += $"ForUmiTagLenStr: {ForUmiTagLenStr}\n";
                    if (ForUmiTagLenStr == "")
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
                    //OutputText += $"RevUmiTagLenStr: {RevUmiTagLenStr}\n";
                    if (RevUmiTagLenStr == "")
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

        private void outputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            outputTextBox.ScrollToEnd();
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
                int firstNonZ = 0;
                foreach (char c in read.ToCharArray())
                {
                    if (c != 'Z')
                    {
                        break;
                    }
                    firstNonZ++;
                }
                int firstNonX = 0;
                foreach (char c in read.ToCharArray())
                {
                    if (c != 'Z' && c != 'X')
                    {
                        break;
                    }
                    firstNonX++;
                }

                //Check if X'x ans Z's are in expected order
                if (firstNonX <= firstNonZ)
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

                OutputText += $"firstNonX: {firstNonX}\n";

                TextPointer startPointer = rtb.Document.ContentStart.GetPositionAtOffset(0);
                startPointer = GetTextPointerAtOffset(rtb, firstNonZ);
                TextPointer endPointer = GetTextPointerAtOffset(rtb, firstNonX);

                rtb.Selection.Select(startPointer, endPointer);

                //If multiplexing tags are set in the GUI then use the length from those strings - and replace/insert the appropriate number of X's
                if (Object.ReferenceEquals(rtb, forwardRichTextBox))
                {
                    if (false) //multi-tag list has been set up in the GUI
                    {
                        //rtb.Selection.Text = new String('X', forMultiTagLen.Last());
                    }
                    else
                    {
                        
                    }
                }
                if (Object.ReferenceEquals(rtb, reverseRichTextBox))
                {
                    if (false) //multi-tag list has been set up in the GUI
                    {
                        //rtb.Selection.Text = new String('X', revMultiTagLen.Last());
                    }
                    else
                    {

                    }
                }

                rtb.Selection.ApplyPropertyValue(Inline.BackgroundProperty, MultiTagHighlight);
            }
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


    }
}
