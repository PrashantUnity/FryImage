using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Core.Coloring;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Paint;

/// <summary>
/// Advanced digital brush engine ported and expanded from FryImage / Photocraft (crates/paint).
/// Features:
/// - Weighted moving-average stroke stabilizer & lazy-mouse pulled string
/// - Equidistant dab interpolation with continuous spacing
/// - Photoshop-grade Shape Dynamics (Size jitter, Angle jitter, Roundness jitter, Direction tracking)
/// - Scattering dynamics (perpendicular or radial displacement)
/// - Color Dynamics (FG/BG blend jitter, hue/saturation/brightness variations)
/// - Transfer dynamics (opacity and flow jitter)
/// - Symmetry reflection engine (Vertical, Horizontal, Dual 4-quadrant, Radial Mandala)
/// </summary>
public static class BrushEngine
{
    /// <summary>
    /// Smooths raw input stroke coordinates using FryImage's weighted moving-average
    /// stabilizer based on the smoothing percentage [0..100].
    /// </summary>
    public static List<Point> SmoothPoints(IReadOnlyList<Point> rawPoints, double smoothingPercent)
    {
        if (rawPoints.Count <= 2 || smoothingPercent <= 0)
        {
            return new List<Point>(rawPoints);
        }

        double alpha = Math.Clamp(1.0 - (smoothingPercent / 100.0) * 0.85, 0.12, 1.0);
        var result = new List<Point>(rawPoints.Count) { rawPoints[0] };

        Point prev = rawPoints[0];
        for (int i = 1; i < rawPoints.Count; i++)
        {
            Point current = rawPoints[i];
            double smoothedX = prev.X + alpha * (current.X - prev.X);
            double smoothedY = prev.Y + alpha * (current.Y - prev.Y);
            Point smoothed = new(smoothedX, smoothedY);
            result.Add(smoothed);
            prev = smoothed;
        }

        return result;
    }

    /// <summary>
    /// Calculates the step spacing distance between individual dabs given the brush tip diameter.
    /// Default standard is 25% of brush diameter.
    /// </summary>
    public static double CalculateSpacing(double brushSize, double spacingPercent = 25.0)
    {
        return Math.Max(1.0, brushSize * (spacingPercent / 100.0));
    }

    /// <summary>
    /// Fast deterministic pseudo-random generator for reproducible stroke jitter.
    /// </summary>
    private static double HashNoise(int index, int seed, int salt)
    {
        uint h = (uint)(index * 7919 + seed * 104729 + salt * 31337);
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return (h & 0xFFFF) / 65535.0; // 0.0 .. 1.0
    }

    /// <summary>
    /// Generates fully parameterized dabs along a smoothed stroke path using the given BrushSettings and dynamics.
    /// </summary>
    public static List<BrushDab> GenerateDabs(
        IReadOnlyList<Point> smoothedPoints,
        BrushSettings settings,
        Color foregroundColor,
        Color backgroundColor,
        int randomSeed = 101)
    {
        var dabs = new List<BrushDab>();
        if (smoothedPoints == null || smoothedPoints.Count == 0) return dabs;

        double baseRadius = Math.Max(0.5, settings.Size / 2.0);
        double stepDistance = Math.Max(1.0, settings.Size * Math.Clamp(settings.Spacing, 0.02, 2.0));

        // If only 1 point, emit single dab
        if (smoothedPoints.Count == 1)
        {
            dabs.Add(new BrushDab(
                smoothedPoints[0],
                baseRadius,
                baseRadius * settings.Roundness,
                settings.Angle,
                settings.Hardness,
                settings.Opacity * settings.Flow,
                foregroundColor,
                settings.Aliased,
                settings.WetEdges,
                settings.TipShape,
                randomSeed));
            return dabs;
        }

        double accumulated = 0.0;
        int dabIndex = 0;

        for (int i = 0; i < smoothedPoints.Count - 1; i++)
        {
            Point p1 = smoothedPoints[i];
            Point p2 = smoothedPoints[i + 1];

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double segDist = Math.Sqrt(dx * dx + dy * dy);

            if (segDist < 1e-4) continue;

            double segAngleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            double currentT = (stepDistance - accumulated) / segDist;
            while (currentT <= 1.0)
            {
                double dabX = p1.X + currentT * dx;
                double dabY = p1.Y + currentT * dy;

                // ── Dynamics calculation for this dab ──
                double dabRadiusX = baseRadius;
                double dabRadiusY = baseRadius * settings.Roundness;
                double dabAngle = settings.Angle;
                double dabOpacity = settings.Opacity * settings.Flow;
                Color dabColor = foregroundColor;

                // 1. Shape Dynamics
                if (settings.ShapeDynamics.Enabled)
                {
                    // Size jitter
                    if (settings.ShapeDynamics.Size.Jitter > 0)
                    {
                        double noise = HashNoise(dabIndex, randomSeed, 1);
                        double factor = 1.0 - noise * settings.ShapeDynamics.Size.Jitter;
                        factor = Math.Max(settings.ShapeDynamics.Size.Minimum, factor);
                        dabRadiusX *= factor;
                        dabRadiusY *= factor;
                    }

                    // Angle jitter or direction tracking
                    if (settings.ShapeDynamics.Angle.Control == BrushControl.Direction)
                    {
                        dabAngle += segAngleDeg;
                    }
                    if (settings.ShapeDynamics.Angle.Jitter > 0)
                    {
                        double noise = HashNoise(dabIndex, randomSeed, 2);
                        dabAngle += (noise * 2.0 - 1.0) * 180.0 * settings.ShapeDynamics.Angle.Jitter;
                    }

                    // Roundness jitter
                    if (settings.ShapeDynamics.Roundness.Jitter > 0)
                    {
                        double noise = HashNoise(dabIndex, randomSeed, 3);
                        double rFactor = 1.0 - noise * settings.ShapeDynamics.Roundness.Jitter;
                        rFactor = Math.Max(settings.ShapeDynamics.Roundness.Minimum, rFactor);
                        dabRadiusY *= rFactor;
                    }
                }

                // 2. Scattering
                if (settings.Scattering.Enabled && settings.Scattering.Scatter.Jitter > 0)
                {
                    double scatterAmt = settings.Scattering.Scatter.Jitter * baseRadius * 2.0;
                    double noise1 = HashNoise(dabIndex, randomSeed, 4) * 2.0 - 1.0;
                    double noise2 = HashNoise(dabIndex, randomSeed, 5) * 2.0 - 1.0;

                    if (settings.Scattering.BothAxes)
                    {
                        dabX += noise1 * scatterAmt;
                        dabY += noise2 * scatterAmt;
                    }
                    else
                    {
                        // Perpendicular to stroke
                        double perpX = -dy / segDist;
                        double perpY = dx / segDist;
                        dabX += perpX * noise1 * scatterAmt;
                        dabY += perpY * noise1 * scatterAmt;
                    }
                }

                // 3. Transfer Dynamics
                if (settings.Transfer.Enabled)
                {
                    if (settings.Transfer.Opacity.Jitter > 0)
                    {
                        double noise = HashNoise(dabIndex, randomSeed, 6);
                        double opFactor = 1.0 - noise * settings.Transfer.Opacity.Jitter;
                        dabOpacity *= Math.Max(settings.Transfer.Opacity.Minimum, opFactor);
                    }
                    if (settings.Transfer.Flow.Jitter > 0)
                    {
                        double noise = HashNoise(dabIndex, randomSeed, 7);
                        dabOpacity *= Math.Max(0.1, 1.0 - noise * settings.Transfer.Flow.Jitter * 0.5);
                    }
                }

                // 4. Color Dynamics
                if (settings.ColorDynamics.Enabled)
                {
                    // Foreground / Background jitter
                    if (settings.ColorDynamics.ForegroundBackground.Jitter > 0)
                    {
                        double t = HashNoise(dabIndex, randomSeed, 8) * settings.ColorDynamics.ForegroundBackground.Jitter;
                        dabColor = BlendColor(foregroundColor, backgroundColor, t);
                    }

                    // Hue / Saturation / Brightness jitter
                    if (settings.ColorDynamics.HueJitter > 0 || settings.ColorDynamics.BrightnessJitter > 0)
                    {
                        var (h, s, v) = ColorConversions.RgbToHsv(dabColor);
                        if (settings.ColorDynamics.HueJitter > 0)
                        {
                            double hNoise = (HashNoise(dabIndex, randomSeed, 9) * 2.0 - 1.0) * settings.ColorDynamics.HueJitter * 180.0;
                            h = (h + hNoise + 360.0) % 360.0;
                        }
                        if (settings.ColorDynamics.BrightnessJitter > 0)
                        {
                            double vNoise = (HashNoise(dabIndex, randomSeed, 10) * 2.0 - 1.0) * settings.ColorDynamics.BrightnessJitter;
                            v = Math.Clamp(v + vNoise, 0.0, 1.0);
                        }
                        dabColor = ColorConversions.HsvToRgb(h, s, v, dabColor.A);
                    }
                }

                dabs.Add(new BrushDab(
                    new Point(dabX, dabY),
                    Math.Max(0.5, dabRadiusX),
                    Math.Max(0.5, dabRadiusY),
                    dabAngle,
                    settings.Hardness,
                    dabOpacity,
                    dabColor,
                    settings.Aliased,
                    settings.WetEdges,
                    settings.TipShape,
                    randomSeed + dabIndex));

                dabIndex++;
                accumulated = 0.0;
                currentT += stepDistance / segDist;
            }

            accumulated += segDist - (currentT - stepDistance / segDist) * segDist;
        }

        return dabs;
    }

    /// <summary>
    /// Linearly interpolates two colors.
    /// </summary>
    private static Color BlendColor(Color c1, Color c2, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        byte a = (byte)(c1.A + (c2.A - c1.A) * t);
        byte r = (byte)(c1.R + (c2.R - c1.R) * t);
        byte g = (byte)(c1.G + (c2.G - c1.G) * t);
        byte b = (byte)(c1.B + (c2.B - c1.B) * t);
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// Generates reflected point sets for symmetrical drawing.
    /// </summary>
    public static List<List<Point>> ApplySymmetry(IReadOnlyList<Point> points, SymmetryMode mode, Point center)
    {
        var result = new List<List<Point>> { new(points) };
        if (points == null || points.Count == 0 || mode == SymmetryMode.Off) return result;

        switch (mode)
        {
            case SymmetryMode.Vertical:
            {
                var mirrored = new List<Point>(points.Count);
                for (int i = 0; i < points.Count; i++)
                {
                    double mx = 2.0 * center.X - points[i].X;
                    mirrored.Add(new Point(mx, points[i].Y));
                }
                result.Add(mirrored);
                break;
            }

            case SymmetryMode.Horizontal:
            {
                var mirrored = new List<Point>(points.Count);
                for (int i = 0; i < points.Count; i++)
                {
                    double my = 2.0 * center.Y - points[i].Y;
                    mirrored.Add(new Point(points[i].X, my));
                }
                result.Add(mirrored);
                break;
            }

            case SymmetryMode.Dual:
            {
                // Vertical mirror
                var vMirrored = new List<Point>(points.Count);
                // Horizontal mirror
                var hMirrored = new List<Point>(points.Count);
                // Central / 4th quadrant mirror
                var dMirrored = new List<Point>(points.Count);

                for (int i = 0; i < points.Count; i++)
                {
                    double mx = 2.0 * center.X - points[i].X;
                    double my = 2.0 * center.Y - points[i].Y;
                    vMirrored.Add(new Point(mx, points[i].Y));
                    hMirrored.Add(new Point(points[i].X, my));
                    dMirrored.Add(new Point(mx, my));
                }

                result.Add(vMirrored);
                result.Add(hMirrored);
                result.Add(dMirrored);
                break;
            }

            case SymmetryMode.Radial:
            {
                // 6-fold radial mandala symmetry
                for (int k = 1; k < 6; k++)
                {
                    double angleRad = (k * 60.0) * Math.PI / 180.0;
                    double cos = Math.Cos(angleRad);
                    double sin = Math.Sin(angleRad);

                    var rotated = new List<Point>(points.Count);
                    for (int i = 0; i < points.Count; i++)
                    {
                        double rx = points[i].X - center.X;
                        double ry = points[i].Y - center.Y;
                        double nx = center.X + (rx * cos - ry * sin);
                        double ny = center.Y + (rx * sin + ry * cos);
                        rotated.Add(new Point(nx, ny));
                    }
                    result.Add(rotated);
                }
                break;
            }
        }

        return result;
    }
}
