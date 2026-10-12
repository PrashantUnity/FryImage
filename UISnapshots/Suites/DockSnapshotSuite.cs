using PdfEditorApp.Plugins.ImageEditor.Models;
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

        // 6. Properties Dock Panel (Document Mode)
        vm.SelectRightDockTabCommand.Execute("Properties");
        ctx.Capture("dock_properties_document_tab.png");

        // 6b. Properties Dock Panel (Character OpenType & Typography Mode)
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

        // 7. Navigator Dock Panel (Canvas thumbnail & Zoom slider)
        vm.SelectRightDockTabCommand.Execute("Navigator");
        ctx.Capture("dock_navigator_tab.png");

        // 8. Info Dock Panel (Document specs & Cursor readouts)
        vm.SelectRightDockTabCommand.Execute("Info");
        ctx.Capture("dock_info_tab.png");

        // Reset to Layers dock panel
        vm.SelectRightDockTabCommand.Execute("Layers");
        ctx.FlushUi();
    }
}
