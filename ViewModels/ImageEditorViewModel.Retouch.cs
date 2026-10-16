using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.ImageEditor.Controls;
using PdfEditorApp.Plugins.ImageEditor.Core.Paint;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Remove Tool Options & Staging ─────────────────────────────────────────
    [ObservableProperty] private bool _removeAfterEachStroke = true;
    [ObservableProperty] private bool _removeCreateNewLayer = false;
    [ObservableProperty] private bool _removeSampleAllLayers = false;

    public System.Collections.ObjectModel.ObservableCollection<List<Point>> PendingRemoveStrokes { get; } = new();

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public void CommitRemove()
    {
        if (PendingRemoveStrokes.Count == 0) return;
        var strokes = PendingRemoveStrokes.ToList();
        PendingRemoveStrokes.Clear();
        CanvasControl?.InvalidateVisual();
        ApplyRemoveJobInternal(strokes);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public void CancelRemove()
    {
        if (PendingRemoveStrokes.Count == 0) return;
        PendingRemoveStrokes.Clear();
        CanvasControl?.InvalidateVisual();
        StatusMessage = "Remove strokes cancelled.";
    }

    /// <summary>
    /// Applies a raster retouch stroke (Remove Tool, Clone Stamp, Healing, Dodge, Burn, Sponge, Blur, Sharpen, Smudge)
    /// to the target ImageElement or base canvas raster layer.
    /// </summary>
    public void ApplyRetouchStroke(string tool, IReadOnlyList<Point> points)
    {
        if (points.Count == 0) return;

        if (tool.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            if (!RemoveAfterEachStroke)
            {
                PendingRemoveStrokes.Add(points.ToList());
                CanvasControl?.InvalidateVisual();
                StatusMessage = $"Remove: {PendingRemoveStrokes.Count} strokes waiting. Press Enter to apply or Esc to cancel.";
                return;
            }

            ApplyRemoveJobInternal(new[] { points.ToList() });
            return;
        }

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
                case "patternstamp":
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
                case "healingbrush":
                {
                    // Healing applies cloned texture with Poisson gradient-domain illumination matching
                    Point srcPixel = CloneSourcePoint.HasValue
                        ? new Point((CloneSourcePoint.Value.X - elX) / elW * imgW, (CloneSourcePoint.Value.Y - elY) / elH * imgH)
                        : RetouchEngine.FindBestHealingOffset(beforeBytes, imgW, imgH, pixelPoints[0], pixelRadius);

                    for (int i = 0; i < pixelPoints.Count; i++)
                    {
                        Point currentSrc = srcPixel + (pixelPoints[i] - pixelPoints[0]);
                        RetouchEngine.ApplyHealingDab(
                            bgra, imgW, imgH,
                            beforeBytes, imgW, imgH,
                            pixelPoints[i], currentSrc,
                            pixelRadius, hardness, BrushOpacity);
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
                case "arthistorybrush":
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
                case "magiceraser":
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
                case "colorreplace":
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

                case "redeye":
                {
                    int changed = 0;
                    foreach (var pt in pixelPoints)
                    {
                        changed += RetouchEngine.ApplyRedEye(bgra, imgW, imgH, pt, RedEyePupilSize, RedEyeDarkenAmount);
                    }
                    StatusMessage = changed > 0
                        ? $"Red Eye: Neutralized pupil at click point ({changed} pixels modified)."
                        : "Red Eye: No red pupil detected at click location.";
                    break;
                }

                case "patch":
                {
                    if (pixelPoints.Count >= 2)
                    {
                        Point startPt = pixelPoints[0];
                        Point endPt = pixelPoints[^1];
                        Point offset = endPt - startPt;
                        Rect patchRect = new Rect(
                            Math.Max(0, startPt.X - pixelRadius),
                            Math.Max(0, startPt.Y - pixelRadius),
                            Math.Min(imgW - 1, pixelRadius * 2),
                            Math.Min(imgH - 1, pixelRadius * 2));
                        RetouchEngine.ApplyPatch(bgra, imgW, imgH, patchRect, offset, PatchMode == "Destination");
                        StatusMessage = "Patch Tool: Seamless boundary texture blend committed.";
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
                target.OriginalImageBytes = afterBytes;

                // 6. Record undoable command
                string toolTitle = tool switch
                {
                    "clonestamp" or "patternstamp" => "Clone Stamp",
                    "healing" or "healingbrush" => "Healing Brush",
                    "historybrush" or "arthistorybrush" => "History Brush",
                    "bgeraser" or "magiceraser" => "Background Eraser",
                    "colorreplacement" or "colorreplace" => "Color Replacement",
                    "dodge" => "Dodge",
                    "burn" => "Burn",
                    "sponge" => "Sponge",
                    "blur" => "Blur",
                    "sharpen" => "Sharpen",
                    "smudge" => "Smudge",
                    _ => "Retouch"
                };

                var undoCmd = new RetouchImageCommand(target, rawSourceBytes, afterBytes, toolTitle, (el, b) =>
                {
                    el.AdjustedSource?.Dispose();
                    el.AdjustedSource = new Bitmap(new MemoryStream(b));
                    el.OriginalImageBytes = b;
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

    /// <summary>
    /// Executes the Remove Tool inpainting job: supports multi-stroke batch accumulation,
    /// non-destructive new layer generation, and composite sampling.
    /// </summary>
    public void ApplyRemoveJobInternal(IReadOnlyList<List<Point>> strokes)
    {
        if (strokes == null || strokes.Count == 0) return;

        // 1. Locate target ImageElement (selected, under cursor, or topmost)
        ImageElement? target = SelectedElement as ImageElement;
        if (target == null && strokes[0].Count > 0)
        {
            var hitPt = strokes[0][0];
            target = Elements.OfType<ImageElement>()
                .OrderByDescending(e => e.ZIndex)
                .FirstOrDefault(e => e.IsVisible && !e.IsLocked && e.HitTestRotationAware(hitPt));
        }

        target ??= Elements.OfType<ImageElement>()
            .OrderByDescending(e => e.ZIndex)
            .FirstOrDefault(e => e.IsVisible && !e.IsLocked);

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
        var pixelStrokes = new List<IReadOnlyList<Point>>(strokes.Count);
        foreach (var stroke in strokes)
        {
            var pList = new List<Point>(stroke.Count);
            foreach (var p in stroke)
            {
                double px = Math.Clamp((p.X - elX) / elW * imgW, 0, imgW - 1);
                double py = Math.Clamp((p.Y - elY) / elH * imgH, 0, imgH - 1);
                pList.Add(new Point(px, py));
            }
            pixelStrokes.Add(pList);
        }

        double scaleToImg = imgW / elW;
        double pixelRadius = Math.Max(1.0, (BrushThickness / 2.0) * scaleToImg);

        // Sample All Layers: composite visible layers
        byte[]? sampleSourceBgra = null;
        if (RemoveSampleAllLayers)
        {
            try
            {
                int cW = (int)Math.Max(1, CanvasWidth);
                int cH = (int)Math.Max(1, CanvasHeight);
                var rtb = new RenderTargetBitmap(new PixelSize(cW, cH));
                using (var dc = rtb.CreateDrawingContext())
                {
                    foreach (var el in Elements.Where(e => e.IsVisible).OrderBy(e => e.ZIndex))
                    {
                        EditorCanvasControl.DrawElement(dc, el);
                    }
                }
                using var ms = new MemoryStream();
                rtb.Save(ms, new PngBitmapEncoderOptions());
                using var compBmp = SKBitmap.Decode(ms.ToArray());
                if (compBmp != null)
                {
                    using var subBmp = new SKBitmap(imgW, imgH, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var subCanvas = new SKCanvas(subBmp);
                    var srcRect = new SKRect((float)elX, (float)elY, (float)(elX + elW), (float)(elY + elH));
                    var dstRect = new SKRect(0, 0, imgW, imgH);
                    subCanvas.DrawBitmap(compBmp, srcRect, dstRect);
                    sampleSourceBgra = subBmp.Bytes;
                }
            }
            catch
            {
                sampleSourceBgra = null;
            }
        }

        RetouchEngine.ApplyRemoveJob(
            bgra, imgW, imgH,
            sampleSourceBgra, imgW, imgH,
            pixelStrokes,
            pixelRadius,
            closeLoops: true,
            out bool[] holeMask,
            out byte[] filledBgra);

        if (RemoveCreateNewLayer)
        {
            // Non-destructive: write patched hole to a new layer above target
            byte[] newLayerBgra = new byte[imgW * imgH * 4];
            for (int i = 0; i < imgW * imgH; i++)
            {
                if (holeMask[i])
                {
                    int idx = i * 4;
                    newLayerBgra[idx] = filledBgra[idx];
                    newLayerBgra[idx + 1] = filledBgra[idx + 1];
                    newLayerBgra[idx + 2] = filledBgra[idx + 2];
                    newLayerBgra[idx + 3] = (bgra[idx + 3] > 0) ? bgra[idx + 3] : (byte)255;
                }
            }

            using var resBmp = new SKBitmap(imgW, imgH, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = GCHandle.Alloc(newLayerBgra, GCHandleType.Pinned);
            try
            {
                resBmp.InstallPixels(resBmp.Info, pin.AddrOfPinnedObject());
                using var outImg = SKImage.FromBitmap(resBmp);
                using var encoded = outImg.Encode(SKEncodedImageFormat.Png, 100);
                byte[] pngBytes = encoded.ToArray();

                var newLayer = new ImageElement
                {
                    Name = $"{target.Name} (Removed)",
                    X = target.X,
                    Y = target.Y,
                    Width = target.Width,
                    Height = target.Height,
                    NaturalPixelWidth = imgW,
                    NaturalPixelHeight = imgH,
                    Source = new Bitmap(new MemoryStream(pngBytes)),
                    OriginalImageBytes = pngBytes,
                    ZIndex = target.ZIndex + 1,
                    IsVisible = true,
                    Opacity = 1.0
                };

                int targetIdx = Elements.IndexOf(target);
                int insertIdx = targetIdx >= 0 ? targetIdx + 1 : Elements.Count;
                Elements.Insert(insertIdx, newLayer);
                SelectedElement = newLayer;

                if (!_isApplyingHistory) History.Push(new AddElementCommand(Elements, newLayer));
                RefreshCanvas();
                ScheduleHistogramUpdate();
                StatusMessage = "Remove tool: Inpainted patch placed on new layer.";
            }
            finally
            {
                pin.Free();
            }
        }
        else
        {
            // In-place modification
            using var resBmp = new SKBitmap(imgW, imgH, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = GCHandle.Alloc(filledBgra, GCHandleType.Pinned);
            try
            {
                resBmp.InstallPixels(resBmp.Info, pin.AddrOfPinnedObject());
                using var outImg = SKImage.FromBitmap(resBmp);
                using var encoded = outImg.Encode(SKEncodedImageFormat.Png, 100);
                byte[] afterBytes = encoded.ToArray();

                target.AdjustedSource?.Dispose();
                target.AdjustedSource = new Bitmap(new MemoryStream(afterBytes));
                target.OriginalImageBytes = afterBytes;

                var undoCmd = new RetouchImageCommand(target, rawSourceBytes, afterBytes, "Remove Tool", (el, b) =>
                {
                    el.AdjustedSource?.Dispose();
                    el.AdjustedSource = new Bitmap(new MemoryStream(b));
                    el.OriginalImageBytes = b;
                    RefreshCanvas();
                });
                if (!_isApplyingHistory) History.Push(undoCmd);

                RefreshCanvas();
                ScheduleHistogramUpdate();
                StatusMessage = "Remove tool stroke applied.";
            }
            finally
            {
                pin.Free();
            }
        }
    }

    /// <summary>
    /// Applies Paint Bucket fill to the canvas: fills clicked vector shape FillColor,
    /// TextElement foreground color, raster ImageElement via BFS Flood Fill, or document background.
    /// </summary>
    public void ApplyPaintBucketFill(Point canvasPt)
    {
        // 1. Check if clicking on a vector shape with fill
        var hitShape = Elements.OrderByDescending(e => e.ZIndex)
            .FirstOrDefault(e => e.IsVisible && !e.IsLocked && e.HitTestRotationAware(canvasPt));

        if (hitShape is IHasFill fillable)
        {
            Color oldColor = fillable.FillColor;
            Color newColor = PrimaryColor;
            fillable.FillColor = newColor;
            if (ReferenceEquals(SelectedElement, fillable))
                FillColorField = newColor;
            CanvasControl?.InvalidateVisual();
            if (!_isApplyingHistory)
            {
                History.Push(new GenericPropertyCommand<IHasFill, Color>(
                    fillable,
                    (target, c) => { target.FillColor = c; if (ReferenceEquals(SelectedElement, target)) FillColorField = c; CanvasControl?.InvalidateVisual(); },
                    oldColor, newColor, "Paint Bucket Fill"));
            }
            StatusMessage = $"Paint Bucket: Filled shape with #{PrimaryColor.R:X2}{PrimaryColor.G:X2}{PrimaryColor.B:X2}.";
            return;
        }

        if (hitShape is TextElement textEl)
        {
            Color oldColor = textEl.TextColor;
            Color newColor = PrimaryColor;
            textEl.TextColor = newColor;
            if (ReferenceEquals(SelectedElement, textEl))
                TextForegroundColorField = newColor;
            CanvasControl?.InvalidateVisual();
            if (!_isApplyingHistory)
            {
                History.Push(new GenericPropertyCommand<TextElement, Color>(
                    textEl,
                    (target, c) => { target.TextColor = c; if (ReferenceEquals(SelectedElement, target)) TextForegroundColorField = c; CanvasControl?.InvalidateVisual(); },
                    oldColor, newColor, "Paint Bucket Fill"));
            }
            StatusMessage = $"Paint Bucket: Filled text with #{PrimaryColor.R:X2}{PrimaryColor.G:X2}{PrimaryColor.B:X2}.";
            return;
        }

        // 2. Locate target ImageElement (hit element, selected, or topmost raster)
        ImageElement? targetImg = hitShape as ImageElement ?? SelectedElement as ImageElement;
        if (targetImg == null)
        {
            targetImg = Elements.OfType<ImageElement>()
                .OrderByDescending(e => e.ZIndex)
                .FirstOrDefault(e => e.IsVisible && !e.IsLocked && e.HitTestRotationAware(canvasPt));
        }

        if (targetImg != null)
        {
            ApplyImageFloodFill(targetImg, canvasPt);
            return;
        }

        // 3. Otherwise, fill document background color
        Color oldBg = BackgroundColor;
        Color newBg = PrimaryColor;
        BackgroundColor = newBg;
        CanvasControl?.SetBackgroundColor(newBg);
        CanvasControl?.InvalidateVisual();
        if (!_isApplyingHistory)
        {
            History.Push(new GenericPropertyCommand<ImageEditorViewModel, Color>(
                this,
                (vm, c) => { vm.BackgroundColor = c; vm.CanvasControl?.SetBackgroundColor(c); vm.CanvasControl?.InvalidateVisual(); },
                oldBg, newBg, "Paint Bucket Fill Background"));
        }
        StatusMessage = $"Paint Bucket: Filled canvas background with #{newBg.R:X2}{newBg.G:X2}{newBg.B:X2}.";
    }

    private void ApplyImageFloodFill(ImageElement target, Point canvasPt)
    {
        byte[]? rawSourceBytes = target.OriginalImageBytes;
        if (rawSourceBytes == null || rawSourceBytes.Length == 0)
        {
            if (target.AdjustedSource is Bitmap adjBmp)
            {
                using var ms = new MemoryStream();
                adjBmp.Save(ms, new PngBitmapEncoderOptions());
                rawSourceBytes = ms.ToArray();
                target.OriginalImageBytes = rawSourceBytes;
            }
            else if (target.Source is Bitmap srcBmpAv)
            {
                using var ms = new MemoryStream();
                srcBmpAv.Save(ms, new PngBitmapEncoderOptions());
                rawSourceBytes = ms.ToArray();
                target.OriginalImageBytes = rawSourceBytes;
            }
        }

        if (rawSourceBytes == null || rawSourceBytes.Length == 0) return;

        using var srcBmp = SKBitmap.Decode(rawSourceBytes);
        if (srcBmp == null) return;
        using (srcBmp)
        {
            int imgW = srcBmp.Width;
            int imgH = srcBmp.Height;
            byte[] bgra = srcBmp.Bytes;
            byte[] beforeBytes = (byte[])bgra.Clone();

            double elW = Math.Max(1.0, target.Width);
            double elH = Math.Max(1.0, target.Height);
            double elX = target.X;
            double elY = target.Y;

            int startX = (int)Math.Clamp((canvasPt.X - elX) / elW * imgW, 0, imgW - 1);
            int startY = (int)Math.Clamp((canvasPt.Y - elY) / elH * imgH, 0, imgH - 1);

            int startIdx = (startY * imgW + startX) * 4;
            byte targetB = bgra[startIdx];
            byte targetG = bgra[startIdx + 1];
            byte targetR = bgra[startIdx + 2];
            byte targetA = bgra[startIdx + 3];

            byte fillB = PrimaryColor.B;
            byte fillG = PrimaryColor.G;
            byte fillR = PrimaryColor.R;
            byte fillA = (byte)Math.Clamp((int)(PrimaryColor.A * (BrushOpacity / 100.0)), 0, 255);

            double tol = 32.0;
            double tolSq = (tol * tol) * 3;

            bool Matches(int idx)
            {
                int db = bgra[idx] - targetB;
                int dg = bgra[idx + 1] - targetG;
                int dr = bgra[idx + 2] - targetR;
                return (dr * dr + dg * dg + db * db) <= tolSq;
            }

            int startDr = fillR - targetR;
            int startDg = fillG - targetG;
            int startDb = fillB - targetB;
            if ((startDr * startDr + startDg * startDg + startDb * startDb) <= 4 && Math.Abs(fillA - targetA) <= 4)
            {
                StatusMessage = "Paint Bucket: Selected area already has this color.";
                return;
            }

            bool[] visited = new bool[imgW * imgH];
            Queue<int> queue = new Queue<int>();

            int startCoord = startY * imgW + startX;
            queue.Enqueue(startCoord);
            visited[startCoord] = true;

            int filledCount = 0;
            while (queue.Count > 0)
            {
                int coord = queue.Dequeue();
                int cx = coord % imgW;
                int cy = coord / imgW;
                int idx = coord * 4;

                if (fillA >= 254)
                {
                    bgra[idx] = fillB;
                    bgra[idx + 1] = fillG;
                    bgra[idx + 2] = fillR;
                    bgra[idx + 3] = fillA;
                }
                else
                {
                    double alpha = fillA / 255.0;
                    double inv = 1.0 - alpha;
                    bgra[idx] = (byte)Math.Clamp((int)(fillB * alpha + bgra[idx] * inv), 0, 255);
                    bgra[idx + 1] = (byte)Math.Clamp((int)(fillG * alpha + bgra[idx + 1] * inv), 0, 255);
                    bgra[idx + 2] = (byte)Math.Clamp((int)(fillR * alpha + bgra[idx + 2] * inv), 0, 255);
                    bgra[idx + 3] = (byte)Math.Clamp((int)(fillA + bgra[idx + 3] * inv), 0, 255);
                }
                filledCount++;

                if (cx > 0)
                {
                    int left = coord - 1;
                    if (!visited[left] && Matches(left * 4))
                    {
                        visited[left] = true;
                        queue.Enqueue(left);
                    }
                }
                if (cx < imgW - 1)
                {
                    int right = coord + 1;
                    if (!visited[right] && Matches(right * 4))
                    {
                        visited[right] = true;
                        queue.Enqueue(right);
                    }
                }
                if (cy > 0)
                {
                    int up = coord - imgW;
                    if (!visited[up] && Matches(up * 4))
                    {
                        visited[up] = true;
                        queue.Enqueue(up);
                    }
                }
                if (cy < imgH - 1)
                {
                    int down = coord + imgW;
                    if (!visited[down] && Matches(down * 4))
                    {
                        visited[down] = true;
                        queue.Enqueue(down);
                    }
                }
            }

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
                target.OriginalImageBytes = afterBytes;

                var undoCmd = new RetouchImageCommand(target, rawSourceBytes, afterBytes, "Paint Bucket Fill", (el, b) =>
                {
                    el.AdjustedSource?.Dispose();
                    el.AdjustedSource = new Bitmap(new MemoryStream(b));
                    el.OriginalImageBytes = b;
                    RefreshCanvas();
                });
                if (!_isApplyingHistory) History.Push(undoCmd);
                RefreshCanvas();
                StatusMessage = $"Paint Bucket: Filled {filledCount:N0} pixels with #{fillR:X2}{fillG:X2}{fillB:X2}.";
            }
            finally
            {
                pin.Free();
            }
        }
    }
}

