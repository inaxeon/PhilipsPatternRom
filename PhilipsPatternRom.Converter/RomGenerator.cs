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
        
        private const int _lineLength = 1024;

        private List<ushort> _existingYBackPorchSamples;
        private List<byte> _existingRyBackPorchSamples;
        private List<byte> _existingByBackPorchSamples;

        private List<ushort> _ySamples;
        private List<ushort> _rySamples;
        private List<ushort> _bySamples;

        private List<Tuple<byte, byte, byte>> _sourceVectorEntries;
        private int _linesPerField;

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
            _linesPerField = _sourceVectorEntries.Count / 2;

            if (_romManager.Standard == GeneratorStandard.PAL || _romManager.Standard == GeneratorStandard.PAL_M)
                _linesPerField /= 2;
        }

        public void AddAntiPal(string directory, PatternFixes fixes)
        {
            if (_romManager.Standard == GeneratorStandard.NTSC)
                throw new Exception("Cannot add Anti-PAL pattern for NTSC");

            _invertNewSamples = true;

            LoadSamples(directory);
            ApplyPatternFixes(fixes);
            Do422Conversion();

            var lastPattern = _convertedComponents.LastOrDefault();
            var initialOffset = lastPattern != null ? lastPattern.Item3 : _targetOffset;

            var ap1 = ConvertPatternPal(0, 0, initialOffset);
            var ap2 = ConvertPatternPal(580, ap1.NextLine, ap1.NextOffset);

            _convertedComponents.Add(new Tuple<ConvertedComponents, ConvertedComponents, int>(ap1, ap2, ap2.NextOffset));
        }

        public void AddRegular(string directory, PatternFixes fixes)
        {
            _invertNewSamples = true;

            LoadSamples(directory);
            ApplyPatternFixes(fixes);
            Do422Conversion();

            var lastPattern = _convertedComponents.LastOrDefault();
            var initialOffset = lastPattern != null ? lastPattern.Item3 : _targetOffset;

            ConvertedComponents ap1 = null;

            switch (_romManager.Standard)
            {
                case GeneratorStandard.PAL:
                case GeneratorStandard.PAL_M:
                    ap1 = ConvertPatternPal(0, 0, initialOffset);
                    break;
                case GeneratorStandard.NTSC:
                    ap1 = ConvertPatternNtsc(0, initialOffset);
                    break;
                default:
                    throw new NotImplementedException();
            }

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

            for (int i = 0; i < data.Length; i += 2)
                _rySamples.Add((ushort)(data[i + 1] << 8 | data[i]));

            data = File.ReadAllBytes(byChannelFile);

            for (int i = 0; i < data.Length; i += 2)
                _bySamples.Add((ushort)(data[i + 1] << 8 | data[i]));

        }

        private void Do422Conversion()
        {
            var ryCopy = new List<ushort>(_rySamples);
            _rySamples.Clear();

            for (int i = 0; i < ryCopy.Count; i += 2)
                _rySamples.Add((ushort)((ryCopy[i] + ryCopy[i + 1]) / 2));

            var byCopy = new List<ushort>(_bySamples);
            _bySamples.Clear();

            for (int i = 0; i < byCopy.Count; i += 2)
                _bySamples.Add((ushort)((byCopy[i] + byCopy[i + 1]) / 2));
        }
        
        private ConvertedComponents ConvertPatternPal(int baseVector, int line, int offset)
        {
            var ret = new ConvertedComponents
            {
                SamplesY = new List<int>(),
                SamplesRy = new List<int>(),
                SamplesBy = new List<int>(),
                VectorTable = new Dictionary<int, Tuple<byte, byte, byte>>()
            };

            int i = 0;

            // Right side castellation
            ConvertAllSamples(ret, line++, offset);
            ret.VectorTable[baseVector] = GetVectorForOffset(offset);
            offset += _lineLength;

            line += 3;

            for (i = 2; i < (_linesPerField - 4); i++)
            {
                var alternateField = i + _linesPerField + 1;

                ConvertAllSamples(ret, line++, offset);
                ret.VectorTable[baseVector + i] = GetVectorForOffset(offset);

                offset += _lineLength;

                ConvertAllSamples(ret, line++, offset);
                ret.VectorTable[baseVector + alternateField] = GetVectorForOffset(offset);
                offset += _lineLength;
            }

            ConvertAllSamples(ret, line++, offset);
            ret.VectorTable[baseVector + 1] = GetVectorForOffset(offset);
            offset += _lineLength;

            ConvertAllSamples(ret, line++, offset);
            ret.VectorTable[baseVector + _linesPerField + 1] = GetVectorForOffset(offset);
            offset += _lineLength;

            // Skip the second to last line. Only want the last line.
            line++;

            // Left side castellation
            ConvertAllSamples(ret, line++, offset);
            ret.VectorTable[baseVector + 291] = GetVectorForOffset(offset);
            offset += _lineLength;

            // Extra full lines of border castellation not all of which are in the source pattern
            ret.VectorTable[baseVector + 286] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 287] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 288] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 289] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 290] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 292] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 577] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 578] = ret.VectorTable[baseVector + 1];
            ret.VectorTable[baseVector + 579] = ret.VectorTable[baseVector + 1];

            ret.NextOffset = offset;
            ret.NextLine = line;

            return ret;
        }

        private ConvertedComponents ConvertPatternNtsc(int baseVector, int offset)
        {
            var ret = new ConvertedComponents
            {
                SamplesY = new List<int>(),
                SamplesRy = new List<int>(),
                SamplesBy = new List<int>(),
                VectorTable = new Dictionary<int, Tuple<byte, byte, byte>>()
            };

            int i = 0;
            int line = 0;

            ConvertAllSamples(ret, 1, offset);
            ret.VectorTable[0] = GetVectorForOffset(offset);
            ret.VectorTable[486] = GetVectorForOffset(offset);
            ret.VectorTable[487] = GetVectorForOffset(offset);
            ret.VectorTable[488] = GetVectorForOffset(offset);
            ret.VectorTable[489] = GetVectorForOffset(offset);
            offset += _lineLength;

            ConvertAllSamples(ret, 485, offset);
            ret.VectorTable[1] = GetVectorForOffset(offset);
            offset += _lineLength;

            GenerateBlankLine(ret, offset);
            ret.VectorTable[243] = GetVectorForOffset(offset);
            ret.VectorTable[244] = GetVectorForOffset(offset);
            offset += _lineLength;

            line += 2;

            for (i = 2; i < _linesPerField - 2; i++)
            {
                var alternateField = baseVector + i + _linesPerField - 1;

                ConvertAllSamples(ret, line++, offset);
                ret.VectorTable[alternateField] = GetVectorForOffset(offset);

                offset += _lineLength;

                ConvertAllSamples(ret, line++, offset);
                ret.VectorTable[baseVector + i] = GetVectorForOffset(offset);
                offset += _lineLength;
            }

            ConvertAllSamples(ret, 0, offset);
            ret.VectorTable[245] = GetVectorForOffset(offset);
            offset += _lineLength;

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

        private void GenerateBlankLine(ConvertedComponents components, int offset)
        {
            components.SamplesY.AddRange(Enumerable.Repeat(724, 1024));
            components.SamplesRy.AddRange(Enumerable.Repeat(128, 512));
            components.SamplesBy.AddRange(Enumerable.Repeat(128, 512));
        }

        private List<int> BuildSamples(PatternType type, int line, int offset)
        {
            var lineSamples = new List<int>();
            int totalSize = 0;
            int newPatternStartOffset = 0;
            int maxBackAndCentreSamples = 0;
            int centreSize = 0;
            int backPorchSize = 0;
            var lineLength = _lineLength;

            switch (_romManager.Standard)
            {
                case GeneratorStandard.PAL:
                    totalSize = 1024;
                    newPatternStartOffset = type == PatternType.Luma ? 133 : 67;
                    maxBackAndCentreSamples = 736;
                    centreSize = 512;
                    backPorchSize = 256;
                    break;
                case GeneratorStandard.NTSC:
                case GeneratorStandard.PAL_M:
                    totalSize = 1024;
                    newPatternStartOffset = type == PatternType.Luma ? 138 : 70;
                    maxBackAndCentreSamples = 768;
                    centreSize = 512;
                    backPorchSize = 256;
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (type != PatternType.Luma)
            {
                maxBackAndCentreSamples /= 2;
                offset /= 2;
                lineLength /= 2;
                centreSize /= 2;
                totalSize /= 2;
                backPorchSize /= 2;
            }

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

            if ((_invertNewSamples && _romManager.Standard == GeneratorStandard.PAL) || (_romManager.Standard == GeneratorStandard.NTSC && type == PatternType.BminusY))
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

        private void ApplyPatternFixes(PatternFixes fixes)
        {
            if ((fixes & PatternFixes.FixCircle16x9Clock) == PatternFixes.FixCircle16x9Clock)
            {
                FixCircle16x9Clock();
            }

            if ((fixes & PatternFixes.FixCircle16x9Ap) == PatternFixes.FixCircle16x9Ap)
            {
                FixCircle16x9Ap();
            }

            if ((fixes & PatternFixes.FixFubk16x9Centre) == PatternFixes.FixFubk16x9Centre)
            {
                FixFubk16x9Centre();
            }
        }

        private void FixCircle16x9Clock()
        {
            // Samples for centre without clock cut-out. Extracted from the GREY10 version of the pattern.
            ushort[] centreWithClock = { 64, 97, 495, 902, 825, 358, 65, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    134, 575, 927, 766, 286, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 184, 652, 939, 698, 220,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 244, 725, 937, 624, 164, 64, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    312, 789, 919, 546, 119, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 68, 387, 844, 888, 466, 87,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 201, 675, 940, 675, 201, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 87, 466, 888, 844, 387, 68, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    119, 545, 919, 789, 312, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 164, 624, 937, 725, 244,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 220, 698, 939, 652, 184, 64, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    286, 766, 927, 575, 134, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
                    64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 65, 358, 825, 902, 495, 97, 64 };

            for (int line = 268; line < 287; line++)
            {
                for (int i = 0; i < centreWithClock.Length; i++)
                    _ySamples[(line * _lineLength) + 340 + i] = centreWithClock[i];
            }

            // Most central two lines without clock cut-out
            for (int line = 287; line < 289; line++)
            {
                for (int i = 0; i < centreWithClock.Length; i++)
                    _ySamples[(line * _lineLength) + 340 + i] = 940; // Solid white
            }

            for (int line = 289; line < 308; line++)
            {
                for (int i = 0; i < centreWithClock.Length; i++)
                    _ySamples[(line * _lineLength) + 340 + i] = centreWithClock[i];
            }
        }

        private void FixCircle16x9Ap()
        {
            // The PT5300 patterns use a compromise anti-PAL arrangement where the phase
            // is swapped on each alternating frame, rather than each alternating field
            // which doesn't upset digital transmissions but roughly does the same thing
            // for analogue. But it's just not as good as true analogue anti-PAL so
            // this fixes it up by re-arranging the samples accordingly. The result
            // is equivalent to the G/924's pattern.

            var totalLines = _rySamples.Count / _lineLength / 2;
            var originalLineToPreserve = 202;
            var stripeLength = 36;
            var stripeStartRy = 168;
            var stripeStartBy = 802;
            ushort zeroValue = 512;

            var originalRyStripePlus = _rySamples.Skip((originalLineToPreserve * _lineLength) + stripeStartRy).Take(stripeLength).ToList();
            var originalRyStripeMinus = _rySamples.Skip(((totalLines + originalLineToPreserve) * _lineLength) + stripeStartRy).Take(stripeLength).ToList();

            var originalByStripePlus = _bySamples.Skip((originalLineToPreserve * _lineLength) + stripeStartBy).Take(stripeLength).ToList();
            var originalByStripeMinus = _bySamples.Skip(((totalLines + originalLineToPreserve) * _lineLength) + stripeStartBy).Take(stripeLength).ToList();

            // Odd field
            PatchAntiPal(_rySamples, 0, stripeStartRy, originalRyStripePlus, originalRyStripeMinus);
            PatchAntiPal(_bySamples, 0, stripeStartBy, originalByStripePlus, originalByStripeMinus);
            // Even field
            PatchAntiPal(_rySamples, 576, stripeStartRy, originalRyStripeMinus, originalRyStripePlus);
            PatchAntiPal(_bySamples, 576, stripeStartBy, originalByStripeMinus, originalByStripePlus);
        }

        private void PatchAntiPal(List<ushort> set, int startLine, int stripeStart, List<ushort> originalSamplesPlus, List<ushort> originalSamplesMinus)
        {
            var totalLines = set.Count / _lineLength / 2;
            var originalLineToPreserve = 202 + startLine;
            var stripeLength = 36;
            ushort zeroValue = 512;

            // Clear existing AP
            foreach (var line in new[] {
                    202, 203, 204,
                    207, 208,
                    211, 212,
                    215, 216,
                    219, 220,
                    223,
                    227, 228,
                    231, 232,
                    235, 236,
                    239, 240,
                    243, 244,
                    247, 248,
                    251, 252,
                    255, 256,
                    259, 260,
                    263, 264
                })
            {
                for (int i = 0; i < stripeLength; i++)
                    set[((startLine + line) * _lineLength) + stripeStart + i] = zeroValue;
            }

            // Re-add plus stripes
            foreach (var line in new[] {
                    204,
                    207, 208,
                    211, 212,
                    215, 216,
                    219, 220,
                    223,
                    227, 228,
                    231, 232,
                    235, 236,
                    239, 240,
                    243, 244,
                    247, 248,
                    251, 252,
                    255, 256,
                    259, 260,
                    263, 264
                })
            {
                for (int i = 0; i < stripeLength; i++)
                    set[((startLine + line) * _lineLength) + stripeStart + i] = originalSamplesPlus[i];
            }

            // Re-add minus stripes
            foreach (var line in new[] {
                    205, 206,
                    209, 210,
                    213, 214,
                    217, 218,
                    221, 222,
                    226,
                    229, 230,
                    233, 234,
                    237, 238,
                    241, 242,
                    245, 246,
                    249, 250,
                    253, 254,
                    257, 258,
                    261, 262,
                    265
                })
            {
                for (int i = 0; i < stripeLength; i++)
                    set[((startLine + line) * _lineLength) + stripeStart + i] = originalSamplesMinus[i];
            }
        }

        private void FixFubk16x9Centre()
        {
            var originalLineToPreserve = 288;
            var stripeLength = 9;
            var stripeStart = 498;

            var originalStripe = _ySamples.Skip((originalLineToPreserve * _lineLength) + stripeStart).Take(stripeLength).ToList();

            foreach (var line in new[] { 288, 289, 290, 324, 325, 326 })
            {
                for (int i = 0; i < 8; i++)
                {
                    _ySamples[((line) * _lineLength) + i + 490] = 64;
                    _ySamples[((line) * _lineLength) + i + 515] = 64;
                }
            }

            for (int line = 288; line < 327; line++)
            {
                for (int i = 0; i < stripeLength; i++)
                    _ySamples[(line * _lineLength) + stripeStart + i] = originalStripe[i];
            }
        }
    }
}
