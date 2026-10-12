using System;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class EditorCanvasControl
{
    public bool ShowPixelGrid { get; set; } = true;

    private static readonly IBrush s_pixelGridBrush = new SolidColorBrush(Color.FromArgb(45, 128, 128, 128));

    /// <summary>
    /// Renders discrete pixel boundary grid lines when zoom is 500% (5.0x) or greater.
    /// Uses visible viewport bounds clipping for high-performance rendering.
    /// </summary>
    private void DrawPixelGrid(DrawingContext dc)
    {
        if (!ShowPixelGrid || _zoom < 4.5 || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        // Determine visible canvas rectangle in canvas space
        var topLeft = ScreenToCanvas(new Point(0, 0));
        var bottomRight = ScreenToCanvas(new Point(Bounds.Width, Bounds.Height));

        int minX = Math.Max(0, (int)Math.Floor(topLeft.X));
        int maxX = Math.Min((int)Math.Ceiling(_logicalWidth), (int)Math.Ceiling(bottomRight.X));
        int minY = Math.Max(0, (int)Math.Floor(topLeft.Y));
        int maxY = Math.Min((int)Math.Ceiling(_logicalHeight), (int)Math.Ceiling(bottomRight.Y));

        if (minX >= maxX || minY >= maxY) return;

        // Scaled pen so it reads as exactly 1 screen pixel
        var pen = new Pen(s_pixelGridBrush, 1.0 / _zoom);

        // Vertical pixel separators
        for (int x = minX; x <= maxX; x++)
        {
            dc.DrawLine(pen, new Point(x, minY), new Point(x, maxY));
        }

        // Horizontal pixel separators
        for (int y = minY; y <= maxY; y++)
        {
            dc.DrawLine(pen, new Point(minX, y), new Point(maxX, y));
        }
    }
}
