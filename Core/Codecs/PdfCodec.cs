using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// ISO 32000-1 raster PDF document writer for File > Save As / Save a Copy / Export.
/// Generates standards-compliant, standalone one-page PDF files containing FlateDecode-compressed
/// image XObjects at physical print dimensions (points = pixels / DPI * 72) with paper background.
/// Upstream parity with commit ad96df56 (#3401, #3441).
/// </summary>
public static class PdfCodec
{
    public static bool LooksLikePdf(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 5) return false;
        return bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F' && bytes[4] == '-';
    }

    public static bool LooksLikePdf(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < 5) return false;
        long pos = stream.Position;
        try
        {
            Span<byte> magicBytes = stackalloc byte[5];
            int read = stream.Read(magicBytes);
            if (read < 5) return false;
            return magicBytes[0] == '%' && magicBytes[1] == 'P' && magicBytes[2] == 'D' && magicBytes[3] == 'F' && magicBytes[4] == '-';
        }
        finally
        {
            stream.Position = pos;
        }
    }

    public static byte[] Encode(SKBitmap bitmap, double dpi = 72.0, string? label = null, string? description = null, byte[]? iccProfile = null)
    {
        if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            throw new ArgumentException("Bitmap must have positive non-zero dimensions.", nameof(bitmap));

        int width = bitmap.Width;
        int height = bitmap.Height;
        dpi = Math.Max(1.0, dpi);

        double pw = (double)width * 72.0 / dpi;
        double ph = (double)height * 72.0 / dpi;

        // Extract RGB888 samples
        byte[] rgbData = new byte[width * height * 3];
        int rgbIdx = 0;

        using (var pixels = bitmap.PeekPixels())
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var color = pixels.GetPixelColor(x, y);
                    // Composite over white paper if alpha < 255
                    float a = color.Alpha / 255f;
                    byte r = (byte)Math.Clamp((int)Math.Round(color.Red * a + 255 * (1f - a)), 0, 255);
                    byte g = (byte)Math.Clamp((int)Math.Round(color.Green * a + 255 * (1f - a)), 0, 255);
                    byte b = (byte)Math.Clamp((int)Math.Round(color.Blue * a + 255 * (1f - a)), 0, 255);

                    rgbData[rgbIdx++] = r;
                    rgbData[rgbIdx++] = g;
                    rgbData[rgbIdx++] = b;
                }
            }
        }

        using var ms = new MemoryStream();
        var offsets = new System.Collections.Generic.List<long>();

        void WriteBytes(byte[] bytes) => ms.Write(bytes, 0, bytes.Length);
        void WriteString(string s) => WriteBytes(Encoding.ASCII.GetBytes(s));

        void StartObj()
        {
            offsets.Add(ms.Position);
            WriteString($"{offsets.Count} 0 obj\n");
        }

        void EndObj() => WriteString("\nendobj\n");

        byte[] Deflate(byte[] input)
        {
            using var outStream = new MemoryStream();
            using (var zlib = new ZLibStream(outStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(input, 0, input.Length);
            }
            return outStream.ToArray();
        }

        // Header
        WriteString("%PDF-1.4\n%\xe2\xe3\xcf\xd3\n");

        // 1: Catalog
        StartObj();
        WriteString("<< /Type /Catalog /Pages 2 0 R >>");
        EndObj();

        // 2: Pages
        StartObj();
        WriteString("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        EndObj();

        // 3: Page
        StartObj();
        WriteString($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pw:F2} {ph:F2}] /Resources << /XObject << /Im0 5 0 R >> /Font << /F1 6 0 R >> >> /Contents 4 0 R >>");
        EndObj();

        // 4: Contents
        var contentSb = new StringBuilder();
        // White paper background + draw image scaled across page
        contentSb.Append($"q 1 g 0 0 {pw:F2} {ph:F2} re f Q\n");
        contentSb.Append($"q {pw:F3} 0 0 {ph:F3} 0 0 cm /Im0 Do Q\n");

        if (!string.IsNullOrEmpty(label))
        {
            string safeLabel = label.Replace("(", "\\(").Replace(")", "\\)").Replace("\\", "\\\\");
            contentSb.Append($"BT /F1 8 Tf 0 g 6.00 {ph - 14.00:F2} Td ({safeLabel}) Tj ET\n");
        }
        if (!string.IsNullOrEmpty(description))
        {
            string safeDesc = description.Replace("(", "\\(").Replace(")", "\\)").Replace("\\", "\\\\");
            contentSb.Append($"BT /F1 8 Tf 0 g 6.00 6.00 Td ({safeDesc}) Tj ET\n");
        }

        byte[] deflatedContent = Deflate(Encoding.ASCII.GetBytes(contentSb.ToString()));
        StartObj();
        WriteString($"<< /Length {deflatedContent.Length} /Filter /FlateDecode >>\nstream\n");
        WriteBytes(deflatedContent);
        WriteString("\nendstream");
        EndObj();

        // 5: Image XObject
        byte[] deflatedImg = Deflate(rgbData);
        string colorSpace = iccProfile != null && iccProfile.Length > 0 ? "[/ICCBased 7 0 R]" : "/DeviceRGB";

        StartObj();
        WriteString($"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace {colorSpace} /BitsPerComponent 8 /Filter /FlateDecode /Length {deflatedImg.Length} >>\nstream\n");
        WriteBytes(deflatedImg);
        WriteString("\nendstream");
        EndObj();

        // 6: Font (Helvetica)
        StartObj();
        WriteString("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        EndObj();

        // 7: Optional ICC Profile
        if (iccProfile != null && iccProfile.Length > 0)
        {
            byte[] deflatedIcc = Deflate(iccProfile);
            StartObj();
            WriteString($"<< /N 3 /Alternate /DeviceRGB /Filter /FlateDecode /Length {deflatedIcc.Length} >>\nstream\n");
            WriteBytes(deflatedIcc);
            WriteString("\nendstream");
            EndObj();
        }

        // Cross-reference table
        long xrefPos = ms.Position;
        WriteString($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteString($"{offset:D10} 00000 n \n");
        }

        // Trailer
        WriteString($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");

        return ms.ToArray();
    }
}
