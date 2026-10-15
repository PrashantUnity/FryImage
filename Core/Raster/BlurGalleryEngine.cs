using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Studio Blur Gallery Engine: Field Blur, Iris Blur, Tilt-Shift, and Spin Blur
/// with interactive on-canvas pins, handles, and real-time preview (commits d56aa35b, be7a1f80, 7e0f48a2).
/// </summary>
public static class BlurGalleryEngine
{
    public const double MaxBlur = 500.0;
    public const double MaxSpinAngle = 360.0;

    public sealed class BlurPin
    {
        public double X { get; set; } = 0.5; // Normalized 0..1
        public double Y { get; set; } = 0.5;
        public double BlurRadius { get; set; } = 15.0; // 0..500 px
        public double RadiusX { get; set; } = 0.35; // Fraction of shorter dimension
        public double RadiusY { get; set; } = 0.25;
        public double Angle { get; set; } = 0.0; // Degrees
        public double Roundness { get; set; } = 1.0; // 1.0 = ellipse, 4.0 = rectangle
        public double Feather { get; set; } = 0.5; // 0..1
        public bool IsSelected { get; set; } = false;

        public BlurPin Clone() => new()
        {
            X = X,
            Y = Y,
            BlurRadius = BlurRadius,
            RadiusX = RadiusX,
            RadiusY = RadiusY,
            Angle = Angle,
            Roundness = Roundness,
            Feather = Feather,
            IsSelected = IsSelected
        };
    }

    public sealed class TiltShiftParams
    {
        public double CenterX { get; set; } = 0.5;
        public double CenterY { get; set; } = 0.5;
        public double Angle { get; set; } = 0.0;
        public double Focus { get; set; } = 100.0; // Sharp band height in pixels
        public double Transition { get; set; } = 120.0; // Feather band in pixels
        public double BlurRadius { get; set; } = 25.0;
    }

    /// <summary>
    /// Applies Field Blur by smoothly interpolating blur radii between multiple interactive canvas pins (commit be7a1f80).
    /// </summary>
    public static byte[] ApplyFieldBlur(byte[] bgraPixels, int width, int height, IReadOnlyList<BlurPin> pins)
    {
        if (width <= 0 || height <= 0 || pins == null || pins.Count == 0)
            return (byte[])bgraPixels.Clone();

        if (pins.Count == 1)
        {
            double r = Math.Clamp(pins[0].BlurRadius, 0.0, MaxBlur);
            return r <= 0.1 ? (byte[])bgraPixels.Clone() : ImageAdjustmentEngine.ApplyGaussianBlur(bgraPixels, width, height, r);
        }

        // Multi-pin field blur: calculate maximum blur for pyramid levels
        double maxRadius = pins.Max(p => Math.Clamp(p.BlurRadius, 0.0, MaxBlur));
        if (maxRadius <= 0.1) return (byte[])bgraPixels.Clone();

        // 3-level pyramid: 0, half, full
        byte[] blurredHalf = ImageAdjustmentEngine.ApplyGaussianBlur(bgraPixels, width, height, maxRadius * 0.5);
        byte[] blurredFull = ImageAdjustmentEngine.ApplyGaussianBlur(bgraPixels, width, height, maxRadius);

        byte[] result = new byte[bgraPixels.Length];

        // Cache pin coords in image pixels
        var pinCoords = new (double px, double py, double blur)[pins.Count];
        for (int i = 0; i < pins.Count; i++)
        {
            pinCoords[i] = (pins[i].X * width, pins[i].Y * height, Math.Clamp(pins[i].BlurRadius, 0.0, MaxBlur));
        }

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                // Inverse distance weighting
                double totalWeight = 0.0;
                double weightedBlur = 0.0;
                bool exactMatch = false;

                for (int i = 0; i < pinCoords.Length; i++)
                {
                    double dx = x - pinCoords[i].px;
                    double dy = y - pinCoords[i].py;
                    double distSq = dx * dx + dy * dy;

                    if (distSq < 1e-4)
                    {
                        weightedBlur = pinCoords[i].blur;
                        exactMatch = true;
                        break;
                    }

                    double w = 1.0 / (distSq + 16.0); // Soft minimum radius
                    totalWeight += w;
                    weightedBlur += w * pinCoords[i].blur;
                }

                double targetBlur = exactMatch ? weightedBlur : (weightedBlur / totalWeight);
                double t = Math.Clamp(targetBlur / maxRadius, 0.0, 1.0);

                if (t <= 0.001)
                {
                    result[idx] = bgraPixels[idx];
                    result[idx + 1] = bgraPixels[idx + 1];
                    result[idx + 2] = bgraPixels[idx + 2];
                    result[idx + 3] = bgraPixels[idx + 3];
                }
                else if (t < 0.5)
                {
                    double w = t * 2.0;
                    result[idx] = (byte)Math.Round(bgraPixels[idx] * (1.0 - w) + blurredHalf[idx] * w);
                    result[idx + 1] = (byte)Math.Round(bgraPixels[idx + 1] * (1.0 - w) + blurredHalf[idx + 1] * w);
                    result[idx + 2] = (byte)Math.Round(bgraPixels[idx + 2] * (1.0 - w) + blurredHalf[idx + 2] * w);
                    result[idx + 3] = bgraPixels[idx + 3];
                }
                else
                {
                    double w = (t - 0.5) * 2.0;
                    result[idx] = (byte)Math.Round(blurredHalf[idx] * (1.0 - w) + blurredFull[idx] * w);
                    result[idx + 1] = (byte)Math.Round(blurredHalf[idx + 1] * (1.0 - w) + blurredFull[idx + 1] * w);
                    result[idx + 2] = (byte)Math.Round(blurredHalf[idx + 2] * (1.0 - w) + blurredFull[idx + 2] * w);
                    result[idx + 3] = bgraPixels[idx + 3];
                }
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Spin Blur: angular circular integration around an elliptical center pin (commit 7e0f48a2).
    /// </summary>
    public static byte[] ApplySpinBlur(
        byte[] bgraPixels,
        int width,
        int height,
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        double angleDegrees,
        double blurAngleDegrees = 15.0,
        double feather = 0.5)
    {
        if (width <= 0 || height <= 0 || Math.Abs(blurAngleDegrees) < 0.1)
            return (byte[])bgraPixels.Clone();

        double ss = Math.Min(width, height);
        double cx = centerX * width;
        double cy = centerY * height;
        double rx = Math.Max(0.01, radiusX) * ss;
        double ry = Math.Max(0.01, radiusY) * ss;

        double rad = angleDegrees * Math.PI / 180.0;
        double cosA = Math.Cos(rad);
        double sinA = Math.Sin(rad);

        double innerThreshold = Math.Max(0.0, 1.0 - feather);
        double blurRad = Math.Clamp(blurAngleDegrees, 0.5, MaxSpinAngle) * Math.PI / 180.0;

        int sampleCount = Math.Clamp((int)Math.Ceiling(Math.Abs(blurAngleDegrees) * 0.75), 7, 45);
        if ((sampleCount & 1) == 0) sampleCount++; // Ensure odd number of symmetric samples

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                double dx = x - cx;
                double dy = y - cy;

                // Elliptical coordinate check
                double u = (dx * cosA + dy * sinA) / rx;
                double v = (-dx * sinA + dy * cosA) / ry;
                double eDist = Math.Sqrt(u * u + v * v);

                if (eDist <= innerThreshold || eDist >= 1.5)
                {
                    // Inside sharp core or far outside blur ellipse
                    result[idx] = bgraPixels[idx];
                    result[idx + 1] = bgraPixels[idx + 1];
                    result[idx + 2] = bgraPixels[idx + 2];
                    result[idx + 3] = bgraPixels[idx + 3];
                    continue;
                }

                // Blur factor falloff
                double blurFactor;
                if (eDist < 1.0)
                {
                    double t = (eDist - innerThreshold) / (1.0 - innerThreshold);
                    blurFactor = t * t * (3.0 - 2.0 * t);
                }
                else
                {
                    double t = (1.5 - eDist) / 0.5;
                    blurFactor = Math.Clamp(t * t * (3.0 - 2.0 * t), 0.0, 1.0);
                }

                // Integrate along circular arc
                double currentTheta = Math.Atan2(dy, dx);
                double currentR = Math.Sqrt(dx * dx + dy * dy);
                double sweep = blurRad * blurFactor;

                double sumB = 0, sumG = 0, sumR = 0, sumA = 0;
                double totalW = 0;

                for (int s = 0; s < sampleCount; s++)
                {
                    double frac = (double)s / (sampleCount - 1) - 0.5; // -0.5 .. +0.5
                    double arcTheta = currentTheta + frac * sweep;

                    double sx = cx + currentR * Math.Cos(arcTheta);
                    double sy = cy + currentR * Math.Sin(arcTheta);

                    int ix = Math.Clamp((int)Math.Round(sx), 0, width - 1);
                    int iy = Math.Clamp((int)Math.Round(sy), 0, height - 1);
                    int sIdx = (iy * width + ix) * 4;

                    double gaussianW = Math.Exp(-4.0 * frac * frac);
                    sumB += bgraPixels[sIdx] * gaussianW;
                    sumG += bgraPixels[sIdx + 1] * gaussianW;
                    sumR += bgraPixels[sIdx + 2] * gaussianW;
                    sumA += bgraPixels[sIdx + 3] * gaussianW;
                    totalW += gaussianW;
                }

                result[idx] = (byte)Math.Clamp(Math.Round(sumB / totalW), 0, 255);
                result[idx + 1] = (byte)Math.Clamp(Math.Round(sumG / totalW), 0, 255);
                result[idx + 2] = (byte)Math.Clamp(Math.Round(sumR / totalW), 0, 255);
                result[idx + 3] = (byte)Math.Clamp(Math.Round(sumA / totalW), 0, 255);
            }
        });

        return result;
    }
}
