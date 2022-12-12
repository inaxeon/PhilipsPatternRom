using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public class StripeSet
    {
        public int HorizontalStart { get; set; }
        public int HorizontalEnd { get; set; }

        public int VerticalStart { get; set; }
        public int VerticalEnd { get; set; }

        public List<Stripe> Stripes { get; set; }
    }
}
