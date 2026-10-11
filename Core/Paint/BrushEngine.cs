using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Paint;

/// <summary>
/// Ported brush stroke smoothing and dab spacing engine from FryImage (crates/paint/src/brush.rs).
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

        double alpha = Math.Clamp(1.0 - (smoothingPercent / 100.0) * 0.85, 0.15, 1.0);
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
    /// Interpolates equidistant dabs along a smoothed stroke line path.
    /// </summary>
    public static List<Point> GenerateDabs(IReadOnlyList<Point> smoothedPoints, double stepDistance)
    {
        var dabs = new List<Point>();
        if (smoothedPoints.Count == 0) return dabs;

        dabs.Add(smoothedPoints[0]);
        double accumulated = 0.0;

        for (int i = 0; i < smoothedPoints.Count - 1; i++)
        {
            Point p1 = smoothedPoints[i];
            Point p2 = smoothedPoints[i + 1];

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double segDist = Math.Sqrt(dx * dx + dy * dy);

            if (segDist < 1e-4) continue;

            double currentT = (stepDistance - accumulated) / segDist;
            while (currentT <= 1.0)
            {
                double dabX = p1.X + currentT * dx;
                double dabY = p1.Y + currentT * dy;
                dabs.Add(new Point(dabX, dabY));
                accumulated = 0.0;
                currentT += stepDistance / segDist;
            }

            accumulated += segDist - (currentT - stepDistance / segDist) * segDist;
        }

        return dabs;
    }
}
