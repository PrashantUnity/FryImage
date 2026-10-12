using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies high-zoom pixel grid rendering (discrete 1px pixel boundaries when zoom >= 500%).
/// </summary>
public static class PixelGridSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;
        var canvas = vm.CanvasControl;
        if (canvas == null) return;

        // 1. High-zoom (600% / 6.0x) with Pixel Grid visible
        vm.ZoomLevel = 6.0;
        canvas.SetZoom(6.0);
        canvas.ShowPixelGrid = true;
        canvas.InvalidateVisual();
        ctx.Capture("25_pixel_grid_high_zoom.png");

        // 2. High-zoom with Pixel Grid disabled
        canvas.ShowPixelGrid = false;
        canvas.InvalidateVisual();
        ctx.Capture("26_pixel_grid_disabled.png");

        // Clean up & restore standard zoom
        canvas.ShowPixelGrid = true;
        vm.ZoomLevel = 1.5;
        canvas.SetZoom(1.5);
        canvas.InvalidateVisual();
        ctx.FlushUi();
    }
}
