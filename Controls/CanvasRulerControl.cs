using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PdfEditorApp.Core.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public class CanvasRulerControl : Control
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<CanvasRulerControl, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public static readonly StyledProperty<RulerUnit> UnitProperty =
        AvaloniaProperty.Register<CanvasRulerControl, RulerUnit>(nameof(Unit), RulerUnit.Points);

    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<CanvasRulerControl, double>(nameof(ZoomLevel), 1.0);

    public static readonly StyledProperty<double> CursorPositionProperty =
        AvaloniaProperty.Register<CanvasRulerControl, double>(nameof(CursorPosition), -1);

    public static readonly StyledProperty<double> PageOffsetProperty =
        AvaloniaProperty.Register<CanvasRulerControl, double>(nameof(PageOffset), 0.0);

    public static readonly StyledProperty<double> PageDimensionProperty =
        AvaloniaProperty.Register<CanvasRulerControl, double>(nameof(PageDimension), 800.0);

    public static readonly StyledProperty<EditorCanvasControl?> TargetCanvasProperty =
        AvaloniaProperty.Register<CanvasRulerControl, EditorCanvasControl?>(nameof(TargetCanvas));

    public EditorCanvasControl? TargetCanvas
    {
        get => GetValue(TargetCanvasProperty);
        set => SetValue(TargetCanvasProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public RulerUnit Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public double CursorPosition
    {
        get => GetValue(CursorPositionProperty);
        set => SetValue(CursorPositionProperty, value);
    }

    public double PageOffset
    {
        get => GetValue(PageOffsetProperty);
        set => SetValue(PageOffsetProperty, value);
    }

    public double PageDimension
    {
        get => GetValue(PageDimensionProperty);
        set => SetValue(PageDimensionProperty, value);
    }

    // Default Fallback Resources (FryImage Pro Dark palette)
    private static readonly IBrush s_defBgBrush = new SolidColorBrush(Color.Parse("#242424"));
    private static readonly IPen s_defBorderPen = new Pen(new SolidColorBrush(Color.Parse("#383838")), 1);
    private static readonly IPen s_defTickPen = new Pen(new SolidColorBrush(Color.Parse("#606060")), 1);
    private static readonly IPen s_defMajorTickPen = new Pen(new SolidColorBrush(Color.Parse("#808080")), 1);
    private static readonly IPen s_defPageBorderPen = new Pen(new SolidColorBrush(Color.Parse("#4A4A4A")), 1.5);
    private static readonly IPen s_cursorPen = new Pen(new SolidColorBrush(Color.Parse("#378EF0")), 1.5);
    private static readonly IBrush s_defPageShadeBrush = new SolidColorBrush(Color.Parse("#2D2D2D"));
    private static readonly IBrush s_defLabelBrush = new SolidColorBrush(Color.Parse("#9A9A9A"));
    private static readonly Typeface s_typeface = new("Segoe UI, -apple-system, sans-serif");

    static CanvasRulerControl()
    {
        AffectsRender<CanvasRulerControl>(
            OrientationProperty,
            UnitProperty,
            ZoomLevelProperty,
            CursorPositionProperty,
            PageOffsetProperty,
            PageDimensionProperty);
    }

    private Pen? _borderPen;
    private Pen? _tickPen;
    private Pen? _majorTickPen;

    private readonly Dictionary<(string Text, double Size), FormattedText> _labelCache = new();
    private IBrush? _labelCacheBrush;

    private static Pen GetCachedPen(ref Pen? cached, IBrush? brush)
    {
        if (cached == null || !ReferenceEquals(cached.Brush, brush))
        {
            cached = new Pen(brush, 1);
        }

        return cached;
    }

    private FormattedText GetCachedLabel(string label, IBrush? labelBrush, double fontSize = 8.5)
    {
        if (!ReferenceEquals(_labelCacheBrush, labelBrush))
        {
            _labelCache.Clear();
            _labelCacheBrush = labelBrush;
        }

        var key = (label, fontSize);
        if (_labelCache.TryGetValue(key, out var cached)) return cached;

        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            s_typeface,
            fontSize,
            labelBrush);

        if (_labelCache.Count >= MaxCachedLabels)
        {
            _labelCache.Clear();
        }

        _labelCache[key] = text;
        return text;
    }

    private const int MaxCachedLabels = 256;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        bool isHorizontal = Orientation == Orientation.Horizontal;

        var bgBrush = (this.TryFindResource("PsChromeBrush", out var bgObj) && bgObj is IBrush b1) ? b1 : s_defBgBrush;
        var borderBrush = (this.TryFindResource("PsBorderBrush", out var bdrObj) && bdrObj is IBrush b2) ? b2 : s_defBorderPen.Brush;
        var tickBrush = (this.TryFindResource("PsBorderLightBrush", out var tObj) && tObj is IBrush b3) ? b3 : s_defTickPen.Brush;
        var majorTickBrush = (this.TryFindResource("PsTextDimBrush", out var mtObj) && mtObj is IBrush b4) ? b4 : s_defMajorTickPen.Brush;

        var borderPen = GetCachedPen(ref _borderPen, borderBrush);
        var tickPen = GetCachedPen(ref _tickPen, tickBrush);
        var majorTickPen = GetCachedPen(ref _majorTickPen, majorTickBrush);
        var pageShadeBrush = (this.TryFindResource("PsPanelBrush", out var psObj) && psObj is IBrush b5) ? b5 : s_defPageShadeBrush;
        var labelBrush = majorTickBrush ?? s_defLabelBrush;

        context.FillRectangle(bgBrush, new Rect(0, 0, bounds.Width, bounds.Height));

        if (isHorizontal)
        {
            context.DrawLine(borderPen, new Point(0, bounds.Height), new Point(bounds.Width, bounds.Height));

            double zoom = Math.Max(0.05, ZoomLevel);
            double targetScreenSpacing = 100.0;
            double rawStep = targetScreenSpacing / zoom;
            double stepPts = Unit == RulerUnit.Inches ? (rawStep < 72 ? 36.0 : 72.0) : ComputeAdaptiveRulerStep(rawStep);
            double minorStepPts = stepPts / 5.0;

            double startX = PageOffset;
            double endX = startX + (PageDimension * zoom);

            context.FillRectangle(pageShadeBrush, new Rect(Math.Max(0, startX), 0, Math.Max(0, endX - Math.Max(0, startX)), bounds.Height));
            context.DrawLine(borderPen, new Point(startX, 0), new Point(startX, bounds.Height));
            context.DrawLine(borderPen, new Point(endX, 0), new Point(endX, bounds.Height));

            double maxPts = PageDimension + (bounds.Width / zoom);
            for (double pt = 0; pt <= maxPts; pt += minorStepPts)
            {
                double x = startX + (pt * zoom);
                if (x < 0 || x > bounds.Width) continue;

                bool isMajor = Math.Abs(pt % stepPts) < 0.01;
                double tickH = isMajor ? bounds.Height * 0.55 : bounds.Height * 0.25;

                context.DrawLine(isMajor ? majorTickPen : tickPen, new Point(x, bounds.Height - tickH), new Point(x, bounds.Height));

                if (isMajor)
                {
                    string label = Unit switch
                    {
                        RulerUnit.Inches => $"{pt / 72.0:0.#}\"",
                        RulerUnit.Millimeters => $"{pt * 25.4 / 72.0:0}",
                        _ => $"{pt:0}"
                    };

                    context.DrawText(GetCachedLabel(label, labelBrush), new Point(x + 2, 2));
                }
            }

            if (CursorPosition >= 0)
            {
                double cursorX = startX + (CursorPosition * zoom);
                if (cursorX >= 0 && cursorX <= bounds.Width)
                {
                    context.DrawLine(s_cursorPen, new Point(cursorX, 0), new Point(cursorX, bounds.Height));
                }
            }
        }
        else
        {
            context.DrawLine(borderPen, new Point(bounds.Width, 0), new Point(bounds.Width, bounds.Height));

            double zoom = Math.Max(0.05, ZoomLevel);
            double targetScreenSpacing = 100.0;
            double rawStep = targetScreenSpacing / zoom;
            double stepPts = Unit == RulerUnit.Inches ? (rawStep < 72 ? 36.0 : 72.0) : ComputeAdaptiveRulerStep(rawStep);
            double minorStepPts = stepPts / 5.0;

            double startY = PageOffset;
            double endY = startY + (PageDimension * zoom);

            context.FillRectangle(pageShadeBrush, new Rect(0, Math.Max(0, startY), bounds.Width, Math.Max(0, endY - Math.Max(0, startY))));
            context.DrawLine(borderPen, new Point(0, startY), new Point(bounds.Width, startY));
            context.DrawLine(borderPen, new Point(0, endY), new Point(bounds.Width, endY));

            double maxPts = PageDimension + (bounds.Height / zoom);
            for (double pt = 0; pt <= maxPts; pt += minorStepPts)
            {
                double y = startY + (pt * zoom);
                if (y < 0 || y > bounds.Height) continue;

                bool isMajor = Math.Abs(pt % stepPts) < 0.01;
                double tickW = isMajor ? bounds.Width * 0.55 : bounds.Width * 0.25;

                context.DrawLine(isMajor ? majorTickPen : tickPen, new Point(bounds.Width - tickW, y), new Point(bounds.Width, y));

                if (isMajor)
                {
                    string label = Unit switch
                    {
                        RulerUnit.Inches => $"{pt / 72.0:0.#}\"",
                        RulerUnit.Millimeters => $"{pt * 25.4 / 72.0:0}",
                        _ => $"{pt:0}"
                    };

                    context.DrawText(GetCachedLabel(label, labelBrush, fontSize: 8.0), new Point(2, y + 2));
                }
            }

            if (CursorPosition >= 0)
            {
                double cursorY = startY + (CursorPosition * zoom);
                if (cursorY >= 0 && cursorY <= bounds.Height)
                {
                    context.DrawLine(s_cursorPen, new Point(0, cursorY), new Point(bounds.Width, cursorY));
                }
            }
        }
    }

    // ── Interactive Ruler-Pulled Guide Drag ─────────────────────────────────
    private bool _isDraggingRulerGuide;

    private EditorCanvasControl? ResolveTargetCanvas()
    {
        if (TargetCanvas != null) return TargetCanvas;
        if (Parent is Visual parentVisual)
        {
            return parentVisual.GetVisualDescendants().OfType<EditorCanvasControl>().FirstOrDefault();
        }
        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var canvas = ResolveTargetCanvas();
        if (canvas == null) return;

        _isDraggingRulerGuide = true;
        e.Pointer.Capture(this);

        // For Horizontal ruler, dragging down creates a Horizontal guide (isVertical = false).
        // For Vertical ruler, dragging right creates a Vertical guide (isVertical = true).
        bool isGuideVertical = Orientation == Orientation.Vertical;

        var ptOnCanvas = this.TranslatePoint(e.GetPosition(this), canvas);
        if (ptOnCanvas.HasValue)
        {
            canvas.BeginGuideDragFromRuler(isGuideVertical, ptOnCanvas.Value);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDraggingRulerGuide) return;

        var canvas = ResolveTargetCanvas();
        if (canvas == null) return;

        bool isGuideVertical = Orientation == Orientation.Vertical;
        var ptOnCanvas = this.TranslatePoint(e.GetPosition(this), canvas);
        if (ptOnCanvas.HasValue)
        {
            canvas.UpdateGuideDrag(isGuideVertical, ptOnCanvas.Value);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isDraggingRulerGuide) return;

        _isDraggingRulerGuide = false;
        e.Pointer.Capture(null);

        var canvas = ResolveTargetCanvas();
        if (canvas != null)
        {
            var ptOnCanvas = this.TranslatePoint(e.GetPosition(this), canvas);
            canvas.EndGuideDrag(commit: true, ptOnCanvas);
        }

        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        if (_isDraggingRulerGuide)
        {
            _isDraggingRulerGuide = false;
            ResolveTargetCanvas()?.CancelGuideDrag();
        }
    }

    private static double ComputeAdaptiveRulerStep(double rawStep)
    {
        if (rawStep <= 0 || double.IsNaN(rawStep) || double.IsInfinity(rawStep))
            return 100.0;

        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        double normalized = rawStep / magnitude;
        double nice;
        if (normalized < 1.5) nice = 1.0;
        else if (normalized < 3.0) nice = 2.0;
        else if (normalized < 7.0) nice = 5.0;
        else nice = 10.0;
        return nice * magnitude;
    }
}
