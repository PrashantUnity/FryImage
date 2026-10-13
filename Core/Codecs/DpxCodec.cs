using System;
using System.IO;
using System.Text;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// SMPTE 268M Digital Picture Exchange (DPX) film and VFX frame decoder and encoder.
/// Supports 8-bit, 10-bit packed (Method A), and 16-bit RGB frames in both Big-Endian (SDPX)
/// and Little-Endian (XPDS) formats. Clean-room implementation matching SMPTE 268M specification.
/// </summary>
public static class DpxCodec
{
    private const uint MagicBigEndian = 0x53445058;    // "SDPX"
    private const uint MagicLittleEndian = 0x58504453; // "XPDS"

    public sealed class DpxImage
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int BitDepth { get; init; }
        public bool IsBigEndian { get; init; }
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

    public static bool LooksLikeDpx(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4) return false;
        return (bytes[0] == 0x53 && bytes[1] == 0x44 && bytes[2] == 0x50 && bytes[3] == 0x58) ||
               (bytes[0] == 0x58 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x53);
    }

    public static bool LooksLikeDpx(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 4) return false;
        long pos = stream.Position;
        try
        {
            Span<byte> magicBytes = stackalloc byte[4];
            int read = stream.Read(magicBytes);
            if (read < 4) return false;
            return (magicBytes[0] == 0x53 && magicBytes[1] == 0x44 && magicBytes[2] == 0x50 && magicBytes[3] == 0x58) ||
                   (magicBytes[0] == 0x58 && magicBytes[1] == 0x50 && magicBytes[2] == 0x44 && magicBytes[3] == 0x53);
        }
        finally
        {
            stream.Position = pos;
        }
    }

    public static DpxImage Decode(string filePath) => Decode(File.ReadAllBytes(filePath));

    public static DpxImage Decode(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Decode(ms.ToArray());
    }

    public static DpxImage Decode(byte[] data)
    {
        if (data.Length < 1024)
            throw new InvalidDataException("DPX file is truncated: header must be at least 1024 bytes.");

        bool isBigEndian;
        if (data[0] == 0x53 && data[1] == 0x44 && data[2] == 0x50 && data[3] == 0x58)
            isBigEndian = true;
        else if (data[0] == 0x58 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x53)
            isBigEndian = false;
        else
            throw new InvalidDataException("Invalid DPX magic identifier (expected 'SDPX' or 'XPDS').");

        uint ReadU32(int offset)
        {
            if (isBigEndian)
            {
                return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
            }
            return BitConverter.ToUInt32(data, offset);
        }

        ushort ReadU16(int offset)
        {
            if (isBigEndian)
            {
                return (ushort)((data[offset] << 8) | data[offset + 1]);
            }
            return BitConverter.ToUInt16(data, offset);
        }

        uint imageOffset = ReadU32(4);
        if (imageOffset < 1024 || imageOffset > data.Length)
            imageOffset = 2048; // Standard SMPTE header size fallback

        int width = (int)ReadU32(772);
        int height = (int)ReadU32(776);
        if (width <= 0 || height <= 0 || width > 32768 || height > 32768)
            throw new InvalidDataException($"Invalid DPX image dimensions: {width}x{height}.");

        byte descriptor = data[801];
        byte bitDepth = data[803];
        ushort packing = ReadU16(804);

        byte[] bgra = new byte[width * height * 4];
        int srcPos = (int)imageOffset;

        if (bitDepth == 8)
        {
            // 8-bit RGB or RGBA
            bool hasAlpha = descriptor == 51; // 50=RGB, 51=RGBA
            int channels = hasAlpha ? 4 : 3;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (srcPos + channels > data.Length) break;

                    byte r = data[srcPos++];
                    byte g = data[srcPos++];
                    byte b = data[srcPos++];
                    byte a = hasAlpha ? data[srcPos++] : (byte)255;

                    int dstIdx = (y * width + x) * 4;
                    bgra[dstIdx + 0] = (byte)((b * a) / 255);
                    bgra[dstIdx + 1] = (byte)((g * a) / 255);
                    bgra[dstIdx + 2] = (byte)((r * a) / 255);
                    bgra[dstIdx + 3] = a;
                }
            }
        }
        else if (bitDepth == 10)
        {
            // 10-bit packed RGB (SMPTE 268M Method A: 32 bits per pixel, 3 10-bit samples with 2 unused bits)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (srcPos + 4 > data.Length) break;

                    uint word = ReadU32(srcPos);
                    srcPos += 4;

                    uint r10, g10, b10;
                    if (packing == 1 || isBigEndian)
                    {
                        // Method A (MSB): R [31..22], G [21..12], B [11..2]
                        r10 = (word >> 22) & 0x3FF;
                        g10 = (word >> 12) & 0x3FF;
                        b10 = (word >> 2) & 0x3FF;
                    }
                    else
                    {
                        // Little-endian packed
                        r10 = word & 0x3FF;
                        g10 = (word >> 10) & 0x3FF;
                        b10 = (word >> 20) & 0x3FF;
                    }

                    byte r = (byte)((r10 * 255) / 1023);
                    byte g = (byte)((g10 * 255) / 1023);
                    byte b = (byte)((b10 * 255) / 1023);

                    int dstIdx = (y * width + x) * 4;
                    bgra[dstIdx + 0] = b;
                    bgra[dstIdx + 1] = g;
                    bgra[dstIdx + 2] = r;
                    bgra[dstIdx + 3] = 255;
                }
            }
        }
        else if (bitDepth == 16)
        {
            // 16-bit RGB
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (srcPos + 6 > data.Length) break;

                    ushort r16 = ReadU16(srcPos);
                    ushort g16 = ReadU16(srcPos + 2);
                    ushort b16 = ReadU16(srcPos + 4);
                    srcPos += 6;

                    byte r = (byte)(r16 >> 8);
                    byte g = (byte)(g16 >> 8);
                    byte b = (byte)(b16 >> 8);

                    int dstIdx = (y * width + x) * 4;
                    bgra[dstIdx + 0] = b;
                    bgra[dstIdx + 1] = g;
                    bgra[dstIdx + 2] = r;
                    bgra[dstIdx + 3] = 255;
                }
            }
        }
        else
        {
            throw new NotSupportedException($"Unsupported DPX bit depth: {bitDepth}-bit.");
        }

        return new DpxImage
        {
            Width = width,
            Height = height,
            BitDepth = bitDepth,
            IsBigEndian = isBigEndian,
            BgraPixels = bgra
        };
    }

    /// <summary>
    /// Encodes a Skia bitmap as a standard SMPTE 268M 8-bit RGB DPX frame.
    /// </summary>
    public static byte[] Encode(SKBitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        int headerSize = 2048;
        int imageSize = width * height * 3;
        byte[] bytes = new byte[headerSize + imageSize];

        // Magic: "SDPX"
        bytes[0] = 0x53; bytes[1] = 0x44; bytes[2] = 0x50; bytes[3] = 0x58;
        // Image offset
        WriteBigEndianU32(bytes, 4, (uint)headerSize);
        // Version "V1.0"
        Encoding.ASCII.GetBytes("V1.0").CopyTo(bytes, 8);
        // Total file size
        WriteBigEndianU32(bytes, 16, (uint)bytes.Length);

        // Generic Image Header
        WriteBigEndianU16(bytes, 768, 0); // Image orientation 0
        WriteBigEndianU16(bytes, 770, 1); // 1 element
        WriteBigEndianU32(bytes, 772, (uint)width);
        WriteBigEndianU32(bytes, 776, (uint)height);

        // Element 0
        bytes[800] = 0; // Data sign: unsigned
        bytes[801] = 50; // Descriptor: RGB
        bytes[802] = 0; // Transfer: User defined
        bytes[803] = 8; // 8-bit
        WriteBigEndianU16(bytes, 804, 0); // Packing: none

        // Pixel data: RGB
        int writePos = headerSize;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                bytes[writePos++] = color.Red;
                bytes[writePos++] = color.Green;
                bytes[writePos++] = color.Blue;
            }
        }

        return bytes;
    }

    private static void WriteBigEndianU32(byte[] dst, int offset, uint value)
    {
        dst[offset] = (byte)((value >> 24) & 0xFF);
        dst[offset + 1] = (byte)((value >> 16) & 0xFF);
        dst[offset + 2] = (byte)((value >> 8) & 0xFF);
        dst[offset + 3] = (byte)(value & 0xFF);
    }

    private static void WriteBigEndianU16(byte[] dst, int offset, ushort value)
    {
        dst[offset] = (byte)((value >> 8) & 0xFF);
        dst[offset + 1] = (byte)(value & 0xFF);
    }
}
