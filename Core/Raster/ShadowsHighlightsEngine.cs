using System;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Image › Adjustments › Shadows/Highlights engine matching Studio calibrated behavior.
///
/// Features:
/// - Bilateral edge-aware guided neighbourhood brightness (Durand &amp; Dorsey fast bilateral filter).
/// - Beta distribution tonal mask table (fitted MASK_BETA parameters across 20 nodes).
/// - Self-screening lift curve (blended between power curves).
/// - S-curve midtone adjustment on luma.
/// - Exponential/Linear color difference gain with RGB gamut clipping.
/// - Percentile black/white clip levels.
/// </summary>
public static class ShadowsHighlightsEngine
{
    private static readonly float[] Rec601W = [0.299f, 0.587f, 0.114f];

    private const float Spatial = 1.1424f;
    private const float GuideSigma = 1.3559f;
    private const float Range = 1.0152f;
    private const int Levels = 17;

    private static readonly (float A, float B)[] MaskBeta =
    [
        (1.9468f, 22.9366f),
        (1.7770f, 18.3471f),
        (1.6403f, 14.7559f),
        (1.5270f, 11.9346f),
        (1.4269f, 9.6467f),
        (1.3470f, 7.8430f),
        (1.2807f, 6.4038f),
        (1.2240f, 5.2384f),
        (1.1772f, 4.2967f),
        (1.1373f, 3.5350f),
        (1.1021f, 2.9109f),
        (1.0755f, 2.4186f),
        (1.0508f, 2.0189f),
        (1.0307f, 1.7034f),
        (1.0140f, 1.4583f),
        (1.0015f, 1.2758f),
        (1.0f, 1.1403f),
        (1.0f, 1.0510f),
        (1.0f, 1.0002f),
        (1.0f, 1.0f)
    ];

    private static readonly (float X, float Y)[] ColorUp =
    [
        (0.0f, 0.0f),
        (1.0f, 0.1566f),
        (2.0f, 0.2271f),
        (5.0f, 0.3776f),
        (10.0f, 0.5653f),
        (20.0f, 0.8669f),
        (30.0f, 1.1299f),
        (40.0f, 1.3748f),
        (50.0f, 1.6093f),
        (60.0f, 1.8375f),
        (75.0f, 2.1721f),
        (100.0f, 2.7193f)
    ];

    private static readonly (float X, float Y)[] ColorDown =
    [
        (0.0f, 0.0f),
        (1.0f, -0.0145f),
        (5.0f, -0.0709f),
        (10.0f, -0.1373f),
        (20.0f, -0.2570f),
        (30.0f, -0.3612f),
        (50.0f, -0.5312f),
        (75.0f, -0.6866f),
        (100.0f, -0.7968f)
    ];

    private static readonly (float V, float K, float P)[] MidtoneParams =
    [
        (-100.0f, -0.6955f, 2.267f),
        (-75.0f, -0.5873f, 2.362f),
        (-50.0f, -0.4429f, 2.492f),
        (-25.0f, -0.2514f, 2.667f),
        (-10.0f, -0.1088f, 2.803f),
        (0.0f, 0.0f, 2.9f),
        (10.0f, 0.1206f, 3.016f),
        (25.0f, 0.3262f, 3.216f),
        (50.0f, 0.7423f, 3.625f),
        (75.0f, 1.2635f, 4.160f),
        (100.0f, 1.9032f, 4.850f)
    ];

    private static float Luma(float r, float g, float b) => Rec601W[0] * r + Rec601W[1] * g + Rec601W[2] * b;

    private static float Interp((float X, float Y)[] table, float x)
    {
        if (table.Length == 0) return 0.0f;
        if (x <= table[0].X) return table[0].Y;
        for (int i = 0; i < table.Length - 1; i++)
        {
            var (xa, ya) = table[i];
            var (xb, yb) = table[i + 1];
            if (x <= xb)
            {
                return ya + (yb - ya) * (x - xa) / (xb - xa);
            }
        }
        return table[^1].Y;
    }

    private static float Strength(float a)
    {
        float u = (float)Math.Pow(Math.Clamp(a, 0.0f, 100.0f) / 100.0f, 0.6216f);
        return u <= 0.3f ? u / 0.3f : (1.0f + 20.0f * u) / 7.0f;
    }

    private static float Lift(float s, float v)
    {
        int n = Math.Clamp((int)Math.Floor(s), 0, 2);
        float t = Math.Clamp(s - n, 0.0f, 1.0f);
        float c = Math.Clamp(1.0f - v, 0.0f, 1.0f);
        float lo = (float)Math.Pow(c, 1 << n);
        float hi = lo * lo;
        return 1.0f - ((1.0f - t) * lo + t * hi);
    }

    private static float Midtone(float m, float y)
    {
        if (Math.Abs(m) < 1e-5f) return y;
        var ks = new (float X, float Y)[MidtoneParams.Length];
        var ps = new (float X, float Y)[MidtoneParams.Length];
        for (int i = 0; i < MidtoneParams.Length; i++)
        {
            ks[i] = (MidtoneParams[i].V, MidtoneParams[i].K);
            ps[i] = (MidtoneParams[i].V, MidtoneParams[i].P);
        }
        float k = Interp(ks, m);
        float p = Interp(ps, m);
        float inner = Math.Max(0.0f, 4.0f * y * (1.0f - y));
        return Math.Clamp(y + k * (y - 0.5f) * (float)Math.Pow(inner, p), 0.0f, 1.0f);
    }

    private static float[] TonalMask(float tone)
    {
        float t = Math.Clamp(tone, 0.0f, 95.0f) / 5.0f;
        int i = Math.Min((int)Math.Floor(t), MaskBeta.Length - 2);
        float f = t - i;
        var (a0, b0) = MaskBeta[i];
        var (a1, b1) = MaskBeta[i + 1];
        double a = a0 + (a1 - a0) * f;
        double b = b0 + (b1 - b0) * f;

        const int sub = 16;
        int n = 255 * sub;
        double Dens(double x) => Math.Exp((a - 1.0) * Math.Log(x) + (b - 1.0) * Math.Log(1.0 - x));

        double[] cum = new double[256];
        double acc = 0.0;
        for (int j = 0; j < n; j++)
        {
            acc += Dens((j + 0.5) / n);
            if ((j + 1) % sub == 0)
            {
                cum[(j + 1) / sub] = acc;
            }
        }
        double total = Math.Max(acc, double.Epsilon);

        float[] mask = new float[256];
        for (int k = 0; k < 256; k++)
        {
            mask[k] = (float)(1.0 - cum[k] / total);
        }
        return mask;
    }

    private static void GaussBlur1D(float[] src, float[] dst, int w, int h, int channels, float sigma, bool horizontal)
    {
        if (sigma <= 0.1f)
        {
            Array.Copy(src, dst, src.Length);
            return;
        }

        int radius = Math.Clamp((int)Math.Ceiling(sigma * 3.0f), 1, 64);
        float[] kernel = new float[radius * 2 + 1];
        float sum = 0.0f;
        for (int i = -radius; i <= radius; i++)
        {
            float v = (float)Math.Exp(-(i * i) / (2.0f * sigma * sigma));
            kernel[i + radius] = v;
            sum += v;
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        if (horizontal)
        {
            Parallel.For(0, h, y =>
            {
                int rowOffset = y * w * channels;
                for (int x = 0; x < w; x++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        float val = 0.0f;
                        for (int k = -radius; k <= radius; k++)
                        {
                            int px = Math.Clamp(x + k, 0, w - 1);
                            val += src[rowOffset + px * channels + c] * kernel[k + radius];
                        }
                        dst[rowOffset + x * channels + c] = val;
                    }
                }
            });
        }
        else
        {
            Parallel.For(0, w, x =>
            {
                for (int y = 0; y < h; y++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        float val = 0.0f;
                        for (int k = -radius; k <= radius; k++)
                        {
                            int py = Math.Clamp(y + k, 0, h - 1);
                            val += src[(py * w + x) * channels + c] * kernel[k + radius];
                        }
                        dst[(y * w + x) * channels + c] = val;
                    }
                }
            });
        }
    }

    private static void GaussBlur(float[] data, int w, int h, int channels, float sigma)
    {
        float[] temp = new float[data.Length];
        GaussBlur1D(data, temp, w, h, channels, sigma, true);
        GaussBlur1D(temp, data, w, h, channels, sigma, false);
    }

    private static float[] Neighbourhood(float[] lum, int w, int h, float radius)
    {
        float[] guide = (float[])lum.Clone();
        GaussBlur(guide, w, h, 1, GuideSigma);

        float sigma = Spatial * radius;
        if (sigma < 0.3f) return guide;

        int d = Math.Max(1, (int)Math.Floor(sigma / 4.0f));
        int rw = (w + d - 1) / d;
        int rh = (h + d - 1) / d;
        int cells = rw * rh;
        float step = 1.0f / (Levels - 1);

        float[] avg = new float[Levels * cells];
        float[] buf = new float[2 * cells];

        for (int k = 0; k < Levels; k++)
        {
            float g = k * step;
            Array.Clear(buf, 0, buf.Length);

            for (int i = 0; i < lum.Length; i++)
            {
                float l = lum[i];
                float wgt = Math.Max(0.0f, 1.0f - Math.Abs(l - g) / Range);
                int c = 2 * (((i / w) / d) * rw + ((i % w) / d));
                buf[c] += wgt * l;
                buf[c + 1] += wgt;
            }

            GaussBlur(buf, rw, rh, 2, sigma / d);

            int avgOffset = k * cells;
            for (int cell = 0; cell < cells; cell++)
            {
                float b0 = buf[cell * 2];
                float b1 = buf[cell * 2 + 1];
                avg[avgOffset + cell] = b1 > 1e-6f ? b0 / b1 : g;
            }
        }

        float[] output = new float[lum.Length];
        Parallel.For(0, h, y =>
        {
            float fy = Math.Clamp(((y + 0.5f) / d) - 0.5f, 0.0f, rh - 1);
            int y0 = (int)Math.Floor(fy);
            float ty = fy - y0;
            int y1 = Math.Min(y0 + 1, rh - 1);

            int rowOffset = y * w;
            for (int x = 0; x < w; x++)
            {
                float fx = Math.Clamp(((x + 0.5f) / d) - 0.5f, 0.0f, rw - 1);
                int x0 = (int)Math.Floor(fx);
                float tx = fx - x0;
                int x1 = Math.Min(x0 + 1, rw - 1);

                float At(int k)
                {
                    int kOffset = k * cells;
                    float top = avg[kOffset + y0 * rw + x0] + (avg[kOffset + y0 * rw + x1] - avg[kOffset + y0 * rw + x0]) * tx;
                    float bot = avg[kOffset + y1 * rw + x0] + (avg[kOffset + y1 * rw + x1] - avg[kOffset + y1 * rw + x0]) * tx;
                    return top + (bot - top) * ty;
                }

                float t = Math.Min(Math.Clamp(guide[rowOffset + x], 0.0f, 1.0f) / step, (Levels - 1) - 1e-4f);
                int kIdx = (int)Math.Floor(t);
                float f = t - kIdx;
                output[rowOffset + x] = At(kIdx) * (1.0f - f) + At(kIdx + 1) * f;
            }
        });

        return output;
    }

    private static void ClipLevels(float[] y2, byte[] px, float black, float white)
    {
        if (black <= 0.0f && white <= 0.0f) return;

        uint[] hist = new uint[4096];
        uint n = 0;
        for (int i = 0; i < y2.Length; i++)
        {
            if (px[i * 4 + 3] > 0)
            {
                int bin = (int)Math.Round(Math.Clamp(y2[i], 0.0f, 1.0f) * 4095.0f);
                hist[bin]++;
                n++;
            }
        }
        if (n == 0) return;

        float Find(float pct, bool rev)
        {
            uint target = (uint)(Math.Clamp(pct, 0.0f, 50.0f) / 100.0f * n);
            uint acc = 0;
            for (int k = 0; k < 4096; k++)
            {
                int i = rev ? 4095 - k : k;
                acc += hist[i];
                if (acc > target) return i / 4095.0f;
            }
            return rev ? 0.0f : 1.0f;
        }

        float lo = black > 0.0f ? Find(black, false) : 0.0f;
        float hi = white > 0.0f ? Find(white, true) : 1.0f;
        if (hi - lo < 1e-3f) return;

        for (int i = 0; i < y2.Length; i++)
        {
            y2[i] = Math.Clamp((y2[i] - lo) / (hi - lo), 0.0f, 1.0f);
        }
    }

    /// <summary>
    /// Applies Studio calibrated Shadows/Highlights adjustment to straight BGRA pixel buffer.
    /// </summary>
    public static byte[] ApplyShadowsHighlights(
        byte[] bgraPixels,
        int width,
        int height,
        double shadowAmount = 35.0,
        double shadowTone = 50.0,
        double shadowRadius = 30.0,
        double highlightAmount = 0.0,
        double highlightTone = 50.0,
        double highlightRadius = 30.0,
        double midtone = 0.0,
        double color = 0.0,
        double blackClip = 0.01,
        double whiteClip = 0.01)
    {
        if (width <= 0 || height <= 0 || bgraPixels.Length != width * height * 4)
            return (byte[])bgraPixels.Clone();

        float sa = (float)shadowAmount;
        float ha = (float)highlightAmount;
        float sr = (float)Math.Max(1.0, shadowRadius);
        float hr = (float)Math.Max(1.0, highlightRadius);
        float st = (float)shadowTone;
        float ht = (float)highlightTone;
        float mid = (float)midtone;
        float col = (float)color;
        float bClip = (float)blackClip;
        float wClip = (float)whiteClip;

        int totalPixels = width * height;
        float[] lum = new float[totalPixels];

        Parallel.For(0, totalPixels, i =>
        {
            int idx = i * 4;
            float b = bgraPixels[idx] / 255.0f;
            float g = bgraPixels[idx + 1] / 255.0f;
            float r = bgraPixels[idx + 2] / 255.0f;
            float maxC = Math.Max(r, Math.Max(g, b));
            float l = Luma(r, g, b);
            lum[i] = (maxC + l) * 0.5f;
        });

        float[]? ls = sa > 0.0f ? Neighbourhood(lum, width, height, sr) : null;
        float[]? lh = ha > 0.0f ? (Math.Abs(hr - sr) < 1e-3f && ls != null ? ls : Neighbourhood(lum, width, height, hr)) : null;

        float[] ms = TonalMask(st);
        float[] mh = TonalMask(ht);
        float ss = Strength(sa);
        float hs = Strength(ha);

        float[] y2 = new float[totalPixels];
        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = rowOffset + x;
                int idx = i * 4;
                float b = bgraPixels[idx] / 255.0f;
                float g = bgraPixels[idx + 1] / 255.0f;
                float r = bgraPixels[idx + 2] / 255.0f;

                float adjR = r, adjG = g, adjB = b;

                if (ls != null)
                {
                    int lvl = Math.Clamp((int)Math.Round(ls[i] * 255.0f), 0, 255);
                    float m = ms[lvl];
                    adjR += m * (Lift(ss, adjR) - adjR);
                    adjG += m * (Lift(ss, adjG) - adjG);
                    adjB += m * (Lift(ss, adjB) - adjB);
                }

                if (lh != null)
                {
                    int lvl = 255 - Math.Clamp((int)Math.Round(lh[i] * 255.0f), 0, 255);
                    float m = mh[lvl];
                    adjR -= m * (Lift(hs, 1.0f - r) - (1.0f - r));
                    adjG -= m * (Lift(hs, 1.0f - g) - (1.0f - g));
                    adjB -= m * (Lift(hs, 1.0f - b) - (1.0f - b));
                }

                float newLuma = Math.Clamp(Luma(adjR, adjG, adjB), 0.0f, 1.0f);
                y2[i] = Midtone(mid, newLuma);
            }
        });

        ClipLevels(y2, bgraPixels, bClip, wClip);

        float Gain(float f) => col >= 0.0f ? (float)Math.Exp(Interp(ColorUp, col) * f) : (1.0f + Interp(ColorDown, -col) * f);

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = rowOffset + x;
                int idx = i * 4;
                byte alpha = bgraPixels[idx + 3];
                if (alpha == 0)
                {
                    result[idx] = bgraPixels[idx];
                    result[idx + 1] = bgraPixels[idx + 1];
                    result[idx + 2] = bgraPixels[idx + 2];
                    result[idx + 3] = 0;
                    continue;
                }

                float b = bgraPixels[idx] / 255.0f;
                float g = bgraPixels[idx + 1] / 255.0f;
                float r = bgraPixels[idx + 2] / 255.0f;

                float y1 = Luma(r, g, b);
                float yn = y2[i];
                float f = Math.Abs(yn - y1) / Math.Max(Math.Max(yn, y1), 1e-6f);
                float gainVal = Gain(f);

                float dR = (r - y1) * gainVal;
                float dG = (g - y1) * gainVal;
                float dB = (b - y1) * gainVal;

                float k = 1.0f;
                foreach (float v in (ReadOnlySpan<float>)[dR, dG, dB])
                {
                    if (v > 1e-9f) k = Math.Min(k, (1.0f - yn) / v);
                    else if (v < -1e-9f) k = Math.Min(k, yn / -v);
                }
                k = Math.Clamp(k, 0.0f, 1.0f);

                result[idx] = (byte)Math.Clamp((int)Math.Round((yn + k * dB) * 255.0f), 0, 255);
                result[idx + 1] = (byte)Math.Clamp((int)Math.Round((yn + k * dG) * 255.0f), 0, 255);
                result[idx + 2] = (byte)Math.Clamp((int)Math.Round((yn + k * dR) * 255.0f), 0, 255);
                result[idx + 3] = alpha;
            }
        });

        return result;
    }
}
