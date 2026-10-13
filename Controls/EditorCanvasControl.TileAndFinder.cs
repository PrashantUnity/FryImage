using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class EditorCanvasControl
{
    public static readonly StyledProperty<bool> IsPatternPreviewEnabledProperty =
        AvaloniaProperty.Register<EditorCanvasControl, bool>(nameof(IsPatternPreviewEnabled));

    public bool IsPatternPreviewEnabled
    {
        get => GetValue(IsPatternPreviewEnabledProperty);
        set => SetValue(IsPatternPreviewEnabledProperty, value);
    }

    public static readonly StyledProperty<bool> IsTileGridOverlayEnabledProperty =
        AvaloniaProperty.Register<EditorCanvasControl, bool>(nameof(IsTileGridOverlayEnabled));

    public bool IsTileGridOverlayEnabled
    {
        get => GetValue(IsTileGridOverlayEnabledProperty);
        set => SetValue(IsTileGridOverlayEnabledProperty, value);
    }

    public ObjectFinderEngine.DiscoveredObject? HoveredDiscoveredObject { get; set; }

    /// <summary>
    /// Renders 8 surrounding repeated copies of the canvas for seamless 3x3 pattern design preview.
    /// Matches Photoshop Pattern Preview specification.
    /// </summary>
    private void DrawPatternPreview(DrawingContext dc)
    {
        if (!IsPatternPreviewEnabled || _logicalWidth <= 0 || _logicalHeight <= 0) return;

        double w = _logicalWidth;
        double h = _logicalHeight;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue; // Skip main central canvas

                double offsetX = dx * w;
                double offsetY = dy * h;
                var tileRect = new Rect(offsetX, offsetY, w, h);

                using (dc.PushTransform(Matrix.CreateTranslation(offsetX, offsetY)))
                using (dc.PushOpacity(0.85))
                {
                    // Draw tile background
                    if (_backgroundColor.A == 0)
                    {
                        dc.DrawImage(GetCheckerboardTile(w, h), new Rect(0, 0, w, h));
                    }
                    else
                    {
                        dc.DrawRectangle(new SolidColorBrush(_backgroundColor), null, new Rect(0, 0, w, h));
                    }

                    // Draw all elements in the repeating tile
                    foreach (var el in _elements)
                    {
                        DrawElement(dc, el);
                    }
                }

                // Subtle dashed border outlining repeating tile bounds
                var dashPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 80, 160, 255)), 1.0 / _zoom,
                    new DashStyle(new double[] { 4, 4 }, 0));
                dc.DrawRectangle(null, dashPen, tileRect);
            }
        }
    }

    /// <summary>
    /// Renders developer sparse storage tile boundaries with coordinate badges [tx, ty].
    /// Matches Photoshop sparse storage tile overlay specification.
    /// </summary>
    private void DrawTileGridOverlay(DrawingContext dc)
    {
        if (!IsTileGridOverlayEnabled || _logicalWidth <= 0 || _logicalHeight <= 0) return;

        const double tileSize = 128.0;
        int tilesX = (int)Math.Ceiling(_logicalWidth / tileSize);
        int tilesY = (int)Math.Ceiling(_logicalHeight / tileSize);

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 45, 140, 235)), 1.0 / _zoom);
        var tileFill = new SolidColorBrush(Color.FromArgb(35, 45, 140, 235));
        var badgeBg = new SolidColorBrush(Color.FromArgb(180, 20, 20, 25));
        var textBrush = new SolidColorBrush(Color.FromArgb(230, 220, 220, 230));

        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
        double fontSize = Math.Max(9.0, 11.0 / _zoom);

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                var tileRect = new Rect(tx * tileSize, ty * tileSize,
                    Math.Min(tileSize, _logicalWidth - tx * tileSize),
                    Math.Min(tileSize, _logicalHeight - ty * tileSize));

                dc.DrawRectangle(tileFill, gridPen, tileRect);

                if (_zoom >= 0.5)
                {
                    string label = $"{tx},{ty}";
                    var ft = new FormattedText(
                        label,
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        fontSize,
                        textBrush);

                    var badgeRect = new Rect(tileRect.X + 4 / _zoom, tileRect.Y + 4 / _zoom, ft.Width + 6 / _zoom, ft.Height + 2 / _zoom);
                    dc.DrawRectangle(badgeBg, null, badgeRect, 2 / _zoom, 2 / _zoom);
                    dc.DrawText(ft, new Point(badgeRect.X + 3 / _zoom, badgeRect.Y + 1 / _zoom));
                }
            }
        }
    }

    /// <summary>
    /// Renders cyan highlight overlay and marching-ants outline over the hovered object found by Object Finder.
    /// </summary>
    private void DrawObjectFinderOverlay(DrawingContext dc)
    {
        if (ActiveToolMode != "objectselection" || HoveredDiscoveredObject == null) return;

        var bounds = HoveredDiscoveredObject.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var highlightFill = new SolidColorBrush(Color.FromArgb(50, 0, 190, 255));
        var outlinePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 0, 220, 255)), 1.5 / _zoom,
            new DashStyle(new double[] { 4, 3 }, 0));

        dc.DrawRectangle(highlightFill, outlinePen, bounds, 3 / _zoom, 3 / _zoom);
    }
}
