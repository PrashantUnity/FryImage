using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
    // ── Select and Mask / Natural Matting Workspace State ───────────────────
    [ObservableProperty] private bool _isSelectAndMaskDialogOpen;
    [ObservableProperty] private double _mattingRadius = 4.0;
    [ObservableProperty] private bool _mattingSmartRadius = true;
    [ObservableProperty] private double _mattingSmooth = 0.0;
    [ObservableProperty] private double _mattingFeather = 0.5;
    [ObservableProperty] private double _mattingContrast = 10.0;
    [ObservableProperty] private double _mattingShiftEdge = 0.0;
    [ObservableProperty] private bool _mattingDecontaminateColors = true;
    [ObservableProperty] private double _mattingDecontaminateAmount = 100.0;
    [ObservableProperty] private string _mattingViewMode = "OnBlack"; // OnionSkin | MarchingAnts | Overlay | OnBlack | OnWhite | BlackWhite
    [ObservableProperty] private string _mattingOutputMode = "NewLayer"; // Selection | LayerMask | NewLayer

    public IReadOnlyList<string> AvailableMattingViewModes { get; } =
        ["OnBlack", "OnWhite", "BlackWhite", "Overlay", "MarchingAnts"];

    public IReadOnlyList<string> AvailableMattingOutputModes { get; } =
        ["NewLayer", "LayerMask", "Selection"];

    private ImageElement? _mattingTargetImage;
    private byte[]? _mattingOriginalImageBytes;
    private byte[]? _mattingInitialMask;

    [RelayCommand]
    public void OpenSelectAndMaskDialog()
    {
        _mattingTargetImage = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (_mattingTargetImage == null)
        {
            StatusMessage = "Select an image layer to open Select and Mask workspace.";
            return;
        }

        _mattingOriginalImageBytes = _mattingTargetImage.OriginalImageBytes;
        if (_mattingOriginalImageBytes == null || _mattingOriginalImageBytes.Length == 0)
        {
            StatusMessage = "Target layer has no image data to refine.";
            return;
        }

        // Initialize default mask from image alpha or centered foreground ellipse
        using var bmp = SKBitmap.Decode(_mattingOriginalImageBytes);
        if (bmp != null)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            byte[] mask = new byte[w * h];
            byte[] bgra = bmp.Bytes;

            bool hasAlpha = false;
            for (int i = 0; i < mask.Length; i++)
            {
                byte a = bgra[i * 4 + 3];
                mask[i] = a;
                if (a < 250) hasAlpha = true;
            }

            // If completely opaque, create an initial center elliptical selection mask
            if (!hasAlpha)
            {
                int cx = w / 2;
                int cy = h / 2;
                double rx = w * 0.38;
                double ry = h * 0.38;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        double dx = (x - cx) / rx;
                        double dy = (y - cy) / ry;
                        mask[y * w + x] = (byte)(dx * dx + dy * dy <= 1.0 ? 255 : 0);
                    }
                }
            }

            _mattingInitialMask = mask;
        }

        IsSelectAndMaskDialogOpen = true;
        StatusMessage = "Select and Mask workspace open. Adjust edge detection and matting parameters.";
    }

    [RelayCommand]
    public void CancelSelectAndMaskDialog()
    {
        if (_mattingTargetImage != null && _mattingOriginalImageBytes != null)
        {
            _mattingTargetImage.AdjustedSource?.Dispose();
            _mattingTargetImage.AdjustedSource = null;
            RefreshCanvas();
        }

        _mattingTargetImage = null;
        _mattingOriginalImageBytes = null;
        _mattingInitialMask = null;
        IsSelectAndMaskDialogOpen = false;
        StatusMessage = "Select and Mask cancelled.";
    }

    [RelayCommand]
    public void ConfirmSelectAndMaskDialog()
    {
        if (_mattingTargetImage == null || _mattingOriginalImageBytes == null || _mattingInitialMask == null)
        {
            IsSelectAndMaskDialogOpen = false;
            return;
        }

        try
        {
            using var bmp = SKBitmap.Decode(_mattingOriginalImageBytes);
            if (bmp == null)
            {
                IsSelectAndMaskDialogOpen = false;
                return;
            }

            int w = bmp.Width;
            int h = bmp.Height;
            byte[] bgra = bmp.Bytes;

            // 1. Run Natural Matting Engine
            byte[] refinedMask = MattingEngine.RefineSelectionMask(
                _mattingInitialMask,
                bgra,
                w,
                h,
                MattingRadius,
                MattingSmartRadius,
                MattingSmooth,
                MattingFeather,
                MattingContrast,
                MattingShiftEdge);

            // 2. Color Decontamination (cleans foreground fringe / halo)
            byte[] finalBgra = (byte[])bgra.Clone();
            if (MattingDecontaminateColors)
            {
                finalBgra = MattingEngine.DecontaminateColors(finalBgra, refinedMask, w, h, MattingDecontaminateAmount / 100.0);
            }

            // Apply refined alpha mask into BGRA
            for (int i = 0; i < refinedMask.Length; i++)
            {
                byte a = refinedMask[i];
                finalBgra[i * 4 + 3] = a;
            }

            // 3. Output according to selected mode
            using var outBmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = GCHandle.Alloc(finalBgra, GCHandleType.Pinned);
            try
            {
                outBmp.InstallPixels(outBmp.Info, pin.AddrOfPinnedObject());
                using var outImg = SKImage.FromBitmap(outBmp);
                using var enc = outImg.Encode(SKEncodedImageFormat.Png, 100);
                byte[] pngBytes = enc.ToArray();

                if (MattingOutputMode == "NewLayer")
                {
                    var newElement = new ImageElement
                    {
                        X = _mattingTargetImage.X,
                        Y = _mattingTargetImage.Y,
                        Width = _mattingTargetImage.Width,
                        Height = _mattingTargetImage.Height,
                        NaturalPixelWidth = w,
                        NaturalPixelHeight = h,
                        Name = $"{_mattingTargetImage.Name} (Masked Cutout)",
                        OriginalImageBytes = pngBytes,
                        Source = new Bitmap(new MemoryStream(pngBytes)),
                        ZIndex = Elements.Count > 0 ? Elements.Max(e => e.ZIndex) + 1 : 1
                    };

                    Elements.Add(newElement);
                    SelectedElement = newElement;
                    History.Push(new AddElementCommand(Elements, newElement));
                    StatusMessage = "Refined cutout created on new layer.";
                }
                else
                {
                    byte[] oldBytes = _mattingTargetImage.OriginalImageBytes ?? Array.Empty<byte>();
                    _mattingTargetImage.OriginalImageBytes = pngBytes;
                    _mattingTargetImage.AdjustedSource?.Dispose();
                    _mattingTargetImage.AdjustedSource = new Bitmap(new MemoryStream(pngBytes));

                    History.Push(new RetouchImageCommand(_mattingTargetImage, oldBytes, pngBytes, "Select and Mask", (el, b) =>
                    {
                        el.OriginalImageBytes = b;
                        el.AdjustedSource?.Dispose();
                        el.AdjustedSource = new Bitmap(new MemoryStream(b));
                        RefreshCanvas();
                    }));

                    StatusMessage = "Select and Mask applied to layer.";
                }
            }
            finally
            {
                pin.Free();
            }

            RefreshCanvas();
            ScheduleHistogramUpdate();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Matting error: {ex.Message}";
        }
        finally
        {
            _mattingTargetImage = null;
            _mattingOriginalImageBytes = null;
            _mattingInitialMask = null;
            IsSelectAndMaskDialogOpen = false;
        }
    }

    [RelayCommand]
    public void InvertSelection()
    {
        // 1. If elements are selected, invert selected elements
        if (Elements.Count > 0)
        {
            var currentlySelected = SelectedElements.ToList();
            if (currentlySelected.Count == 0 && SelectedElement != null)
            {
                currentlySelected.Add(SelectedElement);
            }

            var unselected = Elements.Where(e => !currentlySelected.Contains(e)).ToList();
            SelectedElements.Clear();
            foreach (var el in unselected)
            {
                SelectedElements.Add(el);
            }
            SelectedElement = unselected.LastOrDefault();
            CanvasControl?.SetSelection(SelectedElements);
            StatusMessage = $"Selection inverted ({SelectedElements.Count} layers selected).";
        }
    }

    // ── Color Range Workspace State ─────────────────────────────────────────
    [ObservableProperty] private bool _isColorRangeDialogOpen;
    [ObservableProperty] private string _colorRangeSelectMode = "SampledColors";
    [ObservableProperty] private double _colorRangeFuzziness = 40.0;
    [ObservableProperty] private bool _colorRangeInvert = false;

    public IReadOnlyList<string> AvailableColorRangeModes { get; } =
    [
        "SampledColors",
        "Reds",
        "Yellows",
        "Greens",
        "Cyans",
        "Blues",
        "Magentas",
        "Highlights",
        "Midtones",
        "Shadows"
    ];

    private ImageElement? _colorRangeTargetImage;
    private byte[]? _colorRangeOriginalImageBytes;

    [RelayCommand]
    public void OpenColorRangeDialog()
    {
        _colorRangeTargetImage = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (_colorRangeTargetImage == null)
        {
            using var rtb = RenderCanvasToBitmap(1.0);
            if (rtb != null)
            {
                using var ms = new MemoryStream();
                rtb.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                var bytes = ms.ToArray();
                var img = new ImageElement
                {
                    Name = "Background Composite",
                    X = 0,
                    Y = 0,
                    Width = _canvasWidth,
                    Height = _canvasHeight,
                    OriginalImageBytes = bytes,
                    Source = new Bitmap(new MemoryStream(bytes)),
                    NaturalPixelWidth = (int)_canvasWidth,
                    NaturalPixelHeight = (int)_canvasHeight,
                    ZIndex = 0
                };
                Elements.Insert(0, img);
                SelectedElement = img;
                _colorRangeTargetImage = img;
                _colorRangeFallbackComposite = img;
            }
        }

        if (_colorRangeTargetImage == null)
        {
            StatusMessage = "Select an image layer to open Color Range.";
            return;
        }

        _colorRangeOriginalImageBytes = _colorRangeTargetImage.OriginalImageBytes;
        if (_colorRangeOriginalImageBytes == null || _colorRangeOriginalImageBytes.Length == 0)
        {
            StatusMessage = "Target layer has no image data to select from.";
            return;
        }

        IsColorRangeDialogOpen = true;
        StatusMessage = "Color Range dialog open. Choose color range or sampled color.";
    }

    private ImageElement? _colorRangeFallbackComposite;

    [RelayCommand]
    public void CancelColorRangeDialog()
    {
        if (_colorRangeFallbackComposite != null)
        {
            Elements.Remove(_colorRangeFallbackComposite);
            _colorRangeFallbackComposite = null;
        }

        _colorRangeTargetImage = null;
        _colorRangeOriginalImageBytes = null;
        IsColorRangeDialogOpen = false;
        StatusMessage = "Ready";
    }

    [RelayCommand]
    public void ConfirmColorRangeDialog()
    {
        if (_colorRangeTargetImage == null || _colorRangeOriginalImageBytes == null)
        {
            IsColorRangeDialogOpen = false;
            return;
        }

        try
        {
            using var bmp = SKBitmap.Decode(_colorRangeOriginalImageBytes);
            if (bmp == null) return;

            byte[] mask = ColorRangeEngine.ComputeMask(
                bmp.Bytes,
                bmp.Width,
                bmp.Height,
                ColorRangeSelectMode,
                ColorRangeFuzziness,
                ColorRangeInvert,
                PrimaryColor);

            if (CanvasControl != null)
            {
                CanvasControl.QuickMaskBuffer = mask;
                CanvasControl.IsQuickMaskMode = true;
                CanvasControl.InvalidateVisual();
            }

            StatusMessage = $"Color Range selection generated ({ColorRangeSelectMode}, Fuzziness: {ColorRangeFuzziness:0}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Color Range error: {ex.Message}";
        }
        finally
        {
            _colorRangeTargetImage = null;
            _colorRangeOriginalImageBytes = null;
            IsColorRangeDialogOpen = false;
        }
    }

    // ── Select Subject & Remove Background ──────────────────────────────────

    /// <summary>
    /// Computes a high-fidelity subject/foreground selection mask using border color contrast,
    /// center saliency prior, and natural edge matting refinement.
    /// </summary>
    public static byte[] ComputeSubjectMask(SKBitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        if (w <= 0 || h <= 0) return [];

        byte[] bgra = bmp.Bytes;
        byte[] rawMask = new byte[w * h];

        // 1. Sample perimeter border pixels to estimate background color profile
        long sumB = 0, sumG = 0, sumR = 0;
        int borderSamples = 0;
        int step = Math.Max(1, Math.Min(w, h) / 64);

        for (int x = 0; x < w; x += step)
        {
            // Top border
            int idxTop = x * 4;
            sumB += bgra[idxTop]; sumG += bgra[idxTop + 1]; sumR += bgra[idxTop + 2];
            borderSamples++;

            // Bottom border
            int idxBot = ((h - 1) * w + x) * 4;
            sumB += bgra[idxBot]; sumG += bgra[idxBot + 1]; sumR += bgra[idxBot + 2];
            borderSamples++;
        }

        for (int y = 0; y < h; y += step)
        {
            // Left border
            int idxLeft = (y * w) * 4;
            sumB += bgra[idxLeft]; sumG += bgra[idxLeft + 1]; sumR += bgra[idxLeft + 2];
            borderSamples++;

            // Right border
            int idxRight = (y * w + (w - 1)) * 4;
            sumB += bgra[idxRight]; sumG += bgra[idxRight + 1]; sumR += bgra[idxRight + 2];
            borderSamples++;
        }

        double bgB = borderSamples > 0 ? (double)sumB / borderSamples : 255.0;
        double bgG = borderSamples > 0 ? (double)sumG / borderSamples : 255.0;
        double bgR = borderSamples > 0 ? (double)sumR / borderSamples : 255.0;

        // 2. Compute saliency score for each pixel
        double cx = w / 2.0;
        double cy = h / 2.0;
        double maxRadiusSq = (cx * cx) + (cy * cy);

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            double dy = (y - cy);
            for (int x = 0; x < w; x++)
            {
                int pIdx = (row + x) * 4;
                byte b = bgra[pIdx];
                byte g = bgra[pIdx + 1];
                byte r = bgra[pIdx + 2];
                byte a = bgra[pIdx + 3];

                if (a < 10)
                {
                    rawMask[row + x] = 0;
                    continue;
                }

                // Color difference from border background
                double db = b - bgB;
                double dg = g - bgG;
                double dr = r - bgR;
                double colorDiff = Math.Sqrt(dr * dr + dg * dg + db * db) / 441.67; // [0..1]

                // Spatial center prior
                double dx = (x - cx);
                double distSq = (dx * dx + dy * dy);
                double centerPrior = Math.Exp(-distSq / (maxRadiusSq * 0.65));

                // Combined foreground confidence score
                double score = colorDiff * 0.70 + centerPrior * 0.30;

                // Threshold to binary candidate
                rawMask[row + x] = (byte)(score >= 0.28 ? 255 : 0);
            }
        }

        // 3. Refine edges with MattingEngine (smart radius guided filtering and smoothing)
        return MattingEngine.RefineSelectionMask(
            rawMask,
            bgra,
            w,
            h,
            radius: 3.0,
            smartRadius: true,
            smooth: 1.0,
            feather: 0.5,
            contrast: 10.0,
            shiftEdge: 0.0);
    }

    [RelayCommand]
    public void SelectSubject()
    {
        try
        {
            ImageElement? target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
            byte[]? imageBytes = target?.OriginalImageBytes;

            // If no specific image layer selected, composite the canvas to select subject from entire composition
            if (imageBytes == null || imageBytes.Length == 0)
            {
                int docW = (int)Math.Max(1, CanvasWidth);
                int docH = (int)Math.Max(1, CanvasHeight);
                using var surface = SKSurface.Create(new SKImageInfo(docW, docH, SKColorType.Bgra8888, SKAlphaType.Premul));
                if (surface != null)
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SKColors.White);
                    foreach (var elem in Elements.Where(e => e.IsVisible).OrderBy(e => e.ZIndex))
                    {
                        if (elem is ImageElement img && img.OriginalImageBytes != null)
                        {
                            using var imgBmp = SKBitmap.Decode(img.OriginalImageBytes);
                            if (imgBmp != null)
                            {
                                var destRect = new SKRect((float)img.X, (float)img.Y, (float)(img.X + img.Width), (float)(img.Y + img.Height));
                                canvas.DrawBitmap(imgBmp, destRect);
                            }
                        }
                    }
                    using var snap = surface.Snapshot();
                    using var data = snap.Encode(SKEncodedImageFormat.Png, 90);
                    imageBytes = data.ToArray();
                }
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                StatusMessage = "No image data available to select subject.";
                return;
            }

            using var bmp = SKBitmap.Decode(imageBytes);
            if (bmp == null)
            {
                StatusMessage = "Failed to decode image for subject detection.";
                return;
            }

            byte[] mask = ComputeSubjectMask(bmp);

            if (CanvasControl != null)
            {
                CanvasControl.QuickMaskBuffer = mask;
                CanvasControl.IsQuickMaskMode = true;
                CanvasControl.InvalidateVisual();
            }

            StatusMessage = "Subject detected and selected.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Select Subject error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void RemoveBackground()
    {
        try
        {
            ImageElement? target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
            if (target == null || target.OriginalImageBytes == null || target.OriginalImageBytes.Length == 0)
            {
                StatusMessage = "Select an image layer to remove its background.";
                return;
            }

            byte[] oldBytes = target.OriginalImageBytes;
            using var bmp = SKBitmap.Decode(oldBytes);
            if (bmp == null)
            {
                StatusMessage = "Failed to decode target image for background removal.";
                return;
            }

            int w = bmp.Width;
            int h = bmp.Height;

            // Compute subject mask
            byte[] mask = ComputeSubjectMask(bmp);

            // Create cut-out bitmap by multiplying alpha with the subject mask
            byte[] bgra = bmp.Bytes;
            byte[] newBgra = new byte[bgra.Length];
            Array.Copy(bgra, newBgra, bgra.Length);

            for (int i = 0; i < mask.Length; i++)
            {
                int alphaIdx = i * 4 + 3;
                byte origA = newBgra[alphaIdx];
                byte maskVal = mask[i];
                newBgra[alphaIdx] = (byte)((origA * maskVal) / 255);
            }

            using var newBmp = new SKBitmap();
            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            var gcHandle = GCHandle.Alloc(newBgra, GCHandleType.Pinned);
            try
            {
                newBmp.InstallPixels(info, gcHandle.AddrOfPinnedObject(), info.RowBytes);
                using var image = SKImage.FromBitmap(newBmp);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                byte[] newPngBytes = data.ToArray();

                // Push undo
                History.Push(new RetouchImageCommand(target, oldBytes, newPngBytes, "Remove Background", (el, b) =>
                {
                    el.OriginalImageBytes = b;
                    el.Source = new Bitmap(new MemoryStream(b));
                    CanvasControl?.InvalidateVisual();
                }));

                // Apply to element
                target.OriginalImageBytes = newPngBytes;
                target.Source = new Bitmap(new MemoryStream(newPngBytes));
                CanvasControl?.InvalidateVisual();

                StatusMessage = "Background removed successfully.";
            }
            finally
            {
                gcHandle.Free();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Remove Background error: {ex.Message}";
        }
    }
}
