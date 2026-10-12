using System.Linq;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies mathematical closed-form text warp rendering deformations directly on canvas.
/// </summary>
public static class WarpSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;
        var textEl = vm.Elements.OfType<TextElement>().FirstOrDefault();
        if (textEl == null) return;

        // 1. Arc Warp (+50% bend)
        textEl.Warp = new TextWarpSettings
        {
            Style = TextWarpStyle.Arc,
            Bend = 50.0,
            IsHorizontal = true
        };
        vm.CanvasControl?.InvalidateVisual();
        ctx.Capture("07_warped_text_arc.png");

        // 2. Flag Warp (+40% bend)
        textEl.Warp = new TextWarpSettings
        {
            Style = TextWarpStyle.Flag,
            Bend = 40.0,
            IsHorizontal = true
        };
        vm.CanvasControl?.InvalidateVisual();
        ctx.Capture("08_warped_text_flag.png");

        // 3. Wave Warp (+40% bend)
        textEl.Warp = new TextWarpSettings
        {
            Style = TextWarpStyle.Wave,
            Bend = 40.0,
            IsHorizontal = true
        };
        vm.CanvasControl?.InvalidateVisual();
        ctx.Capture("09_warped_text_wave.png");

        // Reset warp back to none
        textEl.Warp = new TextWarpSettings { Style = TextWarpStyle.None };
        vm.CanvasControl?.InvalidateVisual();
        ctx.FlushUi();
    }
}
