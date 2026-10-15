using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Styling;

/// <summary>
/// Built-in contour presets, in the order of Studio's default contour picker.
/// Each is Studio's own curve as measured in Calibrated Studio (a Satin of each preset against
/// a Linear one, 38 levels per curve, read back through Satin's mirrored lookup
/// |c(1 - v) - c(1)| and thinned to within 1%), joined by straight segments (every inner point a corner).
/// </summary>
public static class StudioContourLibrary
{
    public readonly record struct ContourPoint(float X, float Y, bool IsCorner);

    private static readonly Dictionary<string, ContourPoint[]> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Linear"] = new[]
        {
            new ContourPoint(0.0f, 0.0f, false),
            new ContourPoint(1.0f, 1.0f, false),
        },
        ["Cone"] = new[]
        {
            new ContourPoint(0.0f, 0.0f, false),
            new ContourPoint(0.172f, 0.477f, true),
            new ContourPoint(0.195f, 0.555f, true),
            new ContourPoint(0.266f, 0.711f, true),
            new ContourPoint(0.383f, 0.922f, true),
            new ContourPoint(0.430f, 0.969f, true),
            new ContourPoint(0.477f, 0.992f, true),
            new ContourPoint(0.523f, 0.992f, true),
            new ContourPoint(0.570f, 0.969f, true),
            new ContourPoint(0.664f, 0.859f, true),
            new ContourPoint(0.703f, 0.789f, true),
            new ContourPoint(0.742f, 0.711f, true),
            new ContourPoint(0.836f, 0.477f, true),
            new ContourPoint(0.859f, 0.398f, true),
            new ContourPoint(0.914f, 0.266f, true),
            new ContourPoint(0.945f, 0.156f, true),
            new ContourPoint(0.977f, 0.078f, true),
            new ContourPoint(1.0f, 0.0f, false),
        },
        ["Cone - Inverted"] = new[]
        {
            new ContourPoint(0.0f, 1.0f, false),
            new ContourPoint(0.031f, 0.922f, true),
            new ContourPoint(0.070f, 0.789f, true),
            new ContourPoint(0.172f, 0.523f, true),
            new ContourPoint(0.195f, 0.445f, true),
            new ContourPoint(0.266f, 0.289f, true),
            new ContourPoint(0.383f, 0.078f, true),
            new ContourPoint(0.430f, 0.031f, true),
            new ContourPoint(0.477f, 0.008f, true),
            new ContourPoint(0.523f, 0.008f, true),
            new ContourPoint(0.570f, 0.031f, true),
            new ContourPoint(0.664f, 0.141f, true),
            new ContourPoint(0.703f, 0.211f, true),
            new ContourPoint(0.742f, 0.289f, true),
            new ContourPoint(0.836f, 0.523f, true),
            new ContourPoint(0.859f, 0.602f, true),
            new ContourPoint(0.914f, 0.734f, true),
            new ContourPoint(0.945f, 0.844f, true),
            new ContourPoint(0.977f, 0.922f, true),
            new ContourPoint(1.0f, 1.0f, false),
        },
        ["Cove - Deep"] = new[]
        {
            new ContourPoint(0.0f, 0.258f, false),
            new ContourPoint(0.344f, 0.289f, true),
            new ContourPoint(0.383f, 0.297f, true),
            new ContourPoint(0.477f, 0.352f, true),
            new ContourPoint(0.570f, 0.430f, true),
            new ContourPoint(0.617f, 0.484f, true),
            new ContourPoint(0.664f, 0.570f, true),
            new ContourPoint(0.703f, 0.711f, true),
            new ContourPoint(0.742f, 0.906f, true),
            new ContourPoint(0.773f, 1.0f, true),
            new ContourPoint(1.0f, 1.0f, false),
        },
        ["Cove - Shallow"] = new[]
        {
            new ContourPoint(0.0f, 0.0f, false),
            new ContourPoint(0.008f, 0.164f, true),
            new ContourPoint(0.016f, 0.477f, true),
            new ContourPoint(0.039f, 0.508f, true),
            new ContourPoint(0.172f, 0.570f, true),
            new ContourPoint(0.305f, 0.688f, true),
            new ContourPoint(0.344f, 0.734f, true),
            new ContourPoint(0.477f, 0.945f, true),
            new ContourPoint(0.523f, 1.0f, true),
            new ContourPoint(1.0f, 1.0f, false),
        },
        ["Gaussian"] = new[]
        {
            new ContourPoint(0.0f, 0.0f, false),
            new ContourPoint(0.117f, 0.023f, true),
            new ContourPoint(0.172f, 0.055f, true),
            new ContourPoint(0.234f, 0.117f, true),
            new ContourPoint(0.305f, 0.234f, true),
            new ContourPoint(0.383f, 0.414f, true),
            new ContourPoint(0.523f, 0.688f, true),
            new ContourPoint(0.617f, 0.812f, true),
            new ContourPoint(0.703f, 0.891f, true),
            new ContourPoint(0.805f, 0.953f, true),
            new ContourPoint(1.0f, 1.0f, false),
        },
        ["Half Round"] = new[]
        {
            new ContourPoint(0.0f, 0.0f, false),
            new ContourPoint(0.070f, 0.180f, true),
            new ContourPoint(0.195f, 0.445f, true),
            new ContourPoint(0.266f, 0.555f, true),
            new ContourPoint(0.383f, 0.703f, true),
            new ContourPoint(0.523f, 0.828f, true),
            new ContourPoint(0.617f, 0.883f, true),
            new ContourPoint(0.773f, 0.945f, true),
            new ContourPoint(1.0f, 1.0f, false),
        },
        ["Ring"] = new[]
        {
            new ContourPoint(0.0f, 0.0f, false),
            new ContourPoint(0.031f, 0.078f, true),
            new ContourPoint(0.039f, 0.117f, true),
            new ContourPoint(0.117f, 0.383f, true),
            new ContourPoint(0.172f, 0.609f, true),
            new ContourPoint(0.195f, 0.734f, true),
            new ContourPoint(0.266f, 0.953f, true),
            new ContourPoint(0.305f, 1.0f, true),
            new ContourPoint(0.344f, 0.992f, true),
            new ContourPoint(0.383f, 0.922f, true),
            new ContourPoint(0.430f, 0.805f, true),
            new ContourPoint(0.477f, 0.641f, true),
            new ContourPoint(0.570f, 0.281f, true),
            new ContourPoint(0.617f, 0.141f, true),
            new ContourPoint(0.664f, 0.055f, true),
            new ContourPoint(0.703f, 0.016f, true),
            new ContourPoint(0.742f, 0.016f, true),
            new ContourPoint(0.773f, 0.062f, true),
            new ContourPoint(0.836f, 0.234f, true),
            new ContourPoint(0.859f, 0.344f, true),
            new ContourPoint(0.891f, 0.445f, true),
            new ContourPoint(0.914f, 0.547f, true),
            new ContourPoint(0.945f, 0.734f, true),
            new ContourPoint(0.977f, 0.875f, true),
            new ContourPoint(1.0f, 1.0f, false),
        },
        ["Ring - Double"] = new[]
        {
            new ContourPoint(0.0f, 0.0f, false),
            new ContourPoint(0.055f, 0.086f, true),
            new ContourPoint(0.094f, 0.172f, true),
            new ContourPoint(0.117f, 0.242f, true),
            new ContourPoint(0.172f, 0.477f, true),
            new ContourPoint(0.195f, 0.625f, true),
            new ContourPoint(0.266f, 0.891f, true),
            new ContourPoint(0.305f, 0.945f, true),
            new ContourPoint(0.344f, 0.891f, true),
            new ContourPoint(0.383f, 0.695f, true),
            new ContourPoint(0.430f, 0.367f, true),
            new ContourPoint(0.477f, 0.070f, true),
            new ContourPoint(0.523f, 0.047f, true),
            new ContourPoint(0.570f, 0.320f, true),
            new ContourPoint(0.617f, 0.648f, true),
            new ContourPoint(0.664f, 0.867f, true),
            new ContourPoint(0.703f, 0.930f, true),
            new ContourPoint(0.742f, 0.852f, true),
            new ContourPoint(0.773f, 0.641f, true),
            new ContourPoint(0.805f, 0.375f, true),
            new ContourPoint(0.836f, 0.156f, true),
            new ContourPoint(0.859f, 0.031f, true),
            new ContourPoint(0.891f, 0.023f, true),
            new ContourPoint(0.914f, 0.109f, true),
            new ContourPoint(0.977f, 0.703f, true),
            new ContourPoint(0.984f, 0.820f, true),
            new ContourPoint(1.0f, 1.0f, false),
        },
        ["Rolling Slope - Descending"] = new[]
        {
            new ContourPoint(0.0f, 1.0f, false),
            new ContourPoint(0.008f, 1.0f, true),
            new ContourPoint(0.031f, 0.930f, true),
            new ContourPoint(0.039f, 0.883f, true),
            new ContourPoint(0.070f, 0.758f, true),
            new ContourPoint(0.141f, 0.539f, true),
            new ContourPoint(0.195f, 0.406f, true),
            new ContourPoint(0.266f, 0.352f, true),
            new ContourPoint(0.305f, 0.352f, true),
            new ContourPoint(0.523f, 0.570f, true),
            new ContourPoint(0.570f, 0.602f, true),
            new ContourPoint(0.617f, 0.617f, true),
            new ContourPoint(0.703f, 0.602f, true),
            new ContourPoint(0.742f, 0.562f, true),
            new ContourPoint(0.836f, 0.391f, true),
            new ContourPoint(0.914f, 0.211f, true),
            new ContourPoint(0.945f, 0.109f, true),
            new ContourPoint(0.977f, 0.039f, true),
            new ContourPoint(0.984f, 0.008f, true),
            new ContourPoint(1.0f, 0.0f, false),
        },
        ["Rounded Steps"] = new[]
        {
            new ContourPoint(0.0f, 0.234f, false),
            new ContourPoint(0.031f, 0.305f, true),
            new ContourPoint(0.039f, 0.336f, true),
            new ContourPoint(0.094f, 0.430f, true),
            new ContourPoint(0.141f, 0.477f, true),
            new ContourPoint(0.234f, 0.492f, true),
            new ContourPoint(0.266f, 0.516f, true),
            new ContourPoint(0.305f, 0.602f, true),
            new ContourPoint(0.344f, 0.664f, true),
            new ContourPoint(0.383f, 0.711f, true),
            new ContourPoint(0.477f, 0.734f, true),
            new ContourPoint(0.523f, 0.797f, true),
            new ContourPoint(0.570f, 0.898f, true),
            new ContourPoint(0.617f, 0.953f, true),
            new ContourPoint(0.664f, 0.984f, true),
            new ContourPoint(0.773f, 0.984f, true),
            new ContourPoint(0.836f, 0.906f, true),
            new ContourPoint(0.891f, 0.891f, true),
            new ContourPoint(0.914f, 0.898f, true),
            new ContourPoint(1.0f, 1.0f, false),
        },
        ["Sawtooth 1"] = new[]
        {
            new ContourPoint(0.0f, 0.0f, false),
            new ContourPoint(0.031f, 0.047f, true),
            new ContourPoint(0.195f, 0.375f, true),
            new ContourPoint(0.266f, 0.492f, true),
            new ContourPoint(0.477f, 0.047f, true),
            new ContourPoint(0.523f, 0.055f, true),
            new ContourPoint(0.742f, 0.469f, true),
            new ContourPoint(0.773f, 0.445f, true),
            new ContourPoint(0.914f, 0.180f, true),
            new ContourPoint(0.945f, 0.102f, true),
            new ContourPoint(1.0f, 0.0f, false),
        },
    };

    public static IReadOnlyList<string> PresetNames => Presets.Keys.ToList();

    public static ContourPoint[]? GetPreset(string name)
    {
        if (Presets.TryGetValue(name, out var pts)) return pts;
        return Presets["Linear"];
    }

    /// <summary>
    /// Builds a discrete 0..1 lookup table (LUT) for the given contour preset.
    /// </summary>
    public static float[] GetContourLut(string name, int steps = 256)
    {
        var pts = GetPreset(name) ?? Presets["Linear"];
        float[] lut = new float[Math.Max(2, steps)];
        for (int i = 0; i < lut.Length; i++)
        {
            float t = (float)i / (lut.Length - 1);
            lut[i] = EvaluatePoints(pts, t);
        }
        return lut;
    }

    /// <summary>
    /// Satin's coverage transfer: the contour read from its far end, `|c(1 - v) - c(1)|`.
    /// Measured in Calibrated Studio: Cone and Cone - Inverted Satins are identical, and
    /// a Gaussian Satin runs under the diagonal (0.36 at 0.5) while the same Gaussian
    /// on a glow or gloss runs over it (0.64).
    /// </summary>
    public static float[] GetSatinLut(string name, int steps = 256)
    {
        float[] baseLut = GetContourLut(name, steps);
        float end = baseLut[^1];
        float[] satin = new float[baseLut.Length];
        for (int i = 0; i < baseLut.Length; i++)
        {
            float v = baseLut[baseLut.Length - 1 - i];
            satin[i] = Math.Abs(v - end);
        }
        return satin;
    }

    /// <summary>
    /// Piecewise linear evaluation of contour points at t in [0..1].
    /// </summary>
    public static float EvaluatePoints(ContourPoint[] points, float t)
    {
        if (points.Length == 0) return t;
        if (t <= points[0].X) return points[0].Y;
        if (t >= points[^1].X) return points[^1].Y;

        for (int i = 0; i < points.Length - 1; i++)
        {
            var p0 = points[i];
            var p1 = points[i + 1];
            if (t >= p0.X && t <= p1.X)
            {
                float span = p1.X - p0.X;
                if (span <= 1e-6f) return p1.Y;
                float frac = (t - p0.X) / span;
                return p0.Y + (p1.Y - p0.Y) * frac;
            }
        }
        return points[^1].Y;
    }

    public static float Evaluate(string name, float t)
    {
        var pts = GetPreset(name) ?? Presets["Linear"];
        return EvaluatePoints(pts, Math.Clamp(t, 0.0f, 1.0f));
    }

    public static float EvaluateSatin(string name, float t)
    {
        var pts = GetPreset(name) ?? Presets["Linear"];
        float end = pts[^1].Y;
        float v = EvaluatePoints(pts, Math.Clamp(1.0f - t, 0.0f, 1.0f));
        return Math.Abs(v - end);
    }
}
