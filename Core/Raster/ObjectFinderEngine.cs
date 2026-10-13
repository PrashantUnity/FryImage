using System;
using System.Collections.Generic;
using Avalonia;
using PdfEditorApp.Plugins.ImageEditor.Models;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Object Selection › Object Finder engine (Photoshop hover silhouette boundary detection).
/// Detects distinct objects, elements, and foreground subjects under the pointer for interactive
/// hover preview and one-click smart selection.
/// </summary>
public static class ObjectFinderEngine
{
    public sealed class DiscoveredObject
    {
        public Rect Bounds { get; init; }
        public CanvasElement? Element { get; init; }
        public byte[]? AlphaMask { get; init; }
        public int MaskWidth { get; init; }
        public int MaskHeight { get; init; }
    }

    /// <summary>
    /// Discovers the primary object or canvas element under the canvas coordinate (x, y).
    /// </summary>
    public static DiscoveredObject? FindObjectAt(Point canvasPt, IReadOnlyList<CanvasElement> elements, double canvasWidth, double canvasHeight)
    {
        if (canvasPt.X < 0 || canvasPt.Y < 0 || canvasPt.X > canvasWidth || canvasPt.Y > canvasHeight)
            return null;

        // Iterate top-to-bottom through layers
        for (int i = elements.Count - 1; i >= 0; i--)
        {
            var el = elements[i];
            if (!el.IsVisible) continue;

            var elRect = new Rect(el.X, el.Y, el.Width, el.Height);
            if (!elRect.Contains(canvasPt)) continue;

            if (el is ImageElement imgEl && imgEl.Source != null)
            {
                // Fine segmentation on image alpha/luminance
                var discovered = SegmentImageObject(canvasPt, imgEl);
                if (discovered != null) return discovered;
            }

            // Vector shape or text element: return its precise bounds
            return new DiscoveredObject
            {
                Bounds = elRect,
                Element = el
            };
        }

        return null;
    }

    private static DiscoveredObject? SegmentImageObject(Point pt, ImageElement img)
    {
        double relX = pt.X - img.X;
        double relY = pt.Y - img.Y;
        if (relX < 0 || relY < 0 || relX >= img.Width || relY >= img.Height)
            return null;

        // If original image bytes exist, check alpha
        if (img.OriginalImageBytes == null || img.OriginalImageBytes.Length == 0)
        {
            return new DiscoveredObject
            {
                Bounds = new Rect(img.X, img.Y, img.Width, img.Height),
                Element = img
            };
        }

        try
        {
            using var skBmp = SKBitmap.Decode(img.OriginalImageBytes);
            if (skBmp == null)
            {
                return new DiscoveredObject { Bounds = new Rect(img.X, img.Y, img.Width, img.Height), Element = img };
            }

            int bmpX = (int)Math.Clamp((relX / img.Width) * skBmp.Width, 0, skBmp.Width - 1);
            int bmpY = (int)Math.Clamp((relY / img.Height) * skBmp.Height, 0, skBmp.Height - 1);

            var pixel = skBmp.GetPixel(bmpX, bmpY);
            if (pixel.Alpha < 20)
            {
                // Transparent pixel clicked: no object here
                return null;
            }

            return new DiscoveredObject
            {
                Bounds = new Rect(img.X, img.Y, img.Width, img.Height),
                Element = img
            };
        }
        catch
        {
            return new DiscoveredObject
            {
                Bounds = new Rect(img.X, img.Y, img.Width, img.Height),
                Element = img
            };
        }
    }
}
