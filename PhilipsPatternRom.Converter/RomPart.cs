using PhilipsPatternRom.Converter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter
{
    public class RomPart
    {
        private int _offset;

        public string FileName { get; set; }
        public int Length { get; private set; }
        public RomType Type { get; private set; }
        public byte[] Data { get; set; }

        public RomPart(RomType type, string filename, int offset, int length)
        {
            Type = type;
            FileName = filename;
            _offset = offset;
            Length = length;
        }

        public void Load()
        {
            string finalFileName = FileName;

            if (finalFileName.Contains("\\"))
            {
                // It's a regex
                var allFiles = Directory.GetFiles(".");
                var regex = new Regex(finalFileName);
                var matching = allFiles.Where(el => regex.Match(el).Success);

                if (matching.Count() > 1)
                    throw new Exception("More than one input file matches the search pattern: " + FileName);

                finalFileName = matching.Single();
            }

            using (BinaryReader reader = new BinaryReader(new FileStream(finalFileName, FileMode.Open)))
            {
                Data = new byte[Length];
                reader.BaseStream.Seek(_offset, SeekOrigin.Begin);
                reader.Read(Data, 0, Length);
            }
        }

        public void Save()
        {
            string finalFileName = FileName;

            if (Type == RomType.CPU)
            {
                // My implementation of the PM5644's "chicken-and-egg" checksum mechanism, where the checksum is included in the checksum,
                // This is why the CPU ROM's checksum always ends in 00h. There is probably a more elegant way of doing this...

                int cs = ComputeChecksum16(Data);
                Data[0xFFFE] = 0x00;
                Data[0xFFFF] = (byte)(cs >> 8);

                do
                {
                    unchecked
                    {
                        if (IsCorrectChecksum(Data))
                        {
                            // My algorithm doesn't quite get the right result first go, so do one more pass
                            cs = ComputeChecksum16(Data);
                            Data[0xFFFF] = (byte)(cs >> 8);

                            // And back off the difference byte until it's correct
                            while (!IsCorrectChecksum(Data))
                                Data[0xFFFE]--;

                            break;
                        }

                        cs = ComputeChecksum16(Data);
                        Data[0xFFFE]++;
                        Data[0xFFFF] = (byte)(cs >> 8);
                    }

                } while (true);
            }

            if (FileName.Contains("\\"))
            {
                // It's a regex. Replace with checksum
                finalFileName = finalFileName.Replace("[A-F0-9]+\\", string.Format("{0:X4}", ComputeChecksum16(Data)));
            }

            File.WriteAllBytes(Path.GetFileNameWithoutExtension(finalFileName) + ".BIN", Data);
        }

        private bool IsCorrectChecksum(byte[] buffer)
        {
            ushort cs = ComputeChecksum16(buffer);

            var sum = buffer[0xFFFF] - (byte)cs >> 8;
            var lower = (byte)(cs & 0xFF);

            return sum == 0 && lower == 0;
        }

        public static ushort ComputeChecksum16(byte[] buffer)
        {
            ushort sum = 0;
            unchecked
            {
                foreach (byte b in buffer)
                    sum += b;
            }
            return sum;
        }
    }
}
