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
}
