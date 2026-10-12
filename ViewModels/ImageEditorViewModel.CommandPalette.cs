using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Paint;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    [ObservableProperty]
    private string _commandPaletteQuery = string.Empty;

    [ObservableProperty]
    private StudioCommand? _selectedStudioCommand;

    public ObservableCollection<StudioCommand> AllStudioCommands { get; } = new();
    public ObservableCollection<StudioCommand> FilteredStudioCommands { get; } = new();

    private void InitializeStudioCommands()
    {
        if (AllStudioCommands.Count > 0) return;

        // ── 1. Tools ─────────────────────────────────────────
        AddCmd("tool.select", "Move / Selection Tool", "Tools", "V", "CursorDefault", () => SetTool("select"));
        AddCmd("tool.marquee", "Marquee Selection Tool", "Tools", "M", "SelectionDrag", () => SetTool("marquee"));
        AddCmd("tool.lasso", "Lasso Selection Tool", "Tools", "L", "VectorPolyline", () => SetTool("lasso"));
        AddCmd("tool.wand", "Magic Wand Tool", "Tools", "W", "Wand", () => SetTool("wand"));
        AddCmd("tool.crop", "Crop Tool", "Tools", "C", "Crop", () => SetTool("crop"));
        AddCmd("tool.eyedropper", "Eyedropper Tool", "Tools", "I", "Eyedropper", () => SetTool("eyedropper"));
        AddCmd("tool.healing", "Spot Healing Brush Tool", "Tools", "J", "Bandage", () => SetTool("healing"));
        AddCmd("tool.brush", "Brush Tool", "Tools", "B", "Brush", () => SetTool("brush"));
        AddCmd("tool.pencil", "Pencil Tool", "Tools", "P", "Pencil", () => SetTool("pencil"));
        AddCmd("tool.clonestamp", "Clone Stamp Tool", "Tools", "S", "Stamp", () => SetTool("clonestamp"));
        AddCmd("tool.historybrush", "History Brush Tool", "Tools", "Y", "History", () => SetTool("historybrush"));
        AddCmd("tool.eraser", "Eraser Tool", "Tools", "E", "Eraser", () => SetTool("eraser"));
        AddCmd("tool.gradient", "Gradient Tool", "Tools", "G", "GradientHorizontal", () => SetTool("gradient"));
        AddCmd("tool.blur", "Blur / Sharpen / Smudge Tool", "Tools", "R", "Blur", () => SetTool("blur"));
        AddCmd("tool.dodge", "Dodge / Burn / Sponge Tool", "Tools", "O", "WhiteBalanceSunny", () => SetTool("dodge"));
        AddCmd("tool.pen", "Pen Tool (Vector Paths)", "Tools", "P", "VectorPen", () => SetTool("pen"));
        AddCmd("tool.pathselection", "Path Selection Tool", "Tools", "A", "CursorPointer", () => SetTool("pathselection"));
        AddCmd("tool.text", "Horizontal Type Tool", "Tools", "T", "FormatColorText", () => SetTool("text"));
        AddCmd("tool.rect", "Rectangle Tool", "Tools", "U", "RectangleOutline", () => SelectShapeTool("rect"));
        AddCmd("tool.ellipse", "Ellipse Tool", "Tools", "U", "CircleOutline", () => SelectShapeTool("ellipse"));
        AddCmd("tool.hand", "Hand Tool", "Tools", "H", "HandBackRight", () => SetTool("hand"));
        AddCmd("tool.rotateview", "Rotate View Tool", "Tools", "R", "CompassOutline", () => SetTool("rotateview"));
        AddCmd("tool.zoom", "Zoom Tool", "Tools", "Z", "Magnify", () => SetTool("zoom"));
        AddCmd("tool.patch", "Patch Tool", "Tools", "J", "VectorSelection", () => SetTool("patch"));
        AddCmd("tool.redeye", "Red Eye Tool", "Tools", "J", "EyeOutline", () => SetTool("redeye"));
        AddCmd("tool.ruler", "Ruler / Measure Tool", "Tools", "I", "Ruler", () => SetTool("ruler"));
        AddCmd("view.resetrotation", "Reset Canvas View Rotation", "View", "Esc", "Compass", ResetViewportRotation);
        AddCmd("layer.clippingmask", "Create / Release Clipping Mask", "Layer", "Ctrl+Alt+G", "FormatListGroup", () => ToggleClippingMaskCommand.Execute(null));
        AddCmd("edit.contentawarescale", "Content-Aware Scale...", "Edit", "Alt+Shift+Ctrl+C", "ArrowExpandAll", OpenContentAwareScaleDialog);

        // ── 2. Filters ───────────────────────────────────────
        AddCmd("filter.cameraraw", "Camera Raw Filter...", "Filter", "Shift+Ctrl+A", "CameraIris", () => OpenFilterDialog("CameraRaw"));
        AddCmd("filter.liquify", "Liquify (Studio Displacement Mesh)...", "Filter", "Shift+Ctrl+X", "Waves", OpenLiquifyDialog);
        AddCmd("filter.blur.tiltshift", "Tilt-Shift Blur...", "Filter", null, "BlurRadial", () => OpenFilterDialog("TiltShift"));
        AddCmd("filter.blur.iris", "Iris Blur...", "Filter", null, "EyeOutline", () => OpenFilterDialog("IrisBlur"));
        AddCmd("filter.blur.gaussian", "Gaussian Blur...", "Filter", null, "Blur", () => OpenFilterDialog("GaussianBlur"));
        AddCmd("filter.blur.motion", "Motion Blur...", "Filter", null, "MotionBlur", () => OpenFilterDialog("MotionBlur"));
        AddCmd("filter.blur.radial", "Radial Blur...", "Filter", null, "RotateRight", () => OpenFilterDialog("RadialBlur"));
        AddCmd("filter.sharpen.unsharp", "Unsharp Mask...", "Filter", null, "ShieldHalfFull", () => OpenFilterDialog("UnsharpMask"));
        AddCmd("filter.stylize.oilpaint", "Oil Paint...", "Filter", null, "PaletteSwatch", () => OpenFilterDialog("OilPaint"));
        AddCmd("filter.stylize.facet", "Facet...", "Filter", null, "DiamondOutline", () => OpenFilterDialog("Facet"));
        AddCmd("filter.distort.wave", "Wave Distortion...", "Filter", null, "Wave", () => OpenFilterDialog("Wave"));
        AddCmd("filter.distort.ripple", "Ripple Distortion...", "Filter", null, "Water", () => OpenFilterDialog("Ripple"));
        AddCmd("filter.distort.pinch", "Pinch Distortion...", "Filter", null, "ArrowCollapseAll", () => OpenFilterDialog("Pinch"));
        AddCmd("filter.distort.polar", "Polar Coordinates...", "Filter", null, "CompassOutline", () => OpenFilterDialog("PolarCoordinates"));
        AddCmd("filter.distort.twirl", "Twirl Distortion...", "Filter", null, "Restore", () => OpenFilterDialog("Twirl"));
        AddCmd("filter.distort.spherize", "Spherize Distortion...", "Filter", null, "Sphere", () => OpenFilterDialog("Spherize"));
        AddCmd("filter.pixelate.halftone", "Color Halftone...", "Filter", null, "DotsGrid", () => OpenFilterDialog("ColorHalftone"));
        AddCmd("filter.pixelate.crystallize", "Crystallize...", "Filter", null, "HexagonMultipleOutline", () => OpenFilterDialog("Crystallize"));
        AddCmd("filter.pixelate.pointillize", "Pointillize...", "Filter", null, "ScatterPlot", () => OpenFilterDialog("Pointillize"));
        AddCmd("filter.pixelate.mosaic", "Mosaic Pixelate...", "Filter", null, "ViewGridPlusOutline", () => OpenFilterDialog("Mosaic"));
        AddCmd("filter.stylize.emboss", "Emboss Relief...", "Filter", null, "TextureBox", () => OpenFilterDialog("Emboss"));
        AddCmd("filter.noise.add", "Add Noise...", "Filter", null, "Grain", () => OpenFilterDialog("AddNoise"));
        AddCmd("filter.noise.dustAndScratches", "Dust & Scratches...", "Filter", null, "Broom", () => OpenFilterDialog("DustAndScratches"));
        AddCmd("filter.noise.median", "Median Filter...", "Filter", null, "FilterVariant", () => OpenFilterDialog("Median"));
        AddCmd("filter.other.highpass", "High Pass Filter...", "Filter", null, "ContrastBox", () => OpenFilterDialog("HighPass"));
        AddCmd("filter.other.colortoalpha", "Color to Alpha...", "Filter", null, "FormatColorFill", () => OpenFilterDialog("ColorToAlpha"));
        AddCmd("automate.cropandstraighten", "Crop and Straighten Photos", "Automate", null, "CropRotate", () => CropAndStraightenPhotosCommand.Execute(null));
        AddCmd("filter.render.clouds", "Clouds...", "Filter", null, "CloudOutline", () => OpenFilterDialog("Clouds"));
        AddCmd("filter.render.lensflare", "Lens Flare...", "Filter", null, "Flare", () => OpenFilterDialog("LensFlare"));
        AddCmd("filter.render.fibers", "Fibers...", "Filter", null, "Texture", () => OpenFilterDialog("Fibers"));
        AddCmd("filter.stylize.wind", "Wind...", "Filter", null, "WeatherWindy", () => OpenFilterDialog("Wind"));
        AddCmd("filter.stylize.diffuse", "Diffuse...", "Filter", null, "Grain", () => OpenFilterDialog("Diffuse"));
        AddCmd("filter.stylize.extrude", "Extrude 3D Blocks...", "Filter", null, "CubeOutline", () => OpenFilterDialog("Extrude"));
        AddCmd("filter.stylize.tiles", "Tiles...", "Filter", null, "Grid", () => OpenFilterDialog("Tiles"));
        AddCmd("filter.stylize.tracecontour", "Trace Contour...", "Filter", null, "VectorPolygon", () => OpenFilterDialog("TraceContour"));
        AddCmd("filter.distort.zigzag", "ZigZag...", "Filter", null, "Zigzag", () => OpenFilterDialog("ZigZag"));
        AddCmd("filter.distort.shear", "Shear...", "Filter", null, "ArrowTopRightBottomLeftBold", () => OpenFilterDialog("Shear"));
        AddCmd("filter.pixelate.mezzotint", "Mezzotint...", "Filter", null, "DotsHorizontalCircleOutline", () => OpenFilterDialog("Mezzotint"));
        AddCmd("filter.sketch.waterpaper", "Water Paper...", "Filter", null, "Water", () => OpenFilterDialog("WaterPaper"));

        // ── 3. Adjustments ───────────────────────────────────
        AddCmd("adjust.brightness", "Brightness / Contrast...", "Adjustments", null, "Brightness6", () => OpenAdjustmentDialog("BrightnessContrast"));
        AddCmd("adjust.levels", "Levels...", "Adjustments", "Ctrl+L", "Tune", () => OpenAdjustmentDialog("Levels"));
        AddCmd("adjust.exposure", "Exposure...", "Adjustments", null, "Exposure", () => OpenAdjustmentDialog("Exposure"));
        AddCmd("adjust.curves", "Curves...", "Adjustments", "Ctrl+M", "ChartTimelineVariantShaped", () => OpenAdjustmentDialog("Curves"));
        AddCmd("adjust.huesat", "Hue / Saturation...", "Adjustments", "Ctrl+U", "Palette", () => OpenAdjustmentDialog("HueSaturation"));
        AddCmd("adjust.vibrance", "Vibrance...", "Adjustments", null, "Sparkles", () => OpenAdjustmentDialog("Vibrance"));
        AddCmd("adjust.colorbalance", "Color Balance...", "Adjustments", "Ctrl+B", "ScaleBalance", () => OpenAdjustmentDialog("ColorBalance"));
        AddCmd("adjust.selectivecolor", "Selective Color...", "Adjustments", null, "SelectColor", () => OpenAdjustmentDialog("SelectiveColor"));
        AddCmd("adjust.colorlookup", "Color Lookup (3D LUT)...", "Adjustments", null, "TableFilter", () => OpenAdjustmentDialog("ColorLookup"));
        AddCmd("adjust.photofilter", "Photo Filter...", "Adjustments", null, "CameraFilter", () => OpenAdjustmentDialog("PhotoFilter"));
        AddCmd("adjust.gradientmap", "Gradient Map...", "Adjustments", null, "GradientHorizontal", () => OpenAdjustmentDialog("GradientMap"));
        AddCmd("adjust.threshold", "Threshold...", "Adjustments", null, "CircleHalfFull", () => OpenAdjustmentDialog("Threshold"));
        AddCmd("adjust.posterize", "Posterize...", "Adjustments", null, "ChartBarStacked", () => OpenAdjustmentDialog("Posterize"));
        AddCmd("adjust.autotone", "Auto Tone", "Adjustments", "Shift+Ctrl+L", "AutoFix", ApplyAutoTone);
        AddCmd("adjust.autocontrast", "Auto Contrast", "Adjustments", "Alt+Shift+Ctrl+L", "Contrast", ApplyAutoContrast);
        AddCmd("adjust.autocolor", "Auto Color", "Adjustments", "Shift+Ctrl+B", "FormatColorFill", ApplyAutoColor);
        AddCmd("adjust.equalize", "Equalize Histogram", "Adjustments", null, "Equalizer", ApplyEqualize);
        AddCmd("adjust.invert", "Invert Colors", "Adjustments", "Ctrl+I", "InvertColors", ApplyInvert);

        // ── Selection & Masks ────────────────────────────────
        AddCmd("select.colorrange", "Color Range...", "Select", null, "Eyedropper", OpenColorRangeDialog);
        AddCmd("select.andmask", "Select and Mask...", "Select", "Ctrl+Alt+R", "SelectDrag", OpenSelectAndMaskDialog);
        AddCmd("select.quickmask", "Quick Mask Mode", "Select", "Q", "CircleHalfFull", ToggleQuickMask);
        AddCmd("select.all", "Select All Elements", "Select", "Ctrl+A", "SelectAll", SelectAllElements);
        AddCmd("select.deselect", "Deselect All", "Select", "Ctrl+D", "SelectOff", DeselectAll);
        AddCmd("select.inverse", "Invert Selection Mask", "Select", "Shift+Ctrl+I", "SelectInverse", InvertSelection);
        AddCmd("select.makeworkpath", "Make Work Path from Selection...", "Select", null, "VectorPen", () => MakeWorkPathFromSelectionCommand.Execute(null));

        // ── Color Swatches ───────────────────────────────────
        AddCmd("swatches.import", "Import Swatches (.aco, .ase)...", "Color", null, "FileImportOutline", () => _ = ImportSwatches());
        AddCmd("swatches.export", "Export Swatches (.aco)...", "Color", null, "FileExportOutline", () => _ = ExportSwatches());
        AddCmd("swatches.reset", "Reset Default Swatches", "Color", null, "Restore", ResetDefaultSwatches);

        // ── 4. Layers & Styling ──────────────────────────────
        AddCmd("layer.duplicate", "Duplicate Layer", "Layer", "Ctrl+J", "ContentCopy", Duplicate);
        AddCmd("layer.delete", "Delete Layer", "Layer", "Del", "DeleteOutline", DeleteSelectedElement);
        AddCmd("layer.style", "Layer Style (fx)...", "Layer", null, "StarFace", OpenLayerStyleDialog);
        AddCmd("layer.bringforward", "Bring Forward", "Layer", "Ctrl+]", "ArrangeBringForward", BringForward);
        AddCmd("layer.sendbackward", "Send Backward", "Layer", "Ctrl+[", "ArrangeSendBackward", SendBackward);
        AddCmd("layer.bringtofront", "Bring to Front", "Layer", "Ctrl+Shift+]", "ArrangeBringToFront", BringToFront);
        AddCmd("layer.sendtoback", "Send to Back", "Layer", "Ctrl+Shift+[", "ArrangeSendToBack", SendToBack);

        // ── 5. Brush Settings & Panels ───────────────────────
        AddCmd("window.brushsettings", "Brush Settings Panel", "Window", "F5", "BrushVariant", () => SelectRightDockTab("Brushes"));
        AddCmd("window.layers", "Layers Panel", "Window", "F7", "LayersTripleOutline", () => SelectRightDockTab("Layers"));
        AddCmd("window.color", "Color Panel", "Window", "F6", "PaletteOutline", () => SelectRightDockTab("Color"));
        AddCmd("window.properties", "Properties Panel", "Window", null, "TuneVertical", () => SelectRightDockTab("Properties"));
        AddCmd("brush.symmetry.off", "Symmetry: Off", "Brush", null, "VectorLine", () => ActiveSymmetryMode = SymmetryMode.Off);
        AddCmd("brush.symmetry.vertical", "Symmetry: Vertical", "Brush", null, "FlipHorizontal", () => ActiveSymmetryMode = SymmetryMode.Vertical);
        AddCmd("brush.symmetry.horizontal", "Symmetry: Horizontal", "Brush", null, "FlipVertical", () => ActiveSymmetryMode = SymmetryMode.Horizontal);
        AddCmd("brush.symmetry.dual", "Symmetry: Dual Axis (4-Quadrant)", "Brush", null, "CrosshairsGps", () => ActiveSymmetryMode = SymmetryMode.Dual);
        AddCmd("brush.symmetry.radial", "Symmetry: Radial (Mandala)", "Brush", null, "RadiusOutline", () => ActiveSymmetryMode = SymmetryMode.Radial);

        // ── 6. Edit & History ────────────────────────────────
        AddCmd("edit.undo", "Undo", "Edit", "Ctrl+Z", "Undo", Undo);
        AddCmd("edit.redo", "Redo", "Edit", "Ctrl+Y", "Redo", Redo);
        AddCmd("edit.cut", "Cut", "Edit", "Ctrl+X", "ContentCut", DeleteSelectedElement);
        AddCmd("edit.copy", "Copy", "Edit", "Ctrl+C", "ContentCopy", CopyElements);
        AddCmd("edit.paste", "Paste", "Edit", "Ctrl+V", "ContentPaste", PasteElements);
        AddCmd("edit.clear", "Clear Canvas", "Edit", null, "DeleteSweep", RequestClearCanvas);

        // ── 7. View & Document ───────────────────────────────
        AddCmd("view.fit", "Fit on Screen", "View", "Ctrl+0", "FitToScreenOutline", FitToWindow);
        AddCmd("view.actual", "Actual Pixels 100%", "View", "Ctrl+1", "MagnifyScan", ResetZoom);
        AddCmd("view.zoomin", "Zoom In", "View", "Ctrl++", "MagnifyPlusOutline", ZoomIn);
        AddCmd("view.zoomout", "Zoom Out", "View", "Ctrl+-", "MagnifyMinusOutline", ZoomOut);
        AddCmd("view.rulers", "Toggle Rulers", "View", "Ctrl+R", "Ruler", ToggleRulers);
        AddCmd("view.grid", "Toggle Grid", "View", "Ctrl+'", "Grid", ToggleGrid);
        AddCmd("view.guides.toggle", "Toggle Guides", "View", "Ctrl+;", "EyeOutline", ToggleGuides);
        AddCmd("view.guides.lock", "Lock Guides", "View", "Alt+Ctrl+;", "LockOutline", ToggleLockGuides);
        AddCmd("view.guides.clear", "Clear Guides", "View", null, "DeleteOutline", ClearGuides);
        AddCmd("view.guides.new", "New Guide...", "View", null, "Ruler", OpenNewGuideDialog);
        AddCmd("view.guides.layout", "New Guide Layout...", "View", null, "GridLarge", OpenNewGuideLayoutDialog);
        AddCmd("view.snap.toggle", "Toggle Snap", "View", "Ctrl+Shift+;", "Magnet", ToggleSnap);
        AddCmd("type.warp", "Warp Text...", "Type", null, "Waves", OpenWarpTextDialog);
        AddCmd("file.exportas", "Export As...", "File", "Alt+Shift+Ctrl+S", "Export", OpenExportDialog);
        AddCmd("file.new", "New Document...", "File", "Ctrl+N", "FileDocumentPlusOutline", OpenNewDocumentDialog);

        void AddCmd(string id, string title, string category, string? shortcut, string icon, Action action)
        {
            AllStudioCommands.Add(new StudioCommand
            {
                Id = id,
                Title = title,
                Category = category,
                Shortcut = shortcut,
                IconKind = icon,
                Action = action
            });
        }
    }

    partial void OnCommandPaletteQueryChanged(string value)
    {
        FilterStudioCommands();
    }

    private void FilterStudioCommands()
    {
        FilteredStudioCommands.Clear();
        string q = CommandPaletteQuery?.Trim().ToLowerInvariant() ?? string.Empty;

        var matches = string.IsNullOrEmpty(q)
            ? AllStudioCommands
            : AllStudioCommands.Where(c =>
                c.Title.ToLowerInvariant().Contains(q) ||
                c.Category.ToLowerInvariant().Contains(q) ||
                (c.Shortcut != null && c.Shortcut.ToLowerInvariant().Contains(q)));

        foreach (var cmd in matches)
        {
            FilteredStudioCommands.Add(cmd);
        }

        SelectedStudioCommand = FilteredStudioCommands.FirstOrDefault();
    }

    [RelayCommand]
    public void OpenCommandPalette()
    {
        InitializeStudioCommands();
        CommandPaletteQuery = string.Empty;
        FilterStudioCommands();
        IsCommandPaletteOpen = true;
    }

    [RelayCommand]
    public void CloseCommandPalette()
    {
        IsCommandPaletteOpen = false;
    }

    [RelayCommand]
    public void ToggleCommandPalette()
    {
        if (IsCommandPaletteOpen)
            CloseCommandPalette();
        else
            OpenCommandPalette();
    }

    [RelayCommand]
    public void ExecuteSelectedCommandPaletteItem()
    {
        if (SelectedStudioCommand is { } cmd)
        {
            CloseCommandPalette();
            cmd.Action();
            StatusMessage = $"Executed: {cmd.Title}";
        }
    }

    [RelayCommand]
    public void SelectPreviousCommandPaletteItem()
    {
        if (FilteredStudioCommands.Count == 0) return;
        int idx = SelectedStudioCommand != null ? FilteredStudioCommands.IndexOf(SelectedStudioCommand) : 0;
        idx = (idx - 1 + FilteredStudioCommands.Count) % FilteredStudioCommands.Count;
        SelectedStudioCommand = FilteredStudioCommands[idx];
    }

    [RelayCommand]
    public void SelectNextCommandPaletteItem()
    {
        if (FilteredStudioCommands.Count == 0) return;
        int idx = SelectedStudioCommand != null ? FilteredStudioCommands.IndexOf(SelectedStudioCommand) : -1;
        idx = (idx + 1) % FilteredStudioCommands.Count;
        SelectedStudioCommand = FilteredStudioCommands[idx];
    }
}
