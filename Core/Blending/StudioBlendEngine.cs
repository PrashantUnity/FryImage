using System;
using Avalonia.Media;
using Color = Avalonia.Media.Color;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Blending;

/// <summary>
/// FryImage pixel-level layer blend engine implementing the complete set of 27 standard Studio
/// / ISO 32000-1 blend modes for professional compositing.
/// Includes separable and non-separable (HSL, luminance comparator) blend equations.
/// </summary>
public static class StudioBlendEngine
{
    private const float Edge = 1e-4f;

    public static Color BlendPixel(Color backdrop, Color source, LayerBlendMode mode, double opacity = 1.0)
    {
        float cbR = backdrop.R / 255.0f;
        float cbG = backdrop.G / 255.0f;
        float cbB = backdrop.B / 255.0f;
        float cbA = backdrop.A / 255.0f;

        float csR = source.R / 255.0f;
        float csG = source.G / 255.0f;
        float csB = source.B / 255.0f;
        float effectiveAlpha = (source.A / 255.0f) * (float)Math.Clamp(opacity, 0.0, 1.0);

        if (effectiveAlpha <= 1e-5f) return backdrop;
        if (cbA <= 1e-5f)
        {
            return Color.FromArgb((byte)Math.Round(effectiveAlpha * 255.0f), source.R, source.G, source.B);
        }

        // Dissolve mode: pseudo-random noise threshold
        if (mode == LayerBlendMode.Dissolve)
        {
            uint seed = (uint)(backdrop.R * 73856093 ^ backdrop.G * 19349663 ^ backdrop.B * 83492791 ^ source.R * 2654435761u);
            float rnd = (seed & 0xFFFF) / 65535.0f;
            if (rnd > effectiveAlpha) return backdrop;
            return source;
        }

        float blendedR, blendedG, blendedB;

        // Check for non-separable blend modes
        if (IsNonSeparable(mode))
        {
            (blendedR, blendedG, blendedB) = BlendNonSeparable(cbR, cbG, cbB, csR, csG, csB, mode);
        }
        else
        {
            blendedR = BlendComponent(cbR, csR, mode);
            blendedG = BlendComponent(cbG, csG, mode);
            blendedB = BlendComponent(cbB, csB, mode);
        }

        // Standard compositing equation: out = Cs * as + Cb * ab * (1 - as)
        float outA = effectiveAlpha + cbA * (1.0f - effectiveAlpha);
        float outR = (effectiveAlpha * blendedR + cbA * cbR * (1.0f - effectiveAlpha)) / Math.Max(1e-5f, outA);
        float outG = (effectiveAlpha * blendedG + cbA * cbG * (1.0f - effectiveAlpha)) / Math.Max(1e-5f, outA);
        float outB = (effectiveAlpha * blendedB + cbA * cbB * (1.0f - effectiveAlpha)) / Math.Max(1e-5f, outA);

        return Color.FromArgb(
            (byte)Math.Round(Math.Clamp(outA * 255.0f, 0, 255)),
            (byte)Math.Round(Math.Clamp(outR * 255.0f, 0, 255)),
            (byte)Math.Round(Math.Clamp(outG * 255.0f, 0, 255)),
            (byte)Math.Round(Math.Clamp(outB * 255.0f, 0, 255)));
    }

    private static bool IsNonSeparable(LayerBlendMode mode) => mode is
        LayerBlendMode.DarkerColor or
        LayerBlendMode.LighterColor or
        LayerBlendMode.Hue or
        LayerBlendMode.Saturation or
        LayerBlendMode.Color or
        LayerBlendMode.Luminosity;

    public static float BlendComponent(float cb, float cs, LayerBlendMode mode) => mode switch
    {
        LayerBlendMode.Normal => cs,
        LayerBlendMode.Multiply => cb * cs,
        LayerBlendMode.Screen => 1.0f - (1.0f - cb) * (1.0f - cs),
        LayerBlendMode.Overlay => cb < 0.5f ? 2.0f * cb * cs : 1.0f - 2.0f * (1.0f - cb) * (1.0f - cs),
        LayerBlendMode.Darken => Math.Min(cb, cs),
        LayerBlendMode.Lighten => Math.Max(cb, cs),
        LayerBlendMode.ColorDodge => ColorDodge(cb, cs),
        LayerBlendMode.ColorBurn => ColorBurn(cb, cs),
        LayerBlendMode.HardLight => cs < 0.5f ? 2.0f * cb * cs : 1.0f - 2.0f * (1.0f - cb) * (1.0f - cs),
        LayerBlendMode.SoftLight => SoftLight(cb, cs),
        LayerBlendMode.Difference => Math.Abs(cb - cs),
        LayerBlendMode.Exclusion => cb + cs - 2.0f * cb * cs,
        LayerBlendMode.LinearBurn => Math.Max(0.0f, cb + cs - 1.0f),
        LayerBlendMode.LinearDodge => Math.Min(1.0f, cb + cs),
        LayerBlendMode.VividLight => VividLight(cb, cs),
        LayerBlendMode.LinearLight => LinearLight(cb, cs),
        LayerBlendMode.PinLight => PinLight(cb, cs),
        LayerBlendMode.HardMix => HardMix(cb, cs),
        LayerBlendMode.Subtract => Math.Max(0.0f, cb - cs),
        LayerBlendMode.Divide => Divide(cb, cs),
        _ => cs
    };

    private static float ColorDodge(float cb, float cs)
    {
        if (cb <= Edge) return 0.0f;
        if (cs >= 1.0f) return 1.0f;
        return Math.Min(1.0f, cb / (1.0f - cs));
    }

    private static float ColorBurn(float cb, float cs)
    {
        if (cb >= 1.0f - Edge) return 1.0f;
        if (cs <= 0.0f) return 0.0f;
        return 1.0f - Math.Min(1.0f, (1.0f - cb) / cs);
    }

    private static float SoftLight(float cb, float cs)
    {
        if (cs <= 0.5f)
        {
            return cb - (1.0f - 2.0f * cs) * cb * (1.0f - cb);
        }

        float d = cb <= 0.25f
            ? ((16.0f * cb - 12.0f) * cb + 4.0f) * cb
            : MathF.Sqrt(cb);
        return cb + (2.0f * cs - 1.0f) * (d - cb);
    }

    private static float VividLight(float cb, float cs)
    {
        if (cs <= 0.5f)
        {
            if (cs <= 0.0f) return 0.0f;
            return 1.0f - Math.Min(1.0f, (1.0f - cb) / (2.0f * cs));
        }

        if (cs >= 1.0f) return 1.0f;
        return Math.Min(1.0f, cb / (2.0f * (1.0f - cs)));
    }

    private static float LinearLight(float cb, float cs)
    {
        return Math.Clamp(cb + 2.0f * cs - 1.0f, 0.0f, 1.0f);
    }

    private static float PinLight(float cb, float cs)
    {
        if (cs <= 0.5f) return Math.Min(cb, 2.0f * cs);
        return Math.Max(cb, 2.0f * (cs - 0.5f));
    }

    private static float HardMix(float cb, float cs)
    {
        return LinearLight(cb, cs) >= 0.5f ? 1.0f : 0.0f;
    }

    private static float Divide(float cb, float cs)
    {
        if (cs <= 0.0f) return 1.0f;
        return Math.Min(1.0f, cb / cs);
    }

    #region Non-Separable HSL & Luminance Blending

    private static float Lum(float r, float g, float b) => 0.3f * r + 0.59f * g + 0.11f * b;

    private static float Sat(float r, float g, float b) => Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));

    private static (float R, float G, float B) ClipColor(float r, float g, float b)
    {
        float l = Lum(r, g, b);
        float n = Math.Min(r, Math.Min(g, b));
        float x = Math.Max(r, Math.Max(g, b));

        if (n < 0.0f)
        {
            r = l + (((r - l) * l) / (l - n));
            g = l + (((g - l) * l) / (l - n));
            b = l + (((b - l) * l) / (l - n));
        }

        if (x > 1.0f)
        {
            r = l + (((r - l) * (1.0f - l)) / (x - l));
            g = l + (((g - l) * (1.0f - l)) / (x - l));
            b = l + (((b - l) * (1.0f - l)) / (x - l));
        }

        return (r, g, b);
    }

    private static (float R, float G, float B) SetLum(float r, float g, float b, float l)
    {
        float d = l - Lum(r, g, b);
        return ClipColor(r + d, g + d, b + d);
    }

    private static (float R, float G, float B) SetSat(float r, float g, float b, float s)
    {
        float[] c = { r, g, b };
        int minIdx = 0, midIdx = 1, maxIdx = 2;
        if (c[minIdx] > c[midIdx]) (minIdx, midIdx) = (midIdx, minIdx);
        if (c[midIdx] > c[maxIdx]) (midIdx, maxIdx) = (maxIdx, midIdx);
        if (c[minIdx] > c[midIdx]) (minIdx, midIdx) = (midIdx, minIdx);

        if (c[maxIdx] > c[minIdx])
        {
            c[midIdx] = (((c[midIdx] - c[minIdx]) * s) / (c[maxIdx] - c[minIdx]));
            c[maxIdx] = s;
        }
        else
        {
            c[midIdx] = 0.0f;
            c[maxIdx] = 0.0f;
        }
        c[minIdx] = 0.0f;

        float[] result = new float[3];
        result[minIdx] = c[minIdx];
        result[midIdx] = c[midIdx];
        result[maxIdx] = c[maxIdx];
        return (result[0], result[1], result[2]);
    }

    private static (float R, float G, float B) BlendNonSeparable(
        float cbR, float cbG, float cbB,
        float csR, float csG, float csB,
        LayerBlendMode mode)
    {
        switch (mode)
        {
            case LayerBlendMode.DarkerColor:
                return Lum(csR, csG, csB) < Lum(cbR, cbG, cbB)
                    ? (csR, csG, csB)
                    : (cbR, cbG, cbB);

            case LayerBlendMode.LighterColor:
                return Lum(csR, csG, csB) > Lum(cbR, cbG, cbB)
                    ? (csR, csG, csB)
                    : (cbR, cbG, cbB);

            case LayerBlendMode.Hue:
            {
                var (r, g, b) = SetSat(csR, csG, csB, Sat(cbR, cbG, cbB));
                return SetLum(r, g, b, Lum(cbR, cbG, cbB));
            }

            case LayerBlendMode.Saturation:
            {
                var (r, g, b) = SetSat(cbR, cbG, cbB, Sat(csR, csG, csB));
                return SetLum(r, g, b, Lum(cbR, cbG, cbB));
            }

            case LayerBlendMode.Color:
                return SetLum(csR, csG, csB, Lum(cbR, cbG, cbB));

            case LayerBlendMode.Luminosity:
                return SetLum(cbR, cbG, cbB, Lum(csR, csG, csB));

            default:
                return (csR, csG, csB);
        }
    }

    /// <summary>
    /// Computes the Blend If visibility factor (0.0 .. 1.0) for a given pixel value against a range
    /// defined by [blackLow, blackHigh] (ramp up from black) and [whiteLow, whiteHigh] (ramp down into white).
    /// Matches ISO/IEC Blend If specifications (#4395).
    /// </summary>
    public static float ComputeBlendRangeFactor(byte value, byte blackLow, byte blackHigh, byte whiteLow, byte whiteHigh)
    {
        if (value < blackLow || value > whiteHigh) return 0.0f;
        if (value >= blackHigh && value <= whiteLow) return 1.0f;

        if (value < blackHigh)
        {
            float span = blackHigh - blackLow;
            return span <= 0 ? 1.0f : (value - blackLow) / span;
        }

        if (value > whiteLow)
        {
            float span = whiteHigh - whiteLow;
            return span <= 0 ? 1.0f : (whiteHigh - value) / span;
        }

        return 1.0f;
    }

    /// <summary>
    /// Computes overall Blend If visibility factor combining This Layer and Underlying Layer ranges (#4395).
    /// </summary>
    public static float EvaluateBlendIf(
        Color thisColor, Color underColor,
        byte thisBlackLow = 0, byte thisBlackHigh = 0, byte thisWhiteLow = 255, byte thisWhiteHigh = 255,
        byte underBlackLow = 0, byte underBlackHigh = 0, byte underWhiteLow = 255, byte underWhiteHigh = 255)
    {
        byte thisLum = (byte)Math.Round(0.299f * thisColor.R + 0.587f * thisColor.G + 0.114f * thisColor.B);
        byte underLum = (byte)Math.Round(0.299f * underColor.R + 0.587f * underColor.G + 0.114f * underColor.B);

        float thisFactor = ComputeBlendRangeFactor(thisLum, thisBlackLow, thisBlackHigh, thisWhiteLow, thisWhiteHigh);
        if (thisFactor <= 0.0f) return 0.0f;

        float underFactor = ComputeBlendRangeFactor(underLum, underBlackLow, underBlackHigh, underWhiteLow, underWhiteHigh);
        return thisFactor * underFactor;
    }

    #endregion
}
