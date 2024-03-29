﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Windows.Controls;
using System.Globalization;

namespace BartenderWindow
{
    //code borrowed from: https://www.codeproject.com/Articles/15610/Regex-Validation-in-WPF
    class RegexValidationRule : ValidationRule
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

                // If the string does not match the regex, return a value
                // which indicates failure and provide an error mesasge.
                if (!Regex.IsMatch(text, this.RegexText, this.ValidationOptions))
                    result = new ValidationResult(false, this.ErrorMessage);
            }

            return result;
        }
    }
}
