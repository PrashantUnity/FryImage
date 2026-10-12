using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// High-fidelity BMP format codec with dedicated indexed palette table support (1-bit, 4-bit, 8-bit).
/// Preserves embedded color tables to open palette BMPs in Indexed Color mode matching Photoshop 27.x (#3802, #3869).
/// </summary>
public static class BmpCodec
{
    public sealed class BmpDecodeResult
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int BitsPerPixel { get; set; }
        public bool IsIndexed => BitsPerPixel <= 8 && ColorTable.Count > 0;
        public List<Color> ColorTable { get; set; } = new();
        public byte[] BgraPixels { get; set; } = Array.Empty<byte>();
        public byte[] OriginalBytes { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Checks if the given stream contains a valid BMP file header ('BM').
    /// </summary>
    public static bool IsBmp(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D; // 'B', 'M'
    }

    /// <summary>
    /// Reads and parses BMP image data from a stream, preserving indexed color palette tables.
    /// </summary>
    public static BmpDecodeResult Decode(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        byte[] bytes = ms.ToArray();
        return Decode(bytes);
    }

    /// <summary>
    /// Reads and parses BMP image data from byte array, preserving indexed color palette tables.
    /// </summary>
    public static BmpDecodeResult Decode(byte[] bytes)
    {
        if (bytes.Length < 54)
            throw new InvalidDataException("Invalid BMP: Stream length is smaller than the minimum BMP header (54 bytes).");

        if (bytes[0] != 0x42 || bytes[1] != 0x4D)
            throw new InvalidDataException("Invalid BMP signature, expected 'BM'.");

        int pixelOffset = BitConverter.ToInt32(bytes, 10);
        int headerSize = BitConverter.ToInt32(bytes, 14);

        int width = BitConverter.ToInt32(bytes, 18);
        int heightRaw = BitConverter.ToInt32(bytes, 22);
        bool topDown = heightRaw < 0;
        int height = Math.Abs(heightRaw);

        ushort planes = BitConverter.ToUInt16(bytes, 26);
        ushort bpp = BitConverter.ToUInt16(bytes, 28);
        uint compression = BitConverter.ToUInt32(bytes, 30);
        int colorsUsed = BitConverter.ToInt32(bytes, 46);

        var result = new BmpDecodeResult
        {
            Width = width,
            Height = height,
            BitsPerPixel = bpp,
            OriginalBytes = bytes
        };

        if (width <= 0 || height <= 0)
            return result;

        // Extract color table for indexed BMP (1, 4, 8 bpp)
        if (bpp <= 8)
        {
            int paletteOffset = 14 + headerSize;
            int numColors = colorsUsed > 0 ? colorsUsed : (1 << bpp);
            int entrySize = 4; // RGBQUAD: B, G, R, 0

            for (int i = 0; i < numColors; i++)
            {
                int entryPos = paletteOffset + (i * entrySize);
                if (entryPos + 3 <= bytes.Length && entryPos + 3 <= pixelOffset)
                {
                    byte b = bytes[entryPos];
                    byte g = bytes[entryPos + 1];
                    byte r = bytes[entryPos + 2];
                    result.ColorTable.Add(Color.FromRgb(r, g, b));
                }
                else
                {
                    result.ColorTable.Add(Color.FromRgb(0, 0, 0));
                }
            }
        }

        // Decode pixel data to BGRA8888
        byte[] bgra = new byte[width * height * 4];

        if (bpp == 8 && compression == 0)
        {
            int rowStride = (width + 3) & ~3; // 4-byte aligned
            for (int y = 0; y < height; y++)
            {
                int srcY = topDown ? y : (height - 1 - y);
                int srcRowOffset = pixelOffset + (srcY * rowStride);
                int dstRowOffset = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    if (srcRowOffset + x < bytes.Length)
                    {
                        byte colorIdx = bytes[srcRowOffset + x];
                        Color c = colorIdx < result.ColorTable.Count
                            ? result.ColorTable[colorIdx]
                            : Colors.Black;

                        int dstIdx = dstRowOffset + (x * 4);
                        bgra[dstIdx] = c.B;
                        bgra[dstIdx + 1] = c.G;
                        bgra[dstIdx + 2] = c.R;
                        bgra[dstIdx + 3] = 255;
                    }
                }
            }
        }
        else if (bpp == 4 && compression == 0)
        {
            int rowStride = ((width + 1) / 2 + 3) & ~3;
            for (int y = 0; y < height; y++)
            {
                int srcY = topDown ? y : (height - 1 - y);
                int srcRowOffset = pixelOffset + (srcY * rowStride);
                int dstRowOffset = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    int byteIdx = srcRowOffset + (x / 2);
                    if (byteIdx < bytes.Length)
                    {
                        byte b = bytes[byteIdx];
                        int colorIdx = (x % 2 == 0) ? (b >> 4) : (b & 0x0F);
                        Color c = colorIdx < result.ColorTable.Count
                            ? result.ColorTable[colorIdx]
                            : Colors.Black;

                        int dstIdx = dstRowOffset + (x * 4);
                        bgra[dstIdx] = c.B;
                        bgra[dstIdx + 1] = c.G;
                        bgra[dstIdx + 2] = c.R;
                        bgra[dstIdx + 3] = 255;
                    }
                }
            }
        }
        else if (bpp == 1 && compression == 0)
        {
            int rowStride = ((width + 7) / 8 + 3) & ~3;
            for (int y = 0; y < height; y++)
            {
                int srcY = topDown ? y : (height - 1 - y);
                int srcRowOffset = pixelOffset + (srcY * rowStride);
                int dstRowOffset = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    int byteIdx = srcRowOffset + (x / 8);
                    if (byteIdx < bytes.Length)
                    {
                        byte b = bytes[byteIdx];
                        int bit = (b >> (7 - (x % 8))) & 1;
                        Color c = bit < result.ColorTable.Count
                            ? result.ColorTable[bit]
                            : (bit == 1 ? Colors.White : Colors.Black);

                        int dstIdx = dstRowOffset + (x * 4);
                        bgra[dstIdx] = c.B;
                        bgra[dstIdx + 1] = c.G;
                        bgra[dstIdx + 2] = c.R;
                        bgra[dstIdx + 3] = 255;
                    }
                }
            }
        }
        else
        {
            // Use SkiaSharp decoding fallback for 24/32-bit or compressed BMP
            using var skBmp = SKBitmap.Decode(bytes);
            if (skBmp != null)
            {
                result.Width = skBmp.Width;
                result.Height = skBmp.Height;
                bgra = skBmp.Bytes;
            }
        }

        result.BgraPixels = bgra;
        return result;
    }
}
