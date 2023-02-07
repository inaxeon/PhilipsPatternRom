using PhilipsPatternRom.Converter.Extensions;
using PhilipsPatternRom.Converter.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter
{
    public class PatternRenderer
    {
        private RomManager _romManager;
        private PatternData _patternData;
        private List<Stripe> _stripes;
        private List<Tuple<byte, byte, byte>> _vectorEntries;

        private int? _hsMarker;
        private int? _vsMarker;
        private int? _heMarker;
        private int? _veMarker;

        private int _vline;
        private int _hsample;
        private int _stripeStart;
        private int _centreLength;
        private int _backSpriteLength;
        private int _frontSpriteLength;
        private GeneratorType _type;

        // Offset from stripe generator UI for various experimental fiddling
        public int Offset { get; set; }

        public delegate void PixelRenderer(Bitmap bitmap, int start, int finish, bool mark);

        public PatternRenderer()
        {
            _romManager = new RomManager();
            _stripes = new List<Stripe>();
            _vectorEntries = new List<Tuple<byte, byte, byte>>();
        }

        public void LoadPattern(GeneratorType type, string directory, int patternIndex)
        {
            _romManager.OpenSet(type, directory, patternIndex);
            _type = type;

            LoadVectors();

            switch (_romManager.Standard)
            {
                case GeneratorStandard.PAL:
                    _centreLength = 120;
                    _frontSpriteLength = 32;
                    _backSpriteLength = 64;
                    break;
                case GeneratorStandard.PAL_16_9:
                    // Technically it's just "one-half" and the "other-half"
                    _backSpriteLength = 256;
                    _centreLength = 256;
                    break;
                case GeneratorStandard.NTSC:
                case GeneratorStandard.PAL_M:
                    _centreLength = 128;
                    _frontSpriteLength = 32;
                    _backSpriteLength = 64;
                    break;
            }
        }

        private void LoadVectors()
        {
            _vectorEntries = Utility.LoadVectors(_romManager, _romManager.Standard);
        }

        public void SetMarkers(int xs, int xe, int ys, int ye)
        {
            _hsMarker = xs;
            _heMarker = xe;
            _vsMarker = ys;
            _veMarker = ye;
        }

        public void ClearMarkers()
        {
            _hsMarker = null;
            _heMarker = null;
            _vsMarker = null;
            _veMarker = null;
        }

        public PatternComponents GeneratePatternComponents()
        {
            var components = new PatternComponents();
            _patternData = new PatternData();

            components.Luma = GenerateBitmapFromSpriteRomSet(PatternType.Luma, PatternSubType.DeInterlaced);
            components.ChromaRy = GenerateBitmapFromSpriteRomSet(PatternType.RminusY, PatternSubType.DeInterlaced);
            components.ChromaBy = GenerateBitmapFromSpriteRomSet(PatternType.BminusY, PatternSubType.DeInterlaced);
            components.Standard = _romManager.Standard;

            return components;
        }

        private Bitmap GenerateBitmapFromSpriteRomSet(PatternType type, PatternSubType subType)
        {
            var centreLength = _centreLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);
            var backSpriteLength = _backSpriteLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);
            var frontSpriteLength = _frontSpriteLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);

            var linesPerField = 0;

            switch (_romManager.Standard)
            {
                case GeneratorStandard.PAL:
                    linesPerField = _vectorEntries.Count / 4;
                    break;
                case GeneratorStandard.NTSC:
                    linesPerField = _vectorEntries.Count / 6;
                    break;
                case GeneratorStandard.PAL_M:
                    linesPerField = _vectorEntries.Count / 6;
                    linesPerField--;
                    break;
                case GeneratorStandard.PAL_16_9:
                    linesPerField = _vectorEntries.Count / 4;
                    break;
                default:
                    throw new NotImplementedException();
            }
       
            var lineWidth = backSpriteLength + centreLength + frontSpriteLength;
            var bitmap = new Bitmap(lineWidth, subType == PatternSubType.DeInterlaced ? linesPerField * 2 : linesPerField);

            _stripes.Clear();
            _hsample = 0;
            _vline = 0;

            if (_romManager.Standard == GeneratorStandard.PAL_16_9)
            {
                for (int i = 0; i < linesPerField; i++)
                {
                    var entry = _vectorEntries[i + linesPerField];

                    DrawLine_WideScreen(bitmap, type, entry);

                    entry = _vectorEntries[i];
                    DrawLine_WideScreen(bitmap, type, entry);
                }
            }
            else
            {
                DrawLine(bitmap, type, _vectorEntries[0]);
                DrawLine(bitmap, type, _vectorEntries[linesPerField - 1]);

                for (int i = 2; i < (linesPerField - 2); i++)
                {
                    DrawLine(bitmap, type, _vectorEntries[i + linesPerField]);
                    DrawLine(bitmap, type, _vectorEntries[i]);
                }

                DrawLine(bitmap, type, _vectorEntries[1]);
                DrawLine(bitmap, type, _vectorEntries[linesPerField + 1]);
            }

            return bitmap;
        }

        private void DrawLine(Bitmap bitmap, PatternType type, Tuple<byte, byte, byte> entry)
        {
            var romsPerComponent = type == PatternType.Luma ? 4 : 2;
            var centreLength = _centreLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);
            var backSpriteLength = _backSpriteLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);
            var frontSpriteLength = _frontSpriteLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);

            PixelRenderer render = null;

            switch (type)
            {
                case PatternType.Luma:
                    render = DrawPixelsY;
                    break;
                case PatternType.LumaLSB:
                    render = DrawPixelsLSB;
                    break;
                case PatternType.RminusY:
                    render = DrawPixelsRY;
                    break;
                case PatternType.BminusY:
                    render = DrawPixelsBY;
                    break;
            }

            int addr1 = Utility.DecodeVector(entry, Utility.SampleType.BackPorch, romsPerComponent);
            int addr2 = Utility.DecodeVector(entry, Utility.SampleType.Centre, romsPerComponent);
            int addr3 = Utility.DecodeVector(entry, Utility.SampleType.FrontPorch, romsPerComponent);

            var center = addr2 / 4;

            render(bitmap, addr1, addr1 + backSpriteLength, false);
            render(bitmap, addr2, addr2 + centreLength, false);
            render(bitmap, addr3, addr3 + frontSpriteLength, false);

            _vline++;
            _hsample = 0;
        }

        private void DrawLine_WideScreen(Bitmap bitmap, PatternType type, Tuple<byte, byte, byte> entry)
        {
            var romsPerComponent = type == PatternType.Luma ? 4 : 2;
            var backSpriteLength = _backSpriteLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);
            var centreLength = _centreLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);

            PixelRenderer render = null;

            switch (type)
            {
                case PatternType.Luma:
                    render = DrawPixelsY;
                    break;
                case PatternType.LumaLSB:
                    render = DrawPixelsLSB;
                    break;
                case PatternType.RminusY:
                    render = DrawPixelsRY;
                    break;
                case PatternType.BminusY:
                    render = DrawPixelsBY;
                    break;
            }

            byte[] lsbSequence = null;

            var centreAddr = (entry.Item1 << 8);

            if ((entry.Item2 & 0x20) == 0x20)
                centreAddr |= 0x10000;

            if ((entry.Item2 & 0x04) == 0x04)
                centreAddr |= 0x20000;

            if ((entry.Item2 & 0x08) == 0x08)
                centreAddr |= 0x40000;

            int addr1 = (centreAddr * romsPerComponent) - (type == PatternType.Luma ? 1024 : 512);
            int addr2 = (centreAddr * romsPerComponent);

            render(bitmap, addr1, addr1 + backSpriteLength, false);
            render(bitmap, addr2, addr2 + centreLength, false);

            _vline++;
            _hsample = 0;
        }

        public StripeSet GetStripeSet()
        {
            if (_stripes == null || _stripes.Count == 0)
                return null;

            //Stripe duplicate;
            //if (_stripes.HasDuplicate(out duplicate))
            //    throw new Exception($"Duplicate stripe found on line {duplicate.Line}.");

            var ret = new StripeSet { HorizontalStart = _hsMarker.Value, HorizontalEnd = _heMarker.Value, VerticalStart = _vsMarker.Value, VerticalEnd = _veMarker.Value };

            ret.Stripes = _stripes;

            return ret;
        }

        public void DrawPixelsY(Bitmap bitmap, int start, int finish, bool mark)
        {
            for (int i = start; i < finish; i++)
            {
                var setTo = _hsMarker.HasValue && _hsMarker.Value == _hsample ||
                    _heMarker.HasValue && _heMarker.Value == _hsample ||
                    _vsMarker.HasValue && _vsMarker.Value == _vline ||
                    _veMarker.HasValue && _veMarker.Value == _vline
                    || mark ? Color.Red : MonochromeFromByte(_romManager.LuminanceSamples[i]);

                if (_hsMarker.HasValue && _hsMarker.Value == _hsample)
                    _stripeStart = i;

                if (_heMarker.HasValue && _heMarker.Value == _hsample)
                {
                    if (_vline >= _vsMarker.Value && _vline <= _veMarker.Value)
                        _stripes.Add(new Stripe { StartAddress = _stripeStart, EndAddress = i, Line = _vline, CentreAddress = string.Format("0x{0:X4}", start / 4) });
                }

                bitmap.SetPixel(_hsample++, _vline, setTo);
            }

            var newFragment = new PatternFragment { Address = start / 4, Length = (finish - start) / 4 };
            PatternFragment existing = null;

            _patternData.Fragments.TryGetValue(newFragment.GetHashCode(), out existing);

            if (existing != null)
            {
                if (existing.Luminance == null)
                    existing.Luminance = _romManager.LuminanceSamplesFull.Skip(newFragment.Address).Take(newFragment.Length).ToList();
            }
            else
            {
                newFragment.Luminance = _romManager.LuminanceSamplesFull.Skip(newFragment.Address).Take(newFragment.Length).ToList();
                _patternData.Fragments[newFragment.GetHashCode()] = newFragment;
            }
        }

        public void DrawPixelsLSB(Bitmap bitmap, int start, int finish, bool mark)
        {
            for (int i = start; i < finish; i++)
            {
                var addr = i >> 2;
                var bit = (i & 0x03);
                var sample = _romManager.LuminanceLsbSamples[addr];

                var masked = sample & (0x11 << bit);
                var maskedH = masked & 0xF0;
                var maskedL = masked & 0x0F;

                byte setTo = 0;
                if (maskedL != 0)
                    setTo |= 0x01;
                if (maskedH != 0)
                    setTo |= 0x02;

                bitmap.SetPixel(_hsample++, _vline, MonochromeFromByte(setTo));
            }
        }

        public void DrawPixelsRY(Bitmap bitmap, int start, int finish, bool mark)
        {
            for (int i = start; i < finish; i++)
            {
                bitmap.SetPixel(_hsample++, _vline, MonochromeFromByte(_romManager.ChrominanceRySamples[i]));
            }

            var newFragment = new PatternFragment { Address = start / 2, Length = (finish - start) / 2 };
            PatternFragment existing = null;

            _patternData.Fragments.TryGetValue(newFragment.GetHashCode(), out existing);

            if (existing != null)
            {
                if (existing.ChrominanceRY == null)
                    existing.ChrominanceRY = _romManager.ChrominanceRySamples.Skip(newFragment.Address).Take(newFragment.Length).ToList();
            }
            else
            {
                newFragment.ChrominanceRY = _romManager.ChrominanceRySamples.Skip(newFragment.Address).Take(newFragment.Length).ToList();
                _patternData.Fragments[newFragment.GetHashCode()] = newFragment;
            }
        }

        public void DrawPixelsBY(Bitmap bitmap, int start, int finish, bool mark)
        {
            for (int i = start; i < finish; i++)
            {
                bitmap.SetPixel(_hsample++, _vline, MonochromeFromByte(_romManager.ChrominanceBySamples[i]));
            }

            var newFragment = new PatternFragment { Address = start / 2, Length = (finish - start) / 2 };
            PatternFragment existing = null;

            _patternData.Fragments.TryGetValue(newFragment.GetHashCode(), out existing);

            if (existing != null)
            {
                if (existing.ChrominanceBY == null)
                    existing.ChrominanceBY = _romManager.ChrominanceBySamples.Skip(newFragment.Address).Take(newFragment.Length).ToList();
            }
            else
            {
                newFragment.ChrominanceBY = _romManager.ChrominanceBySamples.Skip(newFragment.Address).Take(newFragment.Length).ToList();
                _patternData.Fragments[newFragment.GetHashCode()] = newFragment;
            }
        }

        public Color MonochromeFromByte(byte b)
        {
            return Color.FromArgb(b, b, b);
        }
    }
}
