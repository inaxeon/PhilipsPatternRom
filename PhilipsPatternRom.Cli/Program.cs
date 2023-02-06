using PhilipsPatternRom.Converter;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Cli
{
    class Program
    {
        // /Generator Pm5644g00 /InROMs "N:\Electronics\Analog TV\PM5644\PM5644G00" /AddApPattern "C:\Dev\PTV\PT5230\PT8633\TPD\BILLEDDATA\Data fra PTV_Brandskab\G\PHIL16X9\TXT_M_AP" /AddPattern "C:\Dev\PTV\PT5230\PT8633\TPD\BILLEDDATA\FUBK16X9\U_ANTIPA" /OutROMs  "N:\Electronics\Analog TV\PM5644\PM5644G00_Modified"
        // /Generator Pm5644g00 /InROMs "N:\Electronics\Analog TV\PM5644\PM5644G00" /RenderPattern
        static void Main(string[] args)
        {
            var antiPalPatternsToAdd = new List<string>();
            var regularPatternsToAdd = new List<string>();
            string inputDir = null;
            string outputDir = null;
            OperationType operation = OperationType.None;
            Converter.Models.GeneratorType type = PhilipsPatternRom.Converter.Models.GeneratorType.None;

            for (int i = 0; i < args.Count(); i++)
            {
                switch (args[i])
                {
                    case "/RenderPattern":
                        operation = OperationType.RenderPattern;
                        break;
                    case "/AddApPattern":
                        operation = OperationType.AddPattern;
                        antiPalPatternsToAdd.Add(args[++i]);
                        break;
                    case "/AddPattern":
                        operation = OperationType.AddPattern;
                        regularPatternsToAdd.Add(args[++i]);
                        break;
                    case "/Generator":
                        if (!Enum.TryParse(args[++i], out type))
                            Console.Error.WriteLine("Invalid generator type");
                        break;
                    case "/InROMs":
                        inputDir = args[++i];
                        break;
                    case "/OutROMs":
                        outputDir = args[++i];
                        break;
                }
            }

            if (operation == OperationType.None)
            {
                Console.Error.WriteLine("No operation specified");
                return;
            }

            if (operation == OperationType.RenderPattern || operation == OperationType.AddPattern)
            {
                if (string.IsNullOrEmpty(inputDir) || !Directory.Exists(inputDir))
                {
                    Console.Error.WriteLine("Invalid ROM source directory");
                    return;
                }
                if (type == PhilipsPatternRom.Converter.Models.GeneratorType.None)
                {
                    Console.Error.WriteLine("Invalid generator type");
                    return;
                }
            }

            switch (operation)
            {
                case OperationType.RenderPattern:
                    {
                        ConvertRawBitmapsToProcessedBitmaps(type, inputDir);
                        break;
                    }
                case OperationType.AddPattern:
                    {
                        if (inputDir.ToLowerInvariant() == outputDir.ToLowerInvariant())
                        {
                            Console.Error.WriteLine("Input and output dir must be different");
                            return;
                        }

                        if (string.IsNullOrEmpty(outputDir))
                        {
                            Console.Error.WriteLine("Invalid output directory");
                            return;
                        }
                        if (antiPalPatternsToAdd.Count == 0 || antiPalPatternsToAdd.Any(el => !Directory.Exists(el)))
                        {
                            Console.Error.WriteLine("Invalid / missing pattern source directory");
                            return;
                        }
                        AddPatternsToRoms(type, inputDir, outputDir, antiPalPatternsToAdd, regularPatternsToAdd);
                        break;
                    }

            }
        }

        static void AddPatternsToRoms(PhilipsPatternRom.Converter.Models.GeneratorType type, string inputDir, string outputDir, List<string> antiPalPatternsToAdd, List<string> regularPatternsToAdd)
        {
            var romGenerator = new RomGenerator();

            romGenerator.Init(type, inputDir);

            foreach (var patternDir in antiPalPatternsToAdd)
                romGenerator.AddAntiPal(patternDir);

            foreach (var patternDir in regularPatternsToAdd)
                romGenerator.AddRegular(patternDir);

            romGenerator.Save(outputDir);
        }

        /// <summary>
        /// Process the bitmap representations of the EPROM contents for viewing on computer screens
        /// </summary>
        static void ConvertRawBitmapsToProcessedBitmaps(PhilipsPatternRom.Converter.Models.GeneratorType type, string directory)
        {
            int cropX = 0;
            int cropY = 0;
            int cropWidth = 0;
            int cropHeight = 0;
            int rYrange = 0;
            int bYrange = 0;
            int chromaOffset = 0;
            bool invertRy = false;
            float aspectRatioAdjustment = 0f;

            var renderer = new PatternRenderer();
            renderer.LoadPattern(type, directory);
            var components = renderer.GeneratePatternComponents();

            switch (components.Standard)
            {
                case Converter.Models.GeneratorStandard.PAL:
                    cropX = 144;
                    cropY = 0;
                    cropWidth = 707;
                    cropHeight = 576;
                    rYrange = 65;
                    bYrange = 46;
                    invertRy = true;
                    chromaOffset = -2;
                    aspectRatioAdjustment = 1.09f;
                    break;
                case Converter.Models.GeneratorStandard.PAL_16_9:
                    cropX = 218;
                    cropY = 0;
                    cropWidth = 1045;
                    cropHeight = 580;
                    rYrange = 62;
                    bYrange = 44;
                    invertRy = true;
                    chromaOffset = -4;
                    aspectRatioAdjustment = 0.743f;
                    break;
                case Converter.Models.GeneratorStandard.NTSC:
                    cropX = 135;
                    cropY = 3;
                    cropWidth = 714;
                    cropHeight = 483;
                    rYrange = 59;
                    bYrange = 41;
                    invertRy = false;
                    chromaOffset = -3;
                    aspectRatioAdjustment = 0.91f;
                    break;
                case Converter.Models.GeneratorStandard.PAL_M:
                    cropX = 135;
                    cropY = 3;
                    cropWidth = 714;
                    cropHeight = 483;
                    rYrange = 59;
                    bYrange = 41;
                    invertRy = true;
                    chromaOffset = -3;
                    aspectRatioAdjustment = 0.91f;
                    break;
            }

            components.Luma.Save("PM5644_Luma.png", ImageFormat.Png);
            components.ChromaRy.Save("PM5644_ChromaRy.png", ImageFormat.Png);
            components.ChromaBy.Save("PM5644_ChromaBy.png", ImageFormat.Png);

            var lumaSaturated = GenerateSaturatedLuma(components.Standard, components.Luma);

            var lumaCropped = lumaSaturated.Clone(new Rectangle(cropX, cropY, cropWidth, cropHeight), components.Luma.PixelFormat);
            lumaCropped.Save("PM5644_Luma_Inverted_Saturated_Cropped.png", ImageFormat.Png);

            var rySaturated = GenerateSaturatedChroma(components.Standard, components.ChromaRy, rYrange, invertRy);
            var rYexpanded = new Bitmap(rySaturated, new Size(rySaturated.Width * 2, rySaturated.Height));

            var ryCropped = rYexpanded.Clone(new Rectangle(cropX + chromaOffset, cropY, cropWidth, cropHeight), components.ChromaRy.PixelFormat);
            ryCropped.Save("PM5644_RminusY_Inverted_Saturated_Expanded_Cropped.png", ImageFormat.Png);

            var bySaturated = GenerateSaturatedChroma(components.Standard, components.ChromaBy, bYrange, true);
            var bYexpanded = new Bitmap(bySaturated, new Size(bySaturated.Width * 2, bySaturated.Height));
            var bYcropped = bYexpanded.Clone(new Rectangle(cropX + chromaOffset, cropY, cropWidth, cropHeight), components.ChromaBy.PixelFormat);
            bYcropped.Save("PM5644_BminusY_Inverted_Saturated_Expanded_Cropped.png", ImageFormat.Png);

            var composite = GenerateComposite(components.Standard, lumaCropped, ryCropped, bYcropped);
            composite.Save("PM5644_Composite.png", ImageFormat.Png);

            var compositeAdjusted = new Bitmap(composite, new Size((int)(composite.Width * aspectRatioAdjustment), composite.Height));
            compositeAdjusted.Save("PM5644_Composite_Adjusted.png", ImageFormat.Png);
        }

        static Bitmap GenerateComposite(Converter.Models.GeneratorStandard standard, Bitmap Y, Bitmap RY, Bitmap BY)
        {
            var comp = new Bitmap(Y.Width, Y.Height);

            for (int line = 0; line < Y.Height; line++)
            {
                for (int pixel = 0; pixel < Y.Width; pixel++)
                    comp.SetPixel(pixel, line, RGBFromYCbCr(Y.GetPixel(pixel, line).R, BY.GetPixel(pixel, line).R, RY.GetPixel(pixel, line).R));
            }

            return comp;
        }

        static Bitmap GenerateSaturatedLuma(Converter.Models.GeneratorStandard standard, Bitmap unsaturated)
        {
            var saturated = new Bitmap(unsaturated.Width, unsaturated.Height);

            for (int line = 0; line < unsaturated.Height; line++)
            {
                for (int pixel = 0; pixel < unsaturated.Width; pixel++)
                    saturated.SetPixel(pixel, line, SaturateY(standard, unsaturated.GetPixel(pixel, line).R));
            }

            return saturated;
        }

        static Bitmap GenerateSaturatedChroma(Converter.Models.GeneratorStandard standard, Bitmap unsaturated, int range, bool invert)
        {
            var saturated = new Bitmap(unsaturated.Width, unsaturated.Height);

            for (int line = 0; line < unsaturated.Height; line++)
            {
                for (int pixel = 0; pixel < unsaturated.Width; pixel++)
                    saturated.SetPixel(pixel, line, SaturateChroma(standard, unsaturated.GetPixel(pixel, line).R, range, invert));
            }

            return saturated;
        }

        static Color MonochromeFromByte(byte b)
        {
            return Color.FromArgb(b, b, b);
        }

        /// <summary>
        /// Inverts and fully saturates a luma pixel
        /// </summary>
        /// <param name="lum"></param>
        /// <returns></returns>
        static Color SaturateY(Converter.Models.GeneratorStandard standard, int romData)
        {
            float adjusted = 0f;

            //Luma range is found in a range between 41 and 181 (PAL)
            // and 38 and 170 (NTSC)

            if (standard == PhilipsPatternRom.Converter.Models.GeneratorStandard.NTSC || standard == PhilipsPatternRom.Converter.Models.GeneratorStandard.PAL_M)
            {
                if (romData < 38)
                    romData = 38;

                adjusted = romData - 38;
                adjusted = 132 - adjusted; // Invert
                adjusted *= 1.98f;
            }
            else
            {

                if (romData < 41)
                    romData = 41;

                adjusted = romData - 41; // Now 0-140
                adjusted = 140 - adjusted; // Invert
                adjusted *= 1.82f;
            }

            if ((int)adjusted > 255)
                adjusted = 255;
                //throw new InvalidDataException(); // Overshoot

            if ((int)adjusted < 0)
                adjusted = 0; // Clip the negative luminance in the black ref area in the centre of the circle;

            return Color.FromArgb((byte)adjusted, (byte)adjusted, (byte)adjusted);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="romData">The actual data from the ROM</param>
        /// <param name="range">The amount (decimal) that chrominance data is observed to deviate from 128 (0 degrees) in ROM</param>
        /// <returns></returns>
        static Color SaturateChroma(Converter.Models.GeneratorStandard standard, int romData, int range, bool invert)
        {
            float adjusted = romData - 128;

            // The design amplitude of the chroma samples is not known. Therefore this is a manually
            // adjusted figure which was observed to reduce clipping to near-zero in RGBFromYCbCr()
            // which results in a saturation of around 75%. This matches the "Zacabeb" recreation and
            // is assumed to be what we're aiming for.
            float headroom = standard == PhilipsPatternRom.Converter.Models.GeneratorStandard.PAL ? 32f : 36f;

            if (invert)
                adjusted = -adjusted;

            adjusted *= ((128 - (float)headroom) / (float)range);
            adjusted += 128;

            if (Math.Abs((float)(adjusted - 128)) > (128 - headroom))
            {
                //throw new InvalidDataException(); // Overshoot
            }

            return Color.FromArgb((byte)adjusted, (byte)adjusted, (byte)adjusted);
        }

        /// <summary>
        /// ITU-R BT.601 YCbCr -> RGB conversion
        /// </summary>
        /// <param name="Y"></param>
        /// <param name="Cb"></param>
        /// <param name="Cr"></param>
        /// <returns></returns>
        static Color RGBFromYCbCr(byte Y, byte Cb, byte Cr)
        {
            float r = Y + 1.38f * (Cr - 128f);
            float g = Y - 0.34f * (Cb - 128) - 0.71f * (Cr - 128);
            float b = Y + 1.77f * (Cb - 128);

            // Some clipping is currently necessary for the sake of accurate colours on the colourbars.
            // This only activates on transition pixels.

            if (r < 0)
                r = 0;
            if (g < 0)
                g = 0;
            if (b < 0)
                b = 0;

            if (r > 255)
                r = 255;
            if (g > 255)
                g = 255;
            if (b > 255)
                b = 255;

            return Color.FromArgb((int)r, (int)g, (int)b);
        }

        static void ReadVectors()
        {
            var file = File.ReadAllBytes(@"N:\Electronics\Analog TV\PM5644\PM5644P00\EPROM_4008_102_59391_CSUM_0D00.BIN");
            var lastByte = 0x7587;

            for (int i = lastByte; ;)
            {
                var byte1 = file[i--];
                var byte2 = file[i--];
                var byte3 = file[i--];

                if (byte3 != 0x00 && byte3 != 0x01 && byte3 != 0x02 && byte3 != 0x03)
                {

                }
            }
        }
    }
}
