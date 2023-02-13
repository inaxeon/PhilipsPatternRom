using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class Generator
    {
        public GeneratorType Type { get; set; }
        public GeneratorStandard Standard { get; set; }
        public List<RomPart> RomParts { get; set; }
        public int VectorTableStart { get; set; }
        public int VectorTableLength { get; set; }
    }
}
