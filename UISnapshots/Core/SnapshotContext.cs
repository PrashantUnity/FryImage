using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PdfEditorApp.Plugins.ImageEditor;
using PdfEditorApp.Plugins.ImageEditor.Runner;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

/// <summary>
/// Execution context for headless UI snapshots.
/// Encapsulates the host window, view model, dispatcher pumping, and frame capture.
/// </summary>
public sealed class SnapshotContext
{
    public Window Window { get; }
    public ImageEditorViewModel ViewModel { get; }
    public string OutputDirectory { get; }

    public SnapshotContext(Window window, ImageEditorViewModel viewModel, string outputDirectory)
    {
        Window = window;
        ViewModel = viewModel;
        OutputDirectory = outputDirectory;
    }

    public static SnapshotContext Initialize(int width = 1440, int height = 900)
    {
        var outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../snapshots"));
        Directory.CreateDirectory(outputDir);

        var settingsStore = new StandaloneSettingsStore();
        var serviceProvider = new StandaloneServiceProvider(settingsStore);

        var window = new MainWindow(serviceProvider)
        {
            Width = width,
            Height = height
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        if (window.DataContext is not ImageEditorViewModel vm)
        {
            throw new InvalidOperationException("MainWindow DataContext is not ImageEditorViewModel.");
        }

        return new SnapshotContext(window, vm, outputDir);
    }

    public void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
    }

    public void Capture(string fileName)
    {
        FlushUi();
        var frame = Window.CaptureRenderedFrame();
        if (frame != null)
        {
            var destination = Path.Combine(OutputDirectory, fileName);
            using var fs = File.Create(destination);
            frame.Save(fs, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            Console.WriteLine($"[Snapshot] Captured {fileName} ({frame.PixelSize.Width}x{frame.PixelSize.Height}, {new FileInfo(destination).Length:N0} bytes)");
        }
        else
        {
            Console.WriteLine($"[Snapshot Warning] CaptureRenderedFrame returned null for {fileName}");
        }
    }
}
