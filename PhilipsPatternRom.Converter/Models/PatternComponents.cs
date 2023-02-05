using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class PatternComponents
    {
        public GeneratorStandard Standard { get; set; }
        public Bitmap Luma { get; set; }
        public Bitmap ChromaRy { get; set; }
        public Bitmap ChromaBy { get; set; }
    }
}
