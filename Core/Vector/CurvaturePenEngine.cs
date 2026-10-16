using System;
using System.Collections.Generic;
using Avalonia;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Vector;

/// <summary>
/// Curvature Pen Engine for creating and editing smooth paths through anchor points.
/// Implements natural cubic Bezier handle reflow, 45° angle snapping, and smooth/corner toggling (TOOL-214-10, #5197).
/// </summary>
public static class CurvaturePenEngine
{
    /// <summary>
    /// Snaps an angle from start to target to the nearest 45° ray.
    /// </summary>
    public static Point SnapTo45Degrees(Point start, Point target)
    {
        double dx = target.X - start.X;
        double dy = target.Y - start.Y;
        if (double.IsNaN(dx) || double.IsNaN(dy)) return target;
        double distSq = dx * dx + dy * dy;
        if (distSq < 1e-9) return start;

        double angle = Math.Atan2(dy, dx);
        const double step = Math.PI / 4.0; // 45 degrees
        double snapped = Math.Round(angle / step) * step;
        double dirX = Math.Cos(snapped);
        double dirY = Math.Sin(snapped);

        double t = dx * dirX + dy * dirY;
        return new Point(start.X + dirX * t, start.Y + dirY * t);
    }

    /// <summary>
    /// Recalculates smooth handles for every smooth knot in the subpath so the curve
    /// flows continuously through all anchors. Sharp corners keep retracted handles.
    /// </summary>
    public static void ReflowCurvatureHandles(VectorSubpath subpath)
    {
        if (subpath == null || subpath.Knots.Count < 2) return;

        var knots = subpath.Knots;
        int n = knots.Count;
        bool isClosed = subpath.IsClosed;

        for (int i = 0; i < n; i++)
        {
            var k = knots[i];
            if (!k.IsSmooth)
            {
                k.RetractHandles();
                continue;
            }

            Point prev, next;
            if (isClosed)
            {
                prev = knots[(i - 1 + n) % n].Anchor;
                next = knots[(i + 1) % n].Anchor;
            }
            else
            {
                if (i == 0)
                {
                    prev = k.Anchor;
                    next = knots[1].Anchor;
                }
                else if (i == n - 1)
                {
                    prev = knots[n - 2].Anchor;
                    next = k.Anchor;
                }
                else
                {
                    prev = knots[i - 1].Anchor;
                    next = knots[i + 1].Anchor;
                }
            }

            // Catmull-Rom to cubic Bezier tangent conversion: (next - prev) / 6
            double tx = (next.X - prev.X) / 6.0;
            double ty = (next.Y - prev.Y) / 6.0;

            if (i > 0 || isClosed)
                k.HandleIn = new Point(k.Anchor.X - tx, k.Anchor.Y - ty);
            else
                k.HandleIn = k.Anchor;

            if (i < n - 1 || isClosed)
                k.HandleOut = new Point(k.Anchor.X + tx, k.Anchor.Y + ty);
            else
                k.HandleOut = k.Anchor;
        }
    }

    /// <summary>
    /// Toggles a knot between smooth and corner point.
    /// </summary>
    public static void ToggleSmoothCorner(VectorSubpath subpath, PathKnot knot)
    {
        knot.IsSmooth = !knot.IsSmooth;
        if (!knot.IsSmooth)
        {
            knot.RetractHandles();
        }
        ReflowCurvatureHandles(subpath);
    }
}
