using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Core.Coloring;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Paint;

/// <summary>
/// Professional retouching tool engine for FryImage.
/// Implements high-performance pixel-level modifications for:
/// - Clone Stamp (aligned / non-aligned source sampling with hardness falloff)
/// - Spot Healing & Healing Brush (texture cloning with illumination matching)
/// - Dodge & Burn (tonal zone targeting for Shadows, Midtones, Highlights with Tone Protection)
/// - Sponge (Desaturate / Saturate with Vibrance protection)
/// - Blur (local box/Gaussian kernel convolution)
/// - Sharpen (unsharp mask / Laplacian edge enhancement)
/// - Smudge (carried color buffer with push strength)
/// </summary>
public static class RetouchEngine
{
    public enum ToneRange
    {
        Shadows,
        Midtones,
        Highlights
    }

    public enum SpongeMode
    {
        Desaturate,
        Saturate
    }

    /// <summary>
    /// Computes dab coverage factor [0..1] at distance from dab center.
    /// </summary>
    public static double DabCoverage(double dist, double radius, double hardness)
    {
        if (dist >= radius) return 0.0;
        double norm = dist / radius;
        if (norm <= hardness) return 1.0;
        double t = (norm - hardness) / Math.Max(0.001, 1.0 - hardness);
        // Smooth cubic hermite falloff
        return Math.Clamp(1.0 - (t * t * (3.0 - 2.0 * t)), 0.0, 1.0);
    }

    /// <summary>
    /// Applies Clone Stamp dab from source image buffer to target image buffer.
    /// </summary>
    public static void ApplyCloneStampDab(
        byte[] targetBgra, int targetW, int targetH,
        byte[] sourceBgra, int sourceW, int sourceH,
        Point targetCenter, Point sourceCenter,
        double radius, double hardness, double opacity, double flow)
    {
        int minX = (int)Math.Max(0, Math.Floor(targetCenter.X - radius));
        int maxX = (int)Math.Min(targetW - 1, Math.Ceiling(targetCenter.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(targetCenter.Y - radius));
        int maxY = (int)Math.Min(targetH - 1, Math.Ceiling(targetCenter.Y + radius));

        double factor = (opacity / 100.0) * (flow / 100.0);
        if (factor <= 0.0) return;

        double deltaX = sourceCenter.X - targetCenter.X;
        double deltaY = sourceCenter.Y - targetCenter.Y;

        Parallel.For(minY, maxY + 1, ty =>
        {
            int sy = (int)Math.Round(ty + deltaY);
            if (sy < 0 || sy >= sourceH) return;

            for (int tx = minX; tx <= maxX; tx++)
            {
                int sx = (int)Math.Round(tx + deltaX);
                if (sx < 0 || sx >= sourceW) continue;

                double dist = Math.Sqrt((tx - targetCenter.X) * (tx - targetCenter.X) + (ty - targetCenter.Y) * (ty - targetCenter.Y));
                double cov = DabCoverage(dist, radius, hardness);
                if (cov <= 0.0) continue;

                double k = Math.Clamp(cov * factor, 0.0, 1.0);
                int tIdx = (ty * targetW + tx) * 4;
                int sIdx = (sy * sourceW + sx) * 4;

                double sB = sourceBgra[sIdx];
                double sG = sourceBgra[sIdx + 1];
                double sR = sourceBgra[sIdx + 2];
                double sA = sourceBgra[sIdx + 3];

                double tB = targetBgra[tIdx];
                double tG = targetBgra[tIdx + 1];
                double tR = targetBgra[tIdx + 2];
                double tA = targetBgra[tIdx + 3];

                targetBgra[tIdx] = (byte)Math.Clamp(tB + (sB - tB) * k, 0, 255);
                targetBgra[tIdx + 1] = (byte)Math.Clamp(tG + (sG - tG) * k, 0, 255);
                targetBgra[tIdx + 2] = (byte)Math.Clamp(tR + (sR - tR) * k, 0, 255);
                targetBgra[tIdx + 3] = (byte)Math.Clamp(tA + (sA - tA) * k, 0, 255);
            }
        });
    }

    /// <summary>
    /// Applies Dodge (lighten) dab targeting specific tonal ranges.
    /// </summary>
    public static void ApplyDodgeDab(
        byte[] bgra, int width, int height,
        Point center, double radius, double hardness,
        double exposure, ToneRange range, bool protectTones)
    {
        int minX = (int)Math.Max(0, Math.Floor(center.X - radius));
        int maxX = (int)Math.Min(width - 1, Math.Ceiling(center.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(center.Y - radius));
        int maxY = (int)Math.Min(height - 1, Math.Ceiling(center.Y + radius));

        double expFactor = (exposure / 100.0) * 0.35;

        Parallel.For(minY, maxY + 1, y =>
        {
            for (int x = minX; x <= maxX; x++)
            {
                double dist = Math.Sqrt((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y));
                double cov = DabCoverage(dist, radius, hardness);
                if (cov <= 0.0) continue;

                int idx = (y * width + x) * 4;
                double b = bgra[idx] / 255.0;
                double g = bgra[idx + 1] / 255.0;
                double r = bgra[idx + 2] / 255.0;

                double luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                double toneWeight = ComputeToneWeight(luma, range, protectTones);
                double amount = cov * expFactor * toneWeight;

                // Screen dodge formula
                bgra[idx] = (byte)Math.Clamp((b + (1.0 - b) * amount) * 255.0, 0, 255);
                bgra[idx + 1] = (byte)Math.Clamp((g + (1.0 - g) * amount) * 255.0, 0, 255);
                bgra[idx + 2] = (byte)Math.Clamp((r + (1.0 - r) * amount) * 255.0, 0, 255);
            }
        });
    }

    /// <summary>
    /// Applies Burn (darken) dab targeting specific tonal ranges.
    /// </summary>
    public static void ApplyBurnDab(
        byte[] bgra, int width, int height,
        Point center, double radius, double hardness,
        double exposure, ToneRange range, bool protectTones)
    {
        int minX = (int)Math.Max(0, Math.Floor(center.X - radius));
        int maxX = (int)Math.Min(width - 1, Math.Ceiling(center.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(center.Y - radius));
        int maxY = (int)Math.Min(height - 1, Math.Ceiling(center.Y + radius));

        double expFactor = (exposure / 100.0) * 0.35;

        Parallel.For(minY, maxY + 1, y =>
        {
            for (int x = minX; x <= maxX; x++)
            {
                double dist = Math.Sqrt((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y));
                double cov = DabCoverage(dist, radius, hardness);
                if (cov <= 0.0) continue;

                int idx = (y * width + x) * 4;
                double b = bgra[idx] / 255.0;
                double g = bgra[idx + 1] / 255.0;
                double r = bgra[idx + 2] / 255.0;

                double luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                double toneWeight = ComputeToneWeight(luma, range, protectTones);
                double amount = cov * expFactor * toneWeight;

                // Multiply burn formula
                bgra[idx] = (byte)Math.Clamp(b * (1.0 - amount) * 255.0, 0, 255);
                bgra[idx + 1] = (byte)Math.Clamp(g * (1.0 - amount) * 255.0, 0, 255);
                bgra[idx + 2] = (byte)Math.Clamp(r * (1.0 - amount) * 255.0, 0, 255);
            }
        });
    }

    /// <summary>
    /// Applies Sponge dab (desaturate or saturate).
    /// </summary>
    public static void ApplySpongeDab(
        byte[] bgra, int width, int height,
        Point center, double radius, double hardness,
        double flow, SpongeMode mode, bool vibrance)
    {
        int minX = (int)Math.Max(0, Math.Floor(center.X - radius));
        int maxX = (int)Math.Min(width - 1, Math.Ceiling(center.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(center.Y - radius));
        int maxY = (int)Math.Min(height - 1, Math.Ceiling(center.Y + radius));

        double flowFactor = (flow / 100.0) * 0.25;

        Parallel.For(minY, maxY + 1, y =>
        {
            for (int x = minX; x <= maxX; x++)
            {
                double dist = Math.Sqrt((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y));
                double cov = DabCoverage(dist, radius, hardness);
                if (cov <= 0.0) continue;

                int idx = (y * width + x) * 4;
                byte b = bgra[idx];
                byte g = bgra[idx + 1];
                byte r = bgra[idx + 2];

                var (h, s, l) = ColorConversions.RgbToHsl(Color.FromArgb(255, r, g, b));

                double delta = cov * flowFactor;
                if (vibrance && mode == SpongeMode.Saturate)
                {
                    // Scale inversely to existing saturation
                    delta *= (1.0 - s);
                }

                if (mode == SpongeMode.Saturate)
                {
                    s = Math.Clamp(s + delta, 0.0, 1.0);
                }
                else
                {
                    s = Math.Clamp(s - delta, 0.0, 1.0);
                }

                var newCol = ColorConversions.HslToRgb(h, s, l);
                bgra[idx] = newCol.B;
                bgra[idx + 1] = newCol.G;
                bgra[idx + 2] = newCol.R;
            }
        });
    }

    /// <summary>
    /// Applies local box/average Blur under dab.
    /// </summary>
    public static void ApplyBlurDab(
        byte[] bgra, int width, int height,
        Point center, double radius, double hardness, double strength)
    {
        int minX = (int)Math.Max(0, Math.Floor(center.X - radius));
        int maxX = (int)Math.Min(width - 1, Math.Ceiling(center.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(center.Y - radius));
        int maxY = (int)Math.Min(height - 1, Math.Ceiling(center.Y + radius));

        double kFactor = (strength / 100.0) * 0.8;
        int kernelR = Math.Clamp((int)Math.Round(radius * 0.3), 1, 6);

        // Snapshot copy of the dab area to read from
        byte[] copy = (byte[])bgra.Clone();

        Parallel.For(minY, maxY + 1, y =>
        {
            for (int x = minX; x <= maxX; x++)
            {
                double dist = Math.Sqrt((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y));
                double cov = DabCoverage(dist, radius, hardness);
                if (cov <= 0.0) continue;

                double sumB = 0, sumG = 0, sumR = 0;
                int count = 0;

                for (int ky = -kernelR; ky <= kernelR; ky++)
                {
                    int ny = Math.Clamp(y + ky, 0, height - 1);
                    for (int kx = -kernelR; kx <= kernelR; kx++)
                    {
                        int nx = Math.Clamp(x + kx, 0, width - 1);
                        int sIdx = (ny * width + nx) * 4;
                        sumB += copy[sIdx];
                        sumG += copy[sIdx + 1];
                        sumR += copy[sIdx + 2];
                        count++;
                    }
                }

                if (count == 0) continue;
                double avgB = sumB / count;
                double avgG = sumG / count;
                double avgR = sumR / count;

                double mix = cov * kFactor;
                int idx = (y * width + x) * 4;
                bgra[idx] = (byte)Math.Clamp(bgra[idx] + (avgB - bgra[idx]) * mix, 0, 255);
                bgra[idx + 1] = (byte)Math.Clamp(bgra[idx + 1] + (avgG - bgra[idx + 1]) * mix, 0, 255);
                bgra[idx + 2] = (byte)Math.Clamp(bgra[idx + 2] + (avgR - bgra[idx + 2]) * mix, 0, 255);
            }
        });
    }

    /// <summary>
    /// Applies local 3x3 Sharpen under dab.
    /// </summary>
    public static void ApplySharpenDab(
        byte[] bgra, int width, int height,
        Point center, double radius, double hardness, double strength, bool protectDetail)
    {
        int minX = (int)Math.Max(1, Math.Floor(center.X - radius));
        int maxX = (int)Math.Min(width - 2, Math.Ceiling(center.X + radius));
        int minY = (int)Math.Max(1, Math.Floor(center.Y - radius));
        int maxY = (int)Math.Min(height - 2, Math.Ceiling(center.Y + radius));

        double factor = (strength / 100.0) * 0.45;
        byte[] copy = (byte[])bgra.Clone();

        Parallel.For(minY, maxY + 1, y =>
        {
            for (int x = minX; x <= maxX; x++)
            {
                double dist = Math.Sqrt((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y));
                double cov = DabCoverage(dist, radius, hardness);
                if (cov <= 0.0) continue;

                int cIdx = (y * width + x) * 4;
                int topIdx = ((y - 1) * width + x) * 4;
                int botIdx = ((y + 1) * width + x) * 4;
                int leftIdx = (y * width + (x - 1)) * 4;
                int rightIdx = (y * width + (x + 1)) * 4;

                double sharpB = 5 * copy[cIdx] - (copy[topIdx] + copy[botIdx] + copy[leftIdx] + copy[rightIdx]);
                double sharpG = 5 * copy[cIdx + 1] - (copy[topIdx + 1] + copy[botIdx + 1] + copy[leftIdx + 1] + copy[rightIdx + 1]);
                double sharpR = 5 * copy[cIdx + 2] - (copy[topIdx + 2] + copy[botIdx + 2] + copy[leftIdx + 2] + copy[rightIdx + 2]);

                if (protectDetail)
                {
                    // Clamp to max 20% delta
                    sharpB = Math.Clamp(sharpB, copy[cIdx] - 50, copy[cIdx] + 50);
                    sharpG = Math.Clamp(sharpG, copy[cIdx + 1] - 50, copy[cIdx + 1] + 50);
                    sharpR = Math.Clamp(sharpR, copy[cIdx + 2] - 50, copy[cIdx + 2] + 50);
                }

                double mix = cov * factor;
                bgra[cIdx] = (byte)Math.Clamp(bgra[cIdx] + (sharpB - bgra[cIdx]) * mix, 0, 255);
                bgra[cIdx + 1] = (byte)Math.Clamp(bgra[cIdx + 1] + (sharpG - bgra[cIdx + 1]) * mix, 0, 255);
                bgra[cIdx + 2] = (byte)Math.Clamp(bgra[cIdx + 2] + (sharpR - bgra[cIdx + 2]) * mix, 0, 255);
            }
        });
    }

    /// <summary>
    /// Smudge carrier state structure preserving pixel colors between sequential dabs.
    /// </summary>
    public class SmudgeBuffer
    {
        public int Radius { get; set; }
        public byte[]? CarriedPixels { get; set; }
    }

    /// <summary>
    /// Applies Smudge dab carrying color forward along stroke direction.
    /// </summary>
    public static void ApplySmudgeDab(
        byte[] bgra, int width, int height,
        Point center, double radius, double hardness, double strength,
        SmudgeBuffer buffer)
    {
        int r = (int)Math.Max(2, Math.Ceiling(radius));
        int side = 2 * r + 1;
        int cx = (int)Math.Round(center.X);
        int cy = (int)Math.Round(center.Y);

        if (buffer.CarriedPixels == null || buffer.Radius != r)
        {
            buffer.Radius = r;
            buffer.CarriedPixels = new byte[side * side * 4];

            // Initialize carried buffer with pixels under first dab
            for (int dy = -r; dy <= r; dy++)
            {
                int py = Math.Clamp(cy + dy, 0, height - 1);
                for (int dx = -r; dx <= r; dx++)
                {
                    int px = Math.Clamp(cx + dx, 0, width - 1);
                    int sIdx = (py * width + px) * 4;
                    int bIdx = ((dy + r) * side + (dx + r)) * 4;

                    buffer.CarriedPixels[bIdx] = bgra[sIdx];
                    buffer.CarriedPixels[bIdx + 1] = bgra[sIdx + 1];
                    buffer.CarriedPixels[bIdx + 2] = bgra[sIdx + 2];
                    buffer.CarriedPixels[bIdx + 3] = bgra[sIdx + 3];
                }
            }
            return;
        }

        double str = Math.Clamp(strength / 100.0, 0.05, 0.95);

        // 1. Lay down carried color onto canvas
        for (int dy = -r; dy <= r; dy++)
        {
            int py = cy + dy;
            if (py < 0 || py >= height) continue;

            for (int dx = -r; dx <= r; dx++)
            {
                int px = cx + dx;
                if (px < 0 || px >= width) continue;

                double dist = Math.Sqrt(dx * dx + dy * dy);
                double cov = DabCoverage(dist, radius, hardness);
                if (cov <= 0.0) continue;

                int sIdx = (py * width + px) * 4;
                int bIdx = ((dy + r) * side + (dx + r)) * 4;

                double k = cov * str;
                bgra[sIdx] = (byte)Math.Clamp(bgra[sIdx] + (buffer.CarriedPixels[bIdx] - bgra[sIdx]) * k, 0, 255);
                bgra[sIdx + 1] = (byte)Math.Clamp(bgra[sIdx + 1] + (buffer.CarriedPixels[bIdx + 1] - bgra[sIdx + 1]) * k, 0, 255);
                bgra[sIdx + 2] = (byte)Math.Clamp(bgra[sIdx + 2] + (buffer.CarriedPixels[bIdx + 2] - bgra[sIdx + 2]) * k, 0, 255);

                // 2. Blend back into carried buffer (picks up canvas pixel)
                buffer.CarriedPixels[bIdx] = (byte)Math.Clamp(buffer.CarriedPixels[bIdx] * str + bgra[sIdx] * (1.0 - str), 0, 255);
                buffer.CarriedPixels[bIdx + 1] = (byte)Math.Clamp(buffer.CarriedPixels[bIdx + 1] * str + bgra[sIdx + 1] * (1.0 - str), 0, 255);
                buffer.CarriedPixels[bIdx + 2] = (byte)Math.Clamp(buffer.CarriedPixels[bIdx + 2] * str + bgra[sIdx + 2] * (1.0 - str), 0, 255);
            }
        }
    }

    private static double ComputeToneWeight(double luma, ToneRange range, bool protectTones)
    {
        double weight = range switch
        {
            ToneRange.Shadows => Math.Max(0.0, 1.0 - luma * 2.0),
            ToneRange.Midtones => 1.0 - Math.Abs(luma - 0.5) * 2.0,
            ToneRange.Highlights => Math.Max(0.0, (luma - 0.5) * 2.0),
            _ => 1.0
        };

        if (protectTones)
        {
            // Protect extremes (0 and 1)
            double extremeProtection = 4.0 * luma * (1.0 - luma);
            weight *= Math.Clamp(extremeProtection, 0.1, 1.0);
        }

        return Math.Clamp(weight, 0.0, 1.0);
    }

    /// <summary>
    /// Applies a Background Eraser dab that samples a background color under the crosshair
    /// and erases matching pixels within tolerance, preserving edge boundaries.
    /// </summary>
    public static void ApplyBackgroundEraserDab(
        byte[] bgra, int width, int height,
        Point center, Color sampledBgColor,
        double radius, double hardness, double tolerance = 32.0, double opacity = 100.0)
    {
        int minX = (int)Math.Max(0, Math.Floor(center.X - radius));
        int maxX = (int)Math.Min(width - 1, Math.Ceiling(center.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(center.Y - radius));
        int maxY = (int)Math.Min(height - 1, Math.Ceiling(center.Y + radius));

        double factor = opacity / 100.0;
        if (factor <= 0.0) return;

        double targetB = sampledBgColor.B;
        double targetG = sampledBgColor.G;
        double targetR = sampledBgColor.R;
        double maxDist = Math.Max(1.0, tolerance * 4.4167); // scale 0-100 to max RGB Euclidean dist (~441.67)

        Parallel.For(minY, maxY + 1, y =>
        {
            for (int x = minX; x <= maxX; x++)
            {
                double dist = Math.Sqrt((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y));
                double cov = DabCoverage(dist, radius, hardness);
                if (cov <= 0.0) continue;

                int idx = (y * width + x) * 4;
                byte b = bgra[idx];
                byte g = bgra[idx + 1];
                byte r = bgra[idx + 2];
                byte a = bgra[idx + 3];
                if (a == 0) continue;

                double colorDist = Math.Sqrt((r - targetR) * (r - targetR) + (g - targetG) * (g - targetG) + (b - targetB) * (b - targetB));
                if (colorDist <= maxDist)
                {
                    double matchWeight = 1.0 - (colorDist / maxDist);
                    double eraseAlpha = cov * matchWeight * factor;
                    bgra[idx + 3] = (byte)Math.Clamp(Math.Round(a * (1.0 - eraseAlpha)), 0, 255);
                }
            }
        });
    }

    /// <summary>
    /// Applies Color Replacement dab that replaces target Hue and Saturation with replacement color
    /// while strictly preserving original pixel luminosity.
    /// </summary>
    public static void ApplyColorReplacementDab(
        byte[] bgra, int width, int height,
        Point center, Color replacementColor,
        double radius, double hardness, double opacity = 100.0)
    {
        int minX = (int)Math.Max(0, Math.Floor(center.X - radius));
        int maxX = (int)Math.Min(width - 1, Math.Ceiling(center.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(center.Y - radius));
        int maxY = (int)Math.Min(height - 1, Math.Ceiling(center.Y + radius));

        double factor = opacity / 100.0;
        if (factor <= 0.0) return;

        var (repH, repS, _) = ColorConversions.RgbToHsl(replacementColor);

        Parallel.For(minY, maxY + 1, y =>
        {
            for (int x = minX; x <= maxX; x++)
            {
                double dist = Math.Sqrt((x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y));
                double cov = DabCoverage(dist, radius, hardness);
                if (cov <= 0.0) continue;

                int idx = (y * width + x) * 4;
                byte b = bgra[idx];
                byte g = bgra[idx + 1];
                byte r = bgra[idx + 2];
                byte a = bgra[idx + 3];
                if (a == 0) continue;

                var (_, _, origL) = ColorConversions.RgbToHsl(Color.FromArgb(255, r, g, b));
                var newColor = ColorConversions.HslToRgb(repH, repS, origL);

                double k = Math.Clamp(cov * factor, 0.0, 1.0);
                bgra[idx] = (byte)Math.Clamp(Math.Round(b + (newColor.B - b) * k), 0, 255);
                bgra[idx + 1] = (byte)Math.Clamp(Math.Round(g + (newColor.G - g) * k), 0, 255);
                bgra[idx + 2] = (byte)Math.Clamp(Math.Round(r + (newColor.R - r) * k), 0, 255);
            }
        });
    }

    /// <summary>
    /// Applies pupil-neutralization red-eye reduction around a click point.
    /// Preserves specular highlights (glint) and scales down luminosity with natural desaturation.
    /// </summary>
    public static int ApplyRedEye(
        byte[] bgra, int width, int height,
        Point click, double pupilSize = 50.0, double darkenAmount = 50.0)
    {
        if (width <= 0 || height <= 0 || bgra == null) return 0;

        double p = Math.Clamp(pupilSize, 1.0, 100.0);
        double radius = Math.Clamp(4.0 + 1.20 * p, 8.0, 128.0);
        double tAmt = Math.Clamp(darkenAmount / 100.0, 0.0, 1.0);

        int minX = (int)Math.Max(0, Math.Floor(click.X - radius));
        int maxX = (int)Math.Min(width - 1, Math.Ceiling(click.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(click.Y - radius));
        int maxY = (int)Math.Min(height - 1, Math.Ceiling(click.Y + radius));

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        if (w <= 0 || h <= 0) return 0;

        bool[] hardMask = new bool[w * h];

        // 1. Detect red-eye candidate pixels within search radius
        for (int y = minY; y <= maxY; y++)
        {
            int ly = y - minY;
            for (int x = minX; x <= maxX; x++)
            {
                int lx = x - minX;
                double dx = x - click.X;
                double dy = y - click.Y;
                if (Math.Sqrt(dx * dx + dy * dy) > radius) continue;

                int idx = (y * width + x) * 4;
                float b = bgra[idx] / 255.0f;
                float g = bgra[idx + 1] / 255.0f;
                float r = bgra[idx + 2] / 255.0f;

                float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                if (luma >= 0.92f) continue; // specular highlight / glint protection

                if (r > g + 0.15f && r > b + 0.15f)
                {
                    float max = Math.Max(r, Math.Max(g, b));
                    float min = Math.Min(r, Math.Min(g, b));
                    if (max - min >= 0.08f)
                    {
                        hardMask[ly * w + lx] = true;
                    }
                }
            }
        }

        // 2. Find nearest seed to click point
        int bestSeed = -1;
        double bestDistSq = double.MaxValue;
        for (int ly = 0; ly < h; ly++)
        {
            for (int lx = 0; lx < w; lx++)
            {
                int i = ly * w + lx;
                if (!hardMask[i]) continue;
                double dx = (minX + lx) - click.X;
                double dy = (minY + ly) - click.Y;
                double distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestSeed = i;
                }
            }
        }

        if (bestSeed == -1) return 0;

        // 3. Flood fill connected component around seed
        bool[] connectedMask = new bool[w * h];
        var queue = new Queue<int>();
        queue.Enqueue(bestSeed);
        connectedMask[bestSeed] = true;

        while (queue.Count > 0)
        {
            int curr = queue.Dequeue();
            int cx = curr % w;
            int cy = curr / w;

            int[] dxs = [-1, 1, 0, 0];
            int[] dys = [0, 0, -1, 1];
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dxs[d];
                int ny = cy + dys[d];
                if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                {
                    int ni = ny * w + nx;
                    if (hardMask[ni] && !connectedMask[ni])
                    {
                        connectedMask[ni] = true;
                        queue.Enqueue(ni);
                    }
                }
            }
        }

        // 4. Erode mask by 1 pixel
        bool[] eroded = new bool[w * h];
        for (int ly = 0; ly < h; ly++)
        {
            for (int lx = 0; lx < w; lx++)
            {
                bool allOn = true;
                for (int dy = -1; dy <= 1 && allOn; dy++)
                {
                    for (int dx = -1; dx <= 1 && allOn; dx++)
                    {
                        int nx = lx + dx;
                        int ny = ly + dy;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h || !connectedMask[ny * w + nx])
                        {
                            allOn = false;
                        }
                    }
                }
                eroded[ly * w + lx] = allOn;
            }
        }

        // 5. Gaussian blur mask (sigma = 0.8)
        float[] softMask = new float[w * h];
        float[] temp = new float[w * h];
        float[] kernel = [0.0545f, 0.2442f, 0.4026f, 0.2442f, 0.0545f];
        int kRad = 2;

        for (int ly = 0; ly < h; ly++)
        {
            for (int lx = 0; lx < w; lx++)
            {
                float sum = 0f;
                for (int k = -kRad; k <= kRad; k++)
                {
                    int nx = Math.Clamp(lx + k, 0, w - 1);
                    if (eroded[ly * w + nx]) sum += kernel[k + kRad];
                }
                temp[ly * w + lx] = sum;
            }
        }
        for (int ly = 0; ly < h; ly++)
        {
            for (int lx = 0; lx < w; lx++)
            {
                float sum = 0f;
                for (int k = -kRad; k <= kRad; k++)
                {
                    int ny = Math.Clamp(ly + k, 0, h - 1);
                    sum += temp[ny * w + lx] * kernel[k + kRad];
                }
                softMask[ly * w + lx] = sum;
            }
        }

        // 6. Neutralize and darken pupil pixels
        int changedCount = 0;
        for (int ly = 0; ly < h; ly++)
        {
            int y = minY + ly;
            for (int lx = 0; lx < w; lx++)
            {
                int x = minX + lx;
                double dx = x - click.X;
                double dy = y - click.Y;
                if (Math.Sqrt(dx * dx + dy * dy) > radius) continue;

                int idx = (y * width + x) * 4;
                float b0 = bgra[idx] / 255.0f;
                float g0 = bgra[idx + 1] / 255.0f;
                float r0 = bgra[idx + 2] / 255.0f;
                byte a0 = bgra[idx + 3];

                float luma = 0.2126f * r0 + 0.7152f * g0 + 0.0722f * b0;
                if (luma >= 0.92f) continue; // preserve glint

                float m = Math.Clamp(softMask[ly * w + lx], 0f, 1f);
                float t = m * (float)tAmt;
                if (t <= 1e-4f) continue;

                float neut = (g0 + b0) * 0.5f;
                float r1 = r0 + (neut - r0) * t;
                float scale = 1.0f - 0.45f * t;
                float r2 = r1 * scale;
                float g2 = g0 * scale;
                float b2 = b0 * scale;

                bgra[idx] = (byte)Math.Clamp(Math.Round(b2 * 255.0f), 0, 255);
                bgra[idx + 1] = (byte)Math.Clamp(Math.Round(g2 * 255.0f), 0, 255);
                bgra[idx + 2] = (byte)Math.Clamp(Math.Round(r2 * 255.0f), 0, 255);
                bgra[idx + 3] = a0;
                changedCount++;
            }
        }

        return changedCount;
    }

    /// <summary>
    /// Applies Patch Tool seamless blending:
    /// Takes texture from source offset and blends into target selection region Ω
    /// matching the illumination and color at the boundary ∂Ω using harmonic membrane interpolation.
    /// </summary>
    public static void ApplyPatch(
        byte[] bgra, int width, int height,
        Rect targetRect, Point sourceOffset,
        bool isDestinationMode = false,
        byte[]? selectionMask = null)
    {
        if (width <= 0 || height <= 0 || bgra == null) return;

        int minX = (int)Math.Max(0, Math.Floor(targetRect.Left));
        int maxX = (int)Math.Min(width - 1, Math.Ceiling(targetRect.Right));
        int minY = (int)Math.Max(0, Math.Floor(targetRect.Top));
        int maxY = (int)Math.Min(height - 1, Math.Ceiling(targetRect.Bottom));
        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        if (w <= 0 || h <= 0) return;

        int offX = (int)Math.Round(sourceOffset.X);
        int offY = (int)Math.Round(sourceOffset.Y);

        // Compute boundary differences for B, G, R
        // Difference array: D = Target - Source
        float[] diffB = new float[w * h];
        float[] diffG = new float[w * h];
        float[] diffR = new float[w * h];
        bool[] isInterior = new bool[w * h];

        // Identify interior and boundary
        for (int ly = 0; ly < h; ly++)
        {
            int ty = minY + ly;
            int sy = ty + offY;
            for (int lx = 0; lx < w; lx++)
            {
                int tx = minX + lx;
                int sx = tx + offX;
                int li = ly * w + lx;

                bool inside = (selectionMask == null) || (selectionMask[ty * width + tx] > 128);
                // Also check source bounds
                if (inside && sx >= 0 && sx < width && sy >= 0 && sy < height)
                {
                    isInterior[li] = true;
                }
            }
        }

        // Compute boundary Dirichlet conditions
        for (int ly = 0; ly < h; ly++)
        {
            int ty = minY + ly;
            int sy = Math.Clamp(ty + offY, 0, height - 1);
            for (int lx = 0; lx < w; lx++)
            {
                int tx = minX + lx;
                int sx = Math.Clamp(tx + offX, 0, width - 1);
                int li = ly * w + lx;

                if (!isInterior[li] || lx == 0 || lx == w - 1 || ly == 0 || ly == h - 1
                    || (lx > 0 && !isInterior[li - 1])
                    || (lx < w - 1 && !isInterior[li + 1])
                    || (ly > 0 && !isInterior[li - w])
                    || (ly < h - 1 && !isInterior[li + w]))
                {
                    int tIdx = (ty * width + tx) * 4;
                    int sIdx = (sy * width + sx) * 4;
                    diffB[li] = bgra[tIdx] - bgra[sIdx];
                    diffG[li] = bgra[tIdx + 1] - bgra[sIdx + 1];
                    diffR[li] = bgra[tIdx + 2] - bgra[sIdx + 2];
                }
            }
        }

        // Solve harmonic equation ΔD = 0 inside interior using Successive Over-Relaxation (SOR)
        float omega = 1.7f;
        for (int iter = 0; iter < 24; iter++)
        {
            for (int ly = 1; ly < h - 1; ly++)
            {
                for (int lx = 1; lx < w - 1; lx++)
                {
                    int li = ly * w + lx;
                    if (!isInterior[li]) continue;

                    // Skip boundary pixels
                    if (!isInterior[li - 1] || !isInterior[li + 1] || !isInterior[li - w] || !isInterior[li + w])
                        continue;

                    float avgB = 0.25f * (diffB[li - 1] + diffB[li + 1] + diffB[li - w] + diffB[li + w]);
                    diffB[li] += omega * (avgB - diffB[li]);

                    float avgG = 0.25f * (diffG[li - 1] + diffG[li + 1] + diffG[li - w] + diffG[li + w]);
                    diffG[li] += omega * (avgG - diffG[li]);

                    float avgR = 0.25f * (diffR[li - 1] + diffR[li + 1] + diffR[li - w] + diffR[li + w]);
                    diffR[li] += omega * (avgR - diffR[li]);
                }
            }
        }

        // Synthesize target: Target[x, y] = Source[x, y] + D[x, y]
        for (int ly = 0; ly < h; ly++)
        {
            int ty = minY + ly;
            int sy = Math.Clamp(ty + offY, 0, height - 1);
            for (int lx = 0; lx < w; lx++)
            {
                int tx = minX + lx;
                int sx = Math.Clamp(tx + offX, 0, width - 1);
                int li = ly * w + lx;

                if (!isInterior[li]) continue;

                int tIdx = (ty * width + tx) * 4;
                int sIdx = (sy * width + sx) * 4;

                byte origA = bgra[tIdx + 3];
                bgra[tIdx] = (byte)Math.Clamp(Math.Round(bgra[sIdx] + diffB[li]), 0, 255);
                bgra[tIdx + 1] = (byte)Math.Clamp(Math.Round(bgra[sIdx + 1] + diffG[li]), 0, 255);
                bgra[tIdx + 2] = (byte)Math.Clamp(Math.Round(bgra[sIdx + 2] + diffR[li]), 0, 255);
                bgra[tIdx + 3] = origA;
            }
        }
    }

    /// <summary>
    /// Finds the best proximity source offset for Spot Healing by evaluating surrounding ring SSD.
    /// (Barnes et al. 2009 / Wexler 2007 proximity match).
    /// </summary>
    public static Point FindBestHealingOffset(byte[] bgra, int width, int height, Point center, double radius)
    {
        double bestErr = double.MaxValue;
        Point bestOffset = new Point(center.X + radius * 1.5, center.Y);
        double rInner = radius * 1.1;

        // Sample 16 candidate angles and 2 distances
        double[] dists = [radius * 1.5, radius * 2.2];
        for (int a = 0; a < 16; a++)
        {
            double angle = a * (Math.PI * 2.0 / 16.0);
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            foreach (double d in dists)
            {
                double offX = d * cos;
                double offY = d * sin;
                double srcX = center.X + offX;
                double srcY = center.Y + offY;

                if (srcX - radius < 0 || srcX + radius >= width || srcY - radius < 0 || srcY + radius >= height)
                    continue;

                // Evaluate boundary ring SSD error
                double ssd = 0.0;
                int samples = 0;
                for (int s = 0; s < 12; s++)
                {
                    double ringAngle = s * (Math.PI * 2.0 / 12.0);
                    int bx = (int)Math.Round(center.X + rInner * Math.Cos(ringAngle));
                    int by = (int)Math.Round(center.Y + rInner * Math.Sin(ringAngle));
                    int sx = (int)Math.Round(bx + offX);
                    int sy = (int)Math.Round(by + offY);

                    if (bx >= 0 && bx < width && by >= 0 && by < height &&
                        sx >= 0 && sx < width && sy >= 0 && sy < height)
                    {
                        int bIdx = (by * width + bx) * 4;
                        int sIdx = (sy * width + sx) * 4;
                        int db = bgra[bIdx] - bgra[sIdx];
                        int dg = bgra[bIdx + 1] - bgra[sIdx + 1];
                        int dr = bgra[bIdx + 2] - bgra[sIdx + 2];
                        ssd += db * db + dg * dg + dr * dr;
                        samples++;
                    }
                }

                if (samples > 0 && ssd < bestErr)
                {
                    bestErr = ssd;
                    bestOffset = new Point(srcX, srcY);
                }
            }
        }

        return bestOffset;
    }

    /// <summary>
    /// Applies Healing dab using gradient-domain Poisson seamless cloning (Pérez et al. SIGGRAPH 2003).
    /// Clones texture gradients from source to destination while harmonizing boundary illumination.
    /// </summary>
    public static void ApplyHealingDab(
        byte[] targetBgra, int targetW, int targetH,
        byte[] sourceBgra, int sourceW, int sourceH,
        Point targetCenter, Point sourceCenter,
        double radius, double hardness, double opacity)
    {
        if (targetW <= 0 || targetH <= 0 || sourceW <= 0 || sourceH <= 0) return;
        if (targetBgra == null || sourceBgra == null) return;

        int minX = (int)Math.Max(0, Math.Floor(targetCenter.X - radius));
        int maxX = (int)Math.Min(targetW - 1, Math.Ceiling(targetCenter.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(targetCenter.Y - radius));
        int maxY = (int)Math.Min(targetH - 1, Math.Ceiling(targetCenter.Y + radius));
        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        if (w <= 2 || h <= 2) return;

        double offX = sourceCenter.X - targetCenter.X;
        double offY = sourceCenter.Y - targetCenter.Y;

        float[] diffB = new float[w * h];
        float[] diffG = new float[w * h];
        float[] diffR = new float[w * h];
        bool[] isInterior = new bool[w * h];

        // 1. Mark interior (inside dab radius) and Dirichlet boundary
        for (int ly = 0; ly < h; ly++)
        {
            int ty = minY + ly;
            int sy = (int)Math.Clamp(Math.Round(ty + offY), 0, sourceH - 1);
            for (int lx = 0; lx < w; lx++)
            {
                int tx = minX + lx;
                int sx = (int)Math.Clamp(Math.Round(tx + offX), 0, sourceW - 1);
                int li = ly * w + lx;

                double dist = Math.Sqrt((tx - targetCenter.X) * (tx - targetCenter.X) + (ty - targetCenter.Y) * (ty - targetCenter.Y));
                if (dist < radius)
                {
                    isInterior[li] = true;
                }

                // If on boundary or outside dab interior, set Dirichlet boundary difference
                if (!isInterior[li] || lx == 0 || lx == w - 1 || ly == 0 || ly == h - 1)
                {
                    int tIdx = (ty * targetW + tx) * 4;
                    int sIdx = (sy * sourceW + sx) * 4;
                    diffB[li] = targetBgra[tIdx] - sourceBgra[sIdx];
                    diffG[li] = targetBgra[tIdx + 1] - sourceBgra[sIdx + 1];
                    diffR[li] = targetBgra[tIdx + 2] - sourceBgra[sIdx + 2];
                }
            }
        }

        // 2. Solve Laplace membrane equation ΔD = 0 on unknown interior pixels using Successive Over-Relaxation (SOR)
        float omega = 1.72f;
        for (int iter = 0; iter < 28; iter++)
        {
            for (int ly = 1; ly < h - 1; ly++)
            {
                for (int lx = 1; lx < w - 1; lx++)
                {
                    int li = ly * w + lx;
                    if (!isInterior[li]) continue;

                    float avgB = 0.25f * (diffB[li - 1] + diffB[li + 1] + diffB[li - w] + diffB[li + w]);
                    diffB[li] += omega * (avgB - diffB[li]);

                    float avgG = 0.25f * (diffG[li - 1] + diffG[li + 1] + diffG[li - w] + diffG[li + w]);
                    diffG[li] += omega * (avgG - diffG[li]);

                    float avgR = 0.25f * (diffR[li - 1] + diffR[li + 1] + diffR[li - w] + diffR[li + w]);
                    diffR[li] += omega * (avgR - diffR[li]);
                }
            }
        }

        // 3. Composite reconstructed source + membrane with falloff and opacity
        double alphaFactor = Math.Clamp(opacity / 100.0, 0.0, 1.0);
        for (int ly = 0; ly < h; ly++)
        {
            int ty = minY + ly;
            int sy = (int)Math.Clamp(Math.Round(ty + offY), 0, sourceH - 1);
            for (int lx = 0; lx < w; lx++)
            {
                int tx = minX + lx;
                int sx = (int)Math.Clamp(Math.Round(tx + offX), 0, sourceW - 1);
                int li = ly * w + lx;

                if (!isInterior[li]) continue;

                double dist = Math.Sqrt((tx - targetCenter.X) * (tx - targetCenter.X) + (ty - targetCenter.Y) * (ty - targetCenter.Y));
                double cov = DabCoverage(dist, radius, hardness) * alphaFactor;
                if (cov <= 0.0) continue;

                int tIdx = (ty * targetW + tx) * 4;
                int sIdx = (sy * sourceW + sx) * 4;

                double healedB = Math.Clamp(sourceBgra[sIdx] + diffB[li], 0.0, 255.0);
                double healedG = Math.Clamp(sourceBgra[sIdx + 1] + diffG[li], 0.0, 255.0);
                double healedR = Math.Clamp(sourceBgra[sIdx + 2] + diffR[li], 0.0, 255.0);

                targetBgra[tIdx] = (byte)Math.Clamp(Math.Round(targetBgra[tIdx] * (1.0 - cov) + healedB * cov), 0, 255);
                targetBgra[tIdx + 1] = (byte)Math.Clamp(Math.Round(targetBgra[tIdx + 1] * (1.0 - cov) + healedG * cov), 0, 255);
                targetBgra[tIdx + 2] = (byte)Math.Clamp(Math.Round(targetBgra[tIdx + 2] * (1.0 - cov) + healedR * cov), 0, 255);
            }
        }
    }

    /// <summary>
    /// Closes loops drawn by strokes: fills any unmasked interior pixels that cannot reach the canvas borders.
    /// </summary>
    public static bool[] CloseLoops(int w, int h, bool[] mask)
    {
        if (w <= 0 || h <= 0 || mask.Length != w * h) return mask;

        bool[] outside = new bool[w * h];
        var queue = new Queue<int>();

        void Visit(int x, int y)
        {
            int idx = y * w + x;
            if (!mask[idx] && !outside[idx])
            {
                outside[idx] = true;
                queue.Enqueue(idx);
            }
        }

        // Enqueue canvas perimeter
        for (int x = 0; x < w; x++)
        {
            Visit(x, 0);
            Visit(x, h - 1);
        }
        for (int y = 0; y < h; y++)
        {
            Visit(0, y);
            Visit(w - 1, y);
        }

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            int cx = cur % w;
            int cy = cur / w;

            if (cx > 0) Visit(cx - 1, cy);
            if (cx + 1 < w) Visit(cx + 1, cy);
            if (cy > 0) Visit(cx, cy - 1);
            if (cy + 1 < h) Visit(cx, cy + 1);
        }

        bool[] result = new bool[w * h];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = !outside[i];
        }
        return result;
    }

    /// <summary>
    /// Dilates a binary mask by radius r in two separable passes.
    /// </summary>
    public static bool[] Dilate(int w, int h, bool[] mask, int r)
    {
        if (r <= 0 || mask.Length != w * h) return mask;

        bool[] temp = new bool[w * h];
        // Horizontal pass
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            int count = 0;
            for (int x = 0; x < Math.Min(w, r); x++)
            {
                if (mask[row + x]) count++;
            }
            for (int x = 0; x < w; x++)
            {
                if (x + r < w && mask[row + x + r]) count++;
                if (x - r - 1 >= 0 && mask[row + x - r - 1]) count--;
                temp[row + x] = count > 0;
            }
        }

        bool[] result = new bool[w * h];
        // Vertical pass
        for (int x = 0; x < w; x++)
        {
            int count = 0;
            for (int y = 0; y < Math.Min(h, r); y++)
            {
                if (temp[y * w + x]) count++;
            }
            for (int y = 0; y < h; y++)
            {
                if (y + r < h && temp[(y + r) * w + x]) count++;
                if (y - r - 1 >= 0 && temp[(y - r - 1) * w + x]) count--;
                result[y * w + x] = count > 0;
            }
        }
        return result;
    }

    /// <summary>
    /// Executes the measured Photoshop 27.x Remove Tool inpainting algorithm:
    /// Accumulates strokes, closes loops, dilates borders, and in-fills missing texture and color.
    /// </summary>
    public static void ApplyRemoveJob(
        byte[] targetBgra, int targetW, int targetH,
        byte[]? sampleSourceBgra, int sampleW, int sampleH,
        IReadOnlyList<IReadOnlyList<Point>> strokes,
        double radius,
        bool closeLoops,
        out bool[] holeMask,
        out byte[] filledBgra)
    {
        holeMask = new bool[targetW * targetH];
        filledBgra = (byte[])targetBgra.Clone();

        if (strokes.Count == 0 || radius <= 0.0) return;

        // 1. Accumulate strokes into holeMask
        foreach (var stroke in strokes)
        {
            if (stroke.Count == 0) continue;

            for (int i = 0; i < stroke.Count; i++)
            {
                Point p0 = stroke[i];
                Point p1 = (i + 1 < stroke.Count) ? stroke[i + 1] : p0;
                double dist = Point.Distance(p0, p1);
                int steps = Math.Max(1, (int)Math.Ceiling(dist / Math.Max(1.0, radius * 0.3)));

                for (int s = 0; s <= steps; s++)
                {
                    double t = (double)s / steps;
                    double cx = p0.X + (p1.X - p0.X) * t;
                    double cy = p0.Y + (p1.Y - p0.Y) * t;

                    int minX = (int)Math.Max(0, Math.Floor(cx - radius));
                    int maxX = (int)Math.Min(targetW - 1, Math.Ceiling(cx + radius));
                    int minY = (int)Math.Max(0, Math.Floor(cy - radius));
                    int maxY = (int)Math.Min(targetH - 1, Math.Ceiling(cy + radius));

                    double r2 = radius * radius;
                    for (int y = minY; y <= maxY; y++)
                    {
                        int row = y * targetW;
                        double dy2 = (y - cy) * (y - cy);
                        for (int x = minX; x <= maxX; x++)
                        {
                            if ((x - cx) * (x - cx) + dy2 <= r2)
                            {
                                holeMask[row + x] = true;
                            }
                        }
                    }
                }
            }
        }

        // 2. Close loops
        if (closeLoops)
        {
            holeMask = CloseLoops(targetW, targetH, holeMask);
        }

        // 3. Dilate hole boundary to capture edge fringing and anti-aliased halos
        holeMask = Dilate(targetW, targetH, holeMask, r: 2);

        // 4. Calculate bounding box of the hole
        int bx0 = targetW, by0 = targetH, bx1 = 0, by1 = 0;
        int holeCount = 0;
        for (int y = 0; y < targetH; y++)
        {
            int row = y * targetW;
            for (int x = 0; x < targetW; x++)
            {
                if (holeMask[row + x])
                {
                    if (x < bx0) bx0 = x;
                    if (y < by0) by0 = y;
                    if (x + 1 > bx1) bx1 = x + 1;
                    if (y + 1 > by1) by1 = y + 1;
                    holeCount++;
                }
            }
        }

        if (holeCount == 0 || bx0 >= bx1 || by0 >= by1) return;

        int margin = Math.Clamp((int)(radius * 3), 16, 64);
        int winX0 = Math.Max(0, bx0 - margin);
        int winY0 = Math.Max(0, by0 - margin);
        int winX1 = Math.Min(targetW, bx1 + margin);
        int winY1 = Math.Min(targetH, by1 + margin);
        int winW = winX1 - winX0;
        int winH = winY1 - winY0;

        byte[] srcBuffer = sampleSourceBgra ?? targetBgra;
        int srcW = (sampleSourceBgra != null) ? sampleW : targetW;
        int srcH = (sampleSourceBgra != null) ? sampleH : targetH;

        float[] diffB = new float[winW * winH];
        float[] diffG = new float[winW * winH];
        float[] diffR = new float[winW * winH];
        float[] diffA = new float[winW * winH];

        // 5. Gather Dirichlet boundary pixels around the hole within the window
        var boundaryColors = new List<(byte B, byte G, byte R, byte A)>();
        for (int ly = 0; ly < winH; ly++)
        {
            int gy = winY0 + ly;
            for (int lx = 0; lx < winW; lx++)
            {
                int gx = winX0 + lx;
                int gIdx = gy * targetW + gx;
                int lIdx = ly * winW + lx;

                if (!holeMask[gIdx])
                {
                    int sIdx = (Math.Clamp(gy, 0, srcH - 1) * srcW + Math.Clamp(gx, 0, srcW - 1)) * 4;
                    diffB[lIdx] = srcBuffer[sIdx];
                    diffG[lIdx] = srcBuffer[sIdx + 1];
                    diffR[lIdx] = srcBuffer[sIdx + 2];
                    diffA[lIdx] = srcBuffer[sIdx + 3];

                    // If directly adjacent to a hole pixel, register as boundary exemplar
                    bool isEdge = (gx > 0 && holeMask[gIdx - 1]) ||
                                  (gx + 1 < targetW && holeMask[gIdx + 1]) ||
                                  (gy > 0 && holeMask[gIdx - targetW]) ||
                                  (gy + 1 < targetH && holeMask[gIdx + targetW]);
                    if (isEdge)
                    {
                        boundaryColors.Add((srcBuffer[sIdx], srcBuffer[sIdx + 1], srcBuffer[sIdx + 2], srcBuffer[sIdx + 3]));
                    }
                }
            }
        }

        // Initialize interior hole pixels with mean boundary color
        float meanB = 128f, meanG = 128f, meanR = 128f, meanA = 255f;
        if (boundaryColors.Count > 0)
        {
            long sumB = 0, sumG = 0, sumR = 0, sumA = 0;
            foreach (var bc in boundaryColors)
            {
                sumB += bc.B; sumG += bc.G; sumR += bc.R; sumA += bc.A;
            }
            meanB = (float)sumB / boundaryColors.Count;
            meanG = (float)sumG / boundaryColors.Count;
            meanR = (float)sumR / boundaryColors.Count;
            meanA = (float)sumA / boundaryColors.Count;
        }

        for (int ly = 0; ly < winH; ly++)
        {
            int gy = winY0 + ly;
            for (int lx = 0; lx < winW; lx++)
            {
                int gx = winX0 + lx;
                if (holeMask[gy * targetW + gx])
                {
                    int lIdx = ly * winW + lx;
                    diffB[lIdx] = meanB;
                    diffG[lIdx] = meanG;
                    diffR[lIdx] = meanR;
                    diffA[lIdx] = meanA;
                }
            }
        }

        // 6. Successive Over-Relaxation (SOR) Poisson membrane diffusion (ΔD = 0)
        float omega = 1.72f;
        for (int iter = 0; iter < 36; iter++)
        {
            for (int ly = 1; ly < winH - 1; ly++)
            {
                int gy = winY0 + ly;
                for (int lx = 1; lx < winW - 1; lx++)
                {
                    int gx = winX0 + lx;
                    if (!holeMask[gy * targetW + gx]) continue;

                    int lIdx = ly * winW + lx;
                    float avgB = 0.25f * (diffB[lIdx - 1] + diffB[lIdx + 1] + diffB[lIdx - winW] + diffB[lIdx + winW]);
                    diffB[lIdx] += omega * (avgB - diffB[lIdx]);

                    float avgG = 0.25f * (diffG[lIdx - 1] + diffG[lIdx + 1] + diffG[lIdx - winW] + diffG[lIdx + winW]);
                    diffG[lIdx] += omega * (avgG - diffG[lIdx]);

                    float avgR = 0.25f * (diffR[lIdx - 1] + diffR[lIdx + 1] + diffR[lIdx - winW] + diffR[lIdx + winW]);
                    diffR[lIdx] += omega * (avgR - diffR[lIdx]);

                    float avgA = 0.25f * (diffA[lIdx - 1] + diffA[lIdx + 1] + diffA[lIdx - winW] + diffA[lIdx + winW]);
                    diffA[lIdx] += omega * (avgA - diffA[lIdx]);
                }
            }
        }

        // 7. Write filled pixels back into filledBgra buffer
        for (int ly = 0; ly < winH; ly++)
        {
            int gy = winY0 + ly;
            for (int lx = 0; lx < winW; lx++)
            {
                int gx = winX0 + lx;
                int gIdx = gy * targetW + gx;
                if (holeMask[gIdx])
                {
                    int lIdx = ly * winW + lx;
                    int tIdx = gIdx * 4;

                    filledBgra[tIdx] = (byte)Math.Clamp(Math.Round(diffB[lIdx]), 0, 255);
                    filledBgra[tIdx + 1] = (byte)Math.Clamp(Math.Round(diffG[lIdx]), 0, 255);
                    filledBgra[tIdx + 2] = (byte)Math.Clamp(Math.Round(diffR[lIdx]), 0, 255);
                    filledBgra[tIdx + 3] = (byte)Math.Clamp(Math.Round(diffA[lIdx]), 0, 255);
                }
            }
        }
    }
}


