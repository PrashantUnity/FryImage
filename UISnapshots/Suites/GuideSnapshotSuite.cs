using Avalonia;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies interactive ruler-pulled guides, canvas guide rendering, live HUD coordinate badges,
/// and multi-column guide layouts.
/// </summary>
public static class GuideSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;
        var canvas = vm.CanvasControl;
        if (canvas == null) return;

        // 1. Guides on Canvas (Horizontal and Vertical)
        canvas.ClearUserGuides();
        canvas.AddUserGuide(isVertical: true, position: 200.0);
        canvas.AddUserGuide(isVertical: true, position: 600.0);
        canvas.AddUserGuide(isVertical: false, position: 150.0);
        canvas.AddUserGuide(isVertical: false, position: 450.0);
        canvas.InvalidateVisual();
        ctx.Capture("20_user_guides_canvas.png");

        // 2. Interactive Guide Drag with Floating HUD Pill Badge
        // Simulate pulling a horizontal guide down to Y = 280 px
        canvas.BeginGuideDragFromRuler(isVertical: false, new Point(350, 280));
        canvas.UpdateGuideDrag(isVertical: false, new Point(350, 280));
        canvas.InvalidateVisual();
        ctx.Capture("21_guide_drag_hud_badge.png");

        // Release the drag
        canvas.EndGuideDrag(commit: true, new Point(350, 280));

        // 3. Multi-Column / Multi-Row Guide Layout Grid
        canvas.ApplyGuideLayout(
            columns: 8,
            rows: 4,
            colWidth: 0,
            rowHeight: 0,
            colGutter: 16,
            rowGutter: 16,
            marginTop: 40,
            marginLeft: 40,
            marginBottom: 40,
            marginRight: 40,
            clearExisting: true);
        canvas.InvalidateVisual();
        ctx.Capture("22_guide_layout_grid.png");

        // Clean up guides for subsequent suites
        canvas.ClearUserGuides();
        canvas.InvalidateVisual();
        ctx.FlushUi();
    }
}
