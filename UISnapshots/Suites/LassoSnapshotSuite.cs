using System;
using System.Collections.Generic;
using Avalonia;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Audits Freehand, Polygonal, and Magnetic Lasso (Intelligent Scissors) interaction,
/// live-wire path rendering, anchor knots, and selection completion.
/// </summary>
public static class LassoSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;
        var canvas = vm.CanvasControl;
        if (canvas == null) return;

        // 1. Set Magnetic Lasso Tool
        vm.SetToolCommand.Execute("lasso");
        vm.LassoSubMode = "magnetic";
        canvas.LassoMode = "magnetic";
        canvas.MagneticWidth = 15;
        canvas.MagneticContrast = 10;
        canvas.MagneticFrequency = 60;
        ctx.FlushUi();

        // 2. Simulate planting knots on canvas
        var knots = new List<Point>
        {
            new Point(80, 80),
            new Point(180, 75),
            new Point(240, 140),
            new Point(210, 230),
            new Point(100, 210)
        };

        canvas.StartLasso(knots[0]);
        for (int i = 1; i < knots.Count; i++)
        {
            canvas.AddLassoKnot(knots[i]);
        }

        // Live cursor position currently hovering near origin (triggers close-loop circle indicator)
        canvas.UpdateLassoCursor(new Point(82, 82));
        canvas.InvalidateVisual();
        ctx.FlushUi();

        // 3. Capture active magnetic lasso with anchor knots & highlight on origin
        ctx.Capture("30_magnetic_lasso_live_wire_knots.png");

        // 4. Complete selection
        canvas.CompleteLassoSelection();
        canvas.InvalidateVisual();
        ctx.FlushUi();
        ctx.Capture("31_magnetic_lasso_selection_completed.png");

        // Reset tool to select
        vm.SetToolCommand.Execute("select");
        ctx.FlushUi();
    }
}
