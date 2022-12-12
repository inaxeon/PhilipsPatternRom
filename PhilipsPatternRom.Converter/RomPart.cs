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
        private int _length;

        public RomType Type { get; private set; }
        public byte[] Data { get; set; }

        public RomPart(RomType type, string filename, int offset, int length)
        {
            Type = type;
            _fileName = filename;
            _offset = offset;
            _length = length;
        }

        public void Load()
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(_fileName, FileMode.Open)))
            {
                Data = new byte[_length];
                reader.BaseStream.Seek(_offset, SeekOrigin.Begin);
                reader.Read(Data, 0, _length);
            }
        }

        public void Save()
        {
            File.WriteAllBytes(Path.GetFileNameWithoutExtension(_fileName) + "_modified.bin", Data);
        }
    }
}
