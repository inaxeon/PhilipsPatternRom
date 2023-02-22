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
        private GeneratorType _outputType;

        private const int _sourceLines = 576;
        private const int _lineLength = 1024;

        private List<ushort> _existingYBackPorchSamples;
        private List<byte> _existingRyBackPorchSamples;
        private List<byte> _existingByBackPorchSamples;

        private List<Tuple<byte, byte, byte>> _sourceVectorEntries;
        private int _linesPerField;

        private List<PatternSamples> _patternLineSamples { get; set; }

        private List<Tuple<ConvertedComponents, ConvertedComponents, int>> _convertedComponents;

        private int _targetOffset;

        private bool _invertLuma;
        private bool _invertChroma;
        private bool _useDigitalFactors;

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

        public void AddAntiPal(string directory, bool useDigitalFactors, PatternFixType fixes)
        {
            if (_romManager.Standard == GeneratorStandard.NTSC)
                throw new Exception("Cannot add Anti-PAL pattern for NTSC");

            var patternSamples = new PatternSamples();

            _invertLuma = useDigitalFactors;
            _invertChroma = useDigitalFactors;
            _useDigitalFactors = useDigitalFactors;

            LoadSamples(patternSamples, directory);
            ApplyPatternFixes(patternSamples, fixes);
            patternSamples.Do422Conversion();

            var lastPattern = _convertedComponents.LastOrDefault();
            var initialOffset = lastPattern != null ? lastPattern.Item3 : _targetOffset;

            var ap1 = ConvertPatternPal(patternSamples.Frame0, 0, initialOffset);
            var ap2 = ConvertPatternPal(patternSamples.Frame1, 580, ap1.NextOffset);

            _convertedComponents.Add(new Tuple<ConvertedComponents, ConvertedComponents, int>(ap1, ap2, ap2.NextOffset));
        }

        public void AddRegular(string directory, bool useDigitalFactors, PatternFixType fixes)
        {
            var patternSamples = new PatternSamples();
            _invertLuma = useDigitalFactors;
            _invertChroma = useDigitalFactors;
            _useDigitalFactors = useDigitalFactors;

            LoadSamples(patternSamples, directory);
            ApplyPatternFixes(patternSamples, fixes);
            patternSamples.Do422Conversion();

            var lastPattern = _convertedComponents.LastOrDefault();
            var initialOffset = lastPattern != null ? lastPattern.Item3 : _targetOffset;

            ConvertedComponents ap1 = null;

            switch (_romManager.Standard)
            {
                case GeneratorStandard.PAL:
                case GeneratorStandard.PAL_M:
                    ap1 = ConvertPatternPal(patternSamples.Frame0, 0, initialOffset);
                    break;
                case GeneratorStandard.NTSC:
                    ap1 = ConvertPatternNtsc(patternSamples.Frame0, 0, initialOffset);
                    break;
                default:
                    throw new NotImplementedException();
            }

            _convertedComponents.Add(new Tuple<ConvertedComponents, ConvertedComponents, int>(ap1, null, ap1.NextOffset));
        }

        public void Save(string directory, int outputPatternIndex)
        {
            _romManager.SetFilenamesAndSize(_outputType);
            _romManager.AppendComponents(_convertedComponents, outputPatternIndex);
            _romManager.SaveSet(directory);
        }

        public void Init(GeneratorType type, GeneratorType outputType, string directory, int patternIndex)
        {
            _convertedComponents = new List<Tuple<ConvertedComponents, ConvertedComponents, int>>();

            _romManager.OpenSet(type, directory, patternIndex);
            _outputType = outputType;

            _targetOffset = _romManager.RomSize * 4; // Starting offset for new patterns

            LoadSourceVectors();

            var backPorchVector = _sourceVectorEntries[5]; // Pick a line (any line) which has an appropriate back porch

            var addr = Utility.DecodeVector(backPorchVector, Utility.SampleType.BackPorch, 4);

            _existingYBackPorchSamples = _romManager.LuminanceSamplesFull.Skip(addr).Take(128).ToList();

            addr = Utility.DecodeVector(backPorchVector, Utility.SampleType.BackPorch, 2);

            _existingRyBackPorchSamples = _romManager.ChrominanceRySamples.Skip(addr).Take(64).ToList();
            _existingByBackPorchSamples = _romManager.ChrominanceBySamples.Skip(addr).Take(64).ToList();
        }

        private void LoadSamples(PatternSamples patternSamples, string directory)
        {
            var yChannelFile = Path.Combine(directory, "CHANNEL1.DAT");
            var byChannelFile = Path.Combine(directory, "CHANNEL2.DAT");
            var ryChannelFile = Path.Combine(directory, "CHANNEL3.DAT");

            var ySamples = new List<ushort>();
            var rySamples = new List<ushort>();
            var bySamples = new List<ushort>();

            var data = File.ReadAllBytes(yChannelFile);

            for (int i = 0; i < data.Length; i += 2)
                ySamples.Add((ushort)(data[i + 1] << 8 | data[i]));

            data = File.ReadAllBytes(ryChannelFile);

            for (int i = 0; i < data.Length; i += 2)
                rySamples.Add((ushort)(data[i + 1] << 8 | data[i]));

            data = File.ReadAllBytes(byChannelFile);

            for (int i = 0; i < data.Length; i += 2)
                bySamples.Add((ushort)(data[i + 1] << 8 | data[i]));

            var totalLines = ySamples.Count / _lineLength;

            if (totalLines == 576 || totalLines == 1152)
            {
                patternSamples.Frame0 = new List<LineSamples>();

                for (int i = 0; i < _sourceLines; i++)
                {
                    var lineSamples = new LineSamples(ySamples.Skip(_lineLength * i).Take(1024).ToList(),
                        rySamples.Skip(_lineLength * i).Take(1024).ToList(), bySamples.Skip(_lineLength * i).Take(1024).ToList());

                    patternSamples.Frame0.Add(lineSamples);
                }
            }
            if (totalLines == (_sourceLines * 2))
            {
                patternSamples.Frame1 = new List<LineSamples>();

                for (int i = _sourceLines; i < (_sourceLines * 2); i++)
                {
                    var lineSamples = new LineSamples(ySamples.Skip(_lineLength * i).Take(1024).ToList(),
                        rySamples.Skip(_lineLength * i).Take(1024).ToList(), bySamples.Skip(_lineLength * i).Take(1024).ToList());

                    patternSamples.Frame1.Add(lineSamples);
                }
            }

            if (totalLines != _sourceLines && totalLines != (_sourceLines * 2))
            {
                throw new Exception("Source file has an unexpected number of lines");
            }
        }

        private ConvertedComponents ConvertPatternPal(List<LineSamples> samples, int baseVector, int offset)
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

            // Right side castellation
            ConvertAllSamples(samples[line++], ret, offset);
            ret.VectorTable[baseVector] = GetVectorForOffset(offset);
            offset += _lineLength;

            line += 3;

            for (i = 2; i < (_linesPerField - 4); i++)
            {
                var alternateField = i + _linesPerField + 1;

                ConvertAllSamples(samples[line++], ret, offset);
                ret.VectorTable[baseVector + i] = GetVectorForOffset(offset);
                offset += _lineLength;

                ConvertAllSamples(samples[line++], ret, offset);
                ret.VectorTable[baseVector + alternateField] = GetVectorForOffset(offset);
                offset += _lineLength;
            }

            ConvertAllSamples(samples[line++], ret, offset);
            ret.VectorTable[baseVector + 1] = GetVectorForOffset(offset);
            offset += _lineLength;

            ConvertAllSamples(samples[line++], ret, offset);
            ret.VectorTable[baseVector + _linesPerField + 1] = GetVectorForOffset(offset);
            offset += _lineLength;

            // Skip the second to last line. Only want the last line.
            line++;

            // Left side castellation
            ConvertAllSamples(samples[line++], ret, offset);
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

        private ConvertedComponents ConvertPatternNtsc(List<LineSamples> samples, int baseVector, int offset)
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

            ConvertAllSamples(samples[1], ret, offset);
            ret.VectorTable[0] = GetVectorForOffset(offset);
            ret.VectorTable[486] = GetVectorForOffset(offset);
            ret.VectorTable[487] = GetVectorForOffset(offset);
            ret.VectorTable[488] = GetVectorForOffset(offset);
            ret.VectorTable[489] = GetVectorForOffset(offset);
            offset += _lineLength;

            ConvertAllSamples(samples[485], ret, offset);
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

                ConvertAllSamples(samples[line++], ret, offset);
                ret.VectorTable[alternateField] = GetVectorForOffset(offset);

                offset += _lineLength;

                ConvertAllSamples(samples[line++], ret, offset);
                ret.VectorTable[baseVector + i] = GetVectorForOffset(offset);
                offset += _lineLength;
            }

            ConvertAllSamples(samples[0], ret, offset);
            ret.VectorTable[245] = GetVectorForOffset(offset);
            offset += _lineLength;

            ret.NextOffset = offset;
            ret.NextLine = line;

            return ret;
        }

        private void ConvertAllSamples(LineSamples samples, ConvertedComponents components, int offset)
        {
            components.SamplesY.AddRange(BuildSamples(samples, PatternType.Luma, offset));
            components.SamplesRy.AddRange(BuildSamples(samples, PatternType.RminusY, offset));
            components.SamplesBy.AddRange(BuildSamples(samples, PatternType.BminusY, offset));
        }

        private void GenerateBlankLine(ConvertedComponents components, int offset)
        {
            components.SamplesY.AddRange(Enumerable.Repeat(724, 1024));
            components.SamplesRy.AddRange(Enumerable.Repeat(128, 512));
            components.SamplesBy.AddRange(Enumerable.Repeat(128, 512));
        }

        private List<int> BuildSamples(LineSamples samples, PatternType type, int offset)
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


            // Splice new samples with HSync+Colour burst from old pattern

            for (int i = newPatternStartOffset; i < (maxBackAndCentreSamples + newPatternStartOffset); i++)
            {
                switch (type)
                {
                    case PatternType.Luma:
                        lineSamples.Add(AdjustLuma(samples.SamplesY[i]));
                        break;
                    case PatternType.RminusY:
                        lineSamples.Add(AdjustChroma(PatternType.RminusY, samples.SamplesRy[i]));
                        break;
                    case PatternType.BminusY:
                        lineSamples.Add(AdjustChroma(PatternType.BminusY, samples.SamplesBy[i]));
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

            // Original (Digital):
            // Black level: 0x2D4 (724)
            // White level: 0xA4 (164)

            // New:
            // Black level: 0x40 (64)
            // White level: 0x3AC (940)

            // New (Analogue):
            // Black level: 3072
            // White level: 832

            ushort originalBlackLevel = 724;
            ushort originalWhiteLevel = 164;

            ushort newBlackLevel = _useDigitalFactors ? (ushort)64 : (ushort)3072;
            ushort newWhiteLevel = _useDigitalFactors ? (ushort)940 : (ushort)832;

            var originalRange = originalBlackLevel - originalWhiteLevel;
            var newRange = _invertLuma ? newWhiteLevel - newBlackLevel : newBlackLevel - newWhiteLevel; //Invert

            sourceAdjusted -= (_invertLuma ? newBlackLevel : newWhiteLevel);

            if (_invertLuma)
                sourceAdjusted = newRange - sourceAdjusted;

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

            // Alg RY:
            // 770 254
            // Alg BY:
            // 695 329

            var originalMax = type == PatternType.RminusY ? 193 : 174;
            var originalCentre = 128;

            var newMax = _useDigitalFactors ? 848 : (type == PatternType.RminusY ? 254 : 329);
            var newCentre = 512;

            decimal originalRange = (originalMax - originalCentre);
            decimal newRange = _useDigitalFactors ? (newMax - newCentre) : (newCentre - newMax);

            decimal scaleFactor = (decimal)originalRange / (decimal)newRange;

            int sourceAdjusted = (source - newCentre);
            decimal adjusted = (decimal)sourceAdjusted * scaleFactor;

            if ((_invertChroma && _romManager.Standard == GeneratorStandard.PAL) || (_romManager.Standard == GeneratorStandard.NTSC && type == PatternType.BminusY))
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

        private void ApplyPatternFixes(PatternSamples samples, PatternFixType fixes)
        {
            if ((fixes & PatternFixType.FixDigCircle16x9Clock) == PatternFixType.FixDigCircle16x9Clock)
                FixDigCircle16x9Clock(samples);

            if ((fixes & PatternFixType.FixDigCircle16x9BottomBox) == PatternFixType.FixDigCircle16x9BottomBox)
                FixDigCircle16x9BottomBox(samples);

            if ((fixes & PatternFixType.FixDigCircle16x9Ap) == PatternFixType.FixDigCircle16x9Ap)
                FixDigCircle16x9Ap(samples);

            if ((fixes & PatternFixType.FixDigFubk16x9Centre) == PatternFixType.FixDigFubk16x9Centre)
                FixDigFubk16x9Centre(samples);

            if ((fixes & PatternFixType.FixAlgFubk16x9LowerIdBoxes) == PatternFixType.FixAlgFubk16x9LowerIdBoxes)
                FixAlgFubk16x9LowerIdBoxes(samples);
        }

        private void FixDigCircle16x9Clock(PatternSamples samples)
        {
            if (!_useDigitalFactors)
                throw new Exception("This fix is for digital patterns only");

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

            FixDigCircle16x9Clock(samples.Frame0, centreWithClock);
            FixDigCircle16x9Clock(samples.Frame1, centreWithClock);
        }

        private void FixDigCircle16x9Clock(List<LineSamples> samples, ushort[] centreWithClock)
        {
            if (!_useDigitalFactors)
                throw new Exception("This fix is for digital patterns only");

            for (int line = 268; line < 287; line++)
            {
                for (int i = 0; i < centreWithClock.Length; i++)
                    samples[line].SamplesY[340 + i] = centreWithClock[i];
            }

            // Most central two lines without clock cut-out
            for (int line = 287; line < 289; line++)
            {
                for (int i = 0; i < centreWithClock.Length; i++)
                    samples[line].SamplesY[340 + i] = 940;
            }

            for (int line = 289; line < 308; line++)
            {
                for (int i = 0; i < centreWithClock.Length; i++)
                    samples[line].SamplesY[340 + i] = centreWithClock[i];
            }
        }

        private void FixDigCircle16x9BottomBox(PatternSamples samples)
        {
            if (!_useDigitalFactors)
                throw new Exception("This fix is for digital patterns only");

            for (var line = 434; line < 476; line++)
            {
                for (int i = 0; i < 8; i++)
                {
                    samples.Frame0[line].SamplesY[i + 427] = 64;
                    if (samples.Frame1 != null)
                        samples.Frame1[line].SamplesY[i + 427] = 64;
                }
            }
        }

        private void FixDigCircle16x9Ap(PatternSamples samples)
        {
            // The PT5300/PT8633 patterns use a compromise anti-PAL arrangement where the phase
            // is swapped on each alternating frame, rather than each alternating field
            // which doesn't upset digital transmissions but roughly does the same thing
            // for analogue. But it's just not as good as true analogue anti-PAL so
            // this fixes it up by re-arranging the samples accordingly. The result
            // is equivalent to the G/924's pattern.

            if (!_useDigitalFactors)
                throw new Exception("This fix is for digital patterns only");

            var originalLineToPreserve = 202;
            var stripeLength = 36;
            var stripeStartRy = 168;
            var stripeStartBy = 802;
            ushort zeroValue = 512;

            var originalRyStripePlus = samples.Frame0[originalLineToPreserve].SamplesRy.Skip(stripeStartRy).Take(stripeLength).ToList();
            var originalRyStripeMinus = samples.Frame1[originalLineToPreserve].SamplesRy.Skip(stripeStartRy).Take(stripeLength).ToList();

            var originalByStripePlus = samples.Frame0[originalLineToPreserve].SamplesBy.Skip(stripeStartBy).Take(stripeLength).ToList();
            var originalByStripeMinus = samples.Frame1[originalLineToPreserve].SamplesBy.Skip(stripeStartBy).Take(stripeLength).ToList();

            PatchAntiPal(samples.Frame0, stripeStartRy, stripeStartBy, originalRyStripePlus, originalByStripePlus, originalRyStripeMinus, originalByStripeMinus);
            PatchAntiPal(samples.Frame1, stripeStartRy, stripeStartBy, originalRyStripeMinus, originalByStripeMinus, originalRyStripePlus, originalByStripePlus);
        }

        private void PatchAntiPal(List<LineSamples> set, int stripeStartRy, int stripeStartBy,
            List<ushort> originalRyStripeMinus, List<ushort> originalByStripeMinus,
            List<ushort> originalRyStripePlus, List<ushort> originalByStripePlus)
        {
            var totalLines = set.Count / _lineLength / 2;
            var originalLineToPreserve = 202;
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
                {
                    set[line].SamplesRy[stripeStartRy + i] = zeroValue;
                    set[line].SamplesBy[stripeStartBy + i] = zeroValue;
                }
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
                {
                    set[line].SamplesRy[stripeStartRy + i] = originalRyStripePlus[i];
                    set[line].SamplesBy[stripeStartBy + i] = originalByStripePlus[i];
                }
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
                {
                    set[line].SamplesRy[stripeStartRy + i] = originalRyStripeMinus[i];
                    set[line].SamplesBy[stripeStartBy + i] = originalByStripeMinus[i];
                }
            }
        }

        private void FixDigFubk16x9Centre(PatternSamples samples)
        {
            var originalLineToPreserve = 288;
            var stripeLength = 9;
            var stripeStart = 498;

            var originalStripe = samples.Frame0[originalLineToPreserve].SamplesY.Skip(stripeStart).Take(stripeLength).ToList();

            foreach (var line in new[] { 288, 289, 290, 324, 325, 326 })
            {
                for (int i = 0; i < 8; i++)
                {
                    samples.Frame0[line].SamplesY[i + 490] = 64;
                    samples.Frame0[line].SamplesY[i + 515] = 64;
                }
            }

            for (int line = 288; line < 327; line++)
            {
                for (int i = 0; i < stripeLength; i++)
                    samples.Frame0[line].SamplesY[stripeStart + i] = originalStripe[i];
            }
        }

        private void FixAlgFubk16x9LowerIdBoxes(PatternSamples samples)
        {
            // Remove station ID boxes. Data taken from non-AntiPAL pattern.

            if (_useDigitalFactors)
                throw new Exception("This fix is for analogue patterns only");

            var fixedSamples = new[,]
            {
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2406, 1908, 1277, 872, 918, 1382, 2005, 2443, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2266, 1696, 1110, 834, 1027, 1586, 2196, 2506, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2320, 1764, 1158, 841, 985, 1506, 2114, 2482, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2345, 1821, 1213, 855, 947, 1444, 2071, 2474, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2508, 2219, 1624, 1058, 832, 1065, 1625, 2208, 2504, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2406, 1935, 1321, 894, 889, 1315, 1941, 2418, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2484, 2108, 1493, 979, 842, 1152, 1737, 2286, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2452, 2037, 1428, 947, 853, 1202, 1810, 2344, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2439, 1993, 1374, 918, 867, 1242, 1841, 2350, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2483, 2126, 1532, 1010, 835, 1107, 1684, 2257, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2379, 1878, 1268, 876, 904, 1333, 1935, 2400, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2501, 2202, 1631, 1079, 833, 1028, 1565, 2162, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2308, 1766, 1176, 849, 949, 1421, 2018, 2438, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2328, 2267, 1723, 1151, 844, 965, 1456, 2063, 2465, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2508, 2230, 1660, 1098, 835, 1000, 1504, 2090, 2115, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2158, 1806, 1223, 865, 916, 1358, 1964, 2420, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2490, 2148, 1561, 1034, 832, 1054, 1582, 1868, 1222, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1397, 1766, 1294, 893, 881, 1271, 1868, 2366, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2461, 2067, 1472, 981, 838, 1107, 1476, 1155, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1243, 1303, 925, 857, 1195, 1776, 2306, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2425, 1987, 1393, 940, 849, 1089, 1040, 840, 1456, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1212, 838, 1050, 946, 842, 1131, 1691, 2243, 2508, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2383, 1911, 1323, 908, 858, 921, 837, 1373, 2332, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2101, 1123, 835, 892, 834, 1078, 1613, 2179, 2495, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2339, 1841, 1263, 873, 844, 834, 1199, 2153, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2457, 1822, 998, 833, 832, 1012, 1543, 2118, 2476, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2294, 1777, 1130, 840, 832, 1003, 1746, 2332, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2468, 2120, 1412, 887, 832, 893, 1411, 2059, 2452, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2507, 2251, 1529, 911, 832, 864, 1272, 1885, 2348, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2475, 2144, 1601, 1034, 833, 833, 1052, 1877, 2425, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2499, 1913, 1029, 834, 837, 923, 1351, 1908, 2359, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2479, 2161, 1627, 1111, 840, 860, 835, 1247, 2228, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2132, 1148, 836, 934, 840, 947, 1371, 1923, 2365, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 2171, 1646, 1129, 847, 910, 955, 837, 1400, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1219, 839, 1116, 1058, 838, 956, 1383, 1930, 2366, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2481, 2176, 1656, 1141, 851, 919, 1216, 1082, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1310, 1490, 1071, 836, 962, 1388, 1930, 2363, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2479, 2173, 1659, 1147, 854, 911, 1298, 1636, 1190, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1438, 1960, 1550, 1061, 836, 963, 1385, 1922, 2355, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2475, 2165, 1653, 1147, 856, 907, 1284, 1831, 1998, 1233, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2276, 2081, 1540, 1058, 836, 961, 1375, 1907, 2342, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2467, 2149, 1640, 1141, 855, 905, 1277, 1818, 2299, 2155, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2443, 2074, 1537, 1059, 836, 954, 1358, 1884, 2324, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2456, 2127, 1619, 1128, 853, 907, 1277, 1812, 2292, 2510, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2441, 2073, 1542, 1066, 838, 943, 1334, 1853, 2300, 2508, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2441, 2098, 1590, 1111, 849, 913, 1283, 1815, 2290, 2509, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2442, 2080, 1554, 1079, 841, 929, 1303, 1814, 2269, 2501, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2421, 2061, 1553, 1088, 844, 922, 1297, 1824, 2293, 2509, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2445, 2093, 1574, 1097, 846, 913, 1266, 1768, 2231, 2489, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2394, 2016, 1509, 1060, 839, 935, 1318, 1842, 2301, 2510, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2452, 2112, 1602, 1122, 854, 895, 1223, 1712, 2184, 2471, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2359, 1962, 1458, 1029, 835, 953, 1346, 1867, 2314, 2511, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2461, 2138, 1638, 1154, 866, 877, 1175, 1649, 2128, 2446, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2512, 2507, 2316, 1899, 1400, 995, 832, 977, 1382, 1899, 2332, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2472, 2169, 1682, 1194, 884, 859, 1124, 1578, 2062, 2410, 2512, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2512, 2494, 2262, 1828, 1337, 960, 833, 1007, 1426, 1938, 2353, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2483, 2206, 1733, 1241, 907, 845, 1071, 1501, 1985, 2363, 2512, 2512, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2512, 2470, 2197, 1747, 1268, 925, 839, 1045, 1478, 1984, 2378, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2494, 2246, 1792, 1298, 939, 835, 1017, 1417, 1898, 2302, 2502, 2512, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2512, 2434, 2119, 1657, 1197, 892, 851, 1092, 1538, 2035, 2404, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2504, 2289, 1858, 1364, 980, 832, 965, 1329, 1800, 2228, 2479, 2512, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2512, 2512, 2381, 2029, 1560, 1124, 864, 871, 1149, 1608, 2092, 2431, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2510, 2334, 1929, 1440, 1032, 838, 918, 1238, 1693, 2138, 2437, 2512, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2505, 2503, 2313, 1926, 1457, 1053, 844, 902, 1216, 1685, 2153, 2457, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2378, 2006, 1525, 1096, 856, 878, 1148, 1577, 2032, 2376, 2505, 1904, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1904, 2467, 2225, 1811, 1350, 986, 833, 946, 1294, 1770, 2216, 2480, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2421, 2087, 1620, 1174, 888, 848, 1061, 1456, 1912, 2288, 1894, 1023, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1023, 1847, 2115, 1685, 1243, 926, 835, 1004, 1385, 1861, 2279, 2499, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2458, 2169, 1723, 1265, 936, 833, 982, 1332, 1774, 1699, 1016, 925, 1730, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1730, 925, 1004, 1575, 1548, 1138, 878, 853, 1077, 1486, 1957, 2341, 2510, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2487, 2250, 1833, 1370, 1002, 836, 915, 1209, 1344, 972, 918, 1729, 2464, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2464, 1710, 910, 948, 1203, 1039, 845, 890, 1167, 1599, 2056, 2398, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2506, 2326, 1948, 1489, 1088, 860, 865, 1000, 906, 892, 1607, 2441, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1518, 880, 882, 910, 832, 947, 1273, 1720, 2155, 2447, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
                { 836, 1416, 2305, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2397, 1569, 866, 1137, 2054, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2016, 1105, 878, 1610, 2417, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2365, 1510, 851, 1184, 2106, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2511, 1961, 1062, 900, 1670, 2443, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2151, 1228, 841, 1458, 2333, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2395, 2063, 1620, 1195, 908, 835, 850, 860, 1325, 2116, 2427, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2290, 1395, 834, 1285, 2204, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2496, 1847, 987, 955, 1789, 2482, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2248, 1339, 832, 1339, 2248, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2482, 1789, 955, 987, 1847, 2496, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2204, 1285, 834, 1395, 2290, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2495, 2311, 1932, 1210, 849, 838, 839, 1027, 1397, 1848, 2251, 2485, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2156, 1234, 840, 1452, 2329, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2443, 1670, 900, 1062, 1961, 2511, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2107, 1185, 851, 1509, 2364, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2417, 1610, 878, 1105, 2016, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2055, 1137, 865, 1568, 2396, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2512, 2387, 1551, 861 },
            };

            for (int line = 0; line < 38; line++)
            {
                for (int i = 0; i < 494; i++)
                {
                    samples.Frame0[line + 488].SamplesY[i + 256] = (ushort)fixedSamples[line, i];
                    samples.Frame1[line + 488].SamplesY[i + 256] = (ushort)fixedSamples[line, i];
                }
            }
        }
    }
}
