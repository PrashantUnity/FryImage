using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Controls;
using PdfEditorApp.Plugins.ImageEditor.Core.Codecs;
using PdfEditorApp.Plugins.ImageEditor.Models;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Export As Studio Modal (Photoshop Parity) ────────
    [ObservableProperty] private bool _isExportDialogOpen;
    [ObservableProperty] private string _exportFormat = "PNG"; // PNG, JPEG, WEBP, FITS, BMP
    [ObservableProperty] private double _exportScale = 1.0; // 0.5, 1.0, 2.0, 3.0
    [ObservableProperty] private int _exportQuality = 90;
    [ObservableProperty] private bool _exportWebpLossless = false;
    [ObservableProperty] private bool _isBatchLayerExport = false;
    [ObservableProperty] private int _batchLayerExportCount = 0;
    [ObservableProperty] private string _exportDialogTitle = "Export As";
    [ObservableProperty] private string _exportDimensionsLabel = "";
    [ObservableProperty] private string _exportEstimatedSizeLabel = "";

    private readonly List<CanvasElement> _exportTargetLayers = new();

    public string ExportDimensionsText => ExportDimensionsLabel;
    public string ExportEstimatedSizeText => ExportEstimatedSizeLabel;

    public bool IsQualityVisible => ExportFormat is "JPEG" or "JPG" || (ExportFormat is "WEBP" && !ExportWebpLossless);
    public bool IsWebpFormat => ExportFormat is "WEBP";

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
        OnPropertyChanged(nameof(IsQualityVisible));
        OnPropertyChanged(nameof(IsWebpFormat));
    }

    partial void OnExportWebpLosslessChanged(bool value)
    {
        UpdateExportMetrics();
        OnPropertyChanged(nameof(IsQualityVisible));
    }

    partial void OnExportQualityChanged(int value)
    {
        UpdateExportMetrics();
    }

    [RelayCommand]
    public void OpenExportDialog()
    {
        if (SelectedElements.Count > 1)
        {
            OpenExportLayersDialog(SelectedElements);
            return;
        }

        IsBatchLayerExport = false;
        _exportTargetLayers.Clear();
        BatchLayerExportCount = 0;
        ExportDialogTitle = "Export As";

        UpdateExportMetrics();
        IsExportDialogOpen = true;
    }

    [RelayCommand]
    public void OpenExportSelectedLayersDialog()
    {
        var layers = SelectedElements.Count > 0 ? SelectedElements : Elements;
        OpenExportLayersDialog(layers);
    }

    public void OpenExportLayersDialog(IEnumerable<CanvasElement> layers)
    {
        _exportTargetLayers.Clear();
        _exportTargetLayers.AddRange(layers);

        IsBatchLayerExport = _exportTargetLayers.Count > 1;
        BatchLayerExportCount = _exportTargetLayers.Count;
        ExportDialogTitle = IsBatchLayerExport
            ? $"Export As: {BatchLayerExportCount} layers"
            : "Export As";

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

        if (IsBatchLayerExport && _exportTargetLayers.Count > 0)
        {
            var first = _exportTargetLayers[0];
            int fw = Math.Max(1, (int)(first.Width * ExportScale));
            int fh = Math.Max(1, (int)(first.Height * ExportScale));
            ExportDimensionsLabel = $"{fw} × {fh} px ({_exportTargetLayers.Count} files)";
        }
        else
        {
            ExportDimensionsLabel = $"{w} × {h} px";
        }

        double pixels = (double)w * h;
        double estBytes = ExportFormat switch
        {
            "JPEG" => pixels * 0.25 * (ExportQuality / 100.0),
            "WEBP" => ExportWebpLossless ? pixels * 0.45 : pixels * 0.18 * (ExportQuality / 100.0),
            "FITS" => pixels * 3.0, // Planar 8-bit RGB
            "BMP" => pixels * 4.0,  // 32-bit uncompressed
            _ => pixels * 1.5       // PNG
        };

        if (IsBatchLayerExport && _exportTargetLayers.Count > 1)
        {
            estBytes *= _exportTargetLayers.Count * 0.6; // Average layer size estimate
        }

        if (estBytes > 1024 * 1024)
            ExportEstimatedSizeLabel = $"~{(estBytes / (1024 * 1024)):0.1} MB";
        else
            ExportEstimatedSizeLabel = $"~{(estBytes / 1024):0} KB";

        OnPropertyChanged(nameof(ExportDimensionsText));
        OnPropertyChanged(nameof(ExportEstimatedSizeText));
        OnPropertyChanged(nameof(IsQualityVisible));
        OnPropertyChanged(nameof(IsWebpFormat));
    }

    public RenderTargetBitmap? RenderElementToBitmap(CanvasElement el, double scale = 1.0)
    {
        if (CanvasControl == null) return null;
        int w = Math.Max(1, (int)(el.Width * scale));
        int h = Math.Max(1, (int)(el.Height * scale));
        var pixelSize = new PixelSize(w, h);
        var rtb = new RenderTargetBitmap(pixelSize);
        using var dc = rtb.CreateDrawingContext();

        using (dc.PushTransform(Matrix.CreateTranslation(-el.X, -el.Y) * Matrix.CreateScale(scale, scale)))
        {
            EditorCanvasControl.DrawElement(dc, el);
        }

        return rtb;
    }

    private async Task SaveBitmapToStreamAsync(RenderTargetBitmap rtb, string fmt, Stream outStream)
    {
        if (fmt == "FITS")
        {
            using var pngMs = new MemoryStream();
            rtb.Save(pngMs, new PngBitmapEncoderOptions());
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
            rtb.Save(pngMs, new PngBitmapEncoderOptions());
            pngMs.Position = 0;
            using var skBmp = SKBitmap.Decode(pngMs);
            int quality = ExportWebpLossless ? 100 : ExportQuality;
            using var data = skBmp.Encode(SKEncodedImageFormat.Webp, quality);
            data.SaveTo(outStream);
        }
        else if (fmt == "BMP")
        {
            using var pngMs = new MemoryStream();
            rtb.Save(pngMs, new PngBitmapEncoderOptions());
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
                rtb.Save(tempPath, new JpegBitmapEncoderOptions { Quality = ExportQuality });
                var bytes = await File.ReadAllBytesAsync(tempPath);
                await outStream.WriteAsync(bytes);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        else // PNG
        {
            var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
            try
            {
                rtb.Save(tempPath, new PngBitmapEncoderOptions());
                var bytes = await File.ReadAllBytesAsync(tempPath);
                await outStream.WriteAsync(bytes);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }

    [RelayCommand]
    public async Task QuickExportAsPngAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage || CanvasControl == null) return;

            var singleFile = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Quick Export as PNG",
                SuggestedFileName = "quick_export.png",
                DefaultExtension = ".png",
                FileTypeChoices =
                [
                    new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }
                ]
            });

            if (singleFile == null) return;

            using var bitmap = RenderCanvasToBitmap(1.0);
            if (bitmap == null) return;

            await using var outStream = await singleFile.OpenWriteAsync();
            await SaveBitmapToStreamAsync(bitmap, "PNG", outStream);

            StatusMessage = $"Quick exported to {singleFile.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Quick export failed: {ex.Message}";
        }
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

            if (IsBatchLayerExport && _exportTargetLayers.Count > 0)
            {
                var firstLayer = _exportTargetLayers[0];
                string sanitizedFirst = string.Join("_", firstLayer.DisplayName.Split(Path.GetInvalidFileNameChars()));
                var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = $"Export As: {_exportTargetLayers.Count} Layers",
                    SuggestedFileName = $"{sanitizedFirst}_{ExportScale:0.#}x.{ext}",
                    DefaultExtension = $".{ext}",
                    FileTypeChoices =
                    [
                        new FilePickerFileType($"{fmt} Image") { Patterns = [$"*.{ext}"] }
                    ]
                });

                if (file == null) return;

                string primaryPath = file.Path.LocalPath;
                string? dir = Path.GetDirectoryName(primaryPath);
                string baseStem = Path.GetFileNameWithoutExtension(primaryPath);

                int exportedCount = 0;
                for (int i = 0; i < _exportTargetLayers.Count; i++)
                {
                    var layer = _exportTargetLayers[i];
                    using var layerBmp = RenderElementToBitmap(layer, ExportScale);
                    if (layerBmp == null) continue;

                    if (i == 0)
                    {
                        await using var outStream = await file.OpenWriteAsync();
                        await SaveBitmapToStreamAsync(layerBmp, fmt, outStream);
                        exportedCount++;
                    }
                    else if (!string.IsNullOrEmpty(dir))
                    {
                        string safeName = string.Join("_", layer.DisplayName.Split(Path.GetInvalidFileNameChars()));
                        string siblingPath = Path.Combine(dir, $"{baseStem}-{i + 1}-{safeName}.{ext}");
                        int suffix = 1;
                        while (File.Exists(siblingPath))
                        {
                            siblingPath = Path.Combine(dir, $"{baseStem}-{i + 1}-{safeName}-{suffix}.{ext}");
                            suffix++;
                        }
                        await using var outStream = File.Create(siblingPath);
                        await SaveBitmapToStreamAsync(layerBmp, fmt, outStream);
                        exportedCount++;
                    }
                }

                IsExportDialogOpen = false;
                StatusMessage = $"Exported {exportedCount} layer(s) as {fmt}.";
                return;
            }

            var singleFile = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Export As {fmt}",
                SuggestedFileName = $"canvas_export_{ExportScale:0.#}x.{ext}",
                DefaultExtension = $".{ext}",
                FileTypeChoices =
                [
                    new FilePickerFileType($"{fmt} Image") { Patterns = [$"*.{ext}"] }
                ]
            });

            if (singleFile == null) return;

            using var bitmap = RenderCanvasToBitmap(ExportScale);
            if (bitmap == null) return;

            await using var singleOutStream = await singleFile.OpenWriteAsync();
            await SaveBitmapToStreamAsync(bitmap, fmt, singleOutStream);

            IsExportDialogOpen = false;
            StatusMessage = $"Exported {ExportDimensionsLabel} to {singleFile.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }
}
