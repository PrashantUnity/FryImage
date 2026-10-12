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

        // Reset to Move tool
        vm.SetToolCommand.Execute("select");
        ctx.FlushUi();
    }
}
