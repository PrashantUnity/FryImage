using System.Linq;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies visual layout and tool options bar for each studio editing tool.
/// </summary>
public static class ToolSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;

        // 1. Move / Selection Tool (Default launch)
        vm.SetToolCommand.Execute("select");
        ctx.Capture("01_main_window_move_tool.png");

        // 1b. Two-Column Tool Palette Mode (Studio Parity - Image 1)
        vm.IsTwoColumnToolPalette = true;
        vm.ActivateSubToolCommand.Execute("objectselection");
        ctx.Capture("01b_two_column_palette_object_selection.png");

        // 1c. Tool Flyout Context Menu (Studio Parity - Image 2)
        var toolPalette = ctx.Window.GetVisualDescendants().OfType<PdfEditorApp.Plugins.ImageEditor.Controls.ToolPaletteControl>().FirstOrDefault();
        if (toolPalette != null)
        {
            var wandBtn = toolPalette.GetVisualDescendants().OfType<Avalonia.Controls.Button>()
                .FirstOrDefault(b => b.ContextMenu != null && b.Classes.Contains("active"));
            if (wandBtn?.ContextMenu != null)
            {
                wandBtn.ContextMenu.Open(wandBtn);
                ctx.FlushUi();
                ctx.Capture("01c_tool_flyout_context_menu.png");
                wandBtn.ContextMenu.Close();
                ctx.FlushUi();
            }
        }

        vm.IsTwoColumnToolPalette = false;
        vm.SetToolCommand.Execute("select");

        // 2. Brush Tool Options Bar
        vm.SetToolCommand.Execute("brush");
        ctx.Capture("02_brush_tool_options.png");

        // 3. Marquee Selection Tool Options Bar
        vm.SetToolCommand.Execute("marquee");
        ctx.Capture("03_marquee_tool_options.png");

        // 4. Text Tool Options Bar
        vm.SetToolCommand.Execute("text");
        ctx.Capture("04_text_tool_options.png");

        // 5. Crop Tool Options Bar
        vm.SetToolCommand.Execute("crop");
        ctx.Capture("05_crop_tool_options.png");

        // 6. Gradient Tool Options Bar
        vm.SetToolCommand.Execute("gradient");
        ctx.Capture("06_gradient_tool_options.png");

        // 7. Pen Tool Options Bar
        vm.SetToolCommand.Execute("pen");
        ctx.Capture("10_pen_tool_options.png");

        // 8. Clone Stamp Tool Options Bar
        vm.SetToolCommand.Execute("clonestamp");
        ctx.Capture("11_clonestamp_tool_options.png");

        // 9. Eyedropper Tool Options Bar
        vm.SetToolCommand.Execute("eyedropper");
        ctx.Capture("12_eyedropper_tool_options.png");

        // 10. Dodge Tool Options Bar
        vm.SetToolCommand.Execute("dodge");
        ctx.Capture("13_dodge_tool_options.png");

        // 11. Lasso Tool Options Bar (Standard & Magnetic)
        vm.SetToolCommand.Execute("lasso");
        vm.LassoSubMode = "standard";
        ctx.Capture("14_lasso_tool_options.png");
        vm.LassoSubMode = "magnetic";
        ctx.Capture("14b_magnetic_lasso_options.png");
        vm.LassoSubMode = "standard";

        // 12. Magic Wand Tool Options Bar
        vm.SetToolCommand.Execute("wand");
        ctx.Capture("15_magic_wand_tool_options.png");

        // 13. Shape Tool Options Bar
        vm.SetToolCommand.Execute("rect");
        ctx.Capture("16_shape_tool_options.png");

        // 14. Red Eye Tool Options Bar
        vm.SetToolCommand.Execute("redeye");
        ctx.Capture("17_redeye_tool_options.png");

        // 15. Patch Tool Options Bar
        vm.SetToolCommand.Execute("patch");
        ctx.Capture("18_patch_tool_options.png");

        // 16. Ruler Tool Options Bar
        vm.SetToolCommand.Execute("ruler");
        vm.UpdateRulerMeasurement(new Avalonia.Point(50, 50), new Avalonia.Point(250, 180));
        ctx.Capture("19_ruler_tool_options.png");

        // 17. Rotate View Tool Options Bar
        vm.SetToolCommand.Execute("rotateview");
        vm.ViewportRotationAngle = 25.0;
        ctx.Capture("20_rotate_view_tool_options.png");
        vm.ResetViewportRotationCommand.Execute(null);

        // 18. Quick Selection Tool Options Bar (W)
        vm.ActivateSubTool("quickselection");
        ctx.Capture("21_quick_selection_options.png");

        // 19. Object Selection Tool Options Bar (W)
        vm.ActivateSubTool("objectselection");
        ctx.Capture("22_object_selection_options.png");

        // 20. Modern Floating Contextual Task Bar
        vm.IsContextualTaskBarVisible = true;
        ctx.Capture("23_contextual_taskbar.png");

        // Reset to Move tool
        vm.SetToolCommand.Execute("select");
        ctx.FlushUi();
    }
}
