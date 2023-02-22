using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class LineSamples
    {
        public LineSamples(List<ushort> samplesY, List<ushort> samplesRY, List<ushort> samplesBY)
        {
            SamplesY = samplesY;
            SamplesRy = samplesRY;
            SamplesBy = samplesBY;

            LineHashCode = SamplesY.GetHashCode() + SamplesRy.GetHashCode() + SamplesBy.GetHashCode();
        }

        public List<ushort> SamplesY { get; set; }
        public List<ushort> SamplesRy { get; set; }
        public List<ushort> SamplesBy { get; set; }

        public long LineHashCode { get; private set; }

        public void Do422Conversion()
        {
            var ryCopy = new List<ushort>(SamplesRy);
            SamplesRy.Clear();

            for (int i = 0; i < ryCopy.Count; i += 2)
                SamplesRy.Add((ushort)((ryCopy[i] + ryCopy[i + 1]) / 2));

            var byCopy = new List<ushort>(SamplesBy);
            SamplesBy.Clear();

            for (int i = 0; i < byCopy.Count; i += 2)
                SamplesBy.Add((ushort)((byCopy[i] + byCopy[i + 1]) / 2));

            LineHashCode = SamplesY.GetHashCode() + SamplesRy.GetHashCode() + SamplesBy.GetHashCode();
        }
    }
}
