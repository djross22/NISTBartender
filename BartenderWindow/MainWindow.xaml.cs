using System;
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

        private string outputText;

        private static Brush UmiTagHighlight = Brushes.Yellow;

        #region Properties Getters and Setters
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
            CopyReverseComplement();
            ReadLengthStr = "150";

            DataContext = this;
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
            }
            else
            {
                umiLenStr = revUmiTagLenStr;
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

            UnderLineReadLength();


            HighlightUmiTag();

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

                endPointer = GetPointerFromCharOffset(readLength, startPointer, rtb.Document);

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
                OutputText += $"firstNonZ: {firstNonZ}\n";

                TextPointer startPointer = rtb.Document.ContentStart.GetPositionAtOffset(0);
                TextPointer endPointer = GetPointerFromCharOffset(firstNonZ, startPointer, rtb.Document);

                rtb.Selection.Select(startPointer, endPointer);

                rtb.Selection.ApplyPropertyValue(Inline.BackgroundProperty, UmiTagHighlight);

                //rtb.Selection.ClearAllProperties();

                //if (textRange.Text.Length > readLength)
                //{
                //    endPointer = rtb.Document.ContentStart.GetPositionAtOffset(readLength);
                //}
                //rtb.Selection.Select(startPointer, endPointer);
                //rtb.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
            }
        }

        private TextPointer GetPointerFromCharOffset(int charOffset, TextPointer startPointer, FlowDocument document)
        {
            //modified version of method from: https://social.msdn.microsoft.com/Forums/vstudio/en-US/bc67d8c5-41f0-48bd-8d3d-79159e86b355/textpointer-into-a-flowdocument-based-on-character-index?forum=wpf
            TextPointer navigator = startPointer;

            if (charOffset == 0)
            {
                return navigator;
            }

            TextPointer nextPointer = navigator;
            int counter = 0;
            while (nextPointer != null && counter < charOffset)
            {
                if (nextPointer.CompareTo(document.ContentEnd) == 0)
                {
                    // If we reach to the end of document, return the EOF pointer.
                    return nextPointer;
                }

                if (nextPointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    // Only incremennt the character counter if the current pointer is pointing at a character.
                    counter++;
                }
                nextPointer = nextPointer.GetNextInsertionPosition(LogicalDirection.Forward);
            }

            return nextPointer;
        }

    }
}
