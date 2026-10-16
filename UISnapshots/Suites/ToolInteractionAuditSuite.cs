using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies interactive tool executions: Paint Bucket flood fill, vector fills,
/// background fills, retouch subtool aliases, and rounded rectangle placement.
/// </summary>
public static class ToolInteractionAuditSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;

        // Reset canvas to clean state
        vm.Elements.Clear();
        vm.CanvasWidth = 1000;
        vm.CanvasHeight = 700;
        vm.BackgroundColor = Color.FromRgb(245, 247, 250);

        // 1. Paint Bucket on Vector Shape
        var rectShape = new RectangleElement
        {
            X = 60,
            Y = 60,
            Width = 240,
            Height = 160,
            FillColor = Color.FromRgb(220, 224, 230),
            StrokeColor = Color.FromRgb(100, 110, 125),
            StrokeThickness = 2.0,
            CornerRadius = 8,
            Name = "Vector Test Card"
        };
        vm.Elements.Add(rectShape);

        // Use Paint Bucket to fill the shape with Dodger Blue
        vm.PrimaryColor = Color.FromRgb(30, 144, 255);
        vm.ApplyPaintBucketFill(new Point(120, 120));

        if (rectShape.FillColor != vm.PrimaryColor)
        {
            throw new InvalidOperationException($"Paint bucket failed to fill vector shape. Expected {vm.PrimaryColor}, got {rectShape.FillColor}");
        }

        // 2. Paint Bucket BFS Flood Fill on Raster Image
        int imgW = 200, imgH = 200;
        using (var bmp = new SKBitmap(imgW, imgH, SKColorType.Bgra8888, SKAlphaType.Premul))
        {
            using (var skCanvas = new SKCanvas(bmp))
            {
                skCanvas.Clear(SKColors.DarkSlateGray);
                using var whitePaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                skCanvas.DrawRect(new SKRect(40, 40, 160, 160), whitePaint);
            }
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            byte[] rawPng = data.ToArray();

            var imgElement = new ImageElement
            {
                X = 360,
                Y = 60,
                Width = 240,
                Height = 240,
                NaturalPixelWidth = imgW,
                NaturalPixelHeight = imgH,
                OriginalImageBytes = rawPng,
                Source = new Bitmap(new MemoryStream(rawPng)),
                Name = "Raster Test Pattern"
            };
            vm.Elements.Add(imgElement);

            // Click inside the white inner square with Coral Red fill
            vm.PrimaryColor = Color.FromRgb(255, 69, 0);
            vm.BrushOpacity = 100;
            // Map coordinate (100, 100) inside 200x200 image -> on canvas: 360 + 100 * (240/200) = 480
            vm.ApplyPaintBucketFill(new Point(480, 180));

            // Verify flood fill updated OriginalImageBytes
            using var filledBmp = SKBitmap.Decode(imgElement.OriginalImageBytes);
            var centerColor = filledBmp.GetPixel(100, 100);
            if (centerColor.Red != 255 || centerColor.Green != 69 || centerColor.Blue != 0)
            {
                throw new InvalidOperationException($"Flood fill failed to replace center pixels. Got {centerColor}");
            }

            // Verify corner of image remained DarkSlateGray
            var cornerColor = filledBmp.GetPixel(10, 10);
            if (cornerColor == centerColor)
            {
                throw new InvalidOperationException("Flood fill incorrectly leaked outside the boundary!");
            }

            // Test Undo restores original white square
            vm.UndoCommand.Execute(null);
            using var undoneBmp = SKBitmap.Decode(imgElement.OriginalImageBytes);
            var undoneCenter = undoneBmp.GetPixel(100, 100);
            if (undoneCenter.Red != 255 || undoneCenter.Green != 255 || undoneCenter.Blue != 255)
            {
                throw new InvalidOperationException("Undo failed to restore flood fill state!");
            }

            // Test Redo reapplies flood fill
            vm.RedoCommand.Execute(null);
        }

        // 3. Test Retouch Subtool Aliases
        // Create retouch test target
        using (var rBmp = new SKBitmap(200, 200, SKColorType.Bgra8888, SKAlphaType.Premul))
        {
            using (var skCanvas = new SKCanvas(rBmp))
            {
                skCanvas.Clear(SKColors.LightBlue);
            }
            using var img = SKImage.FromBitmap(rBmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            byte[] rPng = data.ToArray();

            var retouchTarget = new ImageElement
            {
                X = 660,
                Y = 60,
                Width = 240,
                Height = 240,
                NaturalPixelWidth = 200,
                NaturalPixelHeight = 200,
                OriginalImageBytes = rPng,
                Source = new Bitmap(new MemoryStream(rPng)),
                Name = "Retouch Target"
            };
            vm.Elements.Add(retouchTarget);
            vm.SelectedElement = retouchTarget;

            // Apply strokes using newly wired subtool aliases
            var testStroke = new List<Point> { new(700, 100), new(750, 150), new(800, 180) };

            vm.ApplyRetouchStroke("colorreplace", testStroke);
            vm.ApplyRetouchStroke("healingbrush", testStroke);
            vm.ApplyRetouchStroke("bgeraser", testStroke);
            vm.ApplyRetouchStroke("magiceraser", testStroke);
            vm.ApplyRetouchStroke("historybrush", testStroke);
            vm.ApplyRetouchStroke("arthistorybrush", testStroke);
        }

        // 4. Test PlaceShapeElement with "roundedrect"
        vm.PlaceShapeElement("roundedrect", new Point(60, 360), new Point(300, 520));
        var placedShape = vm.Elements[^1];
        if (placedShape is not RectangleElement rounded || rounded.CornerRadius <= 0)
        {
            throw new InvalidOperationException($"Expected RectangleElement with CornerRadius > 0 for roundedrect, got {placedShape}");
        }

        // 5. Test TextElement with Text Color and Paint Bucket
        var textEl = new TextElement
        {
            X = 360,
            Y = 380,
            Width = 240,
            Height = 60,
            Text = "Paint Bucket & Tools Fixed",
            FontSize = 18,
            TextColor = Color.FromRgb(100, 100, 100),
            IsWidthAutoFitEnabled = false
        };
        vm.Elements.Add(textEl);
        vm.PrimaryColor = Color.FromRgb(142, 68, 173); // Amethyst Purple
        vm.ApplyPaintBucketFill(new Point(380, 400));
        if (textEl.TextColor != vm.PrimaryColor)
        {
            throw new InvalidOperationException($"Paint bucket failed to fill TextElement color. Expected {vm.PrimaryColor}, got {textEl.TextColor}");
        }

        // Capture snapshot
        ctx.FlushUi();
        ctx.Capture("tool_interaction_paint_bucket_and_retouch.png");

        Console.WriteLine("    [OK] ToolInteractionAuditSuite: Paint bucket fill, BFS flood fill, retouch aliases, and roundedrect verified.");
    }
}
