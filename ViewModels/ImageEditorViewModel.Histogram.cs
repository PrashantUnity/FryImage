using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Models;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Continuous RGB Ribbon Histogram (FryImage / Camera Raw) ───────────
    [ObservableProperty] private int[]? _histogramRed = new int[256];
    [ObservableProperty] private int[]? _histogramGreen = new int[256];
    [ObservableProperty] private int[]? _histogramBlue = new int[256];
    [ObservableProperty] private int[]? _histogramLuminance = new int[256];
    [ObservableProperty] private string _histogramChannel = "RGB"; // RGB, Red, Green, Blue, Luminosity
    [ObservableProperty] private double _histogramMean = 128;
    [ObservableProperty] private double _histogramMedian = 128;
    [ObservableProperty] private double _histogramStdDev = 45;
    [ObservableProperty] private int _histogramTotalPixels;
    [ObservableProperty] private bool _isHistogramComputing;

    [RelayCommand]
    private void SetHistogramChannel(string channel) => HistogramChannel = channel;

    public void ScheduleHistogramUpdate()
    {
        _ = UpdateHistogramAsync();
    }

    public async Task UpdateHistogramAsync()
    {
        if (IsHistogramComputing) return;

        try
        {
            IsHistogramComputing = true;
            if (CanvasControl == null) return;
            using var rtb = RenderCanvasToBitmap(0.25);
            if (rtb == null) return;

            using var ms = new MemoryStream();
            rtb.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            var bytes = ms.ToArray();

            var (r, g, b, lum, mean, median, stdDev, total) = await Task.Run(() =>
            {
                using var sk = SKBitmap.Decode(bytes);
                if (sk == null) return (new int[256], new int[256], new int[256], new int[256], 128.0, 128.0, 0.0, 0);
                return ImageAdjustmentProcessor.ComputeHistogram(sk);
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                HistogramRed = r;
                HistogramGreen = g;
                HistogramBlue = b;
                HistogramLuminance = lum;
                HistogramMean = mean;
                HistogramMedian = median;
                HistogramStdDev = stdDev;
                HistogramTotalPixels = total;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Histogram] update error: {ex}");
        }
        finally
        {
            IsHistogramComputing = false;
        }
    }
}
