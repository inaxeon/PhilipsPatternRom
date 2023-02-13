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
                finalFileName = allFiles.First(el => regex.Match(el).Success);
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

            if (FileName.Contains("\\"))
            {
                // It's a regex. Replace with checksum
                finalFileName = finalFileName.Replace("[A-F0-9]+\\", string.Format("{0:X4}", ComputeChecksum16(Data)));
            }

            File.WriteAllBytes(Path.GetFileNameWithoutExtension(finalFileName) + ".BIN", Data);
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
