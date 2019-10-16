using System;
using System.Collections.Generic;
using System.Text;

namespace BarcodeParser
{
    public interface IDisplaysOutputText
    {
        public void DisplayOutput(string text, bool newLine = true);
    }
}
