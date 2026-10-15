using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

/// <summary>
/// Industry-compatible interactive Gradient Map ramp editor.
/// Supports clicking along the spectrum to add color stops, dragging stops horizontally,
/// and selecting stops for color/location tuning.
/// </summary>
public class GradientMapEditorControl : Control
{
    public static readonly StyledProperty<ObservableCollection<GradientMapRampStop>?> StopsProperty =
        AvaloniaProperty.Register<GradientMapEditorControl, ObservableCollection<GradientMapRampStop>?>(
            nameof(Stops), defaultValue: null);

    public static readonly StyledProperty<int> SelectedStopIndexProperty =
        AvaloniaProperty.Register<GradientMapEditorControl, int>(
            nameof(SelectedStopIndex), defaultValue: 0);

    public static readonly StyledProperty<bool> IsReversedProperty =
        AvaloniaProperty.Register<GradientMapEditorControl, bool>(
            nameof(IsReversed), defaultValue: false);

    public ObservableCollection<GradientMapRampStop>? Stops
    {
        get => GetValue(StopsProperty);
        set => SetValue(StopsProperty, value);
    }

    public int SelectedStopIndex
    {
        get => GetValue(SelectedStopIndexProperty);
        set => SetValue(SelectedStopIndexProperty, value);
    }

    public bool IsReversed
    {
        get => GetValue(IsReversedProperty);
        set => SetValue(IsReversedProperty, value);
    }

    public event EventHandler? StopsChanged;

    private bool _isDragging;
    private int _draggedStopIndex = -1;

    static GradientMapEditorControl()
    {
        AffectsRender<GradientMapEditorControl>(StopsProperty, SelectedStopIndexProperty, IsReversedProperty);
    }

    public GradientMapEditorControl()
    {
        ClipToBounds = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StopsProperty)
        {
            if (change.OldValue is ObservableCollection<GradientMapRampStop> oldColl)
                oldColl.CollectionChanged -= OnCollectionChanged;

            if (change.NewValue is ObservableCollection<GradientMapRampStop> newColl)
                newColl.CollectionChanged += OnCollectionChanged;

            InvalidateVisual();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
        StopsChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);

        double w = Math.Max(10, Bounds.Width);
        const double barHeight = 22.0;
        var barRect = new Rect(0, 0, w, barHeight);

        var stops = Stops;
        if (stops == null || stops.Count == 0)
        {
            dc.DrawRectangle(Brushes.Gray, new Pen(new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)), 1), barRect);
            return;
        }

        // 1. Draw Gradient Spectrum Bar
        var sortedStops = stops.OrderBy(s => s.Position).ToList();
        var brushStops = new GradientStops();

        foreach (var stop in sortedStops)
        {
            double pos = IsReversed ? (1.0 - stop.Position) : stop.Position;
            brushStops.Add(new Avalonia.Media.GradientStop(stop.Color, Math.Clamp(pos, 0.0, 1.0)));
        }

        // Sort brush stops by offset for correct rendering if reversed
        var orderedBrushStops = new GradientStops();
        foreach (var bs in brushStops.OrderBy(s => s.Offset))
        {
            orderedBrushStops.Add(bs);
        }

        var gradBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = orderedBrushStops
        };

        dc.DrawRectangle(gradBrush, null, barRect);
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0x4E, 0x4E, 0x4E)), 1), barRect);

        // 2. Draw Color Stop Markers Below Bar
        var accentBrush = new SolidColorBrush(Color.FromRgb(0x37, 0x8E, 0xF0));
        var dimBorderBrush = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));

        for (int i = 0; i < stops.Count; i++)
        {
            var s = stops[i];
            double cx = Math.Clamp(s.Position * w, 5.0, w - 5.0);
            bool isSelected = (i == SelectedStopIndex);

            var borderBrush = isSelected ? accentBrush : dimBorderBrush;
            double strokeWidth = isSelected ? 1.5 : 1.0;

            // Marker Arrow (triangle pointing up to bar bottom)
            var arrowGeometry = new StreamGeometry();
            using (var ctx = arrowGeometry.Open())
            {
                ctx.BeginFigure(new Point(cx, barHeight + 2), isFilled: true);
                ctx.LineTo(new Point(cx - 5, barHeight + 7));
                ctx.LineTo(new Point(cx + 5, barHeight + 7));
                ctx.EndFigure(isClosed: true);
            }
            dc.DrawGeometry(borderBrush, null, arrowGeometry);

            // Marker Color Chip Box
            var chipRect = new Rect(cx - 5.5, barHeight + 7, 11, 11);
            dc.DrawRectangle(new SolidColorBrush(s.Color), null, chipRect);
            dc.DrawRectangle(null, new Pen(borderBrush, strokeWidth), chipRect);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Stops == null || Stops.Count == 0) return;
        var pt = e.GetPosition(this);
        double w = Math.Max(10, Bounds.Width);

        // Check if hit an existing stop marker
        const double barHeight = 22.0;
        int hitIndex = -1;

        for (int i = 0; i < Stops.Count; i++)
        {
            double cx = Math.Clamp(Stops[i].Position * w, 5.0, w - 5.0);
            var markerBounds = new Rect(cx - 7, barHeight, 14, 20);
            if (markerBounds.Contains(pt))
            {
                hitIndex = i;
                break;
            }
        }

        if (hitIndex >= 0)
        {
            SelectedStopIndex = hitIndex;
            _isDragging = true;
            _draggedStopIndex = hitIndex;
            e.Pointer.Capture(this);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // If clicked on the bar itself, add a new stop at pt.X
        if (pt.Y >= 0 && pt.Y <= barHeight + 4 && Stops.Count < 32)
        {
            float pos = (float)Math.Clamp(pt.X / w, 0.0, 1.0);
            Color sampleColor = SampleColorAt(pos);

            var newStop = new GradientMapRampStop(pos, sampleColor);
            Stops.Add(newStop);
            SelectedStopIndex = Stops.Count - 1;

            _isDragging = true;
            _draggedStopIndex = SelectedStopIndex;
            e.Pointer.Capture(this);

            InvalidateVisual();
            StopsChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isDragging && _draggedStopIndex >= 0 && Stops != null && _draggedStopIndex < Stops.Count)
        {
            var pt = e.GetPosition(this);
            double w = Math.Max(10, Bounds.Width);
            float newPos = (float)Math.Clamp(pt.X / w, 0.0, 1.0);

            Stops[_draggedStopIndex].Position = newPos;
            InvalidateVisual();
            StopsChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDragging)
        {
            _isDragging = false;
            _draggedStopIndex = -1;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    public Color SampleColorAt(float t)
    {
        if (Stops == null || Stops.Count == 0) return Colors.Black;
        var sorted = Stops.OrderBy(s => s.Position).ToList();

        if (t <= sorted[0].Position) return sorted[0].Color;
        if (t >= sorted[^1].Position) return sorted[^1].Color;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var a = sorted[i];
            var b = sorted[i + 1];
            if (t >= a.Position && t <= b.Position)
            {
                float span = Math.Max(0.0001f, b.Position - a.Position);
                float f = (t - a.Position) / span;

                byte red = (byte)(a.Color.R + (b.Color.R - a.Color.R) * f);
                byte green = (byte)(a.Color.G + (b.Color.G - a.Color.G) * f);
                byte blue = (byte)(a.Color.B + (b.Color.B - a.Color.B) * f);
                byte alpha = (byte)(a.Color.A + (b.Color.A - a.Color.A) * f);

                return Color.FromArgb(alpha, red, green, blue);
            }
        }

        return sorted[^1].Color;
    }
}
