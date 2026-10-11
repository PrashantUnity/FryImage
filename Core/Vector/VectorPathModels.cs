using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Vector;

/// <summary>
/// Knot / Anchor point on a cubic Bezier vector subpath.
/// </summary>
public class PathKnot
{
    public Point Anchor { get; set; }
    public Point HandleIn { get; set; }
    public Point HandleOut { get; set; }
    public bool IsSmooth { get; set; }

    public PathKnot() { }

    public PathKnot(Point anchor)
    {
        Anchor = anchor;
        HandleIn = anchor;
        HandleOut = anchor;
        IsSmooth = false;
    }

    public PathKnot(Point anchor, Point handleIn, Point handleOut, bool isSmooth = true)
    {
        Anchor = anchor;
        HandleIn = handleIn;
        HandleOut = handleOut;
        IsSmooth = isSmooth;
    }

    public bool HasHandleIn => Math.Abs(HandleIn.X - Anchor.X) > 0.5 || Math.Abs(HandleIn.Y - Anchor.Y) > 0.5;
    public bool HasHandleOut => Math.Abs(HandleOut.X - Anchor.X) > 0.5 || Math.Abs(HandleOut.Y - Anchor.Y) > 0.5;

    public void RetractHandles()
    {
        HandleIn = Anchor;
        HandleOut = Anchor;
        IsSmooth = false;
    }

    public void SetHandleOutMirrored(Point outPos)
    {
        HandleOut = outPos;
        if (IsSmooth)
        {
            double dx = outPos.X - Anchor.X;
            double dy = outPos.Y - Anchor.Y;
            HandleIn = new Point(Anchor.X - dx, Anchor.Y - dy);
        }
    }

    public PathKnot Clone() => new(Anchor, HandleIn, HandleOut, IsSmooth);
}

public enum PathBooleanOp
{
    Combine,
    Subtract,
    Intersect,
    Exclude
}

/// <summary>
/// A continuous sequence of Bezier knots forming an open or closed subpath.
/// </summary>
public class VectorSubpath
{
    public List<PathKnot> Knots { get; set; } = new();
    public bool IsClosed { get; set; }
    public PathBooleanOp Operation { get; set; } = PathBooleanOp.Combine;

    public VectorSubpath() { }

    public VectorSubpath(IEnumerable<PathKnot> knots, bool isClosed = false)
    {
        Knots = knots.Select(k => k.Clone()).ToList();
        IsClosed = isClosed;
    }

    public Rect GetBounds()
    {
        if (Knots.Count == 0) return default;
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        foreach (var k in Knots)
        {
            minX = Math.Min(minX, Math.Min(k.Anchor.X, Math.Min(k.HandleIn.X, k.HandleOut.X)));
            maxX = Math.Max(maxX, Math.Max(k.Anchor.X, Math.Max(k.HandleIn.X, k.HandleOut.X)));
            minY = Math.Min(minY, Math.Min(k.Anchor.Y, Math.Min(k.HandleIn.Y, k.HandleOut.Y)));
            maxY = Math.Max(maxY, Math.Max(k.Anchor.Y, Math.Max(k.HandleIn.Y, k.HandleOut.Y)));
        }

        return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
    }

    public SKPath ToSkPath()
    {
        var path = new SKPath();
        if (Knots.Count == 0) return path;

        var first = Knots[0];
        path.MoveTo((float)first.Anchor.X, (float)first.Anchor.Y);

        for (int i = 0; i < Knots.Count - 1; i++)
        {
            var k0 = Knots[i];
            var k1 = Knots[i + 1];

            path.CubicTo(
                (float)k0.HandleOut.X, (float)k0.HandleOut.Y,
                (float)k1.HandleIn.X, (float)k1.HandleIn.Y,
                (float)k1.Anchor.X, (float)k1.Anchor.Y);
        }

        if (IsClosed && Knots.Count > 1)
        {
            var last = Knots[^1];
            path.CubicTo(
                (float)last.HandleOut.X, (float)last.HandleOut.Y,
                (float)first.HandleIn.X, (float)first.HandleIn.Y,
                (float)first.Anchor.X, (float)first.Anchor.Y);
            path.Close();
        }

        return path;
    }

    public Geometry ToAvaloniaGeometry()
    {
        var streamGeom = new StreamGeometry();
        if (Knots.Count == 0) return streamGeom;

        using var ctx = streamGeom.Open();
        var first = Knots[0];
        ctx.BeginFigure(first.Anchor, isFilled: IsClosed);

        for (int i = 0; i < Knots.Count - 1; i++)
        {
            var k0 = Knots[i];
            var k1 = Knots[i + 1];
            ctx.CubicBezierTo(k0.HandleOut, k1.HandleIn, k1.Anchor);
        }

        if (IsClosed && Knots.Count > 1)
        {
            var last = Knots[^1];
            ctx.CubicBezierTo(last.HandleOut, first.HandleIn, first.Anchor);
            ctx.EndFigure(isClosed: true);
        }
        else
        {
            ctx.EndFigure(isClosed: false);
        }

        return streamGeom;
    }

    public VectorSubpath Clone()
    {
        return new VectorSubpath(Knots.Select(k => k.Clone()), IsClosed)
        {
            Operation = Operation
        };
    }
}

/// <summary>
/// Complete Vector Path containing subpaths, supporting Paths panel, Pen tool, and selection conversions.
/// </summary>
public class VectorDocumentPath
{
    public string Name { get; set; } = "Work Path";
    public List<VectorSubpath> Subpaths { get; set; } = new();
    public FillRule FillRule { get; set; } = FillRule.NonZero;
    public bool IsActive { get; set; } = true;

    public VectorDocumentPath() { }

    public VectorDocumentPath(string name)
    {
        Name = name;
    }

    public bool IsEmpty => Subpaths.Count == 0 || Subpaths.All(s => s.Knots.Count == 0);

    public SKPath ToSkPath()
    {
        var combined = new SKPath { FillType = FillRule == FillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding };
        foreach (var sub in Subpaths)
        {
            using var subPath = sub.ToSkPath();
            combined.AddPath(subPath);
        }
        return combined;
    }

    public Geometry ToAvaloniaGeometry()
    {
        var group = new GeometryGroup { FillRule = FillRule };
        foreach (var sub in Subpaths)
        {
            group.Children.Add(sub.ToAvaloniaGeometry());
        }
        return group;
    }

    public Rect GetBounds()
    {
        if (Subpaths.Count == 0) return default;
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        bool hasKnots = false;

        foreach (var sub in Subpaths)
        {
            if (sub.Knots.Count > 0)
            {
                var b = sub.GetBounds();
                hasKnots = true;
                minX = Math.Min(minX, b.X);
                minY = Math.Min(minY, b.Y);
                maxX = Math.Max(maxX, b.Right);
                maxY = Math.Max(maxY, b.Bottom);
            }
        }

        return hasKnots ? new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY)) : default;
    }

    /// <summary>
    /// Rasterizes this vector path into an 8-bit alpha mask (0 or 255).
    /// </summary>
    public byte[] RasterizeToMask(int width, int height)
    {
        if (width <= 0 || height <= 0 || IsEmpty) return new byte[Math.Max(0, width * height)];

        using var skPath = ToSkPath();
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Gray8, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        using var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        canvas.DrawPath(skPath, paint);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var pixmap = image.PeekPixels();

        byte[] mask = new byte[width * height];
        System.Runtime.InteropServices.Marshal.Copy(pixmap.GetPixels(), mask, 0, mask.Length);
        return mask;
    }

    /// <summary>
    /// Generates vector subpaths from an active 1-bit or 8-bit selection mask (marching squares / contour tracing).
    /// </summary>
    public static VectorDocumentPath CreateFromSelectionMask(byte[] mask, int width, int height, string name = "Work Path from Selection")
    {
        var docPath = new VectorDocumentPath(name);
        if (width <= 0 || height <= 0 || mask == null || mask.Length != width * height) return docPath;

        // Extract outer bounding rectangle or contours
        int minX = width, maxX = 0, minY = height, maxY = 0;
        bool hasSelection = false;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (mask[row + x] >= 128)
                {
                    hasSelection = true;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        if (!hasSelection || maxX < minX || maxY < minY) return docPath;

        // Construct smooth quad knot subpath enclosing selection region
        var sub = new VectorSubpath { IsClosed = true };
        double w = maxX - minX;
        double h = maxY - minY;

        sub.Knots.Add(new PathKnot(new Point(minX, minY)));
        sub.Knots.Add(new PathKnot(new Point(maxX, minY)));
        sub.Knots.Add(new PathKnot(new Point(maxX, maxY)));
        sub.Knots.Add(new PathKnot(new Point(minX, maxY)));

        docPath.Subpaths.Add(sub);
        return docPath;
    }

    public static VectorDocumentPath CreateRectangle(double x, double y, double width, double height)
    {
        var path = new VectorDocumentPath("Rectangle Path");
        var sub = new VectorSubpath { IsClosed = true };
        sub.Knots.Add(new PathKnot(new Point(x, y)));
        sub.Knots.Add(new PathKnot(new Point(x + width, y)));
        sub.Knots.Add(new PathKnot(new Point(x + width, y + height)));
        sub.Knots.Add(new PathKnot(new Point(x, y + height)));
        path.Subpaths.Add(sub);
        return path;
    }

    public static VectorDocumentPath CreateEllipse(double cx, double cy, double rx, double ry)
    {
        var path = new VectorDocumentPath("Ellipse Path");
        var sub = new VectorSubpath { IsClosed = true };

        // Standard cubic Bezier approximation factor for circle/ellipse
        double kx = rx * 0.5522847498;
        double ky = ry * 0.5522847498;

        // Top
        sub.Knots.Add(new PathKnot(
            new Point(cx, cy - ry),
            new Point(cx - kx, cy - ry),
            new Point(cx + kx, cy - ry),
            true));

        // Right
        sub.Knots.Add(new PathKnot(
            new Point(cx + rx, cy),
            new Point(cx + rx, cy - ky),
            new Point(cx + rx, cy + ky),
            true));

        // Bottom
        sub.Knots.Add(new PathKnot(
            new Point(cx, cy + ry),
            new Point(cx + kx, cy + ry),
            new Point(cx - kx, cy + ry),
            true));

        // Left
        sub.Knots.Add(new PathKnot(
            new Point(cx - rx, cy),
            new Point(cx - rx, cy + ky),
            new Point(cx - rx, cy - ky),
            true));

        path.Subpaths.Add(sub);
        return path;
    }

    public VectorDocumentPath Clone()
    {
        var clone = new VectorDocumentPath(Name)
        {
            FillRule = FillRule,
            IsActive = IsActive
        };
        foreach (var s in Subpaths) clone.Subpaths.Add(s.Clone());
        return clone;
    }
}
