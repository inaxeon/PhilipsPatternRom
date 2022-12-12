using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class PatternFragment : IEquatable<PatternFragment>
    {
        public int Address { get; set; }
        public int Length { get; set; }

        public List<ushort> Luminance { get; set; }
        public List<byte> ChrominanceRY { get; set; }
        public List<byte> ChrominanceBY { get; set; }

        public bool Equals(PatternFragment other)
        {
            return Address == other.Address && Length == other.Length;
        }

        public override int GetHashCode()
        {
            var hashCode = 1444564768;
            hashCode = hashCode * -1521134295 + Address.GetHashCode();
            hashCode = hashCode * -1521134295 + Length.GetHashCode();
            return hashCode;
        }
    }
}
