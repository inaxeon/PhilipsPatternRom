using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class ConvertedPattern
    {
        public Dictionary<int, Tuple<byte, byte, byte>> VectorTable { get; set; }
    }
}
