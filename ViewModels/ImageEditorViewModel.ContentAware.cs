using System;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Content-Aware Scaling Modal State ───────────────────────────────────
    [ObservableProperty] private bool _isContentAwareScaleDialogOpen;
    [ObservableProperty] private double _contentAwareScaleWidth = 800;
    [ObservableProperty] private double _contentAwareScaleHeight = 600;
    [ObservableProperty] private double _contentAwareScalePercent = 80;

    [RelayCommand]
    public void OpenContentAwareScaleDialog()
    {
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target == null)
        {
            StatusMessage = "Select a raster layer to use Content-Aware Scale.";
            return;
        }

        ContentAwareScaleWidth = Math.Round(target.Width * 0.8);
        ContentAwareScaleHeight = target.Height;
        ContentAwareScalePercent = 80;
        IsContentAwareScaleDialogOpen = true;
    }

    [RelayCommand]
    public void CancelContentAwareScaleDialog()
    {
        IsContentAwareScaleDialogOpen = false;
    }

    [RelayCommand]
    public void ConfirmContentAwareScaleDialog()
    {
        IsContentAwareScaleDialogOpen = false;
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target == null || target.OriginalImageBytes == null) return;

        int targetW = (int)Math.Max(10, ContentAwareScaleWidth);
        int targetH = (int)Math.Max(10, ContentAwareScaleHeight);

        ApplyContentAwareScale(target, targetW, targetH);
    }

    /// <summary>
    /// Executes Content-Aware Seam Carving scaling on a target ImageElement with full undo/redo.
    /// </summary>
    public void ApplyContentAwareScale(ImageElement target, int targetW, int targetH)
    {
        if (target.OriginalImageBytes == null) return;

        try
        {
            using var srcBmp = SKBitmap.Decode(target.OriginalImageBytes);
            if (srcBmp == null) return;

            byte[] oldBytes = (byte[])target.OriginalImageBytes.Clone();
            double oldW = target.Width;
            double oldH = target.Height;

            // Run Seam Carving algorithm
            using var carvedBmp = SeamCarvingEngine.ResizeContentAware(srcBmp, targetW, targetH);
            using var img = SKImage.FromBitmap(carvedBmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            byte[] newBytes = data.ToArray();

            target.AdjustedSource?.Dispose();
            target.AdjustedSource = new Bitmap(new MemoryStream(newBytes));
            target.OriginalImageBytes = newBytes;
            target.Width = targetW;
            target.Height = targetH;

            History.Push(new RetouchImageCommand(target, oldBytes, newBytes, "Content-Aware Scale", (el, b) =>
            {
                el.AdjustedSource?.Dispose();
                el.AdjustedSource = new Bitmap(new MemoryStream(b));
                el.OriginalImageBytes = b;
                el.Width = oldW;
                el.Height = oldH;
                RefreshCanvas();
            }));

            RefreshCanvas();
            StatusMessage = $"Content-Aware Scale applied ({targetW}×{targetH} px).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Content-Aware Scale error: {ex.Message}";
        }
    }

    /// <summary>
    /// Applies Content-Aware Fill to the active selection or masked area on the current image layer.
    /// </summary>
    [RelayCommand]
    public void ApplyContentAwareFill()
    {
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target == null || target.OriginalImageBytes == null)
        {
            StatusMessage = "Select an image layer to apply Content-Aware Fill.";
            return;
        }

        try
        {
            using var srcBmp = SKBitmap.Decode(target.OriginalImageBytes);
            if (srcBmp == null) return;

            int w = srcBmp.Width;
            int h = srcBmp.Height;

            // Generate mask: either from selection or default central patch
            byte[] mask = new byte[w * h];
            bool hasCustomMask = false;

            if (CanvasControl != null && CanvasControl.QuickMaskBuffer != null && CanvasControl.IsQuickMaskMode)
            {
                // Take Quick Mask
                Array.Copy(CanvasControl.QuickMaskBuffer, mask, Math.Min(mask.Length, CanvasControl.QuickMaskBuffer.Length));
                hasCustomMask = true;
            }

            if (!hasCustomMask)
            {
                // Default fill region: center rectangle (e.g. for demonstration or marquee selection)
                int rw = w / 4;
                int rh = h / 4;
                int rx = (w - rw) / 2;
                int ry = (h - rh) / 2;

                for (int y = ry; y < ry + rh; y++)
                {
                    int row = y * w;
                    for (int x = rx; x < rx + rw; x++)
                    {
                        mask[row + x] = 255;
                    }
                }
            }

            byte[] oldBytes = (byte[])target.OriginalImageBytes.Clone();

            // Run patch-based inpainting
            using var filledBmp = ContentAwareFillEngine.ApplyContentAwareFill(srcBmp, mask, colorAdaptation: true);
            using var img = SKImage.FromBitmap(filledBmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            byte[] newBytes = data.ToArray();

            target.AdjustedSource?.Dispose();
            target.AdjustedSource = new Bitmap(new MemoryStream(newBytes));
            target.OriginalImageBytes = newBytes;

            History.Push(new RetouchImageCommand(target, oldBytes, newBytes, "Content-Aware Fill", (el, b) =>
            {
                el.AdjustedSource?.Dispose();
                el.AdjustedSource = new Bitmap(new MemoryStream(b));
                el.OriginalImageBytes = b;
                RefreshCanvas();
            }));

            RefreshCanvas();
            StatusMessage = "Content-Aware Fill completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Content-Aware Fill error: {ex.Message}";
        }
    }
}
