using System;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class EditorCanvasControl
{
    private bool _isDraggingCornerRadius = false;
    private RectangleElement? _cornerRadiusTarget = null;
    private int _cornerRadiusGrabbedCorner = 0;
    private Point _cornerRadiusDragStart;
    private double _cornerRadiusStartRadius = 0;
    private bool _cornerRadiusAltHeld = false;
    private double _cornerRadiusLiveRadius = 0;

    private static readonly IBrush s_cornerWidgetFill = Brushes.White;
    private static readonly IPen s_cornerWidgetPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 120, 212)), 1.5);
    private static readonly IBrush s_cornerBadgeBg = new SolidColorBrush(Color.FromArgb(230, 24, 24, 24));
    private static readonly IPen s_cornerBadgeBorder = new Pen(new SolidColorBrush(Color.FromArgb(180, 70, 70, 70)), 1);
    private static readonly IBrush s_cornerBadgeText = Brushes.White;

    public bool ShowsCornerRadiusWidgets =>
        (ActiveToolMode == "rect" || ActiveToolMode == "pathselection" || (ActiveToolMode == "select" && ShowTransformControls));

    /// <summary>
    /// Gets corner widget center point for corner i (0=TL, 1=TR, 2=BR, 3=BL) in canvas coordinates.
    /// </summary>
    private static (Point Center, Point Dir) GetCornerWidgetPoint(RectangleElement rect, int i, double zoom)
    {
        double x = rect.X;
        double y = rect.Y;
        double w = Math.Max(1.0, rect.Width);
        double h = Math.Max(1.0, rect.Height);
        double maxR = Math.Min(w, h) / 2.0;
        double r = Math.Clamp(rect.CornerRadius, 10.0 / Math.Max(0.01, zoom), maxR);

        Point cornerPt;
        Point dir;
        switch (i)
        {
            case 0: // TL
                cornerPt = new Point(x, y);
                dir = new Point(1.0, 1.0);
                break;
            case 1: // TR
                cornerPt = new Point(x + w, y);
                dir = new Point(-1.0, 1.0);
                break;
            case 2: // BR
                cornerPt = new Point(x + w, y + h);
                dir = new Point(-1.0, -1.0);
                break;
            default: // BL
                cornerPt = new Point(x, y + h);
                dir = new Point(1.0, -1.0);
                break;
        }

        return (new Point(cornerPt.X + dir.X * r, cornerPt.Y + dir.Y * r), dir);
    }

    /// <summary>
    /// Draws corner radius circular widgets at each corner of selected rectangle elements.
    /// </summary>
    public void DrawCornerRadiusWidgets(DrawingContext dc)
    {
        if (!ShowsCornerRadiusWidgets || _selection.Count == 0) return;

        double widgetRadius = 3.5 / _zoom;
        var pen = new Pen(s_cornerWidgetPen.Brush, 1.5 / _zoom);

        foreach (var el in _selection)
        {
            if (el is not RectangleElement rect) continue;

            for (int i = 0; i < 4; i++)
            {
                var (center, _) = GetCornerWidgetPoint(rect, i, _zoom);
                dc.DrawEllipse(s_cornerWidgetFill, pen, center, widgetRadius, widgetRadius);
            }
        }

        // Live badge readout while dragging
        if (_isDraggingCornerRadius && _cornerRadiusTarget != null)
        {
            var text = new FormattedText(
                $"R: {_cornerRadiusLiveRadius:0.0} px",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI, -apple-system, sans-serif"),
                11.0 / _zoom,
                s_cornerBadgeText);

            var (cPt, _) = GetCornerWidgetPoint(_cornerRadiusTarget, _cornerRadiusGrabbedCorner, _zoom);
            double pad = 4.0 / _zoom;
            var badgeRect = new Rect(cPt.X + 10 / _zoom, cPt.Y - 18 / _zoom, text.Width + pad * 2, text.Height + pad * 2);

            dc.DrawRectangle(s_cornerBadgeBg, s_cornerBadgeBorder, badgeRect, 2 / _zoom, 2 / _zoom);
            dc.DrawText(text, new Point(badgeRect.X + pad, badgeRect.Y + pad));
        }
    }

    /// <summary>
    /// Hit-tests corner radius widgets. Returns true if a widget was hit and starts drag.
    /// </summary>
    public bool TryStartCornerRadiusDrag(Point canvasPt, bool altHeld)
    {
        if (!ShowsCornerRadiusWidgets || _selection.Count == 0) return false;

        double hitRadius = 8.0 / _zoom;

        foreach (var el in _selection)
        {
            if (el is not RectangleElement rect) continue;

            for (int i = 0; i < 4; i++)
            {
                var (center, _) = GetCornerWidgetPoint(rect, i, _zoom);
                if (Point.Distance(canvasPt, center) <= hitRadius)
                {
                    _isDraggingCornerRadius = true;
                    _cornerRadiusTarget = rect;
                    _cornerRadiusGrabbedCorner = i;
                    _cornerRadiusDragStart = canvasPt;
                    _cornerRadiusStartRadius = rect.CornerRadius;
                    _cornerRadiusAltHeld = altHeld;
                    _cornerRadiusLiveRadius = rect.CornerRadius;
                    InvalidateVisual();
                    return true;
                }
            }
        }

        return false;
    }

    public void HandleCornerRadiusPointerMoved(Point canvasPt)
    {
        if (!_isDraggingCornerRadius || _cornerRadiusTarget == null) return;

        var (_, dir) = GetCornerWidgetPoint(_cornerRadiusTarget, _cornerRadiusGrabbedCorner, _zoom);
        double delta = ((canvasPt.X - _cornerRadiusDragStart.X) * dir.X + (canvasPt.Y - _cornerRadiusDragStart.Y) * dir.Y) / 2.0;

        double maxR = Math.Min(_cornerRadiusTarget.Width, _cornerRadiusTarget.Height) / 2.0;
        double newR = Math.Clamp(_cornerRadiusStartRadius + delta, 0.0, maxR);
        _cornerRadiusLiveRadius = Math.Round(newR, 1);
        _cornerRadiusTarget.CornerRadius = _cornerRadiusLiveRadius;

        CanvasChanged?.Invoke();
        InvalidateVisual();
    }

    public void HandleCornerRadiusPointerReleased()
    {
        if (_isDraggingCornerRadius && _cornerRadiusTarget != null)
        {
            _isDraggingCornerRadius = false;
            CanvasChanged?.Invoke();
            InvalidateVisual();
        }
        _cornerRadiusTarget = null;
    }
}
