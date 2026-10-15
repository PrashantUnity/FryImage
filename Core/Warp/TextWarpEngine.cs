using System;
using Avalonia;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Warp;

/// <summary>
/// High-precision mathematical transformation engine for text warp deformations.
/// Implements Studio 2026's exact 4x4 Bicubic Bezier control grids across all 15 preset styles.
/// </summary>
public static class TextWarpEngine
{
    public struct Point2D
    {
        public double X;
        public double Y;
        public Point2D(double x, double y) { X = x; Y = y; }
    }

    /// <summary>
    /// Transforms a local coordinate into warped coordinate space using Studio 2026's Bicubic Bezier patch.
    /// </summary>
    public static Point TransformPoint(Point pt, Rect bounds, TextWarpSettings settings)
    {
        if (settings == null || !settings.IsActive || bounds.Width <= 1 || bounds.Height <= 1)
        {
            return pt;
        }

        // Normalized (u, v) in [0, 1] relative to bounds top-left
        double u = Math.Clamp((pt.X - bounds.X) / bounds.Width, 0.0, 1.0);
        double v = Math.Clamp((pt.Y - bounds.Y) / bounds.Height, 0.0, 1.0);

        double b = Math.Clamp(settings.Bend / 100.0, -1.0, 1.0);
        double hd = Math.Clamp(settings.HorizontalDistortion / 100.0, -1.0, 1.0);
        double vd = Math.Clamp(settings.VerticalDistortion / 100.0, -1.0, 1.0);
        bool vertical = !settings.IsHorizontal;
        double aspect = Math.Max(0.01, bounds.Height / Math.Max(1.0, bounds.Width));

        var grid = ComputePresetGrid(settings.Style, b, hd, vd, vertical, aspect);
        var (warpedU, warpedV) = EvaluateBezierPatch(grid, u, v);

        return new Point(
            bounds.X + warpedU * bounds.Width,
            bounds.Y + warpedV * bounds.Height
        );
    }

    private static (double U, double V) EvaluateBezierPatch(Point2D[,] g, double u, double v)
    {
        double su = 1.0 - u;
        double[] bu = [su * su * su, 3.0 * su * su * u, 3.0 * su * u * u, u * u * u];

        double sv = 1.0 - v;
        double[] bv = [sv * sv * sv, 3.0 * sv * sv * v, 3.0 * sv * v * v, v * v * v];

        double rx = 0.0;
        double ry = 0.0;

        for (int j = 0; j < 4; j++)
        {
            for (int i = 0; i < 4; i++)
            {
                double weight = bv[j] * bu[i];
                rx += weight * g[j, i].X;
                ry += weight * g[j, i].Y;
            }
        }

        return (rx, ry);
    }

    private static Point2D[,] ComputePresetGrid(TextWarpStyle style, double b, double hd, double vd, bool vertical, double aspect)
    {
        double h = vertical ? 1.0 / aspect : aspect;
        double effHd = vertical ? vd : hd;
        double effVd = vertical ? hd : vd;

        Point2D[,] g = RestGrid();

        switch (style)
        {
            case TextWarpStyle.Arc:
                g = ArcGrid(b, h);
                break;

            case TextWarpStyle.Arch:
                for (int j = 0; j < 4; j++)
                {
                    var row = EdgeArc(j / 3.0, b, true, h);
                    for (int i = 0; i < 4; i++) g[j, i] = row[i];
                }
                break;

            case TextWarpStyle.Bulge:
                g = BlendRows(EdgeArc(0.0, b, true, h), EdgeArc(1.0, b, false, h));
                break;

            case TextWarpStyle.ArcLower:
            {
                var top = new Point2D[4] { g[0, 0], g[0, 1], g[0, 2], g[0, 3] };
                g = BlendRows(top, EdgeArc(1.0, b, false, h));
                break;
            }

            case TextWarpStyle.ArcUpper:
            {
                var bot = new Point2D[4] { g[3, 0], g[3, 1], g[3, 2], g[3, 3] };
                g = BlendRows(EdgeArc(0.0, b, true, h), bot);
                break;
            }

            case TextWarpStyle.ShellLower:
            {
                var a = ArcGrid(-b, h);
                for (int i = 0; i < 4; i++) g[3, i] = a[3, i];
                g[2, 0] = a[2, 0];
                g[2, 3] = a[2, 3];
                break;
            }

            case TextWarpStyle.ShellUpper:
            {
                var a = ArcGrid(b, h);
                for (int i = 0; i < 4; i++) g[0, i] = a[0, i];
                g[1, 0] = a[1, 0];
                g[1, 3] = a[1, 3];
                break;
            }

            case TextWarpStyle.Flag:
                for (int j = 0; j < 4; j++)
                {
                    g[j, 1].Y -= 2.0 * b;
                    g[j, 2].Y += 2.0 * b;
                }
                break;

            case TextWarpStyle.Wave:
                for (int j = 1; j <= 2; j++)
                {
                    g[j, 1].Y += (4.0 / 3.0) * b;
                    g[j, 2].Y -= (4.0 / 3.0) * b;
                }
                break;

            case TextWarpStyle.Fish:
            {
                double[] factors = [-2.0, -2.0 / 3.0, 2.0 / 3.0, 2.0];
                for (int j = 0; j < 4; j++)
                {
                    g[j, 1].Y += factors[j] * b;
                    g[j, 2].Y -= factors[j] * b;
                }
                break;
            }

            case TextWarpStyle.Rise:
                for (int j = 0; j < 4; j++)
                {
                    g[j, 0].Y += 2.0 * b;
                    g[j, 1].Y += 2.0 * b;
                }
                break;

            case TextWarpStyle.Fisheye:
            {
                (int j, int i, double dx, double dy)[] shifts = [
                    (1, 1, -1.0, -1.0),
                    (1, 2, 1.0, -1.0),
                    (2, 1, -1.0, 1.0),
                    (2, 2, 1.0, 1.0)
                ];
                foreach (var (sj, si, dx, dy) in shifts)
                {
                    g[sj, si].X += dx * (2.0 / 3.0) * b;
                    g[sj, si].Y += dy * (2.0 / 3.0) * b;
                }
                break;
            }

            case TextWarpStyle.Inflate:
            case TextWarpStyle.Squeeze:
            {
                double s = style == TextWarpStyle.Inflate ? 1.0 : -1.0;
                for (int i = 1; i <= 2; i++)
                {
                    g[0, i].Y -= b / 3.0;
                    g[3, i].Y += b / 3.0;
                }
                for (int j = 1; j <= 2; j++)
                {
                    g[j, 0].X -= s * b / 3.0;
                    g[j, 3].X += s * b / 3.0;
                }
                (int j, int i, double dx, double dy)[] shifts = [
                    (1, 1, -1.0, -1.0),
                    (1, 2, 1.0, -1.0),
                    (2, 1, -1.0, 1.0),
                    (2, 2, 1.0, 1.0)
                ];
                foreach (var (sj, si, dx, dy) in shifts)
                {
                    g[sj, si].X += s * dx * b / 9.0;
                    g[sj, si].Y += dy * b / 9.0;
                }
                break;
            }

            case TextWarpStyle.Twist:
            {
                double a = Math.Abs(b);
                if (b >= 0.0)
                {
                    g[1, 1].X += a;
                    g[1, 2].Y += a;
                    g[2, 1].Y -= a;
                    g[2, 2].X -= a;
                }
                else
                {
                    g[1, 1].Y += a;
                    g[1, 2].X -= a;
                    g[2, 1].X += a;
                    g[2, 2].Y -= a;
                }
                break;
            }

            case TextWarpStyle.None:
            default:
                break;
        }

        // Vertical distortion: scale top and bottom rows around midpoints
        if (Math.Abs(effVd) > 1e-9)
        {
            var d0 = ComputeRowMove(GetRow(g, 0), 1.0 - effVd);
            var d3 = ComputeRowMove(GetRow(g, 3), 1.0 + effVd);
            for (int j = 0; j < 4; j++)
            {
                double t = j / 3.0;
                for (int i = 0; i < 4; i++)
                {
                    g[j, i].X += d0[i].X + (d3[i].X - d0[i].X) * t;
                    g[j, i].Y += d0[i].Y + (d3[i].Y - d0[i].Y) * t;
                }
            }
        }

        // Horizontal distortion: scale each column around midpoint
        if (Math.Abs(effHd) > 1e-9)
        {
            for (int i = 0; i < 4; i++)
            {
                double s = 1.0 + effHd * (2.0 * i / 3.0 - 1.0);
                double mx = (g[0, i].X + g[3, i].X) / 2.0;
                double my = (g[0, i].Y + g[3, i].Y) / 2.0;
                for (int j = 0; j < 4; j++)
                {
                    g[j, i] = new Point2D(mx + (g[j, i].X - mx) * s, my + (g[j, i].Y - my) * s);
                }
            }
        }

        // Vertical orientation: swap axes
        if (vertical)
        {
            Point2D[,] transposed = new Point2D[4, 4];
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    transposed[j, i] = new Point2D(g[i, j].Y, g[i, j].X);
                }
            }
            g = transposed;
        }

        return g;
    }

    private static Point2D[] GetRow(Point2D[,] g, int row)
    {
        return [g[row, 0], g[row, 1], g[row, 2], g[row, 3]];
    }

    private static Point2D[] ComputeRowMove(Point2D[] row, double s)
    {
        double mx = (row[0].X + row[3].X) / 2.0;
        double my = (row[0].Y + row[3].Y) / 2.0;
        Point2D[] res = new Point2D[4];
        for (int i = 0; i < 4; i++)
        {
            res[i] = new Point2D(mx + (row[i].X - mx) * s - row[i].X, my + (row[i].Y - my) * s - row[i].Y);
        }
        return res;
    }

    private static Point2D[,] RestGrid()
    {
        Point2D[,] g = new Point2D[4, 4];
        for (int j = 0; j < 4; j++)
        {
            for (int i = 0; i < 4; i++)
            {
                g[j, i] = new Point2D(i / 3.0, j / 3.0);
            }
        }
        return g;
    }

    private static Point2D[] EdgeArc(double y, double b, bool up, double h)
    {
        if (Math.Abs(b) < 1e-9)
        {
            return [new Point2D(0.0, y), new Point2D(1.0 / 3.0, y), new Point2D(2.0 / 3.0, y), new Point2D(1.0, y)];
        }

        double th = Math.Abs(b) * Math.PI;
        double k = (4.0 / 3.0) * Math.Tan(th / 4.0) * 0.5 / Math.Sin(th / 2.0);
        double s = (b > 0.0) == up ? -1.0 : 1.0;
        double c = Math.Cos(th / 2.0);
        double n = Math.Sin(th / 2.0);
        double dy = s * k * n / h;

        return [
            new Point2D(0.0, y),
            new Point2D(k * c, y + dy),
            new Point2D(1.0 - k * c, y + dy),
            new Point2D(1.0, y)
        ];
    }

    private static Point2D[,] ArcGrid(double b, double h)
    {
        if (Math.Abs(b) < 1e-9) return RestGrid();

        bool up = b > 0.0;
        double th = Math.Abs(b) * Math.PI;
        double r = 0.5 / Math.Sin(th / 2.0);
        double k = (4.0 / 3.0) * Math.Tan(th / 4.0);
        double s = up ? -1.0 : 1.0;
        double cy = up ? h + r * Math.Cos(th / 2.0) : -r * Math.Cos(th / 2.0);

        Point2D[,] g = new Point2D[4, 4];
        for (int j = 0; j < 4; j++)
        {
            double v = j / 3.0;
            double big = r + h * (up ? 1.0 - v : v);
            Func<double, Point2D> at = a => new Point2D(0.5 + big * Math.Sin(a), cy + s * big * Math.Cos(a));
            Func<double, Point2D> tangent = a => new Point2D(Math.Cos(a), -s * Math.Sin(a));

            var p0 = at(-th / 2.0);
            var p3 = at(th / 2.0);
            var t0 = tangent(-th / 2.0);
            var t1 = tangent(th / 2.0);

            double l = k * big;
            g[j, 0] = new Point2D(p0.X, p0.Y / h);
            g[j, 1] = new Point2D(p0.X + l * t0.X, (p0.Y + l * t0.Y) / h);
            g[j, 2] = new Point2D(p3.X - l * t1.X, (p3.Y - l * t1.Y) / h);
            g[j, 3] = new Point2D(p3.X, p3.Y / h);
        }

        return g;
    }

    private static Point2D[,] BlendRows(Point2D[] top, Point2D[] bottom)
    {
        Point2D[,] g = new Point2D[4, 4];
        for (int j = 0; j < 4; j++)
        {
            double t = j / 3.0;
            for (int i = 0; i < 4; i++)
            {
                g[j, i] = new Point2D(
                    top[i].X + (bottom[i].X - top[i].X) * t,
                    top[i].Y + (bottom[i].Y - top[i].Y) * t
                );
            }
        }
        return g;
    }
}
