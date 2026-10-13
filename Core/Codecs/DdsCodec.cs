using System;
using System.IO;
using System.Text;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// DirectDraw Surface (DDS) image and texture decoder.
/// Supports uncompressed textures (8, 16, 24, 32-bit RGB/RGBA/Luminance with bitmasks)
/// and block-compressed textures BC1 (DXT1), BC2 (DXT2/DXT3), and BC3 (DXT4/DXT5).
/// Clean-room implementation matching DirectDraw Surface (DDS) specification.
/// </summary>
public static class DdsCodec
{
    private const uint DdsMagic = 0x20534444; // "DDS " in little-endian
    private const uint DdsHeaderSize = 124;
    private const uint DdpfFourCc = 0x4;
    private const uint DdpfRgb = 0x40;
    private const uint DdpfAlphaPixels = 0x1;
    private const uint DdpfLuminance = 0x20000;

    public sealed class DdsImage
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public string FormatName { get; init; } = "";
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

    public static bool LooksLikeDds(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 128) return false;
        return BitConverter.ToUInt32(bytes, 0) == DdsMagic;
    }

    public static bool LooksLikeDds(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 128) return false;
        long pos = stream.Position;
        try
        {
            Span<byte> magic = stackalloc byte[4];
            int read = stream.Read(magic);
            if (read < 4) return false;
            return BitConverter.ToUInt32(magic) == DdsMagic;
        }
        finally
        {
            stream.Position = pos;
        }
    }

    public static DdsImage Decode(string filePath) => Decode(File.ReadAllBytes(filePath));

    public static DdsImage Decode(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Decode(ms.ToArray());
    }

    public static DdsImage Decode(byte[] data)
    {
        if (data.Length < 128)
            throw new InvalidDataException("DDS file is truncated: header must be at least 128 bytes.");

        uint magic = BitConverter.ToUInt32(data, 0);
        if (magic != DdsMagic)
            throw new InvalidDataException("Invalid DDS magic identifier.");

        uint headerSize = BitConverter.ToUInt32(data, 4);
        if (headerSize != DdsHeaderSize)
            throw new InvalidDataException($"Invalid DDS header size: expected 124, got {headerSize}.");

        int height = (int)BitConverter.ToUInt32(data, 12);
        int width = (int)BitConverter.ToUInt32(data, 16);
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
            throw new InvalidDataException($"Invalid DDS dimensions: {width}x{height}.");

        // Pixel Format
        uint pfFlags = BitConverter.ToUInt32(data, 80);
        uint fourCc = BitConverter.ToUInt32(data, 84);
        uint rgbBitCount = BitConverter.ToUInt32(data, 88);
        uint rMask = BitConverter.ToUInt32(data, 92);
        uint gMask = BitConverter.ToUInt32(data, 96);
        uint bMask = BitConverter.ToUInt32(data, 100);
        uint aMask = BitConverter.ToUInt32(data, 104);

        int dataOffset = 128;
        string fourCcString = Encoding.ASCII.GetString(data, 84, 4);

        byte[] outputBgra = new byte[width * height * 4];
        string formatName;

        if ((pfFlags & DdpfFourCc) != 0)
        {
            if (fourCcString == "DXT1")
            {
                formatName = "BC1 / DXT1";
                DecompressBc1(data, dataOffset, width, height, outputBgra);
            }
            else if (fourCcString == "DXT2" || fourCcString == "DXT3")
            {
                formatName = "BC2 / DXT3";
                DecompressBc2(data, dataOffset, width, height, outputBgra);
            }
            else if (fourCcString == "DXT4" || fourCcString == "DXT5")
            {
                formatName = "BC3 / DXT5";
                DecompressBc3(data, dataOffset, width, height, outputBgra);
            }
            else if (fourCcString == "DX10")
            {
                if (data.Length < 148)
                    throw new InvalidDataException("DDS DX10 header is truncated.");
                uint dxgiFormat = BitConverter.ToUInt32(data, 128);
                dataOffset = 148;
                // DXGI format 70/71/72 = BC1, 73/74/75 = BC2, 76/77/78 = BC3
                if (dxgiFormat is 70 or 71 or 72)
                {
                    formatName = "DX10 (BC1)";
                    DecompressBc1(data, dataOffset, width, height, outputBgra);
                }
                else if (dxgiFormat is 73 or 74 or 75)
                {
                    formatName = "DX10 (BC2)";
                    DecompressBc2(data, dataOffset, width, height, outputBgra);
                }
                else if (dxgiFormat is 76 or 77 or 78)
                {
                    formatName = "DX10 (BC3)";
                    DecompressBc3(data, dataOffset, width, height, outputBgra);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported DX10 format {dxgiFormat}.");
                }
            }
            else
            {
                throw new NotSupportedException($"Unsupported DDS FourCC format '{fourCcString}'.");
            }
        }
        else if ((pfFlags & DdpfRgb) != 0 || (pfFlags & DdpfLuminance) != 0)
        {
            formatName = $"Uncompressed {rgbBitCount}-bit";
            DecompressUncompressed(data, dataOffset, width, height, rgbBitCount, rMask, gMask, bMask, aMask, (pfFlags & DdpfLuminance) != 0, outputBgra);
        }
        else
        {
            throw new NotSupportedException("Unsupported DDS pixel format configuration.");
        }

        return new DdsImage
        {
            Width = width,
            Height = height,
            FormatName = formatName,
            BgraPixels = outputBgra
        };
    }

    private static void DecompressBc1(byte[] src, int offset, int width, int height, byte[] dst)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;

        Span<byte> colorBlock = stackalloc byte[4 * 4 * 4]; // 16 pixels BGRA
        Span<byte> r = stackalloc byte[4];
        Span<byte> g = stackalloc byte[4];
        Span<byte> b = stackalloc byte[4];
        Span<byte> a = stackalloc byte[4];

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                if (offset + 8 > src.Length) return;

                ushort c0 = BitConverter.ToUInt16(src, offset);
                ushort c1 = BitConverter.ToUInt16(src, offset + 2);
                uint table = BitConverter.ToUInt32(src, offset + 4);
                offset += 8;

                DecodeRgb565(c0, out byte r0, out byte g0, out byte b0);
                DecodeRgb565(c1, out byte r1, out byte g1, out byte b1);

                r[0] = r0; g[0] = g0; b[0] = b0; a[0] = 255;
                r[1] = r1; g[1] = g1; b[1] = b1; a[1] = 255;

                if (c0 > c1)
                {
                    r[2] = (byte)((2 * r0 + r1 + 1) / 3);
                    g[2] = (byte)((2 * g0 + g1 + 1) / 3);
                    b[2] = (byte)((2 * b0 + b1 + 1) / 3);
                    a[2] = 255;

                    r[3] = (byte)((r0 + 2 * r1 + 1) / 3);
                    g[3] = (byte)((g0 + 2 * g1 + 1) / 3);
                    b[3] = (byte)((b0 + 2 * b1 + 1) / 3);
                    a[3] = 255;
                }
                else
                {
                    r[2] = (byte)((r0 + r1) / 2);
                    g[2] = (byte)((g0 + g1) / 2);
                    b[2] = (byte)((b0 + b1) / 2);
                    a[2] = 255;

                    r[3] = 0; g[3] = 0; b[3] = 0; a[3] = 0; // 1-bit transparent punchthrough
                }

                for (int py = 0; py < 4; py++)
                {
                    int y = by * 4 + py;
                    if (y >= height) continue;

                    for (int px = 0; px < 4; px++)
                    {
                        int x = bx * 4 + px;
                        if (x >= width) continue;

                        int code = (int)((table >> ((py * 4 + px) * 2)) & 3);
                        int dstIdx = (y * width + x) * 4;

                        byte alpha = a[code];
                        byte red = r[code];
                        byte green = g[code];
                        byte blue = b[code];

                        // Premultiply alpha for Skia
                        dst[dstIdx + 0] = (byte)((blue * alpha) / 255);
                        dst[dstIdx + 1] = (byte)((green * alpha) / 255);
                        dst[dstIdx + 2] = (byte)((red * alpha) / 255);
                        dst[dstIdx + 3] = alpha;
                    }
                }
            }
        }
    }

    private static void DecompressBc2(byte[] src, int offset, int width, int height, byte[] dst)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;

        Span<byte> r = stackalloc byte[4];
        Span<byte> g = stackalloc byte[4];
        Span<byte> b = stackalloc byte[4];

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                if (offset + 16 > src.Length) return;

                ulong alphaData = BitConverter.ToUInt64(src, offset);
                ushort c0 = BitConverter.ToUInt16(src, offset + 8);
                ushort c1 = BitConverter.ToUInt16(src, offset + 10);
                uint table = BitConverter.ToUInt32(src, offset + 12);
                offset += 16;

                DecodeRgb565(c0, out byte r0, out byte g0, out byte b0);
                DecodeRgb565(c1, out byte r1, out byte g1, out byte b1);

                r[0] = r0; g[0] = g0; b[0] = b0;
                r[1] = r1; g[1] = g1; b[1] = b1;
                r[2] = (byte)((2 * r0 + r1 + 1) / 3);
                g[2] = (byte)((2 * g0 + g1 + 1) / 3);
                b[2] = (byte)((2 * b0 + b1 + 1) / 3);
                r[3] = (byte)((r0 + 2 * r1 + 1) / 3);
                g[3] = (byte)((g0 + 2 * g1 + 1) / 3);
                b[3] = (byte)((b0 + 2 * b1 + 1) / 3);

                for (int py = 0; py < 4; py++)
                {
                    int y = by * 4 + py;
                    if (y >= height) continue;

                    for (int px = 0; px < 4; px++)
                    {
                        int x = bx * 4 + px;
                        if (x >= width) continue;

                        int pixIndex = py * 4 + px;
                        int code = (int)((table >> (pixIndex * 2)) & 3);
                        int a4 = (int)((alphaData >> (pixIndex * 4)) & 0xF);
                        byte alpha = (byte)((a4 * 255) / 15);

                        int dstIdx = (y * width + x) * 4;
                        byte red = r[code];
                        byte green = g[code];
                        byte blue = b[code];

                        dst[dstIdx + 0] = (byte)((blue * alpha) / 255);
                        dst[dstIdx + 1] = (byte)((green * alpha) / 255);
                        dst[dstIdx + 2] = (byte)((red * alpha) / 255);
                        dst[dstIdx + 3] = alpha;
                    }
                }
            }
        }
    }

    private static void DecompressBc3(byte[] src, int offset, int width, int height, byte[] dst)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;

        Span<byte> aTable = stackalloc byte[8];
        Span<byte> r = stackalloc byte[4];
        Span<byte> g = stackalloc byte[4];
        Span<byte> b = stackalloc byte[4];

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                if (offset + 16 > src.Length) return;

                byte a0 = src[offset];
                byte a1 = src[offset + 1];
                ulong aBits = (ulong)src[offset + 2]
                            | ((ulong)src[offset + 3] << 8)
                            | ((ulong)src[offset + 4] << 16)
                            | ((ulong)src[offset + 5] << 24)
                            | ((ulong)src[offset + 6] << 32)
                            | ((ulong)src[offset + 7] << 40);

                ushort c0 = BitConverter.ToUInt16(src, offset + 8);
                ushort c1 = BitConverter.ToUInt16(src, offset + 10);
                uint table = BitConverter.ToUInt32(src, offset + 12);
                offset += 16;

                // Build alpha lookup table
                aTable[0] = a0;
                aTable[1] = a1;
                if (a0 > a1)
                {
                    aTable[2] = (byte)((6 * a0 + 1 * a1 + 3) / 7);
                    aTable[3] = (byte)((5 * a0 + 2 * a1 + 3) / 7);
                    aTable[4] = (byte)((4 * a0 + 3 * a1 + 3) / 7);
                    aTable[5] = (byte)((3 * a0 + 4 * a1 + 3) / 7);
                    aTable[6] = (byte)((2 * a0 + 5 * a1 + 3) / 7);
                    aTable[7] = (byte)((1 * a0 + 6 * a1 + 3) / 7);
                }
                else
                {
                    aTable[2] = (byte)((4 * a0 + 1 * a1 + 2) / 5);
                    aTable[3] = (byte)((3 * a0 + 2 * a1 + 2) / 5);
                    aTable[4] = (byte)((2 * a0 + 3 * a1 + 2) / 5);
                    aTable[5] = (byte)((1 * a0 + 4 * a1 + 2) / 5);
                    aTable[6] = 0;
                    aTable[7] = 255;
                }

                DecodeRgb565(c0, out byte r0, out byte g0, out byte b0);
                DecodeRgb565(c1, out byte r1, out byte g1, out byte b1);

                r[0] = r0; g[0] = g0; b[0] = b0;
                r[1] = r1; g[1] = g1; b[1] = b1;
                r[2] = (byte)((2 * r0 + r1 + 1) / 3);
                g[2] = (byte)((2 * g0 + g1 + 1) / 3);
                b[2] = (byte)((2 * b0 + b1 + 1) / 3);
                r[3] = (byte)((r0 + 2 * r1 + 1) / 3);
                g[3] = (byte)((g0 + 2 * g1 + 1) / 3);
                b[3] = (byte)((b0 + 2 * b1 + 1) / 3);

                for (int py = 0; py < 4; py++)
                {
                    int y = by * 4 + py;
                    if (y >= height) continue;

                    for (int px = 0; px < 4; px++)
                    {
                        int x = bx * 4 + px;
                        if (x >= width) continue;

                        int pixIndex = py * 4 + px;
                        int cCode = (int)((table >> (pixIndex * 2)) & 3);
                        int aCode = (int)((aBits >> (pixIndex * 3)) & 7);
                        byte alpha = aTable[aCode];

                        int dstIdx = (y * width + x) * 4;
                        byte red = r[cCode];
                        byte green = g[cCode];
                        byte blue = b[cCode];

                        dst[dstIdx + 0] = (byte)((blue * alpha) / 255);
                        dst[dstIdx + 1] = (byte)((green * alpha) / 255);
                        dst[dstIdx + 2] = (byte)((red * alpha) / 255);
                        dst[dstIdx + 3] = alpha;
                    }
                }
            }
        }
    }

    private static void DecompressUncompressed(byte[] src, int offset, int width, int height,
        uint bitCount, uint rMask, uint gMask, uint bMask, uint aMask, bool isLuminance, byte[] dst)
    {
        int bytesPerPixel = (int)(bitCount / 8);
        if (bytesPerPixel <= 0 || bytesPerPixel > 4)
            throw new NotSupportedException($"Unsupported bit depth: {bitCount}");

        int rShift = GetShift(rMask);
        int gShift = GetShift(gMask);
        int bShift = GetShift(bMask);
        int aShift = aMask != 0 ? GetShift(aMask) : 0;

        uint rMax = rMask != 0 ? rMask >> rShift : 255;
        uint gMax = gMask != 0 ? gMask >> gShift : 255;
        uint bMax = bMask != 0 ? bMask >> bShift : 255;
        uint aMax = aMask != 0 ? aMask >> aShift : 255;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (offset + bytesPerPixel > src.Length) return;

                uint raw = 0;
                for (int b = 0; b < bytesPerPixel; b++)
                    raw |= (uint)src[offset + b] << (b * 8);
                offset += bytesPerPixel;

                byte red, green, blue, alpha;
                if (isLuminance)
                {
                    byte lum = (byte)((((raw & rMask) >> rShift) * 255) / (rMax == 0 ? 1 : rMax));
                    red = green = blue = lum;
                    alpha = aMask != 0 ? (byte)((((raw & aMask) >> aShift) * 255) / (aMax == 0 ? 1 : aMax)) : (byte)255;
                }
                else
                {
                    red = rMask != 0 ? (byte)((((raw & rMask) >> rShift) * 255) / (rMax == 0 ? 1 : rMax)) : (byte)0;
                    green = gMask != 0 ? (byte)((((raw & gMask) >> gShift) * 255) / (gMax == 0 ? 1 : gMax)) : (byte)0;
                    blue = bMask != 0 ? (byte)((((raw & bMask) >> bShift) * 255) / (bMax == 0 ? 1 : bMax)) : (byte)0;
                    alpha = aMask != 0 ? (byte)((((raw & aMask) >> aShift) * 255) / (aMax == 0 ? 1 : aMax)) : (byte)255;
                }

                int dstIdx = (y * width + x) * 4;
                dst[dstIdx + 0] = (byte)((blue * alpha) / 255);
                dst[dstIdx + 1] = (byte)((green * alpha) / 255);
                dst[dstIdx + 2] = (byte)((red * alpha) / 255);
                dst[dstIdx + 3] = alpha;
            }
        }
    }

    private static int GetShift(uint mask)
    {
        if (mask == 0) return 0;
        int shift = 0;
        while ((mask & 1) == 0)
        {
            mask >>= 1;
            shift++;
        }
        return shift;
    }

    private static void DecodeRgb565(ushort c, out byte r, out byte g, out byte b)
    {
        int r5 = (c >> 11) & 0x1F;
        int g6 = (c >> 5) & 0x3F;
        int b5 = c & 0x1F;

        r = (byte)((r5 * 527 + 23) >> 6);
        g = (byte)((g6 * 259 + 33) >> 6);
        b = (byte)((b5 * 527 + 23) >> 6);
    }
}
