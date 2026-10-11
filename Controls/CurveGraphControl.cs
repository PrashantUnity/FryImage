using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

/// <summary>
/// Professional Interactive Curves Tone Graph Control.
/// Renders a 256x256 tone mapping graph with 4x4 subdivisions, background channel histogram,
/// smooth Fritsch-Carlson monotone cubic Hermite spline curve, and draggable control handles.
/// </summary>
public sealed class CurveGraphControl : Control
{
    public static readonly StyledProperty<ObservableCollection<CurvePoint>?> PointsProperty =
        AvaloniaProperty.Register<CurveGraphControl, ObservableCollection<CurvePoint>?>(nameof(Points));

    public static readonly StyledProperty<string> ChannelNameProperty =
        AvaloniaProperty.Register<CurveGraphControl, string>(nameof(ChannelName), "RGB");

    public static readonly StyledProperty<int[]?> HistogramBinsProperty =
        AvaloniaProperty.Register<CurveGraphControl, int[]?>(nameof(HistogramBins));

    public static readonly StyledProperty<int> SelectedPointIndexProperty =
        AvaloniaProperty.Register<CurveGraphControl, int>(nameof(SelectedPointIndex), -1);

    public ObservableCollection<CurvePoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public string ChannelName
    {
        get => GetValue(ChannelNameProperty);
        set => SetValue(ChannelNameProperty, value);
    }

    public int[]? HistogramBins
    {
        get => GetValue(HistogramBinsProperty);
        set => SetValue(HistogramBinsProperty, value);
    }

    public int SelectedPointIndex
    {
        get => GetValue(SelectedPointIndexProperty);
        set => SetValue(SelectedPointIndexProperty, value);
    }

    public event Action? CurveChanged;
    public event Action<CurvePoint>? SelectedPointChanged;

    private bool _isDragging;
    private int _dragPointIndex = -1;

    static CurveGraphControl()
    {
        AffectsRender<CurveGraphControl>(PointsProperty, ChannelNameProperty, HistogramBinsProperty, SelectedPointIndexProperty);
    }

    public CurveGraphControl()
    {
        ClipToBounds = true;
    }

    private Color GetChannelColor() => ChannelName switch
    {
        "Red" => Color.FromRgb(0xEF, 0x44, 0x44),
        "Green" => Color.FromRgb(0x22, 0xC5, 0x5E),
        "Blue" => Color.FromRgb(0x3B, 0x82, 0xF6),
        _ => Color.FromRgb(0xFA, 0xFA, 0xFA)
    };

    private Point ValueToScreen(double inVal, double outVal, Rect bounds)
    {
        double x = bounds.Left + (inVal / 255.0) * bounds.Width;
        double y = bounds.Bottom - (outVal / 255.0) * bounds.Height;
        return new Point(x, y);
    }

    private (double In, double Out) ScreenToValue(Point pt, Rect bounds)
    {
        double inVal = Math.Clamp(((pt.X - bounds.Left) / Math.Max(1.0, bounds.Width)) * 255.0, 0.0, 255.0);
        double outVal = Math.Clamp(((bounds.Bottom - pt.Y) / Math.Max(1.0, bounds.Height)) * 255.0, 0.0, 255.0);
        return (inVal, outVal);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        if (bounds.Width <= 4 || bounds.Height <= 4) return;

        // 1. Graph Background Fill
        var bgBrush = new SolidColorBrush(Color.FromRgb(0x14, 0x18, 0x22));
        dc.DrawRectangle(bgBrush, null, bounds);

        // 2. 4x4 Grid Lines (0, 64, 128, 192, 255)
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF)), 1.0);
        for (int i = 1; i <= 3; i++)
        {
            double gx = bounds.Left + (i / 4.0) * bounds.Width;
            double gy = bounds.Top + (i / 4.0) * bounds.Height;
            dc.DrawLine(gridPen, new Point(gx, bounds.Top), new Point(gx, bounds.Bottom));
            dc.DrawLine(gridPen, new Point(bounds.Left, gy), new Point(bounds.Right, gy));
        }

        // 3. Diagonal Identity Reference Line (0,0 -> 255,255)
        var identityPen = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x94, 0xA3, 0xB8)), 1.0,
            dashStyle: new DashStyle(new double[] { 3, 3 }, 0));
        dc.DrawLine(identityPen, new Point(bounds.Left, bounds.Bottom), new Point(bounds.Right, bounds.Top));

        // 4. Background Histogram Silhouette
        if (HistogramBins != null && HistogramBins.Length >= 256)
        {
            int maxBin = 1;
            for (int i = 1; i < 255; i++)
            {
                if (HistogramBins[i] > maxBin) maxBin = HistogramBins[i];
            }

            var histGeom = new StreamGeometry();
            using (var ctx = histGeom.Open())
            {
                ctx.BeginFigure(new Point(bounds.Left, bounds.Bottom), true);
                for (int x = 0; x < 256; x++)
                {
                    double inRatio = x / 255.0;
                    double hRatio = Math.Clamp(HistogramBins[x] / (double)maxBin, 0.0, 1.0);
                    double px = bounds.Left + inRatio * bounds.Width;
                    double py = bounds.Bottom - hRatio * (bounds.Height * 0.85);
                    ctx.LineTo(new Point(px, py));
                }
                ctx.LineTo(new Point(bounds.Right, bounds.Bottom));
                ctx.EndFigure(true);
            }

            var histColor = GetChannelColor();
            var histBrush = new SolidColorBrush(Color.FromArgb(0x28, histColor.R, histColor.G, histColor.B));
            dc.DrawGeometry(histBrush, null, histGeom);
        }

        // 5. Evaluate Spline Curve
        var pointsList = Points?.ToList() ?? new List<CurvePoint> { new(0, 0), new(255, 255) };
        byte[] lut = CurveSplineEngine.BuildLut(pointsList);

        var curveGeom = new StreamGeometry();
        using (var ctx = curveGeom.Open())
        {
            Point startPt = ValueToScreen(0, lut[0], bounds);
            ctx.BeginFigure(startPt, false);
            for (int x = 1; x < 256; x++)
            {
                Point pt = ValueToScreen(x, lut[x], bounds);
                ctx.LineTo(pt);
            }
            ctx.EndFigure(false);
        }

        var channelColor = GetChannelColor();
        var curvePen = new Pen(new SolidColorBrush(channelColor), 2.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        dc.DrawGeometry(null, curvePen, curveGeom);

        // 6. Draw Control Handles
        for (int i = 0; i < pointsList.Count; i++)
        {
            var cp = pointsList[i];
            Point screenPt = ValueToScreen(cp.In, cp.Out, bounds);
            bool isSelected = (i == SelectedPointIndex);

            double radius = isSelected ? 5.5 : 4.0;
            var fillBrush = isSelected ? new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)) : new SolidColorBrush(Color.FromRgb(0x14, 0x18, 0x22));
            var borderPen = isSelected
                ? new Pen(Brushes.White, 2.0)
                : new Pen(new SolidColorBrush(channelColor), 1.5);

            dc.DrawEllipse(fillBrush, borderPen, screenPt, radius, radius);
        }

        // 7. Graph Border
        var borderOutline = new Pen(new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), 1.0);
        dc.DrawRectangle(null, borderOutline, bounds);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Points == null) return;

        var bounds = Bounds;
        var mousePt = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;

        // Check if clicked near existing point
        int hitIndex = -1;
        double minDistance = 10.0;
        for (int i = 0; i < Points.Count; i++)
        {
            Point sp = ValueToScreen(Points[i].In, Points[i].Out, bounds);
            double dist = Point.Distance(mousePt, sp);
            if (dist < minDistance)
            {
                minDistance = dist;
                hitIndex = i;
            }
        }

        if (props.IsRightButtonPressed)
        {
            // Right-click deletes point if not endpoint
            if (hitIndex > 0 && hitIndex < Points.Count - 1)
            {
                Points.RemoveAt(hitIndex);
                SelectedPointIndex = -1;
                CurveChanged?.Invoke();
                InvalidateVisual();
            }
            return;
        }

        if (hitIndex >= 0)
        {
            SelectedPointIndex = hitIndex;
            SelectedPointChanged?.Invoke(Points[hitIndex]);
            _dragPointIndex = hitIndex;
            _isDragging = true;
            e.Pointer.Capture(this);
        }
        else
        {
            // Insert new point on curve
            var (inVal, outVal) = ScreenToValue(mousePt, bounds);
            var newPoint = new CurvePoint(Math.Round(inVal), Math.Round(outVal));

            // Find insertion index to maintain sorted order
            int insertIdx = 0;
            while (insertIdx < Points.Count && Points[insertIdx].In < newPoint.In)
            {
                insertIdx++;
            }

            Points.Insert(insertIdx, newPoint);
            SelectedPointIndex = insertIdx;
            SelectedPointChanged?.Invoke(newPoint);
            _dragPointIndex = insertIdx;
            _isDragging = true;
            e.Pointer.Capture(this);
            CurveChanged?.Invoke();
        }

        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging || _dragPointIndex < 0 || Points == null || _dragPointIndex >= Points.Count)
            return;

        var bounds = Bounds;
        var mousePt = e.GetPosition(this);
        var (inVal, outVal) = ScreenToValue(mousePt, bounds);

        // Constrain endpoints
        if (_dragPointIndex == 0)
        {
            inVal = 0;
        }
        else if (_dragPointIndex == Points.Count - 1)
        {
            inVal = 255;
        }
        else
        {
            // Constrain between previous and next points
            double prevIn = Points[_dragPointIndex - 1].In + 1;
            double nextIn = Points[_dragPointIndex + 1].In - 1;
            inVal = Math.Clamp(inVal, prevIn, nextIn);
        }

        var updated = new CurvePoint(Math.Round(inVal), Math.Round(outVal));
        Points[_dragPointIndex] = updated;
        SelectedPointChanged?.Invoke(updated);
        CurveChanged?.Invoke();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            _dragPointIndex = -1;
            e.Pointer.Capture(null);
            CurveChanged?.Invoke();
            InvalidateVisual();
        }
    }
}
