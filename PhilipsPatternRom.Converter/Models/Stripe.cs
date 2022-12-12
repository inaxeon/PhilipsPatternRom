using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace PhilipsPatternRom.Converter.Models
{
    public class Stripe : IEquatable<Stripe>
    {
        public string CentreAddress { get; set; }
        public int StartAddress { get; set; }
        public int EndAddress { get; set; }

        [XmlIgnore]
        public int Line { get; set; }

        public bool Equals(Stripe other)
        {
            return StartAddress == other.StartAddress && EndAddress == other.EndAddress;
        }

        public override int GetHashCode()
        {
            return StartAddress | EndAddress << 16;
        }
    }
}
