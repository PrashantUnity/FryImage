using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

/// <summary>
/// High-performance interactive preview canvas for Studio Liquify.
/// Supports real-time displacement brush deformation, sub-pixel bilinear inverse mapping,
/// vector mesh overlay visualization, and precision cursor reticle tracking.
/// </summary>
public sealed class LiquifyPreviewCanvasControl : Control
{
    private LiquifyMesh? _mesh;
    private byte[]? _sourceBgra;
    private byte[]? _deformedBgra;
    private WriteableBitmap? _displayBitmap;
    private int _imageWidth;
    private int _imageHeight;

    private bool _isDeforming;
    private Point _lastDabPoint;
    private Point _cursorScreenPoint;
    private bool _isCursorInside;

    public static readonly StyledProperty<LiquifyTool> ActiveToolProperty =
        AvaloniaProperty.Register<LiquifyPreviewCanvasControl, LiquifyTool>(nameof(ActiveTool), LiquifyTool.ForwardWarp);

    public static readonly StyledProperty<double> BrushSizeProperty =
        AvaloniaProperty.Register<LiquifyPreviewCanvasControl, double>(nameof(BrushSize), 64.0);

    public static readonly StyledProperty<double> BrushDensityProperty =
        AvaloniaProperty.Register<LiquifyPreviewCanvasControl, double>(nameof(BrushDensity), 50.0);

    public static readonly StyledProperty<double> BrushPressureProperty =
        AvaloniaProperty.Register<LiquifyPreviewCanvasControl, double>(nameof(BrushPressure), 80.0);

    public static readonly StyledProperty<double> BrushRateProperty =
        AvaloniaProperty.Register<LiquifyPreviewCanvasControl, double>(nameof(BrushRate), 80.0);

    public static readonly StyledProperty<bool> ShowMeshProperty =
        AvaloniaProperty.Register<LiquifyPreviewCanvasControl, bool>(nameof(ShowMesh), false);

    public static readonly StyledProperty<bool> ShowBackdropProperty =
        AvaloniaProperty.Register<LiquifyPreviewCanvasControl, bool>(nameof(ShowBackdrop), false);

    public LiquifyTool ActiveTool
    {
        get => GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    public double BrushSize
    {
        get => GetValue(BrushSizeProperty);
        set => SetValue(BrushSizeProperty, value);
    }

    public double BrushDensity
    {
        get => GetValue(BrushDensityProperty);
        set => SetValue(BrushDensityProperty, value);
    }

    public double BrushPressure
    {
        get => GetValue(BrushPressureProperty);
        set => SetValue(BrushPressureProperty, value);
    }

    public double BrushRate
    {
        get => GetValue(BrushRateProperty);
        set => SetValue(BrushRateProperty, value);
    }

    public bool ShowMesh
    {
        get => GetValue(ShowMeshProperty);
        set => SetValue(ShowMeshProperty, value);
    }

    public bool ShowBackdrop
    {
        get => GetValue(ShowBackdropProperty);
        set => SetValue(ShowBackdropProperty, value);
    }

    public LiquifyMesh? Mesh => _mesh;

    static LiquifyPreviewCanvasControl()
    {
        AffectsRender<LiquifyPreviewCanvasControl>(ActiveToolProperty, BrushSizeProperty, ShowMeshProperty, ShowBackdropProperty);
    }

    public LiquifyPreviewCanvasControl()
    {
        ClipToBounds = true;
    }

    public void Initialize(byte[] sourceBgra, int width, int height)
    {
        _imageWidth = Math.Max(1, width);
        _imageHeight = Math.Max(1, height);
        _sourceBgra = sourceBgra;
        _deformedBgra = new byte[sourceBgra.Length];
        Array.Copy(sourceBgra, _deformedBgra, sourceBgra.Length);

        _mesh = new LiquifyMesh(_imageWidth, _imageHeight);
        _displayBitmap = new WriteableBitmap(
            new PixelSize(_imageWidth, _imageHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        UpdateBitmapFromBuffer(_deformedBgra);
        InvalidateVisual();
    }

    public void ReconstructAll(double percentage)
    {
        if (_mesh == null || _sourceBgra == null || _deformedBgra == null) return;
        _mesh.ReconstructAll(percentage);
        _mesh.RenderDeformedImage(_sourceBgra, _deformedBgra);
        UpdateBitmapFromBuffer(_deformedBgra);
        InvalidateVisual();
    }

    public void ResetAll()
    {
        if (_mesh == null || _sourceBgra == null || _deformedBgra == null) return;
        _mesh.Reset();
        Array.Copy(_sourceBgra, _deformedBgra, _sourceBgra.Length);
        UpdateBitmapFromBuffer(_deformedBgra);
        InvalidateVisual();
    }

    public byte[]? GetResultBgra()
    {
        if (_deformedBgra == null) return null;
        byte[] copy = new byte[_deformedBgra.Length];
        Array.Copy(_deformedBgra, copy, _deformedBgra.Length);
        return copy;
    }

    private void UpdateBitmapFromBuffer(byte[] bgra)
    {
        if (_displayBitmap == null) return;
        using var fb = _displayBitmap.Lock();
        Marshal.Copy(bgra, 0, fb.Address, bgra.Length);
    }

    private Rect GetImageDestRect(Rect viewportBounds)
    {
        if (_imageWidth <= 0 || _imageHeight <= 0) return viewportBounds;

        double scaleX = (viewportBounds.Width - 32) / _imageWidth;
        double scaleY = (viewportBounds.Height - 32) / _imageHeight;
        double scale = Math.Max(0.01, Math.Min(scaleX, scaleY));

        double drawW = _imageWidth * scale;
        double drawH = _imageHeight * scale;
        double left = viewportBounds.Left + (viewportBounds.Width - drawW) / 2.0;
        double top = viewportBounds.Top + (viewportBounds.Height - drawH) / 2.0;

        return new Rect(left, top, drawW, drawH);
    }

    private Point ScreenToImagePoint(Point screenPt, Rect destRect)
    {
        double xRatio = (screenPt.X - destRect.Left) / Math.Max(1.0, destRect.Width);
        double yRatio = (screenPt.Y - destRect.Top) / Math.Max(1.0, destRect.Height);
        return new Point(xRatio * _imageWidth, yRatio * _imageHeight);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        if (bounds.Width <= 4 || bounds.Height <= 4) return;

        // Dark Studio background
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x18, 0x1B, 0x22)), null, bounds);

        if (_displayBitmap == null || _sourceBgra == null) return;

        var destRect = GetImageDestRect(bounds);

        // Subtle drop shadow and border around image frame
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)), 1.0), destRect);

        // Draw image (either backdrop original or live deformed bitmap)
        if (ShowBackdrop)
        {
            using var origBmp = new SKBitmap(_imageWidth, _imageHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            var pin = GCHandle.Alloc(_sourceBgra, GCHandleType.Pinned);
            try
            {
                origBmp.InstallPixels(origBmp.Info, pin.AddrOfPinnedObject());
                using var img = SkiaSharp.SKImage.FromBitmap(origBmp);
                using var enc = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                using var ms = new System.IO.MemoryStream(enc.ToArray());
                var bmp = new Bitmap(ms);
                dc.DrawImage(bmp, destRect);
            }
            finally
            {
                pin.Free();
            }
        }
        else
        {
            dc.DrawImage(_displayBitmap, destRect);
        }

        // Render Liquify Displacement Mesh Overlay if enabled
        if (ShowMesh && _mesh != null)
        {
            DrawDisplacementMesh(dc, destRect);
        }

        // Render Brush Reticle Circle at cursor position
        if (_isCursorInside)
        {
            double scale = destRect.Width / Math.Max(1.0, _imageWidth);
            double screenRadius = (BrushSize / 2.0) * scale;
            if (screenRadius > 1.0)
            {
                var outerPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), 2.0);
                var innerPen = new Pen(new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)), 1.0);
                dc.DrawEllipse(null, outerPen, _cursorScreenPoint, screenRadius, screenRadius);
                dc.DrawEllipse(null, innerPen, _cursorScreenPoint, screenRadius, screenRadius);
            }
        }
    }

    private void DrawDisplacementMesh(DrawingContext dc, Rect destRect)
    {
        if (_mesh == null) return;

        double scaleX = destRect.Width / _imageWidth;
        double scaleY = destRect.Height / _imageHeight;

        var meshPen = new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0x38, 0xBD, 0xF8)), 0.75);

        Point NodeToScreen(int col, int row)
        {
            int idx = row * _mesh.Cols + col;
            double imgX = col * LiquifyMesh.CellSize + _mesh.Dx[idx];
            double imgY = row * LiquifyMesh.CellSize + _mesh.Dy[idx];
            return new Point(destRect.Left + imgX * scaleX, destRect.Top + imgY * scaleY);
        }

        // Draw horizontal mesh lines
        for (int r = 0; r < _mesh.Rows; r++)
        {
            for (int c = 0; c < _mesh.Cols - 1; c++)
            {
                Point p1 = NodeToScreen(c, r);
                Point p2 = NodeToScreen(c + 1, r);
                if (destRect.Contains(p1) || destRect.Contains(p2))
                    dc.DrawLine(meshPen, p1, p2);
            }
        }

        // Draw vertical mesh lines
        for (int c = 0; c < _mesh.Cols; c++)
        {
            for (int r = 0; r < _mesh.Rows - 1; r++)
            {
                Point p1 = NodeToScreen(c, r);
                Point p2 = NodeToScreen(c, r + 1);
                if (destRect.Contains(p1) || destRect.Contains(p2))
                    dc.DrawLine(meshPen, p1, p2);
            }
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isCursorInside = true;
        _cursorScreenPoint = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isCursorInside = false;
        InvalidateVisual();
    }

    private bool _isResizingBrush;
    private double _initialBrushSize;
    private Point _brushResizeStart;

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) || e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            BrushSize = Math.Clamp(BrushSize + (e.Delta.Y > 0 ? 5.0 : -5.0), 4.0, 500.0);
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var screenPt = e.GetPosition(this);
        _cursorScreenPoint = screenPt;
        _isCursorInside = true;

        // Studio Parity: Alt+Right-drag resizes the brush instead of painting (#4707)
        bool isAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        bool isRightButton = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;
        if (isAlt && isRightButton)
        {
            _isResizingBrush = true;
            _brushResizeStart = screenPt;
            _initialBrushSize = BrushSize;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (_mesh == null || _sourceBgra == null || _deformedBgra == null) return;

        var bounds = Bounds;
        var destRect = GetImageDestRect(bounds);

        if (!destRect.Contains(screenPt)) return;

        var imgPt = ScreenToImagePoint(screenPt, destRect);
        _lastDabPoint = imgPt;
        _isDeforming = true;
        e.Pointer.Capture(this);

        _mesh.ApplyDab(ActiveTool, _lastDabPoint, _lastDabPoint, BrushSize, BrushDensity, BrushPressure, BrushRate);
        _mesh.RenderDeformedImage(_sourceBgra, _deformedBgra);
        UpdateBitmapFromBuffer(_deformedBgra);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _cursorScreenPoint = e.GetPosition(this);
        _isCursorInside = true;

        if (_isResizingBrush)
        {
            double deltaX = _cursorScreenPoint.X - _brushResizeStart.X;
            BrushSize = Math.Clamp(Math.Round(_initialBrushSize + deltaX * 1.5), 4.0, 500.0);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!_isDeforming || _mesh == null || _sourceBgra == null || _deformedBgra == null)
        {
            InvalidateVisual();
            return;
        }

        var bounds = Bounds;
        var destRect = GetImageDestRect(bounds);
        var imgPt = ScreenToImagePoint(_cursorScreenPoint, destRect);

        _mesh.ApplyDab(ActiveTool, _lastDabPoint, imgPt, BrushSize, BrushDensity, BrushPressure, BrushRate);
        _lastDabPoint = imgPt;

        _mesh.RenderDeformedImage(_sourceBgra, _deformedBgra);
        UpdateBitmapFromBuffer(_deformedBgra);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isResizingBrush)
        {
            _isResizingBrush = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isDeforming)
        {
            _isDeforming = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }
}
