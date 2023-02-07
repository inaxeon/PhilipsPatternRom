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

        private List<Tuple<ConvertedComponents, ConvertedComponents, int>> _convertedComponents;

        private bool _invertNewSamples;

        private int _targetOffset;

        public RomGenerator()
        {
            _romManager = new RomManager();
        }

        private void LoadSourceVectors()
        {
            _sourceVectorEntries = Utility.LoadVectors(_romManager, _romManager.Standard);
        }

        public void AddAntiPal(string directory)
        {
            _invertNewSamples = true;

            LoadSamples(directory);

            var lastPattern = _convertedComponents.LastOrDefault();
            var initialOffset = lastPattern != null ? lastPattern.Item3 : _targetOffset;

            var ap1 = ConvertPattern(0, 0, initialOffset);
            var ap2 = ConvertPattern(580, ap1.NextLine, ap1.NextOffset);

            _convertedComponents.Add(new Tuple<ConvertedComponents, ConvertedComponents, int>(ap1, ap2, ap2.NextOffset));
        }

        public void AddRegular(string directory)
        {
            _invertNewSamples = true;

            LoadSamples(directory);

            var lastPattern = _convertedComponents.LastOrDefault();
            var initialOffset = lastPattern != null ? lastPattern.Item3 : _targetOffset;

            var ap1 = ConvertPattern(0, 0, initialOffset);

            _convertedComponents.Add(new Tuple<ConvertedComponents, ConvertedComponents, int>(ap1, null, ap1.NextOffset));
        }

        public void Save(string directory, int outputPatternIndex)
        {
            _romManager.RomSize = 0x80000;
            _romManager.AppendComponents(_convertedComponents, outputPatternIndex);
            _romManager.SaveSet(directory);
        }

        public void Init(GeneratorType type, string directory, int patternIndex)
        {
            _convertedComponents = new List<Tuple<ConvertedComponents, ConvertedComponents, int>>();

            _romManager.OpenSet(type, directory, patternIndex);

            _targetOffset = _romManager.RomSize * 4; // Starting offset for new patterns

            LoadSourceVectors();

            var backPorchVector = _sourceVectorEntries[5]; // Pick a line (any line) which has an appropriate back porch

            var addr = Utility.DecodeVector(backPorchVector, Utility.SampleType.BackPorch, 4);

            _existingYBackPorchSamples = _romManager.LuminanceSamplesFull.Skip(addr).Take(128).ToList();

            addr = Utility.DecodeVector(backPorchVector, Utility.SampleType.BackPorch, 2);

            _existingRyBackPorchSamples = _romManager.ChrominanceRySamples.Skip(addr).Take(64).ToList();
            _existingByBackPorchSamples = _romManager.ChrominanceBySamples.Skip(addr).Take(64).ToList();
        }

        private void LoadSamples(string directory)
        {
            var yChannelFile = Path.Combine(directory, "CHANNEL1.DAT");
            var byChannelFile = Path.Combine(directory, "CHANNEL2.DAT");
            var ryChannelFile = Path.Combine(directory, "CHANNEL3.DAT");

            _ySamples = new List<ushort>();
            _rySamples = new List<ushort>();
            _bySamples = new List<ushort>();

            var data = File.ReadAllBytes(yChannelFile);

            for (int i = 0; i < data.Length; i += 2)
                _ySamples.Add((ushort)(data[i + 1] << 8 | data[i]));

            data = File.ReadAllBytes(ryChannelFile);

            // Do 4:4:4 -> 4:2:2 conversion right here and now

            for (int i = 0; i < data.Length; i += 4)
            {
                var s1 = (data[i + 1] << 8 | data[i]);
                var s2 = (data[i + 3] << 8 | data[i + 2]);
                var avg = (ushort)((s1 + s2) / 2);

                _rySamples.Add(avg);
            }

            data = File.ReadAllBytes(byChannelFile);

            for (int i = 0; i < data.Length; i += 4)
            {
                var s1 = (data[i + 1] << 8 | data[i]);
                var s2 = (data[i + 3] << 8 | data[i + 2]);
                var avg = (ushort)((s1 + s2) / 2);

                _bySamples.Add(avg);
            }
        }

        private ConvertedComponents ConvertPattern(int baseVector, int line, int offset)
        {
            var ret = new ConvertedComponents
            {
                SamplesY = new List<int>(),
                SamplesRy = new List<int>(),
                SamplesBy = new List<int>(),
                VectorTable = new Dictionary<int, Tuple<byte, byte, byte>>()
            };

            int i = 0;

            ConvertAllSamples(ret, line++, offset);
            ret.VectorTable[baseVector] = GetVectorForOffset(offset);
            offset += _lineLength;

            ConvertAllSamples(ret, line++, offset);
            ret.VectorTable[baseVector + (_linesPerField - 1)] = GetVectorForOffset(offset);
            offset += _lineLength;

            for (i = 2; i < (_linesPerField - 2); i++)
            {
                var alternateField = i + _linesPerField;

                ConvertAllSamples(ret, line++, offset);
                ret.VectorTable[baseVector + alternateField] = GetVectorForOffset(offset);

                offset += _lineLength;

                ConvertAllSamples(ret, line++, offset);
                ret.VectorTable[baseVector + i] = GetVectorForOffset(offset);
                offset += _lineLength;
            }

            ConvertAllSamples(ret, line++, offset);
            ret.VectorTable[baseVector + 1] = GetVectorForOffset(offset);
            offset += _lineLength;

            ConvertAllSamples(ret, line++, offset);
            ret.VectorTable[baseVector + _linesPerField + 1] = GetVectorForOffset(offset);
            offset += _lineLength;

            //Dupes
            ret.VectorTable[baseVector + 290] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 578] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 579] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 288] = ret.VectorTable[baseVector + 289];

            ret.NextOffset = offset;
            ret.NextLine = line;

            return ret;
        }

        private void ConvertAllSamples(ConvertedComponents components, int line, int offset)
        {
            components.SamplesY.AddRange(BuildSamples(PatternType.Luma, line, offset));
            components.SamplesRy.AddRange(BuildSamples(PatternType.RminusY, line, offset));
            components.SamplesBy.AddRange(BuildSamples(PatternType.BminusY, line, offset));
        }

        private List<int> BuildSamples(PatternType type, int line, int offset)
        {
            var lineSamples = new List<int>();
            var totalSize = 1024;
            var newPatternStartOffset = type == PatternType.Luma ? 133 : 67;
            var maxBackAndCentreSamples = 736;
            var centreSize = 512;
            var backPorchSize = 256;
            var lineLength = _lineLength;

            if (type != PatternType.Luma)
            {
                maxBackAndCentreSamples /= 2;
                offset /= 2;
                lineLength /= 2;
                centreSize /= 2;
                totalSize /= 2;
                backPorchSize /= 2;
            }

            // _centreLength = 480 (512)
            // _frontSpriteLength = 128
            // _backSpriteLength = 256

            // 864 samples are rendered per line including blanking
            // Take 128 samples from exiting back sprite
            // Skip 133 samples from the new pattern
            // Take 736 samples from the new pattern

            switch (type)
            {
                case PatternType.Luma:
                    lineSamples.AddRange(_existingYBackPorchSamples.Select(el => (int)el));
                    break;
                case PatternType.RminusY:
                    lineSamples.AddRange(_existingRyBackPorchSamples.Select(el => (int)el));
                    break;
                case PatternType.BminusY:
                    lineSamples.AddRange(_existingByBackPorchSamples.Select(el => (int)el));
                    break;
            }


            var lineStart = (line * lineLength);

            // Splice new samples with HSync+Colour burst from old pattern

            for (int i = newPatternStartOffset; i < (maxBackAndCentreSamples + newPatternStartOffset); i++)
            {
                switch (type)
                {
                    case PatternType.Luma:
                        lineSamples.Add(AdjustLuma(_ySamples[lineStart + i]));
                        break;
                    case PatternType.RminusY:
                        lineSamples.Add(AdjustChroma(PatternType.RminusY, _rySamples[lineStart + i]));
                        break;
                    case PatternType.BminusY:
                        lineSamples.Add(AdjustChroma(PatternType.BminusY, _bySamples[lineStart + i]));
                        break;
                }
            }

            var finalSamples = new List<int>();

            // Lay the samples out as per vector type 2, which works rather nicely for this
            finalSamples.AddRange(lineSamples.Skip(backPorchSize).Take(centreSize));
            finalSamples.AddRange(lineSamples.Take(backPorchSize));
            finalSamples.AddRange(lineSamples.Skip(maxBackAndCentreSamples));
            finalSamples.AddRange(Enumerable.Repeat(-1, totalSize - finalSamples.Count));

            return finalSamples;
        }

        private ushort AdjustLuma(ushort source)
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

            decimal scaleFactor = (decimal)originalRange / (decimal)newRange;
            decimal adjusted = (decimal)sourceAdjusted * scaleFactor;
            adjusted += originalWhiteLevel;

            if (adjusted < 0)
                throw new Exception("Adjusted sample less than zero");

            return (ushort)Math.Round(adjusted, 0);
        }

        private ushort AdjustChroma(PatternType type, ushort source)
        {
            // Old RY:
            // Max: 193
            // Centre: 128
            // Min: 63

            // Old BY:
            // Max: 174
            // Centre: 128
            // Min: 82

            // New RY and BY:
            // Max: 848
            // Centre: 512
            // Min: 176

            var originalMax = type == PatternType.RminusY ? 193 : 174;
            var originalCentre = 128;

            var newMax = 848;
            var newCentre = 512;

            decimal originalRange = (originalMax - originalCentre);
            decimal newRange = (newMax - newCentre);

            decimal scaleFactor = (decimal)originalRange / (decimal)newRange;

            int sourceAdjusted = (source - newCentre);
            decimal adjusted = (decimal)sourceAdjusted * scaleFactor;

            if (_invertNewSamples)
                adjusted = -adjusted;

            adjusted += originalCentre;

            return (ushort)Math.Round(adjusted, 0);
        }

        private Tuple<byte, byte, byte> GetVectorForOffset(int offset)
        {
            int trueOffset = offset / 4;

            byte sequence = 2;
            byte msa = (byte)(trueOffset >> 8);

            if ((trueOffset & 0x10000) == 0x10000)
                sequence |= 0x30;

            if ((trueOffset & 0x20000) == 0x20000)
                sequence |= 0x04;

            if ((trueOffset & 0x40000) == 0x40000)
                sequence |= 0x08;

            return new Tuple<byte, byte, byte>(sequence, msa, msa);
        }
    }
}
