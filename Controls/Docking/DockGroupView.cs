using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Docking;

/// <summary>
/// One pane of the dock workspace: a tab strip (with ＋ reopen and ⋯ pane menus) above the active page.
/// The document host pane shows the canvas area instead and has no tab strip.
/// </summary>
internal sealed class DockGroupView : Border
{
    private readonly DockWorkspace _workspace;
    private readonly StackPanel? _tabsPanel;
    private readonly Border _contentHost;
    private readonly List<(string PageId, Border Tab)> _tabs = new();

    public DockGroupView(DockWorkspace workspace, DockGroupNode group, Control? documentContent)
    {
        _workspace = workspace;
        Group = group;
        Classes.Add(group.IsDocumentHost ? "ps-dock-host" : "ps-dock-group");

        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        _contentHost = new Border { ClipToBounds = true };
        Grid.SetRow(_contentHost, 1);
        layout.Children.Add(_contentHost);

        if (group.IsDocumentHost)
        {
            if (documentContent != null)
            {
                DockPageFactory.Detach(documentContent);
                _contentHost.Child = documentContent;
            }
        }
        else
        {
            _tabsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            TabStrip = BuildTabStrip(_tabsPanel);
            layout.Children.Add(TabStrip);
            foreach (var pageId in group.Pages)
            {
                if (DockPageRegistry.Find(pageId) is { } page)
                {
                    var tab = BuildTab(page);
                    _tabs.Add((pageId, tab));
                    _tabsPanel.Children.Add(tab);
                }
            }
        }

        Child = layout;
        UpdateActiveState();
    }

    public DockGroupNode Group { get; }

    /// <summary>The tab strip (drop target for grouping/reordering); null for the document host.</summary>
    public Border? TabStrip { get; }

    public IReadOnlyList<(string PageId, Border Tab)> Tabs => _tabs;

    /// <summary>Marks the active tab and shows its page (no tab rebuild, safe inside tab event handlers).</summary>
    public void UpdateActiveState()
    {
        if (Group.IsDocumentHost) return;

        foreach (var (pageId, tab) in _tabs)
        {
            bool active = pageId == Group.ActivePage;
            tab.Classes.Set("active", active);
            if (active)
            {
                // Crowded strips scroll: keep the active tab visible once it has been laid out.
                Dispatcher.UIThread.Post(tab.BringIntoView, DispatcherPriority.Background);
            }
        }

        var page = Group.ActivePage == null ? null : _workspace.GetPage(Group.ActivePage);
        if (page == null)
        {
            _contentHost.Child = new TextBlock
            {
                Text = "Drag a tab here",
                Classes = { "ps-dock-empty" },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return;
        }

        if (!ReferenceEquals(_contentHost.Child, page))
        {
            DockPageFactory.Detach(page);
            _contentHost.Child = page;
        }
    }

    /// <summary>Index at which a tab dropped at <paramref name="x"/> (workspace coordinates) is inserted, and the caret x.</summary>
    public (int Index, double CaretX) InsertionAt(double x)
    {
        double caret = 0;
        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i].Tab;
            var origin = tab.TranslatePoint(default, _workspace);
            if (origin == null) continue;
            double left = origin.Value.X;
            double right = left + tab.Bounds.Width;
            if (x < (left + right) / 2) return (i, left);
            caret = right;
        }

        if (_tabs.Count == 0 && TabStrip?.TranslatePoint(default, _workspace) is { } stripOrigin)
        {
            caret = stripOrigin.X + 4;
        }

        return (_tabs.Count, caret);
    }

    // ── Construction ────────────────────────────────────────────────────────

    private Border BuildTabStrip(StackPanel tabsPanel)
    {
        var tabsScroller = new ScrollViewer
        {
            Content = tabsPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        tabsScroller.PointerWheelChanged += (_, e) =>
        {
            double max = Math.Max(0, tabsScroller.Extent.Width - tabsScroller.Viewport.Width);
            double step = (Math.Abs(e.Delta.Y) > Math.Abs(e.Delta.X) ? e.Delta.Y : e.Delta.X) * 40;
            tabsScroller.Offset = new Vector(Math.Clamp(tabsScroller.Offset.X - step, 0, max), 0);
            e.Handled = true;
        };

        var addButton = StripButton(MaterialIconKind.Plus, "Open a closed panel here");
        var addFlyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        addFlyout.Opening += (_, _) => PopulateAddMenu(addFlyout);
        addButton.Flyout = addFlyout;

        var menuButton = StripButton(MaterialIconKind.DotsHorizontal, "Pane options");
        var paneFlyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        paneFlyout.Opening += (_, _) => PopulatePaneMenu(paneFlyout);
        menuButton.Flyout = paneFlyout;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1, Margin = new Thickness(2, 0) };
        buttons.Children.Add(addButton);
        buttons.Children.Add(menuButton);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(tabsScroller);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        return new Border { Classes = { "ps-dock-tabstrip" }, Child = grid };
    }

    private static Button StripButton(MaterialIconKind icon, string tip)
    {
        var button = new Button
        {
            Classes = { "ps-dock-strip-btn" },
            Content = new MaterialIcon { Kind = icon, Width = 13, Height = 13 }
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    private Border BuildTab(DockPageDescriptor page)
    {
        var icon = new MaterialIcon { Kind = DockPageFactory.IconOf(page), Width = 13, Height = 13, Classes = { "ps-dock-tab-icon" } };
        var title = new TextBlock { Text = page.Title, Classes = { "ps-dock-tab-title" }, VerticalAlignment = VerticalAlignment.Center };

        var close = new Button
        {
            Classes = { "ps-dock-tab-close" },
            Content = new MaterialIcon { Kind = MaterialIconKind.Close, Width = 11, Height = 11 }
        };
        ToolTip.SetTip(close, $"Close {page.Title}");
        close.Click += (_, e) =>
        {
            e.Handled = true;
            _workspace.ViewModel?.CloseDockPage(page.Id);
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(icon);
        row.Children.Add(title);
        row.Children.Add(close);

        var accent = new Border { Classes = { "ps-dock-tab-accent" }, VerticalAlignment = VerticalAlignment.Top };
        var content = new Panel();
        content.Children.Add(row);
        content.Children.Add(accent);

        var tab = new Border { Classes = { "ps-dock-tab" }, Child = content, Tag = page.Id };
        ToolTip.SetTip(tab, $"{page.Title}: drag to group or split, double-click to maximize");
        tab.ContextMenu = BuildTabContextMenu(page);

        tab.PointerPressed += (_, e) => OnTabPointerPressed(tab, page, e);
        tab.PointerMoved += (_, e) => _workspace.OnTabPointerMoved(e);
        tab.PointerReleased += (_, e) => _workspace.OnTabPointerReleased(e);
        tab.PointerCaptureLost += (_, _) => _workspace.OnTabCaptureLost();
        return tab;
    }

    private void OnTabPointerPressed(Border tab, DockPageDescriptor page, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(tab);
        if (point.Properties.IsMiddleButtonPressed)
        {
            e.Handled = true;
            _workspace.ViewModel?.CloseDockPage(page.Id);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return;

        e.Handled = true;
        if (e.ClickCount >= 2)
        {
            _workspace.ViewModel?.ToggleMaximizeDockGroup(Group.Id);
            return;
        }

        _workspace.OnTabPointerPressed(this, page.Id, tab, e);
    }

    private ContextMenu BuildTabContextMenu(DockPageDescriptor page)
    {
        var menu = new ContextMenu();
        menu.Opening += (_, _) =>
        {
            var vm = _workspace.ViewModel;
            menu.Items.Clear();
            if (vm == null) return;
            menu.Items.Add(Item($"Float {page.Title} in Window", MaterialIconKind.OpenInNew, () => vm.FloatDockPage(page.Id)));
            menu.Items.Add(Item($"Close {page.Title}", MaterialIconKind.Close, () => vm.CloseDockPage(page.Id)));
            menu.Items.Add(Item("Close Other Tabs", MaterialIconKind.TabRemove, () => vm.CloseOtherDockPages(page.Id), Group.Pages.Count > 1));
            menu.Items.Add(new Separator());
            menu.Items.Add(MaximizeItem(vm));
        };
        menu.Items.Add(new MenuItem { Header = page.Title }); // replaced on Opening; keeps the menu non-empty
        return menu;
    }

    private void PopulateAddMenu(MenuFlyout flyout)
    {
        flyout.Items.Clear();
        var vm = _workspace.ViewModel;
        if (vm == null) return;

        var closed = vm.ClosedDockPages;
        if (closed.Count == 0)
        {
            flyout.Items.Add(new MenuItem { Header = "All panels are open", IsEnabled = false });
            return;
        }

        foreach (var page in closed)
        {
            flyout.Items.Add(Item(page.Title, DockPageFactory.IconOf(page), () => vm.ReopenDockPage(page.Id, Group.Id)));
        }
    }

    private void PopulatePaneMenu(MenuFlyout flyout)
    {
        flyout.Items.Clear();
        var vm = _workspace.ViewModel;
        if (vm == null) return;

        var active = DockPageRegistry.Find(Group.ActivePage);
        if (active != null)
        {
            flyout.Items.Add(Item($"Float {active.Title} in Window", MaterialIconKind.OpenInNew, () => vm.FloatDockPage(active.Id)));
            flyout.Items.Add(Item($"Close {active.Title}", MaterialIconKind.Close, () => vm.CloseDockPage(active.Id)));
            flyout.Items.Add(Item("Close Other Tabs", MaterialIconKind.TabRemove, () => vm.CloseOtherDockPages(active.Id), Group.Pages.Count > 1));
        }

        flyout.Items.Add(Item("Close Pane", MaterialIconKind.CloseBoxOutline, () => vm.CloseDockGroup(Group.Id)));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(MaximizeItem(vm));
    }

    private MenuItem MaximizeItem(ImageEditorViewModel vm)
    {
        bool maximized = vm.MaximizedDockGroupId == Group.Id;
        return Item(maximized ? "Restore Layout" : "Maximize Pane",
            maximized ? MaterialIconKind.ArrowCollapseAll : MaterialIconKind.ArrowExpandAll,
            () => vm.ToggleMaximizeDockGroup(Group.Id));
    }

    private static MenuItem Item(string header, MaterialIconKind icon, System.Action action, bool enabled = true)
    {
        var item = new MenuItem
        {
            Header = header,
            IsEnabled = enabled,
            Icon = new MaterialIcon { Kind = icon, Width = 12, Height = 12 }
        };
        item.Click += (_, _) => action();
        return item;
    }

    public override string ToString() => $"DockGroupView({Group.Id}: {string.Join(", ", Group.Pages.DefaultIfEmpty("document"))})";
}
