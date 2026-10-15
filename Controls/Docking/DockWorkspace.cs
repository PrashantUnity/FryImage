using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Docking;

/// <summary>
/// Renders the view model's dock layout tree as resizable splits and tab-group panes, and runs tab
/// drag &amp; drop: drop on a pane's center to group, on an edge to split, between tabs to reorder,
/// on the workspace's outer edge for a full-height/width pane, or on the top badge to float.
/// </summary>
public sealed class DockWorkspace : Grid
{
    public static readonly StyledProperty<Control?> DocumentContentProperty =
        AvaloniaProperty.Register<DockWorkspace, Control?>(nameof(DocumentContent));

    private const double DragThreshold = 5;
    private const double OuterEdgeBand = 18;
    private const double EdgeZone = 0.25;
    private const double PanelMinSize = 140;
    private const double DocumentMinSize = 240;
    private const double SplitterThickness = 4;
    private const double FloatBadgeWidth = 236;
    private const double FloatBadgeHeight = 30;

    private readonly Border _layoutHost = new();
    private readonly Canvas _overlay = new() { IsHitTestVisible = false, IsVisible = false };
    private readonly Border _dropPreview = new() { Classes = { "ps-dock-drop" }, IsVisible = false };
    private readonly Border _caret = new() { Classes = { "ps-dock-caret" }, IsVisible = false };
    private readonly Border _floatBadge;
    private readonly Border _ghost;
    private readonly TextBlock _ghostTitle = new() { Classes = { "ps-dock-ghost-title" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly MaterialIcon _ghostIcon = new() { Width = 13, Height = 13 };
    private readonly Button _restoreButton;

    private readonly Dictionary<string, Control> _pageCache = new();
    private readonly List<DockGroupView> _groupViews = new();

    private ImageEditorViewModel? _vm;
    private bool _rebuildQueued;

    // Drag state
    private string? _pressedPageId;
    private string? _pressedGroupId;
    private Point _pressPoint;
    private IPointer? _pointer;
    private bool _dragging;

    public DockWorkspace()
    {
        ClipToBounds = true;

        _floatBadge = new Border
        {
            Classes = { "ps-dock-float-badge" },
            Width = FloatBadgeWidth,
            Height = FloatBadgeHeight,
            Child = IconLabel(MaterialIconKind.OpenInNew, "Drop here to float in a window")
        };

        var ghostRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        ghostRow.Children.Add(_ghostIcon);
        ghostRow.Children.Add(_ghostTitle);
        _ghost = new Border { Classes = { "ps-dock-ghost" }, Child = ghostRow };

        _overlay.Children.Add(_dropPreview);
        _overlay.Children.Add(_caret);
        _overlay.Children.Add(_floatBadge);
        _overlay.Children.Add(_ghost);

        _restoreButton = new Button
        {
            Classes = { "ps-dock-restore" },
            Content = IconLabel(MaterialIconKind.ArrowCollapseAll, "Restore layout"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 12, 12),
            IsVisible = false
        };
        _restoreButton.Click += (_, _) =>
        {
            if (_vm?.MaximizedDockGroupId is { } id) _vm.ToggleMaximizeDockGroup(id);
        };

        Children.Add(_layoutHost);
        Children.Add(_restoreButton);
        Children.Add(_overlay);
    }

    /// <summary>The canvas area shown inside the document host pane.</summary>
    public Control? DocumentContent
    {
        get => GetValue(DocumentContentProperty);
        set => SetValue(DocumentContentProperty, value);
    }

    internal ImageEditorViewModel? ViewModel => _vm;

    /// <summary>Number of panes currently rendered (one per visible dock group).</summary>
    public int RenderedPaneCount => _groupViews.Count;

    // ── View model wiring ───────────────────────────────────────────────────

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataContextProperty)
        {
            AttachViewModel(DataContext as ImageEditorViewModel);
        }
        else if (change.Property == DocumentContentProperty)
        {
            Rebuild();
        }
    }

    private void AttachViewModel(ImageEditorViewModel? vm)
    {
        if (ReferenceEquals(vm, _vm)) return;
        if (_vm != null) _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = vm;
        foreach (var page in _pageCache.Values) page.DataContext = vm;

        if (_vm != null) _vm.PropertyChanged += OnViewModelPropertyChanged;
        Rebuild();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ImageEditorViewModel.DockLayoutVersion):
            case nameof(ImageEditorViewModel.MaximizedDockGroupId):
                QueueRebuild();
                break;
            case nameof(ImageEditorViewModel.DockActivationVersion):
                foreach (var view in _groupViews) view.UpdateActiveState();
                break;
            case nameof(ImageEditorViewModel.IsDraggingPanel):
                if (_vm is { IsDraggingPanel: false } && _dragging) AbortDrag(); // Esc from the editor
                break;
        }
    }

    /// <summary>
    /// Rebuilds on the next dispatcher turn: structural changes usually come from inside a tab's own
    /// pointer handler, which must not be detached while it runs.
    /// </summary>
    private void QueueRebuild()
    {
        if (_rebuildQueued) return;
        _rebuildQueued = true;
        Dispatcher.UIThread.Post(Rebuild);
    }

    // ── Tree rendering ──────────────────────────────────────────────────────

    private void Rebuild()
    {
        _rebuildQueued = false;
        if (_dragging) AbortDrag();

        foreach (var page in _pageCache.Values) DockPageFactory.Detach(page);
        var document = DocumentContent;
        if (document != null)
        {
            DockPageFactory.Detach(document);
            if (_vm != null) document.DataContext = _vm;
        }

        _groupViews.Clear();
        if (_vm == null)
        {
            _layoutHost.Child = null;
            _restoreButton.IsVisible = false;
            return;
        }

        DockNode root = _vm.DockRoot;
        if (_vm.MaximizedDockGroupId is { } maximizedId && DockLayoutOps.FindGroup(root, maximizedId) is { } maximized)
        {
            root = maximized;
        }

        _layoutHost.Child = BuildNode(root, document);
        _restoreButton.IsVisible = _vm.IsDockMaximized;
    }

    private Control BuildNode(DockNode node, Control? document) => node switch
    {
        DockSplitNode split => BuildSplit(split, document),
        DockGroupNode group => BuildGroup(group, document),
        _ => new Panel()
    };

    private Control BuildGroup(DockGroupNode group, Control? document)
    {
        var view = new DockGroupView(this, group, group.IsDocumentHost ? document : null);
        _groupViews.Add(view);
        return view;
    }

    private Control BuildSplit(DockSplitNode split, Control? document)
    {
        bool horizontal = split.Orientation == DockOrientation.Horizontal;
        var fixedSide = DockLayoutOps.FixedSide(split);
        bool firstHasDocument = DockLayoutOps.ContainsDocumentHost(split.First);
        bool secondHasDocument = DockLayoutOps.ContainsDocumentHost(split.Second);

        GridLength firstLength = fixedSide switch
        {
            DockFixedSide.First => new GridLength(split.PanelSize, GridUnitType.Pixel),
            DockFixedSide.Second => new GridLength(1, GridUnitType.Star),
            _ => new GridLength(split.Ratio, GridUnitType.Star)
        };
        GridLength secondLength = fixedSide switch
        {
            DockFixedSide.Second => new GridLength(split.PanelSize, GridUnitType.Pixel),
            DockFixedSide.First => new GridLength(1, GridUnitType.Star),
            _ => new GridLength(1 - split.Ratio, GridUnitType.Star)
        };
        double firstMin = firstHasDocument ? DocumentMinSize : PanelMinSize;
        double secondMin = secondHasDocument ? DocumentMinSize : PanelMinSize;

        var grid = new Grid();
        if (horizontal)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(firstLength) { MinWidth = firstMin });
            grid.ColumnDefinitions.Add(new ColumnDefinition(SplitterThickness, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(secondLength) { MinWidth = secondMin });
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition(firstLength) { MinHeight = firstMin });
            grid.RowDefinitions.Add(new RowDefinition(SplitterThickness, GridUnitType.Pixel));
            grid.RowDefinitions.Add(new RowDefinition(secondLength) { MinHeight = secondMin });
        }

        var first = BuildNode(split.First, document);
        var second = BuildNode(split.Second, document);
        var splitter = new GridSplitter
        {
            Classes = { "ps-splitter" },
            ResizeDirection = horizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext
        };

        if (horizontal)
        {
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(second, 2);
        }
        else
        {
            Grid.SetRow(splitter, 1);
            Grid.SetRow(second, 2);
        }

        splitter.DragCompleted += (_, _) =>
        {
            double a = horizontal ? grid.ColumnDefinitions[0].ActualWidth : grid.RowDefinitions[0].ActualHeight;
            double b = horizontal ? grid.ColumnDefinitions[2].ActualWidth : grid.RowDefinitions[2].ActualHeight;
            if (a + b <= 0) return;
            double panelSize = fixedSide switch
            {
                DockFixedSide.First => a,
                DockFixedSide.Second => b,
                _ => split.PanelSize
            };
            _vm?.SetDockSplitSize(split.Id, a / (a + b), panelSize);
        };

        grid.Children.Add(first);
        grid.Children.Add(splitter);
        grid.Children.Add(second);
        return grid;
    }

    /// <summary>The docked view of a page, created once and re-parented on every rebuild.</summary>
    internal Control? GetPage(string pageId)
    {
        if (!_pageCache.TryGetValue(pageId, out var page))
        {
            page = DockPageFactory.Create(pageId);
            if (page == null) return null;
            page.DataContext = _vm;
            _pageCache[pageId] = page;
        }

        return page;
    }

    // ── Tab drag & drop ─────────────────────────────────────────────────────

    internal void OnTabPointerPressed(DockGroupView view, string pageId, Border tab, PointerPressedEventArgs e)
    {
        _pressedPageId = pageId;
        _pressPoint = e.GetPosition(this);
        _pointer = e.Pointer;
        _dragging = false;
        _pressedGroupId = view.Group.Id;
        e.Pointer.Capture(tab);
    }

    internal void OnTabPointerMoved(PointerEventArgs e)
    {
        if (_pressedPageId == null || _vm == null) return;
        var point = e.GetPosition(this);

        if (!_dragging)
        {
            var delta = point - _pressPoint;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;
            BeginDrag(_pressedPageId, point);
        }

        UpdateDrag(point);
    }

    internal void OnTabPointerReleased(PointerReleasedEventArgs e)
    {
        string? pageId = _pressedPageId;
        string? groupId = _pressedGroupId;
        bool wasDragging = _dragging;
        var target = wasDragging ? HitTest(e.GetPosition(this)) : null;

        ResetDragState();
        e.Pointer.Capture(null);
        if (_vm == null || pageId == null) return;

        if (!wasDragging)
        {
            if (groupId != null) _vm.SelectDockTab(groupId, pageId);
            return;
        }

        if (target is not { } drop)
        {
            _vm.CancelPanelDrag();
            return;
        }

        _vm.EndPanelDrag();
        switch (drop.Kind)
        {
            case DropKind.Float:
                _vm.FloatDockPage(pageId);
                break;
            case DropKind.WorkspaceEdge:
                _vm.DockPageAtWorkspaceEdge(pageId, drop.Position);
                break;
            case DropKind.TabStrip:
                _vm.MoveDockPage(pageId, drop.GroupId!, DockDropPosition.Center, drop.Index);
                break;
            case DropKind.Pane:
                _vm.MoveDockPage(pageId, drop.GroupId!, drop.Position);
                break;
        }
    }

    internal void OnTabCaptureLost()
    {
        if (_dragging) AbortDrag();
        else ResetDragState();
    }

    private void BeginDrag(string pageId, Point point)
    {
        _dragging = true;
        var page = DockPageRegistry.Find(pageId);
        _ghostTitle.Text = page?.Title ?? pageId;
        _ghostIcon.Kind = page != null ? DockPageFactory.IconOf(page) : MaterialIconKind.TuneVertical;
        _vm?.StartPanelDrag(pageId, point.X, point.Y);
        ShowOverlay();
    }

    private void UpdateDrag(Point point)
    {
        ShowPreview(HitTest(point));
        Canvas.SetLeft(_ghost, point.X + 14);
        Canvas.SetTop(_ghost, point.Y + 14);
    }

    private void AbortDrag()
    {
        var pointer = _pointer;
        bool wasDragging = _dragging;
        ResetDragState();
        pointer?.Capture(null);
        if (wasDragging) _vm?.CancelPanelDrag();
    }

    private void ResetDragState()
    {
        _pressedPageId = null;
        _pressedGroupId = null;
        _pointer = null;
        _dragging = false;
        HideOverlay();
    }

    // ── Hit-testing ─────────────────────────────────────────────────────────

    private enum DropKind
    {
        Float,
        WorkspaceEdge,
        TabStrip,
        Pane
    }

    private readonly record struct DropTarget(DropKind Kind, string? GroupId, DockDropPosition Position, int Index, Rect Preview, double CaretX, Rect CaretBounds);

    private Rect FloatBadgeRect() =>
        new((Bounds.Width - FloatBadgeWidth) / 2, 10, FloatBadgeWidth, FloatBadgeHeight);

    private DropTarget? HitTest(Point p)
    {
        var size = Bounds.Size;
        if (p.X < 0 || p.Y < 0 || p.X > size.Width || p.Y > size.Height) return null;

        if (FloatBadgeRect().Inflate(4).Contains(p))
        {
            return new DropTarget(DropKind.Float, null, DockDropPosition.Center, -1, default, 0, default);
        }

        if (_vm is { IsDockMaximized: false })
        {
            var edge = p.X < OuterEdgeBand ? DockDropPosition.Left
                : p.X > size.Width - OuterEdgeBand ? DockDropPosition.Right
                : p.Y < OuterEdgeBand ? DockDropPosition.Top
                : p.Y > size.Height - OuterEdgeBand ? DockDropPosition.Bottom
                : DockDropPosition.Center;
            if (edge != DockDropPosition.Center)
            {
                return new DropTarget(DropKind.WorkspaceEdge, null, edge, -1, EdgeRect(new Rect(size), edge, 280, 220), 0, default);
            }
        }

        foreach (var view in _groupViews)
        {
            if (view.TranslatePoint(default, this) is not { } origin) continue;
            var rect = new Rect(origin, view.Bounds.Size);
            if (!rect.Contains(p)) continue;

            if (view.TabStrip is { } strip && strip.TranslatePoint(default, this) is { } stripOrigin)
            {
                var stripRect = new Rect(stripOrigin, strip.Bounds.Size);
                if (stripRect.Contains(p))
                {
                    var (index, caretX) = view.InsertionAt(p.X);
                    return new DropTarget(DropKind.TabStrip, view.Group.Id, DockDropPosition.Center, index, default,
                        caretX, new Rect(caretX - 1, stripRect.Y + 3, 2, stripRect.Height - 6));
                }
            }

            double rx = (p.X - rect.X) / Math.Max(1, rect.Width);
            double ry = (p.Y - rect.Y) / Math.Max(1, rect.Height);
            var position = rx < EdgeZone ? DockDropPosition.Left
                : rx > 1 - EdgeZone ? DockDropPosition.Right
                : ry < EdgeZone ? DockDropPosition.Top
                : ry > 1 - EdgeZone ? DockDropPosition.Bottom
                : DockDropPosition.Center;

            // The canvas never holds tabs; only its edges accept drops.
            if (view.Group.IsDocumentHost && position == DockDropPosition.Center) return null;

            var preview = position == DockDropPosition.Center
                ? rect
                : EdgeRect(rect, position, rect.Width / 3, rect.Height / 3);
            return new DropTarget(DropKind.Pane, view.Group.Id, position, -1, preview, 0, default);
        }

        return null;
    }

    private static Rect EdgeRect(Rect r, DockDropPosition edge, double width, double height)
    {
        width = Math.Min(width, r.Width / 2);
        height = Math.Min(height, r.Height / 2);
        return edge switch
        {
            DockDropPosition.Left => new Rect(r.X, r.Y, width, r.Height),
            DockDropPosition.Right => new Rect(r.Right - width, r.Y, width, r.Height),
            DockDropPosition.Top => new Rect(r.X, r.Y, r.Width, height),
            DockDropPosition.Bottom => new Rect(r.X, r.Bottom - height, r.Width, height),
            _ => r
        };
    }

    // ── Overlay ─────────────────────────────────────────────────────────────

    private void ShowOverlay()
    {
        var badge = FloatBadgeRect();
        Canvas.SetLeft(_floatBadge, badge.X);
        Canvas.SetTop(_floatBadge, badge.Y);
        _floatBadge.IsVisible = true;
        _ghost.IsVisible = true;
        _overlay.IsVisible = true;
    }

    private void HideOverlay()
    {
        _overlay.IsVisible = false;
        _dropPreview.IsVisible = false;
        _caret.IsVisible = false;
        _floatBadge.Classes.Set("hot", false);
    }

    private void ShowPreview(DropTarget? target)
    {
        _floatBadge.Classes.Set("hot", target?.Kind == DropKind.Float);
        _caret.IsVisible = target?.Kind == DropKind.TabStrip;
        _dropPreview.IsVisible = target?.Kind is DropKind.Pane or DropKind.WorkspaceEdge;

        if (target is not { } t) return;
        if (t.Kind == DropKind.TabStrip)
        {
            Place(_caret, t.CaretBounds);
        }
        else if (_dropPreview.IsVisible)
        {
            Place(_dropPreview, t.Preview.Deflate(2));
        }
    }

    private static void Place(Control control, Rect rect)
    {
        Canvas.SetLeft(control, rect.X);
        Canvas.SetTop(control, rect.Y);
        control.Width = Math.Max(0, rect.Width);
        control.Height = Math.Max(0, rect.Height);
    }

    /// <summary>
    /// Shows the drop preview for <paramref name="position"/> over a pane without a pointer drag
    /// (used by the headless snapshot suite). Pass null to hide it.
    /// </summary>
    public void ShowDropPreviewForSnapshot(string? groupId, DockDropPosition position, string pageId = "Layers")
    {
        if (groupId == null)
        {
            HideOverlay();
            return;
        }

        var view = _groupViews.FirstOrDefault(v => v.Group.Id == groupId);
        if (view?.TranslatePoint(default, this) is not { } origin) return;
        var rect = new Rect(origin, view.Bounds.Size);
        var centre = rect.Center;
        var probe = position switch
        {
            DockDropPosition.Left => new Point(rect.X + rect.Width * 0.1, centre.Y),
            DockDropPosition.Right => new Point(rect.Right - rect.Width * 0.1, centre.Y),
            DockDropPosition.Top => new Point(centre.X, rect.Y + Math.Max(40, rect.Height * 0.1)),
            DockDropPosition.Bottom => new Point(centre.X, rect.Bottom - rect.Height * 0.1),
            _ => centre
        };

        var page = DockPageRegistry.Find(pageId);
        _ghostTitle.Text = page?.Title ?? pageId;
        _ghostIcon.Kind = page != null ? DockPageFactory.IconOf(page) : MaterialIconKind.TuneVertical;
        ShowOverlay();
        UpdateDrag(probe);
    }

    private static StackPanel IconLabel(MaterialIconKind icon, string text)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(new MaterialIcon { Kind = icon, Width = 14, Height = 14 });
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }
}
