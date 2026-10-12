using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies visual layout, backdrop dimming, typography, and controls across all studio modal dialogs.
/// </summary>
public static class DialogSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;

        // 1. Color Range Selection Workspace
        vm.OpenColorRangeDialogCommand.Execute(null);
        ctx.Capture("dialog_color_range.png");
        vm.CancelColorRangeDialogCommand.Execute(null);

        // 2. Filter - Dust & Scratches
        vm.OpenFilterDialogCommand.Execute("DustAndScratches");
        ctx.Capture("dialog_filter_dust_scratches.png");
        vm.CancelFilterDialogCommand.Execute(null);

        // 3. Filter - Camera Raw Filter
        vm.OpenFilterDialogCommand.Execute("CameraRaw");
        ctx.Capture("dialog_filter_camera_raw.png");
        vm.CancelFilterDialogCommand.Execute(null);

        // 3b. Filter - Color to Alpha
        vm.OpenFilterDialogCommand.Execute("ColorToAlpha");
        ctx.Capture("dialog_filter_color_to_alpha.png");
        vm.CancelFilterDialogCommand.Execute(null);

        // 4. Adjustment - Curves
        vm.OpenAdjustmentDialogCommand.Execute("Curves");
        ctx.Capture("dialog_adjustment_curves.png");
        vm.CancelAdjustmentDialogCommand.Execute(null);

        // 5. Adjustment - Hue / Saturation
        vm.OpenAdjustmentDialogCommand.Execute("HueSaturation");
        ctx.Capture("dialog_adjustment_hue_saturation.png");
        vm.CancelAdjustmentDialogCommand.Execute(null);

        // 6. Layer Style (fx) Workspace
        vm.OpenLayerStyleDialogCommand.Execute(null);
        ctx.Capture("dialog_layer_style.png");
        vm.CloseLayerStyleDialogCommand.Execute(null);

        // 7. Export As Studio Dialog
        vm.OpenExportDialogCommand.Execute(null);
        ctx.Capture("dialog_export_as.png");
        vm.CancelExportCommand.Execute(null);

        // 8. New Document Studio Preset Dialog
        vm.OpenNewDocumentDialogCommand.Execute(null);
        ctx.Capture("dialog_new_document.png");
        vm.CloseNewDocumentDialogCommand.Execute(null);

        // 9. Command Palette (Ctrl+K)
        vm.ToggleCommandPaletteCommand.Execute(null);
        ctx.Capture("dialog_command_palette.png");
        vm.ToggleCommandPaletteCommand.Execute(null);

        // 10. Liquify Displacement Mesh Workspace
        vm.OpenLiquifyDialogCommand.Execute(null);
        ctx.Capture("dialog_liquify.png");
        vm.CancelLiquifyDialogCommand.Execute(null);

        // 11. New Guide Dialog
        vm.OpenNewGuideDialogCommand.Execute(null);
        ctx.Capture("dialog_new_guide.png");
        vm.CancelNewGuideDialogCommand.Execute(null);

        // 12. New Guide Layout Dialog (Columns, Rows, Gutters, Margins)
        vm.OpenNewGuideLayoutDialogCommand.Execute(null);
        ctx.Capture("dialog_new_guide_layout.png");
        vm.CancelNewGuideLayoutDialogCommand.Execute(null);

        // 13. Warp Text Studio Dialog (Arc, Flag, Wave, Fish, Twist...)
        vm.OpenWarpTextDialogCommand.Execute(null);
        ctx.Capture("dialog_warp_text.png");
        vm.CancelWarpTextDialogCommand.Execute(null);

        // 14. Confirm Discard / Save Prompt Dialog
        vm.PromptConfirmDiscard("Save changes to the document \"Untitled Design\" before closing?", () => { });
        ctx.Capture("dialog_confirm_discard.png");
        vm.CancelDiscardCommand.Execute(null);

        // 15. Photoshop Studio Color Picker Dialog
        vm.OpenColorPicker("Foreground");
        ctx.Capture("dialog_color_picker_foreground.png");
        vm.CancelColorPickerCommand.Execute(null);

        vm.OpenColorPicker("Background");
        ctx.Capture("dialog_color_picker_background.png");
        vm.CancelColorPickerCommand.Execute(null);

        ctx.FlushUi();
    }
}
