using System;
using System.IO;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// Kodak Cineon (.cin) 10-bit logarithmic film-scan frame format decoder.
/// Cineon is the historic 10-bit film format from which SMPTE 268M DPX evolved.
/// Unpacks 3 10-bit samples packed per 32-bit big-endian word (RGB or Grayscale).
/// Upstream parity with commit 5dafbeea (#4691).
/// </summary>
public static class CineonCodec
{
    private const uint Magic = 0x802A5FD7;

    public static bool LooksLikeCineon(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4) return false;
        return bytes[0] == 0x80 && bytes[1] == 0x2A && bytes[2] == 0x5F && bytes[3] == 0xD7;
    }

    public static bool LooksLikeCineon(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 4) return false;
        long pos = stream.Position;
        try
        {
            Span<byte> magicBytes = stackalloc byte[4];
            int read = stream.Read(magicBytes);
            if (read < 4) return false;
            return magicBytes[0] == 0x80 && magicBytes[1] == 0x2A && magicBytes[2] == 0x5F && magicBytes[3] == 0xD7;
        }
        finally
        {
            stream.Position = pos;
        }
    }

    public static SKBitmap Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 1024)
            throw new InvalidDataException("Cineon file must be at least 1024 bytes for generic header.");

        if (!LooksLikeCineon(bytes))
            throw new InvalidDataException("Invalid Cineon magic header (expected 0x802A5FD7).");

        uint dataOffset = ReadUInt32BE(bytes, 4);
        if (dataOffset < 1024 || dataOffset >= bytes.Length)
            dataOffset = 1024; // Fallback to standard 1024 generic header

        byte channels = bytes[193];
        if (channels != 1 && channels != 3)
            throw new NotSupportedException($"Cineon only supports 1 (Gray) or 3 (RGB) channels, got {channels}.");

        byte bitsPerPixel = bytes[198];
        if (bitsPerPixel != 10)
            throw new NotSupportedException($"Cineon only supports 10-bit packed channels, got {bitsPerPixel}.");

        int width = (int)ReadUInt32BE(bytes, 200);
        int height = (int)ReadUInt32BE(bytes, 204);

        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
            throw new InvalidDataException($"Invalid Cineon frame dimensions: {width}x{height}.");

        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        byte[] bgra = new byte[width * height * 4];

        int pixelCount = width * height;
        int streamPos = (int)dataOffset;

        byte To8Bit(uint sample10) => (byte)Math.Clamp((int)Math.Round((double)sample10 * 255.0 / 1023.0), 0, 255);

        if (channels == 3)
        {
            int outIdx = 0;
            for (int i = 0; i < pixelCount; i++)
            {
                if (streamPos + 4 > bytes.Length) break;

                uint word = ReadUInt32BE(bytes, streamPos);
                streamPos += 4;

                uint r10 = (word >> 22) & 0x3FF;
                uint g10 = (word >> 12) & 0x3FF;
                uint b10 = (word >> 2) & 0x3FF;

                bgra[outIdx++] = To8Bit(b10); // B
                bgra[outIdx++] = To8Bit(g10); // G
                bgra[outIdx++] = To8Bit(r10); // R
                bgra[outIdx++] = 255;        // A
            }
        }
        else // channels == 1 (Grayscale)
        {
            int outIdx = 0;
            int remaining = pixelCount;
            while (remaining > 0 && streamPos + 4 <= bytes.Length)
            {
                uint word = ReadUInt32BE(bytes, streamPos);
                streamPos += 4;

                uint g0 = (word >> 22) & 0x3FF;
                uint g1 = (word >> 12) & 0x3FF;
                uint g2 = (word >> 2) & 0x3FF;

                uint[] samples = [g0, g1, g2];
                int toTake = Math.Min(3, remaining);
                for (int s = 0; s < toTake; s++)
                {
                    byte v = To8Bit(samples[s]);
                    bgra[outIdx++] = v;
                    bgra[outIdx++] = v;
                    bgra[outIdx++] = v;
                    bgra[outIdx++] = 255;
                }
                remaining -= toTake;
            }
        }

        var pixelsPtr = bitmap.GetPixels();
        System.Runtime.InteropServices.Marshal.Copy(bgra, 0, pixelsPtr, bgra.Length);
        return bitmap;
    }

    private static uint ReadUInt32BE(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24) |
               ((uint)bytes[offset + 1] << 16) |
               ((uint)bytes[offset + 2] << 8) |
               (uint)bytes[offset + 3];
    }
}
