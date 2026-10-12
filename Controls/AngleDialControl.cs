using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

/// <summary>
/// Photoshop-compatible interactive circular Angle Dial control.
/// Provides a sleek rotary needle dial for directional filters (Motion Blur, Emboss, Drop Shadow, etc.)
/// where angle counts degrees counter-clockwise or clockwise from three o'clock.
/// </summary>
public class AngleDialControl : Control
{
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<AngleDialControl, double>(
            nameof(Angle), defaultValue: 0.0);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<AngleDialControl, double>(
            nameof(Minimum), defaultValue: -180.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<AngleDialControl, double>(
            nameof(Maximum), defaultValue: 180.0);

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public event EventHandler<double>? AngleChanged;

    private bool _isDragging;

    static AngleDialControl()
    {
        AffectsRender<AngleDialControl>(AngleProperty, MinimumProperty, MaximumProperty);
    }

    public AngleDialControl()
    {
        Width = 44;
        Height = 44;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);

        double w = Bounds.Width;
        double h = Bounds.Height;
        double cx = w / 2.0;
        double cy = h / 2.0;
        double radius = Math.Min(cx, cy) - 2.5;

        if (radius <= 2) return;

        // 1. Dial Background Disc
        var bgBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), 1.2);
        dc.DrawEllipse(bgBrush, borderPen, new Point(cx, cy), radius, radius);

        // 2. Cardinal Tick Marks (0°, 90°, 180°, 270°)
        var tickPen = new Pen(new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)), 1.0);
        double tickInner = radius - 3.5;
        double tickOuter = radius - 0.5;

        // 0 deg (3 o'clock)
        dc.DrawLine(tickPen, new Point(cx + tickInner, cy), new Point(cx + tickOuter, cy));
        // 90 deg (6 o'clock)
        dc.DrawLine(tickPen, new Point(cx, cy + tickInner), new Point(cx, cy + tickOuter));
        // 180 deg (9 o'clock)
        dc.DrawLine(tickPen, new Point(cx - tickInner, cy), new Point(cx - tickOuter, cy));
        // 270 deg / -90 deg (12 o'clock)
        dc.DrawLine(tickPen, new Point(cx, cy - tickInner), new Point(cx, cy - tickOuter));

        // 3. Center Pivot Dot
        var pivotBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        dc.DrawEllipse(pivotBrush, null, new Point(cx, cy), 2.2, 2.2);

        // 4. Pointer Needle from center to (radius - 4)
        // In Photoshop: Motion Blur / Drop Shadow angle has 0° at 3 o'clock,
        // and positive angle goes counter-clockwise (or clockwise on screen Y depending on sign).
        // Standard Cartesian math: -Angle in radians matches standard UI coordinates (Y down).
        double angleRad = -Angle * (Math.PI / 180.0);
        double needleLen = radius - 3.5;
        double nx = cx + needleLen * Math.Cos(angleRad);
        double ny = cy + needleLen * Math.Sin(angleRad);

        var needlePen = new Pen(new SolidColorBrush(Color.FromRgb(0x37, 0x8E, 0xF0)), 1.6);
        dc.DrawLine(needlePen, new Point(cx, cy), new Point(nx, ny));

        // 5. Needle End Knob
        var knobBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        var knobBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x37, 0x8E, 0xF0)), 1.2);
        dc.DrawEllipse(knobBrush, knobBorderPen, new Point(nx, ny), 3.0, 3.0);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _isDragging = true;
        e.Pointer.Capture(this);
        UpdateAngleFromPointer(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging)
        {
            UpdateAngleFromPointer(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void UpdateAngleFromPointer(Point pt)
    {
        double cx = Bounds.Width / 2.0;
        double cy = Bounds.Height / 2.0;
        double dx = pt.X - cx;
        double dy = pt.Y - cy;

        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001) return;

        // Angle in radians (inverted dy because Y axis goes down)
        double rad = Math.Atan2(-dy, dx);
        double deg = Math.Round(rad * 180.0 / Math.PI);

        if (Minimum >= 0 && Maximum > 180)
        {
            // 0..360 range
            if (deg < 0) deg += 360;
        }

        deg = Math.Clamp(deg, Minimum, Maximum);
        Angle = deg;
        InvalidateVisual();
        AngleChanged?.Invoke(this, deg);
    }
}
