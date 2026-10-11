using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// A freehand vector drawing element (Brush, Pencil, Marker, Highlighter).
/// Stores smoothed points in element-local space [0..Width, 0..Height] so it scales,
/// moves, rotates, and serializes consistently with all other CanvasElement types.
/// </summary>
public class DoodleElement : CanvasElement, IHasStroke
{
    public override string ElementType => IsHighlighter ? "Highlighter" : "Drawing";

    public Color StrokeColor { get; set; } = Color.FromArgb(255, 99, 102, 241);
    public double StrokeThickness { get; set; } = 4.0;
    public StrokeDashStyle DashStyle { get; set; } = StrokeDashStyle.Solid;
    public bool IsHighlighter { get; set; }

    /// <summary>
    /// Points in element-local coordinates relative to (X, Y).
    /// </summary>
    public List<Point> Points { get; set; } = new();

    /// <summary>
    /// Reference dimensions at creation time used for aspect scaling on resize.
    /// </summary>
    public double OriginalWidth { get; set; } = 100;
    public double OriginalHeight { get; set; } = 100;

    public DoodleElement()
    {
        Width = 100;
        Height = 100;
        OriginalWidth = 100;
        OriginalHeight = 100;
    }

    /// <summary>
    /// Initializes a DoodleElement from a collection of canvas-space points.
    /// Normalizes points to local space and sets X, Y, Width, Height.
    /// </summary>
    public static DoodleElement FromCanvasPoints(
        IReadOnlyList<Point> canvasPoints,
        Color strokeColor,
        double strokeThickness,
        bool isHighlighter)
    {
        if (canvasPoints == null || canvasPoints.Count == 0)
        {
            return new DoodleElement
            {
                StrokeColor = strokeColor,
                StrokeThickness = strokeThickness,
                IsHighlighter = isHighlighter
            };
        }

        double minX = canvasPoints.Min(p => p.X);
        double minY = canvasPoints.Min(p => p.Y);
        double maxX = canvasPoints.Max(p => p.X);
        double maxY = canvasPoints.Max(p => p.Y);

        double w = Math.Max(maxX - minX, 8);
        double h = Math.Max(maxY - minY, 8);

        var localPoints = canvasPoints.Select(p => new Point(p.X - minX, p.Y - minY)).ToList();

        // If only 1 point was clicked, add a second micro-offset point so a round dab is drawn
        if (localPoints.Count == 1)
        {
            localPoints.Add(new Point(localPoints[0].X + 0.5, localPoints[0].Y + 0.5));
        }

        return new DoodleElement
        {
            X = minX,
            Y = minY,
            Width = w,
            Height = h,
            OriginalWidth = w,
            OriginalHeight = h,
            Points = localPoints,
            StrokeColor = strokeColor,
            StrokeThickness = strokeThickness,
            IsHighlighter = isHighlighter
        };
    }

    public override void Draw(DrawingContext dc)
    {
        if (Points.Count < 2) return;

        double scaleX = OriginalWidth > 0 ? Width / OriginalWidth : 1.0;
        double scaleY = OriginalHeight > 0 ? Height / OriginalHeight : 1.0;

        // Reconstruct canvas-space points
        var transformedPoints = new Point[Points.Count];
        for (int i = 0; i < Points.Count; i++)
        {
            transformedPoints[i] = new Point(
                X + Points[i].X * scaleX,
                Y + Points[i].Y * scaleY);
        }

        var geometry = BuildSmoothGeometry(transformedPoints);

        var color = StrokeColor;
        if (IsHighlighter && color.A > 140)
        {
            color = Color.FromArgb(120, color.R, color.G, color.B);
        }

        var pen = new Pen(
            new SolidColorBrush(color),
            StrokeThickness,
            ShapeDashStyles.ToAvaloniaDashStyle(DashStyle),
            lineCap: PenLineCap.Round,
            lineJoin: PenLineJoin.Round);

        dc.DrawGeometry(null, pen, geometry);
    }

    /// <summary>
    /// Builds a smooth quadratic bezier spline geometry passing through/between points.
    /// </summary>
    public static StreamGeometry BuildSmoothGeometry(IReadOnlyList<Point> pts)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        if (pts.Count == 0) return geometry;
        if (pts.Count == 1)
        {
            ctx.BeginFigure(pts[0], false);
            ctx.LineTo(new Point(pts[0].X + 0.1, pts[0].Y + 0.1));
            ctx.EndFigure(false);
            return geometry;
        }

        ctx.BeginFigure(pts[0], false);

        if (pts.Count == 2)
        {
            ctx.LineTo(pts[1]);
        }
        else
        {
            // Catmull-Rom / midpoint quadratic smoothing
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var p0 = pts[i];
                var p1 = pts[i + 1];
                var mid = new Point((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0);

                if (i == 0)
                {
                    ctx.LineTo(mid);
                }
                else
                {
                    ctx.QuadraticBezierTo(p0, mid);
                }
            }
            ctx.LineTo(pts[^1]);
        }

        ctx.EndFigure(false);
        return geometry;
    }

    public override bool HitTest(Point pt)
    {
        if (!Bounds.Inflate(StrokeThickness / 2 + 8).Contains(pt))
            return false;

        double scaleX = OriginalWidth > 0 ? Width / OriginalWidth : 1.0;
        double scaleY = OriginalHeight > 0 ? Height / OriginalHeight : 1.0;
        double threshold = Math.Max(StrokeThickness / 2.0 + 6.0, 8.0);
        double thresholdSq = threshold * threshold;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            var p1 = new Point(X + Points[i].X * scaleX, Y + Points[i].Y * scaleY);
            var p2 = new Point(X + Points[i + 1].X * scaleX, Y + Points[i + 1].Y * scaleY);

            if (DistanceSquaredToSegment(pt, p1, p2) <= thresholdSq)
                return true;
        }

        return false;
    }

    private static double DistanceSquaredToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;

        if (lenSq == 0) return PointDistanceSquared(p, a);

        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        var proj = new Point(a.X + t * dx, a.Y + t * dy);
        return PointDistanceSquared(p, proj);
    }

    private static double PointDistanceSquared(Point p1, Point p2)
    {
        double dx = p1.X - p2.X;
        double dy = p1.Y - p2.Y;
        return dx * dx + dy * dy;
    }

    public override CanvasElement Clone()
    {
        var copy = new DoodleElement
        {
            OriginalWidth = OriginalWidth,
            OriginalHeight = OriginalHeight,
            StrokeColor = StrokeColor,
            StrokeThickness = StrokeThickness,
            DashStyle = DashStyle,
            IsHighlighter = IsHighlighter,
            Points = new List<Point>(Points)
        };
        CopyBasePropertiesTo(copy);
        copy.IsLocked = false;
        copy.GroupId = null;
        return copy;
    }
}
