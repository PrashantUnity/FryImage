using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Persistent Guides & Snapping Engine (Studio Parity) ─────────────────
    [ObservableProperty] private bool _showGuides = true;
    [ObservableProperty] private bool _lockGuides = false;
    [ObservableProperty] private bool _snapEnabled = true;
    [ObservableProperty] private bool _snapToGuides = true;
    [ObservableProperty] private bool _snapToGrid = true;
    [ObservableProperty] private bool _snapToLayers = true;
    [ObservableProperty] private bool _snapToDocumentBounds = true;
    [ObservableProperty] private bool _showPixelGrid = true;

    [RelayCommand]
    public void TogglePixelGrid()
    {
        ShowPixelGrid = !ShowPixelGrid;
        if (CanvasControl != null) CanvasControl.ShowPixelGrid = ShowPixelGrid;
        CanvasControl?.InvalidateVisual();
        StatusMessage = ShowPixelGrid ? "Pixel Grid enabled." : "Pixel Grid disabled.";
    }

    // ── Single New Guide Modal ──────────────────────────────────────────────
    [ObservableProperty] private bool _isNewGuideDialogOpen;
    [ObservableProperty] private string _newGuideOrientation = "Horizontal"; // "Horizontal" or "Vertical"
    [ObservableProperty] private double _newGuidePosition = 100.0;

    // ── New Guide Layout Modal ──────────────────────────────────────────────
    [ObservableProperty] private bool _isNewGuideLayoutDialogOpen;
    [ObservableProperty] private bool _guideLayoutColumnsEnabled = true;
    [ObservableProperty] private int _guideLayoutColumnsCount = 12;
    [ObservableProperty] private double _guideLayoutColumnWidth = 0.0;
    [ObservableProperty] private double _guideLayoutColumnGutter = 20.0;

    [ObservableProperty] private bool _guideLayoutRowsEnabled = false;
    [ObservableProperty] private int _guideLayoutRowsCount = 6;
    [ObservableProperty] private double _guideLayoutRowHeight = 0.0;
    [ObservableProperty] private double _guideLayoutRowGutter = 20.0;

    [ObservableProperty] private bool _guideLayoutMarginsEnabled = false;
    [ObservableProperty] private double _guideLayoutMarginTop = 0.0;
    [ObservableProperty] private double _guideLayoutMarginLeft = 0.0;
    [ObservableProperty] private double _guideLayoutMarginBottom = 0.0;
    [ObservableProperty] private double _guideLayoutMarginRight = 0.0;

    [ObservableProperty] private bool _guideLayoutClearExisting = true;

    [RelayCommand]
    public void ToggleGuides()
    {
        ShowGuides = !ShowGuides;
        if (CanvasControl != null) CanvasControl.ShowUserGuides = ShowGuides;
        CanvasControl?.InvalidateVisual();
        StatusMessage = ShowGuides ? "Guides visible." : "Guides hidden.";
    }

    [RelayCommand]
    public void ToggleLockGuides()
    {
        LockGuides = !LockGuides;
        if (CanvasControl != null) CanvasControl.LockUserGuides = LockGuides;
        StatusMessage = LockGuides ? "Guides locked." : "Guides unlocked.";
    }

    [RelayCommand]
    public void ToggleSnap()
    {
        SnapEnabled = !SnapEnabled;
        if (CanvasControl != null) CanvasControl.SnapEnabled = SnapEnabled;
        StatusMessage = SnapEnabled ? "Snapping enabled." : "Snapping disabled.";
    }

    [RelayCommand]
    public void ToggleSnapToGuides()
    {
        SnapToGuides = !SnapToGuides;
        if (CanvasControl != null) CanvasControl.SnapToGuides = SnapToGuides;
    }

    [RelayCommand]
    public void ToggleSnapToGrid()
    {
        SnapToGrid = !SnapToGrid;
        if (CanvasControl != null) CanvasControl.SnapToGrid = SnapToGrid;
    }

    [RelayCommand]
    public void ToggleSnapToLayers()
    {
        SnapToLayers = !SnapToLayers;
        if (CanvasControl != null) CanvasControl.SnapToLayers = SnapToLayers;
    }

    [RelayCommand]
    public void ToggleSnapToDocumentBounds()
    {
        SnapToDocumentBounds = !SnapToDocumentBounds;
        if (CanvasControl != null) CanvasControl.SnapToDocumentBounds = SnapToDocumentBounds;
    }

    [RelayCommand]
    public void ClearGuides()
    {
        CanvasControl?.ClearUserGuides();
        StatusMessage = "Cleared all guides.";
    }

    [RelayCommand]
    public void OpenNewGuideDialog()
    {
        IsNewGuideDialogOpen = true;
    }

    [RelayCommand]
    public void CancelNewGuideDialog()
    {
        IsNewGuideDialogOpen = false;
    }

    [RelayCommand]
    public void ConfirmNewGuideDialog()
    {
        bool isVertical = string.Equals(NewGuideOrientation, "Vertical", System.StringComparison.OrdinalIgnoreCase);
        CanvasControl?.AddUserGuide(isVertical, NewGuidePosition);
        IsNewGuideDialogOpen = false;
        StatusMessage = $"Added {(isVertical ? "vertical" : "horizontal")} guide at {NewGuidePosition:0.#} px.";
    }

    [RelayCommand]
    public void OpenNewGuideLayoutDialog()
    {
        IsNewGuideLayoutDialogOpen = true;
    }

    [RelayCommand]
    public void CancelNewGuideLayoutDialog()
    {
        IsNewGuideLayoutDialogOpen = false;
    }

    [RelayCommand]
    public void ConfirmNewGuideLayoutDialog()
    {
        int cols = GuideLayoutColumnsEnabled ? GuideLayoutColumnsCount : 0;
        int rows = GuideLayoutRowsEnabled ? GuideLayoutRowsCount : 0;

        double top = GuideLayoutMarginsEnabled ? GuideLayoutMarginTop : 0;
        double left = GuideLayoutMarginsEnabled ? GuideLayoutMarginLeft : 0;
        double bottom = GuideLayoutMarginsEnabled ? GuideLayoutMarginBottom : 0;
        double right = GuideLayoutMarginsEnabled ? GuideLayoutMarginRight : 0;

        CanvasControl?.ApplyGuideLayout(
            cols,
            rows,
            GuideLayoutColumnWidth,
            GuideLayoutRowHeight,
            GuideLayoutColumnGutter,
            GuideLayoutRowGutter,
            top,
            left,
            bottom,
            right,
            GuideLayoutClearExisting);

        IsNewGuideLayoutDialogOpen = false;
        StatusMessage = "Applied new guide layout grid.";
    }
}
