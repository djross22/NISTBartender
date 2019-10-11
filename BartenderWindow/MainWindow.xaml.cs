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

        private string outputText;

        #region Properties Getters and Setters
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
            reverseComplementButton_Click(sender, e);


        }
    }
}
