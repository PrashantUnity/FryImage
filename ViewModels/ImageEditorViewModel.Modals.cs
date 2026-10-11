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

    // Exposure parameters
    [ObservableProperty] private double _adjExposure;
    [ObservableProperty] private double _adjExposureOffset;
    [ObservableProperty] private double _adjExposureGamma = 1.0;

    // Selective Color parameters
    [ObservableProperty] private int _adjSelectiveColorRangeIndex; // 0..8 (Reds, Yellows, Greens, Cyans, Blues, Magentas, Whites, Neutrals, Blacks)
    [ObservableProperty] private double _adjSelectiveCyan;
    [ObservableProperty] private double _adjSelectiveMagenta;
    [ObservableProperty] private double _adjSelectiveYellow;
    [ObservableProperty] private double _adjSelectiveBlack;
    [ObservableProperty] private bool _adjSelectiveRelative = true;
    private double[,] _adjSelectiveAdjustments = new double[9, 4];

    // Color Lookup (3D LUT) parameters
    [ObservableProperty] private string _adjLutPreset = "TealAndOrange";
    [ObservableProperty] private bool _adjLutTetrahedral = true;
    [ObservableProperty] private string _adjCustomLutName = string.Empty;
    private float[]? _adjCustomLutData;
    private int _adjCustomLutSize;

    // Gradient Map parameters
    [ObservableProperty] private string _adjGradientPreset = "BlackWhite";
    [ObservableProperty] private bool _adjGradientReverse;

    // Photo Filter parameters
    [ObservableProperty] private string _adjPhotoFilterPreset = "Warming85";
    [ObservableProperty] private Color _adjPhotoFilterColor = Color.FromRgb(0xEC, 0x8A, 0x28);
    [ObservableProperty] private int _adjPhotoFilterDensity = 25;
    [ObservableProperty] private bool _adjPhotoFilterPreserveLuminosity = true;

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
            "Curves" => "Curves",
            "HueSaturation" => "Hue / Saturation",
            "Vibrance" => "Vibrance",
            "ColorBalance" => "Color Balance",
            "Threshold" => "Threshold",
            "Posterize" => "Posterize",
            "Exposure" => "Exposure",
            "SelectiveColor" => "Selective Color",
            "ColorLookup" => "Color Lookup (3D LUT)",
            "GradientMap" => "Gradient Map",
            "PhotoFilter" => "Photo Filter",
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

        // Reset advanced adjustments
        AdjExposure = 0.0;
        AdjExposureOffset = 0.0;
        AdjExposureGamma = 1.0;
        _adjSelectiveAdjustments = new double[9, 4];
        AdjSelectiveColorRangeIndex = 0;
        AdjSelectiveCyan = 0;
        AdjSelectiveMagenta = 0;
        AdjSelectiveYellow = 0;
        AdjSelectiveBlack = 0;
        AdjSelectiveRelative = true;
        AdjLutPreset = "TealAndOrange";
        AdjLutTetrahedral = true;
        AdjGradientPreset = "BlackWhite";
        AdjGradientReverse = false;
        AdjPhotoFilterPreset = "Warming85";
        AdjPhotoFilterColor = Color.FromRgb(0xEC, 0x8A, 0x28);
        AdjPhotoFilterDensity = 25;
        AdjPhotoFilterPreserveLuminosity = true;

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

    partial void OnAdjExposureChanged(double value) => ApplyAdjustmentPreview();
    partial void OnAdjExposureOffsetChanged(double value) => ApplyAdjustmentPreview();
    partial void OnAdjExposureGammaChanged(double value) => ApplyAdjustmentPreview();

    partial void OnAdjSelectiveColorRangeIndexChanged(int value)
    {
        if (value >= 0 && value < 9)
        {
            _adjSelectiveCyan = _adjSelectiveAdjustments[value, 0] * 100.0;
            _adjSelectiveMagenta = _adjSelectiveAdjustments[value, 1] * 100.0;
            _adjSelectiveYellow = _adjSelectiveAdjustments[value, 2] * 100.0;
            _adjSelectiveBlack = _adjSelectiveAdjustments[value, 3] * 100.0;
            OnPropertyChanged(nameof(AdjSelectiveCyan));
            OnPropertyChanged(nameof(AdjSelectiveMagenta));
            OnPropertyChanged(nameof(AdjSelectiveYellow));
            OnPropertyChanged(nameof(AdjSelectiveBlack));
        }
    }

    partial void OnAdjSelectiveCyanChanged(double value)
    {
        if (AdjSelectiveColorRangeIndex >= 0 && AdjSelectiveColorRangeIndex < 9)
            _adjSelectiveAdjustments[AdjSelectiveColorRangeIndex, 0] = value / 100.0;
        ApplyAdjustmentPreview();
    }

    partial void OnAdjSelectiveMagentaChanged(double value)
    {
        if (AdjSelectiveColorRangeIndex >= 0 && AdjSelectiveColorRangeIndex < 9)
            _adjSelectiveAdjustments[AdjSelectiveColorRangeIndex, 1] = value / 100.0;
        ApplyAdjustmentPreview();
    }

    partial void OnAdjSelectiveYellowChanged(double value)
    {
        if (AdjSelectiveColorRangeIndex >= 0 && AdjSelectiveColorRangeIndex < 9)
            _adjSelectiveAdjustments[AdjSelectiveColorRangeIndex, 2] = value / 100.0;
        ApplyAdjustmentPreview();
    }

    partial void OnAdjSelectiveBlackChanged(double value)
    {
        if (AdjSelectiveColorRangeIndex >= 0 && AdjSelectiveColorRangeIndex < 9)
            _adjSelectiveAdjustments[AdjSelectiveColorRangeIndex, 3] = value / 100.0;
        ApplyAdjustmentPreview();
    }

    partial void OnAdjSelectiveRelativeChanged(bool value) => ApplyAdjustmentPreview();

    partial void OnAdjLutPresetChanged(string value) => ApplyAdjustmentPreview();
    partial void OnAdjLutTetrahedralChanged(bool value) => ApplyAdjustmentPreview();

    partial void OnAdjGradientPresetChanged(string value) => ApplyAdjustmentPreview();
    partial void OnAdjGradientReverseChanged(bool value) => ApplyAdjustmentPreview();

    partial void OnAdjPhotoFilterPresetChanged(string value)
    {
        _adjPhotoFilterColor = value switch
        {
            "Warming85" => Color.FromRgb(0xEC, 0x8A, 0x28),
            "Cooling80" => Color.FromRgb(0x1C, 0x68, 0xD2),
            "Sepia" => Color.FromRgb(0xAE, 0x8B, 0x5E),
            "DeepRed" => Color.FromRgb(0xD4, 0x2B, 0x2B),
            "DeepBlue" => Color.FromRgb(0x1D, 0x3B, 0xB7),
            "DeepEmerald" => Color.FromRgb(0x15, 0x80, 0x3D),
            _ => _adjPhotoFilterColor
        };
        OnPropertyChanged(nameof(AdjPhotoFilterColor));
        ApplyAdjustmentPreview();
    }

    partial void OnAdjPhotoFilterColorChanged(Color value) => ApplyAdjustmentPreview();
    partial void OnAdjPhotoFilterDensityChanged(int value) => ApplyAdjustmentPreview();
    partial void OnAdjPhotoFilterPreserveLuminosityChanged(bool value) => ApplyAdjustmentPreview();

    [RelayCommand]
    public async Task LoadCustomLutFile()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage) return;

            var files = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select 3D LUT (.cube)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("3D LUT File (*.cube)") { Patterns = ["*.cube"] }
                ]
            });

            if (files.Count == 0) return;

            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            string text = await reader.ReadToEndAsync();

            var (size, data) = ImageAdjustmentEngine.Lut3DHelper.ParseCube(text);
            if (size > 0 && data.Length >= size * size * size * 3)
            {
                _adjCustomLutSize = size;
                _adjCustomLutData = data;
                AdjCustomLutName = file.Name;
                AdjLutPreset = "Custom";
                ApplyAdjustmentPreview();
                StatusMessage = $"Loaded 3D LUT {file.Name} ({size}×{size}×{size}).";
            }
            else
            {
                StatusMessage = "Failed to parse 3D LUT file.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading LUT: {ex.Message}";
        }
    }

    private byte[] ExecuteAdjustmentMode(byte[] bgra, int width, int height)
    {
        return ActiveAdjustmentMode switch
        {
            "BrightnessContrast" => ImageAdjustmentEngine.ApplyBrightnessContrast(bgra, AdjBrightness, AdjContrast),
            "Levels" => ImageAdjustmentEngine.ApplyLevels(bgra, AdjLevelsInBlack, AdjLevelsGamma, AdjLevelsInWhite, AdjLevelsOutBlack, AdjLevelsOutWhite),
            "Curves" => ExecuteCurvesAdjustment(bgra),
            "HueSaturation" => ImageAdjustmentEngine.ApplyHueSaturation(bgra, AdjHue, AdjSaturation, AdjLightness, AdjColorize),
            "Vibrance" => ImageAdjustmentEngine.ApplyVibrance(bgra, AdjVibrance, AdjSaturation),
            "ColorBalance" => ImageAdjustmentEngine.ApplyColorBalance(bgra, AdjCyanRed, AdjMagentaGreen, AdjYellowBlue),
            "Threshold" => ImageAdjustmentEngine.ApplyThreshold(bgra, AdjThreshold),
            "Posterize" => ImageAdjustmentEngine.ApplyPosterize(bgra, AdjPosterizeLevels),
            "Exposure" => ImageAdjustmentEngine.ApplyExposure(bgra, AdjExposure, AdjExposureOffset, AdjExposureGamma),
            "SelectiveColor" => ImageAdjustmentEngine.ApplySelectiveColor(bgra, AdjSelectiveRelative, _adjSelectiveAdjustments),
            "ColorLookup" => ExecuteColorLookup(bgra),
            "GradientMap" => ExecuteGradientMap(bgra),
            "PhotoFilter" => ImageAdjustmentEngine.ApplyPhotoFilter(bgra, AdjPhotoFilterColor, AdjPhotoFilterDensity, AdjPhotoFilterPreserveLuminosity),
            _ => bgra
        };
    }

    private byte[] ExecuteColorLookup(byte[] bgra)
    {
        if (AdjLutPreset == "Custom" && _adjCustomLutData != null && _adjCustomLutSize > 0)
        {
            return ImageAdjustmentEngine.ApplyColorLookup(bgra, _adjCustomLutSize, _adjCustomLutData, AdjLutTetrahedral);
        }

        var (size, data) = ImageAdjustmentEngine.Lut3DHelper.CreatePresetLut(AdjLutPreset, 17);
        return ImageAdjustmentEngine.ApplyColorLookup(bgra, size, data, AdjLutTetrahedral);
    }

    private byte[] ExecuteGradientMap(byte[] bgra)
    {
        Color[] colors;
        float[] stops;

        switch (AdjGradientPreset)
        {
            case "WarmSunset":
                colors = [Color.FromRgb(0x5E, 0x12, 0x24), Color.FromRgb(0xF9, 0x73, 0x16), Color.FromRgb(0xFE, 0xF0, 0x8A)];
                stops = [0.0f, 0.5f, 1.0f];
                break;
            case "OceanWaves":
                colors = [Color.FromRgb(0x0B, 0x19, 0x2C), Color.FromRgb(0x06, 0xB6, 0xD4), Color.FromRgb(0xE0, 0xF2, 0xFE)];
                stops = [0.0f, 0.5f, 1.0f];
                break;
            case "VioletAurora":
                colors = [Color.FromRgb(0x2E, 0x08, 0x54), Color.FromRgb(0xEC, 0x48, 0x99), Color.FromRgb(0xFD, 0xBA, 0x74)];
                stops = [0.0f, 0.5f, 1.0f];
                break;
            case "SepiaVintage":
                colors = [Color.FromRgb(0x2B, 0x18, 0x10), Color.FromRgb(0xA3, 0x70, 0x4C), Color.FromRgb(0xFB, 0xF5, 0xE6)];
                stops = [0.0f, 0.5f, 1.0f];
                break;
            case "Cyberpunk":
                colors = [Color.FromRgb(0x0A, 0x0A, 0x1A), Color.FromRgb(0x00, 0xFF, 0xFF), Color.FromRgb(0xFF, 0x00, 0x7F)];
                stops = [0.0f, 0.5f, 1.0f];
                break;
            case "BlackWhite":
            default:
                colors = [Colors.Black, Colors.White];
                stops = [0.0f, 1.0f];
                break;
        }

        if (AdjGradientReverse)
        {
            colors = colors.Reverse().ToArray();
        }

        return ImageAdjustmentEngine.ApplyGradientMap(bgra, colors, stops);
    }

    private void ApplyAdjustmentPreview()
    {
        if (!IsAdjustmentDialogOpen || !AdjPreviewEnabled || _adjTargetImageElement == null || _adjOriginalImageBytes == null)
            return;

        try
        {
            using var srcBmp = SKBitmap.Decode(_adjOriginalImageBytes);
            if (srcBmp == null) return;

            byte[] bgra = srcBmp.Bytes;
            byte[] adjustedBgra = ExecuteAdjustmentMode(bgra, srcBmp.Width, srcBmp.Height);

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
        if (_adjTargetImageElement != null && _adjOriginalImageBytes != null)
        {
            var beforeBytes = _adjOriginalImageBytes;
            try
            {
                using var srcBmp = SKBitmap.Decode(beforeBytes);
                if (srcBmp != null)
                {
                    byte[] bgra = srcBmp.Bytes;
                    byte[] adjustedBgra = ExecuteAdjustmentMode(bgra, srcBmp.Width, srcBmp.Height);

                    using var adjustedBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    var pin = System.Runtime.InteropServices.GCHandle.Alloc(adjustedBgra, System.Runtime.InteropServices.GCHandleType.Pinned);
                    try
                    {
                        adjustedBmp.InstallPixels(adjustedBmp.Info, pin.AddrOfPinnedObject());
                        using var img = SKImage.FromBitmap(adjustedBmp);
                        using var enc = img.Encode(SKEncodedImageFormat.Png, 100);
                        byte[] afterBytes = enc.ToArray();
                        _adjTargetImageElement.OriginalImageBytes = afterBytes;
                        _adjTargetImageElement.AdjustedSource?.Dispose();
                        _adjTargetImageElement.AdjustedSource = null;
                        _adjTargetImageElement.Source = new Bitmap(new MemoryStream(afterBytes));

                        var el = _adjTargetImageElement;
                        var title = ActiveAdjustmentTitle;
                        var undoCmd = new RetouchImageCommand(el, beforeBytes, afterBytes, title, (targetEl, b) =>
                        {
                            targetEl.OriginalImageBytes = b;
                            targetEl.AdjustedSource?.Dispose();
                            targetEl.AdjustedSource = null;
                            targetEl.Source = new Bitmap(new MemoryStream(b));
                            RefreshCanvas();
                        });
                        if (!_isApplyingHistory) History.Push(undoCmd);
                        StatusMessage = $"Applied {ActiveAdjustmentTitle}.";
                        ScheduleHistogramUpdate();
                        RefreshCanvas();
                    }
                    finally
                    {
                        pin.Free();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfirmAdjustmentDialog] error: {ex}");
            }
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
    [ObservableProperty] private int _filterDustRadius = 2;
    [ObservableProperty] private int _filterDustThreshold = 0;
    [ObservableProperty] private int _filterMedianRadius = 3;
    [ObservableProperty] private bool _filterPreviewEnabled = true;

    // Additional Studio filter properties
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

    // Advanced Studio Optical & Creative Filters
    [ObservableProperty] private double _filterTiltShiftBlur = 25.0;
    [ObservableProperty] private double _filterTiltShiftAngle = 0.0;
    [ObservableProperty] private double _filterTiltShiftFocus = 0.15;
    [ObservableProperty] private double _filterTiltShiftTransition = 0.25;

    [ObservableProperty] private double _filterIrisBlur = 25.0;
    [ObservableProperty] private double _filterIrisRadiusX = 0.35;
    [ObservableProperty] private double _filterIrisRadiusY = 0.25;
    [ObservableProperty] private double _filterIrisAngle = 0.0;
    [ObservableProperty] private double _filterIrisFeather = 0.4;

    [ObservableProperty] private double _filterOilStylization = 4.0;
    [ObservableProperty] private double _filterOilCleanliness = 2.0;
    [ObservableProperty] private double _filterOilScale = 1.0;
    [ObservableProperty] private double _filterOilBristleDetail = 5.0;
    [ObservableProperty] private bool _filterOilLighting = true;
    [ObservableProperty] private double _filterOilAngle = 45.0;
    [ObservableProperty] private double _filterOilShine = 0.5;

    [ObservableProperty] private int _filterFacetRadius = 2;

    [ObservableProperty] private int _filterWaveGenerators = 5;
    [ObservableProperty] private double _filterWaveMinWavelength = 10.0;
    [ObservableProperty] private double _filterWaveMaxWavelength = 120.0;
    [ObservableProperty] private double _filterWaveMinAmplitude = 5.0;
    [ObservableProperty] private double _filterWaveMaxAmplitude = 35.0;
    [ObservableProperty] private int _filterWaveType = 0; // 0=Sine, 1=Triangle, 2=Square

    [ObservableProperty] private double _filterRippleAmount = 50.0;
    [ObservableProperty] private int _filterRippleSize = 1; // 0=Small, 1=Medium, 2=Large

    [ObservableProperty] private double _filterPinchAmount = 50.0;
    [ObservableProperty] private bool _filterPolarRectangularToPolar = true;

    [ObservableProperty] private double _filterHalftoneMaxRadius = 8.0;
    [ObservableProperty] private double _filterHalftoneAngle1 = 108.0;
    [ObservableProperty] private double _filterHalftoneAngle2 = 162.0;
    [ObservableProperty] private double _filterHalftoneAngle3 = 90.0;
    [ObservableProperty] private double _filterHalftoneAngle4 = 45.0;

    [ObservableProperty] private int _filterCrystallizeCellSize = 20;
    [ObservableProperty] private int _filterPointillizeCellSize = 10;

    [ObservableProperty] private double _filterRawExposure = 0.0;
    [ObservableProperty] private double _filterRawTemperature = 0.0;
    [ObservableProperty] private double _filterRawTint = 0.0;
    [ObservableProperty] private double _filterRawContrast = 0.0;
    [ObservableProperty] private double _filterRawHighlights = 0.0;
    [ObservableProperty] private double _filterRawShadows = 0.0;
    [ObservableProperty] private double _filterRawWhites = 0.0;
    [ObservableProperty] private double _filterRawBlacks = 0.0;
    [ObservableProperty] private double _filterRawClarity = 0.0;
    [ObservableProperty] private double _filterRawDehaze = 0.0;
    [ObservableProperty] private double _filterRawVibrance = 0.0;
    [ObservableProperty] private double _filterRawSaturation = 0.0;

    [ObservableProperty] private double _filterLensBrightness = 100.0;
    [ObservableProperty] private int _filterLensType = 0;
    [ObservableProperty] private int _filterSolarizeThreshold = 128;

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
            "TiltShift" => "Tilt-Shift",
            "IrisBlur" => "Iris Blur",
            "UnsharpMask" => "Unsharp Mask",
            "HighPass" => "High Pass",
            "AddNoise" => "Add Noise",
            "DustAndScratches" => "Dust & Scratches",
            "Median" => "Median",
            "Mosaic" => "Mosaic",
            "Twirl" => "Twirl",
            "Spherize" => "Spherize",
            "Pinch" => "Pinch",
            "Ripple" => "Ripple",
            "Wave" => "Wave",
            "PolarCoordinates" => "Polar Coordinates",
            "ColorHalftone" => "Color Halftone",
            "Crystallize" => "Crystallize",
            "Pointillize" => "Pointillize",
            "Facet" => "Facet",
            "OilPaint" => "Oil Paint",
            "CameraRaw" => "Camera Raw Filter",
            "LensFlare" => "Lens Flare",
            "Clouds" => "Clouds",
            "DifferenceClouds" => "Difference Clouds",
            "FindEdges" => "Find Edges",
            "Solarize" => "Solarize",
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
        FilterTiltShiftBlur = 25.0;
        FilterTiltShiftAngle = 0.0;
        FilterTiltShiftFocus = 0.15;
        FilterTiltShiftTransition = 0.25;
        FilterIrisBlur = 25.0;
        FilterIrisRadiusX = 0.35;
        FilterIrisRadiusY = 0.25;
        FilterIrisAngle = 0.0;
        FilterIrisFeather = 0.4;
        FilterOilStylization = 4.0;
        FilterOilCleanliness = 2.0;
        FilterOilScale = 1.0;
        FilterOilBristleDetail = 5.0;
        FilterOilLighting = true;
        FilterOilAngle = 45.0;
        FilterOilShine = 0.5;
        FilterFacetRadius = 2;
        FilterWaveGenerators = 5;
        FilterWaveMinWavelength = 10.0;
        FilterWaveMaxWavelength = 120.0;
        FilterWaveMinAmplitude = 5.0;
        FilterWaveMaxAmplitude = 35.0;
        FilterWaveType = 0;
        FilterRippleAmount = 50.0;
        FilterRippleSize = 1;
        FilterPinchAmount = 50.0;
        FilterPolarRectangularToPolar = true;
        FilterHalftoneMaxRadius = 8.0;
        FilterCrystallizeCellSize = 20;
        FilterPointillizeCellSize = 10;
        FilterRawExposure = 0.0;
        FilterRawTemperature = 0.0;
        FilterRawTint = 0.0;
        FilterRawContrast = 0.0;
        FilterRawHighlights = 0.0;
        FilterRawShadows = 0.0;
        FilterRawWhites = 0.0;
        FilterRawBlacks = 0.0;
        FilterRawClarity = 0.0;
        FilterRawDehaze = 0.0;
        FilterRawVibrance = 0.0;
        FilterRawSaturation = 0.0;
        FilterLensBrightness = 100.0;
        FilterLensType = 0;
        FilterSolarizeThreshold = 128;
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
    partial void OnFilterTiltShiftBlurChanged(double value) => ApplyFilterPreview();
    partial void OnFilterTiltShiftAngleChanged(double value) => ApplyFilterPreview();
    partial void OnFilterTiltShiftFocusChanged(double value) => ApplyFilterPreview();
    partial void OnFilterTiltShiftTransitionChanged(double value) => ApplyFilterPreview();
    partial void OnFilterIrisBlurChanged(double value) => ApplyFilterPreview();
    partial void OnFilterIrisRadiusXChanged(double value) => ApplyFilterPreview();
    partial void OnFilterIrisRadiusYChanged(double value) => ApplyFilterPreview();
    partial void OnFilterIrisAngleChanged(double value) => ApplyFilterPreview();
    partial void OnFilterIrisFeatherChanged(double value) => ApplyFilterPreview();
    partial void OnFilterOilStylizationChanged(double value) => ApplyFilterPreview();
    partial void OnFilterOilCleanlinessChanged(double value) => ApplyFilterPreview();
    partial void OnFilterOilScaleChanged(double value) => ApplyFilterPreview();
    partial void OnFilterOilBristleDetailChanged(double value) => ApplyFilterPreview();
    partial void OnFilterOilLightingChanged(bool value) => ApplyFilterPreview();
    partial void OnFilterOilAngleChanged(double value) => ApplyFilterPreview();
    partial void OnFilterOilShineChanged(double value) => ApplyFilterPreview();
    partial void OnFilterFacetRadiusChanged(int value) => ApplyFilterPreview();
    partial void OnFilterWaveGeneratorsChanged(int value) => ApplyFilterPreview();
    partial void OnFilterWaveMinWavelengthChanged(double value) => ApplyFilterPreview();
    partial void OnFilterWaveMaxWavelengthChanged(double value) => ApplyFilterPreview();
    partial void OnFilterWaveMinAmplitudeChanged(double value) => ApplyFilterPreview();
    partial void OnFilterWaveMaxAmplitudeChanged(double value) => ApplyFilterPreview();
    partial void OnFilterWaveTypeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterRippleAmountChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRippleSizeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterPinchAmountChanged(double value) => ApplyFilterPreview();
    partial void OnFilterPolarRectangularToPolarChanged(bool value) => ApplyFilterPreview();
    partial void OnFilterHalftoneMaxRadiusChanged(double value) => ApplyFilterPreview();
    partial void OnFilterCrystallizeCellSizeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterPointillizeCellSizeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterRawExposureChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawTemperatureChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawTintChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawContrastChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawHighlightsChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawShadowsChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawWhitesChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawBlacksChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawClarityChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawDehazeChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawVibranceChanged(double value) => ApplyFilterPreview();
    partial void OnFilterRawSaturationChanged(double value) => ApplyFilterPreview();
    partial void OnFilterLensBrightnessChanged(double value) => ApplyFilterPreview();
    partial void OnFilterLensTypeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterSolarizeThresholdChanged(int value) => ApplyFilterPreview();
    partial void OnFilterDustRadiusChanged(int value) => ApplyFilterPreview();
    partial void OnFilterDustThresholdChanged(int value) => ApplyFilterPreview();
    partial void OnFilterMedianRadiusChanged(int value) => ApplyFilterPreview();

    private byte[] ExecuteFilterMode(byte[] bgra, int width, int height)
    {
        return ActiveFilterMode switch
        {
            "GaussianBlur" => ImageAdjustmentEngine.ApplyGaussianBlur(bgra, width, height, FilterBlurRadius),
            "MotionBlur" => ImageAdjustmentEngine.ApplyMotionBlur(bgra, width, height, FilterMotionAngle, FilterMotionDistance),
            "RadialBlur" => ImageAdjustmentEngine.ApplyRadialBlur(bgra, width, height, FilterRadialAmount, FilterRadialIsZoom),
            "TiltShift" => ImageAdjustmentEngine.ApplyTiltShift(bgra, width, height, FilterTiltShiftBlur, angleDegrees: FilterTiltShiftAngle, focus: FilterTiltShiftFocus, transition: FilterTiltShiftTransition),
            "IrisBlur" => ImageAdjustmentEngine.ApplyIrisBlur(bgra, width, height, FilterIrisBlur, radiusX: FilterIrisRadiusX, radiusY: FilterIrisRadiusY, angleDegrees: FilterIrisAngle, feather: FilterIrisFeather),
            "UnsharpMask" => ImageAdjustmentEngine.ApplySharpen(bgra, width, height, FilterSharpenAmount / 100.0, FilterSharpenRadius, FilterSharpenThreshold),
            "HighPass" => ImageAdjustmentEngine.ApplyHighPass(bgra, width, height, FilterHighPassRadius),
            "AddNoise" => ImageAdjustmentEngine.ApplyAddNoise(bgra, FilterNoiseAmount, FilterNoiseMonochromatic),
            "DustAndScratches" => ImageAdjustmentEngine.ApplyDustAndScratches(bgra, width, height, FilterDustRadius, FilterDustThreshold),
            "Median" => ImageAdjustmentEngine.ApplyMedian(bgra, width, height, FilterMedianRadius),
            "Mosaic" => ImageAdjustmentEngine.ApplyMosaic(bgra, width, height, FilterMosaicCellSize),
            "Twirl" => ImageAdjustmentEngine.ApplyTwirl(bgra, width, height, FilterTwirlAngle, FilterTwirlRadius),
            "Spherize" => ImageAdjustmentEngine.ApplySpherize(bgra, width, height, FilterSpherizeAmount),
            "Pinch" => ImageAdjustmentEngine.ApplyPinch(bgra, width, height, FilterPinchAmount),
            "Ripple" => ImageAdjustmentEngine.ApplyRipple(bgra, width, height, FilterRippleAmount, FilterRippleSize),
            "Wave" => ImageAdjustmentEngine.ApplyWave(bgra, width, height, FilterWaveGenerators, FilterWaveMinWavelength, FilterWaveMaxWavelength, FilterWaveMinAmplitude, FilterWaveMaxAmplitude, FilterWaveType),
            "PolarCoordinates" => ImageAdjustmentEngine.ApplyPolarCoordinates(bgra, width, height, FilterPolarRectangularToPolar),
            "ColorHalftone" => ImageAdjustmentEngine.ApplyColorHalftone(bgra, width, height, FilterHalftoneMaxRadius, FilterHalftoneAngle1, FilterHalftoneAngle2, FilterHalftoneAngle3, FilterHalftoneAngle4),
            "Crystallize" => ImageAdjustmentEngine.ApplyCrystallize(bgra, width, height, FilterCrystallizeCellSize),
            "Pointillize" => ImageAdjustmentEngine.ApplyPointillize(bgra, width, height, FilterPointillizeCellSize),
            "Facet" => ImageAdjustmentEngine.ApplyFacet(bgra, width, height, FilterFacetRadius),
            "OilPaint" => ImageAdjustmentEngine.ApplyOilPaint(bgra, width, height, FilterOilStylization, FilterOilCleanliness, FilterOilScale, FilterOilBristleDetail, FilterOilLighting, FilterOilAngle, FilterOilShine),
            "CameraRaw" => ImageAdjustmentEngine.ApplyCameraRaw(bgra, width, height, FilterRawExposure, FilterRawTemperature, FilterRawTint, FilterRawContrast, FilterRawHighlights, FilterRawShadows, FilterRawWhites, FilterRawBlacks, FilterRawClarity, FilterRawDehaze, FilterRawVibrance, FilterRawSaturation),
            "LensFlare" => ImageAdjustmentEngine.ApplyLensFlare(bgra, width, height, FilterLensBrightness, lensType: FilterLensType),
            "Clouds" => ImageAdjustmentEngine.ApplyClouds(bgra, width, height, PrimaryColor, SecondaryColor, false),
            "DifferenceClouds" => ImageAdjustmentEngine.ApplyClouds(bgra, width, height, PrimaryColor, SecondaryColor, true),
            "FindEdges" => ImageAdjustmentEngine.ApplyFindEdges(bgra, width, height),
            "Solarize" => ImageAdjustmentEngine.ApplySolarize(bgra, FilterSolarizeThreshold),
            "Emboss" => ImageAdjustmentEngine.ApplyEmboss(bgra, width, height, FilterEmbossAngle, FilterEmbossDepth),
            _ => bgra
        };
    }

    private void ApplyFilterPreview()
    {
        if (!IsFilterDialogOpen || !FilterPreviewEnabled || _filterTargetImageElement == null || _filterOriginalBytes == null)
            return;

        try
        {
            using var srcBmp = SKBitmap.Decode(_filterOriginalBytes);
            if (srcBmp == null) return;

            byte[] bgra = srcBmp.Bytes;
            byte[] filteredBgra = ExecuteFilterMode(bgra, srcBmp.Width, srcBmp.Height);

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
                    byte[] filteredBgra = ExecuteFilterMode(bgra, srcBmp.Width, srcBmp.Height);

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
                Title = "Open Images, PSD or FITS Documents",
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType("Supported Images, PSD & FITS")
                    {
                        Patterns = ["*.psd", "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.fits", "*.fit", "*.fts"]
                    },
                    new FilePickerFileType("Layered Document (*.psd)") { Patterns = ["*.psd"] },
                    new FilePickerFileType("Astronomical Image (*.fits, *.fit, *.fts)") { Patterns = ["*.fits", "*.fit", "*.fts"] },
                    new FilePickerFileType("All Files") { Patterns = ["*.*"] }
                ]
            });

            if (files.Count == 0) return;

            var orderedFiles = files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
            int openedCount = 0;

            foreach (var file in orderedFiles)
            {
                await using var stream = await file.OpenReadAsync();
                string ext = Path.GetExtension(file.Name).ToLowerInvariant();

                if (ext == ".psd")
                {
                    var psd = PsdCodec.Decode(stream);
                    if (Elements.Count == 0)
                    {
                        ClearCanvas();
                        CanvasWidth = Math.Max(200, psd.Width);
                        CanvasHeight = Math.Max(150, psd.Height);
                        DocumentTitle = file.Name;
                    }

                    int z = Elements.Count + 1;
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

                    openedCount++;
                }
                else if (ext is ".fits" or ".fit" or ".fts")
                {
                    var fits = FitsCodec.Decode(stream);
                    if (Elements.Count == 0)
                    {
                        ClearCanvas();
                        CanvasWidth = Math.Max(200, fits.Width);
                        CanvasHeight = Math.Max(150, fits.Height);
                        DocumentTitle = file.Name;
                    }

                    using var skBmp = new SKBitmap(fits.Width, fits.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    var pin = System.Runtime.InteropServices.GCHandle.Alloc(fits.BgraPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
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
                            Name = Path.GetFileNameWithoutExtension(file.Name),
                            X = (CanvasWidth - fits.Width) / 2.0,
                            Y = (CanvasHeight - fits.Height) / 2.0,
                            Width = fits.Width,
                            Height = fits.Height,
                            NaturalPixelWidth = fits.Width,
                            NaturalPixelHeight = fits.Height,
                            Source = avaloniaBmp,
                            OriginalImageBytes = pngBytes,
                            ZIndex = Elements.Count + 1
                        };

                        Elements.Add(el);
                        SelectedElement = el;
                    }
                    finally
                    {
                        pin.Free();
                    }

                    openedCount++;
                }
                else
                {
                    // Standard image file
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    byte[] bytes = ms.ToArray();

                    using var imgMs = new MemoryStream(bytes);
                    var bmp = new Bitmap(imgMs);

                    if (Elements.Count == 0)
                    {
                        CanvasWidth = Math.Max(200, bmp.PixelSize.Width);
                        CanvasHeight = Math.Max(150, bmp.PixelSize.Height);
                        DocumentTitle = file.Name;
                    }

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
                    openedCount++;
                }
            }

            FitToWindow();
            StatusMessage = orderedFiles.Count == 1
                ? $"Opened {orderedFiles[0].Name}"
                : $"Opened {openedCount} documents in alphabetical order.";
            ScheduleHistogramUpdate();
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
                Title = "Save Layered PSD Document",
                SuggestedFileName = $"{Path.GetFileNameWithoutExtension(DocumentTitle)}.psd",
                DefaultExtension = ".psd",
                FileTypeChoices =
                [
                    new FilePickerFileType("Layered Document (*.psd)") { Patterns = ["*.psd"] }
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
