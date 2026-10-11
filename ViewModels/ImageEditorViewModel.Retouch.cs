using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.ImageEditor.Core.Paint;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    /// <summary>
    /// Applies a raster retouch stroke (Clone Stamp, Healing, Dodge, Burn, Sponge, Blur, Sharpen, Smudge)
    /// to the target ImageElement or base canvas raster layer.
    /// </summary>
    public void ApplyRetouchStroke(string tool, IReadOnlyList<Point> points)
    {
        if (points.Count == 0) return;

        // 1. Locate target ImageElement (selected, under cursor, or topmost)
        ImageElement? target = SelectedElement as ImageElement;
        if (target == null)
        {
            var hitPt = points[0];
            target = Elements.OfType<ImageElement>()
                .OrderByDescending(e => e.ZIndex)
                .FirstOrDefault(e => e.IsVisible && !e.IsLocked && e.HitTestRotationAware(hitPt));
        }

        target ??= Elements.OfType<ImageElement>()
            .OrderByDescending(e => e.ZIndex)
            .FirstOrDefault(e => e.IsVisible && !e.IsLocked);

        // 2. If no ImageElement exists, create a baseline canvas bitmap layer
        if (target == null)
        {
            target = new ImageElement
            {
                X = 0,
                Y = 0,
                Width = CanvasWidth,
                Height = CanvasHeight,
                NaturalPixelWidth = (int)Math.Max(100, CanvasWidth),
                NaturalPixelHeight = (int)Math.Max(100, CanvasHeight),
                Name = "Background Raster Layer"
            };

            using var initBmp = new SKBitmap(target.NaturalPixelWidth, target.NaturalPixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(initBmp))
            {
                canvas.Clear(SKColors.White);
            }
            using var initImg = SKImage.FromBitmap(initBmp);
            using var initEnc = initImg.Encode(SKEncodedImageFormat.Png, 100);
            target.OriginalImageBytes = initEnc.ToArray();
            target.Source = new Bitmap(new MemoryStream(target.OriginalImageBytes));
            Elements.Insert(0, target);
        }

        try
        {
            // 3. Decode source pixels
            byte[]? rawSourceBytes = target.OriginalImageBytes;
            if (rawSourceBytes == null || rawSourceBytes.Length == 0)
            {
                if (target.Source != null)
                {
                    using var ms = new MemoryStream();
                    target.Source.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    rawSourceBytes = ms.ToArray();
                    target.OriginalImageBytes = rawSourceBytes;
                }
            }

            if (rawSourceBytes == null || rawSourceBytes.Length == 0) return;

            using var srcBmp = SKBitmap.Decode(rawSourceBytes);
            if (srcBmp == null) return;

            int imgW = srcBmp.Width;
            int imgH = srcBmp.Height;
            byte[] bgra = srcBmp.Bytes;
            byte[] beforeBytes = (byte[])bgra.Clone();

            double elW = Math.Max(1.0, target.Width);
            double elH = Math.Max(1.0, target.Height);
            double elX = target.X;
            double elY = target.Y;

            // Map stroke points to pixel coordinates on the image
            var pixelPoints = new List<Point>(points.Count);
            foreach (var p in points)
            {
                double px = Math.Clamp((p.X - elX) / elW * imgW, 0, imgW - 1);
                double py = Math.Clamp((p.Y - elY) / elH * imgH, 0, imgH - 1);
                pixelPoints.Add(new Point(px, py));
            }

            double scaleToImg = imgW / elW;
            double pixelRadius = Math.Max(1.0, (BrushThickness / 2.0) * scaleToImg);
            double hardness = BrushHardness / 100.0;

            // 4. Execute specific retouch tool
            switch (tool.ToLowerInvariant())
            {
                case "clonestamp":
                case "clone":
                {
                    if (CloneSourcePoint == null)
                    {
                        StatusMessage = "Option/Alt-click on the canvas to set a clone source point before painting.";
                        return;
                    }

                    Point srcCanvas = CloneSourcePoint.Value;
                    Point startCanvas = points[0];
                    double srcNormX = (srcCanvas.X - elX) / elW;
                    double srcNormY = (srcCanvas.Y - elY) / elH;
                    Point srcPixelOrigin = new Point(srcNormX * imgW, srcNormY * imgH);

                    for (int i = 0; i < pixelPoints.Count; i++)
                    {
                        Point delta = pixelPoints[i] - pixelPoints[0];
                        Point currentSrc = srcPixelOrigin + delta;
                        RetouchEngine.ApplyCloneStampDab(
                            bgra, imgW, imgH,
                            beforeBytes, imgW, imgH,
                            pixelPoints[i], currentSrc,
                            pixelRadius, hardness, BrushOpacity, BrushFlow);
                    }
                    break;
                }

                case "healing":
                case "spothealing":
                {
                    // Healing applies cloned texture with luminance matching
                    Point srcPixel = CloneSourcePoint.HasValue
                        ? new Point((CloneSourcePoint.Value.X - elX) / elW * imgW, (CloneSourcePoint.Value.Y - elY) / elH * imgH)
                        : new Point(pixelPoints[0].X + pixelRadius * 1.5, pixelPoints[0].Y);

                    for (int i = 0; i < pixelPoints.Count; i++)
                    {
                        Point currentSrc = srcPixel + (pixelPoints[i] - pixelPoints[0]);
                        RetouchEngine.ApplyCloneStampDab(
                            bgra, imgW, imgH,
                            beforeBytes, imgW, imgH,
                            pixelPoints[i], currentSrc,
                            pixelRadius, hardness, 90.0, 90.0);
                    }
                    break;
                }

                case "dodge":
                {
                    var range = DodgeBurnRange switch
                    {
                        "Shadows" => RetouchEngine.ToneRange.Shadows,
                        "Highlights" => RetouchEngine.ToneRange.Highlights,
                        _ => RetouchEngine.ToneRange.Midtones
                    };

                    foreach (var pt in pixelPoints)
                    {
                        RetouchEngine.ApplyDodgeDab(
                            bgra, imgW, imgH,
                            pt, pixelRadius, hardness,
                            DodgeBurnExposure, range, DodgeBurnProtectTones);
                    }
                    break;
                }

                case "burn":
                {
                    var range = DodgeBurnRange switch
                    {
                        "Shadows" => RetouchEngine.ToneRange.Shadows,
                        "Highlights" => RetouchEngine.ToneRange.Highlights,
                        _ => RetouchEngine.ToneRange.Midtones
                    };

                    foreach (var pt in pixelPoints)
                    {
                        RetouchEngine.ApplyBurnDab(
                            bgra, imgW, imgH,
                            pt, pixelRadius, hardness,
                            DodgeBurnExposure, range, DodgeBurnProtectTones);
                    }
                    break;
                }

                case "sponge":
                {
                    var mode = SpongeMode == "Saturate"
                        ? RetouchEngine.SpongeMode.Saturate
                        : RetouchEngine.SpongeMode.Desaturate;

                    foreach (var pt in pixelPoints)
                    {
                        RetouchEngine.ApplySpongeDab(
                            bgra, imgW, imgH,
                            pt, pixelRadius, hardness,
                            SpongeFlow, mode, SpongeVibrance);
                    }
                    break;
                }

                case "blur":
                {
                    foreach (var pt in pixelPoints)
                    {
                        RetouchEngine.ApplyBlurDab(
                            bgra, imgW, imgH,
                            pt, pixelRadius, hardness,
                            RetouchStrength);
                    }
                    break;
                }

                case "sharpen":
                {
                    foreach (var pt in pixelPoints)
                    {
                        RetouchEngine.ApplySharpenDab(
                            bgra, imgW, imgH,
                            pt, pixelRadius, hardness,
                            RetouchStrength, ProtectDetail);
                    }
                    break;
                }

                case "smudge":
                {
                    var buffer = new RetouchEngine.SmudgeBuffer();
                    foreach (var pt in pixelPoints)
                    {
                        RetouchEngine.ApplySmudgeDab(
                            bgra, imgW, imgH,
                            pt, pixelRadius, hardness,
                            RetouchStrength, buffer);
                    }
                    break;
                }

                case "historybrush":
                {
                    byte[]? snapBytes = _historyBrushSnapshotBytes;
                    byte[]? snapBgra = null;
                    int snapW = imgW, snapH = imgH;
                    if (snapBytes != null && snapBytes.Length > 0)
                    {
                        using var snapBmp = SKBitmap.Decode(snapBytes);
                        if (snapBmp != null)
                        {
                            snapBgra = snapBmp.Bytes;
                            snapW = snapBmp.Width;
                            snapH = snapBmp.Height;
                        }
                    }

                    snapBgra ??= target.OriginalImageBytes != null ? SKBitmap.Decode(target.OriginalImageBytes)?.Bytes ?? beforeBytes : beforeBytes;

                    foreach (var pt in pixelPoints)
                    {
                        RetouchEngine.ApplyCloneStampDab(
                            bgra, imgW, imgH,
                            snapBgra, snapW, snapH,
                            pt, pt,
                            pixelRadius, hardness, BrushOpacity, BrushFlow);
                    }
                    break;
                }

                case "bgeraser":
                {
                    Color sampledBg = SecondaryColor;
                    if (pixelPoints.Count > 0)
                    {
                        int sx = (int)Math.Clamp(pixelPoints[0].X, 0, imgW - 1);
                        int sy = (int)Math.Clamp(pixelPoints[0].Y, 0, imgH - 1);
                        int idx = (sy * imgW + sx) * 4;
                        sampledBg = Color.FromArgb(bgra[idx + 3], bgra[idx + 2], bgra[idx + 1], bgra[idx]);
                    }
                    foreach (var pt in pixelPoints)
                    {
                        RetouchEngine.ApplyBackgroundEraserDab(
                            bgra, imgW, imgH,
                            pt, sampledBg, pixelRadius, hardness,
                            tolerance: 32.0, opacity: BrushOpacity);
                    }
                    break;
                }

                case "colorreplacement":
                {
                    Color repColor = PrimaryColor;
                    foreach (var pt in pixelPoints)
                    {
                        RetouchEngine.ApplyColorReplacementDab(
                            bgra, imgW, imgH,
                            pt, repColor, pixelRadius, hardness,
                            opacity: BrushOpacity);
                    }
                    break;
                }
            }

            // 5. Bake modified pixels back into target ImageElement
            using var resultBmp = new SKBitmap(imgW, imgH, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = GCHandle.Alloc(bgra, GCHandleType.Pinned);
            try
            {
                resultBmp.InstallPixels(resultBmp.Info, pin.AddrOfPinnedObject());
                using var outImg = SKImage.FromBitmap(resultBmp);
                using var encoded = outImg.Encode(SKEncodedImageFormat.Png, 100);
                byte[] afterBytes = encoded.ToArray();

                target.AdjustedSource?.Dispose();
                target.AdjustedSource = new Bitmap(new MemoryStream(afterBytes));

                // 6. Record undoable command
                string toolTitle = tool switch
                {
                    "clonestamp" => "Clone Stamp",
                    "healing" => "Healing Brush",
                    "historybrush" => "History Brush",
                    "bgeraser" => "Background Eraser",
                    "colorreplacement" => "Color Replacement",
                    "dodge" => "Dodge",
                    "burn" => "Burn",
                    "sponge" => "Sponge",
                    "blur" => "Blur",
                    "sharpen" => "Sharpen",
                    "smudge" => "Smudge",
                    _ => "Retouch"
                };

                var undoCmd = new RetouchImageCommand(target, beforeBytes, afterBytes, toolTitle, (el, b) =>
                {
                    el.AdjustedSource?.Dispose();
                    el.AdjustedSource = new Bitmap(new MemoryStream(b));
                    RefreshCanvas();
                });
                if (!_isApplyingHistory) History.Push(undoCmd);

                RefreshCanvas();
                ScheduleHistogramUpdate();
                StatusMessage = $"{toolTitle} stroke applied.";
            }
            finally
            {
                pin.Free();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApplyRetouchStroke] Error: {ex}");
            StatusMessage = $"Retouch error: {ex.Message}";
        }
    }
}
