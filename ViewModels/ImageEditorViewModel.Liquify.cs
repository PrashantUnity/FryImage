using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Controls.Dialogs;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Liquify Modal Workspace State ──────────────────────────────────────
    [ObservableProperty] private bool _isLiquifyDialogOpen;
    [ObservableProperty] private LiquifyTool _liquifyActiveTool = LiquifyTool.ForwardWarp;
    [ObservableProperty] private double _liquifyBrushSize = 64.0;
    [ObservableProperty] private double _liquifyBrushDensity = 50.0;
    [ObservableProperty] private double _liquifyBrushPressure = 80.0;
    [ObservableProperty] private double _liquifyBrushRate = 80.0;
    [ObservableProperty] private bool _liquifyShowMesh;
    [ObservableProperty] private bool _liquifyShowBackdrop;
    [ObservableProperty] private double _liquifyReconstructAmount = 25.0;

    private ImageElement? _liquifyTargetImageElement;
    private byte[]? _liquifyOriginalImageBytes;

    public event Action<byte[], int, int>? LiquifySessionStarted;
    public event Func<byte[]?>? LiquifyResultRequested;
    public event Action<double>? LiquifyReconstructRequested;
    public event Action? LiquifyResetRequested;

    [RelayCommand]
    public void OpenLiquifyDialog()
    {
        _liquifyTargetImageElement = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (_liquifyTargetImageElement == null)
        {
            using var rtb = RenderCanvasToBitmap(1.0);
            if (rtb != null)
            {
                using var ms = new MemoryStream();
                rtb.Save(ms, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                var bytes = ms.ToArray();
                var img = new ImageElement
                {
                    Name = "Background Composite",
                    X = 0,
                    Y = 0,
                    Width = _canvasWidth,
                    Height = _canvasHeight,
                    OriginalImageBytes = bytes,
                    Source = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(bytes)),
                    NaturalPixelWidth = (int)_canvasWidth,
                    NaturalPixelHeight = (int)_canvasHeight,
                    ZIndex = 0
                };
                Elements.Insert(0, img);
                SelectedElement = img;
                _liquifyTargetImageElement = img;
                _liquifyFallbackComposite = img;
            }
        }

        if (_liquifyTargetImageElement == null)
        {
            StatusMessage = "Select an image layer to open Liquify workspace.";
            return;
        }

        _liquifyOriginalImageBytes = _liquifyTargetImageElement.OriginalImageBytes;
        if (_liquifyOriginalImageBytes == null || _liquifyOriginalImageBytes.Length == 0)
        {
            StatusMessage = "Target layer has no image data to liquify.";
            return;
        }

        try
        {
            using var srcBmp = SKBitmap.Decode(_liquifyOriginalImageBytes);
            if (srcBmp == null)
            {
                StatusMessage = "Failed to decode target image for Liquify.";
                return;
            }

            byte[] bgra = srcBmp.Bytes;
            IsLiquifyDialogOpen = true;
            LiquifySessionStarted?.Invoke(bgra, srcBmp.Width, srcBmp.Height);
            StatusMessage = "Liquify workspace active.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Liquify error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ConfirmLiquifyDialog()
    {
        if (_liquifyTargetImageElement != null && _liquifyOriginalImageBytes != null)
        {
            byte[]? deformedBgra = LiquifyResultRequested?.Invoke();
            if (deformedBgra != null)
            {
                var beforeBytes = _liquifyOriginalImageBytes;
                try
                {
                    using var srcBmp = SKBitmap.Decode(beforeBytes);
                    if (srcBmp != null)
                    {
                        using var resultBmp = new SKBitmap(srcBmp.Width, srcBmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var pin = GCHandle.Alloc(deformedBgra, GCHandleType.Pinned);
                        try
                        {
                            resultBmp.InstallPixels(resultBmp.Info, pin.AddrOfPinnedObject());
                            using var img = SKImage.FromBitmap(resultBmp);
                            using var enc = img.Encode(SKEncodedImageFormat.Png, 100);
                            byte[] afterBytes = enc.ToArray();

                            _liquifyTargetImageElement.OriginalImageBytes = afterBytes;
                            _liquifyTargetImageElement.AdjustedSource?.Dispose();
                            _liquifyTargetImageElement.AdjustedSource = null;
                            _liquifyTargetImageElement.Source = new Bitmap(new MemoryStream(afterBytes));

                            var el = _liquifyTargetImageElement;
                            var undoCmd = new RetouchImageCommand(el, beforeBytes, afterBytes, "Liquify", (targetEl, b) =>
                            {
                                targetEl.OriginalImageBytes = b;
                                targetEl.AdjustedSource?.Dispose();
                                targetEl.AdjustedSource = null;
                                targetEl.Source = new Bitmap(new MemoryStream(b));
                                RefreshCanvas();
                            });
                            if (!_isApplyingHistory) History.Push(undoCmd);

                            StatusMessage = "Applied Liquify deformation.";
                            RefreshCanvas();
                        }
                        finally
                        {
                            pin.Free();
                        }
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error applying Liquify result: {ex.Message}";
                }
            }
        }

        IsLiquifyDialogOpen = false;
    }

    private ImageElement? _liquifyFallbackComposite;

    [RelayCommand]
    public void CancelLiquifyDialog()
    {
        if (_liquifyFallbackComposite != null)
        {
            Elements.Remove(_liquifyFallbackComposite);
            _liquifyFallbackComposite = null;
        }

        IsLiquifyDialogOpen = false;
        StatusMessage = "Liquify canceled.";
    }

    [RelayCommand]
    public void SetLiquifyTool(string toolName)
    {
        LiquifyActiveTool = toolName switch
        {
            "ForwardWarp" or "warp" => LiquifyTool.ForwardWarp,
            "Reconstruct" or "reconstruct" => LiquifyTool.Reconstruct,
            "Smooth" or "smooth" => LiquifyTool.Smooth,
            "TwirlCw" or "twirlcw" => LiquifyTool.TwirlCw,
            "TwirlCcw" or "twirlccw" => LiquifyTool.TwirlCcw,
            "Pucker" or "pucker" => LiquifyTool.Pucker,
            "Bloat" or "bloat" => LiquifyTool.Bloat,
            "PushLeft" or "pushleft" => LiquifyTool.PushLeft,
            _ => LiquifyTool.ForwardWarp
        };
    }

    [RelayCommand]
    public void ReconstructLiquify()
    {
        LiquifyReconstructRequested?.Invoke(LiquifyReconstructAmount);
    }

    [RelayCommand]
    public void ResetLiquify()
    {
        LiquifyResetRequested?.Invoke();
    }
}
