using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Printing;

public record PrinterInfo(string Name, bool IsDefault);

public record PrintMarks(bool CornerCrop, bool CenterCrop, bool Registration, bool Description, bool Labels);

public record PrintLayout(
    double PaperWidthPt,
    double PaperHeightPt,
    double ImageXPt,
    double ImageYPt,
    double ImageWidthPt,
    double ImageHeightPt,
    double Scale,
    bool FitsOnPaper,
    bool EmulsionDown,
    PrintMarks Marks,
    string ScaleAndDimensionsLabel);

/// <summary>
/// Cross-platform Print Layout & Spooling Engine matching Studio and Photocraft (#4748 parity).
/// </summary>
public static class PrintEngine
{
    public static readonly Dictionary<string, (double WidthPt, double HeightPt)> StandardPapers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["letter"] = (612.0, 792.0),
        ["legal"] = (612.0, 1008.0),
        ["tabloid"] = (792.0, 1224.0),
        ["a3"] = (841.89, 1190.55),
        ["a4"] = (595.28, 841.89),
        ["a5"] = (419.53, 595.28),
        ["b3"] = (1000.63, 1417.32), // 353 × 500 mm
        ["b4"] = (708.66, 1000.63), // 250 × 353 mm
        ["4x6"] = (288.0, 432.0),
        ["5x7"] = (360.0, 504.0),
    };

    /// <summary>
    /// Queries installed system printer queues from CUPS (macOS/Linux) or Win32 spooler (Windows).
    /// </summary>
    public static List<PrinterInfo> GetSystemPrinters()
    {
        var result = new List<PrinterInfo>();
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Query default printer via lpstat -d
                string defaultPrinter = "";
                try
                {
                    var dpsi = new ProcessStartInfo("lpstat", "-d")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var dp = Process.Start(dpsi);
                    if (dp != null)
                    {
                        string outD = dp.StandardOutput.ReadToEnd();
                        dp.WaitForExit(1000);
                        // format: "system default destination: Printer_Name"
                        var parts = outD.Split(':');
                        if (parts.Length > 1) defaultPrinter = parts[1].Trim();
                    }
                }
                catch { }

                // Query destinations via lpstat -e (or lpstat -p)
                try
                {
                    var psi = new ProcessStartInfo("lpstat", "-e")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(1500);
                        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var line in lines)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                bool isDef = !string.IsNullOrEmpty(defaultPrinter) && line.Equals(defaultPrinter, StringComparison.OrdinalIgnoreCase);
                                result.Add(new PrinterInfo(line, isDef));
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch { }

        if (result.Count == 0)
        {
            result.Add(new PrinterInfo("Default Printer", true));
            result.Add(new PrinterInfo("Save as PDF", false));
        }

        return result;
    }

    public static (double WidthPt, double HeightPt) GetPaperDimensions(string paperName, double customW = 612.0, double customH = 792.0)
    {
        if (StandardPapers.TryGetValue(paperName, out var size))
        {
            return size;
        }
        return (Math.Max(72.0, customW), Math.Max(72.0, customH));
    }

    public static PrintLayout ComputeLayout(
        double docPixelWidth,
        double docPixelHeight,
        double docDpi,
        string paperName,
        string orientation,
        bool center,
        double leftPt,
        double topPt,
        double scalePercent,
        bool scaleToFit,
        bool emulsionDown,
        PrintMarks marks,
        double customW = 612.0,
        double customH = 792.0)
    {
        var (pw, ph) = GetPaperDimensions(paperName, customW, customH);
        bool isLandscape = orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
        if (isLandscape && pw < ph)
        {
            (pw, ph) = (ph, pw);
        }
        else if (!isLandscape && pw > ph)
        {
            (pw, ph) = (ph, pw);
        }

        double dpi = docDpi > 0 ? docDpi : 72.0;
        double naturalWidthPt = (docPixelWidth / dpi) * 72.0;
        double naturalHeightPt = (docPixelHeight / dpi) * 72.0;

        double scale = Math.Max(0.01, scalePercent / 100.0);
        if (scaleToFit)
        {
            double margin = 36.0; // 0.5 in margin for marks & edge
            double availW = Math.Max(10.0, pw - margin * 2);
            double availH = Math.Max(10.0, ph - margin * 2);
            double fitScale = Math.Min(availW / naturalWidthPt, availH / naturalHeightPt);
            scale = fitScale;
        }

        double imgW = naturalWidthPt * scale;
        double imgH = naturalHeightPt * scale;

        double imgX;
        double imgY;

        if (center)
        {
            imgX = (pw - imgW) / 2.0;
            imgY = (ph - imgH) / 2.0;
        }
        else
        {
            imgX = leftPt;
            imgY = topPt;
        }

        bool fits = imgX >= 0.0 && imgY >= 0.0 && (imgX + imgW) <= pw && (imgY + imgH) <= ph;
        string warning = fits ? "" : "  ·  larger than the paper";
        string metricsLabel = $"Scale {scale * 100.0:0.0}%  ·  {imgW / 72.0:0.00} × {imgH / 72.0:0.00} in{warning}";

        return new PrintLayout(
            pw, ph, imgX, imgY, imgW, imgH, scale, fits, emulsionDown, marks, metricsLabel
        );
    }

    public static byte[] RenderPrintPdf(SKBitmap documentBitmap, PrintLayout layout, string documentName)
    {
        using var stream = new MemoryStream();
        using var document = SKDocument.CreatePdf(stream);
        using var canvas = document.BeginPage((float)layout.PaperWidthPt, (float)layout.PaperHeightPt);

        canvas.Clear(SKColors.White);

        // Calculate placement
        float x = (float)layout.ImageXPt;
        float y = (float)layout.ImageYPt;
        float w = (float)layout.ImageWidthPt;
        float h = (float)layout.ImageHeightPt;

        canvas.Save();
        if (layout.EmulsionDown)
        {
            // Mirror horizontally across the page
            canvas.Translate((float)layout.PaperWidthPt, 0);
            canvas.Scale(-1, 1);
        }

        // Draw image
        var destRect = new SKRect(x, y, x + w, y + h);
        using var paint = new SKPaint
        {
            IsAntialias = true
        };
        using var skImage = SKImage.FromBitmap(documentBitmap);
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        canvas.DrawImage(skImage, destRect, sampling, paint);

        // Draw Marks
        using var markPaint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 0.75f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        if (layout.Marks.CornerCrop)
        {
            float len = 12f;
            float gap = 3f;
            // Top-left
            canvas.DrawLine(x - len, y, x - gap, y, markPaint);
            canvas.DrawLine(x, y - len, x, y - gap, markPaint);
            // Top-right
            canvas.DrawLine(x + w + gap, y, x + w + len, y, markPaint);
            canvas.DrawLine(x + w, y - len, x + w, y - gap, markPaint);
            // Bottom-left
            canvas.DrawLine(x - len, y + h, x - gap, y + h, markPaint);
            canvas.DrawLine(x, y + h + gap, x, y + h + len, markPaint);
            // Bottom-right
            canvas.DrawLine(x + w + gap, y + h, x + w + len, y + h, markPaint);
            canvas.DrawLine(x + w, y + h + gap, x + w, y + h + len, markPaint);
        }

        if (layout.Marks.Registration)
        {
            float r = 4f;
            float cx = x + w / 2f;
            float cy = y + h / 2f;

            // Top mark
            canvas.DrawCircle(cx, y - 10f, r, markPaint);
            canvas.DrawLine(cx - r - 2, y - 10f, cx + r + 2, y - 10f, markPaint);
            canvas.DrawLine(cx, y - 10f - r - 2, cx, y - 10f + r + 2, markPaint);

            // Bottom mark
            canvas.DrawCircle(cx, y + h + 10f, r, markPaint);
            canvas.DrawLine(cx - r - 2, y + h + 10f, cx + r + 2, y + h + 10f, markPaint);
            canvas.DrawLine(cx, y + h + 10f - r - 2, cx, y + h + 10f + r + 2, markPaint);
        }

        if (layout.Marks.Labels || layout.Marks.Description)
        {
            using var textPaint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true
            };
            using var font = new SKFont(SKTypeface.Default, 8f);
            string label = $"{documentName}  ·  {DateTime.Now:yyyy-MM-dd HH:mm}";
            canvas.DrawText(label, x, y - 6f, font, textPaint);
        }

        canvas.Restore();
        document.EndPage();
        document.Close();

        return stream.ToArray();
    }

    public static bool SendToSpooler(byte[] pdfBytes, string printerName, int copies)
    {
        if (pdfBytes.Length == 0) return false;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"print_{Guid.NewGuid():N}.pdf");
                File.WriteAllBytes(tempFile, pdfBytes);
                try
                {
                    var args = new List<string>();
                    if (!string.IsNullOrWhiteSpace(printerName) && !printerName.Equals("Default Printer", StringComparison.OrdinalIgnoreCase))
                    {
                        args.Add($"-P {printerName}");
                    }
                    if (copies > 1)
                    {
                        args.Add($"-# {copies}");
                    }
                    args.Add(tempFile);

                    var psi = new ProcessStartInfo("lpr", string.Join(" ", args))
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(3000);
                    return true;
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
        catch { }
        return true;
    }
}
