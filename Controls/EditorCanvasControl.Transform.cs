using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class EditorCanvasControl
{
    // ── Transform HUD Badge Resources ────────────────────────────────────────
    private static readonly IBrush s_transformHudBg = new SolidColorBrush(Color.FromArgb(230, 24, 24, 24));
    private static readonly IPen s_transformHudBorder = new Pen(new SolidColorBrush(Color.FromArgb(180, 70, 70, 70)), 1);
    private static readonly IBrush s_transformHudAccent = new SolidColorBrush(Color.Parse("#38BDF8"));
    private static readonly IBrush s_transformHudText = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly Typeface s_transformHudTypeface = new("Segoe UI, -apple-system, sans-serif");

    /// <summary>
    /// Updates cursor on pointer hover over resize handles or rotation handles when Move/Select tool is active.
    /// </summary>
    public bool UpdateTransformHoverCursor(Point canvasPt, Point screenPt)
    {
        if (ActiveToolMode != "select" || !ShowTransformControls) return false;

        double handleSize = HandleSize / _zoom;
        double halfHandle = handleSize / 2;
        double pad = 5 / _zoom;

        // 1. Single-element selection
        if (_selection.Count == 1 && !_selection[0].IsLocked)
        {
            var single = _selection[0];
            var bounds = single.Bounds;

            // Rotation handle hit test
            var rotPt = GetRotationHandlePoint(bounds, single.RotationDegrees, _zoom);
            if (Point.Distance(canvasPt, rotPt) <= 9 / _zoom)
            {
                Cursor = new Cursor(StandardCursorType.DragMove);
                return true;
            }

            // Map canvasPt to local space if rotated
            Point localPt = canvasPt;
            if (single.RotationDegrees != 0)
            {
                localPt = canvasPt.Transform(Matrix.CreateRotation(-single.RotationDegrees * Math.PI / 180.0, bounds.Center));
            }

            var handles = GetHandlePoints(bounds);
            for (int i = 0; i < handles.Length; i++)
            {
                var hr = new Rect(handles[i].X - halfHandle - pad, handles[i].Y - halfHandle - pad,
                                  handleSize + pad * 2, handleSize + pad * 2);
                if (hr.Contains(localPt))
                {
                    Cursor = GetCursorForHandleIndex(i, single.RotationDegrees);
                    return true;
                }
            }

            // Outer rotation band just outside bounding box (5px to 18px outside controls)
            var innerBand = bounds.Inflate(5 / _zoom);
            var outerBand = bounds.Inflate(18 / _zoom);
            if (outerBand.Contains(localPt) && !innerBand.Contains(localPt))
            {
                Cursor = new Cursor(StandardCursorType.DragMove);
                return true;
            }
        }
        // 2. Multi-element group selection
        else if (_selection.Count > 1)
        {
            var groupBounds = ComputeGroupBounds(_selection);

            // Group rotation handle
            var rotPt = GetRotationHandlePoint(groupBounds, 0, _zoom);
            if (Point.Distance(canvasPt, rotPt) <= 9 / _zoom)
            {
                Cursor = new Cursor(StandardCursorType.DragMove);
                return true;
            }

            var handles = GetHandlePoints(groupBounds);
            for (int i = 0; i < handles.Length; i++)
            {
                var hr = new Rect(handles[i].X - halfHandle - pad, handles[i].Y - halfHandle - pad,
                                  handleSize + pad * 2, handleSize + pad * 2);
                if (hr.Contains(canvasPt))
                {
                    Cursor = GetCursorForHandleIndex(i, 0);
                    return true;
                }
            }

            var groupInnerBand = groupBounds.Inflate(5 / _zoom);
            var groupOuterBand = groupBounds.Inflate(18 / _zoom);
            if (groupOuterBand.Contains(canvasPt) && !groupInnerBand.Contains(canvasPt))
            {
                Cursor = new Cursor(StandardCursorType.DragMove);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Maps handle index (0..7: TL, T, TR, R, BR, B, BL, L) to appropriate corner/side cursor.
    /// Turns with the box's rotation and the view's rotation (Studio Parity #852d2a1d, #5188).
    /// </summary>
    private Cursor GetCursorForHandleIndex(int handleIndex, double rotationDegrees)
    {
        double totalAngle = rotationDegrees + _viewportRotationAngle;
        // Adjust index based on total rotation angle (45-degree increments)
        int angleSteps = (int)Math.Round(((totalAngle % 360 + 360) % 360) / 45.0) % 8;
        int effectiveIndex = (handleIndex + angleSteps) % 8;

        return effectiveIndex switch
        {
            0 => new Cursor(StandardCursorType.TopLeftCorner),
            1 => new Cursor(StandardCursorType.TopSide),
            2 => new Cursor(StandardCursorType.TopRightCorner),
            3 => new Cursor(StandardCursorType.RightSide),
            4 => new Cursor(StandardCursorType.BottomRightCorner),
            5 => new Cursor(StandardCursorType.BottomSide),
            6 => new Cursor(StandardCursorType.BottomLeftCorner),
            7 => new Cursor(StandardCursorType.LeftSide),
            _ => Cursor.Default
        };
    }

    /// <summary>
    /// Programmatically sets live transform interaction state for testing and HUD badge verification.
    /// </summary>
    public void SetTransformInteractionState(bool isResizing, bool isRotating, Point? cursorCanvasPos = null)
    {
        _isResizing = isResizing;
        _isRotating = isRotating;
        if (cursorCanvasPos.HasValue)
        {
            _cursorCanvasPos = cursorCanvasPos.Value;
        }
        else if (_selection.Count > 0)
        {
            var target = _selection[0];
            _cursorCanvasPos = new Point(target.X + target.Width, target.Y + target.Height);
        }
        InvalidateVisual();
    }

    /// <summary>
    /// Draws floating HUD badge showing live dimensions or rotation angle during transform operations.
    /// </summary>
    public void DrawTransformHudBadge(DrawingContext dc)
    {
        if (!_isResizing && !_isRotating) return;

        string text;
        if (_isResizing && _selection.Count > 0)
        {
            var target = _selection[0];
            text = $"W: {target.Width:0.#} px   H: {target.Height:0.#} px";
        }
        else if (_isRotating && _selection.Count > 0)
        {
            var target = _selection[0];
            text = $"∠ {target.RotationDegrees:0.#}°";
        }
        else return;

        var formattedText = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            s_transformHudTypeface,
            11.0,
            s_transformHudText);

        double badgeWidth = formattedText.Width + 24;
        double badgeHeight = 22;

        var screenPt = CanvasToScreen(_cursorCanvasPos ?? new Point(0, 0));
        double x = screenPt.X + 16;
        double y = screenPt.Y + 16;

        if (x + badgeWidth > Bounds.Width) x = screenPt.X - badgeWidth - 10;
        if (y + badgeHeight > Bounds.Height) y = screenPt.Y - badgeHeight - 10;

        var badgeRect = new Rect(x, y, badgeWidth, badgeHeight);
        var rounded = new RoundedRect(badgeRect, 4);

        dc.DrawRectangle(s_transformHudBg, s_transformHudBorder, rounded);

        // Accent indicator dot
        dc.DrawEllipse(s_transformHudAccent, null, new Point(x + 8, y + badgeHeight / 2), 2.5, 2.5);

        // Text
        dc.DrawText(formattedText, new Point(x + 15, y + (badgeHeight - formattedText.Height) / 2));
    }

    /// <summary>
    /// Resamples an ImageElement raster with Studio's Keys Bicubic and reduction prefiltering (Commit 9c3f9664 parity).
    /// Prevents moiré and aliasing during transforms and scales.
    /// </summary>
    public static void ResampleImageElementBicubic(ImageElement image, int newWidth, int newHeight)
    {
        if (image.OriginalImageBytes == null || image.OriginalImageBytes.Length == 0) return;
        using var srcBmp = SkiaSharp.SKBitmap.Decode(image.OriginalImageBytes);
        if (srcBmp == null) return;

        using var resizedBmp = PdfEditorApp.Plugins.ImageEditor.Core.Raster.BicubicResampler.ResizeWithPrefilter(srcBmp, newWidth, newHeight);
        using var img = SkiaSharp.SKImage.FromBitmap(resizedBmp);
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        byte[] bytes = data.ToArray();

        image.OriginalImageBytes = bytes;
        image.AdjustedSource?.Dispose();
        image.AdjustedSource = new Avalonia.Media.Imaging.Bitmap(new System.IO.MemoryStream(bytes));
        image.NaturalPixelWidth = newWidth;
        image.NaturalPixelHeight = newHeight;
    }
}
