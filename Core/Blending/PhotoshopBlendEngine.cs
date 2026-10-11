using Avalonia.Media;
using Color = Avalonia.Media.Color;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Blending;

/// <summary>
/// Exact Photoshop / FryImage pixel-level blend functions ported from
/// FryImage compose crate (crates/compose/src/psblend.rs).
/// </summary>
public static class PhotoshopBlendEngine
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
        float csA = (source.A / 255.0f) * (float)Math.Clamp(opacity, 0.0, 1.0);

        if (csA <= 1e-5f) return backdrop;
        if (cbA <= 1e-5f)
        {
            return Color.FromArgb((byte)Math.Round(csA * 255.0f), source.R, source.G, source.B);
        }

        float blendedR = BlendComponent(cbR, csR, mode);
        float blendedG = BlendComponent(cbG, csG, mode);
        float blendedB = BlendComponent(cbB, csB, mode);

        // Standard compositing equation: out = Cs * as + Cb * ab * (1 - as)
        float outA = csA + cbA * (1.0f - csA);
        float outR = (csA * blendedR + cbA * cbR * (1.0f - csA)) / Math.Max(1e-5f, outA);
        float outG = (csA * blendedG + cbA * cbG * (1.0f - csA)) / Math.Max(1e-5f, outA);
        float outB = (csA * blendedB + cbA * cbB * (1.0f - csA)) / Math.Max(1e-5f, outA);

        return Color.FromArgb(
            (byte)Math.Round(Math.Clamp(outA * 255.0f, 0, 255)),
            (byte)Math.Round(Math.Clamp(outR * 255.0f, 0, 255)),
            (byte)Math.Round(Math.Clamp(outG * 255.0f, 0, 255)),
            (byte)Math.Round(Math.Clamp(outB * 255.0f, 0, 255)));
    }

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
        LayerBlendMode.PinLight => PinLight(cb, cs),
        LayerBlendMode.HardMix => HardMix(cb, cs),
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

    private static float PinLight(float cb, float cs)
    {
        if (cs <= 0.5f) return Math.Min(cb, 2.0f * cs);
        return Math.Max(cb, 2.0f * (cs - 0.5f));
    }

    private static float HardMix(float cb, float cs)
    {
        return VividLight(cb, cs) >= 0.5f ? 1.0f : 0.0f;
    }
}
