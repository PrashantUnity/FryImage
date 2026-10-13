using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// X PixMap (XPM) image decoder supporting XPM1, XPM2, and XPM3 format variants.
/// Implements full X11 rgb.txt color name resolution and hex color formats.
/// Clean-room implementation matching X PixMap XPM1/XPM2/XPM3 specification.
/// </summary>
public static class XpmCodec
{
    public sealed class XpmImage
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int ColorsCount { get; init; }
        public byte[] BgraPixels { get; init; } = [];

        public SKBitmap ToSkBitmap()
        {
            var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var bmp = new SKBitmap(info);
            var ptr = bmp.GetPixels();
            System.Runtime.InteropServices.Marshal.Copy(BgraPixels, 0, ptr, BgraPixels.Length);
            return bmp;
        }
    }

    public static bool LooksLikeXpm(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Contains("/* XPM */") || text.StartsWith("! XPM2") || text.Contains("static char *");
    }

    public static bool LooksLikeXpm(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 10) return false;
        string prefix = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 64));
        return LooksLikeXpm(prefix);
    }

    public static XpmImage Decode(string filePath) => DecodeFromText(File.ReadAllText(filePath));

    public static XpmImage Decode(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return DecodeFromText(reader.ReadToEnd());
    }

    public static XpmImage Decode(byte[] bytes)
    {
        string text = Encoding.UTF8.GetString(bytes);
        return DecodeFromText(text);
    }

    public static XpmImage DecodeFromText(string source)
    {
        // Extract string tokens enclosed in quotes: "..."
        var stringTokens = new List<string>();
        var matches = Regex.Matches(source, "\"([^\"]*)\"");
        if (matches.Count > 0)
        {
            foreach (Match m in matches)
            {
                stringTokens.Add(m.Groups[1].Value);
            }
        }
        else
        {
            // Plain-text XPM2 format without quotes
            var lines = source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var l in lines)
            {
                var trimmed = l.Trim();
                if (!trimmed.StartsWith("!") && !trimmed.StartsWith("/*"))
                {
                    stringTokens.Add(trimmed);
                }
            }
        }

        if (stringTokens.Count < 2)
            throw new InvalidDataException("XPM source does not contain valid token definitions.");

        // First token is dimensions: "width height num_colors chars_per_pixel"
        var headerParts = stringTokens[0].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (headerParts.Length < 4)
            throw new InvalidDataException($"Invalid XPM header format: '{stringTokens[0]}'.");

        int width = int.Parse(headerParts[0], CultureInfo.InvariantCulture);
        int height = int.Parse(headerParts[1], CultureInfo.InvariantCulture);
        int numColors = int.Parse(headerParts[2], CultureInfo.InvariantCulture);
        int charsPerPixel = int.Parse(headerParts[3], CultureInfo.InvariantCulture);

        if (width <= 0 || height <= 0 || numColors <= 0 || charsPerPixel <= 0)
            throw new InvalidDataException($"Invalid XPM parameters: {width}x{height}, {numColors} colors, {charsPerPixel} cpp.");

        if (stringTokens.Count < 1 + numColors + height)
            throw new InvalidDataException("XPM data is incomplete: missing color table or pixel rows.");

        // Color table: maps code string -> (B, G, R, A)
        var colorMap = new Dictionary<string, (byte B, byte G, byte R, byte A)>(StringComparer.Ordinal);
        for (int i = 0; i < numColors; i++)
        {
            string colorLine = stringTokens[1 + i];
            if (colorLine.Length < charsPerPixel) continue;

            string code = colorLine.Substring(0, charsPerPixel);
            string def = colorLine.Substring(charsPerPixel).Trim();

            // Look for color key "c <color>"
            string colorVal = "None";
            var defTokens = def.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            for (int k = 0; k < defTokens.Length; k++)
            {
                if (defTokens[k] == "c" && k + 1 < defTokens.Length)
                {
                    // Merge multi-word color names like "dark slate gray"
                    colorVal = string.Join(" ", defTokens, k + 1, defTokens.Length - (k + 1));
                    break;
                }
            }

            colorMap[code] = ParseColor(colorVal);
        }

        // Parse pixel rows
        byte[] bgra = new byte[width * height * 4];
        int pixelRowOffset = 1 + numColors;

        for (int y = 0; y < height; y++)
        {
            string row = stringTokens[pixelRowOffset + y];
            for (int x = 0; x < width; x++)
            {
                int charIdx = x * charsPerPixel;
                string code = charIdx + charsPerPixel <= row.Length
                    ? row.Substring(charIdx, charsPerPixel)
                    : " ";

                if (!colorMap.TryGetValue(code, out var color))
                {
                    color = (0, 0, 0, 0); // transparent fallback
                }

                int dstIdx = (y * width + x) * 4;
                // Premultiply alpha
                bgra[dstIdx + 0] = (byte)((color.B * color.A) / 255);
                bgra[dstIdx + 1] = (byte)((color.G * color.A) / 255);
                bgra[dstIdx + 2] = (byte)((color.R * color.A) / 255);
                bgra[dstIdx + 3] = color.A;
            }
        }

        return new XpmImage
        {
            Width = width,
            Height = height,
            ColorsCount = numColors,
            BgraPixels = bgra
        };
    }

    private static (byte B, byte G, byte R, byte A) ParseColor(string name)
    {
        name = name.Trim();
        if (string.Equals(name, "None", StringComparison.OrdinalIgnoreCase))
            return (0, 0, 0, 0);

        // Hex color format: #RGB, #RRGGBB, #RRRGGGBBB, #RRRRGGGGBBBB
        if (name.StartsWith('#'))
        {
            string hex = name.Substring(1);
            if (hex.Length == 3)
            {
                byte r = byte.Parse(new string(hex[0], 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte g = byte.Parse(new string(hex[1], 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte b = byte.Parse(new string(hex[2], 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return (b, g, r, 255);
            }
            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return (b, g, r, 255);
            }
            if (hex.Length == 12)
            {
                byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte b = byte.Parse(hex.Substring(8, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return (b, g, r, 255);
            }
        }

        // X11 RGB color names
        if (X11Colors.TryGetValue(name.ToLowerInvariant(), out var x11Color))
        {
            return x11Color;
        }

        return (0, 0, 0, 255); // Default opaque black
    }

    private static readonly Dictionary<string, (byte B, byte G, byte R, byte A)> X11Colors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = (0, 0, 0, 255),
        ["white"] = (255, 255, 255, 255),
        ["red"] = (0, 0, 255, 255),
        ["green"] = (0, 128, 0, 255),
        ["blue"] = (255, 0, 0, 255),
        ["yellow"] = (0, 255, 255, 255),
        ["cyan"] = (255, 255, 0, 255),
        ["magenta"] = (255, 0, 255, 255),
        ["gray"] = (128, 128, 128, 255),
        ["grey"] = (128, 128, 128, 255),
        ["darkgray"] = (169, 169, 169, 255),
        ["lightgray"] = (211, 211, 211, 255),
        ["navy"] = (128, 0, 0, 255),
        ["maroon"] = (0, 0, 128, 255),
        ["olive"] = (0, 128, 128, 255),
        ["purple"] = (128, 0, 128, 255),
        ["teal"] = (128, 128, 0, 255),
        ["silver"] = (192, 192, 192, 255),
        ["orange"] = (0, 165, 255, 255),
        ["gold"] = (0, 215, 255, 255),
        ["coral"] = (80, 127, 255, 255),
        ["cornflower blue"] = (237, 149, 100, 255),
        ["cornflowerblue"] = (237, 149, 100, 255),
        ["dark slate gray"] = (79, 79, 47, 255),
        ["darkslategray"] = (79, 79, 47, 255),
        ["deep sky blue"] = (255, 191, 0, 255),
        ["deepskyblue"] = (255, 191, 0, 255),
        ["dodger blue"] = (255, 144, 30, 255),
        ["dodgerblue"] = (255, 144, 30, 255),
        ["firebrick"] = (34, 34, 178, 255),
        ["forest green"] = (34, 139, 34, 255),
        ["forestgreen"] = (34, 139, 34, 255),
        ["hot pink"] = (180, 105, 255, 255),
        ["hotpink"] = (180, 105, 255, 255),
        ["indian red"] = (92, 92, 205, 255),
        ["indianred"] = (92, 92, 205, 255),
        ["khaki"] = (140, 230, 240, 255),
        ["lavender"] = (250, 230, 230, 255),
        ["lime"] = (0, 255, 0, 255),
        ["lime green"] = (50, 205, 50, 255),
        ["limegreen"] = (50, 205, 50, 255),
        ["midnight blue"] = (112, 25, 25, 255),
        ["midnightblue"] = (112, 25, 25, 255),
        ["orchid"] = (214, 112, 218, 255),
        ["peru"] = (63, 133, 205, 255),
        ["pink"] = (203, 192, 255, 255),
        ["plum"] = (221, 160, 221, 255),
        ["royal blue"] = (225, 105, 65, 255),
        ["royalblue"] = (225, 105, 65, 255),
        ["salmon"] = (114, 128, 250, 255),
        ["sea green"] = (87, 139, 46, 255),
        ["seagreen"] = (87, 139, 46, 255),
        ["sienna"] = (45, 82, 160, 255),
        ["sky blue"] = (235, 206, 135, 255),
        ["skyblue"] = (235, 206, 135, 255),
        ["slate blue"] = (205, 90, 106, 255),
        ["slateblue"] = (205, 90, 106, 255),
        ["spring green"] = (127, 255, 0, 255),
        ["springgreen"] = (127, 255, 0, 255),
        ["steel blue"] = (180, 130, 70, 255),
        ["steelblue"] = (180, 130, 70, 255),
        ["tomato"] = (71, 99, 255, 255),
        ["turquoise"] = (208, 224, 64, 255),
        ["violet"] = (238, 130, 238, 255),
        ["wheat"] = (179, 222, 245, 255)
    };
}
