using System;
using Avalonia;
using PdfEditorApp.Plugins.ImageEditor.Core.Vector;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies vector document paths (Bezier curves, anchors, handles) and Quick Mask mode ruby rendering.
/// </summary>
public static class VectorPathSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;
        var canvas = vm.CanvasControl;
        if (canvas == null) return;

        // 1. Vector Document Path with Anchors and Bezier Handles
        var path = new VectorDocumentPath("Test Path 1");
        var subpath = new VectorSubpath { IsClosed = true };

        // Knot 1: Top-Left
        var k1 = new PathKnot(new Point(180, 200))
        {
            HandleIn = new Point(140, 160),
            HandleOut = new Point(220, 240)
        };
        // Knot 2: Center-Right
        var k2 = new PathKnot(new Point(450, 150))
        {
            HandleIn = new Point(390, 120),
            HandleOut = new Point(510, 180)
        };
        // Knot 3: Bottom-Center
        var k3 = new PathKnot(new Point(320, 380))
        {
            HandleIn = new Point(400, 390),
            HandleOut = new Point(240, 370)
        };

        subpath.Knots.Add(k1);
        subpath.Knots.Add(k2);
        subpath.Knots.Add(k3);
        path.Subpaths.Add(subpath);

        canvas.ActiveDocumentPath = path;
        vm.SetToolCommand.Execute("pen");
        canvas.InvalidateVisual();
        ctx.Capture("23_vector_path_knots.png");

        // 2. Quick Mask Mode (Ruby overlay with transparent cutout)
        vm.IsQuickMaskMode = true;
        if (canvas.QuickMaskBuffer != null)
        {
            int w = (int)Math.Max(1, vm.CanvasWidth);
            int h = (int)Math.Max(1, vm.CanvasHeight);
            // Cut out a soft circular window in center (reveal area)
            int cx = w / 2;
            int cy = h / 2;
            int r = 100;
            for (int y = Math.Max(0, cy - r); y < Math.Min(h, cy + r); y++)
            {
                for (int x = Math.Max(0, cx - r); x < Math.Min(w, cx + r); x++)
                {
                    double dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (dist < r)
                    {
                        byte maskVal = (byte)(dist / r * 255);
                        canvas.QuickMaskBuffer[y * w + x] = maskVal;
                    }
                }
            }
        }
        canvas.InvalidateVisual();
        ctx.Capture("24_quick_mask_mode.png");

        // Clean up
        vm.IsQuickMaskMode = false;
        canvas.ActiveDocumentPath = null;
        vm.SetToolCommand.Execute("select");
        canvas.InvalidateVisual();
        ctx.FlushUi();
    }
}
