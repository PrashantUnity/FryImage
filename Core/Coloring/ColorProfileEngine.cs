using System;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Coloring;

/// <summary>
/// Industry-compatible color profile definitions and working gray curves.
/// Includes Calibrated Studio default Dot Gain 20%-compatible gray profile (#4610).
/// </summary>
public static class ColorProfileEngine
{
    public const string DefaultWorkingGray = "Dot Gain 20%-compatible (gamma 1.732, D50)";

    /// <summary>
    /// Converts an 8-bit sRGB color component to linear light (0.0 .. 1.0).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static double SrgbToLinear(byte c)
    {
        double v = c / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// Converts RGB into Dot Gain 20%-compatible grayscale value (gamma 1.732, D50 luminance).
    /// Matches Standard default Gray working space: sRGB 64/128/192 -> 46/105/177, pure red -> 107 (#4610).
    /// </summary>
    public static byte ConvertRgbToDotGain20Gray(byte r, byte g, byte b)
    {
        double rLin = SrgbToLinear(r);
        double gLin = SrgbToLinear(g);
        double bLin = SrgbToLinear(b);

        // D50 luminance adapted from sRGB primaries
        double yD50 = 0.22248840 * rLin + 0.71690369 * gLin + 0.06060791 * bLin;

        // Encode with gamma 1.732 (Dot Gain 20% curve)
        double gray = Math.Pow(Math.Clamp(yD50, 0.0, 1.0), 1.0 / 1.732);
        return (byte)Math.Clamp(Math.Round(gray * 255.0), 0, 255);
    }

    /// <summary>
    /// Lists only profiles compatible with the document's color mode (Studio Parity c72dda19, #4951, #5020).
    /// Prevents cross-space profile mismatches (e.g. assigning CMYK profile to RGB document).
    /// </summary>
    public static string[] GetAllowedProfilesForColorMode(string? colorMode)
    {
        string norm = (colorMode ?? "RGB").Trim().ToUpperInvariant();
        return norm switch
        {
            "CMYK" => ["working", "U.S. Web Coated (SWOP) v2", "Coated FOGRA39", "Japan Color 2001 Coated", "Generic CMYK", "none"],
            "GRAYSCALE" or "GRAY" => ["working", "Dot Gain 20%", "Gray Gamma 2.2", "Gray Gamma 1.8", "none"],
            "LAB" => ["working", "CIELAB D50", "none"],
            _ => ["working", "sRGB IEC61966-2.1", "Display P3", "Wide Gamut RGB (1998)", "ProPhoto RGB", "Apple RGB", "none"]
        };
    }
}
