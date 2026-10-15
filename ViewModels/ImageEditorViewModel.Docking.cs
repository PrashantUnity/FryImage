using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Preferences;
using PdfEditorApp.Plugins.ImageEditor.Models.Session;
using PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Dock Layout Tree (LeetCode-style tabs, groups and splits) ───────────
    //
    // Every panel page is a tab that can be dragged onto another pane's center (group),
    // onto a pane edge (split), onto the workspace's outer edge, or floated in a window.
    // The canvas is the one "document host" pane: never closed, never dragged.

    private DockNode _dockRoot = LoadSavedDockLayout();
    private readonly Dictionary<string, string?> _floatingDockPages = new(); // page id → group it was floated from
    private bool _syncingDockTabs;

    /// <summary>Root of the dock layout tree. Replaced on structural changes (see <see cref="DockLayoutVersion"/>).</summary>
    public DockNode DockRoot => _dockRoot;

    /// <summary>Bumped when panes are added, removed, split or moved; the workspace rebuilds its panes.</summary>
    [ObservableProperty]
    private int _dockLayoutVersion;

    /// <summary>Bumped when only the active tab of a group changed; panes refresh in place.</summary>
    [ObservableProperty]
    private int _dockActivationVersion;

    /// <summary>The pane filling the whole workspace, or null when no pane is maximized.</summary>
    [ObservableProperty]
    private string? _maximizedDockGroupId;

    /// <summary>The pane whose tab was used last (target of "Maximize Active Pane" and reopen fallback).</summary>
    [ObservableProperty]
    private string? _activeDockGroupId;

    [ObservableProperty]
    private DockZone _toolsPlacement = DockZone.Left;

    // ── Interactive Tab Drag State ──────────────────────────────────────────

    [ObservableProperty]
    private bool _isDraggingPanel;

    [ObservableProperty]
    private string _draggedPanelName = "";

    // ── Queries ─────────────────────────────────────────────────────────────

    public bool IsDockPageFloating(string pageId) => _floatingDockPages.ContainsKey(pageId);

    public bool IsDockPageDocked(string pageId) => DockLayoutOps.FindGroupOf(_dockRoot, pageId) != null;

    public bool IsDockPageClosed(string pageId) => !IsDockPageDocked(pageId) && !IsDockPageFloating(pageId);

    /// <summary>Pages that are neither docked nor floating (offered by every pane's ＋ button).</summary>
    public IReadOnlyList<DockPageDescriptor> ClosedDockPages =>
        DockPageRegistry.All.Where(p => IsDockPageClosed(p.Id)).ToList();

    public bool HasClosedDockPages => DockPageRegistry.All.Any(p => IsDockPageClosed(p.Id));

    public bool IsDockMaximized => MaximizedDockGroupId != null;

    partial void OnMaximizedDockGroupIdChanged(string? value) => OnPropertyChanged(nameof(IsDockMaximized));

    // Legacy panel placement, derived from where each panel's primary page sits relative to the canvas.
    public DockZone LayersPlacement => PlacementOfPage("Layers");
    public DockZone PropertiesPlacement => PlacementOfPage("Properties");
    public DockZone ColorPlacement => PlacementOfPage("Color");

    public bool IsLayersDocked
    {
        get => IsDockPageDocked("Layers");
        set => SetPageDocked("Layers", value);
    }

    public bool IsPropertiesDocked
    {
        get => IsDockPageDocked("Properties");
        set => SetPageDocked("Properties", value);
    }

    public bool IsColorDocked
    {
        get => IsDockPageDocked("Color");
        set => SetPageDocked("Color", value);
    }

    public bool IsToolsDocked
    {
        get => ToolsPlacement != DockZone.Floating && ToolsPlacement != DockZone.Hidden;
        set => SetPanelPlacement("Tools", value ? DockZone.Left : DockZone.Floating);
    }

    private DockZone PlacementOfPage(string pageId)
    {
        if (IsDockPageFloating(pageId)) return DockZone.Floating;
        var group = DockLayoutOps.FindGroupOf(_dockRoot, pageId);
        return group == null ? DockZone.Hidden : DockLayoutOps.ZoneOf(_dockRoot, group);
    }

    private void SetPageDocked(string pageId, bool docked)
    {
        if (docked)
        {
            if (IsDockPageFloating(pageId)) RedockFloatingPage(pageId);
            else ReopenDockPage(pageId);
        }
        else
        {
            FloatDockPage(pageId);
        }
    }

    // ── Tab Activation ──────────────────────────────────────────────────────

    /// <summary>Shows a page: activates its tab, reopens it if closed, or focuses its floating window.</summary>
    public void ActivateDockPage(string pageId)
    {
        if (DockPageRegistry.Find(pageId) == null) return;

        if (IsDockPageFloating(pageId))
        {
            FocusFloatingPalette(pageId);
            SyncLegacyDockTabs(pageId);
            OnDockPageShown(pageId);
            return;
        }

        var group = DockLayoutOps.FindGroupOf(_dockRoot, pageId);
        if (group == null)
        {
            ReopenDockPage(pageId);
            return;
        }

        if (MaximizedDockGroupId != null && MaximizedDockGroupId != group.Id)
        {
            MaximizedDockGroupId = null;
        }

        SelectDockTab(group.Id, pageId);
    }

    /// <summary>Tab click: makes <paramref name="pageId"/> the visible page of its group.</summary>
    public void SelectDockTab(string groupId, string pageId)
    {
        var group = DockLayoutOps.FindGroup(_dockRoot, groupId);
        if (group == null || !group.Pages.Contains(pageId)) return;

        ActiveDockGroupId = group.Id;
        if (group.ActivePage != pageId)
        {
            group.ActivePage = pageId;
            DockActivationVersion++;
            SaveDockLayout();
        }

        SyncLegacyDockTabs(pageId);
        OnDockPageShown(pageId);
    }

    // The rest of the editor still drives pages through these tab properties
    // (Modals sets ActiveLayersDockTab = "Paths" after creating a work path, …).
    partial void OnActiveLayersDockTabChanged(string value) => RevealFromLegacyTab(value);

    partial void OnActivePropertiesDockTabChanged(string value) => RevealFromLegacyTab(value);

    partial void OnActiveColorDockTabChanged(string value) => RevealFromLegacyTab(value);

    private void RevealFromLegacyTab(string pageId)
    {
        if (!_syncingDockTabs) ActivateDockPage(pageId);
    }

    private void SyncLegacyDockTabs(string pageId)
    {
        var page = DockPageRegistry.Find(pageId);
        if (page == null) return;

        _syncingDockTabs = true;
        try
        {
            ActiveRightDockTab = pageId;
            switch (page.Family)
            {
                case DockPageRegistry.LayersFamily: ActiveLayersDockTab = pageId; break;
                case DockPageRegistry.PropertiesFamily: ActivePropertiesDockTab = pageId; break;
                case DockPageRegistry.ColorFamily: ActiveColorDockTab = pageId; break;
            }
        }
        finally
        {
            _syncingDockTabs = false;
        }
    }

    private void OnDockPageShown(string pageId)
    {
        if (pageId == "Histogram") _ = UpdateHistogramAsync();
        else if (pageId == "History") RebuildHistoryItems();
    }

    // ── Moving, Closing and Reopening Tabs ──────────────────────────────────

    /// <summary>
    /// Drops a page on a pane: <see cref="DockDropPosition.Center"/> groups it with that pane's tabs
    /// (at <paramref name="insertIndex"/>, or last), an edge splits the pane.
    /// </summary>
    public void MoveDockPage(string pageId, string targetGroupId, DockDropPosition position, int insertIndex = -1)
    {
        var page = DockPageRegistry.Find(pageId);
        if (page == null) return;

        var source = DockLayoutOps.FindGroupOf(_dockRoot, pageId);
        if (_floatingDockPages.Remove(pageId)) CloseFloatingPalette(pageId);

        var root = DockLayoutOps.MovePage(_dockRoot, pageId, targetGroupId, position, insertIndex, out bool changed);
        if (!changed)
        {
            if (source == null) ReopenDockPage(pageId); // it was floating: never lose it
            return;
        }

        ActiveDockGroupId = DockLayoutOps.FindGroupOf(root, pageId)?.Id;
        CommitDockLayout(root, position == DockDropPosition.Center
            ? $"Grouped {page.Title} with the tabs of that pane."
            : $"Split {page.Title} to the {position.ToString().ToLowerInvariant()} of that pane.");
        OnDockPageShown(pageId);
    }

    /// <summary>Docks a page along an outer edge of the workspace (a full-height column or full-width row).</summary>
    public void DockPageAtWorkspaceEdge(string pageId, DockDropPosition edge)
    {
        var page = DockPageRegistry.Find(pageId);
        if (page == null || edge == DockDropPosition.Center) return;

        if (_floatingDockPages.Remove(pageId)) CloseFloatingPalette(pageId);

        var root = DockLayoutOps.MovePageToRootEdge(_dockRoot, pageId, edge, out bool changed);
        if (!changed) return;

        ActiveDockGroupId = DockLayoutOps.FindGroupOf(root, pageId)?.Id;
        CommitDockLayout(root, $"Docked {page.Title} along the {edge.ToString().ToLowerInvariant()} edge of the workspace.");
        OnDockPageShown(pageId);
    }

    [RelayCommand]
    public void CloseDockPage(string pageId)
    {
        var page = DockPageRegistry.Find(pageId);
        if (page == null) return;

        if (_floatingDockPages.Remove(pageId))
        {
            CloseFloatingPalette(pageId);
            NotifyDockQueries();
        }
        else if (IsDockPageDocked(pageId))
        {
            CommitDockLayout(DockLayoutOps.RemovePage(_dockRoot, pageId));
        }
        else
        {
            return;
        }

        StatusMessage = $"Closed {page.Title}. Reopen it from a pane's + button or the Window menu.";
    }

    /// <summary>Closes every other tab of the page's group.</summary>
    public void CloseOtherDockPages(string pageId)
    {
        var group = DockLayoutOps.FindGroupOf(_dockRoot, pageId);
        if (group == null || group.Pages.Count < 2) return;

        var root = _dockRoot;
        foreach (var other in group.Pages.Where(p => p != pageId).ToList())
        {
            root = DockLayoutOps.RemovePage(root, other);
        }

        CommitDockLayout(root, $"Closed the other tabs next to {DockPageRegistry.Find(pageId)?.Title}.");
    }

    public void CloseDockGroup(string groupId)
    {
        var group = DockLayoutOps.FindGroup(_dockRoot, groupId);
        if (group == null || group.IsDocumentHost) return;
        CommitDockLayout(DockLayoutOps.RemoveGroup(_dockRoot, group), "Closed pane.");
    }

    /// <summary>
    /// Puts a closed page back: into <paramref name="groupId"/> when given, else next to a page of the
    /// same family, else into the last used pane, else as a new column on the right.
    /// </summary>
    public void ReopenDockPage(string pageId, string? groupId = null)
    {
        var page = DockPageRegistry.Find(pageId);
        if (page == null) return;

        if (IsDockPageDocked(pageId) || IsDockPageFloating(pageId))
        {
            if (groupId != null && DockLayoutOps.FindGroup(_dockRoot, groupId) is { IsDocumentHost: false })
            {
                MoveDockPage(pageId, groupId, DockDropPosition.Center);
            }
            else
            {
                ActivateDockPage(pageId);
            }
            return;
        }

        var target = NonHostGroup(groupId) ?? FamilyHomeGroup(page) ?? NonHostGroup(ActiveDockGroupId);
        DockGroupNode placedIn;
        var root = target != null
            ? DockLayoutOps.InsertPage(_dockRoot, pageId, target, DockDropPosition.Center, -1, out placedIn)
            : DockLayoutOps.InsertAtRootEdge(_dockRoot, pageId, DockDropPosition.Right, out placedIn);

        ActiveDockGroupId = placedIn.Id;
        if (MaximizedDockGroupId != null && MaximizedDockGroupId != placedIn.Id) MaximizedDockGroupId = null;
        CommitDockLayout(root, $"Reopened {page.Title}.");
        SyncLegacyDockTabs(pageId);
        OnDockPageShown(pageId);
    }

    private DockGroupNode? NonHostGroup(string? groupId) =>
        DockLayoutOps.FindGroup(_dockRoot, groupId) is { IsDocumentHost: false } group ? group : null;

    private DockGroupNode? FamilyHomeGroup(DockPageDescriptor page) =>
        DockLayoutOps.Groups(_dockRoot).FirstOrDefault(g =>
            !g.IsDocumentHost && g.Pages.Any(p => DockPageRegistry.Find(p)?.Family == page.Family));

    // ── Floating Windows ────────────────────────────────────────────────────

    [RelayCommand]
    public void FloatDockPage(string pageId)
    {
        var page = DockPageRegistry.Find(pageId);
        if (page == null || IsDockPageFloating(pageId)) return;

        _floatingDockPages[pageId] = DockLayoutOps.FindGroupOf(_dockRoot, pageId)?.Id;
        CommitDockLayout(DockLayoutOps.RemovePage(_dockRoot, pageId), $"Floated {page.Title} in a separate window.");
        SyncLegacyDockTabs(pageId);
        OpenFloatingPalette(pageId);
        OnDockPageShown(pageId);
    }

    /// <summary>Docks a floating page back into the pane it came from (or its family's pane).</summary>
    public void RedockFloatingPage(string pageId)
    {
        if (!_floatingDockPages.Remove(pageId, out var originGroupId)) return;
        CloseFloatingPalette(pageId);
        ReopenDockPage(pageId, NonHostGroup(originGroupId)?.Id);
    }

    // ── Maximize & Resize ───────────────────────────────────────────────────

    public void ToggleMaximizeDockGroup(string groupId)
    {
        if (MaximizedDockGroupId == groupId)
        {
            MaximizedDockGroupId = null;
            StatusMessage = "Restored workspace layout.";
            return;
        }

        var group = DockLayoutOps.FindGroup(_dockRoot, groupId);
        if (group == null) return;

        MaximizedDockGroupId = group.Id;
        ActiveDockGroupId = group.Id;
        StatusMessage = group.IsDocumentHost
            ? "Canvas maximized: panels hidden. Double-click a tab or use Window › Maximize Active Pane to restore."
            : $"Maximized {DockPageRegistry.Find(group.ActivePage)?.Title ?? "pane"}. Double-click its tab again to restore.";
    }

    [RelayCommand]
    public void ToggleMaximizeActivePane() =>
        ToggleMaximizeDockGroup(MaximizedDockGroupId ?? NonHostGroup(ActiveDockGroupId)?.Id ?? DockGroupNode.DocumentHostId);

    /// <summary>Stores a splitter position after the user dragged it (no rebuild needed).</summary>
    public void SetDockSplitSize(string splitId, double ratio, double panelSize)
    {
        var split = DockLayoutOps.FindSplit(_dockRoot, splitId);
        if (split == null) return;
        if (double.IsFinite(ratio)) split.Ratio = Math.Clamp(ratio, 0.05, 0.95);
        if (double.IsFinite(panelSize) && panelSize > 0) split.PanelSize = Math.Clamp(panelSize, 80, 4000);
        SaveDockLayout();
    }

    // ── Drag Lifecycle (the workspace view owns hit-testing and drop preview) ──

    public void StartPanelDrag(string pageId, double x, double y)
    {
        IsDraggingPanel = true;
        DraggedPanelName = pageId;
        StatusMessage = $"Dragging {DockPageRegistry.Find(pageId)?.Title ?? pageId}: drop on a pane's center to group, an edge to split, or the top badge to float. Esc cancels.";
    }

    /// <summary>Ends a drag after a successful drop.</summary>
    public void EndPanelDrag()
    {
        IsDraggingPanel = false;
        DraggedPanelName = "";
    }

    public void CancelPanelDrag()
    {
        if (!IsDraggingPanel && string.IsNullOrEmpty(DraggedPanelName)) return;
        IsDraggingPanel = false;
        DraggedPanelName = "";
        StatusMessage = "Tab drag cancelled; layout preserved.";
    }

    // ── Legacy Panel Placement (Window menu, presets, older callers) ────────

    /// <summary>
    /// Moves a whole panel family ("Layers", "Properties", "Color") as one tab group to a side of the
    /// canvas, floats its primary page, or hides it. "Tools" floats or docks the tool palette.
    /// </summary>
    public void SetPanelPlacement(string panel, DockZone targetZone)
    {
        if (panel == "Tools")
        {
            ToolsPlacement = targetZone;
            if (targetZone == DockZone.Floating) OpenFloatingPalette("Tools");
            else CloseFloatingPalette("Tools");
            OnPropertyChanged(nameof(IsToolsDocked));
            StatusMessage = $"Placed Tools palette: {targetZone}.";
            return;
        }

        var family = DockPageRegistry.FamilyOfPanel(panel) ?? DockPageRegistry.Find(panel)?.Family;
        if (family == null) return;
        string primary = DockPageRegistry.Find(panel)?.Id ?? DockPageRegistry.PagesInFamily(family).First();
        var familyPages = DockPageRegistry.PagesInFamily(family).ToList();

        switch (targetZone)
        {
            case DockZone.Floating:
                FloatDockPage(primary);
                return;

            case DockZone.Hidden:
                foreach (var pageId in familyPages) CloseDockPage(pageId);
                StatusMessage = $"Hid {panel} panel.";
                return;
        }

        string active = DockLayoutOps.Groups(_dockRoot)
            .Select(g => g.ActivePage)
            .FirstOrDefault(p => p != null && familyPages.Contains(p)) ?? primary;

        var root = _dockRoot;
        foreach (var pageId in familyPages)
        {
            if (_floatingDockPages.Remove(pageId)) CloseFloatingPalette(pageId);
            root = DockLayoutOps.RemovePage(root, pageId);
        }

        // Join the zone's existing pane column/row as a stacked pane; otherwise open a new edge pane.
        var neighbour = DockLayoutOps.Groups(root).LastOrDefault(g => !g.IsDocumentHost && DockLayoutOps.ZoneOf(root, g) == targetZone);
        DockGroupNode placedIn;
        if (neighbour != null)
        {
            var below = targetZone is DockZone.Left or DockZone.Right ? DockDropPosition.Bottom : DockDropPosition.Right;
            root = DockLayoutOps.InsertPage(root, familyPages[0], neighbour, below, -1, out placedIn);
        }
        else if (targetZone is DockZone.Left or DockZone.Right)
        {
            var edge = targetZone == DockZone.Left ? DockDropPosition.Left : DockDropPosition.Right;
            root = DockLayoutOps.InsertAtRootEdge(root, familyPages[0], edge, out placedIn);
        }
        else
        {
            var host = DockLayoutOps.DocumentHost(root)!;
            var side = targetZone == DockZone.Top ? DockDropPosition.Top : DockDropPosition.Bottom;
            root = DockLayoutOps.InsertPage(root, familyPages[0], host, side, -1, out placedIn);
        }

        foreach (var pageId in familyPages.Skip(1))
        {
            DockLayoutOps.InsertPage(root, pageId, placedIn, DockDropPosition.Center, -1, out _);
        }
        placedIn.ActivePage = active;

        ActiveDockGroupId = placedIn.Id;
        CommitDockLayout(root, $"Placed {panel} panel: {targetZone}.");
        SyncLegacyDockTabs(active);
    }

    [RelayCommand]
    public void SetPanelPlacementParam(string param)
    {
        if (string.IsNullOrWhiteSpace(param)) return;
        var parts = param.Split(':');
        if (parts.Length != 2) return;

        if (Enum.TryParse<DockZone>(parts[1].Trim(), true, out var zone))
        {
            SetPanelPlacement(parts[0].Trim(), zone);
        }
    }

    // ── Workspace Presets ───────────────────────────────────────────────────

    [RelayCommand]
    public void ApplyDefaultWorkspace() =>
        ApplyWorkspace(DockLayoutPresets.Essentials(), "Applied Essentials workspace (Color, Properties and Layers stacked right).");

    [RelayCommand]
    public void ApplyUnityTwoByThreeWorkspace() =>
        ApplyWorkspace(DockLayoutPresets.TwoByThree(), "Applied 2-by-3 workspace (Layers left, Properties and Color right).");

    [RelayCommand]
    public void ApplyUnityFourSplitWorkspace() =>
        ApplyWorkspace(DockLayoutPresets.FourSplit(), "Applied 4-split workspace (Layers left, Color bottom, Properties right).");

    [RelayCommand]
    public void ApplyTallWorkspace() =>
        ApplyWorkspace(DockLayoutPresets.Tall(), "Applied tall workspace (Layers and Properties left, Color right).");

    [RelayCommand]
    public void ApplyUnityTabbedWorkspace() =>
        ApplyWorkspace(DockLayoutPresets.Tabbed(), "Applied tabbed workspace (every panel as a tab on the right).");

    private void ApplyWorkspace(DockNode layout, string status)
    {
        CloseAllFloatingPalettes();
        ToolsPlacement = DockZone.Left;
        MaximizedDockGroupId = null;
        ActiveDockGroupId = null;
        CommitDockLayout(layout, status);
        foreach (var group in DockLayoutOps.Groups(_dockRoot).Where(g => g.ActivePage != null))
        {
            SyncLegacyDockTabs(group.ActivePage!);
        }
    }

    private void CloseAllFloatingPalettes()
    {
        // Forget the windows before closing them so their Closed handlers don't re-dock into the old layout.
        var windows = _floatingPaletteWindows.Values.ToList();
        _floatingPaletteWindows.Clear();
        _floatingDockPages.Clear();
        foreach (var win in windows)
        {
            try { win.Close(); } catch { }
        }
    }

    // ── Commit & Persistence ────────────────────────────────────────────────

    private void CommitDockLayout(DockNode root, string? status = null)
    {
        _dockRoot = root;
        if (MaximizedDockGroupId != null && DockLayoutOps.FindGroup(_dockRoot, MaximizedDockGroupId) == null)
        {
            MaximizedDockGroupId = null;
        }
        if (ActiveDockGroupId != null && DockLayoutOps.FindGroup(_dockRoot, ActiveDockGroupId) == null)
        {
            ActiveDockGroupId = null;
        }

        DockLayoutVersion++;
        NotifyDockQueries();
        SaveDockLayout();
        if (status != null) StatusMessage = status;
    }

    private void NotifyDockQueries()
    {
        OnPropertyChanged(nameof(DockRoot));
        OnPropertyChanged(nameof(ClosedDockPages));
        OnPropertyChanged(nameof(HasClosedDockPages));
        OnPropertyChanged(nameof(LayersPlacement));
        OnPropertyChanged(nameof(PropertiesPlacement));
        OnPropertyChanged(nameof(ColorPlacement));
        OnPropertyChanged(nameof(IsLayersDocked));
        OnPropertyChanged(nameof(IsPropertiesDocked));
        OnPropertyChanged(nameof(IsColorDocked));
    }

    private static DockNode LoadSavedDockLayout() =>
        DockLayoutSerializer.TryDeserialize(StudioPreferences.Instance.DockLayoutJson, DockPageRegistry.AllIds, out var root) && root != null
            ? root
            : DockLayoutPresets.Essentials();

    private void SaveDockLayout()
    {
        var prefs = StudioPreferences.Instance;
        prefs.DockLayoutJson = DockLayoutSerializer.Serialize(_dockRoot);
        prefs.Save();
    }
}
