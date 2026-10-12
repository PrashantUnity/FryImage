using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Paint;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Tool state ─────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _activeToolMode = "select"; // select | marquee | lasso | wand | crop | eyedropper | brush | pencil | eraser | gradient | text | rect | ellipse | triangle | star | polygon | line | arrow | hand | zoom

    [ObservableProperty]
    private double _brushThickness = 24.0;

    [ObservableProperty]
    private int _brushOpacity = 100;

    [ObservableProperty]
    private int _brushFlow = 100;

    [ObservableProperty]
    private int _brushSmoothing = 15;

    [ObservableProperty]
    private bool _isHighlighterMode;

    [ObservableProperty]
    private string _selectedShapeTool = "rect";

    [ObservableProperty]
    private double _marqueeFeather = 0.0;

    [ObservableProperty]
    private bool _marqueeAntiAlias = true;

    [ObservableProperty]
    private string _cropAspectRatio = "Free"; // Free, 1:1, 16:9, 4:3, Original

    // ── Core Brush Engine State ─────────────────────────────────────────────
    [ObservableProperty]
    private BrushPreset? _selectedBrushPreset = BrushPresetLibrary.DefaultPreset;

    public IReadOnlyList<BrushPreset> AvailableBrushPresets => BrushPresetLibrary.Presets;

    public IReadOnlyList<string> BrushPresetGroups => new[]
    {
        "All",
        "General",
        "Dry Media",
        "Wet Media",
        "Special Effects"
    };

    [ObservableProperty]
    private string _selectedBrushPresetGroup = "All";

    public IEnumerable<BrushPreset> FilteredBrushPresets =>
        BrushPresetLibrary.GetPresetsByGroup(SelectedBrushPresetGroup);

    [ObservableProperty]
    private double _brushHardness = 100.0; // 0..100%

    [ObservableProperty]
    private double _brushSpacing = 25.0; // 1..100%

    [ObservableProperty]
    private double _brushRoundness = 100.0; // 5..100%

    [ObservableProperty]
    private double _brushAngle = 0.0; // -180..180

    [ObservableProperty]
    private bool _brushPressureSize;

    [ObservableProperty]
    private bool _brushPressureOpacity;

    [ObservableProperty]
    private bool _brushWetEdges;

    [ObservableProperty]
    private bool _brushNoise;

    [ObservableProperty]
    private bool _brushAirbrush;

    [ObservableProperty]
    private SymmetryMode _activeSymmetryMode = SymmetryMode.Off;

    // ── Retouching Tool State ──────────────────────────────────────────
    [ObservableProperty]
    private bool _cloneAligned = true;

    [ObservableProperty]
    private bool _cloneSampleAll = true;

    [ObservableProperty]
    private Point? _cloneSourcePoint;

    [ObservableProperty]
    private string _dodgeBurnRange = "Midtones"; // Shadows | Midtones | Highlights

    [ObservableProperty]
    private int _dodgeBurnExposure = 50; // 1..100%

    [ObservableProperty]
    private bool _dodgeBurnProtectTones = true;

    [ObservableProperty]
    private string _spongeMode = "Desaturate"; // Desaturate | Saturate

    [ObservableProperty]
    private int _spongeFlow = 50; // 1..100%

    [ObservableProperty]
    private bool _spongeVibrance = true;

    [ObservableProperty]
    private int _retouchStrength = 50; // 1..100% for Blur/Sharpen/Smudge

    [ObservableProperty]
    private bool _protectDetail = true;

    [ObservableProperty]
    private bool _fingerPainting;

    public bool IsSymmetryActive => ActiveSymmetryMode != SymmetryMode.Off;
    public bool IsBrushToolActive => ActiveToolMode is "brush" or "pencil";
    public bool IsStandardBrushToolActive => ActiveToolMode == "brush";
    public bool IsPencilToolActive => ActiveToolMode == "pencil";
    public bool IsEraserToolActive => ActiveToolMode == "eraser";
    public bool IsShapeToolActive => ActiveToolMode is "rect" or "ellipse" or "triangle" or "star" or "polygon" or "line" or "arrow";
    public bool IsTextToolActive => ActiveToolMode == "text";
    public bool IsSelectToolActive => ActiveToolMode is "select" or "move";
    public bool IsMarqueeToolActive => ActiveToolMode is "marquee" or "rectmarquee" or "ellipsemarquee";
    public bool IsLassoToolActive => ActiveToolMode == "lasso";
    public bool IsWandToolActive => ActiveToolMode == "wand";
    public bool IsEyedropperToolActive => ActiveToolMode == "eyedropper";
    public bool IsCropToolActive => ActiveToolMode == "crop";
    public bool IsGradientToolActive => ActiveToolMode is "gradient" or "bucket";
    public bool IsHandToolActive => ActiveToolMode == "hand";
    public bool IsZoomToolActive => ActiveToolMode == "zoom";

    // Retouch predicates
    public bool IsCloneStampToolActive => ActiveToolMode == "clonestamp";
    public bool IsHealingToolActive => ActiveToolMode == "healing";
    public bool IsHistoryBrushToolActive => ActiveToolMode == "historybrush";
    public bool IsBlurToolActive => ActiveToolMode == "blur";
    public bool IsSharpenToolActive => ActiveToolMode == "sharpen";
    public bool IsSmudgeToolActive => ActiveToolMode == "smudge";
    public bool IsDodgeToolActive => ActiveToolMode == "dodge";
    public bool IsBurnToolActive => ActiveToolMode == "burn";
    public bool IsSpongeToolActive => ActiveToolMode == "sponge";
    public bool IsBlurSharpenSmudgeActive => ActiveToolMode is "blur" or "sharpen" or "smudge";
    public bool IsDodgeBurnSpongeActive => ActiveToolMode is "dodge" or "burn" or "sponge";
    public bool IsRetouchToolActive => ActiveToolMode is "clonestamp" or "healing" or "historybrush" or "blur" or "sharpen" or "smudge" or "dodge" or "burn" or "sponge";

    // Vector Path tool predicates
    public bool IsPenToolActive => ActiveToolMode is "pen" or "freepen";
    public bool IsPathSelectionToolActive => ActiveToolMode is "pathselection" or "directselection";

    public bool HasContextualToolOptions => true;

    partial void OnActiveToolModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsBrushToolActive));
        OnPropertyChanged(nameof(IsStandardBrushToolActive));
        OnPropertyChanged(nameof(IsPencilToolActive));
        OnPropertyChanged(nameof(IsEraserToolActive));
        OnPropertyChanged(nameof(IsShapeToolActive));
        OnPropertyChanged(nameof(IsTextToolActive));
        OnPropertyChanged(nameof(IsSelectToolActive));
        OnPropertyChanged(nameof(IsMarqueeToolActive));
        OnPropertyChanged(nameof(IsLassoToolActive));
        OnPropertyChanged(nameof(IsWandToolActive));
        OnPropertyChanged(nameof(IsEyedropperToolActive));
        OnPropertyChanged(nameof(IsCropToolActive));
        OnPropertyChanged(nameof(IsGradientToolActive));
        OnPropertyChanged(nameof(IsHandToolActive));
        OnPropertyChanged(nameof(IsZoomToolActive));
        OnPropertyChanged(nameof(IsCloneStampToolActive));
        OnPropertyChanged(nameof(IsHealingToolActive));
        OnPropertyChanged(nameof(IsHistoryBrushToolActive));
        OnPropertyChanged(nameof(IsBlurToolActive));
        OnPropertyChanged(nameof(IsSharpenToolActive));
        OnPropertyChanged(nameof(IsSmudgeToolActive));
        OnPropertyChanged(nameof(IsDodgeToolActive));
        OnPropertyChanged(nameof(IsBurnToolActive));
        OnPropertyChanged(nameof(IsSpongeToolActive));
        OnPropertyChanged(nameof(IsBlurSharpenSmudgeActive));
        OnPropertyChanged(nameof(IsDodgeBurnSpongeActive));
        OnPropertyChanged(nameof(IsRetouchToolActive));
        OnPropertyChanged(nameof(IsPenToolActive));
        OnPropertyChanged(nameof(IsPathSelectionToolActive));
        OnPropertyChanged(nameof(HasContextualToolOptions));

        if (value == "pencil")
        {
            var pencilPreset = BrushPresetLibrary.FindById("hard_pencil");
            if (pencilPreset != null) SelectBrushPreset(pencilPreset);
        }
        else if (value == "clonestamp")
        {
            StatusMessage = CloneSourcePoint.HasValue
                ? $"Clone Stamp active (Source: {Math.Round(CloneSourcePoint.Value.X)}, {Math.Round(CloneSourcePoint.Value.Y)}). Paint to stamp."
                : "Option/Alt-click on the canvas to define a source point to clone from.";
        }
        else if (value == "historybrush")
        {
            StatusMessage = "History Brush: Paint over modified pixels to restore the original/snapshot state.";
        }
        else if (value == "pen")
        {
            StatusMessage = "Pen Tool: Click to create vector anchor points, drag for smooth Bezier handles.";
        }
        else if (value == "pathselection")
        {
            StatusMessage = "Path Selection Tool: Click to select, move, or transform vector subpaths.";
        }
        else if (value == "dodge")
        {
            StatusMessage = "Dodge Tool: Lightens pixels. Hold Option/Alt to temporarily switch to Burn.";
        }
        else if (value == "burn")
        {
            StatusMessage = "Burn Tool: Darkens pixels. Hold Option/Alt to temporarily switch to Dodge.";
        }
        else if (value == "blur")
        {
            StatusMessage = "Blur Tool: Softens pixels. Hold Option/Alt to temporarily switch to Sharpen.";
        }
        else if (value == "sharpen")
        {
            StatusMessage = "Sharpen Tool: Enhances contrast and edge detail. Hold Option/Alt to switch to Blur.";
        }
        else if (value == "smudge")
        {
            StatusMessage = "Smudge Tool: Simulates dragging a finger through wet paint.";
        }
        else if (value == "sponge")
        {
            StatusMessage = $"Sponge Tool: {SpongeMode}s image saturation under strokes.";
        }
    }

    partial void OnSelectedBrushPresetGroupChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredBrushPresets));
    }

    partial void OnActiveSymmetryModeChanged(SymmetryMode value)
    {
        OnPropertyChanged(nameof(IsSymmetryActive));
        CanvasControl?.InvalidateVisual();
    }

    [RelayCommand]
    public void SelectBrushPreset(BrushPreset preset)
    {
        if (preset == null) return;
        SelectedBrushPreset = preset;
        BrushThickness = preset.Settings.Size;
        BrushHardness = preset.Settings.Hardness * 100.0;
        BrushSpacing = preset.Settings.Spacing * 100.0;
        BrushRoundness = preset.Settings.Roundness * 100.0;
        BrushAngle = preset.Settings.Angle;
        BrushOpacity = (int)(preset.Settings.Opacity * 100.0);
        BrushFlow = (int)(preset.Settings.Flow * 100.0);
        BrushSmoothing = preset.Settings.Smoothing;
        BrushWetEdges = preset.Settings.WetEdges;
        BrushNoise = preset.Settings.Noise;
        BrushAirbrush = preset.Settings.BuildUp;
        BrushPressureSize = preset.Settings.ShapeDynamics.Size.Control == BrushControl.PenPressure;
        BrushPressureOpacity = preset.Settings.Transfer.Opacity.Control == BrushControl.PenPressure;

        if (preset.Settings.Aliased && ActiveToolMode != "pencil")
        {
            ActiveToolMode = "pencil";
        }
        else if (!preset.Settings.Aliased && ActiveToolMode == "pencil")
        {
            ActiveToolMode = "brush";
        }

        CanvasControl?.InvalidateVisual();
    }

    [RelayCommand]
    public void SelectBrushPresetById(string id)
    {
        var preset = BrushPresetLibrary.FindById(id);
        if (preset != null) SelectBrushPreset(preset);
    }

    [RelayCommand]
    public void SetBrushGroup(string group)
    {
        SelectedBrushPresetGroup = group;
    }

    [RelayCommand]
    public void SetSymmetryModeString(string modeName)
    {
        if (Enum.TryParse<SymmetryMode>(modeName, true, out var mode))
        {
            ActiveSymmetryMode = mode;
        }
    }

    [RelayCommand]
    public void CycleSymmetryMode()
    {
        ActiveSymmetryMode = ActiveSymmetryMode switch
        {
            SymmetryMode.Off => SymmetryMode.Vertical,
            SymmetryMode.Vertical => SymmetryMode.Horizontal,
            SymmetryMode.Horizontal => SymmetryMode.Dual,
            SymmetryMode.Dual => SymmetryMode.Radial,
            _ => SymmetryMode.Off
        };
    }

    public void AdjustBrushSize(double delta)
    {
        BrushThickness = Math.Clamp(Math.Round(BrushThickness + delta), 1.0, 500.0);
    }

    public void AdjustBrushHardness(double delta)
    {
        BrushHardness = Math.Clamp(Math.Round(BrushHardness + delta), 0.0, 100.0);
    }

    /// <summary>
    /// Builds a current snapshot of BrushSettings reflecting all tool options.
    /// </summary>
    public BrushSettings GetCurrentBrushSettings()
    {
        var basePreset = SelectedBrushPreset ?? BrushPresetLibrary.DefaultPreset;
        var settings = basePreset.Settings.Clone();

        settings.Size = BrushThickness;
        settings.Hardness = BrushHardness / 100.0;
        settings.Spacing = Math.Max(0.02, BrushSpacing / 100.0);
        settings.Roundness = Math.Clamp(BrushRoundness / 100.0, 0.05, 1.0);
        settings.Angle = BrushAngle;
        settings.Opacity = BrushOpacity / 100.0;
        settings.Flow = BrushFlow / 100.0;
        settings.Smoothing = BrushSmoothing;
        settings.Aliased = ActiveToolMode == "pencil" || basePreset.Settings.Aliased;
        settings.WetEdges = BrushWetEdges;
        settings.Noise = BrushNoise;
        settings.BuildUp = BrushAirbrush;

        if (BrushPressureSize)
        {
            settings.ShapeDynamics.Enabled = true;
            settings.ShapeDynamics.Size = new BrushDynamic(0.0, BrushControl.PenPressure, minimum: 0.2);
        }

        if (BrushPressureOpacity)
        {
            settings.Transfer.Enabled = true;
            settings.Transfer.Opacity = new BrushDynamic(0.0, BrushControl.PenPressure, minimum: 0.1);
        }

        return settings;
    }

    [RelayCommand]
    public void SelectShapeTool(string shape)
    {
        SelectedShapeTool = shape;
        ActiveToolMode = shape;
    }

    [RelayCommand]
    public void ToggleHighlighterMode()
    {
        IsHighlighterMode = !IsHighlighterMode;
    }

    [RelayCommand]
    public void SetTool(string toolMode)
    {
        ActiveToolMode = toolMode;
        if (toolMode != "select")
        {
            SelectedElement = null;
            SelectedElements.Clear();
        }
    }

    [RelayCommand]
    public void SetTextAlignLeft() => TextAlignmentField = Avalonia.Media.TextAlignment.Left;

    [RelayCommand]
    public void SetTextAlignCenter() => TextAlignmentField = Avalonia.Media.TextAlignment.Center;

    [RelayCommand]
    public void SetTextAlignRight() => TextAlignmentField = Avalonia.Media.TextAlignment.Right;

    // Move Tool Options
    [ObservableProperty] private bool _autoSelectLayers = true;
    [ObservableProperty] private string _autoSelectTarget = "Layer";
    [ObservableProperty] private bool _showTransformControls = true;
    public IReadOnlyList<string> AvailableAutoSelectTargets { get; } = ["Layer", "Group"];

    partial void OnAutoSelectLayersChanged(bool value)
    {
        if (CanvasControl != null)
        {
            CanvasControl.AutoSelectLayers = value;
            CanvasControl.InvalidateVisual();
        }
    }

    partial void OnAutoSelectTargetChanged(string value)
    {
        if (CanvasControl != null)
        {
            CanvasControl.AutoSelectTarget = value;
            CanvasControl.InvalidateVisual();
        }
    }

    partial void OnShowTransformControlsChanged(bool value)
    {
        if (CanvasControl != null)
        {
            CanvasControl.ShowTransformControls = value;
            CanvasControl.InvalidateVisual();
        }
    }

    [ObservableProperty]
    private bool _isQuickMaskMode;

    partial void OnIsQuickMaskModeChanged(bool value)
    {
        if (CanvasControl != null)
        {
            CanvasControl.IsQuickMaskMode = value;
            if (value)
            {
                int w = (int)Math.Max(1, CanvasWidth);
                int h = (int)Math.Max(1, CanvasHeight);
                if (CanvasControl.QuickMaskBuffer == null || CanvasControl.QuickMaskBuffer.Length != w * h)
                {
                    var buf = new byte[w * h];
                    Array.Fill(buf, (byte)255);
                    CanvasControl.QuickMaskBuffer = buf;
                }
            }
            CanvasControl.InvalidateVisual();
        }
        StatusMessage = value ? "Quick Mask Mode enabled (Paint with Black to mask, White to reveal)." : "Standard Editing Mode.";
    }

    [RelayCommand]
    public void ToggleQuickMask()
    {
        IsQuickMaskMode = !IsQuickMaskMode;
    }

    [RelayCommand]
    public void SetCloneSource(Point p)
    {
        CloneSourcePoint = p;
        StatusMessage = $"Clone source defined at ({Math.Round(p.X)}, {Math.Round(p.Y)}). Drag on canvas to paint cloned content.";
        CanvasControl?.InvalidateVisual();
    }

    [RelayCommand]
    public void ClearCloneSource()
    {
        CloneSourcePoint = null;
        StatusMessage = "Clone source cleared. Option/Alt-click to set a new source point.";
        CanvasControl?.InvalidateVisual();
    }

    [RelayCommand]
    public void SetDodgeBurnRangeString(string range)
    {
        DodgeBurnRange = range;
    }

    [RelayCommand]
    public void SetSpongeModeString(string mode)
    {
        SpongeMode = mode;
    }

    [RelayCommand]
    public void DeselectAll()
    {
        SelectedElement = null;
        SelectedElements.Clear();
        CanvasControl?.SetSelection(Array.Empty<CanvasElement>());
    }

    // ── Lasso Tool Options ───────────────────────────────────────────────────
    [ObservableProperty] private string _lassoSubMode = "standard"; // standard | polygonal | magnetic
    [ObservableProperty] private double _magneticLassoWidth = 10;
    [ObservableProperty] private double _magneticLassoEdgeContrast = 10;
    [ObservableProperty] private double _magneticLassoFrequency = 57;

    public bool IsStandardLassoMode => LassoSubMode == "standard";
    public bool IsPolygonalLassoMode => LassoSubMode == "polygonal";
    public bool IsMagneticLassoMode => LassoSubMode == "magnetic";

    partial void OnLassoSubModeChanged(string value)
    {
        if (CanvasControl != null)
        {
            CanvasControl.LassoMode = value;
        }
        OnPropertyChanged(nameof(IsStandardLassoMode));
        OnPropertyChanged(nameof(IsPolygonalLassoMode));
        OnPropertyChanged(nameof(IsMagneticLassoMode));
    }

    partial void OnMagneticLassoWidthChanged(double value)
    {
        if (CanvasControl != null) CanvasControl.MagneticWidth = value;
    }

    partial void OnMagneticLassoEdgeContrastChanged(double value)
    {
        if (CanvasControl != null) CanvasControl.MagneticContrast = value;
    }

    partial void OnMagneticLassoFrequencyChanged(double value)
    {
        if (CanvasControl != null) CanvasControl.MagneticFrequency = value;
    }

    [RelayCommand]
    public void SetLassoSubModeString(string mode)
    {
        LassoSubMode = mode;
    }

    // ── Magic Wand Tool Options ─────────────────────────────────────────────
    [ObservableProperty] private double _magicWandTolerance = 32;
    [ObservableProperty] private bool _magicWandAntiAlias = true;
    [ObservableProperty] private bool _magicWandContiguous = true;
    [ObservableProperty] private bool _magicWandSampleAllLayers;

    partial void OnMagicWandToleranceChanged(double value)
    {
        if (CanvasControl != null) CanvasControl.WandTolerance = value;
    }

    partial void OnMagicWandAntiAliasChanged(bool value)
    {
        if (CanvasControl != null) CanvasControl.WandAntiAlias = value;
    }

    partial void OnMagicWandContiguousChanged(bool value)
    {
        if (CanvasControl != null) CanvasControl.WandContiguous = value;
    }

    partial void OnMagicWandSampleAllLayersChanged(bool value)
    {
        if (CanvasControl != null) CanvasControl.WandSampleAllLayers = value;
    }
}
