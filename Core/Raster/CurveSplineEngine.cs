using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

public readonly record struct CurvePoint(double In, double Out);

/// <summary>
/// Professional Monotone Cubic Hermite Spline Engine for Photographic Curves Adjustments.
/// Uses the Fritsch-Carlson algorithm to ensure tone curves remain strictly monotonic
/// and free of runaway overshoot or oscillatory ringing.
/// </summary>
public static class CurveSplineEngine
{
    public static byte[] BuildLut(IReadOnlyList<CurvePoint> points)
    {
        byte[] lut = new byte[256];
        if (points == null || points.Count == 0)
        {
            for (int i = 0; i < 256; i++) lut[i] = (byte)i;
            return lut;
        }

        var sorted = points.OrderBy(p => p.In).ToList();

        // Ensure endpoints at 0 and 255 exist
        if (sorted[0].In > 0)
        {
            sorted.Insert(0, new CurvePoint(0, sorted[0].Out));
        }
        if (sorted[^1].In < 255)
        {
            sorted.Add(new CurvePoint(255, sorted[^1].Out));
        }

        int n = sorted.Count;
        if (n == 2)
        {
            // Simple linear ramp between endpoints
            double x0 = sorted[0].In, y0 = sorted[0].Out;
            double x1 = sorted[1].In, y1 = sorted[1].Out;
            double slope = (y1 - y0) / Math.Max(0.0001, x1 - x0);
            for (int x = 0; x < 256; x++)
            {
                double val = y0 + (x - x0) * slope;
                lut[x] = (byte)Math.Clamp((int)Math.Round(val), 0, 255);
            }
            return lut;
        }

        // Fritsch-Carlson Monotone Hermite Spline setup
        double[] xVals = new double[n];
        double[] yVals = new double[n];
        for (int i = 0; i < n; i++)
        {
            xVals[i] = sorted[i].In;
            yVals[i] = sorted[i].Out;
        }

        double[] h = new double[n - 1];
        double[] delta = new double[n - 1];
        for (int i = 0; i < n - 1; i++)
        {
            h[i] = Math.Max(0.0001, xVals[i + 1] - xVals[i]);
            delta[i] = (yVals[i + 1] - yVals[i]) / h[i];
        }

        double[] m = new double[n];
        m[0] = delta[0];
        m[n - 1] = delta[n - 2];
        for (int i = 1; i < n - 1; i++)
        {
            m[i] = (delta[i - 1] + delta[i]) * 0.5;
        }

        for (int i = 0; i < n - 1; i++)
        {
            if (Math.Abs(delta[i]) < 1e-9)
            {
                m[i] = 0.0;
                m[i + 1] = 0.0;
            }
            else
            {
                double alpha = m[i] / delta[i];
                double beta = m[i + 1] / delta[i];
                double mag = alpha * alpha + beta * beta;
                if (mag > 9.0)
                {
                    double s = 3.0 / Math.Sqrt(mag);
                    m[i] = s * alpha * delta[i];
                    m[i + 1] = s * beta * delta[i];
                }
            }
        }

        // Evaluate spline for each input level 0..255
        int seg = 0;
        for (int x = 0; x < 256; x++)
        {
            while (seg < n - 2 && x > xVals[seg + 1])
            {
                seg++;
            }

            double segH = h[seg];
            double t = Math.Clamp((x - xVals[seg]) / segH, 0.0, 1.0);
            double t2 = t * t;
            double t3 = t2 * t;

            // Hermite basis functions
            double h00 = 2.0 * t3 - 3.0 * t2 + 1.0;
            double h10 = t3 - 2.0 * t2 + t;
            double h01 = -2.0 * t3 + 3.0 * t2;
            double h11 = t3 - t2;

            double y = h00 * yVals[seg] + h10 * segH * m[seg] + h01 * yVals[seg + 1] + h11 * segH * m[seg + 1];
            lut[x] = (byte)Math.Clamp((int)Math.Round(y), 0, 255);
        }

        return lut;
    }
}
