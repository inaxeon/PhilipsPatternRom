using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class LineSamples
    {
        public LineSamples(List<ushort> samplesY, List<ushort> samplesRY, List<ushort> samplesBY, bool calculateHash)
        {
            SamplesY = samplesY;
            SamplesRy = samplesRY;
            SamplesBy = samplesBY;
            _calculateHash = calculateHash;

            UpdateHash();
        }

        public List<ushort> SamplesY { get; set; }
        public List<ushort> SamplesRy { get; set; }
        public List<ushort> SamplesBy { get; set; }

        public long LineHashCode { get; private set; }
        private bool _calculateHash { get; set; }

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

            UpdateHash();
        }

        private void UpdateHash()
        {
            if (!_calculateHash)
                return;

            int hash = SamplesY.Count;

            foreach (int sample in SamplesY)
                hash = unchecked(hash * 314159 + sample);

            LineHashCode = hash;

            foreach (int sample in SamplesRy)
                hash = unchecked(hash * 314160 + sample);

            LineHashCode += hash;

            foreach (int sample in SamplesBy)
                hash = unchecked(hash * 314161 + sample);

            LineHashCode += hash;
        }
    }
}
