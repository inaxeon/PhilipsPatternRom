using PhilipsPatternRom.Converter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter
{
    public class RomGenerator
    {
        private RomManager _romManager;

        private const int _linesPerField = 290;
        private const int _vectorTableLength = (0xD98 / 3 /* bytes per entry */);
        private const int _lineLength = 1024;

        private List<ushort> _ySamples;
        private List<ushort> _rySamples;
        private List<ushort> _bySamples;

        private Dictionary<int, Tuple<byte, byte, byte>> _vectorEntries;

        private int _targetOffset;

        public RomGenerator()
        {
            _romManager = new RomManager();
        }

        public void LoadROMs(GeneratorType type, string directory)
        {
            _romManager.OpenSet(type, directory);

            _targetOffset = _romManager.RomSize; // Starting offset for new patterns
        }

        public void AddAntiPal(string directory)
        {
            Init();
            LoadSamples(directory);
            GenerateVectorTable();
        }

        private void Init()
        {
            _vectorEntries = new Dictionary<int, Tuple<byte, byte, byte>>();
            _ySamples = new List<ushort>();
            _rySamples = new List<ushort>();
            _bySamples = new List<ushort>();
        }

        private void LoadSamples(string directory)
        {
            var yChannelFile = Path.Combine(directory, "CHANNEL1.DAT");
            var ryChannelFile = Path.Combine(directory, "CHANNEL2.DAT");
            var byChannelFile = Path.Combine(directory, "CHANNEL3.DAT");

            var data = File.ReadAllBytes(yChannelFile);

            for (int i = 0; i < data.Length; i += 2)
                _ySamples.Add((ushort)(data[i + 1] << 8 | data[i]));

            data = File.ReadAllBytes(ryChannelFile);

            for (int i = 0; i < data.Length; i += 2)
                _rySamples.Add((ushort)(data[i + 1] << 8 | data[i]));

            data = File.ReadAllBytes(byChannelFile);

            for (int i = 0; i < data.Length; i += 2)
                _bySamples.Add((ushort)(data[i + 1] << 8 | data[i]));
        }

        private void GenerateVectorTable()
        {
            int romOffset = _targetOffset;
            int i = 0;

            //288 == 289;
            //290 = 1
            //578
            //579

            _vectorEntries[0] = GetVectorForOffset(romOffset);
            romOffset += _lineLength;

            _vectorEntries[_linesPerField - 1] = GetVectorForOffset(romOffset);
            romOffset += _lineLength;

            for (i = 2; i < (_linesPerField - 2); i++)
            {
                var alternateField = i + _linesPerField;
                _vectorEntries[alternateField] = GetVectorForOffset(romOffset);

                romOffset += _lineLength;

                _vectorEntries[i] = GetVectorForOffset(romOffset);
                romOffset += _lineLength;
            }

            _vectorEntries[1] = GetVectorForOffset(romOffset);
            romOffset += _lineLength;

            _vectorEntries[_linesPerField + 1] = GetVectorForOffset(romOffset);
            romOffset += _lineLength;

            //Dupes
            _vectorEntries[290] = _vectorEntries[1];
            _vectorEntries[578] = _vectorEntries[1];
            _vectorEntries[579] = _vectorEntries[1];
            _vectorEntries[288] = _vectorEntries[289];

            for (i = 0; i < _vectorTableLength; i++)
            {
                if (!_vectorEntries.ContainsKey(i))
                {
                    throw new Exception("Missing vector entry " + i);
                }
            }
        }

        private Tuple<byte, byte, byte> GetVectorForOffset(int offset)
        {
            byte sequence = 0x00;
            byte msa = (byte)(offset >> 8);

            if ((offset & 0x10000) == 0x10000)
                sequence |= 0x20;

            if ((offset & 0x20000) == 0x20000)
                sequence |= 0x04;

            if ((offset & 0x40000) == 0x40000)
                sequence |= 0x08;

            return new Tuple<byte, byte, byte>(sequence, msa, msa);
        }
    }
}
