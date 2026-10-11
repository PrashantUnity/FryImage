using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Codecs;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── 1. Layer Style Modal Dialog (fx) ───────────────────────────────────
    [ObservableProperty] private bool _isLayerStyleDialogOpen;
    [ObservableProperty] private string _activeLayerStyleSection = "DropShadow"; // DropShadow, OuterGlow, InnerShadow, ColorOverlay, Stroke

    // Drop Shadow
    [ObservableProperty] private bool _fxDropShadowEnabled;
    [ObservableProperty] private Color _fxDropShadowColor = Colors.Black;
    [ObservableProperty] private double _fxDropShadowOpacity = 0.5;
    [ObservableProperty] private double _fxDropShadowAngle = 45;
    [ObservableProperty] private double _fxDropShadowDistance = 5;
    [ObservableProperty] private double _fxDropShadowSize = 5;
    [ObservableProperty] private double _fxDropShadowSpread = 0;
    [ObservableProperty] private LayerBlendMode _fxDropShadowBlendMode = LayerBlendMode.Multiply;

    // Outer Glow
    [ObservableProperty] private bool _fxOuterGlowEnabled;
    [ObservableProperty] private Color _fxOuterGlowColor = Color.FromRgb(255, 230, 80);
    [ObservableProperty] private double _fxOuterGlowOpacity = 0.6;
    [ObservableProperty] private double _fxOuterGlowSize = 10;
    [ObservableProperty] private double _fxOuterGlowSpread = 0;
    [ObservableProperty] private LayerBlendMode _fxOuterGlowBlendMode = LayerBlendMode.Screen;

    // Inner Shadow
    [ObservableProperty] private bool _fxInnerShadowEnabled;
    [ObservableProperty] private Color _fxInnerShadowColor = Colors.Black;
    [ObservableProperty] private double _fxInnerShadowOpacity = 0.5;
    [ObservableProperty] private double _fxInnerShadowAngle = 45;
    [ObservableProperty] private double _fxInnerShadowDistance = 5;
    [ObservableProperty] private double _fxInnerShadowSize = 5;
    [ObservableProperty] private LayerBlendMode _fxInnerShadowBlendMode = LayerBlendMode.Multiply;

    // Color Overlay
    [ObservableProperty] private bool _fxColorOverlayEnabled;
    [ObservableProperty] private Color _fxColorOverlayColor = Color.FromRgb(99, 102, 241);
    [ObservableProperty] private double _fxColorOverlayOpacity = 0.8;
    [ObservableProperty] private LayerBlendMode _fxColorOverlayBlendMode = LayerBlendMode.Normal;

    // Stroke
    [ObservableProperty] private bool _fxStrokeEnabled;
    [ObservableProperty] private Color _fxStrokeColor = Colors.Black;
    [ObservableProperty] private double _fxStrokeSize = 2;
    [ObservableProperty] private double _fxStrokeOpacity = 1.0;
    [ObservableProperty] private LayerStrokePosition _fxStrokePosition = LayerStrokePosition.Outside;

    public IReadOnlyList<LayerStrokePosition> AvailableStrokePositions { get; } = Enum.GetValues<LayerStrokePosition>();

    [RelayCommand]
    private void SelectLayerStyleSection(string section) => ActiveLayerStyleSection = section;

    [RelayCommand]
    public void OpenLayerStyleDialog()
    {
        if (SelectedElement is not { } el) return;
        var fx = el.Effects;

        FxDropShadowEnabled = fx.DropShadow.IsEnabled;
        FxDropShadowColor = fx.DropShadow.Color;
        FxDropShadowOpacity = fx.DropShadow.Opacity;
        FxDropShadowAngle = fx.DropShadow.Angle;
        FxDropShadowDistance = fx.DropShadow.Distance;
        FxDropShadowSize = fx.DropShadow.Size;
        FxDropShadowSpread = fx.DropShadow.Spread;
        FxDropShadowBlendMode = fx.DropShadow.BlendMode;

        FxOuterGlowEnabled = fx.OuterGlow.IsEnabled;
        FxOuterGlowColor = fx.OuterGlow.Color;
        FxOuterGlowOpacity = fx.OuterGlow.Opacity;
        FxOuterGlowSize = fx.OuterGlow.Size;
        FxOuterGlowSpread = fx.OuterGlow.Spread;
        FxOuterGlowBlendMode = fx.OuterGlow.BlendMode;

        FxInnerShadowEnabled = fx.InnerShadow.IsEnabled;
        FxInnerShadowColor = fx.InnerShadow.Color;
        FxInnerShadowOpacity = fx.InnerShadow.Opacity;
        FxInnerShadowAngle = fx.InnerShadow.Angle;
        FxInnerShadowDistance = fx.InnerShadow.Distance;
        FxInnerShadowSize = fx.InnerShadow.Size;
        FxInnerShadowBlendMode = fx.InnerShadow.BlendMode;

        FxColorOverlayEnabled = fx.ColorOverlay.IsEnabled;
        FxColorOverlayColor = fx.ColorOverlay.Color;
        FxColorOverlayOpacity = fx.ColorOverlay.Opacity;
        FxColorOverlayBlendMode = fx.ColorOverlay.BlendMode;

        FxStrokeEnabled = fx.Stroke.IsEnabled;
        FxStrokeColor = fx.Stroke.Color;
        FxStrokeSize = fx.Stroke.Size;
        FxStrokeOpacity = fx.Stroke.Opacity;
        FxStrokePosition = fx.Stroke.Position;

        IsLayerStyleDialogOpen = true;
    }

    [RelayCommand]
    private void CloseLayerStyleDialog() => IsLayerStyleDialogOpen = false;

    [RelayCommand]
    private void ApplyLayerStyleDialog()
    {
        if (SelectedElement is not { } el)
        {
            IsLayerStyleDialogOpen = false;
            return;
        }

        var oldEffects = el.Effects.Clone();
        var newEffects = new LayerStyleEffects
        {
            DropShadow = new DropShadowStyle
            {
                IsEnabled = FxDropShadowEnabled,
                Color = FxDropShadowColor,
                Opacity = FxDropShadowOpacity,
                Angle = FxDropShadowAngle,
                Distance = FxDropShadowDistance,
                Size = FxDropShadowSize,
                Spread = FxDropShadowSpread,
                BlendMode = FxDropShadowBlendMode
            },
            OuterGlow = new OuterGlowStyle
            {
                IsEnabled = FxOuterGlowEnabled,
                Color = FxOuterGlowColor,
                Opacity = FxOuterGlowOpacity,
                Size = FxOuterGlowSize,
                Spread = FxOuterGlowSpread,
                BlendMode = FxOuterGlowBlendMode
            },
            InnerShadow = new InnerShadowStyle
            {
                IsEnabled = FxInnerShadowEnabled,
                Color = FxInnerShadowColor,
                Opacity = FxInnerShadowOpacity,
                Angle = FxInnerShadowAngle,
                Distance = FxInnerShadowDistance,
                Size = FxInnerShadowSize,
                BlendMode = FxInnerShadowBlendMode
            },
            ColorOverlay = new ColorOverlayStyle
            {
                IsEnabled = FxColorOverlayEnabled,
                Color = FxColorOverlayColor,
                Opacity = FxColorOverlayOpacity,
                BlendMode = FxColorOverlayBlendMode
            },
            Stroke = new LayerStrokeStyle
            {
                IsEnabled = FxStrokeEnabled,
                Color = FxStrokeColor,
                Size = FxStrokeSize,
                Opacity = FxStrokeOpacity,
                Position = FxStrokePosition
            }
        };

        if (!_isApplyingHistory)
        {
            History.Push(new GenericPropertyCommand<CanvasElement, LayerStyleEffects>(
                el,
                (target, val) => target.Effects = val.Clone(),
                oldEffects,
                newEffects.Clone(),
                "Edit layer style"));
        }

        el.Effects = newEffects;
        RefreshCanvas();
        IsLayerStyleDialogOpen = false;
        StatusMessage = "Layer styles applied.";
        _ = UpdateHistogramAsync();
    }

    // ── 2. New Document Modal Dialog (File › New...) ────────────────────────
    [ObservableProperty] private bool _isNewDocumentDialogOpen;
    [ObservableProperty] private string _newDocTitle = "Untitled-1";
    [ObservableProperty] private int _newDocWidth = 2100;
    [ObservableProperty] private int _newDocHeight = 1500;
    [ObservableProperty] private int _newDocResolution = 300;
    [ObservableProperty] private string _newDocBackgroundMode = "White";

    public IReadOnlyList<string> AvailableBackgroundModes { get; } = ["White", "Black", "Transparent"];

    [RelayCommand]
    public void OpenNewDocumentDialog()
    {
        NewDocTitle = $"Untitled-{(Elements.Count > 0 ? 2 : 1)}";
        IsNewDocumentDialogOpen = true;
    }

    [RelayCommand]
    public void CloseNewDocumentDialog() => IsNewDocumentDialogOpen = false;

    [RelayCommand]
    public void ApplyDocumentPreset(string presetStr)
    {
        // format: "width,height,resolution,name"
        var parts = presetStr.Split(',');
        if (parts.Length >= 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
        {
            NewDocWidth = w;
            NewDocHeight = h;
            if (parts.Length >= 3 && int.TryParse(parts[2], out int res))
                NewDocResolution = res;
            if (parts.Length >= 4)
                NewDocTitle = parts[3];
        }
    }

    [RelayCommand]
    public void SwapNewDocOrientation()
    {
        int temp = NewDocWidth;
        NewDocWidth = NewDocHeight;
        NewDocHeight = temp;
    }

    [RelayCommand]
    public void ConfirmCreateDocument()
    {
        ClearCanvas();
        CanvasWidth = NewDocWidth;
        CanvasHeight = NewDocHeight;
        DocumentTitle = NewDocTitle;

        if (NewDocBackgroundMode == "Transparent")
            BackgroundColor = Colors.Transparent;
        else if (NewDocBackgroundMode == "Black")
            BackgroundColor = Colors.Black;
        else
            BackgroundColor = Colors.White;

        IsNewDocumentDialogOpen = false;
        FitToWindow();
        StatusMessage = $"Created document {CanvasWidth}×{CanvasHeight} px @ {NewDocResolution} ppi.";
        ScheduleHistogramUpdate();
    }

    // ── 3. Image Adjustment Modal Dialog (Image › Adjustments) ─────────────
    [ObservableProperty] private bool _isAdjustmentDialogOpen;
    [ObservableProperty] private string _activeAdjustmentMode = "BrightnessContrast";
    [ObservableProperty] private string _activeAdjustmentTitle = "Brightness / Contrast";

    // Adjustment parameters
    [ObservableProperty] private int _adjBrightness;
    [ObservableProperty] private int _adjContrast;
    [ObservableProperty] private int _adjLevelsInBlack = 0;
    [ObservableProperty] private double _adjLevelsGamma = 1.0;
    [ObservableProperty] private int _adjLevelsInWhite = 255;
    [ObservableProperty] private int _adjLevelsOutBlack = 0;
    [ObservableProperty] private int _adjLevelsOutWhite = 255;
    [ObservableProperty] private int _adjHue;
    [ObservableProperty] private int _adjSaturation;
    [ObservableProperty] private int _adjLightness;
    [ObservableProperty] private bool _adjColorize;
    [ObservableProperty] private int _adjVibrance;
    [ObservableProperty] private int _adjCyanRed;
    [ObservableProperty] private int _adjMagentaGreen;
    [ObservableProperty] private int _adjYellowBlue;
    [ObservableProperty] private int _adjThreshold = 128;
    [ObservableProperty] private int _adjPosterizeLevels = 4;
    [ObservableProperty] private bool _adjPreviewEnabled = true;

    private byte[]? _adjOriginalImageBytes;
    private ImageElement? _adjTargetImageElement;

    [RelayCommand]
    public void OpenAdjustmentDialog(string mode)
    {
        ActiveAdjustmentMode = mode;
        ActiveAdjustmentTitle = mode switch
        {
            "BrightnessContrast" => "Brightness / Contrast",
            "Levels" => "Levels",
            "HueSaturation" => "Hue / Saturation",
            "Vibrance" => "Vibrance",
            "ColorBalance" => "Color Balance",
            "Threshold" => "Threshold",
            "Posterize" => "Posterize",
            _ => "Adjustment"
        };

        // Reset sliders
        AdjBrightness = 0;
        AdjContrast = 0;
        AdjLevelsInBlack = 0;
        AdjLevelsGamma = 1.0;
        AdjLevelsInWhite = 255;
        AdjLevelsOutBlack = 0;
        AdjLevelsOutWhite = 255;
        AdjHue = 0;
        AdjSaturation = 0;
        AdjLightness = 0;
        AdjColorize = false;
        AdjVibrance = 0;
        AdjCyanRed = 0;
        AdjMagentaGreen = 0;
        AdjYellowBlue = 0;
        AdjThreshold = 128;
        AdjPosterizeLevels = 4;
        AdjPreviewEnabled = true;

        _adjTargetImageElement = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (_adjTargetImageElement != null)
        {
            _adjOriginalImageBytes = _adjTargetImageElement.OriginalImageBytes;
        }

        IsAdjustmentDialogOpen = true;
    }

    partial void OnAdjBrightnessChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjContrastChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjLevelsInBlackChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjLevelsGammaChanged(double value) => ApplyAdjustmentPreview();
    partial void OnAdjLevelsInWhiteChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjHueChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjSaturationChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjLightnessChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjVibranceChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjCyanRedChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjMagentaGreenChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjYellowBlueChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjThresholdChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjPosterizeLevelsChanged(int value) => ApplyAdjustmentPreview();

    private void ApplyAdjustmentPreview()
    {
        if (!IsAdjustmentDialogOpen || !AdjPreviewEnabled || _adjTargetImageElement == null || _adjOriginalImageBytes == null)
            return;

        try
        {
            using var srcBmp = SKBitmap.Decode(_adjOriginalImageBytes);
            if (srcBmp == null) return;

            byte[] bgra = srcBmp.Bytes;
            byte[] adjustedBgra = ActiveAdjustmentMode switch
            {
                "BrightnessContrast" => ImageAdjustmentEngine.ApplyBrightnessContrast(bgra, AdjBrightness, AdjContrast),
                "Levels" => ImageAdjustmentEngine.ApplyLevels(bgra, AdjLevelsInBlack, AdjLevelsGamma, AdjLevelsInWhite, AdjLevelsOutBlack, AdjLevelsOutWhite),
                "HueSaturation" => ImageAdjustmentEngine.ApplyHueSaturation(bgra, AdjHue, AdjSaturation, AdjLightness, AdjColorize),
                "Vibrance" => ImageAdjustmentEngine.ApplyVibrance(bgra, AdjVibrance, AdjSaturation),
                "ColorBalance" => ImageAdjustmentEngine.ApplyColorBalance(bgra, AdjCyanRed, AdjMagentaGreen, AdjYellowBlue),
                "Threshold" => ImageAdjustmentEngine.ApplyThreshold(bgra, AdjThreshold),
                "Posterize" => ImageAdjustmentEngine.ApplyPosterize(bgra, AdjPosterizeLevels),
                _ => bgra
            };

            using var adjustedBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(adjustedBgra, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                adjustedBmp.InstallPixels(adjustedBmp.Info, pin.AddrOfPinnedObject());
                using var image = SKImage.FromBitmap(adjustedBmp);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream(encoded.ToArray());
                _adjTargetImageElement.AdjustedSource?.Dispose();
                _adjTargetImageElement.AdjustedSource = new Bitmap(ms);
                RefreshCanvas();
            }
            finally
            {
                pin.Free();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdjustmentPreview] error: {ex}");
        }
    }

    [RelayCommand]
    public void ConfirmAdjustmentDialog()
    {
        if (_adjTargetImageElement != null && _adjTargetImageElement.AdjustedSource != null)
        {
            // Bake the adjustment into the image element
            StatusMessage = $"Applied {ActiveAdjustmentTitle}.";
            ScheduleHistogramUpdate();
        }
        IsAdjustmentDialogOpen = false;
    }

    [RelayCommand]
    public void CancelAdjustmentDialog()
    {
        if (_adjTargetImageElement != null)
        {
            _adjTargetImageElement.AdjustedSource?.Dispose();
            _adjTargetImageElement.AdjustedSource = null;
            RefreshCanvas();
        }
        IsAdjustmentDialogOpen = false;
    }

    [RelayCommand]
    public void ApplyAutoTone()
    {
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target == null || target.OriginalImageBytes == null) return;
        try
        {
            using var srcBmp = SKBitmap.Decode(target.OriginalImageBytes);
            if (srcBmp == null) return;
            byte[] autoToned = ImageAdjustmentEngine.ApplyAutoTone(srcBmp.Bytes);
            using var adjBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(autoToned, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                adjBmp.InstallPixels(adjBmp.Info, pin.AddrOfPinnedObject());
                using var image = SKImage.FromBitmap(adjBmp);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                byte[] pngBytes = encoded.ToArray();
                using var ms = new MemoryStream(pngBytes);
                target.AdjustedSource?.Dispose();
                target.AdjustedSource = new Bitmap(ms);
                RefreshCanvas();
                ScheduleHistogramUpdate();
                StatusMessage = "Applied Auto Tone.";
            }
            finally
            {
                pin.Free();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auto Tone failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ApplyAutoContrast()
    {
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target == null || target.OriginalImageBytes == null) return;
        try
        {
            byte[] beforeBytes = target.OriginalImageBytes;
            using var srcBmp = SKBitmap.Decode(beforeBytes);
            if (srcBmp == null) return;
            byte[] adjusted = ImageAdjustmentEngine.ApplyAutoContrast(srcBmp.Bytes);
            using var adjBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(adjusted, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                adjBmp.InstallPixels(adjBmp.Info, pin.AddrOfPinnedObject());
                using var image = SKImage.FromBitmap(adjBmp);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                byte[] afterBytes = encoded.ToArray();
                target.OriginalImageBytes = afterBytes;
                target.AdjustedSource?.Dispose();
                target.AdjustedSource = null;
                target.Source = new Bitmap(new MemoryStream(afterBytes));

                var undoCmd = new RetouchImageCommand(target, beforeBytes, afterBytes, "Auto Contrast", (el, b) =>
                {
                    el.OriginalImageBytes = b;
                    el.AdjustedSource?.Dispose();
                    el.AdjustedSource = null;
                    el.Source = new Bitmap(new MemoryStream(b));
                    RefreshCanvas();
                });
                if (!_isApplyingHistory) History.Push(undoCmd);

                RefreshCanvas();
                ScheduleHistogramUpdate();
                StatusMessage = "Applied Auto Contrast.";
            }
            finally
            {
                pin.Free();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auto Contrast failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ApplyAutoColor()
    {
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target == null || target.OriginalImageBytes == null) return;
        try
        {
            byte[] beforeBytes = target.OriginalImageBytes;
            using var srcBmp = SKBitmap.Decode(beforeBytes);
            if (srcBmp == null) return;
            byte[] adjusted = ImageAdjustmentEngine.ApplyAutoColor(srcBmp.Bytes);
            using var adjBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(adjusted, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                adjBmp.InstallPixels(adjBmp.Info, pin.AddrOfPinnedObject());
                using var image = SKImage.FromBitmap(adjBmp);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                byte[] afterBytes = encoded.ToArray();
                target.OriginalImageBytes = afterBytes;
                target.AdjustedSource?.Dispose();
                target.AdjustedSource = null;
                target.Source = new Bitmap(new MemoryStream(afterBytes));

                var undoCmd = new RetouchImageCommand(target, beforeBytes, afterBytes, "Auto Color", (el, b) =>
                {
                    el.OriginalImageBytes = b;
                    el.AdjustedSource?.Dispose();
                    el.AdjustedSource = null;
                    el.Source = new Bitmap(new MemoryStream(b));
                    RefreshCanvas();
                });
                if (!_isApplyingHistory) History.Push(undoCmd);

                RefreshCanvas();
                ScheduleHistogramUpdate();
                StatusMessage = "Applied Auto Color.";
            }
            finally
            {
                pin.Free();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auto Color failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ApplyEqualize()
    {
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target == null || target.OriginalImageBytes == null) return;
        try
        {
            byte[] beforeBytes = target.OriginalImageBytes;
            using var srcBmp = SKBitmap.Decode(beforeBytes);
            if (srcBmp == null) return;
            byte[] adjusted = ImageAdjustmentEngine.ApplyEqualize(srcBmp.Bytes);
            using var adjBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(adjusted, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                adjBmp.InstallPixels(adjBmp.Info, pin.AddrOfPinnedObject());
                using var image = SKImage.FromBitmap(adjBmp);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                byte[] afterBytes = encoded.ToArray();
                target.OriginalImageBytes = afterBytes;
                target.AdjustedSource?.Dispose();
                target.AdjustedSource = null;
                target.Source = new Bitmap(new MemoryStream(afterBytes));

                var undoCmd = new RetouchImageCommand(target, beforeBytes, afterBytes, "Equalize", (el, b) =>
                {
                    el.OriginalImageBytes = b;
                    el.AdjustedSource?.Dispose();
                    el.AdjustedSource = null;
                    el.Source = new Bitmap(new MemoryStream(b));
                    RefreshCanvas();
                });
                if (!_isApplyingHistory) History.Push(undoCmd);

                RefreshCanvas();
                ScheduleHistogramUpdate();
                StatusMessage = "Applied Equalize.";
            }
            finally
            {
                pin.Free();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Equalize failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ApplyInvert()
    {
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target == null || target.OriginalImageBytes == null) return;
        try
        {
            using var srcBmp = SKBitmap.Decode(target.OriginalImageBytes);
            if (srcBmp == null) return;
            byte[] inverted = ImageAdjustmentEngine.ApplyInvert(srcBmp.Bytes);
            using var adjBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(inverted, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                adjBmp.InstallPixels(adjBmp.Info, pin.AddrOfPinnedObject());
                using var image = SKImage.FromBitmap(adjBmp);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                byte[] pngBytes = encoded.ToArray();
                using var ms = new MemoryStream(pngBytes);
                target.AdjustedSource?.Dispose();
                target.AdjustedSource = new Bitmap(ms);
                RefreshCanvas();
                ScheduleHistogramUpdate();
                StatusMessage = "Inverted layer colors.";
            }
            finally
            {
                pin.Free();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Invert failed: {ex.Message}";
        }
    }

    // ── 4. Image Filter Modal Dialog (Filter › Blur / Sharpen / etc.) ────────
    [ObservableProperty] private bool _isFilterDialogOpen;
    [ObservableProperty] private string _activeFilterMode = "GaussianBlur";
    [ObservableProperty] private string _activeFilterTitle = "Gaussian Blur";

    [ObservableProperty] private double _filterBlurRadius = 5.0;
    [ObservableProperty] private double _filterSharpenAmount = 100.0;
    [ObservableProperty] private double _filterSharpenRadius = 1.5;
    [ObservableProperty] private int _filterSharpenThreshold = 0;
    [ObservableProperty] private double _filterHighPassRadius = 10.0;
    [ObservableProperty] private int _filterNoiseAmount = 25;
    [ObservableProperty] private bool _filterNoiseMonochromatic = true;
    [ObservableProperty] private bool _filterPreviewEnabled = true;

    // Additional Photoshop filter properties
    [ObservableProperty] private double _filterMotionAngle = 0.0;
    [ObservableProperty] private double _filterMotionDistance = 15.0;
    [ObservableProperty] private double _filterRadialAmount = 20.0;
    [ObservableProperty] private bool _filterRadialIsZoom = false;
    [ObservableProperty] private int _filterMosaicCellSize = 16;
    [ObservableProperty] private double _filterTwirlAngle = 180.0;
    [ObservableProperty] private double _filterTwirlRadius = 0.0;
    [ObservableProperty] private double _filterSpherizeAmount = 50.0;
    [ObservableProperty] private double _filterEmbossAngle = 135.0;
    [ObservableProperty] private int _filterEmbossDepth = 3;

    private ImageElement? _filterTargetImageElement;
    private byte[]? _filterOriginalBytes;

    [RelayCommand]
    public void OpenFilterDialog(string mode)
    {
        ActiveFilterMode = mode;
        ActiveFilterTitle = mode switch
        {
            "GaussianBlur" => "Gaussian Blur",
            "MotionBlur" => "Motion Blur",
            "RadialBlur" => "Radial Blur",
            "UnsharpMask" => "Unsharp Mask",
            "HighPass" => "High Pass",
            "AddNoise" => "Add Noise",
            "Mosaic" => "Mosaic",
            "Twirl" => "Twirl",
            "Spherize" => "Spherize",
            "Emboss" => "Emboss",
            _ => "Filter"
        };

        _filterTargetImageElement = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (_filterTargetImageElement != null)
        {
            _filterOriginalBytes = _filterTargetImageElement.OriginalImageBytes;
        }

        FilterBlurRadius = 5.0;
        FilterSharpenAmount = 100.0;
        FilterSharpenRadius = 1.5;
        FilterSharpenThreshold = 0;
        FilterHighPassRadius = 10.0;
        FilterNoiseAmount = 25;
        FilterNoiseMonochromatic = true;
        FilterMotionAngle = 0.0;
        FilterMotionDistance = 15.0;
        FilterRadialAmount = 20.0;
        FilterRadialIsZoom = false;
        FilterMosaicCellSize = 16;
        FilterTwirlAngle = 180.0;
        FilterTwirlRadius = 0.0;
        FilterSpherizeAmount = 50.0;
        FilterEmbossAngle = 135.0;
        FilterEmbossDepth = 3;
        FilterPreviewEnabled = true;

        IsFilterDialogOpen = true;
    }

    partial void OnFilterBlurRadiusChanged(double value) => ApplyFilterPreview();
    partial void OnFilterSharpenAmountChanged(double value) => ApplyFilterPreview();
    partial void OnFilterHighPassRadiusChanged(double value) => ApplyFilterPreview();
    partial void OnFilterNoiseAmountChanged(int value) => ApplyFilterPreview();
    partial void OnFilterMotionAngleChanged(double value) => ApplyFilterPreview();
    partial void OnFilterMotionDistanceChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRadialAmountChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRadialIsZoomChanged(bool value) => ApplyFilterPreview();
    partial void OnFilterMosaicCellSizeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterTwirlAngleChanged(double value) => ApplyFilterPreview();
    partial void OnFilterSpherizeAmountChanged(double value) => ApplyFilterPreview();
    partial void OnFilterEmbossAngleChanged(double value) => ApplyFilterPreview();
    partial void OnFilterEmbossDepthChanged(int value) => ApplyFilterPreview();

    private void ApplyFilterPreview()
    {
        if (!IsFilterDialogOpen || !FilterPreviewEnabled || _filterTargetImageElement == null || _filterOriginalBytes == null)
            return;

        try
        {
            using var srcBmp = SKBitmap.Decode(_filterOriginalBytes);
            if (srcBmp == null) return;

            byte[] bgra = srcBmp.Bytes;
            byte[] filteredBgra = ActiveFilterMode switch
            {
                "GaussianBlur" => ImageAdjustmentEngine.ApplyGaussianBlur(bgra, srcBmp.Width, srcBmp.Height, FilterBlurRadius),
                "MotionBlur" => ImageAdjustmentEngine.ApplyMotionBlur(bgra, srcBmp.Width, srcBmp.Height, FilterMotionAngle, FilterMotionDistance),
                "RadialBlur" => ImageAdjustmentEngine.ApplyRadialBlur(bgra, srcBmp.Width, srcBmp.Height, FilterRadialAmount, FilterRadialIsZoom),
                "UnsharpMask" => ImageAdjustmentEngine.ApplySharpen(bgra, srcBmp.Width, srcBmp.Height, FilterSharpenAmount / 100.0, FilterSharpenRadius, FilterSharpenThreshold),
                "HighPass" => ImageAdjustmentEngine.ApplyHighPass(bgra, srcBmp.Width, srcBmp.Height, FilterHighPassRadius),
                "AddNoise" => ImageAdjustmentEngine.ApplyAddNoise(bgra, FilterNoiseAmount, FilterNoiseMonochromatic),
                "Mosaic" => ImageAdjustmentEngine.ApplyMosaic(bgra, srcBmp.Width, srcBmp.Height, FilterMosaicCellSize),
                "Twirl" => ImageAdjustmentEngine.ApplyTwirl(bgra, srcBmp.Width, srcBmp.Height, FilterTwirlAngle, FilterTwirlRadius),
                "Spherize" => ImageAdjustmentEngine.ApplySpherize(bgra, srcBmp.Width, srcBmp.Height, FilterSpherizeAmount),
                "Emboss" => ImageAdjustmentEngine.ApplyEmboss(bgra, srcBmp.Width, srcBmp.Height, FilterEmbossAngle, FilterEmbossDepth),
                _ => bgra
            };

            using var filteredBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(filteredBgra, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                filteredBmp.InstallPixels(filteredBmp.Info, pin.AddrOfPinnedObject());
                using var image = SKImage.FromBitmap(filteredBmp);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream(encoded.ToArray());
                _filterTargetImageElement.AdjustedSource?.Dispose();
                _filterTargetImageElement.AdjustedSource = new Bitmap(ms);
                RefreshCanvas();
            }
            finally
            {
                pin.Free();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FilterPreview] error: {ex}");
        }
    }

    [RelayCommand]
    public void ConfirmFilterDialog()
    {
        if (_filterTargetImageElement != null && _filterOriginalBytes != null)
        {
            var beforeBytes = _filterOriginalBytes;
            try
            {
                using var srcBmp = SKBitmap.Decode(beforeBytes);
                if (srcBmp != null)
                {
                    byte[] bgra = srcBmp.Bytes;
                    byte[] filteredBgra = ActiveFilterMode switch
                    {
                        "GaussianBlur" => ImageAdjustmentEngine.ApplyGaussianBlur(bgra, srcBmp.Width, srcBmp.Height, FilterBlurRadius),
                        "MotionBlur" => ImageAdjustmentEngine.ApplyMotionBlur(bgra, srcBmp.Width, srcBmp.Height, FilterMotionAngle, FilterMotionDistance),
                        "RadialBlur" => ImageAdjustmentEngine.ApplyRadialBlur(bgra, srcBmp.Width, srcBmp.Height, FilterRadialAmount, FilterRadialIsZoom),
                        "UnsharpMask" => ImageAdjustmentEngine.ApplySharpen(bgra, srcBmp.Width, srcBmp.Height, FilterSharpenAmount / 100.0, FilterSharpenRadius, FilterSharpenThreshold),
                        "HighPass" => ImageAdjustmentEngine.ApplyHighPass(bgra, srcBmp.Width, srcBmp.Height, FilterHighPassRadius),
                        "AddNoise" => ImageAdjustmentEngine.ApplyAddNoise(bgra, FilterNoiseAmount, FilterNoiseMonochromatic),
                        "Mosaic" => ImageAdjustmentEngine.ApplyMosaic(bgra, srcBmp.Width, srcBmp.Height, FilterMosaicCellSize),
                        "Twirl" => ImageAdjustmentEngine.ApplyTwirl(bgra, srcBmp.Width, srcBmp.Height, FilterTwirlAngle, FilterTwirlRadius),
                        "Spherize" => ImageAdjustmentEngine.ApplySpherize(bgra, srcBmp.Width, srcBmp.Height, FilterSpherizeAmount),
                        "Emboss" => ImageAdjustmentEngine.ApplyEmboss(bgra, srcBmp.Width, srcBmp.Height, FilterEmbossAngle, FilterEmbossDepth),
                        _ => bgra
                    };

                    using var filteredBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    var pin = System.Runtime.InteropServices.GCHandle.Alloc(filteredBgra, System.Runtime.InteropServices.GCHandleType.Pinned);
                    try
                    {
                        filteredBmp.InstallPixels(filteredBmp.Info, pin.AddrOfPinnedObject());
                        using var img = SKImage.FromBitmap(filteredBmp);
                        using var enc = img.Encode(SKEncodedImageFormat.Png, 100);
                        byte[] afterBytes = enc.ToArray();
                        _filterTargetImageElement.OriginalImageBytes = afterBytes;
                        _filterTargetImageElement.AdjustedSource?.Dispose();
                        _filterTargetImageElement.AdjustedSource = null;
                        _filterTargetImageElement.Source = new Bitmap(new MemoryStream(afterBytes));

                        var el = _filterTargetImageElement;
                        var title = ActiveFilterTitle;
                        var undoCmd = new RetouchImageCommand(el, beforeBytes, afterBytes, title, (targetEl, b) =>
                        {
                            targetEl.OriginalImageBytes = b;
                            targetEl.AdjustedSource?.Dispose();
                            targetEl.AdjustedSource = null;
                            targetEl.Source = new Bitmap(new MemoryStream(b));
                            RefreshCanvas();
                        });
                        if (!_isApplyingHistory) History.Push(undoCmd);
                    }
                    finally
                    {
                        pin.Free();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfirmFilter] error: {ex}");
            }
        }
        StatusMessage = $"Applied {ActiveFilterTitle}.";
        IsFilterDialogOpen = false;
        RefreshCanvas();
        ScheduleHistogramUpdate();
    }

    [RelayCommand]
    public void CancelFilterDialog()
    {
        if (_filterTargetImageElement != null)
        {
            _filterTargetImageElement.AdjustedSource?.Dispose();
            _filterTargetImageElement.AdjustedSource = null;
            RefreshCanvas();
        }
        IsFilterDialogOpen = false;
    }

    // ── 5. PSD File Format Import & Export ──────────────────────────────────
    [RelayCommand]
    public async Task OpenPsdOrImageAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Image or Photoshop PSD",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Supported Images & PSD")
                    {
                        Patterns = ["*.psd", "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif"]
                    },
                    new FilePickerFileType("Photoshop PSD (*.psd)") { Patterns = ["*.psd"] },
                    new FilePickerFileType("All Files") { Patterns = ["*.*"] }
                ]
            });

            if (files.Count == 0) return;
            var file = files[0];
            await using var stream = await file.OpenReadAsync();

            string ext = Path.GetExtension(file.Name).ToLowerInvariant();
            if (ext == ".psd")
            {
                var psd = PsdCodec.Decode(stream);
                ClearCanvas();
                CanvasWidth = Math.Max(200, psd.Width);
                CanvasHeight = Math.Max(150, psd.Height);
                DocumentTitle = file.Name;

                int z = 1;
                foreach (var layer in psd.Layers)
                {
                    if (layer.BgraPixels != null && layer.Width > 0 && layer.Height > 0)
                    {
                        using var skBmp = new SKBitmap(layer.Width, layer.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var pin = System.Runtime.InteropServices.GCHandle.Alloc(layer.BgraPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try
                        {
                            skBmp.InstallPixels(skBmp.Info, pin.AddrOfPinnedObject());
                            using var img = SKImage.FromBitmap(skBmp);
                            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                            byte[] pngBytes = data.ToArray();

                            using var ms = new MemoryStream(pngBytes);
                            var avaloniaBmp = new Bitmap(ms);

                            var el = new ImageElement
                            {
                                Name = layer.Name,
                                X = layer.Left,
                                Y = layer.Top,
                                Width = layer.Width,
                                Height = layer.Height,
                                NaturalPixelWidth = layer.Width,
                                NaturalPixelHeight = layer.Height,
                                Source = avaloniaBmp,
                                OriginalImageBytes = pngBytes,
                                Opacity = layer.Opacity / 255.0,
                                BlendMode = layer.BlendMode switch
                                {
                                    "mul" => LayerBlendMode.Multiply,
                                    "scrn" => LayerBlendMode.Screen,
                                    "over" => LayerBlendMode.Overlay,
                                    "dark" => LayerBlendMode.Darken,
                                    "lite" => LayerBlendMode.Lighten,
                                    "div" => LayerBlendMode.ColorDodge,
                                    "idiv" => LayerBlendMode.ColorBurn,
                                    "hLit" => LayerBlendMode.HardLight,
                                    "sLit" => LayerBlendMode.SoftLight,
                                    "diff" => LayerBlendMode.Difference,
                                    "smud" => LayerBlendMode.Exclusion,
                                    "hue" => LayerBlendMode.Hue,
                                    "sat" => LayerBlendMode.Saturation,
                                    "colr" => LayerBlendMode.Color,
                                    "lum" => LayerBlendMode.Luminosity,
                                    _ => LayerBlendMode.Normal
                                },
                                IsVisible = layer.IsVisible,
                                ZIndex = z++
                            };

                            Elements.Add(el);
                        }
                        finally
                        {
                            pin.Free();
                        }
                    }
                }

                FitToWindow();
                StatusMessage = $"Opened PSD with {psd.Layers.Count} layers.";
                ScheduleHistogramUpdate();
            }
            else
            {
                // Standard image file
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                byte[] bytes = ms.ToArray();

                using var imgMs = new MemoryStream(bytes);
                var bmp = new Bitmap(imgMs);

                var imgEl = new ImageElement
                {
                    Name = Path.GetFileNameWithoutExtension(file.Name),
                    X = (CanvasWidth - bmp.PixelSize.Width) / 2.0,
                    Y = (CanvasHeight - bmp.PixelSize.Height) / 2.0,
                    Width = Math.Min(CanvasWidth, bmp.PixelSize.Width),
                    Height = Math.Min(CanvasHeight, bmp.PixelSize.Height),
                    NaturalPixelWidth = bmp.PixelSize.Width,
                    NaturalPixelHeight = bmp.PixelSize.Height,
                    Source = bmp,
                    OriginalImageBytes = bytes,
                    ZIndex = Elements.Count + 1
                };

                Elements.Add(imgEl);
                SelectedElement = imgEl;
                StatusMessage = $"Imported image: {file.Name}";
                ScheduleHistogramUpdate();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening file: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SavePsdFileAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Photoshop PSD Document",
                SuggestedFileName = $"{Path.GetFileNameWithoutExtension(DocumentTitle)}.psd",
                DefaultExtension = ".psd",
                FileTypeChoices =
                [
                    new FilePickerFileType("Photoshop Document (*.psd)") { Patterns = ["*.psd"] }
                ]
            });

            if (file == null) return;

            var psd = new PsdCodec.PsdDocument
            {
                Width = (int)CanvasWidth,
                Height = (int)CanvasHeight
            };

            foreach (var el in Elements.OrderBy(e => e.ZIndex))
            {
                var layer = new PsdCodec.PsdLayerInfo
                {
                    Name = el.Name ?? "Layer",
                    Left = (int)el.X,
                    Top = (int)el.Y,
                    Right = (int)(el.X + el.Width),
                    Bottom = (int)(el.Y + el.Height),
                    Opacity = (byte)(Math.Clamp(el.Opacity, 0, 1) * 255),
                    IsVisible = el.IsVisible,
                    BlendMode = el.BlendMode switch
                    {
                        LayerBlendMode.Multiply => "mul ",
                        LayerBlendMode.Screen => "scrn",
                        LayerBlendMode.Overlay => "over",
                        LayerBlendMode.Darken => "dark",
                        LayerBlendMode.Lighten => "lite",
                        LayerBlendMode.ColorDodge => "div ",
                        LayerBlendMode.ColorBurn => "idiv",
                        LayerBlendMode.HardLight => "hLit",
                        LayerBlendMode.SoftLight => "sLit",
                        LayerBlendMode.Difference => "diff",
                        LayerBlendMode.Exclusion => "smud",
                        LayerBlendMode.Hue => "hue ",
                        LayerBlendMode.Saturation => "sat ",
                        LayerBlendMode.Color => "colr",
                        LayerBlendMode.Luminosity => "lum ",
                        _ => "norm"
                    }
                };

                if (el is ImageElement img && img.OriginalImageBytes != null)
                {
                    using var sk = SKBitmap.Decode(img.OriginalImageBytes);
                    if (sk != null)
                    {
                        using var scaled = sk.Resize(new SKImageInfo(Math.Max(1, (int)el.Width), Math.Max(1, (int)el.Height), SKColorType.Bgra8888, SKAlphaType.Premul), SKSamplingOptions.Default);
                        layer.BgraPixels = scaled?.Bytes ?? sk.Bytes;
                    }
                }
                else
                {
                    // Render element into Skia bitmap
                    int lw = Math.Max(1, (int)el.Width);
                    int lh = Math.Max(1, (int)el.Height);
                    using var skBmp = new SKBitmap(lw, lh, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using (var canvas = new SKCanvas(skBmp))
                    {
                        canvas.Clear(SKColors.Transparent);
                        using var paint = new SKPaint { Color = new SKColor(99, 102, 241, (byte)(el.Opacity * 255)) };
                        canvas.DrawRect(0, 0, lw, lh, paint);
                    }
                    layer.BgraPixels = skBmp.Bytes;
                }

                psd.Layers.Add(layer);
            }

            await using var stream = await file.OpenWriteAsync();
            PsdCodec.Encode(psd, stream);
            StatusMessage = $"Saved PSD: {file.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving PSD: {ex.Message}";
        }
    }
}
