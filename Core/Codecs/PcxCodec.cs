using System;
using System.Buffers.Binary;
using System.IO;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// ZSoft PCX (Paintbrush) image codec.
/// Decodes 1-bit monochrome, 4-bit 16-color EGA/VGA palettes, 8-bit grayscale,
/// 8-bit 256-color VGA palettes, and 24-bit 3-plane RGB images with strictly bounded scanline RLE.
/// </summary>
public static class PcxCodec
{
    private const int HeaderSize = 128;
    private const byte PcxMagic = 0x0A;

    public sealed class PcxImage
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int BitsPerPixel { get; init; }
        public int Planes { get; init; }
        public int DpiX { get; init; }
        public int DpiY { get; init; }
        public byte[] BgraPixels { get; init; } = [];
    }

    /// <summary>
    /// Checks whether the byte slice represents a valid PCX file header.
    /// </summary>
    public static bool LooksLikePcx(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize || bytes[0] != PcxMagic) return false;

        byte version = bytes[1];
        if (version is not (0 or 2 or 3 or 4 or 5)) return false;

        byte encoding = bytes[2];
        if (encoding != 1) return false;

        byte bits = bytes[3];
        byte planes = bytes[65];
        ushort stride = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(66, 2));

        ushort x0 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4, 2));
        ushort y0 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(6, 2));
        ushort x1 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(8, 2));
        ushort y1 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(10, 2));

        if (x1 < x0 || y1 < y0) return false;
        if (bits is not (1 or 2 or 4 or 8) || planes is < 1 or > 4) return false;

        uint width = (uint)(x1 - x0) + 1;
        uint minStride = (width * bits + 7) / 8;
        if (stride < minStride || stride == 0) return false;
        if (bits == 8 && planes is not (1 or 3)) return false;
        if (bits != 8 && bits * planes > 4) return false;

        return true;
    }

    /// <summary>
    /// Decodes a PCX image from a stream.
    /// </summary>
    public static PcxImage Decode(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        byte[] bytes = ms.ToArray();
        return Decode(bytes);
    }

    /// <summary>
    /// Decodes a PCX image from raw bytes.
    /// </summary>
    public static PcxImage Decode(byte[] bytes)
    {
        if (!LooksLikePcx(bytes))
            throw new InvalidDataException("Invalid PCX file header or unsupported layout.");

        ushort x0 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        ushort y0 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2));
        ushort x1 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
        ushort y1 = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10, 2));

        int width = (x1 - x0) + 1;
        int height = (y1 - y0) + 1;

        if (width <= 0 || height <= 0 || width > 65536 || height > 65536)
            throw new InvalidDataException($"Plausibility check failed for PCX dimensions: {width}x{height}");

        byte bits = bytes[3];
        byte planes = bytes[65];
        ushort stride = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(66, 2));
        ushort xdpi = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(12, 2));
        ushort ydpi = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(14, 2));
        ushort paletteInfo = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(68, 2));

        int rowLen = stride * planes;
        int totalPixels = width * height;
        byte[] bgra = new byte[totalPixels * 4];

        // 256-color VGA palette at end of file (last 769 bytes with 0x0C marker)
        bool vga = bits == 8 && planes == 1 && bytes.Length >= HeaderSize + 769 && bytes[^769] == 0x0C;
        if (bits == 8 && planes == 1 && !vga && paletteInfo != 2)
        {
            // Fallback: if no 256-color palette marker, interpret as grayscale
        }

        int imageEnd = vga ? bytes.Length - 769 : bytes.Length;
        byte[] rowBuffer = new byte[rowLen];
        int pos = HeaderSize;

        for (int y = 0; y < height; y++)
        {
            // Read scanline planes
            for (int p = 0; p < planes; p++)
            {
                ReadPlaneRow(bytes.AsSpan(0, imageEnd), ref pos, rowBuffer.AsSpan(p * stride, stride));
            }

            int rowOffset = y * width * 4;

            for (int x = 0; x < width; x++)
            {
                byte r, g, b;

                if (bits == 8 && planes == 3)
                {
                    // 24-bit RGB planar
                    r = rowBuffer[x];
                    g = rowBuffer[stride + x];
                    b = rowBuffer[2 * stride + x];
                }
                else
                {
                    int index;
                    if (bits == 8)
                    {
                        index = rowBuffer[x];
                    }
                    else
                    {
                        int bit = x * bits;
                        int shift = 8 - bits - (bit % 8);
                        int mask = (1 << bits) - 1;
                        index = 0;
                        for (int p = 0; p < planes; p++)
                        {
                            int sample = (rowBuffer[p * stride + bit / 8] >> shift) & mask;
                            index |= sample << (p * bits);
                        }
                    }

                    if (vga)
                    {
                        int palOffset = imageEnd + 1 + index * 3;
                        if (palOffset + 2 < bytes.Length)
                        {
                            r = bytes[palOffset];
                            g = bytes[palOffset + 1];
                            b = bytes[palOffset + 2];
                        }
                        else
                        {
                            r = g = b = (byte)index;
                        }
                    }
                    else if (bits == 8)
                    {
                        // Grayscale
                        r = g = b = (byte)index;
                    }
                    else
                    {
                        // 16-color header palette at bytes 16..63
                        int palOffset = 16 + (index & 0x0F) * 3;
                        r = bytes[palOffset];
                        g = bytes[palOffset + 1];
                        b = bytes[palOffset + 2];
                    }
                }

                int px = rowOffset + x * 4;
                bgra[px] = b;
                bgra[px + 1] = g;
                bgra[px + 2] = r;
                bgra[px + 3] = 255; // Fully opaque
            }
        }

        return new PcxImage
        {
            Width = width,
            Height = height,
            BitsPerPixel = bits,
            Planes = planes,
            DpiX = xdpi > 0 ? xdpi : 72,
            DpiY = ydpi > 0 ? ydpi : 72,
            BgraPixels = bgra
        };
    }

    private static void ReadPlaneRow(ReadOnlySpan<byte> source, ref int pos, Span<byte> outPlane)
    {
        int written = 0;
        while (written < outPlane.Length)
        {
            if (pos >= source.Length)
                throw new InvalidDataException("Truncated PCX scanline RLE data.");

            byte code = source[pos++];
            int count;
            byte value;

            if ((code & 0xC0) == 0xC0)
            {
                count = code & 0x3F;
                if (pos >= source.Length)
                    throw new InvalidDataException("Truncated PCX RLE packet.");
                value = source[pos++];
            }
            else
            {
                count = 1;
                value = code;
            }

            if (count == 0 || count > outPlane.Length - written)
                throw new InvalidDataException("PCX RLE packet crosses scanline boundary or is empty.");

            outPlane.Slice(written, count).Fill(value);
            written += count;
        }
    }
}
