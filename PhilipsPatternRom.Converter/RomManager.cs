﻿using PhilipsPatternRom.Converter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter
{
    public class RomManager
    {
        public List<byte> LuminanceSamples { get; set; }
        public List<ushort> LuminanceSamplesFull { get; set; }
        public List<byte> LuminanceLsbSamples { get; set; }
        public List<byte> ChrominanceRySamples { get; set; }
        public List<byte> ChrominanceBySamples { get; set; }
        public List<byte> VectorTable { get; set; }

        public GeneratorStandard Standard { get; private set; }

        private int _romSize { get; set; }

        public int RomSize {  get { return _romSize; } }
        public int PatternDataStart { get { return _patternDataStart; } }

        private List<RomPart> _set;
        bool _clearExisting;
        private GeneratorType _type;
        private int _patternDataStart;
        private int _vectorTableStart;
        private int _vectorTableLength;

        public RomManager()
        {
            LuminanceSamples = new List<byte>();
            LuminanceSamplesFull = new List<ushort>();
            LuminanceLsbSamples = new List<byte>();
            ChrominanceRySamples = new List<byte>();
            ChrominanceBySamples = new List<byte>();
        }

        private static Generator[] _generators =
        {
            new Generator
            {
                Type = GeneratorType.Pm5644g00,
                Standard = GeneratorStandard.PAL,
                PatternDataStart = 0x500,
                VectorTableStart = 0x52F6,
                VectorTableLength = 0xD98,
                RomParts = new List<RomPart>
                {
                    new RomPart(RomType.Luminance0,     "EPROM_4008_102_56191_CSUM_9A6A.BIN", 0, 0x10000),
                    new RomPart(RomType.Luminance1,     "EPROM_4008_102_56201_CSUM_4E67.BIN", 0, 0x10000),
                    new RomPart(RomType.Luminance2,     "EPROM_4008_102_56211_CSUM_3172.BIN", 0, 0x10000),
                    new RomPart(RomType.Luminance3,     "EPROM_4008_102_56221_CSUM_7F95.BIN", 0, 0x10000),
                    new RomPart(RomType.LuminanceLSB,   "EPROM_4008_102_56231_CSUM_78C4.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceRY0, "EPROM_4008_102_56241_CSUM_F0DF.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceRY1, "EPROM_4008_102_56251_CSUM_F397.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceBY0, "EPROM_4008_102_56261_CSUM_2DA9.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceBY1, "EPROM_4008_102_56271_CSUM_2E0F.BIN", 0, 0x10000),
                    new RomPart(RomType.CPU,            "EPROM_4008_102_59371_CSUM_A100.BIN", 0, 0x10000),
                }
            },
            new Generator
            {
                Type = GeneratorType.Pm5644g00MultiPattern,
                Standard = GeneratorStandard.PAL,
                PatternDataStart = 0x500,
                VectorTableStart = 0x52F6,
                VectorTableLength = 0xD98,
                RomParts = new List<RomPart>
                {
                    new RomPart(RomType.Luminance0,     "EPROM_MP_V101_CSUM_[A-F0-9]+\\.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance1,     "EPROM_MP_V103_CSUM_[A-F0-9]+\\.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance2,     "EPROM_MP_V105_CSUM_[A-F0-9]+\\.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance3,     "EPROM_MP_V107_CSUM_[A-F0-9]+\\.BIN", 0, 0x80000),
                    new RomPart(RomType.LuminanceLSB,   "EPROM_MP_V109_CSUM_[A-F0-9]+\\.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceRY0, "EPROM_MP_V201_CSUM_[A-F0-9]+\\.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceRY1, "EPROM_MP_V203_CSUM_[A-F0-9]+\\.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceBY0, "EPROM_MP_V301_CSUM_[A-F0-9]+\\.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceBY1, "EPROM_MP_V303_CSUM_[A-F0-9]+\\.BIN", 0, 0x80000),
                    new RomPart(RomType.CPU,            "EPROM_MP_V12_CSUM_[A-F0-9]+\\.BIN", 0, 0x10000),
                }
            },
            new Generator
            {
                Type = GeneratorType.Pm5644g913,
                Standard = GeneratorStandard.PAL,
                VectorTableStart = 0x50C8,
                VectorTableLength = 0x916,
                RomParts = new List<RomPart>
                {
                    new RomPart(RomType.Luminance0,     "EPROM_4008_102_58802_CSUM_A64A.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance1,     "EPROM_4008_102_58812_CSUM_61E5.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance2,     "EPROM_4008_102_58822_CSUM_3FCA.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance3,     "EPROM_4008_102_58832_CSUM_757D.BIN", 0, 0x80000),
                    new RomPart(RomType.LuminanceLSB,   "EPROM_4008_102_58842_CSUM_E882.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceRY0, "EPROM_4008_102_58852_CSUM_81F9.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceRY1, "EPROM_4008_102_58862_CSUM_81F9.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceBY0, "EPROM_4008_102_58872_CSUM_B4C3.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceBY1, "EPROM_4008_102_58882_CSUM_B4C3.BIN", 0, 0x80000),
                    new RomPart(RomType.CPU,            "EPROM_4008_102_58761_CSUM_1800.BIN", 0, 0x10000),
                }
            },
            new Generator
            {
                Type = GeneratorType.Pm5644g924,
                Standard = GeneratorStandard.PAL_16_9,
                VectorTableStart = 0x6D1D,
                VectorTableLength = 0x918,
                RomParts = new List<RomPart>
                {
                    new RomPart(RomType.Luminance0,     "EPROM_4008_002_00551_CSUM_9F93.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance1,     "EPROM_4008_002_00561_CSUM_3998.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance2,     "EPROM_4008_002_00571_CSUM_5608.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance3,     "EPROM_4008_002_00581_CSUM_739C.BIN", 0, 0x80000),
                    new RomPart(RomType.LuminanceLSB,   "EPROM_4008_002_00591_CSUM_1C84.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceRY0, "EPROM_4008_002_00601_CSUM_F478.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceRY1, "EPROM_4008_002_00611_CSUM_F167.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceBY0, "EPROM_4008_002_00621_CSUM_06E1.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceBY1, "EPROM_4008_002_00631_CSUM_FFB8.BIN", 0, 0x80000),
                    new RomPart(RomType.CPU,            "EPROM_4008_002_00541_CSUM_B700.BIN", 0, 0x10000),
                }
            },
            new Generator
            {
                Type = GeneratorType.Pm5644m00,
                Standard = GeneratorStandard.NTSC,
                VectorTableStart = 0x5314,
                VectorTableLength = 0x5BE,
                RomParts = new List<RomPart>
                {
                    new RomPart(RomType.Luminance0,     "EPROM_4008_102_56741_CSUM_0D4A.BIN", 0, 0x10000),
                    new RomPart(RomType.Luminance1,     "EPROM_4008_102_56751_CSUM_F7B6.BIN", 0, 0x10000),
                    new RomPart(RomType.Luminance2,     "EPROM_4008_102_56761_CSUM_03DF.BIN", 0, 0x10000),
                    new RomPart(RomType.Luminance3,     "EPROM_4008_102_56771_CSUM_1A71.BIN", 0, 0x10000),
                    new RomPart(RomType.LuminanceLSB,   "EPROM_4008_102_56781_CSUM_8E52.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceRY0, "EPROM_4008_102_56791_CSUM_C1EA.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceRY1, "EPROM_4008_102_56801_CSUM_C1D0.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceBY0, "EPROM_4008_102_56811_CSUM_B3AC.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceBY1, "EPROM_4008_102_56821_CSUM_B3EC.BIN", 0, 0x10000),
                    new RomPart(RomType.CPU,            "EPROM_4008_102_59401_CSUM_7300.BIN", 0, 0x10000),
                }
            },
            new Generator
            {
                Type = GeneratorType.Pm5644m00MultiPattern,
                Standard = GeneratorStandard.NTSC,
                VectorTableStart = 0x5314,
                VectorTableLength = 0x5BE,
                RomParts = new List<RomPart>
                {
                    new RomPart(RomType.Luminance0,     "EPROM_4008_102_56741_CSUM_0D4A.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance1,     "EPROM_4008_102_56751_CSUM_F7B6.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance2,     "EPROM_4008_102_56761_CSUM_03DF.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance3,     "EPROM_4008_102_56771_CSUM_1A71.BIN", 0, 0x80000),
                    new RomPart(RomType.LuminanceLSB,   "EPROM_4008_102_56781_CSUM_8E52.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceRY0, "EPROM_4008_102_56791_CSUM_C1EA.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceRY1, "EPROM_4008_102_56801_CSUM_C1D0.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceBY0, "EPROM_4008_102_56811_CSUM_B3AC.BIN", 0, 0x80000),
                    new RomPart(RomType.ChrominanceBY1, "EPROM_4008_102_56821_CSUM_B3EC.BIN", 0, 0x80000),
                    new RomPart(RomType.CPU,            "EPROM_4008_102_59401_CSUM_7300.BIN", 0, 0x10000),
                }
            },
            new Generator
            {
                Type = GeneratorType.Pm5644p00,
                Standard = GeneratorStandard.PAL_M,
                VectorTableStart = 0x5314,
                VectorTableLength = 0x5BE,
                RomParts = new List<RomPart>
                {
                    new RomPart(RomType.Luminance0,     "EPROM_4008_102_57161_CSUM_E601.BIN", 0, 0x10000),
                    new RomPart(RomType.Luminance1,     "EPROM_4008_102_57171_CSUM_D3B6.BIN", 0, 0x10000),
                    new RomPart(RomType.Luminance2,     "EPROM_4008_102_57181_CSUM_DE42.BIN", 0, 0x10000),
                    new RomPart(RomType.Luminance3,     "EPROM_4008_102_57191_CSUM_F178.BIN", 0, 0x10000),
                    new RomPart(RomType.LuminanceLSB,   "EPROM_4008_102_57201_CSUM_E138.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceRY0, "EPROM_4008_102_56851_CSUM_0379.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceRY1, "EPROM_4008_102_56861_CSUM_0393.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceBY0, "EPROM_4008_102_56831_CSUM_6C99.BIN", 0, 0x10000),
                    new RomPart(RomType.ChrominanceBY1, "EPROM_4008_102_56841_CSUM_6CF9.BIN", 0, 0x10000),
                    new RomPart(RomType.CPU,            "EPROM_4008_102_59391_CSUM_0D00.BIN", 0, 0x10000),
                }
            },
            new Generator
            {
                Type = GeneratorType.Pm5644l00,
                Standard = GeneratorStandard.SECAM,
                VectorTableStart = 0x568B,
                VectorTableLength = 0x48A,
                RomParts = new List<RomPart>
                {
                    new RomPart(RomType.Luminance0,     "EPROM_4008_002_01621_CSUM_C923.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance1,     "EPROM_4008_002_01631_CSUM_00D6.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance2,     "EPROM_4008_002_01641_CSUM_C9D6.BIN", 0, 0x80000),
                    new RomPart(RomType.Luminance3,     "EPROM_4008_002_01651_CSUM_9C03.BIN", 0, 0x80000),
                    new RomPart(RomType.LuminanceLSB,   "EPROM_4008_002_01661_CSUM_00E2.BIN", 0, 0x80000),
                    new RomPart(RomType.CPU,            "EPROM_4008_002_01671_CSUM_CD00.BIN", 0, 0x10000),
                }
            },
        };

        public void OpenSet(GeneratorType type, string directory, bool clearExisting, int vectorTableIndex)
        {
            Directory.SetCurrentDirectory(directory);

            LuminanceSamples.Clear();
            ChrominanceRySamples.Clear();
            ChrominanceBySamples.Clear();

            var generator = _generators.Single(g => g.Type == type);
            _clearExisting = clearExisting;
            _set = generator.RomParts;
            _type = type;
            _patternDataStart = generator.PatternDataStart;
            _vectorTableStart = generator.VectorTableStart;
            _vectorTableLength = generator.VectorTableLength;
            Standard = generator.Standard;

            foreach (var rom in _set)
                rom.Load();

            _romSize = _set.Single(el => el.Type == RomType.Luminance0).Length;

            var lum0Data = _set.Single(el => el.Type == RomType.Luminance0).Data;
            var lum1Data = _set.Single(el => el.Type == RomType.Luminance1).Data;
            var lum2Data = _set.Single(el => el.Type == RomType.Luminance2).Data;
            var lum3Data = _set.Single(el => el.Type == RomType.Luminance3).Data;
            var lumLsb = _set.Single(el => el.Type == RomType.LuminanceLSB).Data;

            for (int i = 0; i < _set[0].Data.Length; i++)
            {
                LuminanceSamples.Add(lum0Data[i]);
                LuminanceSamples.Add(lum1Data[i]);
                LuminanceSamples.Add(lum2Data[i]);
                LuminanceSamples.Add(lum3Data[i]);

                /* 
                 * LSBs live in "V109"
                 * Data pins of V109 are connected to the 74HC153 in expected order i.e. D0->1I0 D7->2I3
                 * The bits selected by the 74HC153 exactly match the sequence the MSB roms are selected in
                 * i.e. ROM0 -> D0/D4, ROM1 -> D1/D5, ROM2 -> D2/D6, ROM3 -> D3/D7
                 * Finally 1Y of the 74HC153 is connected pin 2 of the TDC1012 (D10) - least significant used bit
                 * 2Y of the 74HC153 is connected to pin 1 of the TDC1012 (D9)
                 * 
                 * Therefore the below logic is the correct method of decoding the LSB EPROM V109.
                 */

                LuminanceSamplesFull.Add((ushort)(lum0Data[i] << 2 | (lumLsb[i] & 0x10) >> 3 | (lumLsb[i] & 0x01) >> 0));
                LuminanceSamplesFull.Add((ushort)(lum1Data[i] << 2 | (lumLsb[i] & 0x20) >> 4 | (lumLsb[i] & 0x02) >> 1));
                LuminanceSamplesFull.Add((ushort)(lum2Data[i] << 2 | (lumLsb[i] & 0x40) >> 5 | (lumLsb[i] & 0x04) >> 2));
                LuminanceSamplesFull.Add((ushort)(lum3Data[i] << 2 | (lumLsb[i] & 0x80) >> 6 | (lumLsb[i] & 0x08) >> 3));
            }

            LuminanceLsbSamples.AddRange(_set.Single(el => el.Type == RomType.LuminanceLSB).Data);

            VectorTable = _set.Single(el => el.Type == RomType.CPU).Data.Skip(_vectorTableStart +
                (_vectorTableLength * vectorTableIndex)).Take(_vectorTableLength).ToList();

            if (generator.Standard == GeneratorStandard.SECAM)
                return;

            var chromRy0Data = _set.Single(el => el.Type == RomType.ChrominanceRY0).Data;
            var chromRy1Data = _set.Single(el => el.Type == RomType.ChrominanceRY1).Data;

            for (int i = 0; i < _set[0].Data.Length; i++)
            {
                ChrominanceRySamples.Add(chromRy0Data[i]);
                ChrominanceRySamples.Add(chromRy1Data[i]);
            }

            var chromBy0Data = _set.Single(el => el.Type == RomType.ChrominanceBY0).Data;
            var chromBy1Data = _set.Single(el => el.Type == RomType.ChrominanceBY1).Data;

            for (int i = 0; i < _set[0].Data.Length; i++)
            {
                ChrominanceBySamples.Add(chromBy0Data[i]);
                ChrominanceBySamples.Add(chromBy1Data[i]);
            }

            ApplySourceFixes();
        }

        public void ApplySourceFixes()
        {
            if (_type == GeneratorType.Pm5644g00)
            {
                // Remove the "blip" at the right side of the circle.
                // It was probably put there for a good reason but it looks ugly
                // and does not appear in later versions of the pattern.
                ChrominanceRySamples[(0x5E70 * 2) - 1] = 0x8A;
                ChrominanceRySamples[0x5EF0 * 2] = 0x8A;
                ChrominanceRySamples[0x5F70 * 2] = 0x8A;
                ChrominanceRySamples[0x5FF0 * 2] = 0x8A;
                ChrominanceRySamples[0x6070 * 2] = 0x8A;
                ChrominanceRySamples[0x60F0 * 2] = 0x8A;

                // Reinstate the bottom box reflection check
                var pulse = new ushort[] { 724, 636, 367, 174, 244, 510, 712, 724 };
                var lines = new[] { 141454, 163982, 164494, 165006, 165518, 166030, 166542, 167054, 167566,
                168078, 168590, 169102, 169614, 170126, 170638, 171150, 171662, 172174, 172686, 173710,
                173198, 131726, 174222, 174734, 175246, 175758, 176270, 176782, 177294, 177806, 178318,
                178830, 179342, 179854, 180366, 180878, 181390, 181902, 182414, 182926 };

                foreach (var line in lines)
                {
                    for (int i = 0; i < pulse.Length; i++)
                        LuminanceSamplesFull[line + i] = pulse[i];
                }
            }
        }

        public void SetFilenamesAndSize(GeneratorType type)
        {
            var newSet = _generators.Single(g => g.Type == type);
            foreach (var rom in _set)
                rom.FileName = newSet.RomParts.Single(rp => rp.Type == rom.Type).FileName;

            _romSize = newSet.RomParts.Single(rp => rp.Type == RomType.Luminance0).Length;
        }

        public void SaveSet(string directory)
        {
            var romLength = LuminanceSamplesFull.Count / 4;

            if (romLength > _romSize)
            {
                throw new Exception("Too much pattern data");
            }

            _set.Single(el => el.Type == RomType.Luminance0).Data = Enumerable.Repeat((byte)0xFF, _romSize).ToArray();
            _set.Single(el => el.Type == RomType.Luminance1).Data = Enumerable.Repeat((byte)0xFF, _romSize).ToArray();
            _set.Single(el => el.Type == RomType.Luminance2).Data = Enumerable.Repeat((byte)0xFF, _romSize).ToArray();
            _set.Single(el => el.Type == RomType.Luminance3).Data = Enumerable.Repeat((byte)0xFF, _romSize).ToArray();
            _set.Single(el => el.Type == RomType.LuminanceLSB).Data = Enumerable.Repeat((byte)0xFF, _romSize).ToArray();

            _set.Single(el => el.Type == RomType.ChrominanceRY0).Data = Enumerable.Repeat((byte)0xFF, _romSize).ToArray();
            _set.Single(el => el.Type == RomType.ChrominanceRY1).Data = Enumerable.Repeat((byte)0xFF, _romSize).ToArray();
            _set.Single(el => el.Type == RomType.ChrominanceBY0).Data = Enumerable.Repeat((byte)0xFF, _romSize).ToArray();
            _set.Single(el => el.Type == RomType.ChrominanceBY1).Data = Enumerable.Repeat((byte)0xFF, _romSize).ToArray();

            for (int i = 0; i < romLength; i++)
            {
                _set.Single(el => el.Type == RomType.Luminance0).Data[i] = (byte)(LuminanceSamplesFull[(4 * i) + 0] >> 2);
                _set.Single(el => el.Type == RomType.Luminance1).Data[i] = (byte)(LuminanceSamplesFull[(4 * i) + 1] >> 2);
                _set.Single(el => el.Type == RomType.Luminance2).Data[i] = (byte)(LuminanceSamplesFull[(4 * i) + 2] >> 2);
                _set.Single(el => el.Type == RomType.Luminance3).Data[i] = (byte)(LuminanceSamplesFull[(4 * i) + 3] >> 2);

                byte lsb = 0;

                if ((LuminanceSamplesFull[(4 * i) + 0] & 0x01) == 0x01)
                    lsb |= 0x01;

                if ((LuminanceSamplesFull[(4 * i) + 0] & 0x02) == 0x02)
                    lsb |= 0x10;

                if ((LuminanceSamplesFull[(4 * i) + 1] & 0x01) == 0x01)
                    lsb |= 0x02;

                if ((LuminanceSamplesFull[(4 * i) + 1] & 0x02) == 0x02)
                    lsb |= 0x20;

                if ((LuminanceSamplesFull[(4 * i) + 2] & 0x01) == 0x01)
                    lsb |= 0x04;

                if ((LuminanceSamplesFull[(4 * i) + 2] & 0x02) == 0x02)
                    lsb |= 0x40;

                if ((LuminanceSamplesFull[(4 * i) + 3] & 0x01) == 0x01)
                    lsb |= 0x08;

                if ((LuminanceSamplesFull[(4 * i) + 3] & 0x02) == 0x02)
                    lsb |= 0x80;

                _set.Single(el => el.Type == RomType.LuminanceLSB).Data[i] = lsb;
            }

            for (int i = 0; i < romLength; i++)
            {
                _set.Single(el => el.Type == RomType.ChrominanceRY0).Data[i] = ChrominanceRySamples[(2 * i) + 0];
                _set.Single(el => el.Type == RomType.ChrominanceRY1).Data[i] = ChrominanceRySamples[(2 * i) + 1];
            }

            for (int i = 0; i < romLength; i++)
            {
                _set.Single(el => el.Type == RomType.ChrominanceBY0).Data[i] = ChrominanceBySamples[(2 * i) + 0];
                _set.Single(el => el.Type == RomType.ChrominanceBY1).Data[i] = ChrominanceBySamples[(2 * i) + 1];
            }

            Directory.CreateDirectory(directory);
            Directory.SetCurrentDirectory(directory);

            foreach (var rom in _set)
                rom.Save();
        }

        public void AppendComponents(List<Tuple<ConvertedPattern, ConvertedPattern>> componentsSet, List<LineSamples> lines, int outputPatternIndex)
        {
            var vectorTableEntryCount = VectorTable.Count / 3;

            if (_clearExisting)
            {
                LuminanceSamplesFull.RemoveRange(_patternDataStart * 4, LuminanceSamplesFull.Count - (_patternDataStart * 4));
                ChrominanceRySamples.RemoveRange(_patternDataStart * 2, ChrominanceRySamples.Count - (_patternDataStart * 2));
                ChrominanceBySamples.RemoveRange(_patternDataStart * 2, ChrominanceBySamples.Count - (_patternDataStart * 2));
            }

            foreach (var line in lines)
            {
                LuminanceSamplesFull.AddRange(line.SamplesY);
                ChrominanceRySamples.AddRange(line.SamplesRy.Select(el => (byte)el));
                ChrominanceBySamples.AddRange(line.SamplesBy.Select(el => (byte)el));
            }

            foreach (var components in componentsSet)
            {
                var vectorTable = new List<byte>();

                for (int i = 0; i < (vectorTableEntryCount / (Standard == GeneratorStandard.NTSC ? 1 : 2)); i++)
                {
                    var entry = components.Item1.VectorTable[i];

                    vectorTable.Add(entry.Item1);
                    vectorTable.Add(entry.Item2);
                    vectorTable.Add(entry.Item3);
                }

                // NTSC version has no anti-PAL, so each pattern only has one frame.
                // The vector table is only half as long.
                if (Standard != GeneratorStandard.NTSC)
                {
                    if (components.Item2 != null)
                    {
                        // anti-PAL pattern supplied. Copy in its vector table
                        for (int i = (vectorTableEntryCount / 2); i < vectorTableEntryCount; i++)
                        {
                            var entry = components.Item2.VectorTable[i];

                            vectorTable.Add(entry.Item1);
                            vectorTable.Add(entry.Item2);
                            vectorTable.Add(entry.Item3);
                        }
                    }
                    else
                    {
                        // non anti-PAL pattern. Duplicate the first (only) vector table
                        for (int i = 0; i < (vectorTableEntryCount / 2); i++)
                        {
                            var entry = components.Item1.VectorTable[i];

                            vectorTable.Add(entry.Item1);
                            vectorTable.Add(entry.Item2);
                            vectorTable.Add(entry.Item3);
                        }
                    }
                }

                var set = _set.Single(el => el.Type == RomType.CPU);
                var tableStart = _vectorTableStart + (_vectorTableLength * outputPatternIndex);

                for (int i = tableStart; i < (tableStart + _vectorTableLength); i++)
                    set.Data[i] = vectorTable[i - tableStart];

                outputPatternIndex++;
            }
        }
    }
}
