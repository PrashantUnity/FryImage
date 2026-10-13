using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

/// <summary>
/// Photoshop standard in-dialog filter preview control.
/// Features:
/// - Real-time filtered preview rendering on checkerboard canvas
/// - Press-and-hold to instantly compare with the unfiltered original image
/// - Click-and-drag panning across high-resolution image space
/// - Click to center on any document point
/// - Mouse wheel zoom step support (12.5% to 800%)
/// </summary>
public class FilterPreviewControl : Control
{
    public static readonly StyledProperty<Bitmap?> PreviewImageProperty =
        AvaloniaProperty.Register<FilterPreviewControl, Bitmap?>(
            nameof(PreviewImage));

    public static readonly StyledProperty<Bitmap?> OriginalImageProperty =
        AvaloniaProperty.Register<FilterPreviewControl, Bitmap?>(
            nameof(OriginalImage));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<FilterPreviewControl, double>(
            nameof(Zoom), defaultValue: 1.0);

    public static readonly StyledProperty<Point> PanOffsetProperty =
        AvaloniaProperty.Register<FilterPreviewControl, Point>(
            nameof(PanOffset), defaultValue: new Point(0, 0));

    public Bitmap? PreviewImage
    {
        get => GetValue(PreviewImageProperty);
        set => SetValue(PreviewImageProperty, value);
    }

    public Bitmap? OriginalImage
    {
        get => GetValue(OriginalImageProperty);
        set => SetValue(OriginalImageProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public Point PanOffset
    {
        get => GetValue(PanOffsetProperty);
        set => SetValue(PanOffsetProperty, value);
    }

    private bool _isPressed;
    private Point _pointerDownPos;
    private Point _initialPan;
    private bool _hasDragged;

    private static readonly IBrush CheckerLightBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x30, 0x34));
    private static readonly IBrush CheckerDarkBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x24, 0x27));
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x44, 0x48, 0x50)), 1);
    private static readonly IBrush BadgeBgBrush = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));
    private static readonly IBrush BadgeTextBrush = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));

    static FilterPreviewControl()
    {
        AffectsRender<FilterPreviewControl>(PreviewImageProperty, OriginalImageProperty, ZoomProperty, PanOffsetProperty);
    }

    public FilterPreviewControl()
    {
        ClipToBounds = true;
        Height = 150;
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsLeftButtonPressed)
        {
            _isPressed = true;
            _hasDragged = false;
            _pointerDownPos = pt.Position;
            _initialPan = PanOffset;
            e.Pointer.Capture(this);
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isPressed)
        {
            var cur = e.GetPosition(this);
            var delta = cur - _pointerDownPos;
            if (Math.Abs(delta.X) > 2 || Math.Abs(delta.Y) > 2)
            {
                _hasDragged = true;
            }

            PanOffset = new Point(_initialPan.X + delta.X, _initialPan.Y + delta.Y);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPressed)
        {
            _isPressed = false;
            e.Pointer.Capture(null);

            // If user clicked without dragging, recenter towards clicked coordinate
            if (!_hasDragged)
            {
                var cur = e.GetPosition(this);
                double viewCenterX = Bounds.Width / 2.0;
                double viewCenterY = Bounds.Height / 2.0;
                double diffX = viewCenterX - cur.X;
                double diffY = viewCenterY - cur.Y;
                PanOffset = new Point(PanOffset.X + diffX, PanOffset.Y + diffY);
            }

            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        double oldZoom = Zoom;
        double newZoom = e.Delta.Y > 0 ? StepZoom(oldZoom, true) : StepZoom(oldZoom, false);
        if (Math.Abs(newZoom - oldZoom) > 0.001)
        {
            Zoom = newZoom;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    public static double StepZoom(double current, bool up)
    {
        double[] steps = [0.125, 0.25, 0.5, 1.0, 2.0, 4.0, 8.0];
        if (up)
        {
            foreach (var s in steps)
            {
                if (s > current + 0.01) return s;
            }
            return 8.0;
        }
        else
        {
            for (int i = steps.Length - 1; i >= 0; i--)
            {
                if (steps[i] < current - 0.01) return steps[i];
            }
            return 0.125;
        }
    }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Clip to rounded border
        var roundedRect = new RoundedRect(bounds, 3);
        using var clipState = dc.PushClip(roundedRect);

        // 1. Studio dark background
        dc.FillRectangle(CheckerDarkBrush, bounds);

        // 2. Determine which image to show:
        // When pointer is pressed down, show Original; otherwise show Preview (or fallback to Original)
        var activeBitmap = _isPressed
            ? (OriginalImage ?? PreviewImage)
            : (PreviewImage ?? OriginalImage);

        if (activeBitmap != null && activeBitmap.Size.Width > 0 && activeBitmap.Size.Height > 0)
        {
            double imgW = activeBitmap.Size.Width;
            double imgH = activeBitmap.Size.Height;
            double z = Math.Clamp(Zoom, 0.125, 8.0);

            // Maintain exact square pixel scaling without stretching (#4640, #4655)
            double displayW = Math.Round(imgW * z);
            double displayH = Math.Round(imgH * z);

            // Center in viewport + pan offset, snapped to whole pixels
            double destX = Math.Round((bounds.Width - displayW) / 2.0 + PanOffset.X);
            double destY = Math.Round((bounds.Height - displayH) / 2.0 + PanOffset.Y);

            var destRect = new Rect(destX, destY, displayW, displayH);

            // Draw checkerboard strictly under the image area (#4640)
            var visibleImageRect = destRect.Intersect(bounds);
            if (visibleImageRect.Width > 0 && visibleImageRect.Height > 0)
            {
                using (dc.PushClip(visibleImageRect))
                {
                    const double checkSize = 10.0;
                    for (double cy = destRect.Y; cy < destRect.Bottom; cy += checkSize)
                    {
                        int row = (int)((cy - destRect.Y) / checkSize);
                        for (double cx = destRect.X; cx < destRect.Right; cx += checkSize)
                        {
                            int col = (int)((cx - destRect.X) / checkSize);
                            if ((row + col) % 2 == 0)
                            {
                                dc.FillRectangle(CheckerLightBrush, new Rect(cx, cy, Math.Min(checkSize, destRect.Right - cx), Math.Min(checkSize, destRect.Bottom - cy)));
                            }
                        }
                    }
                }
            }

            dc.DrawImage(activeBitmap, new Rect(0, 0, imgW, imgH), destRect);
        }

        // 3. Mode badge overlay when pressed (Hold to Compare)
        if (_isPressed)
        {
            var badgeRect = new Rect(8, 8, 84, 20);
            dc.FillRectangle(BadgeBgBrush, badgeRect, 3);
            var tf = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
            var ft = new FormattedText("ORIGINAL", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, 9.5, BadgeTextBrush);
            dc.DrawText(ft, new Point(14, 11));
        }

        // 4. Subtle Outer Border
        dc.DrawRectangle(null, BorderPen, bounds, 3, 3);
    }
}
