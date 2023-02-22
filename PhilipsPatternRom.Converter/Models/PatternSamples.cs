using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class PatternSamples
    {
        public List<LineSamples> Frame0 { get; set; }
        public List<LineSamples> Frame1 { get; set; }

        public void Do422Conversion()
        {
            foreach (var line in Frame0)
                line.Do422Conversion();

            if (Frame1 != null)
            {
                foreach (var line in Frame1)
                    line.Do422Conversion();
            }
        }
    }
}
