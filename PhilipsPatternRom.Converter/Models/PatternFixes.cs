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
        FixCircle16x9BottomBox = (1 << 2), // PM5644 style (blank) bottom box
        FixFubk16x9Centre = (1 << 3) //Convert digital anti-PAL to analogue
    }
}
