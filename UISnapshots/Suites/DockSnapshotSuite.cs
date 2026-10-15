using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.ImageEditor.Controls.Docking;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies visual layout for every dock page and the dockable tab workspace
/// (presets, grouping, splitting, outer-edge docking, drop preview and maximize).
/// </summary>
public static class DockSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;
        vm.ApplyDefaultWorkspaceCommand.Execute(null);
        ctx.Capture("dock_default_workspace.png");

        // 1. Brushes Preset & Settings Page
        vm.SelectRightDockTabCommand.Execute("Brushes");
        ctx.Capture("dock_brushes_tab.png");

        // 2. Swatches Page
        vm.SelectColorDockTabCommand.Execute("Swatches");
        ctx.Capture("dock_swatches_tab.png");

        // 3. Gradients Preset Library Page
        vm.SelectColorDockTabCommand.Execute("Gradients");
        ctx.Capture("dock_gradients_tab.png");

        // Reset color tab to Color picker
        vm.SelectColorDockTabCommand.Execute("Color");

        // 4. Channels Page (RGB, Red, Green, Blue)
        vm.SelectRightDockTabCommand.Execute("Channels");
        ctx.Capture("dock_channels_tab.png");

        // 5. History Time-Travel Page
        vm.SelectRightDockTabCommand.Execute("History");
        ctx.Capture("dock_history_tab.png");

        // 6. Properties Page (Document Mode)
        vm.SelectRightDockTabCommand.Execute("Properties");
        ctx.Capture("dock_properties_document_tab.png");

        // 6b. Properties Page (Character OpenType & Typography Mode)
        var textEl = new TextElement
        {
            Text = "Typography Elegance",
            FontSize = 28,
            StandardLigatures = true,
            ContextualAlternates = true,
            DiscretionaryLigatures = true,
            Swash = true
        };
        vm.Elements.Add(textEl);
        vm.SelectedElement = textEl;
        vm.SelectRightDockTabCommand.Execute("Properties");
        ctx.Capture("dock_properties_character_opentype.png");
        vm.Elements.Remove(textEl);
        vm.SelectedElement = null;

        // 7. Navigator Page (Canvas thumbnail & Zoom slider)
        vm.SelectRightDockTabCommand.Execute("Navigator");
        ctx.Capture("dock_navigator_tab.png");

        // 8. Info Page (Document specs & Cursor readouts)
        vm.SelectRightDockTabCommand.Execute("Info");
        ctx.Capture("dock_info_tab.png");

        // Reset to Properties / Layers pages
        vm.SelectRightDockTabCommand.Execute("Properties");
        vm.SelectRightDockTabCommand.Execute("Layers");
        ctx.FlushUi();

        // 9. Preset: 4-Split (Layers left, Color below the canvas, Properties right)
        vm.ApplyUnityFourSplitWorkspaceCommand.Execute(null);
        ctx.Capture("dock_unity_4split_workspace.png");

        // 10. Preset: 2-by-3 (Layers left, Properties over Color right)
        vm.ApplyUnityTwoByThreeWorkspaceCommand.Execute(null);
        ctx.Capture("dock_unity_2by3_workspace.png");

        // 11. Preset: every page as a tab of one right-hand group
        vm.ApplyUnityTabbedWorkspaceCommand.Execute(null);
        ctx.Capture("dock_tabbed_mode.png");

        vm.ApplyDefaultWorkspaceCommand.Execute(null);
        ctx.FlushUi();
        var workspace = ctx.Window.GetVisualDescendants().OfType<DockWorkspace>().FirstOrDefault()
            ?? throw new InvalidOperationException("DockWorkspace not found in the editor view.");

        // 12. Drag overlay: drop preview over the bottom edge of the Layers pane + float badge + ghost
        workspace.ShowDropPreviewForSnapshot(GroupId(vm, "Layers"), DockDropPosition.Bottom, "Swatches");
        ctx.Capture("dock_drag_drop_overlay.png");
        workspace.ShowDropPreviewForSnapshot(null, DockDropPosition.Center);

        // 13. Grouping: Swatches joins the Layers pane as a tab
        vm.MoveDockPage("Swatches", GroupId(vm, "Layers"), DockDropPosition.Center);
        ctx.Capture("dock_grouped_tab.png");

        // 14. Splitting: Layers dropped on the canvas's bottom edge (pane below the canvas)
        vm.MoveDockPage("Layers", DockGroupNode.DocumentHostId, DockDropPosition.Bottom);
        ctx.Capture("dock_split_below_canvas.png");

        // 15. Outer-edge docking: History as a full-height column on the far left
        vm.DockPageAtWorkspaceEdge("History", DockDropPosition.Left);
        ctx.Capture("dock_outer_edge_column.png");

        // 16. Maximize the Properties pane, then restore
        vm.ToggleMaximizeDockGroup(GroupId(vm, "Properties"));
        ctx.Capture("dock_maximized_pane.png");
        vm.ToggleMaximizeDockGroup(GroupId(vm, "Properties"));

        ctx.FlushUi();
        if (workspace.RenderedPaneCount != DockLayoutOps.Groups(vm.DockRoot).Count())
            throw new InvalidOperationException("DockWorkspace did not render one pane per dock group.");

        // 17. Real pointer input: click a tab, drag a tab onto a pane edge, drag a tab between tabs
        vm.ApplyDefaultWorkspaceCommand.Execute(null);
        ctx.FlushUi();

        var channels = TabCenter(ctx, "Channels");
        ctx.Window.MouseDown(channels, MouseButton.Left);
        ctx.Window.MouseUp(channels, MouseButton.Left);
        ctx.FlushUi();
        if (DockLayoutOps.FindGroupOf(vm.DockRoot, "Channels")?.ActivePage != "Channels")
            throw new InvalidOperationException("Clicking the Channels tab should activate it.");

        var layersPane = PaneBounds(ctx, "Layers");
        var dropOnLayersBottom = new Point(layersPane.Center.X, layersPane.Bottom - layersPane.Height * 0.08);
        var swatches = TabCenter(ctx, "Swatches");
        ctx.Window.MouseDown(swatches, MouseButton.Left);
        ctx.Window.MouseMove(swatches + new Vector(12, 12), RawInputModifiers.LeftMouseButton);
        ctx.Window.MouseMove(dropOnLayersBottom, RawInputModifiers.LeftMouseButton);
        ctx.Capture("dock_pointer_drag_preview.png");
        ctx.Window.MouseUp(dropOnLayersBottom, MouseButton.Left);
        ctx.FlushUi();
        var swatchesPane = DockLayoutOps.FindGroupOf(vm.DockRoot, "Swatches");
        var layersGroup = DockLayoutOps.FindGroupOf(vm.DockRoot, "Layers");
        if (swatchesPane == null || layersGroup == null
            || DockLayoutOps.FindParent(vm.DockRoot, swatchesPane) is not { Orientation: DockOrientation.Vertical } swatchesSplit
            || swatchesSplit.First != layersGroup || vm.IsDraggingPanel)
            throw new InvalidOperationException("Dragging the Swatches tab onto the Layers pane's bottom edge should split it below Layers.");
        ctx.Capture("dock_pointer_drag_dropped.png");

        var paths = TabCenter(ctx, "Paths");
        var beforeLayersTab = TabCenter(ctx, "Layers") - new Vector(TabWidth(ctx, "Layers") / 2 - 3, 0);
        ctx.Window.MouseDown(paths, MouseButton.Left);
        ctx.Window.MouseMove(paths - new Vector(12, 0), RawInputModifiers.LeftMouseButton);
        ctx.Window.MouseMove(beforeLayersTab, RawInputModifiers.LeftMouseButton);
        ctx.Window.MouseUp(beforeLayersTab, MouseButton.Left);
        ctx.FlushUi();
        if (DockLayoutOps.FindGroupOf(vm.DockRoot, "Paths")?.Pages.FirstOrDefault() != "Paths")
            throw new InvalidOperationException("Dragging the Paths tab before the Layers tab should reorder it to the front.");

        Console.WriteLine("  ✓ Pointer-driven tab activation, edge split and tab reorder verified.");

        // 18. Restore Default Studio Workspace
        vm.ApplyDefaultWorkspaceCommand.Execute(null);
        ctx.FlushUi();
    }

    private static Border Tab(SnapshotContext ctx, string pageId) =>
        ctx.Window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("ps-dock-tab") && Equals(b.Tag, pageId))
        ?? throw new InvalidOperationException($"Tab '{pageId}' is not rendered.");

    private static Point TabCenter(SnapshotContext ctx, string pageId)
    {
        var tab = Tab(ctx, pageId);
        return tab.TranslatePoint(new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2), ctx.Window)
            ?? throw new InvalidOperationException($"Tab '{pageId}' has no position.");
    }

    private static double TabWidth(SnapshotContext ctx, string pageId) => Tab(ctx, pageId).Bounds.Width;

    private static Rect PaneBounds(SnapshotContext ctx, string pageId)
    {
        var pane = Tab(ctx, pageId).GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("ps-dock-group"));
        var origin = pane.TranslatePoint(default, ctx.Window) ?? throw new InvalidOperationException("Pane has no position.");
        return new Rect(origin, pane.Bounds.Size);
    }

    private static string GroupId(ImageEditorViewModel vm, string pageId) =>
        DockLayoutOps.FindGroupOf(vm.DockRoot, pageId)?.Id
        ?? throw new InvalidOperationException($"Page '{pageId}' is not docked.");
}
