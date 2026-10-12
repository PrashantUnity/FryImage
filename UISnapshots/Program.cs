using System;
using Avalonia;
using Avalonia.Headless;
using PdfEditorApp.Plugins.ImageEditor.Runner;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots;

/// <summary>
/// Headless UI Visual Audit Runner for FryImage.
/// Orchestrates modular snapshot suites to audit rendering, alignments, typography, and dialog layouts.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine(" FryImage Headless Visual Audit Runner");
        Console.WriteLine("==================================================");

        BuildAvaloniaApp().SetupWithoutStarting();

        var ctx = SnapshotContext.Initialize();
        Console.WriteLine($"Snapshot output directory: {ctx.OutputDirectory}");

        Console.WriteLine("\n[1/3] Running Tool Options Suite...");
        ToolSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[2/3] Running Studio Dialog Suite...");
        DialogSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[3/4] Running Studio Dock Panels Suite...");
        DockSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[4/5] Running Text Warp Canvas Deformations Suite...");
        WarpSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[5/6] Running Interactive Guides & Snapping Suite...");
        GuideSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[6/8] Running Vector Paths & Quick Mask Suite...");
        VectorPathSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[7/8] Running High-Zoom Pixel Grid Suite...");
        PixelGridSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[8/9] Running Content-Aware Seam Carving & Fill Suite...");
        ContentAwareSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[9/10] Running Lasso & Magnetic Live-Wire Suite...");
        LassoSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[10/11] Running Magic Wand Flood-Fill Tolerance Suite...");
        MagicWandSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[11/12] Running Oriented Transform & Handles Suite...");
        TransformSnapshotSuite.Run(ctx);

        Console.WriteLine("\n[12/12] Running Photoshop Parity & Codecs Suite...");
        ParityFeatureSnapshotSuite.Run(ctx);

        Console.WriteLine("\n==================================================");
        Console.WriteLine(" All Visual Audit Snapshots Captured Successfully!");
        Console.WriteLine("==================================================");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .WithInterFont();
}
