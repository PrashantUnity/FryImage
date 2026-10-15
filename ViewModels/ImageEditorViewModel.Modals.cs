using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Codecs;
using PdfEditorApp.Plugins.ImageEditor.Core.Paint;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;
using PdfEditorApp.Plugins.ImageEditor.Core.Vector;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;
using PdfEditorApp.Plugins.ImageEditor.Core.Undo;
using PdfEditorApp.Plugins.ImageEditor.Core.Printing;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── 1. Layer Style Modal Dialog (fx) ───────────────────────────────────
    [ObservableProperty] private bool _isLayerStyleDialogOpen;
    [ObservableProperty] private string _activeLayerStyleSection = "BlendingOptions"; // BlendingOptions, DropShadow, OuterGlow, InnerShadow, ColorOverlay, Stroke

    // Blending Options & Blend If (#4395, #f57db2b5)
    [ObservableProperty] private double _generalOpacity = 100;
    [ObservableProperty] private double _fillOpacity = 100;
    [ObservableProperty] private string _blendIfChannel = "Gray";
    [ObservableProperty] private byte _blendIfThisBlackLow = 0;
    [ObservableProperty] private byte _blendIfThisBlackHigh = 0;
    [ObservableProperty] private byte _blendIfThisWhiteLow = 255;
    [ObservableProperty] private byte _blendIfThisWhiteHigh = 255;
    [ObservableProperty] private byte _blendIfUnderBlackLow = 0;
    [ObservableProperty] private byte _blendIfUnderBlackHigh = 0;
    [ObservableProperty] private byte _blendIfUnderWhiteLow = 255;
    [ObservableProperty] private byte _blendIfUnderWhiteHigh = 255;

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

    // Inner Glow
    [ObservableProperty] private bool _fxInnerGlowEnabled;
    [ObservableProperty] private Color _fxInnerGlowColor = Color.FromRgb(255, 255, 190);
    [ObservableProperty] private double _fxInnerGlowOpacity = 0.75;
    [ObservableProperty] private double _fxInnerGlowSize = 8.0;
    [ObservableProperty] private double _fxInnerGlowSpread = 0.0;
    [ObservableProperty] private LayerBlendMode _fxInnerGlowBlendMode = LayerBlendMode.Screen;

    [ObservableProperty] private bool _fxBevelEmbossEnabled;
    [ObservableProperty] private BevelEmbossType _fxBevelEmbossStyle = BevelEmbossType.InnerBevel;
    [ObservableProperty] private BevelTechnique _fxBevelEmbossTechnique = BevelTechnique.Smooth;
    [ObservableProperty] private double _fxBevelEmbossDepth = 100.0;
    [ObservableProperty] private double _fxBevelEmbossSize = 5.0;
    [ObservableProperty] private double _fxBevelEmbossSoften = 0.0;
    [ObservableProperty] private double _fxBevelEmbossAngle = 120.0;
    [ObservableProperty] private double _fxBevelEmbossAltitude = 30.0;
    [ObservableProperty] private Color _fxBevelHighlightColor = Colors.White;
    [ObservableProperty] private double _fxBevelHighlightOpacity = 0.75;
    [ObservableProperty] private LayerBlendMode _fxBevelHighlightMode = LayerBlendMode.Screen;
    [ObservableProperty] private Color _fxBevelShadowColor = Colors.Black;
    [ObservableProperty] private double _fxBevelShadowOpacity = 0.75;
    [ObservableProperty] private LayerBlendMode _fxBevelShadowMode = LayerBlendMode.Multiply;

    // Satin
    [ObservableProperty] private bool _fxSatinEnabled;
    [ObservableProperty] private Color _fxSatinColor = Colors.Black;
    [ObservableProperty] private double _fxSatinOpacity = 0.5;
    [ObservableProperty] private LayerBlendMode _fxSatinBlendMode = LayerBlendMode.Multiply;
    [ObservableProperty] private double _fxSatinAngle = 19.0;
    [ObservableProperty] private double _fxSatinDistance = 11.0;
    [ObservableProperty] private double _fxSatinSize = 14.0;
    [ObservableProperty] private bool _fxSatinInvert = true;

    // Gradient Overlay
    [ObservableProperty] private bool _fxGradientOverlayEnabled;
    [ObservableProperty] private LayerBlendMode _fxGradientOverlayBlendMode = LayerBlendMode.Normal;
    [ObservableProperty] private double _fxGradientOverlayOpacity = 1.0;
    [ObservableProperty] private Color _fxGradientOverlayStartColor = Colors.Black;
    [ObservableProperty] private Color _fxGradientOverlayEndColor = Colors.White;
    [ObservableProperty] private double _fxGradientOverlayAngle = 90.0;
    [ObservableProperty] private double _fxGradientOverlayScale = 100.0;
    [ObservableProperty] private bool _fxGradientOverlayReverse = false;

    // Pattern Overlay
    [ObservableProperty] private bool _fxPatternOverlayEnabled;
    [ObservableProperty] private LayerBlendMode _fxPatternOverlayBlendMode = LayerBlendMode.Normal;
    [ObservableProperty] private double _fxPatternOverlayOpacity = 1.0;
    [ObservableProperty] private double _fxPatternOverlayScale = 100.0;
    [ObservableProperty] private string _fxPatternOverlayName = "Grid";

    public IReadOnlyList<LayerStrokePosition> AvailableStrokePositions { get; } = Enum.GetValues<LayerStrokePosition>();
    public IReadOnlyList<BevelEmbossType> AvailableBevelTypes { get; } = Enum.GetValues<BevelEmbossType>();
    public IReadOnlyList<BevelTechnique> AvailableBevelTechniques { get; } = Enum.GetValues<BevelTechnique>();

    [RelayCommand]
    private void SelectLayerStyleSection(string section) => ActiveLayerStyleSection = section;

    [RelayCommand]
    public void OpenLayerStyleDialog()
    {
        var el = SelectedElement ?? Elements.LastOrDefault();
        if (el == null)
        {
            var dummy = new RectangleElement { Name = "Layer 1", Width = 200, Height = 200 };
            Elements.Add(dummy);
            SelectedElement = dummy;
            el = dummy;
        }
        else
        {
            SelectedElement ??= el;
        }
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

        FxInnerGlowEnabled = fx.InnerGlow.IsEnabled;
        FxInnerGlowColor = fx.InnerGlow.Color;
        FxInnerGlowOpacity = fx.InnerGlow.Opacity;
        FxInnerGlowSize = fx.InnerGlow.Size;
        FxInnerGlowSpread = fx.InnerGlow.Spread;
        FxInnerGlowBlendMode = fx.InnerGlow.BlendMode;

        FxBevelEmbossEnabled = fx.BevelEmboss.IsEnabled;
        FxBevelEmbossStyle = fx.BevelEmboss.Style;
        FxBevelEmbossTechnique = fx.BevelEmboss.Technique;
        FxBevelEmbossDepth = fx.BevelEmboss.Depth;
        FxBevelEmbossSize = fx.BevelEmboss.Size;
        FxBevelEmbossSoften = fx.BevelEmboss.Soften;
        FxBevelEmbossAngle = fx.BevelEmboss.Angle;
        FxBevelEmbossAltitude = fx.BevelEmboss.Altitude;
        FxBevelHighlightColor = fx.BevelEmboss.HighlightColor;
        FxBevelHighlightOpacity = fx.BevelEmboss.HighlightOpacity;
        FxBevelHighlightMode = fx.BevelEmboss.HighlightMode;
        FxBevelShadowColor = fx.BevelEmboss.ShadowColor;
        FxBevelShadowOpacity = fx.BevelEmboss.ShadowOpacity;
        FxBevelShadowMode = fx.BevelEmboss.ShadowMode;

        FxSatinEnabled = fx.Satin.IsEnabled;
        FxSatinColor = fx.Satin.Color;
        FxSatinOpacity = fx.Satin.Opacity;
        FxSatinBlendMode = fx.Satin.BlendMode;
        FxSatinAngle = fx.Satin.Angle;
        FxSatinDistance = fx.Satin.Distance;
        FxSatinSize = fx.Satin.Size;
        FxSatinInvert = fx.Satin.Invert;

        FxColorOverlayEnabled = fx.ColorOverlay.IsEnabled;
        FxColorOverlayColor = fx.ColorOverlay.Color;
        FxColorOverlayOpacity = fx.ColorOverlay.Opacity;
        FxColorOverlayBlendMode = fx.ColorOverlay.BlendMode;

        FxGradientOverlayEnabled = fx.GradientOverlay.IsEnabled;
        FxGradientOverlayBlendMode = fx.GradientOverlay.BlendMode;
        FxGradientOverlayOpacity = fx.GradientOverlay.Opacity;
        FxGradientOverlayStartColor = fx.GradientOverlay.StartColor;
        FxGradientOverlayEndColor = fx.GradientOverlay.EndColor;
        FxGradientOverlayAngle = fx.GradientOverlay.Angle;
        FxGradientOverlayScale = fx.GradientOverlay.Scale;
        FxGradientOverlayReverse = fx.GradientOverlay.Reverse;

        FxPatternOverlayEnabled = fx.PatternOverlay.IsEnabled;
        FxPatternOverlayBlendMode = fx.PatternOverlay.BlendMode;
        FxPatternOverlayOpacity = fx.PatternOverlay.Opacity;
        FxPatternOverlayScale = fx.PatternOverlay.Scale;
        FxPatternOverlayName = fx.PatternOverlay.PatternName;

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
        var el = SelectedElement ?? Elements.LastOrDefault();
        if (el == null)
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
            InnerGlow = new InnerGlowStyle
            {
                IsEnabled = FxInnerGlowEnabled,
                Color = FxInnerGlowColor,
                Opacity = FxInnerGlowOpacity,
                Size = FxInnerGlowSize,
                Spread = FxInnerGlowSpread,
                BlendMode = FxInnerGlowBlendMode
            },
            BevelEmboss = new BevelEmbossStyle
            {
                IsEnabled = FxBevelEmbossEnabled,
                Style = FxBevelEmbossStyle,
                Technique = FxBevelEmbossTechnique,
                Depth = FxBevelEmbossDepth,
                Size = FxBevelEmbossSize,
                Soften = FxBevelEmbossSoften,
                Angle = FxBevelEmbossAngle,
                Altitude = FxBevelEmbossAltitude,
                HighlightColor = FxBevelHighlightColor,
                HighlightOpacity = FxBevelHighlightOpacity,
                HighlightMode = FxBevelHighlightMode,
                ShadowColor = FxBevelShadowColor,
                ShadowOpacity = FxBevelShadowOpacity,
                ShadowMode = FxBevelShadowMode
            },
            Satin = new SatinStyle
            {
                IsEnabled = FxSatinEnabled,
                Color = FxSatinColor,
                Opacity = FxSatinOpacity,
                BlendMode = FxSatinBlendMode,
                Angle = FxSatinAngle,
                Distance = FxSatinDistance,
                Size = FxSatinSize,
                Invert = FxSatinInvert
            },
            ColorOverlay = new ColorOverlayStyle
            {
                IsEnabled = FxColorOverlayEnabled,
                Color = FxColorOverlayColor,
                Opacity = FxColorOverlayOpacity,
                BlendMode = FxColorOverlayBlendMode
            },
            GradientOverlay = new GradientOverlayStyle
            {
                IsEnabled = FxGradientOverlayEnabled,
                BlendMode = FxGradientOverlayBlendMode,
                Opacity = FxGradientOverlayOpacity,
                StartColor = FxGradientOverlayStartColor,
                EndColor = FxGradientOverlayEndColor,
                Angle = FxGradientOverlayAngle,
                Scale = FxGradientOverlayScale,
                Reverse = FxGradientOverlayReverse
            },
            PatternOverlay = new PatternOverlayStyle
            {
                IsEnabled = FxPatternOverlayEnabled,
                BlendMode = FxPatternOverlayBlendMode,
                Opacity = FxPatternOverlayOpacity,
                Scale = FxPatternOverlayScale,
                PatternName = FxPatternOverlayName
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

    /// <summary>
    /// Updates Drop Shadow or Inner Shadow angle and distance live from an on-canvas mouse drag (Studio parity #5701).
    /// </summary>
    public void UpdateLayerStyleShadowFromCanvasDrag(double dx, double dy)
    {
        if (!IsLayerStyleDialogOpen) return;

        double angle = FxDropShadowEnabled ? FxDropShadowAngle : FxInnerShadowAngle;
        double dist = FxDropShadowEnabled ? FxDropShadowDistance : FxInnerShadowDistance;

        double rad = angle * Math.PI / 180.0;
        double ox = -Math.Cos(rad) * dist + dx;
        double oy = Math.Sin(rad) * dist + dy;
        double newDist = Math.Sqrt(ox * ox + oy * oy);
        double newAngle = newDist >= 0.5 ? Math.Round(Math.Atan2(oy, -ox) * 180.0 / Math.PI) : angle;
        if (newAngle < 0) newAngle += 360.0;

        if (FxDropShadowEnabled)
        {
            FxDropShadowAngle = newAngle;
            FxDropShadowDistance = Math.Clamp(Math.Round(newDist), 0, 30000);
        }
        else if (FxInnerShadowEnabled)
        {
            FxInnerShadowAngle = newAngle;
            FxInnerShadowDistance = Math.Clamp(Math.Round(newDist), 0, 30000);
        }

        if (SelectedElement != null)
        {
            if (FxDropShadowEnabled)
            {
                SelectedElement.Effects.DropShadow.IsEnabled = true;
                SelectedElement.Effects.DropShadow.Angle = FxDropShadowAngle;
                SelectedElement.Effects.DropShadow.Distance = FxDropShadowDistance;
            }
            else if (FxInnerShadowEnabled)
            {
                SelectedElement.Effects.InnerShadow.IsEnabled = true;
                SelectedElement.Effects.InnerShadow.Angle = FxInnerShadowAngle;
                SelectedElement.Effects.InnerShadow.Distance = FxInnerShadowDistance;
            }
            CanvasControl?.InvalidateVisual();
        }
    }

    // ── 2. New Document Modal Dialog (File › New...) ────────────────────────
    [ObservableProperty] private bool _isNewDocumentDialogOpen;
    [ObservableProperty] private string _newDocTitle = "Untitled-1";
    [ObservableProperty] private decimal _newDocWidth = 2100;
    [ObservableProperty] private decimal _newDocHeight = 1500;
    [ObservableProperty] private decimal _newDocResolution = 300;
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
            NewDocOrientation = h >= w ? "Portrait" : "Landscape";
            if (parts.Length >= 3 && int.TryParse(parts[2], out int res))
                NewDocResolution = res;
            if (parts.Length >= 4)
                NewDocTitle = parts[3];
        }
    }

    [ObservableProperty] private string _newDocOrientation = "Landscape";
    [ObservableProperty] private string _newDocUnit = "Pixels";

    [RelayCommand]
    public void SetNewDocOrientation(string orientation)
    {
        NewDocOrientation = orientation;
        bool isPortrait = string.Equals(orientation, "Portrait", StringComparison.OrdinalIgnoreCase);
        if (isPortrait && NewDocWidth > NewDocHeight)
        {
            SwapNewDocOrientation();
        }
        else if (!isPortrait && NewDocHeight > NewDocWidth)
        {
            SwapNewDocOrientation();
        }
    }

    [RelayCommand]
    public void SwapNewDocOrientation()
    {
        decimal temp = NewDocWidth;
        NewDocWidth = NewDocHeight;
        NewDocHeight = temp;
        NewDocOrientation = NewDocHeight >= NewDocWidth ? "Portrait" : "Landscape";
    }

    [RelayCommand]
    public void ConfirmCreateDocument()
    {
        ClearCanvas();
        CanvasWidth = (double)NewDocWidth;
        CanvasHeight = (double)NewDocHeight;
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

    // ── 2B. New Brush Preset Modal Dialog (Studio Parity #4490) ───────────
    [ObservableProperty] private bool _isNewBrushPresetDialogOpen;
    [ObservableProperty] private string _newBrushPresetName = "Brush 1";
    [ObservableProperty] private bool _newBrushPresetCaptureSize = true;
    [ObservableProperty] private bool _newBrushPresetToolSettings = false;
    [ObservableProperty] private bool _newBrushPresetColor = false;
    [ObservableProperty] private string _newBrushPresetValidationMessage = "";

    public bool CanConfirmNewBrushPreset =>
        !string.IsNullOrWhiteSpace(NewBrushPresetName) &&
        !Core.Paint.BrushPresetLibrary.Presets.Any(p => string.Equals(p.Name, NewBrushPresetName.Trim(), StringComparison.OrdinalIgnoreCase));

    partial void OnNewBrushPresetNameChanged(string value)
    {
        ValidateNewBrushPresetName();
    }

    public void ValidateNewBrushPresetName()
    {
        string trimmed = NewBrushPresetName?.Trim() ?? "";
        if (string.IsNullOrEmpty(trimmed))
        {
            NewBrushPresetValidationMessage = "Enter a name for the brush preset.";
        }
        else if (Core.Paint.BrushPresetLibrary.Presets.Any(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            NewBrushPresetValidationMessage = "A brush preset with this name already exists.";
        }
        else
        {
            NewBrushPresetValidationMessage = "";
        }
        OnPropertyChanged(nameof(CanConfirmNewBrushPreset));
    }

    public static string GetSuggestedBrushPresetName(string? currentName = null)
    {
        var presets = Core.Paint.BrushPresetLibrary.Presets;
        if (!string.IsNullOrEmpty(currentName) && presets.Any(p => string.Equals(p.Name, currentName, StringComparison.OrdinalIgnoreCase)))
        {
            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{currentName} {i}";
                if (!presets.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                    return candidate;
            }
        }

        for (int i = 1; i < 1000; i++)
        {
            string candidate = $"Brush {i}";
            if (!presets.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        return "New Brush";
    }

    [RelayCommand]
    public void OpenNewBrushPresetDialog()
    {
        NewBrushPresetName = GetSuggestedBrushPresetName(SelectedBrushPreset?.Name);
        NewBrushPresetCaptureSize = true;
        NewBrushPresetToolSettings = false;
        NewBrushPresetColor = false;
        ValidateNewBrushPresetName();
        IsNewBrushPresetDialogOpen = true;
    }

    [RelayCommand]
    public void CloseNewBrushPresetDialog()
    {
        IsNewBrushPresetDialogOpen = false;
    }

    [RelayCommand]
    public void ConfirmNewBrushPreset()
    {
        ValidateNewBrushPresetName();
        if (!CanConfirmNewBrushPreset) return;

        string name = NewBrushPresetName.Trim();
        string id = "preset_" + Guid.NewGuid().ToString("N")[..8];
        var settings = GetCurrentBrushSettings();
        if (!NewBrushPresetCaptureSize)
        {
            settings.Size = 24.0;
        }

        var preset = new Core.Paint.BrushPreset(id, name, "General", "Brush", settings)
        {
            CaptureSize = NewBrushPresetCaptureSize,
            IncludeToolSettings = NewBrushPresetToolSettings,
            IncludeColor = NewBrushPresetToolSettings && NewBrushPresetColor,
            PresetColor = (NewBrushPresetToolSettings && NewBrushPresetColor) ? PrimaryColor : null
        };

        Core.Paint.BrushPresetLibrary.AddPreset(preset);
        SelectedBrushPreset = preset;
        StatusMessage = $"Saved brush preset '{name}'.";
        IsNewBrushPresetDialogOpen = false;
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
    [ObservableProperty] private int _adjColorBalanceTone = 1; // 0=Shadows, 1=Midtones, 2=Highlights
    [ObservableProperty] private bool _adjColorBalancePreserveLuminosity = true;
    private readonly double[][] _adjColorBalanceTones = [
        [0, 0, 0], // Shadows
        [0, 0, 0], // Midtones
        [0, 0, 0]  // Highlights
    ];
    private bool _isSwitchingTone = false;

    public bool IsColorBalanceToneShadows
    {
        get => AdjColorBalanceTone == 0;
        set { if (value) AdjColorBalanceTone = 0; }
    }

    public bool IsColorBalanceToneMidtones
    {
        get => AdjColorBalanceTone == 1;
        set { if (value) AdjColorBalanceTone = 1; }
    }

    public bool IsColorBalanceToneHighlights
    {
        get => AdjColorBalanceTone == 2;
        set { if (value) AdjColorBalanceTone = 2; }
    }

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
    [ObservableProperty] private bool _adjGradientDither = true;
    [ObservableProperty] private ObservableCollection<GradientMapRampStop> _adjGradientStops = new();
    [ObservableProperty] private int _adjGradientSelectedStopIndex = 0;

    public double AdjGradientSelectedStopLocationPercent
    {
        get => (AdjGradientStops.Count > 0 && AdjGradientSelectedStopIndex >= 0 && AdjGradientSelectedStopIndex < AdjGradientStops.Count)
            ? AdjGradientStops[AdjGradientSelectedStopIndex].Position * 100.0
            : 0.0;
        set
        {
            if (AdjGradientStops.Count > 0 && AdjGradientSelectedStopIndex >= 0 && AdjGradientSelectedStopIndex < AdjGradientStops.Count)
            {
                AdjGradientStops[AdjGradientSelectedStopIndex].Position = (float)Math.Clamp(value / 100.0, 0.0, 1.0);
                OnPropertyChanged(nameof(AdjGradientSelectedStopLocationPercent));
                ApplyAdjustmentPreview();
            }
        }
    }

    public IBrush AdjGradientSelectedStopColorBrush
    {
        get
        {
            if (AdjGradientStops.Count > 0 && AdjGradientSelectedStopIndex >= 0 && AdjGradientSelectedStopIndex < AdjGradientStops.Count)
                return new SolidColorBrush(AdjGradientStops[AdjGradientSelectedStopIndex].Color);
            return Brushes.Black;
        }
    }

    public bool CanDeleteGradientStop => AdjGradientStops.Count > 2;

    // Photo Filter parameters
    [ObservableProperty] private string _adjPhotoFilterPreset = "Warming85";
    [ObservableProperty] private Color _adjPhotoFilterColor = Color.FromRgb(0xEC, 0x8A, 0x28);
    [ObservableProperty] private int _adjPhotoFilterDensity = 25;
    [ObservableProperty] private bool _adjPhotoFilterPreserveLuminosity = true;

    // Shadows / Highlights parameters (Studio calibrated)
    [ObservableProperty] private double _adjShadowAmount = 35.0;
    [ObservableProperty] private double _adjShadowTone = 50.0;
    [ObservableProperty] private double _adjShadowRadius = 30.0;
    [ObservableProperty] private double _adjHighlightAmount = 0.0;
    [ObservableProperty] private double _adjHighlightTone = 50.0;
    [ObservableProperty] private double _adjHighlightRadius = 30.0;
    [ObservableProperty] private double _adjMidtone = 0.0;
    [ObservableProperty] private double _adjColor = 0.0;
    [ObservableProperty] private double _adjBlackClip = 0.01;
    [ObservableProperty] private double _adjWhiteClip = 0.01;

    // Black & White parameters
    [ObservableProperty] private int _adjBwR = 40;
    [ObservableProperty] private int _adjBwY = 60;
    [ObservableProperty] private int _adjBwG = 40;
    [ObservableProperty] private int _adjBwC = 60;
    [ObservableProperty] private int _adjBwB = 20;
    [ObservableProperty] private int _adjBwM = 80;
    [ObservableProperty] private bool _adjBwTintEnabled = false;
    [ObservableProperty] private Color _adjBwTintColor = Color.FromRgb(0xE1, 0xD3, 0xB3);

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
            "ShadowsHighlights" => "Shadows / Highlights",
            "BlackAndWhite" => "Black & White",
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
        AdjColorBalanceTone = 1;
        AdjColorBalancePreserveLuminosity = true;
        _adjColorBalanceTones[0][0] = 0; _adjColorBalanceTones[0][1] = 0; _adjColorBalanceTones[0][2] = 0;
        _adjColorBalanceTones[1][0] = 0; _adjColorBalanceTones[1][1] = 0; _adjColorBalanceTones[1][2] = 0;
        _adjColorBalanceTones[2][0] = 0; _adjColorBalanceTones[2][1] = 0; _adjColorBalanceTones[2][2] = 0;
        AdjCyanRed = 0;
        AdjMagentaGreen = 0;
        AdjYellowBlue = 0;
        OnPropertyChanged(nameof(IsColorBalanceToneShadows));
        OnPropertyChanged(nameof(IsColorBalanceToneMidtones));
        OnPropertyChanged(nameof(IsColorBalanceToneHighlights));
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
        AdjGradientDither = true;
        PopulateGradientStopsForPreset(AdjGradientPreset);
        AdjPhotoFilterPreset = "Warming85";
        AdjPhotoFilterColor = Color.FromRgb(0xEC, 0x8A, 0x28);
        AdjPhotoFilterDensity = 25;
        AdjPhotoFilterPreserveLuminosity = true;

        AdjPreviewEnabled = true;

        _adjTargetImageElement = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (_adjTargetImageElement == null && Elements.Count > 0)
        {
            using var rtb = RenderCanvasToBitmap(1.0);
            if (rtb != null)
            {
                using var ms = new MemoryStream();
                rtb.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                var bytes = ms.ToArray();
                var img = new ImageElement
                {
                    Name = "Composite Layer",
                    X = 0,
                    Y = 0,
                    Width = _canvasWidth,
                    Height = _canvasHeight,
                    OriginalImageBytes = bytes,
                    Source = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(bytes)),
                    NaturalPixelWidth = (int)_canvasWidth,
                    NaturalPixelHeight = (int)_canvasHeight,
                    ZIndex = Elements.Count
                };
                Elements.Add(img);
                SelectedElement = img;
                _adjTargetImageElement = img;
            }
        }
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
    partial void OnAdjColorBalanceToneChanged(int value)
    {
        if (value < 0 || value > 2) return;
        _isSwitchingTone = true;
        try
        {
            AdjCyanRed = (int)_adjColorBalanceTones[value][0];
            AdjMagentaGreen = (int)_adjColorBalanceTones[value][1];
            AdjYellowBlue = (int)_adjColorBalanceTones[value][2];
        }
        finally
        {
            _isSwitchingTone = false;
        }
        OnPropertyChanged(nameof(IsColorBalanceToneShadows));
        OnPropertyChanged(nameof(IsColorBalanceToneMidtones));
        OnPropertyChanged(nameof(IsColorBalanceToneHighlights));
    }

    partial void OnAdjCyanRedChanged(int value)
    {
        if (!_isSwitchingTone && AdjColorBalanceTone >= 0 && AdjColorBalanceTone <= 2)
            _adjColorBalanceTones[AdjColorBalanceTone][0] = value;
        ApplyAdjustmentPreview();
    }

    partial void OnAdjMagentaGreenChanged(int value)
    {
        if (!_isSwitchingTone && AdjColorBalanceTone >= 0 && AdjColorBalanceTone <= 2)
            _adjColorBalanceTones[AdjColorBalanceTone][1] = value;
        ApplyAdjustmentPreview();
    }

    partial void OnAdjYellowBlueChanged(int value)
    {
        if (!_isSwitchingTone && AdjColorBalanceTone >= 0 && AdjColorBalanceTone <= 2)
            _adjColorBalanceTones[AdjColorBalanceTone][2] = value;
        ApplyAdjustmentPreview();
    }

    partial void OnAdjColorBalancePreserveLuminosityChanged(bool value) => ApplyAdjustmentPreview();

    [RelayCommand]
    public void SetColorBalanceTone(int tone)
    {
        AdjColorBalanceTone = Math.Clamp(tone, 0, 2);
    }
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

    partial void OnAdjGradientPresetChanged(string value)
    {
        if (value != "Custom")
        {
            PopulateGradientStopsForPreset(value);
        }
        ApplyAdjustmentPreview();
    }

    partial void OnAdjGradientReverseChanged(bool value) => ApplyAdjustmentPreview();
    partial void OnAdjGradientDitherChanged(bool value) => ApplyAdjustmentPreview();

    partial void OnAdjGradientSelectedStopIndexChanged(int value)
    {
        OnPropertyChanged(nameof(AdjGradientSelectedStopLocationPercent));
        OnPropertyChanged(nameof(AdjGradientSelectedStopColorBrush));
        OnPropertyChanged(nameof(CanDeleteGradientStop));
    }

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
            "ColorBalance" => ImageAdjustmentEngine.ApplyColorBalanceStandard(bgra, _adjColorBalanceTones, AdjColorBalancePreserveLuminosity),
            "Threshold" => ImageAdjustmentEngine.ApplyThreshold(bgra, AdjThreshold),
            "Posterize" => ImageAdjustmentEngine.ApplyPosterize(bgra, AdjPosterizeLevels),
            "Exposure" => ImageAdjustmentEngine.ApplyExposure(bgra, AdjExposure, AdjExposureOffset, AdjExposureGamma),
            "SelectiveColor" => ImageAdjustmentEngine.ApplySelectiveColor(bgra, AdjSelectiveRelative, _adjSelectiveAdjustments),
            "ColorLookup" => ExecuteColorLookup(bgra),
            "GradientMap" => ExecuteGradientMap(bgra),
            "PhotoFilter" => ImageAdjustmentEngine.ApplyPhotoFilter(bgra, AdjPhotoFilterColor, AdjPhotoFilterDensity, AdjPhotoFilterPreserveLuminosity),
            "ShadowsHighlights" => Core.Raster.ShadowsHighlightsEngine.ApplyShadowsHighlights(bgra, width, height, AdjShadowAmount, AdjShadowTone, AdjShadowRadius, AdjHighlightAmount, AdjHighlightTone, AdjHighlightRadius, AdjMidtone, AdjColor, AdjBlackClip, AdjWhiteClip),
            "BlackAndWhite" => ImageAdjustmentEngine.ApplyBlackAndWhite(bgra, AdjBwR, AdjBwY, AdjBwG, AdjBwC, AdjBwB, AdjBwM, AdjBwTintEnabled ? AdjBwTintColor : null),
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

    public void PopulateGradientStopsForPreset(string preset)
    {
        AdjGradientStops.Clear();
        switch (preset)
        {
            case "WarmSunset":
                AdjGradientStops.Add(new GradientMapRampStop(0.0f, Color.FromRgb(0x5E, 0x12, 0x24)));
                AdjGradientStops.Add(new GradientMapRampStop(0.5f, Color.FromRgb(0xF9, 0x73, 0x16)));
                AdjGradientStops.Add(new GradientMapRampStop(1.0f, Color.FromRgb(0xFE, 0xF0, 0x8A)));
                break;
            case "OceanWaves":
                AdjGradientStops.Add(new GradientMapRampStop(0.0f, Color.FromRgb(0x0B, 0x19, 0x2C)));
                AdjGradientStops.Add(new GradientMapRampStop(0.5f, Color.FromRgb(0x06, 0xB6, 0xD4)));
                AdjGradientStops.Add(new GradientMapRampStop(1.0f, Color.FromRgb(0xE0, 0xF2, 0xFE)));
                break;
            case "VioletAurora":
                AdjGradientStops.Add(new GradientMapRampStop(0.0f, Color.FromRgb(0x2E, 0x08, 0x54)));
                AdjGradientStops.Add(new GradientMapRampStop(0.5f, Color.FromRgb(0xEC, 0x48, 0x99)));
                AdjGradientStops.Add(new GradientMapRampStop(1.0f, Color.FromRgb(0xFD, 0xBA, 0x74)));
                break;
            case "SepiaVintage":
                AdjGradientStops.Add(new GradientMapRampStop(0.0f, Color.FromRgb(0x2B, 0x18, 0x10)));
                AdjGradientStops.Add(new GradientMapRampStop(0.5f, Color.FromRgb(0xA3, 0x70, 0x4C)));
                AdjGradientStops.Add(new GradientMapRampStop(1.0f, Color.FromRgb(0xFB, 0xF5, 0xE6)));
                break;
            case "Cyberpunk":
                AdjGradientStops.Add(new GradientMapRampStop(0.0f, Color.FromRgb(0x0A, 0x0A, 0x1A)));
                AdjGradientStops.Add(new GradientMapRampStop(0.5f, Color.FromRgb(0x00, 0xFF, 0xFF)));
                AdjGradientStops.Add(new GradientMapRampStop(1.0f, Color.FromRgb(0xFF, 0x00, 0x7F)));
                break;
            case "Rainbow":
                AdjGradientStops.Add(new GradientMapRampStop(0.0f, Color.FromRgb(0xFF, 0x00, 0x00)));
                AdjGradientStops.Add(new GradientMapRampStop(0.17f, Color.FromRgb(0xFF, 0xA5, 0x00)));
                AdjGradientStops.Add(new GradientMapRampStop(0.33f, Color.FromRgb(0xFF, 0xFF, 0x00)));
                AdjGradientStops.Add(new GradientMapRampStop(0.50f, Color.FromRgb(0x00, 0x80, 0x00)));
                AdjGradientStops.Add(new GradientMapRampStop(0.67f, Color.FromRgb(0x00, 0x00, 0xFF)));
                AdjGradientStops.Add(new GradientMapRampStop(0.83f, Color.FromRgb(0x4B, 0x00, 0x82)));
                AdjGradientStops.Add(new GradientMapRampStop(1.0f, Color.FromRgb(0xEE, 0x82, 0xEE)));
                break;
            case "BlackWhite":
            default:
                AdjGradientStops.Add(new GradientMapRampStop(0.0f, Colors.Black));
                AdjGradientStops.Add(new GradientMapRampStop(1.0f, Colors.White));
                break;
        }
        AdjGradientSelectedStopIndex = 0;
        OnPropertyChanged(nameof(AdjGradientSelectedStopLocationPercent));
        OnPropertyChanged(nameof(AdjGradientSelectedStopColorBrush));
        OnPropertyChanged(nameof(CanDeleteGradientStop));
    }

    [RelayCommand]
    public void DeleteSelectedGradientStop()
    {
        if (AdjGradientStops.Count > 2 && AdjGradientSelectedStopIndex >= 0 && AdjGradientSelectedStopIndex < AdjGradientStops.Count)
        {
            AdjGradientStops.RemoveAt(AdjGradientSelectedStopIndex);
            AdjGradientSelectedStopIndex = Math.Clamp(AdjGradientSelectedStopIndex, 0, AdjGradientStops.Count - 1);
            OnPropertyChanged(nameof(AdjGradientSelectedStopLocationPercent));
            OnPropertyChanged(nameof(AdjGradientSelectedStopColorBrush));
            OnPropertyChanged(nameof(CanDeleteGradientStop));
            ApplyAdjustmentPreview();
        }
    }

    [RelayCommand]
    public void PickSelectedGradientStopColor()
    {
        if (AdjGradientStops.Count > 0 && AdjGradientSelectedStopIndex >= 0 && AdjGradientSelectedStopIndex < AdjGradientStops.Count)
        {
            AdjGradientStops[AdjGradientSelectedStopIndex].Color = PrimaryColor;
            OnPropertyChanged(nameof(AdjGradientSelectedStopColorBrush));
            ApplyAdjustmentPreview();
        }
    }

    private byte[] ExecuteGradientMap(byte[] bgra)
    {
        if (AdjGradientStops.Count == 0)
        {
            PopulateGradientStopsForPreset(AdjGradientPreset);
        }

        var sorted = AdjGradientStops.OrderBy(s => s.Position).ToList();
        Color[] colors = sorted.Select(s => s.Color).ToArray();
        float[] stops = sorted.Select(s => s.Position).ToArray();

        if (AdjGradientReverse)
        {
            colors = colors.Reverse().ToArray();
            stops = stops.Select(p => 1.0f - p).Reverse().ToArray();
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
    [ObservableProperty] private double _filterRawTexture = 0.0;
    [ObservableProperty] private double _filterRawClarity = 0.0;
    [ObservableProperty] private double _filterRawDehaze = 0.0;
    [ObservableProperty] private double _filterRawVibrance = 0.0;
    [ObservableProperty] private double _filterRawSaturation = 0.0;

    [ObservableProperty] private double _filterLensBrightness = 100.0;
    [ObservableProperty] private int _filterLensType = 0;
    [ObservableProperty] private int _filterSolarizeThreshold = 128;
    [ObservableProperty] private uint _filterRandomSeed = 12345u;

    [RelayCommand]
    public void RandomizeFilterSeed()
    {
        FilterRandomSeed = (uint)Random.Shared.Next(1, int.MaxValue);
        ApplyFilterPreview();
    }

    // 10 Studio Creative & Optical Filters Properties
    [ObservableProperty] private int _filterWindStrength = 15;
    [ObservableProperty] private int _filterWindDirection = 0; // 0=FromLeft, 1=FromRight
    [ObservableProperty] private int _filterDiffuseMode = 0; // 0=Normal, 1=DarkenOnly, 2=LightenOnly
    [ObservableProperty] private int _filterDiffuseRadius = 3;
    [ObservableProperty] private int _filterExtrudeBlockSize = 20;
    [ObservableProperty] private int _filterExtrudeDepth = 30;
    [ObservableProperty] private bool _filterExtrudeRandom = false;
    [ObservableProperty] private int _filterTilesNumTiles = 10;
    [ObservableProperty] private int _filterTilesMaximumOffset = 10;
    [ObservableProperty] private int _filterFibersVariance = 16;
    [ObservableProperty] private int _filterFibersStrength = 4;
    [ObservableProperty] private double _filterZigZagAmount = 50.0;
    [ObservableProperty] private int _filterZigZagRidges = 5;
    [ObservableProperty] private int _filterZigZagStyle = 0; // 0=AroundCenter, 1=OutFromCenter, 2=PondRipples
    [ObservableProperty] private double _filterShearOffset = 25.0;
    [ObservableProperty] private int _filterMezzotintType = 0; // 0=FineDots, 1=MediumDots, 2=GrainyDots, 3=ShortLines, 4=MediumLines, 5=LongLines
    [ObservableProperty] private int _filterTraceContourLevel = 128;
    [ObservableProperty] private int _filterTraceContourEdge = 0; // 0=Lower, 1=Upper
    [ObservableProperty] private int _filterWaterPaperFiberLength = 15;
    [ObservableProperty] private int _filterWaterPaperBrightness = 50;
    [ObservableProperty] private int _filterWaterPaperContrast = 60;
    [ObservableProperty] private Color _filterColorToAlphaKeyColor = Colors.White;
    [ObservableProperty] private double _filterColorToAlphaTransparency = 0.0;
    [ObservableProperty] private double _filterColorToAlphaOpacity = 1.0;

    // Distort > Displace Filter Properties
    [ObservableProperty] private double _filterDisplaceHorizScale = 10.0;
    [ObservableProperty] private double _filterDisplaceVertScale = 10.0;
    [ObservableProperty] private bool _filterDisplaceIsTile = false; // false = Stretch to Fit, true = Tile
    [ObservableProperty] private bool _filterDisplaceIsWrap = false; // false = Repeat Edge Pixels, true = Wrap Around
    [ObservableProperty] private int _filterDisplacePresetIndex = 0; // 0=Ripple, 1=Waves, 2=Fabric, 3=Glass, 4=Custom
    [ObservableProperty] private string _filterDisplaceMapFileName = "Ripple (Procedural)";
    private byte[]? _filterDisplaceCustomBytes;
    private int _filterDisplaceCustomWidth;
    private int _filterDisplaceCustomHeight;

    // 35. Offset Filter (Filter > Other > Offset...) (#4738)
    [ObservableProperty] private double _filterOffsetHorizontal = 0;
    [ObservableProperty] private double _filterOffsetVertical = 0;
    [ObservableProperty] private string _filterOffsetUndefinedMode = "Wrap"; // Wrap, RepeatEdge, Background

    // In-Dialog Settings Undo/Redo History (#4697, #4715)
    private DialogEditHistory<string>? _activeDialogEditHistory;

    // In-Dialog Filter Preview (Studio standard)
    [ObservableProperty] private Bitmap? _filterDialogPreviewBitmap;
    [ObservableProperty] private Bitmap? _filterDialogOriginalBitmap;
    [ObservableProperty] private double _filterDialogPreviewZoom = 1.0;
    [ObservableProperty] private Point _filterDialogPreviewPan = new Point(0, 0);

    public string FilterDialogPreviewZoomPercentText => $"{Math.Round(FilterDialogPreviewZoom * 100):0}%";

    [RelayCommand]
    public void ZoomInFilterPreview()
    {
        FilterDialogPreviewZoom = Controls.FilterPreviewControl.StepZoom(FilterDialogPreviewZoom, true);
        OnPropertyChanged(nameof(FilterDialogPreviewZoomPercentText));
    }

    [RelayCommand]
    public void ZoomOutFilterPreview()
    {
        FilterDialogPreviewZoom = Controls.FilterPreviewControl.StepZoom(FilterDialogPreviewZoom, false);
        OnPropertyChanged(nameof(FilterDialogPreviewZoomPercentText));
    }

    partial void OnFilterDialogPreviewZoomChanged(double value) => OnPropertyChanged(nameof(FilterDialogPreviewZoomPercentText));

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
            "Wind" => "Wind",
            "Diffuse" => "Diffuse",
            "Extrude" => "Extrude",
            "Tiles" => "Tiles",
            "Fibers" => "Fibers",
            "ZigZag" => "ZigZag",
            "Shear" => "Shear",
            "Mezzotint" => "Mezzotint",
            "TraceContour" => "Trace Contour",
            "WaterPaper" => "Water Paper",
            "ColorToAlpha" => "Color to Alpha",
            "Displace" => "Displace",
            "Offset" => "Offset",
            _ => "Filter"
        };

        _filterTargetImageElement = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (_filterTargetImageElement == null && Elements.Count > 0)
        {
            using var rtb = RenderCanvasToBitmap(1.0);
            if (rtb != null)
            {
                using var ms = new MemoryStream();
                rtb.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                var bytes = ms.ToArray();
                var img = new ImageElement
                {
                    Name = "Composite Layer",
                    X = 0,
                    Y = 0,
                    Width = _canvasWidth,
                    Height = _canvasHeight,
                    OriginalImageBytes = bytes,
                    Source = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(bytes)),
                    NaturalPixelWidth = (int)_canvasWidth,
                    NaturalPixelHeight = (int)_canvasHeight,
                    ZIndex = Elements.Count
                };
                Elements.Add(img);
                SelectedElement = img;
                _filterTargetImageElement = img;
            }
        }

        if (_filterTargetImageElement != null)
        {
            _filterOriginalBytes = _filterTargetImageElement.OriginalImageBytes;
            if (_filterOriginalBytes != null)
            {
                using var ms = new MemoryStream(_filterOriginalBytes);
                FilterDialogOriginalBitmap?.Dispose();
                FilterDialogOriginalBitmap = new Bitmap(ms);
            }
        }
        else
        {
            FilterDialogOriginalBitmap?.Dispose();
            FilterDialogOriginalBitmap = null;
        }

        FilterDialogPreviewZoom = 1.0;
        FilterDialogPreviewPan = new Point(0, 0);
        FilterDialogPreviewBitmap?.Dispose();
        FilterDialogPreviewBitmap = null;

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
        FilterRawTexture = 0.0;
        FilterRawClarity = 0.0;
        FilterRawDehaze = 0.0;
        FilterRawVibrance = 0.0;
        FilterRawSaturation = 0.0;
        FilterLensBrightness = 100.0;
        FilterLensType = 0;
        FilterSolarizeThreshold = 128;
        FilterWindStrength = 15;
        FilterWindDirection = 0;
        FilterDiffuseMode = 0;
        FilterDiffuseRadius = 3;
        FilterExtrudeBlockSize = 20;
        FilterExtrudeDepth = 30;
        FilterExtrudeRandom = false;
        FilterTilesNumTiles = 10;
        FilterTilesMaximumOffset = 10;
        FilterFibersVariance = 16;
        FilterFibersStrength = 4;
        FilterZigZagAmount = 50.0;
        FilterZigZagRidges = 5;
        FilterZigZagStyle = 0;
        FilterShearOffset = 25.0;
        FilterMezzotintType = 0;
        FilterTraceContourLevel = 128;
        FilterTraceContourEdge = 0;
        FilterWaterPaperFiberLength = 15;
        FilterWaterPaperBrightness = 50;
        FilterWaterPaperContrast = 60;
        FilterColorToAlphaKeyColor = Colors.White;
        FilterColorToAlphaTransparency = 0.0;
        FilterColorToAlphaOpacity = 1.0;

        FilterDisplaceHorizScale = 10.0;
        FilterDisplaceVertScale = 10.0;
        FilterDisplaceIsTile = false;
        FilterDisplaceIsWrap = false;
        FilterDisplacePresetIndex = 0;
        FilterDisplaceMapFileName = "Ripple (Procedural)";
        _filterDisplaceCustomBytes = null;
        _filterDisplaceCustomWidth = 0;
        _filterDisplaceCustomHeight = 0;

        FilterPreviewEnabled = true;

        IsFilterDialogOpen = true;
        InitDialogHistory(GetCurrentDialogSettingsSnapshot());
    }

    partial void OnFilterDisplaceHorizScaleChanged(double value) => ApplyFilterPreview();
    partial void OnFilterDisplaceVertScaleChanged(double value) => ApplyFilterPreview();
    partial void OnFilterDisplaceIsTileChanged(bool value) => ApplyFilterPreview();
    partial void OnFilterDisplaceIsWrapChanged(bool value) => ApplyFilterPreview();
    partial void OnFilterDisplacePresetIndexChanged(int value)
    {
        FilterDisplaceMapFileName = value switch
        {
            0 => "Ripple (Procedural)",
            1 => "Waves (Procedural)",
            2 => "Fabric (Procedural)",
            3 => "Glass (Procedural)",
            _ => string.IsNullOrEmpty(FilterDisplaceMapFileName) || FilterDisplaceMapFileName.Contains("Procedural") ? "Custom Map" : FilterDisplaceMapFileName
        };
        ApplyFilterPreview();
    }

    [RelayCommand]
    public async Task LoadDisplaceMapFileAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage) return;
            var files = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Displacement Map Image",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }
                ]
            });
            if (files != null && files.Count > 0)
            {
                var file = files[0];
                await using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                byte[] raw = ms.ToArray();
                using var bmp = SKBitmap.Decode(raw);
                if (bmp != null)
                {
                    using var bgra = new SKBitmap(bmp.Width, bmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var canvas = new SKCanvas(bgra);
                    canvas.DrawBitmap(bmp, 0, 0);
                    canvas.Flush();
                    _filterDisplaceCustomBytes = bgra.Bytes;
                    _filterDisplaceCustomWidth = bgra.Width;
                    _filterDisplaceCustomHeight = bgra.Height;
                    FilterDisplacePresetIndex = 4;
                    FilterDisplaceMapFileName = file.Name;
                    ApplyFilterPreview();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DisplaceMap] load error: {ex}");
        }
    }

    partial void OnFilterColorToAlphaKeyColorChanged(Color value) => ApplyFilterPreview();
    partial void OnFilterColorToAlphaTransparencyChanged(double value) => ApplyFilterPreview();
    partial void OnFilterColorToAlphaOpacityChanged(double value) => ApplyFilterPreview();

    [RelayCommand]
    public void SetColorToAlphaKeyColor(string colorOrMode)
    {
        if (colorOrMode == "Primary")
        {
            FilterColorToAlphaKeyColor = PrimaryColor;
        }
        else if (Color.TryParse(colorOrMode, out var c))
        {
            FilterColorToAlphaKeyColor = c;
        }
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
    partial void OnFilterRawTextureChanged(double value) => ApplyFilterPreview();
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
    partial void OnFilterWindStrengthChanged(int value) => ApplyFilterPreview();
    partial void OnFilterWindDirectionChanged(int value) => ApplyFilterPreview();
    partial void OnFilterDiffuseModeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterDiffuseRadiusChanged(int value) => ApplyFilterPreview();
    partial void OnFilterExtrudeBlockSizeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterExtrudeDepthChanged(int value) => ApplyFilterPreview();
    partial void OnFilterExtrudeRandomChanged(bool value) => ApplyFilterPreview();
    partial void OnFilterTilesNumTilesChanged(int value) => ApplyFilterPreview();
    partial void OnFilterTilesMaximumOffsetChanged(int value) => ApplyFilterPreview();
    partial void OnFilterFibersVarianceChanged(int value) => ApplyFilterPreview();
    partial void OnFilterFibersStrengthChanged(int value) => ApplyFilterPreview();
    partial void OnFilterZigZagAmountChanged(double value) => ApplyFilterPreview();
    partial void OnFilterZigZagRidgesChanged(int value) => ApplyFilterPreview();
    partial void OnFilterZigZagStyleChanged(int value) => ApplyFilterPreview();
    partial void OnFilterShearOffsetChanged(double value) => ApplyFilterPreview();
    partial void OnFilterMezzotintTypeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterTraceContourLevelChanged(int value) => ApplyFilterPreview();
    partial void OnFilterTraceContourEdgeChanged(int value) => ApplyFilterPreview();
    partial void OnFilterWaterPaperFiberLengthChanged(int value) => ApplyFilterPreview();
    partial void OnFilterWaterPaperBrightnessChanged(int value) => ApplyFilterPreview();
    partial void OnFilterWaterPaperContrastChanged(int value) => ApplyFilterPreview();
    partial void OnFilterOffsetHorizontalChanged(double value) => ApplyFilterPreview();
    partial void OnFilterOffsetVerticalChanged(double value) => ApplyFilterPreview();
    partial void OnFilterOffsetUndefinedModeChanged(string value) => ApplyFilterPreview();
    partial void OnFilterRandomSeedChanged(uint value) => ApplyFilterPreview();

    public void InitDialogHistory(string initialSettings)
    {
        _activeDialogEditHistory = new DialogEditHistory<string>(initialSettings);
    }

    public void RecordDialogSettingsState(string currentSettings, bool busy = false)
    {
        _activeDialogEditHistory?.Settle(currentSettings, busy, (a, b) => a == b);
    }

    public bool StepDialogHistory(bool redo)
    {
        if (_activeDialogEditHistory == null) return false;
        string current = GetCurrentDialogSettingsSnapshot();
        string? target = redo ? _activeDialogEditHistory.Redo(current) : _activeDialogEditHistory.Undo(current);
        if (target != null)
        {
            ApplyDialogSettingsSnapshot(target);
            return true;
        }
        return false;
    }

    private string GetCurrentDialogSettingsSnapshot()
    {
        if (IsFilterDialogOpen)
        {
            return $"Filter:{ActiveFilterMode}:{FilterBlurRadius}:{FilterSharpenAmount}:{FilterHighPassRadius}:{FilterNoiseAmount}:{FilterOffsetHorizontal}:{FilterOffsetVertical}:{FilterOffsetUndefinedMode}";
        }
        return $"Adjust:{ActiveFilterMode}";
    }

    private void ApplyDialogSettingsSnapshot(string snapshot)
    {
        if (snapshot.StartsWith("Filter:"))
        {
            var parts = snapshot.Split(':');
            if (parts.Length >= 9)
            {
                if (double.TryParse(parts[2], out var blur)) FilterBlurRadius = blur;
                if (double.TryParse(parts[3], out var sh)) FilterSharpenAmount = sh;
                if (double.TryParse(parts[4], out var hp)) FilterHighPassRadius = hp;
                if (int.TryParse(parts[5], out var noise)) FilterNoiseAmount = noise;
                if (double.TryParse(parts[6], out var ox)) FilterOffsetHorizontal = ox;
                if (double.TryParse(parts[7], out var oy)) FilterOffsetVertical = oy;
                FilterOffsetUndefinedMode = parts[8];
                ApplyFilterPreview();
            }
        }
    }

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
            "Crystallize" => ImageAdjustmentEngine.ApplyCrystallize(bgra, width, height, FilterCrystallizeCellSize, FilterRandomSeed),
            "Pointillize" => ImageAdjustmentEngine.ApplyPointillize(bgra, width, height, FilterPointillizeCellSize, FilterRandomSeed),
            "Facet" => ImageAdjustmentEngine.ApplyFacet(bgra, width, height, FilterFacetRadius),
            "OilPaint" => ImageAdjustmentEngine.ApplyOilPaint(bgra, width, height, FilterOilStylization, FilterOilCleanliness, FilterOilScale, FilterOilBristleDetail, FilterOilLighting, FilterOilAngle, FilterOilShine),
            "CameraRaw" => ImageAdjustmentEngine.ApplyCameraRaw(bgra, width, height, FilterRawExposure, FilterRawTemperature, FilterRawTint, FilterRawContrast, FilterRawHighlights, FilterRawShadows, FilterRawWhites, FilterRawBlacks, FilterRawTexture, FilterRawClarity, FilterRawDehaze, FilterRawVibrance, FilterRawSaturation),
            "LensFlare" => ImageAdjustmentEngine.ApplyLensFlare(bgra, width, height, FilterLensBrightness, lensType: FilterLensType),
            "Clouds" => ImageAdjustmentEngine.ApplyClouds(bgra, width, height, PrimaryColor, SecondaryColor, false, FilterRandomSeed),
            "DifferenceClouds" => ImageAdjustmentEngine.ApplyClouds(bgra, width, height, PrimaryColor, SecondaryColor, true, FilterRandomSeed),
            "FindEdges" => ImageAdjustmentEngine.ApplyFindEdges(bgra, width, height),
            "Solarize" => ImageAdjustmentEngine.ApplySolarize(bgra, FilterSolarizeThreshold),
            "Emboss" => ImageAdjustmentEngine.ApplyEmboss(bgra, width, height, FilterEmbossAngle, FilterEmbossDepth),
            "Wind" => ImageAdjustmentEngine.ApplyWind(bgra, width, height, FilterWindDirection, 0, FilterWindStrength),
            "Diffuse" => ImageAdjustmentEngine.ApplyDiffuse(bgra, width, height, FilterDiffuseMode, FilterDiffuseRadius),
            "Extrude" => ImageAdjustmentEngine.ApplyExtrude(bgra, width, height, FilterExtrudeRandom ? 1 : 0, FilterExtrudeBlockSize, FilterExtrudeDepth),
            "Tiles" => ImageAdjustmentEngine.ApplyTiles(bgra, width, height, FilterTilesNumTiles, FilterTilesMaximumOffset, 0),
            "Fibers" => ImageAdjustmentEngine.ApplyFibers(width, height, FilterFibersVariance, FilterFibersStrength, FilterRandomSeed),
            "ZigZag" => ImageAdjustmentEngine.ApplyZigZag(bgra, width, height, (int)FilterZigZagAmount, FilterZigZagRidges, FilterZigZagStyle),
            "Shear" => ImageAdjustmentEngine.ApplyShear(bgra, width, height, FilterShearOffset),
            "Mezzotint" => ImageAdjustmentEngine.ApplyMezzotint(bgra, width, height, FilterMezzotintType),
            "TraceContour" => ImageAdjustmentEngine.ApplyTraceContour(bgra, width, height, FilterTraceContourLevel, FilterTraceContourEdge == 1),
            "WaterPaper" => ImageAdjustmentEngine.ApplyWaterPaper(bgra, width, height, FilterWaterPaperFiberLength, FilterWaterPaperBrightness, FilterWaterPaperContrast),
            "ColorToAlpha" => ImageAdjustmentEngine.ApplyColorToAlpha(bgra, FilterColorToAlphaKeyColor, FilterColorToAlphaTransparency, FilterColorToAlphaOpacity),
            "Displace" => ImageAdjustmentEngine.ApplyDisplace(
                bgra, width, height,
                FilterDisplaceHorizScale,
                FilterDisplaceVertScale,
                FilterDisplaceIsTile,
                FilterDisplaceIsWrap,
                FilterDisplacePresetIndex switch
                {
                    0 => "Ripple",
                    1 => "Waves",
                    2 => "Fabric",
                    3 => "Glass",
                    _ => "Ripple"
                },
                _filterDisplaceCustomBytes,
                _filterDisplaceCustomWidth,
                _filterDisplaceCustomHeight),
            "Offset" => OffsetFilterEngine.ApplyOffset(
                bgra, width, height,
                (int)Math.Round(FilterOffsetHorizontal),
                (int)Math.Round(FilterOffsetVertical),
                FilterOffsetUndefinedMode switch
                {
                    "RepeatEdge" => OffsetFilterEngine.UndefinedAreaMode.Repeat,
                    "Background" => OffsetFilterEngine.UndefinedAreaMode.Background,
                    _ => OffsetFilterEngine.UndefinedAreaMode.Wrap
                },
                SecondaryColor),
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
                byte[] encodedBytes = encoded.ToArray();
                using var ms = new MemoryStream(encodedBytes);
                _filterTargetImageElement.AdjustedSource?.Dispose();
                _filterTargetImageElement.AdjustedSource = new Bitmap(ms);

                // Update in-dialog preview bitmap as well
                using var previewMs = new MemoryStream(encodedBytes);
                FilterDialogPreviewBitmap?.Dispose();
                FilterDialogPreviewBitmap = new Bitmap(previewMs);

                RefreshCanvas();
                RecordDialogSettingsState(GetCurrentDialogSettingsSnapshot());
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
        FilterDialogPreviewBitmap?.Dispose();
        FilterDialogPreviewBitmap = null;
        FilterDialogOriginalBitmap?.Dispose();
        FilterDialogOriginalBitmap = null;

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
        FilterDialogPreviewBitmap?.Dispose();
        FilterDialogPreviewBitmap = null;
        FilterDialogOriginalBitmap?.Dispose();
        FilterDialogOriginalBitmap = null;
        IsFilterDialogOpen = false;
    }

    [RelayCommand]
    public void CropAndStraightenPhotos()
    {
        var targetImg = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (targetImg?.OriginalImageBytes == null)
        {
            StatusMessage = "Select an image layer to crop and straighten.";
            return;
        }

        try
        {
            using var srcBmp = SKBitmap.Decode(targetImg.OriginalImageBytes);
            if (srcBmp == null) return;

            byte[] bgra = srcBmp.Bytes;
            var detected = ImageAdjustmentEngine.DetectScannedPhotos(bgra, srcBmp.Width, srcBmp.Height);
            if (detected.Count == 0)
            {
                StatusMessage = "No distinct photographs detected on flatbed scan.";
                return;
            }

            int idx = Elements.IndexOf(targetImg);
            var undoBatch = new List<CanvasElement>();

            for (int i = 0; i < detected.Count; i++)
            {
                var photo = detected[i];
                var (croppedBgra, cropW, cropH) = ImageAdjustmentEngine.ExtractStraightenedPhoto(
                    bgra, srcBmp.Width, srcBmp.Height, photo);

                using var skPhoto = new SKBitmap(cropW, cropH, SKColorType.Bgra8888, SKAlphaType.Premul);
                var pin = System.Runtime.InteropServices.GCHandle.Alloc(croppedBgra, System.Runtime.InteropServices.GCHandleType.Pinned);
                byte[] encodedBytes;
                try
                {
                    skPhoto.InstallPixels(skPhoto.Info, pin.AddrOfPinnedObject());
                    using var img = SKImage.FromBitmap(skPhoto);
                    using var enc = img.Encode(SKEncodedImageFormat.Png, 100);
                    encodedBytes = enc.ToArray();
                }
                finally
                {
                    pin.Free();
                }

                var newElement = new ImageElement
                {
                    Name = $"{targetImg.Name} - Straightened {i + 1}",
                    X = photo.Center.X - cropW / 2.0,
                    Y = photo.Center.Y - cropH / 2.0,
                    Width = cropW,
                    Height = cropH,
                    OriginalImageBytes = encodedBytes,
                    Source = new Bitmap(new MemoryStream(encodedBytes)),
                    NaturalPixelWidth = cropW,
                    NaturalPixelHeight = cropH,
                    ZIndex = targetImg.ZIndex + i + 1
                };

                Elements.Insert(idx + 1 + i, newElement);
                undoBatch.Add(newElement);
            }

            SelectedElement = undoBatch.LastOrDefault();
            StatusMessage = $"Crop and Straighten completed: {detected.Count} photo(s) extracted upright.";
            RefreshCanvas();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Crop and Straighten failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void MakeWorkPathFromSelection()
    {
        VectorDocumentPath? workPath = null;
        var selectionPolygon = CanvasControl?.GetActiveSelectionPolygon();
        if (selectionPolygon != null && selectionPolygon.Count >= 3)
        {
            workPath = VectorPathTracer.FitPolygonToPath(selectionPolygon, tolerance: 2.0, isClosed: true);
        }
        else if (CanvasControl?.QuickMaskBuffer is { } qm)
        {
            int w = (int)CanvasWidth;
            int h = (int)CanvasHeight;
            float[] mask = new float[w * h];
            for (int i = 0; i < mask.Length; i++)
            {
                mask[i] = qm[i] / 255.0f;
            }
            workPath = VectorPathTracer.TraceMaskToPath(mask, w, h, tolerance: 2.0);
        }
        else if (SelectedElement != null)
        {
            var bounds = SelectedElement.Bounds;
            Point[] rectPoints = [
                new(bounds.Left, bounds.Top),
                new(bounds.Right, bounds.Top),
                new(bounds.Right, bounds.Bottom),
                new(bounds.Left, bounds.Bottom)
            ];
            workPath = VectorPathTracer.FitPolygonToPath(rectPoints, tolerance: 2.0, isClosed: true);
        }

        if (workPath != null)
        {
            workPath.Name = "Work Path";
            DocumentPaths.Add(workPath);
            ActiveDocumentPath = workPath;
            if (CanvasControl != null)
            {
                CanvasControl.ActiveDocumentPath = workPath;
            }
            ActiveToolMode = "pen";
            ActiveLayersDockTab = "Paths";
            StatusMessage = $"Work path created with {workPath.Subpaths.Sum(s => s.Knots.Count)} vector knots.";
            RefreshCanvas();
            return;
        }

        StatusMessage = "No active selection to convert into a work path.";
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
                Title = "Open Images, PSD, Brushes or Swatches",
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType("Supported Studio Formats (*.psd, *.ora, *.abr, *.aco, *.ase, *.grd, *.pat, *.fits, *.pcx, *.kra, *.dds, *.dpx, *.xpm, *.atn, images)")
                    {
                        Patterns = ["*.psd", "*.ora", "*.abr", "*.aco", "*.ase", "*.grd", "*.pat", "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.fits", "*.fit", "*.fts", "*.pcx", "*.kra", "*.dds", "*.dpx", "*.xpm", "*.atn"]
                    },
                    new FilePickerFileType("Layered Document (*.psd)") { Patterns = ["*.psd"] },
                    new FilePickerFileType("OpenRaster Document (*.ora)") { Patterns = ["*.ora"] },
                    new FilePickerFileType("DirectDraw Surface (*.dds)") { Patterns = ["*.dds"] },
                    new FilePickerFileType("SMPTE DPX Film/VFX (*.dpx)") { Patterns = ["*.dpx"] },
                    new FilePickerFileType("X PixMap (*.xpm)") { Patterns = ["*.xpm"] },
                    new FilePickerFileType("Actions (*.atn)") { Patterns = ["*.atn"] },
                    new FilePickerFileType("Krita Document (*.kra)") { Patterns = ["*.kra"] },
                    new FilePickerFileType("ZSoft PCX Image (*.pcx)") { Patterns = ["*.pcx"] },
                    new FilePickerFileType("Brushes (*.abr)") { Patterns = ["*.abr"] },
                    new FilePickerFileType("Color Swatches (*.aco, *.ase)") { Patterns = ["*.aco", "*.ase"] },
                    new FilePickerFileType("Gradients (*.grd)") { Patterns = ["*.grd"] },
                    new FilePickerFileType("Patterns (*.pat)") { Patterns = ["*.pat"] },
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
                        var blendMode = layer.BlendMode.Trim() switch
                        {
                            "diss" => LayerBlendMode.Dissolve,
                            "dark" => LayerBlendMode.Darken,
                            "mul" => LayerBlendMode.Multiply,
                            "idiv" => LayerBlendMode.ColorBurn,
                            "lbrn" => LayerBlendMode.LinearBurn,
                            "dkCl" => LayerBlendMode.DarkerColor,
                            "lite" => LayerBlendMode.Lighten,
                            "scrn" => LayerBlendMode.Screen,
                            "div" => LayerBlendMode.ColorDodge,
                            "lddg" => LayerBlendMode.LinearDodge,
                            "lgCl" => LayerBlendMode.LighterColor,
                            "over" => LayerBlendMode.Overlay,
                            "sLit" => LayerBlendMode.SoftLight,
                            "hLit" => LayerBlendMode.HardLight,
                            "vLit" => LayerBlendMode.VividLight,
                            "lLit" => LayerBlendMode.LinearLight,
                            "pLit" => LayerBlendMode.PinLight,
                            "hMix" => LayerBlendMode.HardMix,
                            "diff" => LayerBlendMode.Difference,
                            "smud" => LayerBlendMode.Exclusion,
                            "fsub" => LayerBlendMode.Subtract,
                            "fdiv" => LayerBlendMode.Divide,
                            "hue" => LayerBlendMode.Hue,
                            "sat" => LayerBlendMode.Saturation,
                            "colr" => LayerBlendMode.Color,
                            "lum" => LayerBlendMode.Luminosity,
                            _ => LayerBlendMode.Normal
                        };

                        if (layer.IsFolder)
                        {
                            var groupEl = new GroupElement
                            {
                                Name = layer.Name,
                                GroupId = layer.GroupId,
                                GroupIndentLevel = layer.GroupIndentLevel,
                                IsExpanded = layer.SectionDividerType == 1,
                                IsVisible = layer.IsVisible,
                                Opacity = layer.Opacity / 255.0,
                                BlendMode = blendMode,
                                ZIndex = z++
                            };
                            Elements.Add(groupEl);
                        }
                        else if (!layer.IsDivider && layer.BgraPixels != null && layer.Width > 0 && layer.Height > 0)
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
                                    GroupId = layer.GroupId,
                                    GroupIndentLevel = layer.GroupIndentLevel,
                                    IsTextLayer = layer.IsTextLayer,
                                    IsVectorShape = layer.IsVectorShape,
                                    IsSmartObject = layer.IsSmartObject,
                                    Source = avaloniaBmp,
                                    OriginalImageBytes = pngBytes,
                                    Opacity = layer.Opacity / 255.0,
                                    BlendMode = blendMode,
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

                    // Populate GroupElement.Children references
                    foreach (var g in Elements.OfType<GroupElement>())
                    {
                        if (g.GroupId.HasValue)
                        {
                            foreach (var child in Elements.Where(e => e.GroupId == g.GroupId && e != g))
                            {
                                g.Children.Add(child);
                            }
                        }
                    }

                    if (Elements.Count == 0 && psd.CompositeBgraPixels != null && psd.Width > 0 && psd.Height > 0)
                    {
                        using var skBmp = new SKBitmap(psd.Width, psd.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var pin = System.Runtime.InteropServices.GCHandle.Alloc(psd.CompositeBgraPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
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
                                Name = "Background",
                                X = 0,
                                Y = 0,
                                Width = psd.Width,
                                Height = psd.Height,
                                NaturalPixelWidth = psd.Width,
                                NaturalPixelHeight = psd.Height,
                                Source = avaloniaBmp,
                                OriginalImageBytes = pngBytes,
                                Opacity = 1.0,
                                BlendMode = LayerBlendMode.Normal,
                                IsVisible = true,
                                ZIndex = 1
                            };
                            Elements.Add(el);
                        }
                        finally
                        {
                            pin.Free();
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
                else if (ext == ".pcx")
                {
                    var pcx = PcxCodec.Decode(stream);
                    if (Elements.Count == 0)
                    {
                        ClearCanvas();
                        CanvasWidth = Math.Max(200, pcx.Width);
                        CanvasHeight = Math.Max(150, pcx.Height);
                        DocumentTitle = file.Name;
                    }

                    using var skBmp = new SKBitmap(pcx.Width, pcx.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    var pin = GCHandle.Alloc(pcx.BgraPixels, GCHandleType.Pinned);
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
                            X = (CanvasWidth - pcx.Width) / 2.0,
                            Y = (CanvasHeight - pcx.Height) / 2.0,
                            Width = pcx.Width,
                            Height = pcx.Height,
                            NaturalPixelWidth = pcx.Width,
                            NaturalPixelHeight = pcx.Height,
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
                else if (ext == ".kra")
                {
                    var kra = KraCodec.Decode(stream);
                    if (Elements.Count == 0)
                    {
                        ClearCanvas();
                        CanvasWidth = Math.Max(200, kra.Width);
                        CanvasHeight = Math.Max(150, kra.Height);
                        DocumentTitle = file.Name;
                    }

                    using var ms = new MemoryStream(kra.PngBytes);
                    var avaloniaBmp = new Bitmap(ms);

                    var el = new ImageElement
                    {
                        Name = Path.GetFileNameWithoutExtension(file.Name),
                        X = (CanvasWidth - kra.Width) / 2.0,
                        Y = (CanvasHeight - kra.Height) / 2.0,
                        Width = kra.Width,
                        Height = kra.Height,
                        NaturalPixelWidth = kra.Width,
                        NaturalPixelHeight = kra.Height,
                        Source = avaloniaBmp,
                        OriginalImageBytes = kra.PngBytes,
                        ZIndex = Elements.Count + 1
                    };

                    Elements.Add(el);
                    SelectedElement = el;
                    StatusMessage = kra.Warning;
                    openedCount++;
                }
                else if (ext == ".abr")
                {
                    var abrBrushes = AbrCodec.Decode(stream);
                    if (abrBrushes.Count > 0)
                    {
                        var presets = abrBrushes.Select(b => AbrCodec.ToBrushPreset(b, Path.GetFileNameWithoutExtension(file.Name))).ToList();
                        BrushPresetLibrary.AddPresets(presets);
                        OnPropertyChanged(nameof(AvailableBrushPresets));
                        ActiveLayersDockTab = "Brushes";
                        StatusMessage = $"Imported {abrBrushes.Count} brushes from {file.Name}.";
                        openedCount++;
                    }
                }
                else if (ext is ".aco" or ".ase")
                {
                    List<SwatchItem> items = ext == ".ase" ? SwatchCodec.DecodeAse(stream) : SwatchCodec.DecodeAco(stream);
                    if (items.Count > 0)
                    {
                        SwatchesList.Clear();
                        foreach (var it in items) SwatchesList.Add(it.Color);
                        ActiveColorDockTab = "Swatches";
                        StatusMessage = $"Imported {items.Count} swatches from {file.Name}.";
                        openedCount++;
                    }
                }
                else if (ext == ".grd")
                {
                    var grads = GrdCodec.Decode(stream);
                    if (grads.Count > 0)
                    {
                        ActiveColorDockTab = "Gradients";
                        StatusMessage = $"Imported {grads.Count} gradients from {file.Name}.";
                        openedCount++;
                    }
                }
                else if (ext == ".pat")
                {
                    var patterns = PatternCodec.Decode(stream);
                    if (patterns.Count > 0)
                    {
                        StatusMessage = $"Imported {patterns.Count} patterns from {file.Name}.";
                        openedCount++;
                    }
                }
                else if (ext == ".atn")
                {
                    var atnFile = AtnCodec.Decode(stream);
                    ActionSets.Add(atnFile);
                    SelectedActionSet = atnFile;
                    SelectedAction = atnFile.Actions.FirstOrDefault();
                    ActiveLayersDockTab = "Actions";
                    StatusMessage = $"Loaded Actions '{atnFile.Name}' ({atnFile.Actions.Count} actions).";
                    openedCount++;
                }
                else if (ext == ".dds")
                {
                    var dds = DdsCodec.Decode(stream);
                    if (Elements.Count == 0)
                    {
                        ClearCanvas();
                        CanvasWidth = Math.Max(200, dds.Width);
                        CanvasHeight = Math.Max(150, dds.Height);
                        DocumentTitle = file.Name;
                    }

                    using var skBmp = dds.ToSkBitmap();
                    using var img = SKImage.FromBitmap(skBmp);
                    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                    byte[] pngBytes = data.ToArray();

                    using var ms = new MemoryStream(pngBytes);
                    var avaloniaBmp = new Bitmap(ms);

                    var el = new ImageElement
                    {
                        Name = Path.GetFileNameWithoutExtension(file.Name),
                        X = (CanvasWidth - dds.Width) / 2.0,
                        Y = (CanvasHeight - dds.Height) / 2.0,
                        Width = dds.Width,
                        Height = dds.Height,
                        NaturalPixelWidth = dds.Width,
                        NaturalPixelHeight = dds.Height,
                        Source = avaloniaBmp,
                        OriginalImageBytes = pngBytes,
                        ZIndex = Elements.Count + 1
                    };

                    Elements.Add(el);
                    SelectedElement = el;
                    StatusMessage = $"Loaded DDS texture ({dds.FormatName}, {dds.Width}x{dds.Height}).";
                    openedCount++;
                }
                else if (ext == ".dpx")
                {
                    var dpx = DpxCodec.Decode(stream);
                    if (Elements.Count == 0)
                    {
                        ClearCanvas();
                        CanvasWidth = Math.Max(200, dpx.Width);
                        CanvasHeight = Math.Max(150, dpx.Height);
                        DocumentTitle = file.Name;
                    }

                    using var skBmp = dpx.ToSkBitmap();
                    using var img = SKImage.FromBitmap(skBmp);
                    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                    byte[] pngBytes = data.ToArray();

                    using var ms = new MemoryStream(pngBytes);
                    var avaloniaBmp = new Bitmap(ms);

                    var el = new ImageElement
                    {
                        Name = Path.GetFileNameWithoutExtension(file.Name),
                        X = (CanvasWidth - dpx.Width) / 2.0,
                        Y = (CanvasHeight - dpx.Height) / 2.0,
                        Width = dpx.Width,
                        Height = dpx.Height,
                        NaturalPixelWidth = dpx.Width,
                        NaturalPixelHeight = dpx.Height,
                        Source = avaloniaBmp,
                        OriginalImageBytes = pngBytes,
                        ZIndex = Elements.Count + 1
                    };

                    Elements.Add(el);
                    SelectedElement = el;
                    StatusMessage = $"Loaded DPX frame ({dpx.BitDepth}-bit, {dpx.Width}x{dpx.Height}).";
                    openedCount++;
                }
                else if (ext == ".xpm")
                {
                    var xpm = XpmCodec.Decode(stream);
                    if (Elements.Count == 0)
                    {
                        ClearCanvas();
                        CanvasWidth = Math.Max(200, xpm.Width);
                        CanvasHeight = Math.Max(150, xpm.Height);
                        DocumentTitle = file.Name;
                    }

                    using var skBmp = xpm.ToSkBitmap();
                    using var img = SKImage.FromBitmap(skBmp);
                    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                    byte[] pngBytes = data.ToArray();

                    using var ms = new MemoryStream(pngBytes);
                    var avaloniaBmp = new Bitmap(ms);

                    var el = new ImageElement
                    {
                        Name = Path.GetFileNameWithoutExtension(file.Name),
                        X = (CanvasWidth - xpm.Width) / 2.0,
                        Y = (CanvasHeight - xpm.Height) / 2.0,
                        Width = xpm.Width,
                        Height = xpm.Height,
                        NaturalPixelWidth = xpm.Width,
                        NaturalPixelHeight = xpm.Height,
                        Source = avaloniaBmp,
                        OriginalImageBytes = pngBytes,
                        ZIndex = Elements.Count + 1
                    };

                    Elements.Add(el);
                    SelectedElement = el;
                    StatusMessage = $"Loaded XPM image ({xpm.Width}x{xpm.Height}, {xpm.ColorsCount} colors).";
                    openedCount++;
                }
                else if (ext == ".ora")
                {
                    var ora = OraCodec.Decode(stream);
                    if (Elements.Count == 0)
                    {
                        ClearCanvas();
                        CanvasWidth = Math.Max(200, ora.Width);
                        CanvasHeight = Math.Max(150, ora.Height);
                        DocumentTitle = file.Name;
                    }

                    int z = Elements.Count + 1;
                    foreach (var layer in ora.Layers)
                    {
                        if (layer.PngBytes != null && layer.PngBytes.Length > 0)
                        {
                            using var ms = new MemoryStream(layer.PngBytes);
                            var bmp = new Bitmap(ms);

                            var el = new ImageElement
                            {
                                Name = layer.Name,
                                X = layer.X,
                                Y = layer.Y,
                                Width = bmp.PixelSize.Width,
                                Height = bmp.PixelSize.Height,
                                NaturalPixelWidth = bmp.PixelSize.Width,
                                NaturalPixelHeight = bmp.PixelSize.Height,
                                Source = bmp,
                                OriginalImageBytes = layer.PngBytes,
                                Opacity = layer.Opacity,
                                IsVisible = layer.IsVisible,
                                ZIndex = z++
                            };

                            Elements.Add(el);
                            SelectedElement = el;
                        }
                    }

                    openedCount++;
                    StatusMessage = $"Loaded OpenRaster document '{file.Name}' ({ora.Layers.Count} layers).";
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

            StatusMessage = "Saving PSD 0%...";
            var sortedElements = Elements.OrderBy(e => e.ZIndex).ToList();
            for (int i = 0; i < sortedElements.Count; i++)
            {
                var el = sortedElements[i];
                int pct = (int)((i + 1) * 75.0 / Math.Max(1, sortedElements.Count));
                StatusMessage = $"Saving PSD {pct}%...";
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
                        LayerBlendMode.Dissolve => "diss",
                        LayerBlendMode.Darken => "dark",
                        LayerBlendMode.Multiply => "mul ",
                        LayerBlendMode.ColorBurn => "idiv",
                        LayerBlendMode.LinearBurn => "lbrn",
                        LayerBlendMode.DarkerColor => "dkCl",
                        LayerBlendMode.Lighten => "lite",
                        LayerBlendMode.Screen => "scrn",
                        LayerBlendMode.ColorDodge => "div ",
                        LayerBlendMode.LinearDodge => "lddg",
                        LayerBlendMode.LighterColor => "lgCl",
                        LayerBlendMode.Overlay => "over",
                        LayerBlendMode.SoftLight => "sLit",
                        LayerBlendMode.HardLight => "hLit",
                        LayerBlendMode.VividLight => "vLit",
                        LayerBlendMode.LinearLight => "lLit",
                        LayerBlendMode.PinLight => "pLit",
                        LayerBlendMode.HardMix => "hMix",
                        LayerBlendMode.Difference => "diff",
                        LayerBlendMode.Exclusion => "smud",
                        LayerBlendMode.Subtract => "fsub",
                        LayerBlendMode.Divide => "fdiv",
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

            StatusMessage = "Saving PSD 85%...";
            await using var stream = await file.OpenWriteAsync();
            PsdCodec.Encode(psd, stream);
            StatusMessage = $"Saved PSD: {file.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving PSD: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveOraFileAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save OpenRaster Document (.ora)",
                SuggestedFileName = $"{Path.GetFileNameWithoutExtension(DocumentTitle)}.ora",
                DefaultExtension = ".ora",
                FileTypeChoices =
                [
                    new FilePickerFileType("OpenRaster Document (*.ora)") { Patterns = ["*.ora"] }
                ]
            });

            if (file == null) return;

            // Loss protection check (#3806)
            var losses = OraCodec.CheckLosses(Elements);
            if (losses.Count > 0)
            {
                StatusMessage = $"OpenRaster warning: {losses[0]}";
            }

            var oraDoc = new OraCodec.OraDocument
            {
                Width = (int)CanvasWidth,
                Height = (int)CanvasHeight
            };

            var sorted = Elements.OrderBy(e => e.ZIndex).ToList();
            foreach (var el in sorted)
            {
                byte[] pngBytes = [];
                if (el is ImageElement img && img.OriginalImageBytes != null)
                {
                    pngBytes = img.OriginalImageBytes;
                }
                else
                {
                    int lw = Math.Max(1, (int)el.Width);
                    int lh = Math.Max(1, (int)el.Height);
                    using var surface = SKSurface.Create(new SKImageInfo(lw, lh, SKColorType.Bgra8888, SKAlphaType.Premul));
                    if (surface != null)
                    {
                        var canvas = surface.Canvas;
                        canvas.Clear(SKColors.Transparent);
                        using var paint = new SKPaint { Color = new SKColor(99, 102, 241, (byte)(el.Opacity * 255)) };
                        canvas.DrawRect(0, 0, lw, lh, paint);
                        using var snap = surface.Snapshot();
                        using var data = snap.Encode(SKEncodedImageFormat.Png, 100);
                        pngBytes = data.ToArray();
                    }
                }

                oraDoc.Layers.Add(new OraCodec.OraLayer
                {
                    Name = el.DisplayName,
                    X = (int)el.X,
                    Y = (int)el.Y,
                    Opacity = el.Opacity,
                    IsVisible = el.IsVisible,
                    PngBytes = pngBytes
                });
            }

            await using var stream = await file.OpenWriteAsync();
            OraCodec.Encode(oraDoc, stream);
            StatusMessage = losses.Count > 0
                ? $"Saved OpenRaster: {file.Name} ({losses.Count} features flattened)."
                : $"Saved OpenRaster: {file.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving OpenRaster: {ex.Message}";
        }
    }

    // ── 15. Channel Options & Quick Mask Options Modal Dialog (#3761 parity) ──
    [ObservableProperty] private bool _isChannelOptionsDialogOpen;
    [ObservableProperty] private string _channelOptionsDialogTitle = "Channel Options";
    [ObservableProperty] private DocumentChannelItem? _channelOptionsTarget;
    [ObservableProperty] private string _channelOptionsName = "Alpha 1";
    [ObservableProperty] private string _channelOptionsIndicates = "masked"; // masked | selected | spot
    [ObservableProperty] private Color _channelOptionsColor = Color.FromRgb(255, 0, 0);
    [ObservableProperty] private double _channelOptionsOpacity = 50.0;
    [ObservableProperty] private bool _isChannelOptionsNameVisible = true;
    [ObservableProperty] private bool _isChannelOptionsSpotVisible = true;
    [ObservableProperty] private string _channelOptionsOpacityLabel = "Opacity:";

    [ObservableProperty] private bool _quickMaskIndicatesSelected = false;
    [ObservableProperty] private Color _quickMaskColor = Color.FromRgb(255, 0, 0);
    [ObservableProperty] private double _quickMaskOpacity = 50.0;

    partial void OnQuickMaskIndicatesSelectedChanged(bool value)
    {
        if (CanvasControl != null)
        {
            CanvasControl.QuickMaskIndicatesSelected = value;
            CanvasControl.InvalidateVisual();
        }
    }

    partial void OnQuickMaskColorChanged(Color value)
    {
        if (CanvasControl != null)
        {
            CanvasControl.QuickMaskOverlayColor = value;
            CanvasControl.InvalidateVisual();
        }
    }

    partial void OnQuickMaskOpacityChanged(double value)
    {
        if (CanvasControl != null)
        {
            CanvasControl.QuickMaskOverlayOpacity = value / 100.0;
            CanvasControl.InvalidateVisual();
        }
    }

    [RelayCommand]
    public void OpenChannelOptions(DocumentChannelItem? channel = null)
    {
        var target = channel ?? SelectedChannel;
        if (target == null || target.IsComposite) return;

        ChannelOptionsTarget = target;
        ChannelOptionsDialogTitle = "Channel Options";
        ChannelOptionsName = target.Name;
        ChannelOptionsIndicates = target.IsSpot ? "spot" : target.ColorIndicates;
        ChannelOptionsColor = target.ChannelColor;
        ChannelOptionsOpacity = target.IsSpot ? target.Solidity : target.Opacity;
        IsChannelOptionsNameVisible = true;
        IsChannelOptionsSpotVisible = true;
        ChannelOptionsOpacityLabel = ChannelOptionsIndicates == "spot" ? "Solidity:" : "Opacity:";

        IsChannelOptionsDialogOpen = true;
    }

    [RelayCommand]
    public void OpenQuickMaskOptions()
    {
        ChannelOptionsTarget = null;
        ChannelOptionsDialogTitle = "Quick Mask Options";
        ChannelOptionsName = "Quick Mask";
        ChannelOptionsIndicates = QuickMaskIndicatesSelected ? "selected" : "masked";
        ChannelOptionsColor = QuickMaskColor;
        ChannelOptionsOpacity = QuickMaskOpacity;
        IsChannelOptionsNameVisible = false;
        IsChannelOptionsSpotVisible = false;
        ChannelOptionsOpacityLabel = "Opacity:";

        IsChannelOptionsDialogOpen = true;
    }

    [RelayCommand]
    public void SetChannelIndicates(string indicates)
    {
        ChannelOptionsIndicates = indicates;
        ChannelOptionsOpacityLabel = indicates == "spot" ? "Solidity:" : "Opacity:";
    }

    [RelayCommand]
    public void PickChannelOptionsColor()
    {
        OpenColorPicker(ChannelOptionsColor, c =>
        {
            ChannelOptionsColor = c;
        });
    }

    [RelayCommand]
    public void ConfirmChannelOptions()
    {
        if (ChannelOptionsTarget != null)
        {
            var target = ChannelOptionsTarget;
            target.Name = string.IsNullOrWhiteSpace(ChannelOptionsName) ? target.Name : ChannelOptionsName.Trim();
            target.ColorIndicates = ChannelOptionsIndicates;
            target.ChannelColor = ChannelOptionsColor;
            if (ChannelOptionsIndicates == "spot")
            {
                target.IsSpot = true;
                target.Solidity = ChannelOptionsOpacity;
            }
            else
            {
                target.IsSpot = false;
                target.Opacity = ChannelOptionsOpacity;
            }
            StatusMessage = $"Updated channel options for '{target.Name}'.";
            CanvasControl?.InvalidateVisual();
        }
        else
        {
            QuickMaskIndicatesSelected = ChannelOptionsIndicates == "selected";
            QuickMaskColor = ChannelOptionsColor;
            QuickMaskOpacity = ChannelOptionsOpacity;
            StatusMessage = "Updated Quick Mask options.";
            CanvasControl?.InvalidateVisual();
        }

        IsChannelOptionsDialogOpen = false;
    }

    [RelayCommand]
    public void CancelChannelOptions()
    {
        IsChannelOptionsDialogOpen = false;
    }

    // ── 16. New Spot Channel Modal Dialog (#3761 parity) ─────────────────────
    [ObservableProperty] private bool _isNewSpotChannelDialogOpen;
    [ObservableProperty] private string _newSpotChannelName = "Spot Color 1";
    [ObservableProperty] private Color _newSpotChannelColor = Color.FromRgb(255, 0, 0);
    [ObservableProperty] private double _newSpotChannelSolidity = 0.0;

    [RelayCommand]
    public void OpenNewSpotChannelDialog()
    {
        int spotCount = DocumentChannels.Count(c => c.IsSpot) + 1;
        NewSpotChannelName = $"Spot Color {spotCount}";
        IsNewSpotChannelDialogOpen = true;
    }

    [RelayCommand]
    public void PickNewSpotChannelColor()
    {
        OpenColorPicker(NewSpotChannelColor, c =>
        {
            NewSpotChannelColor = c;
        });
    }

    [RelayCommand]
    public void ConfirmNewSpotChannel()
    {
        int docW = (int)Math.Max(1, CanvasWidth);
        int docH = (int)Math.Max(1, CanvasHeight);

        byte[] maskToSave = new byte[docW * docH];
        if (CanvasControl?.QuickMaskBuffer != null && CanvasControl.QuickMaskBuffer.Length >= docW * docH)
        {
            Array.Copy(CanvasControl.QuickMaskBuffer, maskToSave, docW * docH);
        }

        var channelItem = new DocumentChannelItem
        {
            Name = string.IsNullOrWhiteSpace(NewSpotChannelName) ? "Spot Color 1" : NewSpotChannelName.Trim(),
            Shortcut = $"Ctrl+{Math.Min(9, 5 + DocumentChannels.Count(c => c.IsAlpha || c.IsSpot))}",
            IsComposite = false,
            IsAlpha = false,
            IsSpot = true,
            ColorIndicates = "spot",
            ChannelColor = NewSpotChannelColor,
            Solidity = NewSpotChannelSolidity,
            IsVisible = true,
            Width = docW,
            Height = docH,
            MaskData = maskToSave
        };

        DocumentChannels.Add(channelItem);
        SelectedChannel = channelItem;
        StatusMessage = $"Created spot channel '{channelItem.Name}'.";
        IsNewSpotChannelDialogOpen = false;
        CanvasControl?.InvalidateVisual();
    }

    [RelayCommand]
    public void CancelNewSpotChannel()
    {
        IsNewSpotChannelDialogOpen = false;
    }

    // ── 17. Studio Print Dialog & System Spooler Engine (#4748 parity) ────────
    [ObservableProperty] private bool _isPrintDialogOpen;
    [ObservableProperty] private ObservableCollection<string> _availablePrinters = new();
    [ObservableProperty] private string _selectedPrinter = "Default Printer";
    [ObservableProperty] private int _printCopies = 1;
    [ObservableProperty] private ObservableCollection<string> _availablePapers = new()
    {
        "Letter", "Legal", "Tabloid", "A4", "A3", "A5", "B3", "B4", "4x6", "5x7"
    };
    [ObservableProperty] private string _selectedPaper = "Letter";
    [ObservableProperty] private string _printOrientation = "Portrait";
    [ObservableProperty] private string _printColorHandling = "Printer Manages Colors";
    [ObservableProperty] private ObservableCollection<string> _availablePrinterProfiles = new()
    {
        "Coated CMYK", "sRGB IEC61966-2.1", "Adobe RGB (1998) compatible", "Display P3", "Gray Gamma 2.2"
    };
    [ObservableProperty] private string _selectedPrinterProfile = "Coated CMYK";
    [ObservableProperty] private string _printRenderingIntent = "Relative Colorimetric";
    [ObservableProperty] private bool _printBpc = true;
    [ObservableProperty] private bool _printCenter = true;
    [ObservableProperty] private double _printTopPt = 0.0;
    [ObservableProperty] private double _printLeftPt = 0.0;
    [ObservableProperty] private double _printScalePercent = 100.0;
    [ObservableProperty] private bool _printScaleToFit = false;
    [ObservableProperty] private bool _printCornerCropMarks = false;
    [ObservableProperty] private bool _printCenterCropMarks = false;
    [ObservableProperty] private bool _printRegistrationMarks = false;
    [ObservableProperty] private bool _printDescription = false;
    [ObservableProperty] private bool _printLabels = false;
    [ObservableProperty] private bool _printEmulsionDown = false;
    [ObservableProperty] private string _printScaleMetricsLabel = "";

    // Live preview properties for PrintDialog.axaml
    [ObservableProperty] private double _printPreviewPaperWidth = 240;
    [ObservableProperty] private double _printPreviewPaperHeight = 310;
    [ObservableProperty] private double _printPreviewImageLeft = 20;
    [ObservableProperty] private double _printPreviewImageTop = 20;
    [ObservableProperty] private double _printPreviewImageWidth = 200;
    [ObservableProperty] private double _printPreviewImageHeight = 270;
    [ObservableProperty] private double _printPreviewScaleX = 1.0;
    [ObservableProperty] private Geometry? _printPreviewCropMarksGeometry;
    [ObservableProperty] private Geometry? _printPreviewRegMarksGeometry;
    [ObservableProperty] private Bitmap? _printPreviewBitmapSource;

    public bool IsFryImageColorManagement => PrintColorHandling.Contains("FryImage");

    partial void OnPrintColorHandlingChanged(string value)
    {
        OnPropertyChanged(nameof(IsFryImageColorManagement));
    }

    partial void OnSelectedPaperChanged(string value) => UpdatePrintLayout();
    partial void OnPrintOrientationChanged(string value) => UpdatePrintLayout();
    partial void OnPrintCenterChanged(bool value) => UpdatePrintLayout();
    partial void OnPrintTopPtChanged(double value) => UpdatePrintLayout();
    partial void OnPrintLeftPtChanged(double value) => UpdatePrintLayout();
    partial void OnPrintScalePercentChanged(double value) => UpdatePrintLayout();
    partial void OnPrintScaleToFitChanged(bool value) => UpdatePrintLayout();
    partial void OnPrintCornerCropMarksChanged(bool value) => UpdatePrintLayout();
    partial void OnPrintRegistrationMarksChanged(bool value) => UpdatePrintLayout();
    partial void OnPrintEmulsionDownChanged(bool value) => UpdatePrintLayout();

    [RelayCommand]
    public void OpenPrintDialog()
    {
        AvailablePrinters.Clear();
        var sysPrinters = PrintEngine.GetSystemPrinters();
        string defPrinter = "";
        foreach (var p in sysPrinters)
        {
            string label = p.Name.Replace('_', ' ');
            if (p.IsDefault)
            {
                label = $"Default printer ({label})";
                defPrinter = label;
            }
            AvailablePrinters.Add(label);
        }
        SelectedPrinter = !string.IsNullOrEmpty(defPrinter) ? defPrinter : AvailablePrinters.FirstOrDefault() ?? "Default Printer";

        UpdatePrintLayout();
        IsPrintDialogOpen = true;
    }

    [RelayCommand]
    public void PrintOneCopy()
    {
        PrintCopies = 1;
        ConfirmPrint();
    }

    public void UpdatePrintLayout()
    {
        double docW = Math.Max(1, CanvasWidth);
        double docH = Math.Max(1, CanvasHeight);
        var marks = new PrintMarks(PrintCornerCropMarks, PrintCenterCropMarks, PrintRegistrationMarks, PrintDescription, PrintLabels);

        var layout = PrintEngine.ComputeLayout(
            docW, docH, 300.0, SelectedPaper, PrintOrientation, PrintCenter, PrintLeftPt, PrintTopPt,
            PrintScalePercent, PrintScaleToFit, PrintEmulsionDown, marks
        );

        PrintScaleMetricsLabel = layout.ScaleAndDimensionsLabel;

        // Scale to preview area (max ~280x340)
        double maxPreviewW = 280;
        double maxPreviewH = 340;
        double k = Math.Min(maxPreviewW / layout.PaperWidthPt, maxPreviewH / layout.PaperHeightPt);

        PrintPreviewPaperWidth = Math.Round(layout.PaperWidthPt * k);
        PrintPreviewPaperHeight = Math.Round(layout.PaperHeightPt * k);
        PrintPreviewImageWidth = Math.Max(4, Math.Round(layout.ImageWidthPt * k));
        PrintPreviewImageHeight = Math.Max(4, Math.Round(layout.ImageHeightPt * k));
        PrintPreviewImageLeft = Math.Round(layout.ImageXPt * k);
        PrintPreviewImageTop = Math.Round(layout.ImageYPt * k);
        PrintPreviewScaleX = PrintEmulsionDown ? -1.0 : 1.0;

        // Crop marks geometry
        if (PrintCornerCropMarks)
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                double x = PrintPreviewImageLeft;
                double y = PrintPreviewImageTop;
                double w = PrintPreviewImageWidth;
                double h = PrintPreviewImageHeight;
                double len = 8;
                double gap = 2;
                // TL
                ctx.BeginFigure(new Point(x - len, y), false); ctx.LineTo(new Point(x - gap, y));
                ctx.BeginFigure(new Point(x, y - len), false); ctx.LineTo(new Point(x, y - gap));
                // TR
                ctx.BeginFigure(new Point(x + w + gap, y), false); ctx.LineTo(new Point(x + w + len, y));
                ctx.BeginFigure(new Point(x + w, y - len), false); ctx.LineTo(new Point(x + w, y - gap));
                // BL
                ctx.BeginFigure(new Point(x - len, y + h), false); ctx.LineTo(new Point(x - gap, y + h));
                ctx.BeginFigure(new Point(x, y + h + gap), false); ctx.LineTo(new Point(x, y + h + len));
                // BR
                ctx.BeginFigure(new Point(x + w + gap, y + h), false); ctx.LineTo(new Point(x + w + len, y + h));
                ctx.BeginFigure(new Point(x + w, y + h + gap), false); ctx.LineTo(new Point(x + w, y + h + len));
            }
            PrintPreviewCropMarksGeometry = g;
        }
        else
        {
            PrintPreviewCropMarksGeometry = null;
        }

        // Generate thumbnail preview if not loaded
        if (PrintPreviewBitmapSource == null && CanvasControl != null)
        {
            try
            {
                var rtb = RenderCanvasToBitmap(0.25);
                if (rtb != null)
                {
                    using var ms = new MemoryStream();
                    rtb.Save(ms, new PngBitmapEncoderOptions());
                    ms.Position = 0;
                    PrintPreviewBitmapSource = new Bitmap(ms);
                }
            }
            catch { }
        }
    }

    [RelayCommand]
    public void ConfirmPrint()
    {
        try
        {
            double docW = Math.Max(1, CanvasWidth);
            double docH = Math.Max(1, CanvasHeight);
            var marks = new PrintMarks(PrintCornerCropMarks, PrintCenterCropMarks, PrintRegistrationMarks, PrintDescription, PrintLabels);
            var layout = PrintEngine.ComputeLayout(
                docW, docH, 300.0, SelectedPaper, PrintOrientation, PrintCenter, PrintLeftPt, PrintTopPt,
                PrintScalePercent, PrintScaleToFit, PrintEmulsionDown, marks
            );

            using var rtb = RenderCanvasToBitmap(1.0);
            if (rtb != null)
            {
                using var ms = new MemoryStream();
                rtb.Save(ms, new PngBitmapEncoderOptions());
                ms.Position = 0;
                using var skBmp = SKBitmap.Decode(ms);
                byte[] pdfBytes = PrintEngine.RenderPrintPdf(skBmp, layout, DocumentTitle ?? "Document");
                PrintEngine.SendToSpooler(pdfBytes, SelectedPrinter, PrintCopies);
                StatusMessage = $"Printed {PrintCopies} copy to '{SelectedPrinter}'.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print failed: {ex.Message}";
        }
        finally
        {
            IsPrintDialogOpen = false;
        }
    }

    [RelayCommand]
    public void CancelPrint()
    {
        IsPrintDialogOpen = false;
    }
}
