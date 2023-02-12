using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class InputPattern
    {
        public InputPattern(string directory, bool isAntiPal, PatternFixes fixes)
        {
            Directory = directory;
            IsAntiPal = isAntiPal;
            Fixes = fixes;
        }

        public string Directory { get; set; }
        public bool IsAntiPal { get; set; }
        public PatternFixes Fixes { get; set; }
    }
}
