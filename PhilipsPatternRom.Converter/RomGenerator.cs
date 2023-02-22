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

        private List<LineSamples> _patternLineSamples { get; set; }

        private List<Tuple<ConvertedPattern, ConvertedPattern>> _convertedComponents;

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

            var patternSamples = new PatternSamples { IsDigital = useDigitalFactors };

            _invertLuma = useDigitalFactors;
            _invertChroma = useDigitalFactors;
            _useDigitalFactors = useDigitalFactors;

            LoadSamples(patternSamples, directory);
            PatternFixes.ApplyPatternFixes(patternSamples, fixes);

            patternSamples.Do422Conversion();

            var ap1 = ConvertPatternPal(patternSamples.Frame0, 0);
            var ap2 = ConvertPatternPal(patternSamples.Frame1, 580);

            _convertedComponents.Add(new Tuple<ConvertedPattern, ConvertedPattern>(ap1, ap2));
        }

        public void AddRegular(string directory, bool useDigitalFactors, PatternFixType fixes)
        {
            var patternSamples = new PatternSamples { IsDigital = useDigitalFactors };

            _invertLuma = useDigitalFactors;
            _invertChroma = useDigitalFactors;
            _useDigitalFactors = useDigitalFactors;

            LoadSamples(patternSamples, directory);
            PatternFixes.ApplyPatternFixes(patternSamples, fixes);

            patternSamples.Do422Conversion();

            var lastPattern = _convertedComponents.LastOrDefault();

            ConvertedPattern ap1 = null;

            switch (_romManager.Standard)
            {
                case GeneratorStandard.PAL:
                case GeneratorStandard.PAL_M:
                    ap1 = ConvertPatternPal(patternSamples.Frame0, 0);
                    break;
                case GeneratorStandard.NTSC:
                    ap1 = ConvertPatternNtsc(patternSamples.Frame0, 0);
                    break;
                default:
                    throw new NotImplementedException();
            }

            _convertedComponents.Add(new Tuple<ConvertedPattern, ConvertedPattern>(ap1, null));
        }

        public void Save(string directory, int outputPatternIndex)
        {
            _romManager.SetFilenamesAndSize(_outputType);
            _romManager.AppendComponents(_convertedComponents, _patternLineSamples, outputPatternIndex);
            _romManager.SaveSet(directory);
        }

        public void Init(GeneratorType type, GeneratorType outputType, string directory, int patternIndex)
        {
            _convertedComponents = new List<Tuple<ConvertedPattern, ConvertedPattern>>();
            _patternLineSamples = new List<LineSamples>();

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

        private ConvertedPattern ConvertPatternPal(List<LineSamples> samples, int baseVector)
        {
            var ret = new ConvertedPattern
            {
                VectorTable = new Dictionary<int, Tuple<byte, byte, byte>>()
            };

            int i = 0;
            int line = 0;
            int offset;

            // Right side castellation
            offset = ConvertAllSamples(samples[line++]);
            ret.VectorTable[baseVector] = GetVectorForOffset(offset);

            line += 3;

            for (i = 2; i < (_linesPerField - 4); i++)
            {
                var alternateField = i + _linesPerField + 1;

                offset = ConvertAllSamples(samples[line++]);
                ret.VectorTable[baseVector + i] = GetVectorForOffset(offset);

                offset = ConvertAllSamples(samples[line++]);
                ret.VectorTable[baseVector + alternateField] = GetVectorForOffset(offset);
            }

            offset = ConvertAllSamples(samples[line++]);
            ret.VectorTable[baseVector + 1] = GetVectorForOffset(offset);

            offset = ConvertAllSamples(samples[line++]);
            ret.VectorTable[baseVector + _linesPerField + 1] = GetVectorForOffset(offset);

            // Skip the second to last line. Only want the last line.
            line++;

            // Left side castellation
            offset = ConvertAllSamples(samples[line++]);
            ret.VectorTable[baseVector + 291] = GetVectorForOffset(offset);

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

            return ret;
        }

        private ConvertedPattern ConvertPatternNtsc(List<LineSamples> samples, int baseVector)
        {
            var ret = new ConvertedPattern
            {
                VectorTable = new Dictionary<int, Tuple<byte, byte, byte>>()
            };

            int i = 0;
            int line = 0;
            int offset;


            offset = ConvertAllSamples(samples[1]);
            ret.VectorTable[0] = GetVectorForOffset(offset);
            ret.VectorTable[486] = GetVectorForOffset(offset);
            ret.VectorTable[487] = GetVectorForOffset(offset);
            ret.VectorTable[488] = GetVectorForOffset(offset);
            ret.VectorTable[489] = GetVectorForOffset(offset);

            offset = ConvertAllSamples(samples[485]);
            ret.VectorTable[1] = GetVectorForOffset(offset);

            offset = GenerateBlankLine();
            ret.VectorTable[243] = GetVectorForOffset(offset);
            ret.VectorTable[244] = GetVectorForOffset(offset);

            line += 2;

            for (i = 2; i < _linesPerField - 2; i++)
            {
                var alternateField = baseVector + i + _linesPerField - 1;

                offset = ConvertAllSamples(samples[line++]);
                ret.VectorTable[alternateField] = GetVectorForOffset(offset);

                offset = ConvertAllSamples(samples[line++]);
                ret.VectorTable[baseVector + i] = GetVectorForOffset(offset);
            }

            offset = ConvertAllSamples(samples[0]);
            ret.VectorTable[245] = GetVectorForOffset(offset);

            return ret;
        }

        private int StoreLineAndGetOffset(LineSamples samples)
        {
            var existing = _patternLineSamples.SingleOrDefault(el => el.LineHashCode == samples.LineHashCode);
            if (existing != null)
                return (_patternLineSamples.IndexOf(existing) * _lineLength) + _targetOffset;

            _patternLineSamples.Add(samples);

            return (_patternLineSamples.IndexOf(samples) * _lineLength) + _targetOffset;
        }

        private int ConvertAllSamples(LineSamples samples)
        {
            return StoreLineAndGetOffset(new LineSamples(BuildSamples(samples, PatternType.Luma), BuildSamples(samples, PatternType.RminusY), BuildSamples(samples, PatternType.BminusY)));
        }

        private int GenerateBlankLine()
        {
            return StoreLineAndGetOffset(new LineSamples(Enumerable.Repeat((ushort)724, 1024).ToList(), Enumerable.Repeat((ushort)128, 512).ToList(), Enumerable.Repeat((ushort)128, 512).ToList()));
        }

        private List<ushort> BuildSamples(LineSamples samples, PatternType type)
        {
            var lineSamples = new List<ushort>();
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
                lineLength /= 2;
                centreSize /= 2;
                totalSize /= 2;
                backPorchSize /= 2;
            }

            switch (type)
            {
                case PatternType.Luma:
                    lineSamples.AddRange(_existingYBackPorchSamples.Select(el => el));
                    break;
                case PatternType.RminusY:
                    lineSamples.AddRange(_existingRyBackPorchSamples.Select(el => (ushort)el));
                    break;
                case PatternType.BminusY:
                    lineSamples.AddRange(_existingByBackPorchSamples.Select(el => (ushort)el));
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

            var finalSamples = new List<ushort>();

            // Lay the samples out as per vector type 2, which works rather nicely for this
            finalSamples.AddRange(lineSamples.Skip(backPorchSize).Take(centreSize));
            finalSamples.AddRange(lineSamples.Take(backPorchSize));
            finalSamples.AddRange(lineSamples.Skip(maxBackAndCentreSamples));
            finalSamples.AddRange(Enumerable.Repeat((ushort)0xFFFF, totalSize - finalSamples.Count));

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
    }
}
