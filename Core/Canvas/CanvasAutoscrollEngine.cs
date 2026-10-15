using System;
using Avalonia;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Canvas;

/// <summary>
/// Canvas autoscroll physics engine (Studio viewport edge autoscroll specification).
/// Automatically scrolls the viewport when dragging a marquee, lasso, transform handle, or brush
/// beyond the visible viewport edges.
/// </summary>
public static class CanvasAutoscrollEngine
{
    private const double MinSpeed = 120.0;  // pixels per second
    private const double MaxSpeed = 2400.0; // pixels per second
    private const double Gain = 12.0;       // speed boost per point beyond edge
    private const double MaxDt = 0.05;      // 50ms cap to avoid teleportation

    /// <summary>
    /// Computes the scroll displacement vector for the given pointer position and viewport rect.
    /// Returns Avalonia.Vector.Zero if pointer is inside the viewport.
    /// </summary>
    public static Avalonia.Vector ComputeAutoscrollDelta(Point screenPointer, Rect viewportRect, double dt)
    {
        if (viewportRect.Width <= 0 || viewportRect.Height <= 0 || dt <= 0)
            return Avalonia.Vector.Zero;

        dt = Math.Min(dt, MaxDt);

        double pastLeft = screenPointer.X < viewportRect.Left ? screenPointer.X - viewportRect.Left : 0.0;
        double pastRight = screenPointer.X > viewportRect.Right ? screenPointer.X - viewportRect.Right : 0.0;
        double pastTop = screenPointer.Y < viewportRect.Top ? screenPointer.Y - viewportRect.Top : 0.0;
        double pastBottom = screenPointer.Y > viewportRect.Bottom ? screenPointer.Y - viewportRect.Bottom : 0.0;

        double dX = pastLeft != 0 ? pastLeft : pastRight;
        double dY = pastTop != 0 ? pastTop : pastBottom;

        if (dX == 0.0 && dY == 0.0)
            return Avalonia.Vector.Zero;

        double CalcAxis(double dist)
        {
            if (dist == 0.0) return 0.0;
            double sign = Math.Sign(dist);
            double speed = Math.Min(MaxSpeed, MinSpeed + Gain * Math.Abs(dist));
            return sign * speed * dt;
        }

        return new Avalonia.Vector(CalcAxis(dX), CalcAxis(dY));
    }
}
