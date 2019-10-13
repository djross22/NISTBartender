using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Windows.Controls;
using System.Globalization;

namespace BartenderWindow
{
    //code borrowed from: https://www.codeproject.com/Articles/15610/Regex-Validation-in-WPF
    //Extension that validates against each line of multi-line string
    class MultiLineValidationRule : ValidationRule
    {
        public string RegexText { get; set; }
        public string ErrorMessage { get; set; }
        public RegexOptions ValidationOptions { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            ValidationResult result = ValidationResult.ValidResult;

            // If there is no regular expression to evaluate,
            // then the data is considered to be valid.
            if (!String.IsNullOrEmpty(this.RegexText))
            {
                // Cast the input value to a string (null becomes empty string).
                string text = value as string ?? String.Empty;

                //Break string into lines
                string[] textArr = text.Split();

                using (StringReader reader = new StringReader(text))
                {
                    string line = string.Empty;
                    do
                    {
                        line = reader.ReadLine();
                        //if (line != null)
                        if (!String.IsNullOrEmpty(line))
                        {
                            // If the each string does not match the regex, return a value
                            // which indicates failure and provide an error mesasge.
                            if (!Regex.IsMatch(line, this.RegexText, this.ValidationOptions))
                            {
                                return new ValidationResult(false, this.ErrorMessage);
                            }
                        }

                    } while (line != null);
                }
 
            }

            return result;
        }
    }
}
