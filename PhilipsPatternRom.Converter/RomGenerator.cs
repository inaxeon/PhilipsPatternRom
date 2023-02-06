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

        private List<ushort> _existingYBackPorchSamples;
        private List<byte> _existingRyBackPorchSamples;
        private List<byte> _existingByBackPorchSamples;

        private List<ushort> _ySamples;
        private List<ushort> _rySamples;
        private List<ushort> _bySamples;

        private List<Tuple<byte, byte, byte>> _sourceVectorEntries;

        private bool _invertNewSamples;

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

        private void LoadSourceVectors()
        {
            _sourceVectorEntries = Utility.LoadVectors(_romManager, _romManager.Standard);
        }

        public void AddAntiPal(string directory)
        {
            int line = 0;
            int romOffset = 0;

            _invertNewSamples = true;

            Init(directory);

            ConvertPattern(0, ref line, ref romOffset);
            ConvertPattern(580, ref line, ref romOffset);

            for (int i = 0; i < _vectorTableLength; i++)
            {
                if (!_vectorEntries.ContainsKey(i))
                {
                    throw new Exception("Missing vector entry " + i);
                }
            }
        }

        private void Init(string directory)
        {
            _vectorEntries = new Dictionary<int, Tuple<byte, byte, byte>>();
            _ySamples = new List<ushort>();
            _rySamples = new List<ushort>();
            _bySamples = new List<ushort>();

            LoadSamples(directory);
            LoadSourceVectors();

            var backPorchVector = _sourceVectorEntries[5]; // Pick a line (any line) which has an appropriate back porch

            var addr = Utility.DecodeVector(backPorchVector, Utility.SampleType.Centre, 4);

            var existingYCentreSamples = _romManager.LuminanceSamplesFull.Skip(addr).Take(480).ToList();

            addr = Utility.DecodeVector(backPorchVector, Utility.SampleType.BackPorch, 4);

            _existingYBackPorchSamples = _romManager.LuminanceSamplesFull.Skip(addr).Take(128).ToList();

            addr = Utility.DecodeVector(backPorchVector, Utility.SampleType.BackPorch, 2);

            _existingRyBackPorchSamples = _romManager.ChrominanceRySamples.Skip(addr).Take(64).ToList();
            _existingByBackPorchSamples = _romManager.ChrominanceBySamples.Skip(addr).Take(64).ToList();
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

        private int ConvertPattern(int baseVector, ref int line, ref int romOffset)
        {
            int i = 0;
            var outSamples = new List<int>();
            var vectorEntries = new Dictionary<int, Tuple<byte, byte, byte>>();

            outSamples.AddRange(BuildSamples(line++, romOffset));
            vectorEntries[baseVector] = GetVectorForOffset(romOffset);
            romOffset += _lineLength;

            outSamples.AddRange(BuildSamples(line++, romOffset));
            vectorEntries[baseVector + (_linesPerField - 1)] = GetVectorForOffset(romOffset);
            romOffset += _lineLength;

            for (i = 2; i < (_linesPerField - 2); i++)
            {
                var alternateField = i + _linesPerField;

                outSamples.AddRange(BuildSamples(line++, romOffset));
                vectorEntries[baseVector + alternateField] = GetVectorForOffset(romOffset);

                romOffset += _lineLength;

                outSamples.AddRange(BuildSamples(line++, romOffset));
                vectorEntries[baseVector + i] = GetVectorForOffset(romOffset);
                romOffset += _lineLength;
            }

            outSamples.AddRange(BuildSamples(line++, romOffset));
            vectorEntries[baseVector + 1] = GetVectorForOffset(romOffset);
            romOffset += _lineLength;

            outSamples.AddRange(BuildSamples(line++, romOffset));
            vectorEntries[baseVector + _linesPerField + 1] = GetVectorForOffset(romOffset);
            romOffset += _lineLength;

            //Dupes
            vectorEntries[baseVector + 290] = vectorEntries[baseVector + 1];
            vectorEntries[baseVector + 578] = vectorEntries[baseVector + 1];
            vectorEntries[baseVector + 579] = vectorEntries[baseVector + 1];
            vectorEntries[baseVector + 288] = vectorEntries[baseVector + 289];

            return romOffset;
        }

        private List<int> BuildSamples(int line, int offset)
        {
            var outYSamples = new List<int>();
            var newPatternStartOffset = 133;
            var maxBackAndCentreSamples = 736;

            // _centreLength = 480 (512)
            // _frontSpriteLength = 128
            // _backSpriteLength = 256

            // 864 samples are rendered per line including blanking
            // Take 128 samples from exiting back sprite
            // Skip 133 samples from the new pattern
            // Take 736 samples from the new pattern

            // Now we have 864 of the new - but - they must be laid out differently:

            // After 736 samples there must be a gap
            // Resume the rest at 768
            // With the last 128 that is 896 total per line
            // 0x00-0xE0 per rom. The rest per line is wasted.

            line = 5;

            outYSamples.AddRange(_existingYBackPorchSamples.Select(el => (int)el));

            var existingBackPorchToNewSamplesOffset = (newPatternStartOffset - _existingYBackPorchSamples.Count);
            var lineStart = (line * _lineLength);

            // Splice new samples with HSync+Colour burst from old pattern

            for (int i = newPatternStartOffset; i < (maxBackAndCentreSamples + existingBackPorchToNewSamplesOffset); i++)
                outYSamples.Add(AdjustY(_ySamples[lineStart + i]));

            outYSamples.AddRange(Enumerable.Repeat(-1, 32));

            for (int i = (maxBackAndCentreSamples + existingBackPorchToNewSamplesOffset); i < (maxBackAndCentreSamples + newPatternStartOffset); i++)
                outYSamples.Add(AdjustY(_ySamples[lineStart + i]));

            outYSamples.AddRange(Enumerable.Repeat(-1, 128));

            return outYSamples;
        }

        private ushort AdjustY(ushort source)
        {
            int sourceAdjusted = source;

            // Original:
            // Black level: 0x2D4 (724)
            // White level: 0xA4 (164)

            // New:
            // Black level: 0x40 (64)
            // White level: 0x3AC (940)

            ushort originalBlackLevel = 724;
            ushort originalWhiteLevel = 164;

            ushort newBlackLevel = 64;
            ushort newWhiteLevel = 940;

            var originalRange = originalBlackLevel - originalWhiteLevel;
            var newRange = _invertNewSamples ? newWhiteLevel - newBlackLevel : newBlackLevel - newWhiteLevel; //Invert

            if (_invertNewSamples)
            {
                sourceAdjusted -= newBlackLevel;
                sourceAdjusted = newRange - sourceAdjusted;
            }
            else
            {
                sourceAdjusted -= newWhiteLevel;
            }

            float scaleFactor = (float)originalRange / (float)newRange;
            float adjusted = (float)sourceAdjusted * scaleFactor;
            adjusted += originalWhiteLevel;

            if (adjusted < 0)
                throw new Exception("Adjusted sample less than zero");

            return (ushort)adjusted;
        }

        private Tuple<byte, byte, byte> GetVectorForOffset(int offset)
        {
            int trueOffset = offset / 4; // 4 EPROMs in PM5644

            byte sequence = 0x00;
            byte msa = (byte)(trueOffset >> 8);

            if ((trueOffset & 0x10000) == 0x10000)
                sequence |= 0x20;

            if ((trueOffset & 0x20000) == 0x20000)
                sequence |= 0x04;

            if ((trueOffset & 0x40000) == 0x40000)
                sequence |= 0x08;

            return new Tuple<byte, byte, byte>(sequence, msa, msa);
        }
    }
}
