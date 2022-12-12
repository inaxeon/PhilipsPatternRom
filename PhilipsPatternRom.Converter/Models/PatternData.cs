using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class PatternData
    {
        public PatternData()
        {
            Fragments = new Dictionary<int, PatternFragment>();
        }

        public Dictionary<int, PatternFragment> Fragments { get; set; }
    }
}
