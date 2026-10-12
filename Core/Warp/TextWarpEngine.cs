using System;
using Avalonia;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Warp;

/// <summary>
/// High-precision mathematical transformation engine for text warp deformations.
/// Implements standard closed-form warp approximations for all studio preset styles.
/// </summary>
public static class TextWarpEngine
{
    /// <summary>
    /// Transforms a local coordinate into warped coordinate space.
    /// </summary>
    public static Point TransformPoint(Point pt, Rect bounds, TextWarpSettings settings)
    {
        if (settings == null || !settings.IsActive || bounds.Width <= 1 || bounds.Height <= 1)
        {
            return pt;
        }

        double halfW = bounds.Width / 2.0;
        double halfH = bounds.Height / 2.0;

        // Normalize to [-1, 1] relative to bounds center
        double u = (pt.X - bounds.Center.X) / halfW;
        double v = (pt.Y - bounds.Center.Y) / halfH;

        // If orientation is vertical, rotate axis by 90 deg
        if (!settings.IsHorizontal)
        {
            double tmp = u;
            u = v;
            v = tmp;
        }

        // Apply perspective distortions
        double hDist = settings.HorizontalDistortion / 100.0;
        double vDist = settings.VerticalDistortion / 100.0;

        if (Math.Abs(hDist) > 0.001)
        {
            v *= (1.0 + hDist * u);
        }

        if (Math.Abs(vDist) > 0.001)
        {
            u *= (1.0 - vDist * v);
        }

        // Compute aspect ratio and bend parameter
        double aspect = Math.Max(0.1, bounds.Width / Math.Max(1.0, bounds.Height));
        double b = Math.Clamp(settings.Bend / 100.0, -1.0, 1.0);
        double bell = Math.Max(0.0, 1.0 - u * u);

        var (uWarp, vWarp) = ApplyWarpMath(settings.Style, u, v, b, bell, aspect);

        // Revert orientation rotation if vertical
        if (!settings.IsHorizontal)
        {
            double tmp = uWarp;
            uWarp = vWarp;
            vWarp = tmp;
        }

        // Map back to canvas coordinate space
        return new Point(
            bounds.Center.X + uWarp * halfW,
            bounds.Center.Y + vWarp * halfH
        );
    }

    private static (double U, double V) ApplyWarpMath(TextWarpStyle style, double u, double v, double b, double bell, double aspect)
    {
        switch (style)
        {
            case TextWarpStyle.Arc:
                return Arc(u, v, b, aspect);

            case TextWarpStyle.ArcLower:
                return Lerp2((u, v), Arc(u, v, b, aspect), (v + 1.0) / 2.0);

            case TextWarpStyle.ArcUpper:
                return Lerp2((u, v), Arc(u, v, b, aspect), (1.0 - v) / 2.0);

            case TextWarpStyle.Arch:
                return (u, v - b * bell);

            case TextWarpStyle.Bulge:
                return (u, v * (1.0 + b * bell));

            case TextWarpStyle.ShellLower:
                return (u, v + b * bell * (v + 1.0) / 2.0);

            case TextWarpStyle.ShellUpper:
                return (u, v - b * bell * (1.0 - v) / 2.0);

            case TextWarpStyle.Flag:
                return (u, v - b * 0.5 * Math.Sin(Math.PI * u));

            case TextWarpStyle.Wave:
                return (u, v - b * 0.5 * Math.Sin(Math.PI * u + v * (Math.PI / 2.0)));

            case TextWarpStyle.Fish:
                return (u, v * (1.0 + b * 0.6 * (1.0 - u) * Math.Sqrt(bell)) - b * 0.35 * bell);

            case TextWarpStyle.Rise:
                return (u, v - b * 0.5 * Math.Sin((Math.PI / 2.0) * u));

            case TextWarpStyle.Fisheye:
            {
                double r2 = Math.Min(1.0, u * u + v * v);
                double k = 1.0 + b * (1.0 - r2);
                return (u * k, v * k);
            }

            case TextWarpStyle.Inflate:
            {
                double bu = Math.Max(0.0, 1.0 - v * v);
                return (u * (1.0 + b * 0.5 * bu), v * (1.0 + b * 0.5 * bell));
            }

            case TextWarpStyle.Squeeze:
                return (u * (1.0 + b * 0.5 * Math.Max(0.0, 1.0 - v * v)), v * (1.0 - b * 0.5 * bell));

            case TextWarpStyle.Twist:
            {
                double r = Math.Sqrt(u * u + v * v);
                if (r >= 1.0) return (u, v);
                double a = b * (Math.PI / 2.0) * (1.0 - r) * (1.0 - r);
                double s = Math.Sin(a);
                double c = Math.Cos(a);
                double x = u * aspect;
                double y = v;
                return ((x * c - y * s) / aspect, x * s + y * c);
            }

            case TextWarpStyle.None:
            default:
                return (u, v);
        }
    }

    private static (double U, double V) Arc(double u, double v, double b, double aspect)
    {
        if (Math.Abs(b) < 1e-6) return (u, v);

        double half = aspect;
        double thetaMax = b * (Math.PI / 2.0);
        double r = half / thetaMax;
        double theta = u * thetaMax;
        double rr = r - v;
        double x = rr * Math.Sin(theta);
        double y = r - rr * Math.Cos(theta);
        return (x / half, y);
    }

    private static (double U, double V) Lerp2((double U, double V) a, (double U, double V) b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return (a.U + (b.U - a.U) * t, a.V + (b.V - a.V) * t);
    }
}
