using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies visual layout for all studio right-dock panels and tab views.
/// </summary>
public static class DockSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;

        // 1. Brushes Preset & Settings Dock Panel
        vm.SelectRightDockTabCommand.Execute("Brushes");
        ctx.Capture("dock_brushes_tab.png");

        // 2. Color / Swatches Dock Panel
        vm.SelectColorDockTabCommand.Execute("Swatches");
        ctx.Capture("dock_swatches_tab.png");

        // 3. Gradients Preset Library Dock Panel
        vm.SelectColorDockTabCommand.Execute("Gradients");
        ctx.Capture("dock_gradients_tab.png");

        // Reset color tab to Color picker
        vm.SelectColorDockTabCommand.Execute("Color");

        // 4. Channels Dock Panel (RGB, Red, Green, Blue)
        vm.SelectRightDockTabCommand.Execute("Channels");
        ctx.Capture("dock_channels_tab.png");

        // 5. History Time-Travel Dock Panel
        vm.SelectRightDockTabCommand.Execute("History");
        ctx.Capture("dock_history_tab.png");

        // Reset to Layers dock panel
        vm.SelectRightDockTabCommand.Execute("Layers");
        ctx.FlushUi();
    }
}
