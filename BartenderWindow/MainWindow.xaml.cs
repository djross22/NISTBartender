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

namespace BartenderWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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

        private void clearWhiteSpaces()
        {
            TextRange textRange;

            textRange = new TextRange(forwardRichTextBox.Document.ContentStart, forwardRichTextBox.Document.ContentEnd);
            textRange.Text = RemoveStringWhitespace(textRange.Text);

            textRange = new TextRange(reverseRichTextBox.Document.ContentStart, reverseRichTextBox.Document.ContentEnd);
            textRange.Text = RemoveStringWhitespace(textRange.Text);
        }
    }
}
