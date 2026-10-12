using System;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Audits Content-Aware Scale (Seam Carving) and Content-Aware Fill (PatchMatch Exemplar Inpainting)
/// features and dialog presentation.
/// </summary>
public static class ContentAwareSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;
        var canvas = vm.CanvasControl;
        if (canvas == null) return;

        // 1. Create a test raster ImageElement with distinct visual content to test Seam Carving
        byte[] testImageBytes = CreateSynthesizedTestImage(300, 200);
        var testImgElement = new ImageElement
        {
            Name = "Seam Carve Test Layer",
            X = 50,
            Y = 50,
            Width = 300,
            Height = 200,
            OriginalImageBytes = testImageBytes,
            AdjustedSource = new Bitmap(new MemoryStream(testImageBytes)),
            Opacity = 1.0,
            IsVisible = true
        };

        vm.Elements.Add(testImgElement);
        vm.SelectedElement = testImgElement;
        canvas.InvalidateVisual();
        ctx.FlushUi();

        // 2. Open Content-Aware Scale Dialog and capture modal UI
        vm.OpenContentAwareScaleDialog();
        ctx.FlushUi();
        ctx.Capture("27_content_aware_scale_dialog.png");
        vm.CancelContentAwareScaleDialog();
        ctx.FlushUi();

        // 3. Apply Seam Carving: Shrink width from 300 to 220 px
        vm.ApplyContentAwareScale(testImgElement, 220, 200);
        canvas.InvalidateVisual();
        ctx.FlushUi();
        ctx.Capture("28_content_aware_seam_carving_applied.png");

        // 4. Test Content-Aware Fill
        // Fill a region in the center
        vm.ApplyContentAwareFill();
        canvas.InvalidateVisual();
        ctx.FlushUi();
        ctx.Capture("29_content_aware_fill_applied.png");

        // Clean up test element
        vm.Elements.Remove(testImgElement);
        vm.SelectedElement = null;
        canvas.InvalidateVisual();
        ctx.FlushUi();
    }

    private static byte[] CreateSynthesizedTestImage(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var skCanvas = new SKCanvas(bitmap);

        // Gradient background
        using var paint = new SKPaint();
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(width, height),
            new[] { new SKColor(40, 80, 160), new SKColor(20, 30, 60), new SKColor(180, 120, 70) },
            null,
            SKShaderTileMode.Clamp);
        paint.Shader = shader;
        skCanvas.DrawRect(0, 0, width, height, paint);

        // Subject in foreground (high gradient energy, will be preserved by seam carving)
        paint.Shader = null;
        paint.Color = new SKColor(240, 200, 60);
        paint.IsAntialias = true;
        skCanvas.DrawCircle(width / 2f, height / 2f, 40, paint);

        paint.Color = new SKColor(220, 60, 60);
        skCanvas.DrawRect(width / 4f, height / 3f, 30, 60, paint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
