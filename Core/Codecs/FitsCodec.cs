using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// FITS (Flexible Image Transport System, FITS Standard 4.0) Codec for astronomical images.
/// Reads and writes primary HDUs with BITPIX 8, 16, 32, -32 (IEEE float) and -64 (IEEE double),
/// with BSCALE/BZERO physical scaling, bottom-up/top-down row order, and 2880-byte block framing.
/// </summary>
public static class FitsCodec
{
    private const int BlockSize = 2880;
    private const int CardSize = 80;

    public sealed class FitsImage
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int BitPix { get; init; }
        public int Channels { get; init; } = 3;
        public byte[] BgraPixels { get; init; } = [];
        public Dictionary<string, string> HeaderCards { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Decodes a FITS primary image HDU from a stream into 32-bit BGRA pixels.
    /// </summary>
    public static FitsImage Decode(Stream stream)
    {
        var cards = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        byte[] cardBuffer = new byte[CardSize];
        bool endFound = false;
        int totalHeaderBytes = 0;

        while (!endFound)
        {
            int read = stream.Read(cardBuffer, 0, CardSize);
            if (read < CardSize)
                throw new InvalidDataException("Unexpected end of file while reading FITS header.");
            totalHeaderBytes += CardSize;

            string card = Encoding.ASCII.GetString(cardBuffer);
            string key = card[..Math.Min(8, card.Length)].Trim();

            if (string.Equals(key, "END", StringComparison.OrdinalIgnoreCase))
            {
                endFound = true;
            }
            else if (card.Length > 8 && card[8] == '=')
            {
                string val = card[9..].Trim();
                int slashIdx = val.IndexOf('/');
                if (slashIdx >= 0) val = val[..slashIdx].Trim();
                val = val.Trim('\'').Trim();
                if (!string.IsNullOrEmpty(key)) cards[key] = val;
            }
        }

        // Align stream to next 2880-byte boundary
        int pad = (BlockSize - (totalHeaderBytes % BlockSize)) % BlockSize;
        if (pad > 0)
        {
            stream.Seek(pad, SeekOrigin.Current);
        }

        int bitpix = int.Parse(cards.GetValueOrDefault("BITPIX", "8"));
        int naxis = int.Parse(cards.GetValueOrDefault("NAXIS", "2"));
        int width = int.Parse(cards.GetValueOrDefault("NAXIS1", "0"));
        int height = int.Parse(cards.GetValueOrDefault("NAXIS2", "0"));
        int naxis3 = int.Parse(cards.GetValueOrDefault("NAXIS3", "1"));

        if (width <= 0 || height <= 0 || naxis < 2)
            throw new InvalidDataException("Invalid or empty FITS image dimensions.");

        double bzero = 0.0;
        if (cards.TryGetValue("BZERO", out var bzeroStr))
            double.TryParse(bzeroStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bzero);

        double bscale = 1.0;
        if (cards.TryGetValue("BSCALE", out var bscaleStr))
            double.TryParse(bscaleStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bscale);

        bool isTopDown = string.Equals(cards.GetValueOrDefault("ROWORDER", "BOTTOM-UP"), "TOP-DOWN", StringComparison.OrdinalIgnoreCase);

        int planes = Math.Max(1, naxis3);
        int bytesPerSample = Math.Abs(bitpix) / 8;
        int totalPixelsPerPlane = width * height;
        int totalSamples = totalPixelsPerPlane * planes;
        int dataBytes = totalSamples * bytesPerSample;

        byte[] rawData = new byte[dataBytes];
        int readData = 0;
        while (readData < dataBytes)
        {
            int r = stream.Read(rawData, readData, dataBytes - readData);
            if (r == 0) break;
            readData += r;
        }

        // Decode samples to float planes
        float[][] planeFloats = new float[planes][];
        for (int p = 0; p < planes; p++)
        {
            planeFloats[p] = new float[totalPixelsPerPlane];
            int planeByteOffset = p * totalPixelsPerPlane * bytesPerSample;

            for (int i = 0; i < totalPixelsPerPlane; i++)
            {
                int offset = planeByteOffset + i * bytesPerSample;
                if (offset + bytesPerSample > rawData.Length) break;

                double rawVal = bitpix switch
                {
                    8 => rawData[offset],
                    16 => BinaryPrimitives.ReadInt16BigEndian(rawData.AsSpan(offset, 2)),
                    32 => BinaryPrimitives.ReadInt32BigEndian(rawData.AsSpan(offset, 4)),
                    64 => BinaryPrimitives.ReadInt64BigEndian(rawData.AsSpan(offset, 8)),
                    -32 => BinaryPrimitives.ReadSingleBigEndian(rawData.AsSpan(offset, 4)),
                    -64 => BinaryPrimitives.ReadDoubleBigEndian(rawData.AsSpan(offset, 8)),
                    _ => rawData[offset]
                };

                double physVal = rawVal * bscale + bzero;
                planeFloats[p][i] = float.IsNaN((float)physVal) ? 0.0f : (float)physVal;
            }
        }

        // Determine min and max for scaling
        float minVal = float.MaxValue;
        float maxVal = float.MinValue;
        for (int p = 0; p < planes; p++)
        {
            for (int i = 0; i < totalPixelsPerPlane; i++)
            {
                float v = planeFloats[p][i];
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }
        }

        if (maxVal <= minVal) maxVal = minVal + 1.0f;
        float scale = 255.0f / (maxVal - minVal);

        byte[] bgra = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            int srcY = isTopDown ? y : (height - 1 - y);
            int dstRowOffset = y * width * 4;
            int srcRowOffset = srcY * width;

            for (int x = 0; x < width; x++)
            {
                int srcIdx = srcRowOffset + x;
                int dstIdx = dstRowOffset + x * 4;

                byte r, g, b;
                if (planes >= 3)
                {
                    r = (byte)Math.Clamp((planeFloats[0][srcIdx] - minVal) * scale, 0, 255);
                    g = (byte)Math.Clamp((planeFloats[1][srcIdx] - minVal) * scale, 0, 255);
                    b = (byte)Math.Clamp((planeFloats[2][srcIdx] - minVal) * scale, 0, 255);
                }
                else
                {
                    byte gray = (byte)Math.Clamp((planeFloats[0][srcIdx] - minVal) * scale, 0, 255);
                    r = g = b = gray;
                }

                bgra[dstIdx] = b;
                bgra[dstIdx + 1] = g;
                bgra[dstIdx + 2] = r;
                bgra[dstIdx + 3] = 255;
            }
        }

        var result = new FitsImage
        {
            Width = width,
            Height = height,
            BitPix = bitpix,
            Channels = planes >= 3 ? 3 : 1,
            BgraPixels = bgra
        };

        foreach (var (k, v) in cards) result.HeaderCards[k] = v;
        return result;
    }

    /// <summary>
    /// Encodes a 32-bit BGRA image into a standard FITS file with primary 3D RGB HDU.
    /// </summary>
    public static void Encode(byte[] bgra, int width, int height, Stream outStream)
    {
        var cards = new List<string>
        {
            FormatCard("SIMPLE", "T", "Standard FITS 4.0 format"),
            FormatCard("BITPIX", "8", "8-bit unsigned integer samples"),
            FormatCard("NAXIS", "3", "3-dimensional RGB image"),
            FormatCard("NAXIS1", width.ToString(), "Image width in pixels"),
            FormatCard("NAXIS2", height.ToString(), "Image height in pixels"),
            FormatCard("NAXIS3", "3", "RGB color planes"),
            FormatCard("ROWORDER", "'BOTTOM-UP'", "Standard astronomical convention"),
            FormatCard("CREATOR", "'FryImage Studio'", "Exported from FryImage Studio"),
            FormatCard("END", "", "")
        };

        var headerSb = new StringBuilder();
        foreach (var c in cards)
        {
            headerSb.Append(c);
        }

        // Pad header to 2880-byte multiple with spaces
        int headerBytes = headerSb.Length;
        int headerPad = (BlockSize - (headerBytes % BlockSize)) % BlockSize;
        headerSb.Append(' ', headerPad);

        byte[] headerData = Encoding.ASCII.GetBytes(headerSb.ToString());
        outStream.Write(headerData, 0, headerData.Length);

        // Write image data: planar Red, Green, Blue from bottom-to-top
        int planeSize = width * height;
        byte[] rPlane = new byte[planeSize];
        byte[] gPlane = new byte[planeSize];
        byte[] bPlane = new byte[planeSize];

        for (int y = 0; y < height; y++)
        {
            int srcY = height - 1 - y; // Bottom-up
            int srcRowOffset = srcY * width * 4;
            int dstRowOffset = y * width;

            for (int x = 0; x < width; x++)
            {
                int srcIdx = srcRowOffset + x * 4;
                int dstIdx = dstRowOffset + x;

                bPlane[dstIdx] = bgra[srcIdx];
                gPlane[dstIdx] = bgra[srcIdx + 1];
                rPlane[dstIdx] = bgra[srcIdx + 2];
            }
        }

        outStream.Write(rPlane, 0, planeSize);
        outStream.Write(gPlane, 0, planeSize);
        outStream.Write(bPlane, 0, planeSize);

        // Pad data to 2880-byte boundary with 0
        int totalDataBytes = planeSize * 3;
        int dataPad = (BlockSize - (totalDataBytes % BlockSize)) % BlockSize;
        if (dataPad > 0)
        {
            byte[] zeroes = new byte[dataPad];
            outStream.Write(zeroes, 0, dataPad);
        }
    }

    private static string FormatCard(string key, string val, string comment)
    {
        string card;
        if (key == "END")
        {
            card = "END".PadRight(CardSize, ' ');
        }
        else
        {
            string keyPart = key.PadRight(8, ' ');
            string valPart = string.IsNullOrEmpty(val) ? "" : $"= {val,20}";
            string commentPart = string.IsNullOrEmpty(comment) ? "" : $" / {comment}";
            card = (keyPart + valPart + commentPart).PadRight(CardSize, ' ');
            if (card.Length > CardSize) card = card[..CardSize];
        }
        return card;
    }
}
