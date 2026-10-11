using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Tool state ─────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _activeToolMode = "select"; // select | marquee | lasso | wand | crop | eyedropper | brush | pencil | eraser | gradient | text | rect | ellipse | triangle | star | polygon | line | arrow | hand | zoom

    [ObservableProperty]
    private double _brushThickness = 20.0;

    [ObservableProperty]
    private int _brushOpacity = 100;

    [ObservableProperty]
    private int _brushFlow = 100;

    [ObservableProperty]
    private int _brushSmoothing = 10;

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

    [ObservableProperty]
    private bool _autoSelectLayers = true;

    [ObservableProperty]
    private bool _showTransformControls = true;

    public bool IsBrushToolActive => ActiveToolMode is "brush" or "pencil";
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

    public bool HasContextualToolOptions => true;

    partial void OnActiveToolModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsBrushToolActive));
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
        OnPropertyChanged(nameof(HasContextualToolOptions));
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

    [ObservableProperty]
    private bool _isQuickMaskMode;

    [RelayCommand]
    public void ToggleQuickMask()
    {
        IsQuickMaskMode = !IsQuickMaskMode;
        StatusMessage = IsQuickMaskMode ? "Quick Mask Mode enabled." : "Standard Editing Mode.";
    }

    [RelayCommand]
    public void DeselectAll()
    {
        SelectedElement = null;
        SelectedElements.Clear();
        CanvasControl?.SetSelection(Array.Empty<CanvasElement>());
    }
}
