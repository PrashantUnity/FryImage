using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Models.Session;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Multi-Zone & Tabbed Docking State (Unity-Style Architecture) ────────

    [ObservableProperty]
    private DockZone _layersPlacement = DockZone.Right;

    [ObservableProperty]
    private DockZone _propertiesPlacement = DockZone.Right;

    [ObservableProperty]
    private DockZone _colorPlacement = DockZone.Right;

    [ObservableProperty]
    private DockZone _toolsPlacement = DockZone.Left;

    [ObservableProperty]
    private string _activeLeftDockTab = "Layers";

    [ObservableProperty]
    private string _activeBottomDockTab = "Color";

    partial void OnActiveRightDockTabChanged(string value) => RefreshDockZones();

    [ObservableProperty]
    private string _leftDockMode = "Tabbed"; // "Tabbed" or "Stacked"

    [ObservableProperty]
    private string _rightDockMode = "Stacked"; // "Stacked" or "Tabbed"

    [ObservableProperty]
    private string _bottomDockMode = "Tabbed"; // "Tabbed" or "Split"

    // ── Interactive Drag & Drop Docking State ───────────────────────────────

    [ObservableProperty]
    private bool _isDraggingPanel = false;

    [ObservableProperty]
    private string _draggedPanelName = "";

    [ObservableProperty]
    private DockZone _hoveredDockZone = DockZone.Right;

    [ObservableProperty]
    private string _dragTargetHint = "Release to Dock";

    [ObservableProperty]
    private double _dragPointerX = 0;

    [ObservableProperty]
    private double _dragPointerY = 0;

    public bool IsDropLeftActive => IsDraggingPanel && HoveredDockZone == DockZone.Left;
    public bool IsDropBottomActive => IsDraggingPanel && HoveredDockZone == DockZone.Bottom;
    public bool IsDropRightActive => IsDraggingPanel && HoveredDockZone == DockZone.Right;
    public bool IsDropFloatActive => IsDraggingPanel && HoveredDockZone == DockZone.Floating;

    // ── Zone Aggregation Queries ────────────────────────────────────────────

    public bool HasAnyLeftPanelDocked =>
        LayersPlacement == DockZone.Left || PropertiesPlacement == DockZone.Left || ColorPlacement == DockZone.Left;

    public bool HasAnyRightPanelDocked =>
        LayersPlacement == DockZone.Right || PropertiesPlacement == DockZone.Right || ColorPlacement == DockZone.Right;

    public bool HasAnyBottomPanelDocked =>
        LayersPlacement == DockZone.Bottom || PropertiesPlacement == DockZone.Bottom || ColorPlacement == DockZone.Bottom;

    public int LeftDockedCount =>
        (LayersPlacement == DockZone.Left ? 1 : 0) +
        (PropertiesPlacement == DockZone.Left ? 1 : 0) +
        (ColorPlacement == DockZone.Left ? 1 : 0);

    public int RightDockedCount =>
        (LayersPlacement == DockZone.Right ? 1 : 0) +
        (PropertiesPlacement == DockZone.Right ? 1 : 0) +
        (ColorPlacement == DockZone.Right ? 1 : 0);

    public int BottomDockedCount =>
        (LayersPlacement == DockZone.Bottom ? 1 : 0) +
        (PropertiesPlacement == DockZone.Bottom ? 1 : 0) +
        (ColorPlacement == DockZone.Bottom ? 1 : 0);

    public bool ShowLeftDockTabs => LeftDockedCount > 1;
    public bool ShowBottomDockTabs => BottomDockedCount > 1;
    public bool ShowRightDockTabs => (RightDockMode == "Tabbed" && RightDockedCount > 1) || RightDockMode == "Stacked";

    // ── Per-Zone Panel Visibility & Splitting ───────────────────────────────

    // Left Zone (Supports Stacked Split & Tabbed)
    public bool IsLayersInLeftVisible =>
        LayersPlacement == DockZone.Left && (LeftDockMode == "Stacked" || LeftDockedCount <= 1 || ActiveLeftDockTab == "Layers");

    public bool IsPropertiesInLeftVisible =>
        PropertiesPlacement == DockZone.Left && (LeftDockMode == "Stacked" || LeftDockedCount <= 1 || ActiveLeftDockTab == "Properties");

    public bool IsColorInLeftVisible =>
        ColorPlacement == DockZone.Left && (LeftDockMode == "Stacked" || LeftDockedCount <= 1 || ActiveLeftDockTab == "Color");

    public bool IsColorSplitterInLeftVisible =>
        LeftDockMode == "Stacked" && ColorPlacement == DockZone.Left && (PropertiesPlacement == DockZone.Left || LayersPlacement == DockZone.Left);

    public bool IsPropertiesSplitterInLeftVisible =>
        LeftDockMode == "Stacked" && PropertiesPlacement == DockZone.Left && LayersPlacement == DockZone.Left;

    // Bottom Shelf Zone (Supports Side-by-Side Split & Tabbed)
    public bool IsLayersInBottomVisible =>
        LayersPlacement == DockZone.Bottom && (BottomDockMode == "Split" || BottomDockedCount <= 1 || ActiveBottomDockTab == "Layers");

    public bool IsPropertiesInBottomVisible =>
        PropertiesPlacement == DockZone.Bottom && (BottomDockMode == "Split" || BottomDockedCount <= 1 || ActiveBottomDockTab == "Properties");

    public bool IsColorInBottomVisible =>
        ColorPlacement == DockZone.Bottom && (BottomDockMode == "Split" || BottomDockedCount <= 1 || ActiveBottomDockTab == "Color");

    public bool IsBottomShelfSplitterVisible =>
        BottomDockMode == "Split" && BottomDockedCount > 1;

    // Right Zone (Supports Stacked Split & Tabbed)
    public bool IsLayersInRightVisible =>
        LayersPlacement == DockZone.Right && (RightDockMode == "Stacked" || RightDockedCount <= 1 || ActiveRightDockTab == "Layers");

    public bool IsPropertiesInRightVisible =>
        PropertiesPlacement == DockZone.Right && (RightDockMode == "Stacked" || RightDockedCount <= 1 || ActiveRightDockTab == "Properties");

    public bool IsColorInRightVisible =>
        ColorPlacement == DockZone.Right && (RightDockMode == "Stacked" || RightDockedCount <= 1 || ActiveRightDockTab == "Color");

    // Splitter visibility in Stacked Right Dock
    public bool IsColorSplitterInRightVisible =>
        RightDockMode == "Stacked" && ColorPlacement == DockZone.Right && (PropertiesPlacement == DockZone.Right || LayersPlacement == DockZone.Right);

    public bool IsPropertiesSplitterInRightVisible =>
        RightDockMode == "Stacked" && PropertiesPlacement == DockZone.Right && LayersPlacement == DockZone.Right;

    // ── Backwards-Compatible Boolean Properties ─────────────────────────────

    public bool IsLayersDocked
    {
        get => LayersPlacement != DockZone.Floating && LayersPlacement != DockZone.Hidden;
        set => SetPanelPlacement("Layers", value ? DockZone.Right : DockZone.Floating);
    }

    public bool IsPropertiesDocked
    {
        get => PropertiesPlacement != DockZone.Floating && PropertiesPlacement != DockZone.Hidden;
        set => SetPanelPlacement("Properties", value ? DockZone.Right : DockZone.Floating);
    }

    public bool IsColorDocked
    {
        get => ColorPlacement != DockZone.Floating && ColorPlacement != DockZone.Hidden;
        set => SetPanelPlacement("Color", value ? DockZone.Right : DockZone.Floating);
    }

    public bool IsToolsDocked
    {
        get => ToolsPlacement != DockZone.Floating && ToolsPlacement != DockZone.Hidden;
        set => SetPanelPlacement("Tools", value ? DockZone.Left : DockZone.Floating);
    }

    // ── Notification and State Sync ─────────────────────────────────────────

    public void RefreshDockZones()
    {
        // Auto-fix active tabs if departed
        if (LayersPlacement == DockZone.Left && ActiveLeftDockTab != "Layers" && PropertiesPlacement != DockZone.Left && ColorPlacement != DockZone.Left)
            ActiveLeftDockTab = "Layers";
        else if (PropertiesPlacement == DockZone.Left && ActiveLeftDockTab == "Layers" && LayersPlacement != DockZone.Left)
            ActiveLeftDockTab = "Properties";
        else if (ColorPlacement == DockZone.Left && ActiveLeftDockTab != "Color" && LayersPlacement != DockZone.Left && PropertiesPlacement != DockZone.Left)
            ActiveLeftDockTab = "Color";

        if (LayersPlacement == DockZone.Bottom && ActiveBottomDockTab != "Layers" && PropertiesPlacement != DockZone.Bottom && ColorPlacement != DockZone.Bottom)
            ActiveBottomDockTab = "Layers";
        else if (PropertiesPlacement == DockZone.Bottom && ActiveBottomDockTab != "Properties" && LayersPlacement != DockZone.Bottom && ColorPlacement != DockZone.Bottom)
            ActiveBottomDockTab = "Properties";
        else if (ColorPlacement == DockZone.Bottom && ActiveBottomDockTab != "Color" && LayersPlacement != DockZone.Bottom && PropertiesPlacement != DockZone.Bottom)
            ActiveBottomDockTab = "Color";

        if (LayersPlacement == DockZone.Right && ActiveRightDockTab != "Layers" && PropertiesPlacement != DockZone.Right && ColorPlacement != DockZone.Right)
            ActiveRightDockTab = "Layers";
        else if (PropertiesPlacement == DockZone.Right && ActiveRightDockTab != "Properties" && LayersPlacement != DockZone.Right && ColorPlacement != DockZone.Right)
            ActiveRightDockTab = "Properties";
        else if (ColorPlacement == DockZone.Right && ActiveRightDockTab != "Color" && LayersPlacement != DockZone.Right && PropertiesPlacement != DockZone.Right)
            ActiveRightDockTab = "Color";

        OnPropertyChanged(nameof(HasAnyLeftPanelDocked));
        OnPropertyChanged(nameof(HasAnyRightPanelDocked));
        OnPropertyChanged(nameof(HasAnyBottomPanelDocked));

        OnPropertyChanged(nameof(LeftDockedCount));
        OnPropertyChanged(nameof(RightDockedCount));
        OnPropertyChanged(nameof(BottomDockedCount));

        OnPropertyChanged(nameof(ShowLeftDockTabs));
        OnPropertyChanged(nameof(ShowBottomDockTabs));
        OnPropertyChanged(nameof(ShowRightDockTabs));

        OnPropertyChanged(nameof(IsLayersInLeftVisible));
        OnPropertyChanged(nameof(IsPropertiesInLeftVisible));
        OnPropertyChanged(nameof(IsColorInLeftVisible));
        OnPropertyChanged(nameof(IsColorSplitterInLeftVisible));
        OnPropertyChanged(nameof(IsPropertiesSplitterInLeftVisible));

        OnPropertyChanged(nameof(IsLayersInBottomVisible));
        OnPropertyChanged(nameof(IsPropertiesInBottomVisible));
        OnPropertyChanged(nameof(IsColorInBottomVisible));
        OnPropertyChanged(nameof(IsBottomShelfSplitterVisible));

        OnPropertyChanged(nameof(IsLayersInRightVisible));
        OnPropertyChanged(nameof(IsPropertiesInRightVisible));
        OnPropertyChanged(nameof(IsColorInRightVisible));
        OnPropertyChanged(nameof(IsColorSplitterInRightVisible));
        OnPropertyChanged(nameof(IsPropertiesSplitterInRightVisible));

        OnPropertyChanged(nameof(IsLayersDocked));
        OnPropertyChanged(nameof(IsPropertiesDocked));
        OnPropertyChanged(nameof(IsColorDocked));
        OnPropertyChanged(nameof(IsToolsDocked));

        OnPropertyChanged(nameof(IsDropLeftActive));
        OnPropertyChanged(nameof(IsDropBottomActive));
        OnPropertyChanged(nameof(IsDropRightActive));
        OnPropertyChanged(nameof(IsDropFloatActive));
    }

    // ── Panel Placement Commands ────────────────────────────────────────────

    public void SetPanelPlacement(string panel, DockZone targetZone)
    {
        switch (panel)
        {
            case "Layers":
                LayersPlacement = targetZone;
                if (targetZone == DockZone.Left) ActiveLeftDockTab = "Layers";
                else if (targetZone == DockZone.Bottom) ActiveBottomDockTab = "Layers";
                else if (targetZone == DockZone.Right) ActiveRightDockTab = "Layers";
                break;

            case "Properties":
                PropertiesPlacement = targetZone;
                if (targetZone == DockZone.Left) ActiveLeftDockTab = "Properties";
                else if (targetZone == DockZone.Bottom) ActiveBottomDockTab = "Properties";
                else if (targetZone == DockZone.Right) ActiveRightDockTab = "Properties";
                break;

            case "Color":
            case "Color & Swatches":
                ColorPlacement = targetZone;
                if (targetZone == DockZone.Left) ActiveLeftDockTab = "Color";
                else if (targetZone == DockZone.Bottom) ActiveBottomDockTab = "Color";
                else if (targetZone == DockZone.Right) ActiveRightDockTab = "Color";
                break;

            case "Tools":
                ToolsPlacement = targetZone;
                break;
        }

        if (targetZone == DockZone.Floating)
        {
            OpenFloatingPalette(panel == "Color" ? "Color & Swatches" : panel);
        }
        else
        {
            CloseFloatingPalette(panel == "Color" ? "Color & Swatches" : panel);
        }

        RefreshDockZones();
        StatusMessage = $"Placed {panel} panel into {targetZone} dock zone.";
    }

    [RelayCommand]
    public void SetPanelPlacementParam(string param)
    {
        if (string.IsNullOrWhiteSpace(param)) return;
        var parts = param.Split(':');
        if (parts.Length != 2) return;

        string panel = parts[0].Trim();
        if (Enum.TryParse<DockZone>(parts[1].Trim(), true, out var zone))
        {
            SetPanelPlacement(panel, zone);
        }
    }

    [RelayCommand]
    public void SelectLeftZoneTab(string tab)
    {
        ActiveLeftDockTab = tab;
        RefreshDockZones();
    }

    [RelayCommand]
    public void SelectRightZoneTab(string tab)
    {
        ActiveRightDockTab = tab;
        RefreshDockZones();
    }

    [RelayCommand]
    public void SelectBottomZoneTab(string tab)
    {
        ActiveBottomDockTab = tab;
        RefreshDockZones();
    }

    [RelayCommand]
    public void ToggleLeftDockMode()
    {
        LeftDockMode = LeftDockMode == "Tabbed" ? "Stacked" : "Tabbed";
        RefreshDockZones();
        StatusMessage = $"Left dock layout set to: {LeftDockMode}.";
    }

    [RelayCommand]
    public void SetLeftDockMode(string mode)
    {
        LeftDockMode = mode;
        RefreshDockZones();
        StatusMessage = $"Left dock layout set to: {LeftDockMode}.";
    }

    [RelayCommand]
    public void ToggleBottomDockMode()
    {
        BottomDockMode = BottomDockMode == "Tabbed" ? "Split" : "Tabbed";
        RefreshDockZones();
        StatusMessage = $"Bottom shelf layout set to: {BottomDockMode}.";
    }

    [RelayCommand]
    public void SetBottomDockMode(string mode)
    {
        BottomDockMode = mode;
        RefreshDockZones();
        StatusMessage = $"Bottom shelf layout set to: {BottomDockMode}.";
    }

    [RelayCommand]
    public void ToggleRightDockMode()
    {
        RightDockMode = RightDockMode == "Stacked" ? "Tabbed" : "Stacked";
        RefreshDockZones();
        StatusMessage = $"Right dock mode set to: {RightDockMode}.";
    }

    [RelayCommand]
    public void SetRightDockMode(string mode)
    {
        RightDockMode = mode;
        RefreshDockZones();
        StatusMessage = $"Right dock mode set to: {RightDockMode}.";
    }

    // ── Interactive Panel Drag-and-Drop Operations ──────────────────────────

    public void StartPanelDrag(string panelName, double x, double y)
    {
        IsDraggingPanel = true;
        DraggedPanelName = panelName;
        HoveredDockZone = DockZone.Right;
        DragPointerX = x;
        DragPointerY = y;
        DragTargetHint = $"Dragging '{panelName}' - Drop in Left, Right, Bottom, or Canvas to Float";
        RefreshDockZones();
    }

    public void UpdatePanelDrag(DockZone targetZone, double x, double y, string hint)
    {
        HoveredDockZone = targetZone;
        DragPointerX = x;
        DragPointerY = y;
        DragTargetHint = hint;
        RefreshDockZones();
    }

    public void CompletePanelDrag()
    {
        if (IsDraggingPanel && !string.IsNullOrEmpty(DraggedPanelName))
        {
            SetPanelPlacement(DraggedPanelName, HoveredDockZone);
        }
        IsDraggingPanel = false;
        DraggedPanelName = "";
        RefreshDockZones();
    }

    public void CancelPanelDrag()
    {
        IsDraggingPanel = false;
        DraggedPanelName = "";
        RefreshDockZones();
    }

    // ── Workspace Presets (Unity & Studio Styles) ───────────────────────────

    [RelayCommand]
    public void ApplyDefaultWorkspace()
    {
        CloseAllFloatingPalettes();
        LayersPlacement = DockZone.Right;
        PropertiesPlacement = DockZone.Right;
        ColorPlacement = DockZone.Right;
        ToolsPlacement = DockZone.Left;
        RightDockMode = "Stacked";
        LeftDockMode = "Tabbed";
        BottomDockMode = "Tabbed";
        ActiveRightDockTab = "Layers";
        RefreshDockZones();
        StatusMessage = "Applied Default Studio Workspace (All Panels Stacked Right).";
    }

    [RelayCommand]
    public void ApplyUnityTwoByThreeWorkspace()
    {
        CloseAllFloatingPalettes();
        LayersPlacement = DockZone.Left;
        PropertiesPlacement = DockZone.Right;
        ColorPlacement = DockZone.Right;
        ToolsPlacement = DockZone.Left;
        RightDockMode = "Stacked";
        LeftDockMode = "Stacked";
        BottomDockMode = "Tabbed";
        ActiveLeftDockTab = "Layers";
        ActiveRightDockTab = "Properties";
        RefreshDockZones();
        StatusMessage = "Applied Unity 2-by-3 Workspace (Layers Left, Inspector Right).";
    }

    [RelayCommand]
    public void ApplyUnityFourSplitWorkspace()
    {
        CloseAllFloatingPalettes();
        LayersPlacement = DockZone.Left;
        ColorPlacement = DockZone.Bottom;
        PropertiesPlacement = DockZone.Right;
        ToolsPlacement = DockZone.Left;
        RightDockMode = "Stacked";
        LeftDockMode = "Tabbed";
        BottomDockMode = "Tabbed";
        ActiveLeftDockTab = "Layers";
        ActiveBottomDockTab = "Color";
        ActiveRightDockTab = "Properties";
        RefreshDockZones();
        StatusMessage = "Applied Unity 4-Split Workspace (Layers Left, Color Bottom, Properties Right).";
    }

    [RelayCommand]
    public void ApplyTallWorkspace()
    {
        CloseAllFloatingPalettes();
        LayersPlacement = DockZone.Left;
        PropertiesPlacement = DockZone.Left;
        ColorPlacement = DockZone.Right;
        ToolsPlacement = DockZone.Left;
        RightDockMode = "Stacked";
        LeftDockMode = "Stacked";
        BottomDockMode = "Tabbed";
        ActiveLeftDockTab = "Layers";
        ActiveRightDockTab = "Color";
        RefreshDockZones();
        StatusMessage = "Applied Tall Dual-Sidebar Workspace (Layers & Properties Left, Color Right).";
    }

    [RelayCommand]
    public void ApplyUnityTabbedWorkspace()
    {
        CloseAllFloatingPalettes();
        LayersPlacement = DockZone.Right;
        PropertiesPlacement = DockZone.Right;
        ColorPlacement = DockZone.Right;
        ToolsPlacement = DockZone.Left;
        RightDockMode = "Tabbed";
        ActiveRightDockTab = "Layers";
        RefreshDockZones();
        StatusMessage = "Applied Unity Tabbed Workspace (Tabbed Panels in Right Dock).";
    }

    private void CloseAllFloatingPalettes()
    {
        foreach (var win in _floatingPaletteWindows.Values.ToList())
        {
            try { win.Close(); } catch { }
        }
        _floatingPaletteWindows.Clear();
    }
}
