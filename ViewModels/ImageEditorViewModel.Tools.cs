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
    // ── Two-Column Tool Palette Layout State ───────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolPaletteWidth))]
    [NotifyPropertyChangedFor(nameof(ToolPaletteChevron))]
    private bool _isTwoColumnToolPalette;

    public double ToolPaletteWidth => IsTwoColumnToolPalette ? 68.0 : 36.0;

    public string ToolPaletteChevron => IsTwoColumnToolPalette ? "«" : "»";

    [RelayCommand]
    public void ToggleToolPaletteColumns()
    {
        IsTwoColumnToolPalette = !IsTwoColumnToolPalette;
    }

    // ── Active Sub-Tools per Toolbar Slot ──────────────────────────────────
    [ObservableProperty] private string _activeMoveSubTool = "select"; // select | artboard
    [ObservableProperty] private string _activeMarqueeSubTool = "marquee"; // marquee | ellipsemarquee | rowmarquee | colmarquee
    [ObservableProperty] private string _activeWandSubTool = "objectselection"; // objectselection | quickselection | wand
    [ObservableProperty] private string _activeCropSubTool = "crop"; // crop | perspectivecrop | slice | sliceselect
    [ObservableProperty] private string _activeEyedropperSubTool = "eyedropper"; // eyedropper | materialeyedropper | colorsampler | ruler | note | count
    [ObservableProperty] private string _activeHealingSubTool = "remove"; // remove | healing | healingbrush | patch | contentawaremove | redeye
    [ObservableProperty] private string _activeBrushSubTool = "brush"; // brush | pencil | colorreplace | mixerbrush
    [ObservableProperty] private string _activeStampSubTool = "clonestamp"; // clonestamp | patternstamp
    [ObservableProperty] private string _activeHistorySubTool = "historybrush"; // historybrush | arthistorybrush
    [ObservableProperty] private string _activeEraserSubTool = "eraser"; // eraser | bgeraser | magiceraser
    [ObservableProperty] private string _activeGradientSubTool = "gradient"; // gradient | bucket | materialdrop
    [ObservableProperty] private string _activeFocusSubTool = "blur"; // blur | sharpen | smudge
    [ObservableProperty] private string _activeToningSubTool = "dodge"; // dodge | burn | sponge
    [ObservableProperty] private string _activePenSubTool = "pen"; // pen | freepen | curvaturepen | addanchor | deleteanchor | convertpoint
    [ObservableProperty] private string _activeTypeSubTool = "text"; // text | verticaltext | verticaltextmask | textmask
    [ObservableProperty] private string _activePathSubTool = "pathselection"; // pathselection | directselection
    [ObservableProperty] private string _activeShapeSubTool = "rect"; // rect | roundedrect | ellipse | triangle | polygon | line | star
    [ObservableProperty] private string _activeHandSubTool = "hand"; // hand | rotateview

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
    private bool _brushPulledStringMode = true;

    [ObservableProperty]
    private bool _brushCatchUpOnStrokeEnd = true;

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

    // Red Eye Tool settings
    [ObservableProperty]
    private int _redEyePupilSize = 50; // 1..100%

    [ObservableProperty]
    private int _redEyeDarkenAmount = 50; // 1..100%

    // Patch Tool settings
    [ObservableProperty]
    private string _patchMode = "Source"; // Source | Destination

    // Ruler Tool measurements
    [ObservableProperty]
    private double _rulerStartX;

    [ObservableProperty]
    private double _rulerStartY;

    [ObservableProperty]
    private double _rulerEndX;

    [ObservableProperty]
    private double _rulerEndY;

    [ObservableProperty]
    private double _rulerDistance;

    [ObservableProperty]
    private double _rulerAngle;

    [ObservableProperty]
    private double _rulerDeltaX;

    [ObservableProperty]
    private double _rulerDeltaY;

    // Rotate View Tool settings
    [ObservableProperty]
    private double _viewportRotationAngle = 0.0; // -180..180 degrees

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
    public bool IsWandToolActive => ActiveToolMode is "wand" or "objectselection" or "quickselection";
    public bool IsEyedropperToolActive => ActiveToolMode == "eyedropper";
    public bool IsCropToolActive => ActiveToolMode == "crop";
    public bool IsGradientToolActive => ActiveToolMode is "gradient" or "bucket";
    public bool IsHandToolActive => ActiveToolMode == "hand";
    public bool IsZoomToolActive => ActiveToolMode == "zoom";

    // Retouch predicates
    public bool IsRemoveToolActive => ActiveToolMode == "remove";
    public bool IsCloneStampToolActive => ActiveToolMode == "clonestamp";
    public bool IsHealingToolActive => ActiveToolMode == "healing";
    public bool IsHistoryBrushToolActive => ActiveToolMode == "historybrush";
    public bool IsBlurToolActive => ActiveToolMode == "blur";
    public bool IsSharpenToolActive => ActiveToolMode == "sharpen";
    public bool IsSmudgeToolActive => ActiveToolMode == "smudge";
    public bool IsDodgeToolActive => ActiveToolMode == "dodge";
    public bool IsBurnToolActive => ActiveToolMode == "burn";
    public bool IsSpongeToolActive => ActiveToolMode == "sponge";
    public bool IsRedEyeToolActive => ActiveToolMode == "redeye";
    public bool IsPatchToolActive => ActiveToolMode == "patch";
    public bool IsBlurSharpenSmudgeActive => ActiveToolMode is "blur" or "sharpen" or "smudge";
    public bool IsDodgeBurnSpongeActive => ActiveToolMode is "dodge" or "burn" or "sponge";
    public bool IsRetouchToolActive => ActiveToolMode is "remove" or "clonestamp" or "healing" or "historybrush" or "blur" or "sharpen" or "smudge" or "dodge" or "burn" or "sponge" or "redeye" or "patch";

    // Navigation & Measurement predicates
    public bool IsRulerToolActive => ActiveToolMode == "ruler";
    public bool IsRotateViewToolActive => ActiveToolMode == "rotateview";

    // Vector Path tool predicates
    public bool IsPenToolActive => ActiveToolMode is "pen" or "freepen";
    public bool IsPathSelectionToolActive => ActiveToolMode is "pathselection" or "directselection";

    // Slot Active Predicates (Photoshop blue border highlight on active slot)
    public bool IsMoveSlotActive => ActiveToolMode is "select" or "move" or "artboard";
    public bool IsMarqueeSlotActive => ActiveToolMode is "marquee" or "rectmarquee" or "ellipsemarquee" or "rowmarquee" or "colmarquee";
    public bool IsLassoSlotActive => ActiveToolMode == "lasso";
    public bool IsWandSlotActive => ActiveToolMode is "objectselection" or "quickselection" or "wand";
    public bool IsCropSlotActive => ActiveToolMode is "crop" or "perspectivecrop" or "slice" or "sliceselect";
    public bool IsFrameSlotActive => ActiveToolMode == "frame";
    public bool IsEyedropperSlotActive => ActiveToolMode is "eyedropper" or "materialeyedropper" or "colorsampler" or "ruler" or "note" or "count";
    public bool IsHealingSlotActive => ActiveToolMode is "remove" or "healing" or "healingbrush" or "patch" or "contentawaremove" or "redeye";
    public bool IsBrushSlotActive => ActiveToolMode is "brush" or "pencil" or "colorreplace" or "mixerbrush";
    public bool IsStampSlotActive => ActiveToolMode is "clonestamp" or "patternstamp";
    public bool IsHistorySlotActive => ActiveToolMode is "historybrush" or "arthistorybrush";
    public bool IsEraserSlotActive => ActiveToolMode is "eraser" or "bgeraser" or "magiceraser";
    public bool IsGradientSlotActive => ActiveToolMode is "gradient" or "bucket" or "materialdrop";
    public bool IsFocusSlotActive => ActiveToolMode is "blur" or "sharpen" or "smudge";
    public bool IsToningSlotActive => ActiveToolMode is "dodge" or "burn" or "sponge";
    public bool IsPenSlotActive => ActiveToolMode is "pen" or "freepen" or "curvaturepen" or "addanchor" or "deleteanchor" or "convertpoint";
    public bool IsTypeSlotActive => ActiveToolMode is "text" or "verticaltext" or "verticaltextmask" or "textmask";
    public bool IsPathSlotActive => ActiveToolMode is "pathselection" or "directselection";
    public bool IsShapeSlotActive => ActiveToolMode is "rect" or "roundedrect" or "ellipse" or "triangle" or "polygon" or "line" or "star";
    public bool IsHandSlotActive => ActiveToolMode is "hand" or "rotateview";
    public bool IsZoomSlotActive => ActiveToolMode == "zoom";

    // Flyout Menu Sub-Tool Active Predicates (shows ▪ active indicator square)
    public bool IsSubMoveActive => ActiveToolMode is "select" or "move";
    public bool IsSubArtboardActive => ActiveToolMode == "artboard";

    public bool IsSubRectMarqueeActive => ActiveToolMode is "marquee" or "rectmarquee";
    public bool IsSubEllipseMarqueeActive => ActiveToolMode == "ellipsemarquee";
    public bool IsSubRowMarqueeActive => ActiveToolMode == "rowmarquee";
    public bool IsSubColMarqueeActive => ActiveToolMode == "colmarquee";

    public bool IsSubLassoActive => ActiveToolMode == "lasso" && LassoSubMode == "standard";
    public bool IsSubPolygonalLassoActive => ActiveToolMode == "lasso" && LassoSubMode == "polygonal";
    public bool IsSubMagneticLassoActive => ActiveToolMode == "lasso" && LassoSubMode == "magnetic";

    public bool IsSubObjectSelectionActive => ActiveToolMode == "objectselection";
    public bool IsSubQuickSelectionActive => ActiveToolMode == "quickselection";
    public bool IsSubMagicWandActive => ActiveToolMode == "wand";

    public bool IsSubCropActive => ActiveToolMode == "crop";
    public bool IsSubPerspectiveCropActive => ActiveToolMode == "perspectivecrop";
    public bool IsSubSliceActive => ActiveToolMode == "slice";
    public bool IsSubSliceSelectActive => ActiveToolMode == "sliceselect";

    public bool IsSubEyedropperActive => ActiveToolMode == "eyedropper";
    public bool IsSubMaterialEyedropperActive => ActiveToolMode == "materialeyedropper";
    public bool IsSubColorSamplerActive => ActiveToolMode == "colorsampler";
    public bool IsSubRulerActive => ActiveToolMode == "ruler";
    public bool IsSubNoteActive => ActiveToolMode == "note";
    public bool IsSubCountActive => ActiveToolMode == "count";

    public bool IsSubRemoveActive => ActiveToolMode == "remove";
    public bool IsSubSpotHealingActive => ActiveToolMode == "healing";
    public bool IsSubHealingBrushActive => ActiveToolMode == "healingbrush";
    public bool IsSubPatchActive => ActiveToolMode == "patch";
    public bool IsSubContentAwareMoveActive => ActiveToolMode == "contentawaremove";
    public bool IsSubRedEyeActive => ActiveToolMode == "redeye";

    public bool IsSubBrushActive => ActiveToolMode == "brush";
    public bool IsSubPencilActive => ActiveToolMode == "pencil";
    public bool IsSubColorReplaceActive => ActiveToolMode == "colorreplace";
    public bool IsSubMixerBrushActive => ActiveToolMode == "mixerbrush";

    public bool IsSubCloneStampActive => ActiveToolMode == "clonestamp";
    public bool IsSubPatternStampActive => ActiveToolMode == "patternstamp";

    public bool IsSubHistoryBrushActive => ActiveToolMode == "historybrush";
    public bool IsSubArtHistoryBrushActive => ActiveToolMode == "arthistorybrush";

    public bool IsSubEraserActive => ActiveToolMode == "eraser";
    public bool IsSubBgEraserActive => ActiveToolMode == "bgeraser";
    public bool IsSubMagicEraserActive => ActiveToolMode == "magiceraser";

    public bool IsSubGradientActive => ActiveToolMode == "gradient";
    public bool IsSubBucketActive => ActiveToolMode == "bucket";
    public bool IsSubMaterialDropActive => ActiveToolMode == "materialdrop";

    public bool IsSubBlurActive => ActiveToolMode == "blur";
    public bool IsSubSharpenActive => ActiveToolMode == "sharpen";
    public bool IsSubSmudgeActive => ActiveToolMode == "smudge";

    public bool IsSubDodgeActive => ActiveToolMode == "dodge";
    public bool IsSubBurnActive => ActiveToolMode == "burn";
    public bool IsSubSpongeActive => ActiveToolMode == "sponge";

    public bool IsSubPenActive => ActiveToolMode == "pen";
    public bool IsSubFreePenActive => ActiveToolMode == "freepen";
    public bool IsSubCurvaturePenActive => ActiveToolMode == "curvaturepen";
    public bool IsSubAddAnchorActive => ActiveToolMode == "addanchor";
    public bool IsSubDeleteAnchorActive => ActiveToolMode == "deleteanchor";
    public bool IsSubConvertPointActive => ActiveToolMode == "convertpoint";

    public bool IsSubTextActive => ActiveToolMode == "text";
    public bool IsSubVerticalTextActive => ActiveToolMode == "verticaltext";
    public bool IsSubVerticalTextMaskActive => ActiveToolMode == "verticaltextmask";
    public bool IsSubTextMaskActive => ActiveToolMode == "textmask";

    public bool IsSubPathSelectionActive => ActiveToolMode == "pathselection";
    public bool IsSubDirectSelectionActive => ActiveToolMode == "directselection";

    public bool IsSubRectActive => ActiveToolMode == "rect";
    public bool IsSubRoundedRectActive => ActiveToolMode == "roundedrect";
    public bool IsSubEllipseActive => ActiveToolMode == "ellipse";
    public bool IsSubTriangleActive => ActiveToolMode == "triangle";
    public bool IsSubPolygonActive => ActiveToolMode == "polygon";
    public bool IsSubLineActive => ActiveToolMode == "line";
    public bool IsSubStarActive => ActiveToolMode == "star";

    public bool IsSubHandActive => ActiveToolMode == "hand";
    public bool IsSubRotateViewActive => ActiveToolMode == "rotateview";

    // Slot Execution Commands (clicking slot button directly)
    [RelayCommand] public void SelectMoveSlot() => SetTool(ActiveMoveSubTool);
    [RelayCommand] public void SelectMarqueeSlot() => SetTool(ActiveMarqueeSubTool);
    [RelayCommand] public void SelectLassoSlot() => SetTool("lasso");
    [RelayCommand] public void SelectWandSlot() => SetTool(ActiveWandSubTool);
    [RelayCommand] public void SelectCropSlot() => SetTool(ActiveCropSubTool);
    [RelayCommand] public void SelectFrameSlot() => SetTool("frame");
    [RelayCommand] public void SelectEyedropperSlot() => SetTool(ActiveEyedropperSubTool);
    [RelayCommand] public void SelectHealingSlot() => SetTool(ActiveHealingSubTool);
    [RelayCommand] public void SelectBrushSlot() => SetTool(ActiveBrushSubTool);
    [RelayCommand] public void SelectStampSlot() => SetTool(ActiveStampSubTool);
    [RelayCommand] public void SelectHistorySlot() => SetTool(ActiveHistorySubTool);
    [RelayCommand] public void SelectEraserSlot() => SetTool(ActiveEraserSubTool);
    [RelayCommand] public void SelectGradientSlot() => SetTool(ActiveGradientSubTool);
    [RelayCommand] public void SelectFocusSlot() => SetTool(ActiveFocusSubTool);
    [RelayCommand] public void SelectToningSlot() => SetTool(ActiveToningSubTool);
    [RelayCommand] public void SelectPenSlot() => SetTool(ActivePenSubTool);
    [RelayCommand] public void SelectTypeSlot() => SetTool(ActiveTypeSubTool);
    [RelayCommand] public void SelectPathSlot() => SetTool(ActivePathSubTool);
    [RelayCommand] public void SelectShapeSlot() => SelectShapeTool(ActiveShapeSubTool);
    [RelayCommand] public void SelectHandSlot() => SetTool(ActiveHandSubTool);
    [RelayCommand] public void SelectZoomSlot() => SetTool("zoom");

    [RelayCommand]
    public void ActivateSubTool(string toolMode)
    {
        if (toolMode is "select" or "artboard") ActiveMoveSubTool = toolMode;
        else if (toolMode is "marquee" or "rectmarquee" or "ellipsemarquee" or "rowmarquee" or "colmarquee") ActiveMarqueeSubTool = toolMode;
        else if (toolMode is "objectselection" or "quickselection" or "wand") ActiveWandSubTool = toolMode;
        else if (toolMode is "crop" or "perspectivecrop" or "slice" or "sliceselect") ActiveCropSubTool = toolMode;
        else if (toolMode is "eyedropper" or "materialeyedropper" or "colorsampler" or "ruler" or "note" or "count") ActiveEyedropperSubTool = toolMode;
        else if (toolMode is "remove" or "healing" or "healingbrush" or "patch" or "contentawaremove" or "redeye") ActiveHealingSubTool = toolMode;
        else if (toolMode is "brush" or "pencil" or "colorreplace" or "mixerbrush") ActiveBrushSubTool = toolMode;
        else if (toolMode is "clonestamp" or "patternstamp") ActiveStampSubTool = toolMode;
        else if (toolMode is "historybrush" or "arthistorybrush") ActiveHistorySubTool = toolMode;
        else if (toolMode is "eraser" or "bgeraser" or "magiceraser") ActiveEraserSubTool = toolMode;
        else if (toolMode is "gradient" or "bucket" or "materialdrop") ActiveGradientSubTool = toolMode;
        else if (toolMode is "blur" or "sharpen" or "smudge") ActiveFocusSubTool = toolMode;
        else if (toolMode is "dodge" or "burn" or "sponge") ActiveToningSubTool = toolMode;
        else if (toolMode is "pen" or "freepen" or "curvaturepen" or "addanchor" or "deleteanchor" or "convertpoint") ActivePenSubTool = toolMode;
        else if (toolMode is "text" or "verticaltext" or "verticaltextmask" or "textmask") ActiveTypeSubTool = toolMode;
        else if (toolMode is "pathselection" or "directselection") ActivePathSubTool = toolMode;
        else if (toolMode is "rect" or "roundedrect" or "ellipse" or "triangle" or "polygon" or "line" or "star")
        {
            ActiveShapeSubTool = toolMode;
            SelectShapeTool(toolMode);
            return;
        }
        else if (toolMode is "hand" or "rotateview") ActiveHandSubTool = toolMode;

        SetTool(toolMode);
    }

    [RelayCommand]
    public void ActivateLassoSubMode(string mode)
    {
        LassoSubMode = mode;
        SetTool("lasso");
    }

    public bool HasContextualToolOptions => true;

    [RelayCommand]
    private void ClearRuler()
    {
        RulerDistance = 0;
        RulerAngle = 0;
        RulerDeltaX = 0;
        RulerDeltaY = 0;
    }

    [RelayCommand]
    private void ResetViewportRotation()
    {
        ViewportRotationAngle = 0.0;
        StatusMessage = "Canvas view rotation reset to 0°";
    }

    public void UpdateRulerMeasurement(Point start, Point end)
    {
        RulerStartX = Math.Round(start.X, 1);
        RulerStartY = Math.Round(start.Y, 1);
        RulerEndX = Math.Round(end.X, 1);
        RulerEndY = Math.Round(end.Y, 1);
        RulerDeltaX = Math.Round(end.X - start.X, 1);
        RulerDeltaY = Math.Round(end.Y - start.Y, 1);
        RulerDistance = Math.Round(Math.Sqrt(RulerDeltaX * RulerDeltaX + RulerDeltaY * RulerDeltaY), 1);
        RulerAngle = Math.Round(Math.Atan2(-RulerDeltaY, RulerDeltaX) * 180.0 / Math.PI, 1);
        StatusMessage = $"Ruler: D={RulerDistance} px, Angle={RulerAngle}°, W={RulerDeltaX} px, H={RulerDeltaY} px";
    }

    private void InvalidateAllToolProperties()
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
        OnPropertyChanged(nameof(IsRemoveToolActive));
        OnPropertyChanged(nameof(IsCloneStampToolActive));
        OnPropertyChanged(nameof(IsHealingToolActive));
        OnPropertyChanged(nameof(IsHistoryBrushToolActive));
        OnPropertyChanged(nameof(IsBlurToolActive));
        OnPropertyChanged(nameof(IsSharpenToolActive));
        OnPropertyChanged(nameof(IsSmudgeToolActive));
        OnPropertyChanged(nameof(IsDodgeToolActive));
        OnPropertyChanged(nameof(IsBurnToolActive));
        OnPropertyChanged(nameof(IsSpongeToolActive));
        OnPropertyChanged(nameof(IsRedEyeToolActive));
        OnPropertyChanged(nameof(IsPatchToolActive));
        OnPropertyChanged(nameof(IsRulerToolActive));
        OnPropertyChanged(nameof(IsRotateViewToolActive));
        OnPropertyChanged(nameof(IsBlurSharpenSmudgeActive));
        OnPropertyChanged(nameof(IsDodgeBurnSpongeActive));
        OnPropertyChanged(nameof(IsRetouchToolActive));
        OnPropertyChanged(nameof(IsPenToolActive));
        OnPropertyChanged(nameof(IsPathSelectionToolActive));
        OnPropertyChanged(nameof(HasContextualToolOptions));

        // Slot active highlights
        OnPropertyChanged(nameof(IsMoveSlotActive));
        OnPropertyChanged(nameof(IsMarqueeSlotActive));
        OnPropertyChanged(nameof(IsLassoSlotActive));
        OnPropertyChanged(nameof(IsWandSlotActive));
        OnPropertyChanged(nameof(IsCropSlotActive));
        OnPropertyChanged(nameof(IsFrameSlotActive));
        OnPropertyChanged(nameof(IsEyedropperSlotActive));
        OnPropertyChanged(nameof(IsHealingSlotActive));
        OnPropertyChanged(nameof(IsBrushSlotActive));
        OnPropertyChanged(nameof(IsStampSlotActive));
        OnPropertyChanged(nameof(IsHistorySlotActive));
        OnPropertyChanged(nameof(IsEraserSlotActive));
        OnPropertyChanged(nameof(IsGradientSlotActive));
        OnPropertyChanged(nameof(IsFocusSlotActive));
        OnPropertyChanged(nameof(IsToningSlotActive));
        OnPropertyChanged(nameof(IsPenSlotActive));
        OnPropertyChanged(nameof(IsTypeSlotActive));
        OnPropertyChanged(nameof(IsPathSlotActive));
        OnPropertyChanged(nameof(IsShapeSlotActive));
        OnPropertyChanged(nameof(IsHandSlotActive));
        OnPropertyChanged(nameof(IsZoomSlotActive));

        // Subtool indicators
        OnPropertyChanged(nameof(IsSubMoveActive));
        OnPropertyChanged(nameof(IsSubArtboardActive));
        OnPropertyChanged(nameof(IsSubRectMarqueeActive));
        OnPropertyChanged(nameof(IsSubEllipseMarqueeActive));
        OnPropertyChanged(nameof(IsSubRowMarqueeActive));
        OnPropertyChanged(nameof(IsSubColMarqueeActive));
        OnPropertyChanged(nameof(IsSubLassoActive));
        OnPropertyChanged(nameof(IsSubPolygonalLassoActive));
        OnPropertyChanged(nameof(IsSubMagneticLassoActive));
        OnPropertyChanged(nameof(IsSubObjectSelectionActive));
        OnPropertyChanged(nameof(IsSubQuickSelectionActive));
        OnPropertyChanged(nameof(IsSubMagicWandActive));
        OnPropertyChanged(nameof(IsSubCropActive));
        OnPropertyChanged(nameof(IsSubPerspectiveCropActive));
        OnPropertyChanged(nameof(IsSubSliceActive));
        OnPropertyChanged(nameof(IsSubSliceSelectActive));
        OnPropertyChanged(nameof(IsSubEyedropperActive));
        OnPropertyChanged(nameof(IsSubMaterialEyedropperActive));
        OnPropertyChanged(nameof(IsSubColorSamplerActive));
        OnPropertyChanged(nameof(IsSubRulerActive));
        OnPropertyChanged(nameof(IsSubNoteActive));
        OnPropertyChanged(nameof(IsSubCountActive));
        OnPropertyChanged(nameof(IsSubRemoveActive));
        OnPropertyChanged(nameof(IsSubSpotHealingActive));
        OnPropertyChanged(nameof(IsSubHealingBrushActive));
        OnPropertyChanged(nameof(IsSubPatchActive));
        OnPropertyChanged(nameof(IsSubContentAwareMoveActive));
        OnPropertyChanged(nameof(IsSubRedEyeActive));
        OnPropertyChanged(nameof(IsSubBrushActive));
        OnPropertyChanged(nameof(IsSubPencilActive));
        OnPropertyChanged(nameof(IsSubColorReplaceActive));
        OnPropertyChanged(nameof(IsSubMixerBrushActive));
        OnPropertyChanged(nameof(IsSubCloneStampActive));
        OnPropertyChanged(nameof(IsSubPatternStampActive));
        OnPropertyChanged(nameof(IsSubHistoryBrushActive));
        OnPropertyChanged(nameof(IsSubArtHistoryBrushActive));
        OnPropertyChanged(nameof(IsSubEraserActive));
        OnPropertyChanged(nameof(IsSubBgEraserActive));
        OnPropertyChanged(nameof(IsSubMagicEraserActive));
        OnPropertyChanged(nameof(IsSubGradientActive));
        OnPropertyChanged(nameof(IsSubBucketActive));
        OnPropertyChanged(nameof(IsSubMaterialDropActive));
        OnPropertyChanged(nameof(IsSubBlurActive));
        OnPropertyChanged(nameof(IsSubSharpenActive));
        OnPropertyChanged(nameof(IsSubSmudgeActive));
        OnPropertyChanged(nameof(IsSubDodgeActive));
        OnPropertyChanged(nameof(IsSubBurnActive));
        OnPropertyChanged(nameof(IsSubSpongeActive));
        OnPropertyChanged(nameof(IsSubPenActive));
        OnPropertyChanged(nameof(IsSubFreePenActive));
        OnPropertyChanged(nameof(IsSubCurvaturePenActive));
        OnPropertyChanged(nameof(IsSubAddAnchorActive));
        OnPropertyChanged(nameof(IsSubDeleteAnchorActive));
        OnPropertyChanged(nameof(IsSubConvertPointActive));
        OnPropertyChanged(nameof(IsSubTextActive));
        OnPropertyChanged(nameof(IsSubVerticalTextActive));
        OnPropertyChanged(nameof(IsSubVerticalTextMaskActive));
        OnPropertyChanged(nameof(IsSubTextMaskActive));
        OnPropertyChanged(nameof(IsSubPathSelectionActive));
        OnPropertyChanged(nameof(IsSubDirectSelectionActive));
        OnPropertyChanged(nameof(IsSubRectActive));
        OnPropertyChanged(nameof(IsSubRoundedRectActive));
        OnPropertyChanged(nameof(IsSubEllipseActive));
        OnPropertyChanged(nameof(IsSubTriangleActive));
        OnPropertyChanged(nameof(IsSubPolygonActive));
        OnPropertyChanged(nameof(IsSubLineActive));
        OnPropertyChanged(nameof(IsSubStarActive));
        OnPropertyChanged(nameof(IsSubHandActive));
        OnPropertyChanged(nameof(IsSubRotateViewActive));
    }

    partial void OnActiveToolModeChanged(string value)
    {
        InvalidateAllToolProperties();

        if (value != "remove" && PendingRemoveStrokes.Count > 0)
        {
            PendingRemoveStrokes.Clear();
            CanvasControl?.InvalidateVisual();
        }

        if (value == "remove")
        {
            StatusMessage = RemoveAfterEachStroke
                ? "Remove Tool (J): Paint over unwanted objects to automatically remove them."
                : $"Remove Tool (J): {PendingRemoveStrokes.Count} strokes waiting. Press Enter or ✓ to apply, Esc to cancel.";
        }
        else if (value == "redeye")
        {
            StatusMessage = "Red Eye Tool: Click directly on the red pupil to automatically neutralize and darken it.";
        }
        else if (value == "patch")
        {
            StatusMessage = "Patch Tool: Drag across the target area to seamlessly blend texture from the source.";
        }
        else if (value == "ruler")
        {
            StatusMessage = "Ruler Tool: Click and drag on the canvas to measure distances and angles.";
        }
        else if (value == "rotateview")
        {
            StatusMessage = "Rotate View Tool (R): Drag around the canvas center to rotate the camera. Shift snaps to 15°. Esc resets.";
        }

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
        SelectedElement = null;
        SelectedElements.Clear();
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
        OnPropertyChanged(nameof(IsSubLassoActive));
        OnPropertyChanged(nameof(IsSubPolygonalLassoActive));
        OnPropertyChanged(nameof(IsSubMagneticLassoActive));
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

    // ── Object Selection & Quick Selection Tool Options ─────────────────────
    [ObservableProperty] private string _objectSelectionMode = "Rectangle"; // Rectangle | Lasso
    [ObservableProperty] private bool _objectSelectionSampleAllLayers = true;
    [ObservableProperty] private bool _objectSelectionAutoEnhance = true;

    [ObservableProperty] private double _quickSelectionBrushSize = 30;
    [ObservableProperty] private double _quickSelectionHardness = 100;
    [ObservableProperty] private string _quickSelectionMode = "New"; // New | Add | Subtract

    [RelayCommand] public void SetObjectSelectionMode(string mode) => ObjectSelectionMode = mode;
    [RelayCommand] public void SetQuickSelectionMode(string mode) => QuickSelectionMode = mode;

    // ── Selection Combine Modes (New, Add, Subtract, Intersect) ──────────────
    [ObservableProperty] private string _selectionCombineMode = "New"; // New | Add | Subtract | Intersect
    [RelayCommand] public void SetSelectionCombineMode(string mode) => SelectionCombineMode = mode;

    // ── Marquee & Lasso Extended Style Options ──────────────────────────────
    [ObservableProperty] private string _marqueeStyle = "Normal"; // Normal | FixedRatio | FixedSize
    [ObservableProperty] private double _marqueeRatioW = 1.0;
    [ObservableProperty] private double _marqueeRatioH = 1.0;
    [ObservableProperty] private double _marqueeFixedW = 100.0;
    [ObservableProperty] private double _marqueeFixedH = 100.0;

    // ── Crop Tool Extended Options ──────────────────────────────────────────
    [ObservableProperty] private string _cropOverlayGrid = "RuleOfThirds"; // RuleOfThirds | Grid | GoldenRatio
    [ObservableProperty] private bool _cropDeleteCroppedPixels = true;

    [RelayCommand]
    public void StraightenCanvas()
    {
        StatusMessage = "Straighten: Click and drag a line across the horizon to level.";
    }

    // ── Move & Transform Extended Options ────────────────────────────────────
    [ObservableProperty] private string _transformAnchor = "Center"; // TopLeft | TopCenter | TopRight | MidLeft | Center | MidRight | BotLeft | BotCenter | BotRight

    [RelayCommand] public void SetTransformAnchor(string anchor) => TransformAnchor = anchor;

    // ── Modern Photoshop Contextual Task Bar State ──────────────────────────
    [ObservableProperty] private bool _isContextualTaskBarVisible = true;

    [RelayCommand]
    public void ToggleContextualTaskBar() => IsContextualTaskBarVisible = !IsContextualTaskBarVisible;

    [RelayCommand]
    public void HideContextualTaskBar() => IsContextualTaskBarVisible = false;

    // ── Keyboard Sub-Tool Cycling (Shift + Key) ─────────────────────────────
    public void CycleSubToolInSlot(string slotKey)
    {
        switch (slotKey.ToLowerInvariant())
        {
            case "wand":
            case "w":
                if (ActiveToolMode == "objectselection") ActivateSubTool("quickselection");
                else if (ActiveToolMode == "quickselection") ActivateSubTool("wand");
                else ActivateSubTool("objectselection");
                break;
            case "marquee":
            case "m":
                if (ActiveToolMode is "marquee" or "rectmarquee") ActivateSubTool("ellipsemarquee");
                else if (ActiveToolMode == "ellipsemarquee") ActivateSubTool("rowmarquee");
                else if (ActiveToolMode == "rowmarquee") ActivateSubTool("colmarquee");
                else ActivateSubTool("marquee");
                break;
            case "lasso":
            case "l":
                if (LassoSubMode == "standard") ActivateLassoSubMode("polygonal");
                else if (LassoSubMode == "polygonal") ActivateLassoSubMode("magnetic");
                else ActivateLassoSubMode("standard");
                break;
            case "brush":
            case "b":
                if (ActiveToolMode == "brush") ActivateSubTool("pencil");
                else if (ActiveToolMode == "pencil") ActivateSubTool("colorreplace");
                else ActivateSubTool("brush");
                break;
            case "eraser":
            case "e":
                if (ActiveToolMode == "eraser") ActivateSubTool("bgeraser");
                else if (ActiveToolMode == "bgeraser") ActivateSubTool("magiceraser");
                else ActivateSubTool("eraser");
                break;
            case "gradient":
            case "g":
                if (ActiveToolMode == "gradient") ActivateSubTool("bucket");
                else ActivateSubTool("gradient");
                break;
            case "crop":
            case "c":
                if (ActiveToolMode == "crop") ActivateSubTool("perspectivecrop");
                else if (ActiveToolMode == "perspectivecrop") ActivateSubTool("slice");
                else ActivateSubTool("crop");
                break;
            case "eyedropper":
            case "i":
                if (ActiveToolMode == "eyedropper") ActivateSubTool("ruler");
                else if (ActiveToolMode == "ruler") ActivateSubTool("note");
                else ActivateSubTool("eyedropper");
                break;
            case "shape":
            case "u":
                if (ActiveShapeSubTool == "rect") ActivateSubTool("roundedrect");
                else if (ActiveShapeSubTool == "roundedrect") ActivateSubTool("ellipse");
                else if (ActiveShapeSubTool == "ellipse") ActivateSubTool("polygon");
                else if (ActiveShapeSubTool == "polygon") ActivateSubTool("line");
                else ActivateSubTool("rect");
                break;
            case "pen":
            case "p":
                if (ActiveToolMode == "pen") ActivateSubTool("freepen");
                else ActivateSubTool("pen");
                break;
            case "toning":
            case "o":
                if (ActiveToolMode == "dodge") ActivateSubTool("burn");
                else if (ActiveToolMode == "burn") ActivateSubTool("sponge");
                else ActivateSubTool("dodge");
                break;
            case "focus":
            case "r":
                if (ActiveToolMode == "blur") ActivateSubTool("sharpen");
                else if (ActiveToolMode == "sharpen") ActivateSubTool("smudge");
                else ActivateSubTool("blur");
                break;
            case "healing":
            case "j":
                if (ActiveToolMode == "remove") ActivateSubTool("healing");
                else if (ActiveToolMode == "healing") ActivateSubTool("patch");
                else if (ActiveToolMode == "patch") ActivateSubTool("redeye");
                else ActivateSubTool("remove");
                break;
            case "move":
            case "v":
                if (ActiveToolMode is "select" or "move") ActivateSubTool("artboard");
                else ActivateSubTool("select");
                break;
        }
    }

    /// <summary>
    /// Rotates the brush tip by the specified degrees (wrapped into -180..180), matching Photoshop Left/Right arrow keys.
    /// </summary>
    [RelayCommand]
    public void RotateBrush(double degrees)
    {
        double newAngle = BrushAngle + degrees;
        while (newAngle > 180.0) newAngle -= 360.0;
        while (newAngle <= -180.0) newAngle += 360.0;
        BrushAngle = Math.Round(newAngle, 1);
        if (CanvasControl != null)
        {
            CanvasControl.BrushAngle = BrushAngle;
            CanvasControl.InvalidateVisual();
        }
        StatusMessage = $"Brush angle: {BrushAngle:0}°";
    }
}

