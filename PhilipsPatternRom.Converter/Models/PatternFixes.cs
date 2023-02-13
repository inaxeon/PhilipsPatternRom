using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Models
{
    [Flags]
    public enum PatternFixes
    {
        FixCircle16x9Clock = (1 << 0), //Remove clock cut-out
        FixCircle16x9Ap = (1 << 1), //Convert digital anti-PAL to analogue
        FixFubk16x9Centre = (1 << 2) //Convert digital anti-PAL to analogue
    }
}
