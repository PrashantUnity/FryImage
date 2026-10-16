using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;
using PdfEditorApp.Plugins.ImageEditor.Models;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class EditorCanvasControl
{
    // ── Lasso & Magnetic Lasso State ─────────────────────────────────────────
    private bool _isLassoActive;
    private readonly List<Point> _lassoPoints = new();
    private readonly List<Point> _lassoKnots = new();
    private Point _lassoCurrentCursor;
    private float[]? _magneticCostMap;
    private int _magneticCostMapW;
    private int _magneticCostMapH;
    private Point _magneticAnchorPt;
    private double _magneticDistSinceLastKnot;

    public string LassoMode { get; set; } = "standard"; // "standard" | "polygonal" | "magnetic"
    public double MagneticWidth { get; set; } = 10;
    public double MagneticContrast { get; set; } = 10;
    public double MagneticFrequency { get; set; } = 57;

    public event Action<IReadOnlyList<Point>>? LassoSelectionCompleted;

    /// <summary>
    /// Starts a new lasso contour at the specified canvas point.
    /// </summary>
    public void StartLasso(Point canvasPt)
    {
        _isLassoActive = true;
        _lassoPoints.Clear();
        _lassoKnots.Clear();
        _lassoPoints.Add(canvasPt);
        _lassoKnots.Add(canvasPt);
        _magneticAnchorPt = canvasPt;
        _magneticDistSinceLastKnot = 0;
        _lassoCurrentCursor = canvasPt;

        if (LassoMode == "magnetic")
        {
            EnsureMagneticCostMap(canvasPt);
        }
        InvalidateVisual();
    }

    /// <summary>
    /// Adds an anchor knot to polygonal or magnetic lasso.
    /// </summary>
    public void AddLassoKnot(Point canvasPt)
    {
        if (!_isLassoActive)
        {
            StartLasso(canvasPt);
            return;
        }

        if (_lassoKnots.Count > 2 && Point.Distance(canvasPt, _lassoKnots[0]) < 8.0 / _zoom)
        {
            CompleteLassoSelection();
            return;
        }

        _lassoKnots.Add(canvasPt);
        _lassoPoints.Add(canvasPt);
        _magneticAnchorPt = canvasPt;
        _magneticDistSinceLastKnot = 0;
        _lassoCurrentCursor = canvasPt;
        InvalidateVisual();
    }

    /// <summary>
    /// Updates cursor position and live wire tracking.
    /// </summary>
    public void UpdateLassoCursor(Point canvasPt)
    {
        _lassoCurrentCursor = canvasPt;

        if (LassoMode == "standard")
        {
            if (_lassoPoints.Count == 0 || Point.Distance(_lassoPoints[^1], canvasPt) >= 1.5 / _zoom)
            {
                _lassoPoints.Add(canvasPt);
                InvalidateVisual();
            }
        }
        else if (LassoMode == "polygonal")
        {
            InvalidateVisual();
        }
        else if (LassoMode == "magnetic")
        {
            if (_magneticCostMap != null && _magneticCostMapW > 0 && _magneticCostMapH > 0)
            {
                // Live wire tracking from last anchor knot to current cursor
                var liveWire = MagneticLassoEngine.FindLiveWirePath(
                    _magneticCostMap,
                    _magneticCostMapW,
                    _magneticCostMapH,
                    _magneticAnchorPt,
                    canvasPt,
                    searchRadius: (int)Math.Max(40, MagneticWidth * 8));

                if (liveWire.Count > 0)
                {
                    double stepLen = Point.Distance(_magneticAnchorPt, canvasPt);
                    _magneticDistSinceLastKnot += stepLen;

                    double knotSpacing = Math.Max(20, 150 - MagneticFrequency);
                    if (_magneticDistSinceLastKnot >= knotSpacing)
                    {
                        var lastPt = liveWire[^1];
                        _lassoKnots.Add(lastPt);
                        _magneticAnchorPt = lastPt;
                        _magneticDistSinceLastKnot = 0;
                        _lassoPoints.AddRange(liveWire);
                    }
                }
            }
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Handles pointer pressed for Lasso tools.
    /// </summary>
    public void HandleLassoPointerPressed(Point canvasPt, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) return;

        e.Pointer.Capture(this);
        e.Handled = true;

        if (LassoMode == "standard")
        {
            StartLasso(canvasPt);
        }
        else if (LassoMode == "polygonal")
        {
            AddLassoKnot(canvasPt);
        }
        else if (LassoMode == "magnetic")
        {
            AddLassoKnot(canvasPt);
        }
    }

    /// <summary>
    /// Handles pointer movement for live-wire and contour updates.
    /// </summary>
    public void HandleLassoPointerMoved(Point canvasPt, PointerEventArgs e)
    {
        UpdateLassoCursor(canvasPt);
    }

    /// <summary>
    /// Handles pointer release for standard freeform lasso.
    /// </summary>
    public void HandleLassoPointerReleased(Point canvasPt, PointerReleasedEventArgs e)
    {
        if (LassoMode == "standard")
        {
            _lassoPoints.Add(canvasPt);
            CompleteLassoSelection();
        }
    }

    /// <summary>
    /// Closes the lasso polygon and converts the contour to an active selection.
    /// </summary>
    public void CompleteLassoSelection()
    {
        if (_lassoPoints.Count < 3 && _lassoKnots.Count < 3)
        {
            _isLassoActive = false;
            _lassoPoints.Clear();
            _lassoKnots.Clear();
            InvalidateVisual();
            return;
        }

        var polygon = new List<Point>(_lassoPoints);
        if (LassoMode is "polygonal" or "magnetic" && _lassoKnots.Count >= 3)
        {
            polygon = new List<Point>(_lassoKnots);
        }

        // Close polygon loop
        if (polygon.Count > 0 && polygon[0] != polygon[^1])
        {
            polygon.Add(polygon[0]);
        }

        // 1. Select vector/canvas elements intersecting or inside polygon
        var minX = polygon.Min(p => p.X);
        var maxX = polygon.Max(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var maxY = polygon.Max(p => p.Y);
        var bounds = new Rect(minX, minY, maxX - minX, maxY - minY);

        var hits = _elements.Where(el => el.IsVisible && !el.IsLocked && bounds.Intersects(el.GetRotatedBounds())).ToList();
        if (hits.Count > 0)
        {
            _selection.Clear();
            _selection.AddRange(hits);
            NotifySelectionChanged();
        }

        // 2. If Quick Mask mode is active, fill raster polygon mask
        if (IsQuickMaskMode && QuickMaskBuffer != null)
        {
            FillPolygonIntoQuickMask(polygon);
        }

        _lastCompletedLassoPolygon = polygon.ToList();
        LassoSelectionCompleted?.Invoke(polygon);

        _isLassoActive = false;
        _lassoPoints.Clear();
        _lassoKnots.Clear();
        _magneticCostMap = null;
        InvalidateVisual();
    }

    public void CancelLasso()
    {
        _isLassoActive = false;
        _lassoPoints.Clear();
        _lassoKnots.Clear();
        _magneticCostMap = null;
        InvalidateVisual();
    }

    private List<Point>? _lastCompletedLassoPolygon;
    public IReadOnlyList<Point>? GetActiveSelectionPolygon() => _lastCompletedLassoPolygon;

    private void EnsureMagneticCostMap(Point startPt)
    {
        // Find raster image under start point
        var imgElement = _elements.OfType<ImageElement>().LastOrDefault(img => img.GetRotatedBounds().Contains(startPt));
        if (imgElement?.OriginalImageBytes != null)
        {
            try
            {
                using var skBmp = SKBitmap.Decode(imgElement.OriginalImageBytes);
                if (skBmp != null)
                {
                    _magneticCostMap = MagneticLassoEngine.ComputeEdgeCostMap(skBmp);
                    _magneticCostMapW = skBmp.Width;
                    _magneticCostMapH = skBmp.Height;
                    return;
                }
            }
            catch { }
        }

        // Fallback: Synthesize local contrast map from canvas dimensions
        int w = (int)Math.Max(10, _logicalWidth);
        int h = (int)Math.Max(10, _logicalHeight);
        _magneticCostMap = new float[w * h];
        Array.Fill(_magneticCostMap, 0.5f);
        _magneticCostMapW = w;
        _magneticCostMapH = h;
    }

    private void FillPolygonIntoQuickMask(List<Point> polygon)
    {
        int w = (int)Math.Max(1, _logicalWidth);
        int h = (int)Math.Max(1, _logicalHeight);
        if (QuickMaskBuffer == null || QuickMaskBuffer.Length < w * h) return;

        // Scanline rasterization
        int minY = Math.Clamp((int)Math.Floor(polygon.Min(p => p.Y)), 0, h - 1);
        int maxY = Math.Clamp((int)Math.Ceiling(polygon.Max(p => p.Y)), 0, h - 1);

        for (int y = minY; y <= maxY; y++)
        {
            var nodeX = new List<double>();
            for (int i = 0; i < polygon.Count - 1; i++)
            {
                var p1 = polygon[i];
                var p2 = polygon[i + 1];
                if ((p1.Y < y && p2.Y >= y) || (p2.Y < y && p1.Y >= y))
                {
                    double x = p1.X + (y - p1.Y) / (p2.Y - p1.Y) * (p2.X - p1.X);
                    nodeX.Add(x);
                }
            }

            nodeX.Sort();
            for (int i = 0; i < nodeX.Count - 1; i += 2)
            {
                int startX = Math.Clamp((int)Math.Floor(nodeX[i]), 0, w - 1);
                int endX = Math.Clamp((int)Math.Ceiling(nodeX[i + 1]), 0, w - 1);
                int row = y * w;
                for (int x = startX; x <= endX; x++)
                {
                    QuickMaskBuffer[row + x] = 255;
                }
            }
        }
    }

    /// <summary>
    /// Renders the lasso polygon, live wire path, and anchor knots onto the canvas.
    /// </summary>
    private void DrawLassoOverlay(DrawingContext dc, double zoom)
    {
        double strokeW = Math.Max(1.0, 1.0 / zoom);

        // Dashed marching ants pen
        var strokePen = new Pen(new SolidColorBrush(Color.FromArgb(240, 0, 180, 255)), strokeW,
            new DashStyle(new double[] { 4, 3 }, 0));
        var shadowPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), strokeW * 1.5);

        // 1. Draw contour segments
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            if (LassoMode == "standard" && _lassoPoints.Count > 1)
            {
                ctx.BeginFigure(_lassoPoints[0], false);
                for (int i = 1; i < _lassoPoints.Count; i++)
                {
                    ctx.LineTo(_lassoPoints[i]);
                }
                if (_isLassoActive)
                {
                    ctx.LineTo(_lassoCurrentCursor);
                }
                ctx.EndFigure(false);
            }
            else if (LassoMode is "polygonal" or "magnetic" && _lassoKnots.Count > 0)
            {
                ctx.BeginFigure(_lassoKnots[0], false);
                for (int i = 1; i < _lassoKnots.Count; i++)
                {
                    ctx.LineTo(_lassoKnots[i]);
                }
                if (_isLassoActive)
                {
                    ctx.LineTo(_lassoCurrentCursor);
                }
                ctx.EndFigure(false);
            }
        }

        dc.DrawGeometry(null, shadowPen, geom);
        dc.DrawGeometry(null, strokePen, geom);

        // 2. Draw anchor knots (white squares with dark border)
        double knotSize = 5.0 / zoom;
        var knotFill = new SolidColorBrush(Colors.White);
        var knotBorder = new Pen(new SolidColorBrush(Color.FromArgb(240, 20, 20, 20)), strokeW);

        foreach (var knot in _lassoKnots)
        {
            var knotRect = new Rect(knot.X - knotSize / 2, knot.Y - knotSize / 2, knotSize, knotSize);
            dc.DrawRectangle(knotFill, knotBorder, knotRect);
        }

        // Highlight origin knot when mouse is near start
        if (_lassoKnots.Count > 2 && Point.Distance(_lassoCurrentCursor, _lassoKnots[0]) < 8.0 / zoom)
        {
            var origin = _lassoKnots[0];
            var highlightPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 220, 50)), strokeW * 2);
            dc.DrawEllipse(null, highlightPen, origin, 6.0 / zoom, 6.0 / zoom);
        }
    }
}
