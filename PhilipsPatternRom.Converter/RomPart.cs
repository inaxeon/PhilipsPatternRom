using PhilipsPatternRom.Converter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter
{
    public class RomPart
    {
        private string _fileName;
        private int _offset;

        public int Length { get; private set; }
        public RomType Type { get; private set; }
        public byte[] Data { get; set; }

        public RomPart(RomType type, string filename, int offset, int length)
        {
            Type = type;
            _fileName = filename;
            _offset = offset;
            Length = length;
        }

        public void Load()
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(_fileName, FileMode.Open)))
            {
                Data = new byte[Length];
                reader.BaseStream.Seek(_offset, SeekOrigin.Begin);
                reader.Read(Data, 0, Length);
            }
        }

        public void Save()
        {
            File.WriteAllBytes(Path.GetFileNameWithoutExtension(_fileName) + ".bin", Data);
        }
    }
}
