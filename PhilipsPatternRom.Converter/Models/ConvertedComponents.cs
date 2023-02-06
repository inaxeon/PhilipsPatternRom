using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class ConvertedComponents
    {
        public List<int> SamplesY { get; set; }
        public List<int> SamplesRy { get; set; }
        public List<int> SamplesBy { get; set; }
        public Dictionary<int, Tuple<byte, byte, byte>> VectorTable { get; set; }
        public int NextOffset { get; set; }
        public int NextLine { get; set; }
    }
}
