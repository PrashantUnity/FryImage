using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;
using PdfEditorApp.Plugins.ImageEditor.Models;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class EditorCanvasControl
{
    // ── Magic Wand Tool State ────────────────────────────────────────────────
    public double WandTolerance { get; set; } = 32;
    public bool WandAntiAlias { get; set; } = true;
    public bool WandContiguous { get; set; } = true;
    public bool WandSampleAllLayers { get; set; } = false;

    public event Action<byte[], int, int>? MagicWandSelected;

    /// <summary>
    /// Handles pointer pressed for the Magic Wand tool.
    /// </summary>
    public void HandleMagicWandPointerPressed(Point canvasPt, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) return;

        e.Handled = true;
        ExecuteMagicWandSelection(canvasPt);
    }

    /// <summary>
    /// Executes color tolerance flood-fill / global selection at the specified canvas point.
    /// </summary>
    public void ExecuteMagicWandSelection(Point canvasPt)
    {
        int docW = (int)Math.Max(1, _logicalWidth);
        int docH = (int)Math.Max(1, _logicalHeight);

        // Find candidate ImageElement under cursor
        var targetImage = _elements.OfType<ImageElement>()
            .LastOrDefault(img => img.IsVisible && img.GetRotatedBounds().Contains(canvasPt));

        SKBitmap? sourceBitmap = null;
        bool isDocComposite = false;

        if (targetImage?.OriginalImageBytes != null && !WandSampleAllLayers)
        {
            try
            {
                sourceBitmap = SKBitmap.Decode(targetImage.OriginalImageBytes);
            }
            catch { }
        }

        if (sourceBitmap == null)
        {
            // Synthesize composite document bitmap
            isDocComposite = true;
            sourceBitmap = new SKBitmap(docW, docH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var skCanvas = new SKCanvas(sourceBitmap);
            skCanvas.Clear(SKColors.White);

            foreach (var el in _elements.Where(el => el.IsVisible))
            {
                if (el is ImageElement img && img.OriginalImageBytes != null)
                {
                    using var imgBmp = SKBitmap.Decode(img.OriginalImageBytes);
                    if (imgBmp != null)
                    {
                        var destRect = new SKRect((float)img.X, (float)img.Y, (float)(img.X + img.Width), (float)(img.Y + img.Height));
                        skCanvas.DrawBitmap(imgBmp, destRect);
                    }
                }
            }
        }

        using (sourceBitmap)
        {
            int seedX = (int)Math.Clamp(Math.Round(canvasPt.X), 0, sourceBitmap.Width - 1);
            int seedY = (int)Math.Clamp(Math.Round(canvasPt.Y), 0, sourceBitmap.Height - 1);

            if (!isDocComposite && targetImage != null)
            {
                // Map to image's local coordinate space
                seedX = (int)Math.Clamp(Math.Round(canvasPt.X - targetImage.X), 0, sourceBitmap.Width - 1);
                seedY = (int)Math.Clamp(Math.Round(canvasPt.Y - targetImage.Y), 0, sourceBitmap.Height - 1);
            }

            var mask = MagicWandEngine.ComputeSelectionMask(
                sourceBitmap,
                seedX,
                seedY,
                WandTolerance,
                WandContiguous,
                WandAntiAlias);

            // If target image was selected, select it in selection list
            if (targetImage != null && !_selection.Contains(targetImage))
            {
                _selection.Clear();
                _selection.Add(targetImage);
                NotifySelectionChanged();
            }

            // Populate Quick Mask buffer
            if (QuickMaskBuffer == null || QuickMaskBuffer.Length < docW * docH)
            {
                QuickMaskBuffer = new byte[docW * docH];
            }

            if (isDocComposite || (sourceBitmap.Width == docW && sourceBitmap.Height == docH))
            {
                Array.Copy(mask, QuickMaskBuffer, Math.Min(mask.Length, QuickMaskBuffer.Length));
            }
            else if (targetImage != null)
            {
                // Transfer local image mask into document space
                int imgW = sourceBitmap.Width;
                int imgH = sourceBitmap.Height;
                int originX = (int)Math.Round(targetImage.X);
                int originY = (int)Math.Round(targetImage.Y);

                for (int ly = 0; ly < imgH; ly++)
                {
                    int dy = originY + ly;
                    if (dy < 0 || dy >= docH) continue;

                    int srcRow = ly * imgW;
                    int dstRow = dy * docW;

                    for (int lx = 0; lx < imgW; lx++)
                    {
                        int dx = originX + lx;
                        if (dx < 0 || dx >= docW) continue;
                        QuickMaskBuffer[dstRow + dx] = mask[srcRow + lx];
                    }
                }
            }

            IsQuickMaskMode = true;
            MagicWandSelected?.Invoke(mask, sourceBitmap.Width, sourceBitmap.Height);
            InvalidateVisual();
        }
    }
}
