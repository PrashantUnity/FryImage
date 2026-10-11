using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PdfEditorApp.Plugins.ImageEditor.Runner;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("Initializing Avalonia Headless engine...");
        BuildAvaloniaApp().SetupWithoutStarting();

        var outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../snapshots"));
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"Snapshot output directory: {outputDir}");

        var settingsStore = new StandaloneSettingsStore();
        var serviceProvider = new StandaloneServiceProvider(settingsStore);

        var window = new MainWindow(serviceProvider)
        {
            Width = 1440,
            Height = 900
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        if (window.DataContext is not ImageEditorViewModel vm)
        {
            Console.WriteLine("Error: Window DataContext is not ImageEditorViewModel");
            return;
        }

        // 1. Snapshot Default Launch State (Move Tool Active)
        CaptureSnapshot(window, Path.Combine(outputDir, "01_main_window_move_tool.png"));

        // 2. Snapshot Brush Tool Active
        vm.SetToolCommand.Execute("brush");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "02_brush_tool_options.png"));

        // 3. Snapshot Marquee Tool Active
        vm.SetToolCommand.Execute("marquee");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "03_marquee_tool_options.png"));

        // 4. Snapshot Text Tool Active
        vm.SetToolCommand.Execute("text");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "04_text_tool_options.png"));

        // 5. Snapshot Crop Tool Active
        vm.SetToolCommand.Execute("crop");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "05_crop_tool_options.png"));

        // 6. Snapshot Gradient Tool Active
        vm.SetToolCommand.Execute("gradient");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "06_gradient_tool_options.png"));

        // Reset to Move tool
        vm.SetToolCommand.Execute("select");
        Dispatcher.UIThread.RunJobs();

        // 7. Snapshot Color Range Dialog
        vm.OpenColorRangeDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "07_color_range_dialog.png"));
        vm.CancelColorRangeDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 8. Snapshot Filter Dialog (Dust & Scratches)
        vm.OpenFilterDialogCommand.Execute("DustAndScratches");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "08_filter_dialog_dust_scratches.png"));
        vm.CancelFilterDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 9. Snapshot Filter Dialog (Camera Raw)
        vm.OpenFilterDialogCommand.Execute("CameraRaw");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "09_filter_dialog_camera_raw.png"));
        vm.CancelFilterDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 10. Snapshot Adjustment Dialog (Curves)
        vm.OpenAdjustmentDialogCommand.Execute("Curves");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "10_adjustment_dialog_curves.png"));
        vm.CancelAdjustmentDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 11. Snapshot Adjustment Dialog (Hue/Saturation)
        vm.OpenAdjustmentDialogCommand.Execute("HueSaturation");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "11_adjustment_dialog_hue_saturation.png"));
        vm.CancelAdjustmentDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 12. Snapshot Layer Style Dialog
        vm.OpenLayerStyleDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "12_layer_style_dialog.png"));
        vm.CloseLayerStyleDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 13. Snapshot Export As Dialog
        vm.OpenExportDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "13_export_dialog.png"));
        vm.CancelExportCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 14. Snapshot New Document Dialog
        vm.OpenNewDocumentDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "14_new_document_dialog.png"));
        vm.CloseNewDocumentDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 15. Snapshot Command Palette (Ctrl+K)
        vm.ToggleCommandPaletteCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "15_command_palette.png"));
        vm.ToggleCommandPaletteCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 16. Snapshot Dock Panel - Brushes
        vm.SelectRightDockTabCommand.Execute("Brushes");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "16_dock_brushes_tab.png"));

        // 17. Snapshot Dock Panel - Channels
        vm.SelectRightDockTabCommand.Execute("Channels");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "17_dock_channels_tab.png"));

        // 18. Snapshot Dock Panel - History
        vm.SelectRightDockTabCommand.Execute("History");
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "18_dock_history_tab.png"));

        // Reset to Layers
        vm.SelectRightDockTabCommand.Execute("Layers");
        Dispatcher.UIThread.RunJobs();

        // 19. Snapshot Liquify Dialog (Studio Mesh Workspace)
        vm.OpenLiquifyDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        CaptureSnapshot(window, Path.Combine(outputDir, "19_liquify_dialog.png"));
        vm.CancelLiquifyDialogCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Console.WriteLine("All headless UI snapshots captured successfully!");
    }

    private static void CaptureSnapshot(Window window, string filePath)
    {
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        if (frame != null)
        {
            using var fs = File.Create(filePath);
            frame.Save(fs);
            Console.WriteLine($"Captured: {Path.GetFileName(filePath)} ({frame.PixelSize.Width}x{frame.PixelSize.Height}, {new FileInfo(filePath).Length} bytes)");
        }
        else
        {
            Console.WriteLine($"Warning: CaptureRenderedFrame returned null for {Path.GetFileName(filePath)}");
        }
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
