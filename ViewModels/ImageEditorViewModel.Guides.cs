using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.ImageEditor;

public class GuideColorOption
{
    public string Name { get; set; } = "";
    public Color Color { get; set; }
    public IBrush DisplayBrush => new SolidColorBrush(Color);

    public GuideColorOption(string name, Color color)
    {
        Name = name;
        Color = color;
    }
}

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
    [ObservableProperty] private bool _isNewGuideDialogOpen;
    [ObservableProperty] private string _newGuideOrientation = "Horizontal"; // "Horizontal" or "Vertical"
    [ObservableProperty] private double _newGuidePosition = 100.0;
    [ObservableProperty] private string _newGuidePositionText = "100 px";
    [ObservableProperty] private int _selectedNewGuideColorIndex = 0; // Cyan default
    [ObservableProperty] private string _newGuideErrorMessage = "";

    public static readonly List<GuideColorOption> GuideColorChoices = new()
    {
        new GuideColorOption("Cyan", Color.FromRgb(0x4a, 0xff, 0xff)),
        new GuideColorOption("Light Blue", Color.FromRgb(0x33, 0x66, 0xff)),
        new GuideColorOption("Light Red", Color.FromRgb(0xff, 0x33, 0x33)),
        new GuideColorOption("Green", Color.FromRgb(0x33, 0xff, 0x33)),
        new GuideColorOption("Medium Blue", Color.FromRgb(0x33, 0x33, 0xff)),
        new GuideColorOption("Yellow", Color.FromRgb(0xff, 0xcc, 0x00)),
        new GuideColorOption("Magenta", Color.FromRgb(0xff, 0x00, 0xff)),
        new GuideColorOption("Light Gray", Color.FromRgb(0xe7, 0xe7, 0xe7)),
        new GuideColorOption("Black", Color.FromRgb(0x00, 0x00, 0x00))
    };

    public List<GuideColorOption> GuideColorOptions => GuideColorChoices;

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
        NewGuideErrorMessage = "";
        IsNewGuideDialogOpen = true;
    }

    [RelayCommand]
    public void CancelNewGuideDialog()
    {
        NewGuideErrorMessage = "";
        IsNewGuideDialogOpen = false;
    }

    public bool TryParseGuidePosition(string text, bool isVertical, out double px, out string error)
    {
        px = 0;
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Please enter a guide position.";
            return false;
        }

        string t = text.Trim();
        int splitIdx = 0;
        while (splitIdx < t.Length && (char.IsDigit(t[splitIdx]) || t[splitIdx] == '.' || t[splitIdx] == ',' || t[splitIdx] == '+' || t[splitIdx] == '-'))
        {
            splitIdx++;
        }

        string numPart = t.Substring(0, splitIdx).Trim().Replace(',', '.');
        string suffix = t.Substring(splitIdx).Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(numPart) || !double.TryParse(numPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value))
        {
            error = $"'{text}' is not a valid length. Give a number of pixels, or a number with px, in, cm, mm, pt, pc, or %.";
            return false;
        }

        double dpi = NewDocResolution > 0 ? NewDocResolution : 300.0;
        double extent = isVertical ? (CanvasWidth > 0 ? CanvasWidth : 1000.0) : (CanvasHeight > 0 ? CanvasHeight : 1000.0);

        switch (suffix)
        {
            case "":
            case "px":
            case "pixel":
            case "pixels":
                px = value;
                break;
            case "in":
            case "inch":
            case "inches":
            case "\"":
                px = value * dpi;
                break;
            case "cm":
            case "centimeter":
            case "centimeters":
            case "centimetre":
            case "centimetres":
                px = value * (dpi / 2.54);
                break;
            case "mm":
            case "millimeter":
            case "millimeters":
            case "millimetre":
            case "millimetres":
                px = value * (dpi / 25.4);
                break;
            case "pt":
            case "point":
            case "points":
                px = value * (dpi / 72.0);
                break;
            case "pc":
            case "pica":
            case "picas":
                px = value * (dpi / 6.0); // 1 pc = 12 pt
                break;
            case "%":
            case "percent":
                px = value * (extent / 100.0);
                break;
            default:
                error = $"Unrecognized unit '{suffix}'. Supported: px, in, cm, mm, pt, pc, %.";
                return false;
        }

        if (!double.IsFinite(px) || Math.Abs(px) > 1_000_000.0)
        {
            error = "Guide position is out of range (at most 1,000,000 px).";
            return false;
        }

        return true;
    }

    [RelayCommand]
    public void ConfirmNewGuideDialog()
    {
        bool isVertical = string.Equals(NewGuideOrientation, "Vertical", System.StringComparison.OrdinalIgnoreCase);
        if (!TryParseGuidePosition(NewGuidePositionText, isVertical, out double px, out string error))
        {
            NewGuideErrorMessage = error;
            StatusMessage = error;
            return;
        }

        NewGuideErrorMessage = "";
        Color? guideColor = null;
        if (SelectedNewGuideColorIndex >= 0 && SelectedNewGuideColorIndex < GuideColorChoices.Count)
        {
            guideColor = GuideColorChoices[SelectedNewGuideColorIndex].Color;
        }

        CanvasControl?.AddUserGuide(isVertical, px, guideColor);
        IsNewGuideDialogOpen = false;
        StatusMessage = $"Added {(isVertical ? "vertical" : "horizontal")} guide at {px:0.#} px.";
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
