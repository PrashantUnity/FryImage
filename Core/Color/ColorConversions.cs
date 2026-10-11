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
}
