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
        private ClockMode _clockMode;
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

        public void LoadPattern(GeneratorType type, string directory)
        {
            _romManager.OpenSet(type, directory);
            _type = type;

            LoadVectors(directory);

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
                    _centreLength = 128;
                    _frontSpriteLength = 32;
                    _backSpriteLength = 64;
                    break;
            }
        }

        private void LoadVectors( string directory)
        {
            if (_romManager.Standard == GeneratorStandard.PAL_16_9)
            {
                // Vector table is a different format for 16:9 units. Only two bytes are used per line - Address high and Control
                // Kludge it into the same structure used for 4:3
                for (int i = 0; i < _romManager.VectorTable.Count; i += 2)
                    _vectorEntries.Add(new Tuple<byte, byte, byte>(_romManager.VectorTable[i + 1], _romManager.VectorTable[i + 0], _romManager.VectorTable[i + 1]));
            }
            else
            {
                for (int i = 0; i < _romManager.VectorTable.Count; i += 3)
                    _vectorEntries.Add(new Tuple<byte, byte, byte>(_romManager.VectorTable[i + 0], _romManager.VectorTable[i + 1], _romManager.VectorTable[i + 2]));
            }
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

        public List<PatternComponents> GeneratePatternComponents()
        {
            var ret = new List<PatternComponents>();
            var components = new PatternComponents();
            _patternData = new PatternData();

            components.ClockMode = ClockMode.Off;
            components.ChromaRy = GenerateBitmapFromSpriteRomSet(PatternType.RminusY, PatternSubType.DeInterlaced, ClockMode.Off);
            components.ChromaBy = GenerateBitmapFromSpriteRomSet(PatternType.BminusY, PatternSubType.DeInterlaced, ClockMode.Off);
            components.Luma = GenerateBitmapFromSpriteRomSet(PatternType.Luma, PatternSubType.DeInterlaced, ClockMode.Off);
            components.Standard = _romManager.Standard;

            ret.Add(components);

            //components = new PatternComponents();

            //components.ClockMode = ClockMode.Time;
            //components.Luma = GenerateBitmapFromSpriteRomSet(PatternType.Luma, PatternSubType.DeInterlaced, ClockMode.Time);
            //components.ChromaRy = GenerateBitmapFromSpriteRomSet(PatternType.RminusY, PatternSubType.DeInterlaced, ClockMode.Time);
            //components.ChromaBy = GenerateBitmapFromSpriteRomSet(PatternType.BminusY, PatternSubType.DeInterlaced, ClockMode.Time);

            //ret.Add(components);

            //components = new PatternComponents();

            //components.ClockMode = ClockMode.TimeAndDate;
            //components.Luma = GenerateBitmapFromSpriteRomSet(PatternType.Luma, PatternSubType.DeInterlaced, ClockMode.TimeAndDate);
            //components.ChromaRy = GenerateBitmapFromSpriteRomSet(PatternType.RminusY, PatternSubType.DeInterlaced, ClockMode.TimeAndDate);
            //components.ChromaBy = GenerateBitmapFromSpriteRomSet(PatternType.BminusY, PatternSubType.DeInterlaced, ClockMode.TimeAndDate);

            //ret.Add(components);

            return ret;
        }

        private Bitmap GenerateBitmapFromSpriteRomSet(PatternType type, PatternSubType subType, ClockMode clockMode)
        {
            var centreLength = _centreLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);
            var backSpriteLength = _backSpriteLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);
            var frontSpriteLength = _frontSpriteLength * (type == PatternType.Luma || type == PatternType.LumaLSB ? 4 : 2);
            _clockMode = clockMode;

            var linesPerField = 0;

            switch (_romManager.Standard)
            {
                case GeneratorStandard.PAL:
                    linesPerField = _vectorEntries.Count / 12;
                    break;
                case GeneratorStandard.NTSC:
                    linesPerField = _vectorEntries.Count / 6;
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
                for (int i = 0; i < linesPerField; i++)
                {
                    var entry = _vectorEntries[i + linesPerField];

                    if (i > 0)
                        DrawLine(bitmap, type, entry);

                    entry = _vectorEntries[i];
                    DrawLine(bitmap, type, entry);
                }
            }

            var sorted = _patternData.Fragments.Values.OrderBy(el => el.Address).ToList();

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

            byte[] lsbSequence = null;

            switch (entry.Item1 & 0x03)
            {
                case 0:
                    lsbSequence = new byte[] { 0x00, 0x00, 0x40 };
                    break;
                case 1:
                    lsbSequence = new byte[] { 0x00, 0x80, 0x40 };
                    break;
                case 2:
                    lsbSequence = new byte[] { 0x80, 0x00, 0xC0 };
                    break;
                case 3:
                    lsbSequence = new byte[] { 0x80, 0x80, 0xC0 };
                    break;
            }

            var centreAddr = (entry.Item3 << 8 | lsbSequence[1]);

            // Manually substitute the clock-cutout samples. Don't know how the actual PM5644 does this.
            if (_clockMode == ClockMode.Off && _type == GeneratorType.Pm5644g00)
            {
                if (centreAddr == 0xD380)
                    centreAddr = 0x8180;

                if (centreAddr == 0xD480)
                    centreAddr = 0x8280;

                if (centreAddr == 0xD580)
                    centreAddr = 0x8380;

                if (centreAddr == 0xD680)
                    centreAddr = 0x8480;

                if (centreAddr == 0xD780)
                    centreAddr = 0x8580;

                if (centreAddr == 0xD880)
                    centreAddr = 0x8680;
            }

            // Removing the clock cut-out on the NTSC version is a bastard because the sidebars fall inside the centre segment
            // Lots of alternate samples to find...
            if (_clockMode == ClockMode.Off && _type == GeneratorType.Pm5644m00)
            {
                if (centreAddr == 0x6F80)
                    centreAddr = 0x6680;

                if (centreAddr == 0x7000)
                    centreAddr = 0x6700;

                if (centreAddr == 0x7080)
                    centreAddr = 0x6780;

                if (centreAddr == 0x7100)
                    centreAddr = 0x6800;

                if (centreAddr == 0x7180)
                    centreAddr = 0x6880;

                if (centreAddr == 0x7200)
                    centreAddr = 0x6900;

                if (centreAddr == 0x7280)
                    centreAddr = 0x6980;

                if (centreAddr == 0x7300)
                    centreAddr = 0x6A00;

                if (centreAddr == 0x7380)
                    centreAddr = 0x6A80;

                if (centreAddr == 0xCC00)
                    centreAddr = 0xC400;

                if (centreAddr == 0xCB80)
                    centreAddr = 0xC380;

                if (centreAddr == 0xCB00)
                    centreAddr = 0xC300;

                if (centreAddr == 0xCA80)
                    centreAddr = 0xC280;

                if (centreAddr == 0xCA00)
                    centreAddr = 0xC200;

                if (centreAddr == 0xC980)
                    centreAddr = 0xC180;

                if (centreAddr == 0xC900)
                    centreAddr = 0xC100;

                if (centreAddr == 0xC880)
                    centreAddr = 0xC080;
            }

            int addr1 = (entry.Item2 << 8 | lsbSequence[0]) * romsPerComponent;
            int addr2 = centreAddr * romsPerComponent;
            int addr3 = (entry.Item2 << 8 | lsbSequence[2]) * romsPerComponent;

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

            int addr1 = (centreAddr * romsPerComponent) - 1024;
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
                    ? Color.Red : MonochromeFromByte(_romManager.LuminanceSamples[i]);

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
