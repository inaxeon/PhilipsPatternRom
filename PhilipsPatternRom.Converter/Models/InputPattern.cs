using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class InputPattern
    {
        public InputPattern(string directory, bool isDigital, bool isAntiPal, PatternFixType fixes)
        {
            Directory = directory;
            IsDigital = isDigital;
            IsAntiPal = isAntiPal;
            Fixes = fixes;
        }

        public string Directory { get; set; }
        public bool IsAntiPal { get; set; }
        public bool IsDigital { get; set; }
        public PatternFixType Fixes { get; set; }
    }
}
