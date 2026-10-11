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
        AddCmd("tool.eraser", "Eraser Tool", "Tools", "E", "Eraser", () => SetTool("eraser"));
        AddCmd("tool.gradient", "Gradient Tool", "Tools", "G", "GradientHorizontal", () => SetTool("gradient"));
        AddCmd("tool.blur", "Blur / Sharpen / Smudge Tool", "Tools", "R", "Blur", () => SetTool("blur"));
        AddCmd("tool.dodge", "Dodge / Burn / Sponge Tool", "Tools", "O", "WhiteBalanceSunny", () => SetTool("dodge"));
        AddCmd("tool.text", "Horizontal Type Tool", "Tools", "T", "FormatColorText", () => SetTool("text"));
        AddCmd("tool.rect", "Rectangle Tool", "Tools", "U", "RectangleOutline", () => SelectShapeTool("rect"));
        AddCmd("tool.ellipse", "Ellipse Tool", "Tools", "U", "CircleOutline", () => SelectShapeTool("ellipse"));
        AddCmd("tool.hand", "Hand Tool", "Tools", "H", "HandBackRight", () => SetTool("hand"));
        AddCmd("tool.zoom", "Zoom Tool", "Tools", "Z", "Magnify", () => SetTool("zoom"));

        // ── 2. Filters ───────────────────────────────────────
        AddCmd("filter.blur.gaussian", "Gaussian Blur...", "Filter", null, "Blur", () => OpenFilterDialog("GaussianBlur"));
        AddCmd("filter.blur.motion", "Motion Blur...", "Filter", null, "MotionBlur", () => OpenFilterDialog("MotionBlur"));
        AddCmd("filter.blur.radial", "Radial Blur...", "Filter", null, "RotateRight", () => OpenFilterDialog("RadialBlur"));
        AddCmd("filter.sharpen.unsharp", "Unsharp Mask...", "Filter", null, "ShieldHalfFull", () => OpenFilterDialog("UnsharpMask"));
        AddCmd("filter.distort.twirl", "Twirl Distortion...", "Filter", null, "Restore", () => OpenFilterDialog("Twirl"));
        AddCmd("filter.distort.spherize", "Spherize Distortion...", "Filter", null, "Sphere", () => OpenFilterDialog("Spherize"));
        AddCmd("filter.pixelate.mosaic", "Mosaic Pixelate...", "Filter", null, "ViewGridPlusOutline", () => OpenFilterDialog("Mosaic"));
        AddCmd("filter.stylize.emboss", "Emboss Relief...", "Filter", null, "TextureBox", () => OpenFilterDialog("Emboss"));
        AddCmd("filter.noise.add", "Add Noise...", "Filter", null, "Grain", () => OpenFilterDialog("AddNoise"));
        AddCmd("filter.other.highpass", "High Pass Filter...", "Filter", null, "ContrastBox", () => OpenFilterDialog("HighPass"));

        // ── 3. Adjustments ───────────────────────────────────
        AddCmd("adjust.brightness", "Brightness / Contrast...", "Adjustments", null, "Brightness6", () => OpenAdjustmentDialog("BrightnessContrast"));
        AddCmd("adjust.levels", "Levels...", "Adjustments", "Ctrl+L", "Tune", () => OpenAdjustmentDialog("Levels"));
        AddCmd("adjust.curves", "Curves...", "Adjustments", "Ctrl+M", "ChartTimelineVariantShaped", () => OpenAdjustmentDialog("Curves"));
        AddCmd("adjust.huesat", "Hue / Saturation...", "Adjustments", "Ctrl+U", "Palette", () => OpenAdjustmentDialog("HueSaturation"));
        AddCmd("adjust.vibrance", "Vibrance...", "Adjustments", null, "Sparkles", () => OpenAdjustmentDialog("Vibrance"));
        AddCmd("adjust.colorbalance", "Color Balance...", "Adjustments", "Ctrl+B", "ScaleBalance", () => OpenAdjustmentDialog("ColorBalance"));
        AddCmd("adjust.autotone", "Auto Tone", "Adjustments", "Shift+Ctrl+L", "AutoFix", ApplyAutoTone);
        AddCmd("adjust.autocontrast", "Auto Contrast", "Adjustments", "Alt+Shift+Ctrl+L", "Contrast", ApplyAutoContrast);
        AddCmd("adjust.autocolor", "Auto Color", "Adjustments", "Shift+Ctrl+B", "FormatColorFill", ApplyAutoColor);
        AddCmd("adjust.equalize", "Equalize Histogram", "Adjustments", null, "Equalizer", ApplyEqualize);
        AddCmd("adjust.invert", "Invert Colors", "Adjustments", "Ctrl+I", "InvertColors", ApplyInvert);

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
