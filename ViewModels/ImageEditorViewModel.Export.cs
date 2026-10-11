using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Codecs;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Export As Studio Modal ─────────────────────────────────────────────
    [ObservableProperty] private bool _isExportDialogOpen;
    [ObservableProperty] private string _exportFormat = "PNG"; // PNG, JPEG, WEBP, FITS, BMP
    [ObservableProperty] private double _exportScale = 1.0; // 0.5, 1.0, 2.0, 3.0
    [ObservableProperty] private int _exportQuality = 90;
    [ObservableProperty] private string _exportDimensionsLabel = "";
    [ObservableProperty] private string _exportEstimatedSizeLabel = "";

    public string ExportDimensionsText => ExportDimensionsLabel;
    public string ExportEstimatedSizeText => ExportEstimatedSizeLabel;

    public string ExportScaleStr
    {
        get => ExportScale.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var s))
            {
                ExportScale = s;
                UpdateExportMetrics();
                OnPropertyChanged(nameof(ExportScaleStr));
            }
        }
    }

    partial void OnExportFormatChanged(string value)
    {
        UpdateExportMetrics();
    }

    [RelayCommand]
    public void OpenExportDialog()
    {
        UpdateExportMetrics();
        IsExportDialogOpen = true;
    }

    [RelayCommand]
    private void CloseExportDialog() => IsExportDialogOpen = false;

    [RelayCommand]
    private void SetExportFormat(string format)
    {
        ExportFormat = format;
        UpdateExportMetrics();
    }

    [RelayCommand]
    private void SetExportScale(string scaleStr)
    {
        if (double.TryParse(scaleStr, System.Globalization.CultureInfo.InvariantCulture, out var s))
        {
            ExportScale = s;
            UpdateExportMetrics();
        }
    }

    private void UpdateExportMetrics()
    {
        int w = Math.Max(1, (int)(_canvasWidth * ExportScale));
        int h = Math.Max(1, (int)(_canvasHeight * ExportScale));
        ExportDimensionsLabel = $"{w} × {h} px";

        double pixels = (double)w * h;
        double estBytes = ExportFormat switch
        {
            "JPEG" => pixels * 0.25 * (ExportQuality / 100.0),
            "WEBP" => pixels * 0.18 * (ExportQuality / 100.0),
            "FITS" => pixels * 3.0, // Planar 8-bit RGB
            "BMP" => pixels * 4.0,  // 32-bit uncompressed
            _ => pixels * 1.5       // PNG
        };

        if (estBytes > 1024 * 1024)
            ExportEstimatedSizeLabel = $"~{(estBytes / (1024 * 1024)):0.1} MB";
        else
            ExportEstimatedSizeLabel = $"~{(estBytes / 1024):0} KB";

        OnPropertyChanged(nameof(ExportDimensionsText));
        OnPropertyChanged(nameof(ExportEstimatedSizeText));
    }

    [RelayCommand]
    private async Task ConfirmExportAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage || CanvasControl == null) return;

            string fmt = (ExportFormat ?? "PNG").Trim().ToUpperInvariant();
            string ext = fmt switch
            {
                "JPEG" or "JPG" => "jpg",
                "WEBP" => "webp",
                "FITS" => "fits",
                "BMP" => "bmp",
                _ => "png"
            };

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Export As {fmt}",
                SuggestedFileName = $"canvas_export_{ExportScale:0.#}x.{ext}",
                DefaultExtension = $".{ext}",
                FileTypeChoices =
                [
                    new FilePickerFileType($"{fmt} Image") { Patterns = [$"*.{ext}"] }
                ]
            });

            if (file == null) return;

            using var bitmap = RenderCanvasToBitmap(ExportScale);
            if (bitmap == null) return;

            await using var outStream = await file.OpenWriteAsync();

            if (fmt == "FITS")
            {
                using var pngMs = new MemoryStream();
                bitmap.Save(pngMs, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                pngMs.Position = 0;
                using var skBmp = SKBitmap.Decode(pngMs);
                using var bgraBmp = new SKBitmap(skBmp.Width, skBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(bgraBmp);
                canvas.DrawBitmap(skBmp, 0, 0);
                canvas.Flush();

                FitsCodec.Encode(bgraBmp.Bytes, bgraBmp.Width, bgraBmp.Height, outStream);
            }
            else if (fmt == "WEBP")
            {
                using var pngMs = new MemoryStream();
                bitmap.Save(pngMs, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                pngMs.Position = 0;
                using var skBmp = SKBitmap.Decode(pngMs);
                using var data = skBmp.Encode(SKEncodedImageFormat.Webp, ExportQuality);
                data.SaveTo(outStream);
            }
            else if (fmt == "BMP")
            {
                using var pngMs = new MemoryStream();
                bitmap.Save(pngMs, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                pngMs.Position = 0;
                using var skBmp = SKBitmap.Decode(pngMs);
                using var data = skBmp.Encode(SKEncodedImageFormat.Bmp, 100);
                data.SaveTo(outStream);
            }
            else if (fmt is "JPEG" or "JPG")
            {
                var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".jpg");
                try
                {
                    bitmap.Save(tempPath, new Avalonia.Media.Imaging.JpegBitmapEncoderOptions { Quality = ExportQuality });
                    var bytes = await File.ReadAllBytesAsync(tempPath);
                    await outStream.WriteAsync(bytes);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }
            else
            {
                var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
                try
                {
                    bitmap.Save(tempPath, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    var bytes = await File.ReadAllBytesAsync(tempPath);
                    await outStream.WriteAsync(bytes);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }

            IsExportDialogOpen = false;
            StatusMessage = $"Exported {ExportDimensionsLabel} to {file.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }
}
