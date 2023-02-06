using PhilipsPatternRom.Converter.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter
{
    public static class Utility
    {
        public enum SampleType
        {
            BackPorch,
            Centre,
            FrontPorch
        }

        public static List<Tuple<byte, byte, byte>> LoadVectors(RomManager manager, GeneratorStandard generatorStandard)
        {
            var entries = new List<Tuple<byte, byte, byte>>();

            if (generatorStandard == GeneratorStandard.PAL_16_9)
            {
                // Vector table is a different format for 16:9 units. Only two bytes are used per line - Address high and Control
                // Kludge it into the same structure used for 4:3
                for (int i = 0; i < manager.VectorTable.Count; i += 2)
                    entries.Add(new Tuple<byte, byte, byte>(manager.VectorTable[i + 1], manager.VectorTable[i + 0], manager.VectorTable[i + 1]));
            }
            else
            {
                for (int i = 0; i < manager.VectorTable.Count; i += 3)
                    entries.Add(new Tuple<byte, byte, byte>(manager.VectorTable[i + 0], manager.VectorTable[i + 1], manager.VectorTable[i + 2]));
            }

            return entries;
        }

        public static int DecodeVector(Tuple<byte, byte, byte> vector, SampleType type, int romsPerComponent)
        {
            byte[] lsbSequence = null;

            switch (vector.Item1 & 0x03)
            {
                case 0:
                    lsbSequence = new byte[] { 0x00, 0x00, 0x40 };
                    break;
                case 1:
                    lsbSequence = new byte[] { 0x00, 0x80, 0x40 };
                    break;
                case 2:
                    lsbSequence = new byte[] { 0x80, 0x00, 0xC0 };
                    break;
                case 3:
                    lsbSequence = new byte[] { 0x80, 0x80, 0xC0 };
                    break;
            }

            switch (type)
            {
                case SampleType.BackPorch:
                    return (vector.Item2 << 8 | lsbSequence[0]) * romsPerComponent;
                case SampleType.Centre:
                    return (vector.Item3 << 8 | lsbSequence[1]) * romsPerComponent;
                case SampleType.FrontPorch:
                    return (vector.Item2 << 8 | lsbSequence[2]) * romsPerComponent;
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
