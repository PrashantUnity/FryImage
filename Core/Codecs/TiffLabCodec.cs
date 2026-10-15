using System;
using System.IO;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// CIE L*a*b* (D50 Bradford adapted) color conversion and TIFF Lab codec.
/// Handles PhotometricInterpretation 8 (CIELab) and 9 (ICCLab).
/// Upstream parity with commit 1a4f8679 (#4949).
/// </summary>
public static class TiffLabCodec
{
    private static readonly float[] D50 = [0.96422f, 1.0f, 0.82521f];

    private static readonly float[][] SrgbToXyzD50 = [
        [0.436074f, 0.385064f, 0.143080f],
        [0.222504f, 0.716878f, 0.060618f],
        [0.013932f, 0.097104f, 0.714173f]
    ];

    private static readonly float[][] XyzD50ToSrgb = [
        [3.133856f, -1.616867f, -0.490615f],
        [-0.978768f, 1.916142f, 0.033454f],
        [0.071945f, -0.228991f, 1.405243f]
    ];

    public static (float r, float g, float b) LabToSrgb(float lNorm, float aNorm, float bNorm)
    {
        float l = lNorm * 100.0f;
        float a = aNorm * 255.0f - 128.0f;
        float b = bNorm * 255.0f - 128.0f;

        float fy = (l + 16.0f) / 116.0f;
        float fx = fy + a / 500.0f;
        float fz = fy - b / 200.0f;

        static float Finv(float t)
        {
            const float delta = 6.0f / 29.0f;
            return t > delta ? t * t * t : 3.0f * delta * delta * (t - 4.0f / 29.0f);
        }

        float x = Finv(fx) * D50[0];
        float y = Finv(fy) * D50[1];
        float z = Finv(fz) * D50[2];

        float rLin = Math.Clamp(XyzD50ToSrgb[0][0] * x + XyzD50ToSrgb[0][1] * y + XyzD50ToSrgb[0][2] * z, 0.0f, 1.0f);
        float gLin = Math.Clamp(XyzD50ToSrgb[1][0] * x + XyzD50ToSrgb[1][1] * y + XyzD50ToSrgb[1][2] * z, 0.0f, 1.0f);
        float bLin = Math.Clamp(XyzD50ToSrgb[2][0] * x + XyzD50ToSrgb[2][1] * y + XyzD50ToSrgb[2][2] * z, 0.0f, 1.0f);

        static float ToEncoded(float v) => v <= 0.0031308f ? v * 12.92f : 1.055f * MathF.Pow(v, 1.0f / 2.4f) - 0.055f;

        return (
            Math.Clamp(ToEncoded(rLin), 0.0f, 1.0f),
            Math.Clamp(ToEncoded(gLin), 0.0f, 1.0f),
            Math.Clamp(ToEncoded(bLin), 0.0f, 1.0f)
        );
    }

    public static (float lNorm, float aNorm, float bNorm) SrgbToLab(float rEnc, float gEnc, float bEnc)
    {
        static float ToLinear(float v) => v <= 0.04045f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);

        float r = ToLinear(Math.Clamp(rEnc, 0.0f, 1.0f));
        float g = ToLinear(Math.Clamp(gEnc, 0.0f, 1.0f));
        float b = ToLinear(Math.Clamp(bEnc, 0.0f, 1.0f));

        float x = SrgbToXyzD50[0][0] * r + SrgbToXyzD50[0][1] * g + SrgbToXyzD50[0][2] * b;
        float y = SrgbToXyzD50[1][0] * r + SrgbToXyzD50[1][1] * g + SrgbToXyzD50[1][2] * b;
        float z = SrgbToXyzD50[2][0] * r + SrgbToXyzD50[2][1] * g + SrgbToXyzD50[2][2] * b;

        static float F(float t)
        {
            const float delta = 6.0f / 29.0f;
            const float delta3 = delta * delta * delta;
            return t > delta3 ? MathF.Cbrt(t) : t / (3.0f * delta * delta) + 4.0f / 29.0f;
        }

        float fx = F(x / D50[0]);
        float fy = F(y / D50[1]);
        float fz = F(z / D50[2]);

        float l = 116.0f * fy - 16.0f;
        float a = 500.0f * (fx - fy);
        float bVal = 200.0f * (fy - fz);

        return (
            Math.Clamp(l / 100.0f, 0.0f, 1.0f),
            Math.Clamp((a + 128.0f) / 255.0f, 0.0f, 1.0f),
            Math.Clamp((bVal + 128.0f) / 255.0f, 0.0f, 1.0f)
        );
    }

    /// <summary>
    /// Converts a raw Lab 8-bit buffer (L: 0..255, a: 0..255, b: 0..255) to an SKBitmap.
    /// In CIELab: L* = byte * 100 / 255, a* = signed byte (-128..127) or byte - 128.
    /// </summary>
    public static SKBitmap ConvertLabBufferToBitmap(int width, int height, byte[] labBytes, bool isIccLab = false)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        byte[] bgra = new byte[width * height * 4];

        int inIdx = 0;
        int outIdx = 0;
        int pixelCount = width * height;

        for (int i = 0; i < pixelCount; i++)
        {
            if (inIdx + 3 > labBytes.Length) break;

            byte lByte = labBytes[inIdx++];
            byte aByte = labBytes[inIdx++];
            byte bByte = labBytes[inIdx++];

            float lNorm = lByte / 255.0f;
            float aNorm = aByte / 255.0f;
            float bNorm = bByte / 255.0f;

            var (r, g, b) = LabToSrgb(lNorm, aNorm, bNorm);

            bgra[outIdx++] = (byte)Math.Clamp((int)Math.Round(b * 255.0f), 0, 255);
            bgra[outIdx++] = (byte)Math.Clamp((int)Math.Round(g * 255.0f), 0, 255);
            bgra[outIdx++] = (byte)Math.Clamp((int)Math.Round(r * 255.0f), 0, 255);
            bgra[outIdx++] = 255;
        }

        var ptr = bmp.GetPixels();
        System.Runtime.InteropServices.Marshal.Copy(bgra, 0, ptr, bgra.Length);
        return bmp;
    }

    /// <summary>
    /// Converts an SKBitmap to an 8-bit Lab buffer (3 bytes per pixel: L, a, b).
    /// </summary>
    public static byte[] ConvertBitmapToLabBuffer(SKBitmap bmp)
    {
        int width = bmp.Width;
        int height = bmp.Height;
        byte[] labBytes = new byte[width * height * 3];

        int outIdx = 0;
        using var pixels = bmp.PeekPixels();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = pixels.GetPixelColor(x, y);
                var (l, a, b) = SrgbToLab(color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f);

                labBytes[outIdx++] = (byte)Math.Clamp((int)Math.Round(l * 255.0f), 0, 255);
                labBytes[outIdx++] = (byte)Math.Clamp((int)Math.Round(a * 255.0f), 0, 255);
                labBytes[outIdx++] = (byte)Math.Clamp((int)Math.Round(b * 255.0f), 0, 255);
            }
        }

        return labBytes;
    }

    /// <summary>
    /// Writes an uncompressed baseline TIFF (PhotometricInterpretation 2 RGB, Compression 1 Uncompressed)
    /// exactly matching Standard default specification (commit 5c73f4ff).
    /// </summary>
    public static void WriteTiffUncompressed(SKBitmap bmp, Stream outStream)
    {
        int width = bmp.Width;
        int height = bmp.Height;

        using var bw = new BinaryWriter(outStream, System.Text.Encoding.ASCII, leaveOpen: true);

        // Header: Little-Endian ("II"), Version 42, IFD Offset = 8
        bw.Write((ushort)0x4949);
        bw.Write((ushort)42);
        bw.Write((uint)8);

        // Strip data follows IFD and auxiliary tags
        // IFD has 12 entries (12 * 12 = 144 bytes) + 2 bytes count + 4 bytes next IFD offset = 150 bytes
        // Auxiliary tags: BitsPerSample [8, 8, 8] (6 bytes), XRes (8 bytes), YRes (8 bytes) = 22 bytes
        // Header (8) + IFD (150) + Aux (24) = 182 bytes. Round to 192 for alignment.
        ushort entryCount = 12;

        uint bitsPerSampleOffset = 8 + 2 + (uint)(entryCount * 12) + 4; // 8 + 2 + 144 + 4 = 158
        uint xResOffset = bitsPerSampleOffset + 6;                       // 164
        uint yResOffset = xResOffset + 8;                               // 172
        uint stripOffset = yResOffset + 8;                              // 180 (aligned to 4 -> 180)
        uint stripByteCount = (uint)(width * height * 3);

        bw.Write(entryCount);

        void WriteEntry(ushort tag, ushort type, uint count, uint valOrOffset)
        {
            bw.Write(tag);
            bw.Write(type);
            bw.Write(count);
            bw.Write(valOrOffset);
        }

        // Tag definitions: Type 3 = SHORT, 4 = LONG, 5 = RATIONAL
        WriteEntry(256, 4, 1, (uint)width);           // ImageWidth
        WriteEntry(257, 4, 1, (uint)height);          // ImageLength
        WriteEntry(258, 3, 3, bitsPerSampleOffset);   // BitsPerSample [8, 8, 8]
        WriteEntry(259, 3, 1, 1);                     // Compression = 1 (Uncompressed)
        WriteEntry(262, 3, 1, 2);                     // PhotometricInterpretation = 2 (RGB)
        WriteEntry(273, 4, 1, stripOffset);           // StripOffsets
        WriteEntry(277, 3, 1, 3);                     // SamplesPerPixel = 3
        WriteEntry(278, 4, 1, (uint)height);          // RowsPerStrip
        WriteEntry(279, 4, 1, stripByteCount);        // StripByteCounts
        WriteEntry(282, 5, 1, xResOffset);            // XResolution
        WriteEntry(283, 5, 1, yResOffset);            // YResolution
        WriteEntry(296, 3, 1, 2);                     // ResolutionUnit = 2 (Inch)

        bw.Write((uint)0); // Next IFD = 0 (none)

        // Auxiliary tag values
        bw.Write((ushort)8); // BitsPerSample R
        bw.Write((ushort)8); // BitsPerSample G
        bw.Write((ushort)8); // BitsPerSample B

        bw.Write((uint)72); // XRes numerator
        bw.Write((uint)1);  // XRes denominator

        bw.Write((uint)72); // YRes numerator
        bw.Write((uint)1);  // YRes denominator

        // Raw uncompressed RGB scanlines
        using var pixels = bmp.PeekPixels();
        byte[] scanline = new byte[width * 3];
        for (int y = 0; y < height; y++)
        {
            int sIdx = 0;
            for (int x = 0; x < width; x++)
            {
                var c = pixels.GetPixelColor(x, y);
                scanline[sIdx++] = c.Red;
                scanline[sIdx++] = c.Green;
                scanline[sIdx++] = c.Blue;
            }
            bw.Write(scanline);
        }

        bw.Flush();
    }
}
