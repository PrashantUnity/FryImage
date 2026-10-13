using System;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Professional digital imaging adjustment and filter engine ported from FryImage (crates/ops & crates/raster).
/// Implements multithreaded, real-time pixel processing for:
/// - Brightness & Contrast
/// - 3-Point Levels (Shadow, Midtone Gamma, Highlight)
/// - Spline LUT Curves
/// - Hue / Saturation / Lightness (with optional Colorize)
/// - Vibrance (smart saturation protecting skin tones and already-saturated pixels)
/// - 3-Zone Color Balance (Shadows, Midtones, Highlights across Cyan/Red, Magenta/Green, Yellow/Blue)
/// - Black & White channel weighting
/// - Photo Filter with luminosity preservation
/// - Invert, 1-bit Threshold, Posterize
/// - High-performance Separable 3-Pass Gaussian Box Blur
/// - Unsharp Mask & Spatial Sharpening
/// - High Pass Filter
/// - Monochromatic & Chromatic Noise
/// </summary>
public static partial class ImageAdjustmentEngine
{
    // Standard Rec. 709 / sRGB luma coefficients
    private const double LumaR = 0.2126;
    private const double LumaG = 0.7152;
    private const double LumaB = 0.0722;

    /// <summary>
    /// Applies Brightness [-100..100] and Contrast [-100..100].
    /// </summary>
    public static byte[] ApplyBrightnessContrast(byte[] bgraPixels, int brightness, int contrast)
    {
        byte[] lut = new byte[256];
        double c = (100.0 + contrast) / 100.0;
        double cFactor = c * c;
        double bOffset = brightness * 1.28; // scale to ~[-128..128]

        for (int i = 0; i < 256; i++)
        {
            double val = ((i - 128.0) * cFactor + 128.0) + bOffset;
            lut[i] = (byte)Math.Clamp((int)Math.Round(val), 0, 255);
        }

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = lut[bgraPixels[idx]];         // B
            result[idx + 1] = lut[bgraPixels[idx + 1]]; // G
            result[idx + 2] = lut[bgraPixels[idx + 2]]; // R
            result[idx + 3] = bgraPixels[idx + 3];     // A
        });

        return result;
    }

    /// <summary>
    /// Applies 3-point Levels (black point, midtone gamma, white point) [0..255].
    /// </summary>
    public static byte[] ApplyLevels(byte[] srgbPixels, int inBlack, double gamma, int inWhite, int outBlack = 0, int outWhite = 255)
    {
        byte[] lut = new byte[256];
        double range = Math.Max(1, inWhite - inBlack);
        double outRange = outWhite - outBlack;
        double invGamma = 1.0 / Math.Clamp(gamma, 0.1, 10.0);

        for (int i = 0; i < 256; i++)
        {
            double normalized = Math.Clamp((i - inBlack) / range, 0.0, 1.0);
            double withGamma = Math.Pow(normalized, invGamma);
            int mapped = (int)Math.Round(outBlack + withGamma * outRange);
            lut[i] = (byte)Math.Clamp(mapped, 0, 255);
        }

        byte[] result = new byte[srgbPixels.Length];
        Parallel.For(0, srgbPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = lut[srgbPixels[idx]];         // B
            result[idx + 1] = lut[srgbPixels[idx + 1]]; // G
            result[idx + 2] = lut[srgbPixels[idx + 2]]; // R
            result[idx + 3] = srgbPixels[idx + 3];     // A
        });

        return result;
    }

    /// <summary>
    /// Applies arbitrary LUT Curves per channel (256-byte LUT each for R, G, B).
    /// </summary>
    public static byte[] ApplyCurves(byte[] bgraPixels, byte[] lutR, byte[] lutG, byte[] lutB)
    {
        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = lutB[bgraPixels[idx]];
            result[idx + 1] = lutG[bgraPixels[idx + 1]];
            result[idx + 2] = lutR[bgraPixels[idx + 2]];
            result[idx + 3] = bgraPixels[idx + 3];
        });
        return result;
    }

    /// <summary>
    /// Applies Hue [-180..180], Saturation [-100..100], and Lightness [-100..100].
    /// </summary>
    public static byte[] ApplyHueSaturation(byte[] bgraPixels, int hueShift, int satShift, int lightShift, bool colorize = false)
    {
        byte[] result = new byte[bgraPixels.Length];
        double hDelta = hueShift;
        double lDelta = lightShift / 100.0;
        double sVal = satShift / 100.0;

        static double Lighten(double v, double l) => l > 0.0 ? v + (1.0 - v) * l : v * (1.0 + l);

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            byte b = bgraPixels[idx];
            byte g = bgraPixels[idx + 1];
            byte r = bgraPixels[idx + 2];
            byte a = bgraPixels[idx + 3];

            if (colorize)
            {
                // Photoshop Colorize builds at lightened/darkened lightness
                RgbToHsl(r, g, b, out _, out _, out double ll);
                double targetL = Lighten(ll, lDelta);
                double cHue = ((hDelta % 360.0) + 360.0) % 360.0;
                double cSat = Math.Min(Math.Abs(sVal), 1.0);
                HslToRgb(cHue, cSat, Math.Clamp(targetL, 0.0, 1.0), out byte outR, out byte outG, out byte outB);

                result[idx] = outB;
                result[idx + 1] = outG;
                result[idx + 2] = outR;
                result[idx + 3] = a;
            }
            else
            {
                // Photoshop applies Lightness first, then rotates hue and scales saturation
                double lr = Lighten(r / 255.0, lDelta);
                double lg = Lighten(g / 255.0, lDelta);
                double lb = Lighten(b / 255.0, lDelta);

                byte ir = (byte)Math.Clamp((int)Math.Round(lr * 255.0), 0, 255);
                byte ig = (byte)Math.Clamp((int)Math.Round(lg * 255.0), 0, 255);
                byte ib = (byte)Math.Clamp((int)Math.Round(lb * 255.0), 0, 255);

                RgbToHsl(ir, ig, ib, out double h, out double s, out double l);
                h = ((h + hDelta) % 360.0 + 360.0) % 360.0;

                // Raising saturation divides by (1 - s); lowering multiplies by (1 + s)
                if (sVal > 0.0)
                    s = s / Math.Max(1.0 - sVal, 1.0 / 512.0);
                else
                    s *= (1.0 + sVal);

                s = Math.Clamp(s, 0.0, 1.0);
                HslToRgb(h, s, l, out byte outR, out byte outG, out byte outB);

                result[idx] = outB;
                result[idx + 1] = outG;
                result[idx + 2] = outR;
                result[idx + 3] = a;
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Vibrance [-100..100] and Saturation [-100..100].
    /// Vibrance boosts less-saturated colors more than saturated ones.
    /// </summary>
    public static byte[] ApplyVibrance(byte[] bgraPixels, int vibrance, int saturation)
    {
        byte[] result = new byte[bgraPixels.Length];
        double vAmount = vibrance / 100.0;
        double sAmount = saturation / 100.0;

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            byte b = bgraPixels[idx];
            byte g = bgraPixels[idx + 1];
            byte r = bgraPixels[idx + 2];
            byte a = bgraPixels[idx + 3];

            double max = Math.Max(r, Math.Max(g, b)) / 255.0;
            double min = Math.Min(r, Math.Min(g, b)) / 255.0;
            double currentSat = max == 0 ? 0 : (max - min) / max;

            double luma = LumaR * r + LumaG * g + LumaB * b;
            double totalSat = 1.0 + sAmount + (vAmount * (1.0 - currentSat));
            totalSat = Math.Max(0.0, totalSat);

            double newR = luma + (r - luma) * totalSat;
            double newG = luma + (g - luma) * totalSat;
            double newB = luma + (b - luma) * totalSat;

            result[idx] = (byte)Math.Clamp((int)Math.Round(newB), 0, 255);
            result[idx + 1] = (byte)Math.Clamp((int)Math.Round(newG), 0, 255);
            result[idx + 2] = (byte)Math.Clamp((int)Math.Round(newR), 0, 255);
            result[idx + 3] = a;
        });

        return result;
    }

    private const double BalanceLevels = 0.39;

    /// <summary>
    /// Evaluates Photoshop 2026's exact Color Balance channel curve for a given channel (0=R, 1=G, 2=B).
    /// tones is a 3x3 array: [shadows[R,G,B], midtones[R,G,B], highlights[R,G,B]].
    /// </summary>
    public static double ColorBalanceChannel(double[][] tones, bool preserveLuminosity, int ch, double v)
    {
        double[] shadows = tones[0];
        double[] midtones = tones[1];
        double[] highlights = tones[2];

        static double Black(double val, double b) => b >= 1.0 ? 0.0 : Math.Max(0.0, (val - b) / (1.0 - b));
        static double White(double val, double w) => w <= 0.0 ? 1.0 : Math.Min(1.0, val / w);

        v = Math.Clamp(v, 0.0, 1.0);

        (int tone, double[] sliders)[] passes = [
            (2, highlights),
            (0, shadows),
            (1, midtones)
        ];

        foreach (var (tone, sliders) in passes)
        {
            double[] a = [
                Math.Clamp(sliders[0] / 100.0, -1.0, 1.0),
                Math.Clamp(sliders[1] / 100.0, -1.0, 1.0),
                Math.Clamp(sliders[2] / 100.0, -1.0, 1.0)
            ];

            if (preserveLuminosity && tone != 1)
            {
                double own = a[ch];
                double lo = Math.Min(a[0], Math.Min(a[1], a[2]));
                double hi = Math.Max(a[0], Math.Max(a[1], a[2]));
                v = tone == 0
                    ? Black(v, BalanceLevels * (hi - own))
                    : White(v, 1.0 - BalanceLevels * (own - lo));
                continue;
            }

            for (int src = 0; src < 3; src++)
            {
                double sVal = a[src];
                bool own = src == ch;
                if (sVal == 0.0 || (!preserveLuminosity && !own)) continue;

                if (tone == 1) // midtones
                {
                    if (!preserveLuminosity)
                    {
                        v = Math.Pow(v, Math.Pow(2.0, -sVal));
                    }
                    else
                    {
                        double exp = (own ? -sVal : sVal) / 2.0;
                        v = Math.Pow(v, Math.Pow(2.0, exp));
                    }
                }
                else if (tone == 0) // shadows
                {
                    if (!preserveLuminosity)
                    {
                        if (sVal < 0.0)
                            v = Math.Pow(Black(v, BalanceLevels * -sVal), Math.Pow(2.0, -sVal / 2.0));
                        else
                            v = Math.Pow(v, Math.Pow(2.0, -sVal / 2.0));
                    }
                }
                else if (tone == 2) // highlights
                {
                    if (!preserveLuminosity)
                    {
                        if (sVal > 0.0)
                            v = Math.Pow(White(v, 1.0 - BalanceLevels * sVal), Math.Pow(2.0, -sVal / 2.0));
                        else
                            v = Math.Pow(v, Math.Pow(2.0, -sVal / 2.0));
                    }
                }
            }
        }

        return Math.Clamp(v, 0.0, 1.0);
    }

    /// <summary>
    /// Applies 3-zone Photoshop 2026 Color Balance (Shadows, Midtones, Highlights) with per-channel LUT curves and optional Luminosity Preservation.
    /// </summary>
    public static byte[] ApplyColorBalancePhotoshop(byte[] bgraPixels, double[][] tones, bool preserveLuminosity = true)
    {
        byte[] lutR = new byte[256];
        byte[] lutG = new byte[256];
        byte[] lutB = new byte[256];

        for (int i = 0; i < 256; i++)
        {
            double v = i / 255.0;
            lutR[i] = (byte)Math.Clamp((int)Math.Round(ColorBalanceChannel(tones, preserveLuminosity, 0, v) * 255.0), 0, 255);
            lutG[i] = (byte)Math.Clamp((int)Math.Round(ColorBalanceChannel(tones, preserveLuminosity, 1, v) * 255.0), 0, 255);
            lutB[i] = (byte)Math.Clamp((int)Math.Round(ColorBalanceChannel(tones, preserveLuminosity, 2, v) * 255.0), 0, 255);
        }

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = lutB[bgraPixels[idx]];
            result[idx + 1] = lutG[bgraPixels[idx + 1]];
            result[idx + 2] = lutR[bgraPixels[idx + 2]];
            result[idx + 3] = bgraPixels[idx + 3];
        });

        return result;
    }

    /// <summary>
    /// Backward-compatible Color Balance calling Photoshop 2026's per-channel curves engine.
    /// </summary>
    public static byte[] ApplyColorBalance(byte[] bgraPixels, int cyanRed, int magentaGreen, int yellowBlue, int toneRange = 1)
    {
        double[][] tones = [
            [0, 0, 0], // shadows
            [0, 0, 0], // midtones
            [0, 0, 0]  // highlights
        ];

        int targetTone = Math.Clamp(toneRange, 0, 2);
        tones[targetTone][0] = cyanRed;
        tones[targetTone][1] = magentaGreen;
        tones[targetTone][2] = yellowBlue;

        return ApplyColorBalancePhotoshop(bgraPixels, tones, preserveLuminosity: true);
    }

    /// <summary>
    /// Applies Photoshop Black &amp; White conversion using primary/secondary range decomposition and optional Color blend tint.
    /// </summary>
    public static byte[] ApplyBlackAndWhite(
        byte[] bgraPixels,
        int rW = 40,
        int yW = 60,
        int gW = 40,
        int cW = 60,
        int bW = 20,
        int mW = 80,
        Avalonia.Media.Color? tint = null)
    {
        byte[] result = new byte[bgraPixels.Length];
        double[] weights = [rW, yW, gW, cW, bW, mW];

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            double b = bgraPixels[idx] / 255.0;
            double g = bgraPixels[idx + 1] / 255.0;
            double r = bgraPixels[idx + 2] / 255.0;
            byte a = bgraPixels[idx + 3];

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double mid = r + g + b - max - min;

            int primary = (r >= g && r >= b) ? 0 : (g >= b ? 2 : 4);
            int secondary = (b <= r && b <= g) ? 1 : (r <= g ? 3 : 5);

            double gray = Math.Clamp(min + (mid - min) * weights[secondary] / 100.0 + (max - mid) * weights[primary] / 100.0, 0.0, 1.0);

            if (tint.HasValue)
            {
                // Photoshop tints like the Color blend mode: tint hue/saturation at gray luminosity
                double tR = tint.Value.R / 255.0;
                double tG = tint.Value.G / 255.0;
                double tB = tint.Value.B / 255.0;

                var (outR, outG, outB) = PdfEditorApp.Plugins.ImageEditor.Core.Coloring.ColorConversions.SetLum(tR, tG, tB, gray);
                result[idx] = (byte)Math.Clamp((int)Math.Round(outB * 255.0), 0, 255);
                result[idx + 1] = (byte)Math.Clamp((int)Math.Round(outG * 255.0), 0, 255);
                result[idx + 2] = (byte)Math.Clamp((int)Math.Round(outR * 255.0), 0, 255);
            }
            else
            {
                byte val = (byte)Math.Clamp((int)Math.Round(gray * 255.0), 0, 255);
                result[idx] = val;
                result[idx + 1] = val;
                result[idx + 2] = val;
            }
            result[idx + 3] = a;
        });

        return result;
    }

    /// <summary>
    /// Inverts RGB channels while preserving alpha.
    /// </summary>
    public static byte[] ApplyInvert(byte[] bgraPixels)
    {
        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = (byte)(255 - bgraPixels[idx]);
            result[idx + 1] = (byte)(255 - bgraPixels[idx + 1]);
            result[idx + 2] = (byte)(255 - bgraPixels[idx + 2]);
            result[idx + 3] = bgraPixels[idx + 3];
        });
        return result;
    }

    /// <summary>
    /// Applies high-contrast 1-bit thresholding [0..255].
    /// </summary>
    public static byte[] ApplyThreshold(byte[] bgraPixels, int threshold = 128)
    {
        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            byte b = bgraPixels[idx];
            byte g = bgraPixels[idx + 1];
            byte r = bgraPixels[idx + 2];
            byte a = bgraPixels[idx + 3];

            int luma = (int)(LumaR * r + LumaG * g + LumaB * b);
            byte val = luma >= threshold ? (byte)255 : (byte)0;

            result[idx] = val;
            result[idx + 1] = val;
            result[idx + 2] = val;
            result[idx + 3] = a;
        });
        return result;
    }

    /// <summary>
    /// Reduces tonal steps to discrete levels [2..255].
    /// </summary>
    public static byte[] ApplyPosterize(byte[] bgraPixels, int levels = 4)
    {
        int safeLevels = Math.Clamp(levels, 2, 255);
        byte[] lut = new byte[256];
        double step = 255.0 / (safeLevels - 1);
        for (int i = 0; i < 256; i++)
        {
            int bucket = (int)Math.Round((i / 255.0) * (safeLevels - 1));
            lut[i] = (byte)Math.Clamp((int)Math.Round(bucket * step), 0, 255);
        }

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = lut[bgraPixels[idx]];
            result[idx + 1] = lut[bgraPixels[idx + 1]];
            result[idx + 2] = lut[bgraPixels[idx + 2]];
            result[idx + 3] = bgraPixels[idx + 3];
        });
        return result;
    }

    /// <summary>
    /// Applies Invert to 4-channel CMYK inks (#3930): paper (0% ink) inverts to 100% ink across all 4 channels (400% ink).
    /// </summary>
    public static byte[] ApplyInvertCmyk(byte[] cmykPixels)
    {
        byte[] result = new byte[cmykPixels.Length];
        Parallel.For(0, cmykPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = (byte)(255 - cmykPixels[idx]);         // C
            result[idx + 1] = (byte)(255 - cmykPixels[idx + 1]); // M
            result[idx + 2] = (byte)(255 - cmykPixels[idx + 2]); // Y
            result[idx + 3] = (byte)(255 - cmykPixels[idx + 3]); // K
        });
        return result;
    }

    /// <summary>
    /// Applies Threshold on CMYK inks (#3930): cuts to paper (0% ink) or 100% black ink alone.
    /// </summary>
    public static byte[] ApplyThresholdCmyk(byte[] cmykPixels, int threshold = 128)
    {
        byte[] result = new byte[cmykPixels.Length];
        Parallel.For(0, cmykPixels.Length / 4, p =>
        {
            int idx = p * 4;
            // Total density / black ink threshold
            int k = cmykPixels[idx + 3];
            int totalInk = (cmykPixels[idx] + cmykPixels[idx + 1] + cmykPixels[idx + 2] + k) / 4;
            byte kVal = totalInk >= threshold ? (byte)255 : (byte)0;
            result[idx] = 0;
            result[idx + 1] = 0;
            result[idx + 2] = 0;
            result[idx + 3] = kVal;
        });
        return result;
    }

    /// <summary>
    /// Applies Posterize directly to each CMYK ink channel (#3930).
    /// </summary>
    public static byte[] ApplyPosterizeCmyk(byte[] cmykPixels, int levels = 4)
    {
        int safeLevels = Math.Clamp(levels, 2, 255);
        byte[] lut = new byte[256];
        double step = 255.0 / (safeLevels - 1);
        for (int i = 0; i < 256; i++)
        {
            int bucket = (int)Math.Round((i / 255.0) * (safeLevels - 1));
            lut[i] = (byte)Math.Clamp((int)Math.Round(bucket * step), 0, 255);
        }

        byte[] result = new byte[cmykPixels.Length];
        Parallel.For(0, cmykPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = lut[cmykPixels[idx]];
            result[idx + 1] = lut[cmykPixels[idx + 1]];
            result[idx + 2] = lut[cmykPixels[idx + 2]];
            result[idx + 3] = lut[cmykPixels[idx + 3]];
        });
        return result;
    }

    /// <summary>
    /// 16-bit Lab Invert (#3930): inverts L and mirrors a & b around neutral 32768, keeping greys neutral.
    /// </summary>
    public static ushort[] ApplyInvertLab16(ushort[] labPixels)
    {
        ushort[] result = new ushort[labPixels.Length];
        for (int i = 0; i < labPixels.Length; i += 3)
        {
            result[i] = (ushort)(65535 - labPixels[i]);         // L: 0..65535 inverted
            result[i + 1] = (ushort)(65535 - labPixels[i + 1]); // a: mirrored around 32768
            result[i + 2] = (ushort)(65535 - labPixels[i + 2]); // b: mirrored around 32768
        }
        return result;
    }

    /// <summary>
    /// Applies Hue/Saturation with per-color ranges (Reds, Yellows, Greens, Cyans, Blues, Magentas).
    /// Matches Photoshop oracle parity (#4089): range edits apply in full even to dull colors without chroma fading.
    /// </summary>
    public static byte[] ApplyHueSaturationWithRanges(
        byte[] bgraPixels,
        int masterHue, int masterSat, int masterLight,
        double[]? rangeHues = null, double[]? rangeSats = null, double[]? rangeLights = null)
    {
        byte[] result = new byte[bgraPixels.Length];
        double mHue = masterHue;
        double mSat = masterSat / 100.0;
        double mLight = masterLight / 100.0;

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            byte b = bgraPixels[idx];
            byte g = bgraPixels[idx + 1];
            byte r = bgraPixels[idx + 2];
            byte a = bgraPixels[idx + 3];

            RgbToHsl(r, g, b, out double h0, out double s0, out double l0);

            // Master lightness shift
            double l = mLight > 0 ? l0 + (1.0 - l0) * mLight : l0 * (1.0 + mLight);
            double h = ((h0 + mHue) % 360.0 + 360.0) % 360.0;
            double s = mSat > 0 ? s0 / Math.Max(1.0 - mSat, 1.0 / 512.0) : s0 * (1.0 + mSat);

            // Range-specific edits applied in full (Photoshop #4089: no chroma attenuation damping)
            if (rangeHues != null && rangeSats != null && rangeLights != null && (r != g || g != b))
            {
                // Determine dominant hue sector: 0=Reds, 1=Yellows, 2=Greens, 3=Cyans, 4=Blues, 5=Magentas
                int sector = (int)(Math.Floor(((h0 + 30.0) % 360.0) / 60.0)) % 6;
                if (sector >= 0 && sector < rangeHues.Length)
                {
                    h = ((h + rangeHues[sector]) % 360.0 + 360.0) % 360.0;
                    double rSat = rangeSats[sector] / 100.0;
                    s = rSat > 0 ? s / Math.Max(1.0 - rSat, 1.0 / 512.0) : s * (1.0 + rSat);
                    double rLight = rangeLights[sector] / 100.0;
                    l = rLight > 0 ? l + (1.0 - l) * rLight : l * (1.0 + rLight);
                }
            }

            s = Math.Clamp(s, 0.0, 1.0);
            l = Math.Clamp(l, 0.0, 1.0);
            HslToRgb(h, s, l, out byte outR, out byte outG, out byte outB);

            result[idx] = outB;
            result[idx + 1] = outG;
            result[idx + 2] = outR;
            result[idx + 3] = a;
        });

        return result;
    }

    /// <summary>
    /// Applies Auto Tone: performs per-channel histogram stretching (clipping 0.5% tails) across R, G, B channels.
    /// </summary>
    public static byte[] ApplyAutoTone(byte[] bgraPixels)
    {
        int totalPixels = bgraPixels.Length / 4;
        if (totalPixels == 0) return (byte[])bgraPixels.Clone();

        int[] histB = new int[256];
        int[] histG = new int[256];
        int[] histR = new int[256];

        for (int i = 0; i < bgraPixels.Length; i += 4)
        {
            histB[bgraPixels[i]]++;
            histG[bgraPixels[i + 1]]++;
            histR[bgraPixels[i + 2]]++;
        }

        int clipCount = (int)(totalPixels * 0.005);
        FindLimits(histB, clipCount, out int minB, out int maxB);
        FindLimits(histG, clipCount, out int minG, out int maxG);
        FindLimits(histR, clipCount, out int minR, out int maxR);

        byte[] lutB = BuildStretchLut(minB, maxB);
        byte[] lutG = BuildStretchLut(minG, maxG);
        byte[] lutR = BuildStretchLut(minR, maxR);

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, totalPixels, p =>
        {
            int idx = p * 4;
            result[idx] = lutB[bgraPixels[idx]];
            result[idx + 1] = lutG[bgraPixels[idx + 1]];
            result[idx + 2] = lutR[bgraPixels[idx + 2]];
            result[idx + 3] = bgraPixels[idx + 3];
        });

        return result;

        static void FindLimits(int[] hist, int clip, out int minVal, out int maxVal)
        {
            int accum = 0;
            minVal = 0;
            for (int i = 0; i < 256; i++)
            {
                accum += hist[i];
                if (accum > clip) { minVal = i; break; }
            }
            accum = 0;
            maxVal = 255;
            for (int i = 255; i >= 0; i--)
            {
                accum += hist[i];
                if (accum > clip) { maxVal = i; break; }
            }
            if (maxVal <= minVal) { minVal = 0; maxVal = 255; }
        }

        static byte[] BuildStretchLut(int min, int max)
        {
            byte[] lut = new byte[256];
            double range = Math.Max(1, max - min);
            for (int i = 0; i < 256; i++)
            {
                double normalized = Math.Clamp((i - min) / range, 0.0, 1.0);
                lut[i] = (byte)Math.Clamp((int)Math.Round(normalized * 255.0), 0, 255);
            }
            return lut;
        }
    }

    /// <summary>
    /// High-performance separable 3-pass box blur (approximates Gaussian Blur within 1%).
    /// </summary>
    public static byte[] ApplyGaussianBlur(byte[] bgraPixels, int width, int height, double radius)
    {
        if (radius <= 0.1 || width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        int[] boxes = BoxesForGauss(radius, 3);
        byte[] src = (byte[])bgraPixels.Clone();
        byte[] dst = new byte[bgraPixels.Length];

        for (int pass = 0; pass < 3; pass++)
        {
            int r = (boxes[pass] - 1) / 2;
            BoxBlurHorizontal(src, dst, width, height, r);
            BoxBlurVertical(dst, src, width, height, r);
        }

        return src;
    }

    /// <summary>
    /// Applies unsharp mask sharpening: sharpened = original + (original - blur) * amount.
    /// </summary>
    public static byte[] ApplySharpen(byte[] bgraPixels, int width, int height, double amount = 1.0, double radius = 1.5, int threshold = 0)
    {
        byte[] blurred = ApplyGaussianBlur(bgraPixels, width, height, radius);
        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            for (int c = 0; c < 3; c++)
            {
                int orig = bgraPixels[idx + c];
                int blur = blurred[idx + c];
                int diff = orig - blur;

                if (Math.Abs(diff) >= threshold)
                {
                    int sharp = (int)Math.Round(orig + diff * amount);
                    result[idx + c] = (byte)Math.Clamp(sharp, 0, 255);
                }
                else
                {
                    result[idx + c] = (byte)orig;
                }
            }
            result[idx + 3] = bgraPixels[idx + 3]; // Alpha
        });

        return result;
    }

    /// <summary>
    /// High-pass filter: highPass = (original - blur) + 128.
    /// </summary>
    public static byte[] ApplyHighPass(byte[] bgraPixels, int width, int height, double radius = 10.0)
    {
        byte[] blurred = ApplyGaussianBlur(bgraPixels, width, height, radius);
        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            for (int c = 0; c < 3; c++)
            {
                int orig = bgraPixels[idx + c];
                int blur = blurred[idx + c];
                int val = (orig - blur) + 128;
                result[idx + c] = (byte)Math.Clamp(val, 0, 255);
            }
            result[idx + 3] = bgraPixels[idx + 3];
        });

        return result;
    }

    /// <summary>
    /// Adds synthetic grain / noise [-amount..amount].
    /// </summary>
    public static byte[] ApplyAddNoise(byte[] bgraPixels, int amount = 25, bool monochromatic = true)
    {
        byte[] result = new byte[bgraPixels.Length];
        var rnd = new Random();

        for (int p = 0; p < bgraPixels.Length / 4; p++)
        {
            int idx = p * 4;
            if (monochromatic)
            {
                int n = rnd.Next(-amount, amount + 1);
                result[idx] = (byte)Math.Clamp(bgraPixels[idx] + n, 0, 255);
                result[idx + 1] = (byte)Math.Clamp(bgraPixels[idx + 1] + n, 0, 255);
                result[idx + 2] = (byte)Math.Clamp(bgraPixels[idx + 2] + n, 0, 255);
            }
            else
            {
                result[idx] = (byte)Math.Clamp(bgraPixels[idx] + rnd.Next(-amount, amount + 1), 0, 255);
                result[idx + 1] = (byte)Math.Clamp(bgraPixels[idx + 1] + rnd.Next(-amount, amount + 1), 0, 255);
                result[idx + 2] = (byte)Math.Clamp(bgraPixels[idx + 2] + rnd.Next(-amount, amount + 1), 0, 255);
            }
            result[idx + 3] = bgraPixels[idx + 3];
        }

        return result;
    }

    // ── Helper math ─────────────────────────────────────────────────────────

    private static int[] BoxesForGauss(double sigma, int n)
    {
        double wIdeal = Math.Sqrt((12.0 * sigma * sigma / n) + 1.0);
        int wl = (int)Math.Floor(wIdeal);
        if (wl % 2 == 0) wl--;
        int wu = wl + 2;

        double mIdeal = (12.0 * sigma * sigma - n * wl * wl - 4.0 * n * wl - 3.0 * n) / (-4.0 * wl - 4.0);
        int m = (int)Math.Round(mIdeal);

        int[] sizes = new int[n];
        for (int i = 0; i < n; i++)
            sizes[i] = i < m ? wl : wu;
        return sizes;
    }

    private static void BoxBlurHorizontal(byte[] src, byte[] dst, int w, int h, int r)
    {
        if (r <= 0)
        {
            Buffer.BlockCopy(src, 0, dst, 0, src.Length);
            return;
        }

        double invSize = 1.0 / (r + r + 1);

        Parallel.For(0, h, y =>
        {
            int rowStart = y * w * 4;
            int rSum = 0, gSum = 0, bSum = 0, aSum = 0;

            // Init accumulator
            for (int i = -r; i <= r; i++)
            {
                int px = Math.Clamp(i, 0, w - 1);
                int idx = rowStart + px * 4;
                bSum += src[idx];
                gSum += src[idx + 1];
                rSum += src[idx + 2];
                aSum += src[idx + 3];
            }

            for (int x = 0; x < w; x++)
            {
                int outIdx = rowStart + x * 4;
                dst[outIdx] = (byte)(bSum * invSize);
                dst[outIdx + 1] = (byte)(gSum * invSize);
                dst[outIdx + 2] = (byte)(rSum * invSize);
                dst[outIdx + 3] = (byte)(aSum * invSize);

                int nextX = Math.Clamp(x + r + 1, 0, w - 1);
                int prevX = Math.Clamp(x - r, 0, w - 1);

                int nextIdx = rowStart + nextX * 4;
                int prevIdx = rowStart + prevX * 4;

                bSum += src[nextIdx] - src[prevIdx];
                gSum += src[nextIdx + 1] - src[prevIdx + 1];
                rSum += src[nextIdx + 2] - src[prevIdx + 2];
                aSum += src[nextIdx + 3] - src[prevIdx + 3];
            }
        });
    }

    private static void BoxBlurVertical(byte[] src, byte[] dst, int w, int h, int r)
    {
        if (r <= 0)
        {
            Buffer.BlockCopy(src, 0, dst, 0, src.Length);
            return;
        }

        double invSize = 1.0 / (r + r + 1);

        Parallel.For(0, w, x =>
        {
            int rSum = 0, gSum = 0, bSum = 0, aSum = 0;

            // Init accumulator
            for (int i = -r; i <= r; i++)
            {
                int py = Math.Clamp(i, 0, h - 1);
                int idx = py * w * 4 + x * 4;
                bSum += src[idx];
                gSum += src[idx + 1];
                rSum += src[idx + 2];
                aSum += src[idx + 3];
            }

            for (int y = 0; y < h; y++)
            {
                int outIdx = y * w * 4 + x * 4;
                dst[outIdx] = (byte)(bSum * invSize);
                dst[outIdx + 1] = (byte)(gSum * invSize);
                dst[outIdx + 2] = (byte)(rSum * invSize);
                dst[outIdx + 3] = (byte)(aSum * invSize);

                int nextY = Math.Clamp(y + r + 1, 0, h - 1);
                int prevY = Math.Clamp(y - r, 0, h - 1);

                int nextIdx = nextY * w * 4 + x * 4;
                int prevIdx = prevY * w * 4 + x * 4;

                bSum += src[nextIdx] - src[prevIdx];
                gSum += src[nextIdx + 1] - src[prevIdx + 1];
                rSum += src[nextIdx + 2] - src[prevIdx + 2];
                aSum += src[nextIdx + 3] - src[prevIdx + 3];
            }
        });
    }

    private static void RgbToHsl(byte rByte, byte gByte, byte bByte, out double h, out double s, out double l)
    {
        double r = rByte / 255.0;
        double g = gByte / 255.0;
        double b = bByte / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double d = max - min;

        l = (max + min) / 2.0;

        if (d == 0)
        {
            h = 0;
            s = 0;
        }
        else
        {
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r)
                h = ((g - b) / d + (g < b ? 6.0 : 0.0)) * 60.0;
            else if (max == g)
                h = ((b - r) / d + 2.0) * 60.0;
            else
                h = ((r - g) / d + 4.0) * 60.0;
        }
    }

    private static void HslToRgb(double h, double s, double l, out byte rByte, out byte gByte, out byte bByte)
    {
        if (s == 0)
        {
            byte val = (byte)Math.Clamp((int)Math.Round(l * 255.0), 0, 255);
            rByte = gByte = bByte = val;
            return;
        }

        double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
        double p = 2.0 * l - q;
        double hk = h / 360.0;

        double tr = hk + 1.0 / 3.0;
        double tg = hk;
        double tb = hk - 1.0 / 3.0;

        rByte = (byte)Math.Clamp((int)Math.Round(HueToRgb(p, q, tr) * 255.0), 0, 255);
        gByte = (byte)Math.Clamp((int)Math.Round(HueToRgb(p, q, tg) * 255.0), 0, 255);
        bByte = (byte)Math.Clamp((int)Math.Round(HueToRgb(p, q, tb) * 255.0), 0, 255);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1.0;
        if (t > 1) t -= 1.0;
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }

    /// <summary>
    /// Applies Auto Contrast: stretches composite luminance across all channels uniformly (0.5% tail clip).
    /// </summary>
    public static byte[] ApplyAutoContrast(byte[] bgraPixels)
    {
        int totalPixels = bgraPixels.Length / 4;
        if (totalPixels == 0) return (byte[])bgraPixels.Clone();

        int[] lumaHist = new int[256];
        for (int i = 0; i < bgraPixels.Length; i += 4)
        {
            int luma = (int)Math.Round(LumaB * bgraPixels[i] + LumaG * bgraPixels[i + 1] + LumaR * bgraPixels[i + 2]);
            lumaHist[Math.Clamp(luma, 0, 255)]++;
        }

        int clip = (int)(totalPixels * 0.005);
        int minLuma = 0;
        int accum = 0;
        for (int i = 0; i < 256; i++)
        {
            accum += lumaHist[i];
            if (accum > clip) { minLuma = i; break; }
        }

        int maxLuma = 255;
        accum = 0;
        for (int i = 255; i >= 0; i--)
        {
            accum += lumaHist[i];
            if (accum > clip) { maxLuma = i; break; }
        }

        if (maxLuma <= minLuma) { minLuma = 0; maxLuma = 255; }

        byte[] lut = new byte[256];
        double range = Math.Max(1, maxLuma - minLuma);
        for (int i = 0; i < 256; i++)
        {
            double normalized = Math.Clamp((i - minLuma) / range, 0.0, 1.0);
            lut[i] = (byte)Math.Clamp((int)Math.Round(normalized * 255.0), 0, 255);
        }

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, totalPixels, p =>
        {
            int idx = p * 4;
            result[idx] = lut[bgraPixels[idx]];
            result[idx + 1] = lut[bgraPixels[idx + 1]];
            result[idx + 2] = lut[bgraPixels[idx + 2]];
            result[idx + 3] = bgraPixels[idx + 3];
        });

        return result;
    }

    /// <summary>
    /// Applies Auto Color: clips 0.5% shadows/highlights per channel and neutralizes midtone color cast.
    /// </summary>
    public static byte[] ApplyAutoColor(byte[] bgraPixels)
    {
        // First apply per-channel tone stretch
        byte[] stretched = ApplyAutoTone(bgraPixels);
        int totalPixels = stretched.Length / 4;
        if (totalPixels == 0) return stretched;

        // Find average RGB in the midtone luminance range (96..160)
        long sumB = 0, sumG = 0, sumR = 0, count = 0;
        for (int i = 0; i < stretched.Length; i += 4)
        {
            int luma = (int)Math.Round(LumaB * stretched[i] + LumaG * stretched[i + 1] + LumaR * stretched[i + 2]);
            if (luma >= 96 && luma <= 160)
            {
                sumB += stretched[i];
                sumG += stretched[i + 1];
                sumR += stretched[i + 2];
                count++;
            }
        }

        if (count == 0) return stretched;

        double avgB = (double)sumB / count;
        double avgG = (double)sumG / count;
        double avgR = (double)sumR / count;
        double targetMid = (avgB + avgG + avgR) / 3.0;

        double shiftB = (targetMid - avgB) * 0.7;
        double shiftG = (targetMid - avgG) * 0.7;
        double shiftR = (targetMid - avgR) * 0.7;

        byte[] result = new byte[stretched.Length];
        Parallel.For(0, totalPixels, p =>
        {
            int idx = p * 4;
            int luma = (int)Math.Round(LumaB * stretched[idx] + LumaG * stretched[idx + 1] + LumaR * stretched[idx + 2]);
            // Bell curve weighting centered at 128
            double weight = Math.Max(0.0, 1.0 - Math.Abs(luma - 128) / 128.0);

            result[idx] = (byte)Math.Clamp((int)Math.Round(stretched[idx] + shiftB * weight), 0, 255);
            result[idx + 1] = (byte)Math.Clamp((int)Math.Round(stretched[idx + 1] + shiftG * weight), 0, 255);
            result[idx + 2] = (byte)Math.Clamp((int)Math.Round(stretched[idx + 2] + shiftR * weight), 0, 255);
            result[idx + 3] = stretched[idx + 3];
        });

        return result;
    }

    /// <summary>
    /// Applies Histogram Equalization (Equalize) to flatten the cumulative distribution of luminance.
    /// </summary>
    public static byte[] ApplyEqualize(byte[] bgraPixels)
    {
        int totalPixels = bgraPixels.Length / 4;
        if (totalPixels == 0) return (byte[])bgraPixels.Clone();

        int[] hist = new int[256];
        for (int i = 0; i < bgraPixels.Length; i += 4)
        {
            int luma = (int)Math.Round(LumaB * bgraPixels[i] + LumaG * bgraPixels[i + 1] + LumaR * bgraPixels[i + 2]);
            hist[Math.Clamp(luma, 0, 255)]++;
        }

        // Calculate CDF
        int[] cdf = new int[256];
        int accum = 0;
        int cdfMin = -1;
        for (int i = 0; i < 256; i++)
        {
            accum += hist[i];
            cdf[i] = accum;
            if (cdfMin == -1 && cdf[i] > 0) cdfMin = cdf[i];
        }

        if (cdfMin == -1 || totalPixels <= cdfMin) return (byte[])bgraPixels.Clone();

        byte[] eqLut = new byte[256];
        double denom = Math.Max(1, totalPixels - cdfMin);
        for (int i = 0; i < 256; i++)
        {
            eqLut[i] = (byte)Math.Clamp((int)Math.Round(((cdf[i] - cdfMin) / denom) * 255.0), 0, 255);
        }

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, totalPixels, p =>
        {
            int idx = p * 4;
            int luma = (int)Math.Round(LumaB * bgraPixels[idx] + LumaG * bgraPixels[idx + 1] + LumaR * bgraPixels[idx + 2]);
            int targetLuma = eqLut[Math.Clamp(luma, 0, 255)];
            double scale = luma > 0 ? (double)targetLuma / luma : 1.0;

            result[idx] = (byte)Math.Clamp((int)Math.Round(bgraPixels[idx] * scale), 0, 255);
            result[idx + 1] = (byte)Math.Clamp((int)Math.Round(bgraPixels[idx + 1] * scale), 0, 255);
            result[idx + 2] = (byte)Math.Clamp((int)Math.Round(bgraPixels[idx + 2] * scale), 0, 255);
            result[idx + 3] = bgraPixels[idx + 3];
        });

        return result;
    }

    /// <summary>
    /// Applies Motion Blur along an angle [-180..180] degrees with distance in pixels.
    /// </summary>
    public static byte[] ApplyMotionBlur(byte[] bgraPixels, int width, int height, double angleDegrees, double distance)
    {
        if (distance <= 0.5 || width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        double rad = angleDegrees * Math.PI / 180.0;
        double vx = Math.Cos(rad) * distance;
        double vy = Math.Sin(rad) * distance;
        int steps = Math.Max(3, (int)Math.Ceiling(distance));

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                double sumB = 0, sumG = 0, sumR = 0, sumA = 0;
                for (int s = 0; s < steps; s++)
                {
                    double t = ((double)s / (steps - 1)) - 0.5;
                    double sx = x + t * vx;
                    double sy = y + t * vy;
                    SampleBilinearBgra(bgraPixels, width, height, sx, sy, out byte b, out byte g, out byte r, out byte a);
                    sumB += b;
                    sumG += g;
                    sumR += r;
                    sumA += a;
                }

                int outIdx = (y * width + x) * 4;
                result[outIdx] = (byte)Math.Clamp((int)Math.Round(sumB / steps), 0, 255);
                result[outIdx + 1] = (byte)Math.Clamp((int)Math.Round(sumG / steps), 0, 255);
                result[outIdx + 2] = (byte)Math.Clamp((int)Math.Round(sumR / steps), 0, 255);
                result[outIdx + 3] = (byte)Math.Clamp((int)Math.Round(sumA / steps), 0, 255);
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Radial Blur (Spin or Zoom) centered on the image canvas.
    /// </summary>
    public static byte[] ApplyRadialBlur(byte[] bgraPixels, int width, int height, double amount, bool zoom = false)
    {
        if (amount <= 0.1 || width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        double cx = width * 0.5;
        double cy = height * 0.5;
        const int steps = 16;
        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                double sumB = 0, sumG = 0, sumR = 0, sumA = 0;
                double dx = x - cx;
                double dy = y - cy;

                if (zoom)
                {
                    // Zoom outward/inward along ray
                    for (int s = 0; s < steps; s++)
                    {
                        double factor = 1.0 + (((double)s / (steps - 1)) - 0.5) * (amount * 0.008);
                        double sx = cx + dx * factor;
                        double sy = cy + dy * factor;
                        SampleBilinearBgra(bgraPixels, width, height, sx, sy, out byte b, out byte g, out byte r, out byte a);
                        sumB += b;
                        sumG += g;
                        sumR += r;
                        sumA += a;
                    }
                }
                else
                {
                    // Spin around center
                    double maxAngle = (amount * 0.003) * Math.PI;
                    for (int s = 0; s < steps; s++)
                    {
                        double t = ((double)s / (steps - 1)) - 0.5;
                        double angle = t * maxAngle;
                        double cosA = Math.Cos(angle);
                        double sinA = Math.Sin(angle);
                        double sx = cx + dx * cosA - dy * sinA;
                        double sy = cy + dx * sinA + dy * cosA;
                        SampleBilinearBgra(bgraPixels, width, height, sx, sy, out byte b, out byte g, out byte r, out byte a);
                        sumB += b;
                        sumG += g;
                        sumR += r;
                        sumA += a;
                    }
                }

                int outIdx = (y * width + x) * 4;
                result[outIdx] = (byte)Math.Clamp((int)Math.Round(sumB / steps), 0, 255);
                result[outIdx + 1] = (byte)Math.Clamp((int)Math.Round(sumG / steps), 0, 255);
                result[outIdx + 2] = (byte)Math.Clamp((int)Math.Round(sumR / steps), 0, 255);
                result[outIdx + 3] = (byte)Math.Clamp((int)Math.Round(sumA / steps), 0, 255);
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Mosaic / Pixelate with square cells of specified pixel size.
    /// </summary>
    public static byte[] ApplyMosaic(byte[] bgraPixels, int width, int height, int cellSize)
    {
        int cell = Math.Clamp(cellSize, 2, Math.Max(width, height));
        byte[] result = new byte[bgraPixels.Length];

        int blocksX = (width + cell - 1) / cell;
        int blocksY = (height + cell - 1) / cell;

        Parallel.For(0, blocksY, by =>
        {
            int y0 = by * cell;
            int y1 = Math.Min(y0 + cell, height);

            for (int bx = 0; bx < blocksX; bx++)
            {
                int x0 = bx * cell;
                int x1 = Math.Min(x0 + cell, width);

                long sumB = 0, sumG = 0, sumR = 0, sumA = 0;
                int count = (x1 - x0) * (y1 - y0);

                for (int y = y0; y < y1; y++)
                {
                    int row = y * width * 4;
                    for (int x = x0; x < x1; x++)
                    {
                        int idx = row + x * 4;
                        sumB += bgraPixels[idx];
                        sumG += bgraPixels[idx + 1];
                        sumR += bgraPixels[idx + 2];
                        sumA += bgraPixels[idx + 3];
                    }
                }

                byte avgB = (byte)(sumB / count);
                byte avgG = (byte)(sumG / count);
                byte avgR = (byte)(sumR / count);
                byte avgA = (byte)(sumA / count);

                for (int y = y0; y < y1; y++)
                {
                    int row = y * width * 4;
                    for (int x = x0; x < x1; x++)
                    {
                        int idx = row + x * 4;
                        result[idx] = avgB;
                        result[idx + 1] = avgG;
                        result[idx + 2] = avgR;
                        result[idx + 3] = avgA;
                    }
                }
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Twirl distortion around the center with specified angle [-720..720] and radius.
    /// </summary>
    public static byte[] ApplyTwirl(byte[] bgraPixels, int width, int height, double angleDegrees, double radius = 0)
    {
        if (Math.Abs(angleDegrees) < 0.1 || width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        double cx = width * 0.5;
        double cy = height * 0.5;
        double rMax = radius <= 0 ? Math.Min(width, height) * 0.5 : radius;
        double totalRad = angleDegrees * Math.PI / 180.0;

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int outIdx = (y * width + x) * 4;
                double dx = x - cx;
                double dy = y - cy;
                double r = Math.Sqrt(dx * dx + dy * dy);

                if (r < rMax && rMax > 0)
                {
                    double twist = totalRad * (1.0 - r / rMax);
                    double cosT = Math.Cos(twist);
                    double sinT = Math.Sin(twist);
                    double sx = cx + dx * cosT - dy * sinT;
                    double sy = cy + dx * sinT + dy * cosT;

                    SampleBilinearBgra(bgraPixels, width, height, sx, sy, out byte b, out byte g, out byte rVal, out byte a);
                    result[outIdx] = b;
                    result[outIdx + 1] = g;
                    result[outIdx + 2] = rVal;
                    result[outIdx + 3] = a;
                }
                else
                {
                    result[outIdx] = bgraPixels[outIdx];
                    result[outIdx + 1] = bgraPixels[outIdx + 1];
                    result[outIdx + 2] = bgraPixels[outIdx + 2];
                    result[outIdx + 3] = bgraPixels[outIdx + 3];
                }
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Spherize distortion [-100..100] around center.
    /// </summary>
    public static byte[] ApplySpherize(byte[] bgraPixels, int width, int height, double amount)
    {
        if (Math.Abs(amount) < 0.1 || width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        double cx = width * 0.5;
        double cy = height * 0.5;
        double rMax = Math.Min(width, height) * 0.5;
        double aFactor = Math.Clamp(amount / 100.0, -1.0, 1.0);

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int outIdx = (y * width + x) * 4;
                double dx = x - cx;
                double dy = y - cy;
                double r = Math.Sqrt(dx * dx + dy * dy);

                if (r < rMax && r > 0.001)
                {
                    double normR = r / rMax;
                    double targetR = aFactor >= 0 ? Math.Asin(normR) * 2.0 / Math.PI : Math.Sin(normR * Math.PI * 0.5);
                    double mappedR = rMax * (normR + Math.Abs(aFactor) * (targetR - normR));

                    double sx = cx + dx * (mappedR / r);
                    double sy = cy + dy * (mappedR / r);

                    SampleBilinearBgra(bgraPixels, width, height, sx, sy, out byte b, out byte g, out byte rVal, out byte a);
                    result[outIdx] = b;
                    result[outIdx + 1] = g;
                    result[outIdx + 2] = rVal;
                    result[outIdx + 3] = a;
                }
                else
                {
                    result[outIdx] = bgraPixels[outIdx];
                    result[outIdx + 1] = bgraPixels[outIdx + 1];
                    result[outIdx + 2] = bgraPixels[outIdx + 2];
                    result[outIdx + 3] = bgraPixels[outIdx + 3];
                }
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Emboss relief filter with specified directional angle and depth [1..10].
    /// </summary>
    public static byte[] ApplyEmboss(byte[] bgraPixels, int width, int height, double angleDegrees, int depth = 3)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        int ox = (int)Math.Round(Math.Cos(rad) * depth);
        int oy = (int)Math.Round(Math.Sin(rad) * depth);

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int xPlus = Math.Clamp(x + ox, 0, width - 1);
                int yPlus = Math.Clamp(y + oy, 0, height - 1);
                int xMinus = Math.Clamp(x - ox, 0, width - 1);
                int yMinus = Math.Clamp(y - oy, 0, height - 1);

                int idxPlus = (yPlus * width + xPlus) * 4;
                int idxMinus = (yMinus * width + xMinus) * 4;

                int lumaPlus = (int)Math.Round(LumaB * bgraPixels[idxPlus] + LumaG * bgraPixels[idxPlus + 1] + LumaR * bgraPixels[idxPlus + 2]);
                int lumaMinus = (int)Math.Round(LumaB * bgraPixels[idxMinus] + LumaG * bgraPixels[idxMinus + 1] + LumaR * bgraPixels[idxMinus + 2]);

                byte val = (byte)Math.Clamp(lumaPlus - lumaMinus + 128, 0, 255);

                int outIdx = (y * width + x) * 4;
                result[outIdx] = val;
                result[outIdx + 1] = val;
                result[outIdx + 2] = val;
                result[outIdx + 3] = bgraPixels[outIdx + 3];
            }
        });

        return result;
    }

    private static void SampleBilinearBgra(byte[] src, int w, int h, double x, double y, out byte b, out byte g, out byte r, out byte a)
    {
        x = Math.Clamp(x, 0, w - 1);
        y = Math.Clamp(y, 0, h - 1);

        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = Math.Min(x0 + 1, w - 1);
        int y1 = Math.Min(y0 + 1, h - 1);

        double fx = x - x0;
        double fy = y - y0;
        double w00 = (1.0 - fx) * (1.0 - fy);
        double w10 = fx * (1.0 - fy);
        double w01 = (1.0 - fx) * fy;
        double w11 = fx * fy;

        int idx00 = (y0 * w + x0) * 4;
        int idx10 = (y0 * w + x1) * 4;
        int idx01 = (y1 * w + x0) * 4;
        int idx11 = (y1 * w + x1) * 4;

        b = (byte)Math.Clamp((int)Math.Round(src[idx00] * w00 + src[idx10] * w10 + src[idx01] * w01 + src[idx11] * w11), 0, 255);
        g = (byte)Math.Clamp((int)Math.Round(src[idx00 + 1] * w00 + src[idx10 + 1] * w10 + src[idx01 + 1] * w01 + src[idx11 + 1] * w11), 0, 255);
        r = (byte)Math.Clamp((int)Math.Round(src[idx00 + 2] * w00 + src[idx10 + 2] * w10 + src[idx01 + 2] * w01 + src[idx11 + 2] * w11), 0, 255);
        a = (byte)Math.Clamp((int)Math.Round(src[idx00 + 3] * w00 + src[idx10 + 3] * w10 + src[idx01 + 3] * w01 + src[idx11 + 3] * w11), 0, 255);
    }
}
