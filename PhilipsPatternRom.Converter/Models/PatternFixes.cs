using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    public enum PatternFixes
    {
        FixCircle16x9Clock, //Remove clock cut-out
        FixCircle16x9Ap //Convert digital anti-PAL to analogue
    }
}
