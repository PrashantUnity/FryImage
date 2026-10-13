using System;
using System.Globalization;
using Color = Avalonia.Media.Color;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Coloring;

/// <summary>
/// Ported color conversion math from FryImage (crates/color).
/// Provides high-precision transforms between sRGB, HSL, HSV, CMYK, and Hex.
/// </summary>
public static class ColorConversions
{
    public static (double H, double S, double L) RgbToHsl(Color c)
    {
        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double l = (max + min) / 2.0;
        double h = 0.0;
        double s = 0.0;

        if (delta > 1e-5)
        {
            s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

            if (Math.Abs(max - r) < 1e-5)
                h = ((g - b) / delta) + (g < b ? 6.0 : 0.0);
            else if (Math.Abs(max - g) < 1e-5)
                h = ((b - r) / delta) + 2.0;
            else
                h = ((r - g) / delta) + 4.0;

            h /= 6.0;
        }

        return (h * 360.0, s, l);
    }

    public static Color HslToRgb(double h, double s, double l, byte alpha = 255)
    {
        h = ((h % 360.0) + 360.0) % 360.0 / 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        l = Math.Clamp(l, 0.0, 1.0);

        if (s < 1e-5)
        {
            byte v = (byte)Math.Round(l * 255.0);
            return Color.FromArgb(alpha, v, v, v);
        }

        double q = l < 0.5 ? l * (1.0 + s) : l + s - (l * s);
        double p = (2.0 * l) - q;

        double r = HueToRgb(p, q, h + (1.0 / 3.0));
        double g = HueToRgb(p, q, h);
        double b = HueToRgb(p, q, h - (1.0 / 3.0));

        return Color.FromArgb(
            alpha,
            (byte)Math.Round(Math.Clamp(r * 255.0, 0, 255)),
            (byte)Math.Round(Math.Clamp(g * 255.0, 0, 255)),
            (byte)Math.Round(Math.Clamp(b * 255.0, 0, 255)));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0.0) t += 1.0;
        if (t > 1.0) t -= 1.0;
        if (t < 1.0 / 6.0) return p + ((q - p) * 6.0 * t);
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + ((q - p) * ((2.0 / 3.0) - t) * 6.0);
        return p;
    }

    public static (double H, double S, double V) RgbToHsv(Color c)
    {
        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0.0;
        double s = max < 1e-5 ? 0.0 : delta / max;
        double v = max;

        if (delta > 1e-5)
        {
            if (Math.Abs(max - r) < 1e-5)
                h = ((g - b) / delta) + (g < b ? 6.0 : 0.0);
            else if (Math.Abs(max - g) < 1e-5)
                h = ((b - r) / delta) + 2.0;
            else
                h = ((r - g) / delta) + 4.0;

            h /= 6.0;
        }

        return (h * 360.0, s, v);
    }

    public static Color HsvToRgb(double h, double s, double v, byte alpha = 255)
    {
        h = ((h % 360.0) + 360.0) % 360.0 / 60.0;
        s = Math.Clamp(s, 0.0, 1.0);
        v = Math.Clamp(v, 0.0, 1.0);

        int i = (int)Math.Floor(h);
        double f = h - i;
        double p = v * (1.0 - s);
        double q = v * (1.0 - (s * f));
        double t = v * (1.0 - (s * (1.0 - f)));

        double r = 0, g = 0, b = 0;
        switch (i % 6)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            case 5: r = v; g = p; b = q; break;
        }

        return Color.FromArgb(
            alpha,
            (byte)Math.Round(Math.Clamp(r * 255.0, 0, 255)),
            (byte)Math.Round(Math.Clamp(g * 255.0, 0, 255)),
            (byte)Math.Round(Math.Clamp(b * 255.0, 0, 255)));
    }

    public static string ToHexRgb(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static bool TryParseHex(string? hex, out Color color)
    {
        color = Color.FromArgb(255, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(hex)) return false;

        var clean = hex.Trim().TrimStart('#');
        if (clean.Length == 6 &&
            uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);
            color = Color.FromArgb(255, r, g, b);
            return true;
        }
        if (clean.Length == 8 &&
            uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >> 8) & 0xFF);
            byte b = (byte)(argb & 0xFF);
            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        return false;
    }

    public static (double C, double M, double Y, double K) RgbToCmyk(Color c)
    {
        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;
        double k = 1.0 - Math.Max(r, Math.Max(g, b));
        if (k >= 0.9999) return (0.0, 0.0, 0.0, 1.0);
        double cmykC = (1.0 - r - k) / (1.0 - k);
        double cmykM = (1.0 - g - k) / (1.0 - k);
        double cmykY = (1.0 - b - k) / (1.0 - k);
        return (Math.Clamp(cmykC, 0.0, 1.0), Math.Clamp(cmykM, 0.0, 1.0), Math.Clamp(cmykY, 0.0, 1.0), Math.Clamp(k, 0.0, 1.0));
    }

    public static Color CmykToRgb(double c, double m, double y, double k, byte alpha = 255)
    {
        c = Math.Clamp(c, 0.0, 1.0);
        m = Math.Clamp(m, 0.0, 1.0);
        y = Math.Clamp(y, 0.0, 1.0);
        k = Math.Clamp(k, 0.0, 1.0);

        double r = 255.0 * (1.0 - c) * (1.0 - k);
        double g = 255.0 * (1.0 - m) * (1.0 - k);
        double b = 255.0 * (1.0 - y) * (1.0 - k);

        return Color.FromArgb(
            alpha,
            (byte)Math.Round(Math.Clamp(r, 0, 255)),
            (byte)Math.Round(Math.Clamp(g, 0, 255)),
            (byte)Math.Round(Math.Clamp(b, 0, 255)));
    }

    public static Color SnapToWebSafe(Color c)
    {
        // Web-safe colors only use RGB multiples of 51 (0x00, 0x33, 0x66, 0x99, 0xCC, 0xFF)
        byte r = (byte)(Math.Round(c.R / 51.0) * 51);
        byte g = (byte)(Math.Round(c.G / 51.0) * 51);
        byte b = (byte)(Math.Round(c.B / 51.0) * 51);
        return Color.FromArgb(c.A, r, g, b);
    }

    /// <summary>sRGB electro-optical transfer function (linear light to sRGB gamma).</summary>
    public static double LinearToSrgb(double v)
    {
        v = Math.Max(0.0, v);
        return v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
    }

    /// <summary>sRGB opto-electronic transfer function (sRGB gamma to linear light).</summary>
    public static double SrgbToLinear(double v)
    {
        v = Math.Max(0.0, v);
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    /// <summary>Standard Rec. 601 luma calculation (Photoshop standard for blend modes).</summary>
    public static double Rec601Luma(double r, double g, double b) => 0.299 * r + 0.587 * g + 0.114 * b;

    /// <summary>
    /// Clips an RGB triple back into the gamut [0..top] while preserving luminosity.
    /// </summary>
    public static (double R, double G, double B) ClipColor(double r, double g, double b, double top = 1.0)
    {
        double l = Rec601Luma(r, g, b);
        double n = Math.Min(r, Math.Min(g, b));
        double x = Math.Max(r, Math.Max(g, b));

        if (n < 0.0)
        {
            double d = l - n;
            r = l > 0.0 && d > 0.0 ? l + (r - l) * l / d : l;
            g = l > 0.0 && d > 0.0 ? l + (g - l) * l / d : l;
            b = l > 0.0 && d > 0.0 ? l + (b - l) * l / d : l;
        }

        if (x > top)
        {
            double d = x - l;
            r = l < top && d > 0.0 ? l + (r - l) * (top - l) / d : l;
            g = l < top && d > 0.0 ? l + (g - l) * (top - l) / d : l;
            b = l < top && d > 0.0 ? l + (b - l) * (top - l) / d : l;
        }

        return (r, g, b);
    }

    /// <summary>
    /// Implements Photoshop Color blend mode luminosity adjustment: SetLum(c, l).
    /// </summary>
    public static (double R, double G, double B) SetLum(double r, double g, double b, double l, double top = 1.0)
    {
        double d = l - Rec601Luma(r, g, b);
        return ClipColor(r + d, g + d, b + d, top);
    }

    /// <summary>
    /// D50 CIE XYZ to linear sRGB matrix (Photoshop ICC D50 specification).
    /// </summary>
    private static readonly double[,] XyzD50ToSrgbMatrix =
    {
        {  3.133856, -1.616867, -0.490615 },
        { -0.978768,  1.916142,  0.033454 },
        {  0.071945, -0.228991,  1.405243 }
    };

    /// <summary>
    /// Converts D50 CIE XYZ color (normalized 0..1) to standard sRGB gamma [0..255].
    /// </summary>
    public static Color XyzD50ToSrgb(double x, double y, double z, byte alpha = 255)
    {
        double linR = XyzD50ToSrgbMatrix[0, 0] * x + XyzD50ToSrgbMatrix[0, 1] * y + XyzD50ToSrgbMatrix[0, 2] * z;
        double linG = XyzD50ToSrgbMatrix[1, 0] * x + XyzD50ToSrgbMatrix[1, 1] * y + XyzD50ToSrgbMatrix[1, 2] * z;
        double linB = XyzD50ToSrgbMatrix[2, 0] * x + XyzD50ToSrgbMatrix[2, 1] * y + XyzD50ToSrgbMatrix[2, 2] * z;

        double sR = LinearToSrgb(Math.Clamp(linR, 0.0, 1.0));
        double sG = LinearToSrgb(Math.Clamp(linG, 0.0, 1.0));
        double sB = LinearToSrgb(Math.Clamp(linB, 0.0, 1.0));

        return Color.FromArgb(
            alpha,
            (byte)Math.Clamp((int)Math.Round(sR * 255.0), 0, 255),
            (byte)Math.Clamp((int)Math.Round(sG * 255.0), 0, 255),
            (byte)Math.Clamp((int)Math.Round(sB * 255.0), 0, 255));
    }

    /// <summary>
    /// Converts Adobe RGB (1998) color to sRGB.
    /// </summary>
    public static Color AdobeRgbToSrgb(double r, double g, double b, byte alpha = 255)
    {
        // Adobe RGB gamma 2.19921875 (~2.2) to linear
        double linR = Math.Pow(Math.Clamp(r, 0.0, 1.0), 2.19921875);
        double linG = Math.Pow(Math.Clamp(g, 0.0, 1.0), 2.19921875);
        double linB = Math.Pow(Math.Clamp(b, 0.0, 1.0), 2.19921875);

        // Adobe RGB to sRGB matrix
        double sLinR =  1.3983633 * linR - 0.3983633 * linG - 0.0000000 * linB;
        double sLinG = -0.0000000 * linR + 1.0000000 * linG + 0.0000000 * linB;
        double sLinB = -0.0429443 * linR - 0.0000000 * linG + 1.0429443 * linB;

        double sR = LinearToSrgb(Math.Clamp(sLinR, 0.0, 1.0));
        double sG = LinearToSrgb(Math.Clamp(sLinG, 0.0, 1.0));
        double sB = LinearToSrgb(Math.Clamp(sLinB, 0.0, 1.0));

        return Color.FromArgb(
            alpha,
            (byte)Math.Clamp((int)Math.Round(sR * 255.0), 0, 255),
            (byte)Math.Clamp((int)Math.Round(sG * 255.0), 0, 255),
            (byte)Math.Clamp((int)Math.Round(sB * 255.0), 0, 255));
    }
}
