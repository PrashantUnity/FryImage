using System;
using System.Linq;
using Avalonia;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Audits rotated selection handles, transform bounding box orientation,
/// and live dimension HUD badge rendering.
/// </summary>
public static class TransformSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;
        var canvas = vm.CanvasControl;
        if (canvas == null) return;

        // 1. Select RectangleElement
        var rectEl = vm.Elements.OfType<RectangleElement>().FirstOrDefault();
        if (rectEl == null) return;

        vm.SetToolCommand.Execute("select");
        vm.SelectedElement = rectEl;
        canvas.SetSelection(new[] { rectEl });

        // Rotate element by 25 degrees to verify oriented transform handles
        double origRot = rectEl.RotationDegrees;
        rectEl.RotationDegrees = 25.0;
        canvas.InvalidateVisual();
        ctx.FlushUi();

        // 2. Capture rotated selection handles
        ctx.Capture("33_transform_rotated_handles.png");

        rectEl.Width = 320;
        rectEl.Height = 180;

        // We can simulate resize state on the canvas
        // Trigger live dimension badge
        canvas.InvalidateVisual();
        ctx.FlushUi();

        // 4. Capture resized state
        ctx.Capture("34_transform_dimension_hud_badge.png");

        // Restore original rotation and size
        rectEl.RotationDegrees = origRot;
        rectEl.Width = 240;
        rectEl.Height = 140;
        canvas.InvalidateVisual();
        ctx.FlushUi();
    }
}
