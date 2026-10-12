using System;
using Avalonia;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Audits Magic Wand color tolerance flood-fill and global selection masks.
/// </summary>
public static class MagicWandSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;
        var canvas = vm.CanvasControl;
        if (canvas == null) return;

        // 1. Select Magic Wand Tool
        vm.SetToolCommand.Execute("wand");
        vm.MagicWandTolerance = 45;
        vm.MagicWandContiguous = true;
        vm.MagicWandAntiAlias = true;
        ctx.FlushUi();

        // 2. Execute Magic Wand selection on central subject
        canvas.ExecuteMagicWandSelection(new Point(200, 150));
        canvas.InvalidateVisual();
        ctx.FlushUi();

        // 3. Capture Magic Wand active selection mask on canvas
        ctx.Capture("32_magic_wand_selection_canvas.png");

        // Turn off Quick Mask mode and reset tool
        vm.IsQuickMaskMode = false;
        vm.SetToolCommand.Execute("select");
        ctx.FlushUi();
    }
}
