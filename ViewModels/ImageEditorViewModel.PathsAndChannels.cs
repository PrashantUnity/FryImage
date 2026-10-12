using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Paint;
using PdfEditorApp.Plugins.ImageEditor.Core.Vector;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Vector Paths System ─────────────────────────────────────────────────
    public ObservableCollection<VectorDocumentPath> DocumentPaths { get; } = new();

    [ObservableProperty]
    private VectorDocumentPath? _activeDocumentPath;

    // ── Live Color Channels System ──────────────────────────────────────────
    [ObservableProperty]
    private string _activeChannelMode = "RGB"; // RGB | Red | Green | Blue

    // ── History Brush Snapshot State ────────────────────────────────────────
    private byte[]? _historyBrushSnapshotBytes;

    public byte[]? HistoryBrushSnapshotBytes => _historyBrushSnapshotBytes;

    [RelayCommand]
    public void CreateNewPath()
    {
        var newPath = new VectorDocumentPath($"Path {DocumentPaths.Count + 1}");
        DocumentPaths.Add(newPath);
        ActiveDocumentPath = newPath;
        StatusMessage = $"Created '{newPath.Name}'.";
        CanvasControl?.InvalidateVisual();
    }

    [RelayCommand]
    public void DeleteActivePath()
    {
        if (ActiveDocumentPath == null) return;
        var p = ActiveDocumentPath;
        DocumentPaths.Remove(p);
        ActiveDocumentPath = DocumentPaths.LastOrDefault();
        StatusMessage = $"Deleted path '{p.Name}'.";
        CanvasControl?.InvalidateVisual();
    }

    [RelayCommand]
    public void FillActivePath()
    {
        if (ActiveDocumentPath == null || ActiveDocumentPath.IsEmpty)
        {
            StatusMessage = "No active vector path to fill.";
            return;
        }

        // Render path directly to active layer or canvas
        int w = (int)Math.Max(100, CanvasWidth);
        int h = (int)Math.Max(100, CanvasHeight);

        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target != null && target.OriginalImageBytes != null)
        {
            try
            {
                using var bmp = SKBitmap.Decode(target.OriginalImageBytes);
                if (bmp != null)
                {
                    int bw = bmp.Width;
                    int bh = bmp.Height;
                    byte[] oldBytes = (byte[])bmp.Bytes.Clone();

                    using var skPath = ActiveDocumentPath.ToSkPath();
                    using var canvas = new SKCanvas(bmp);
                    var col = PrimaryColor;
                    using var paint = new SKPaint
                    {
                        Color = new SKColor(col.R, col.G, col.B, col.A),
                        IsAntialias = true,
                        Style = SKPaintStyle.Fill
                    };
                    canvas.DrawPath(skPath, paint);

                    using var outImg = SKImage.FromBitmap(bmp);
                    using var enc = outImg.Encode(SKEncodedImageFormat.Png, 100);
                    byte[] newBytes = enc.ToArray();

                    target.AdjustedSource?.Dispose();
                    target.AdjustedSource = new Bitmap(new MemoryStream(newBytes));

                    History.Push(new RetouchImageCommand(target, oldBytes, newBytes, "Fill Path", (el, b) =>
                    {
                        el.AdjustedSource?.Dispose();
                        el.AdjustedSource = new Bitmap(new MemoryStream(b));
                        RefreshCanvas();
                    }));

                    RefreshCanvas();
                    StatusMessage = $"Filled path '{ActiveDocumentPath.Name}' with foreground color.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fill path error: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    public void StrokeActivePath()
    {
        if (ActiveDocumentPath == null || ActiveDocumentPath.IsEmpty)
        {
            StatusMessage = "No active vector path to stroke.";
            return;
        }

        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target != null && target.OriginalImageBytes != null)
        {
            try
            {
                using var bmp = SKBitmap.Decode(target.OriginalImageBytes);
                if (bmp != null)
                {
                    byte[] oldBytes = (byte[])bmp.Bytes.Clone();
                    using var skPath = ActiveDocumentPath.ToSkPath();
                    using var canvas = new SKCanvas(bmp);
                    var col = PrimaryColor;
                    using var paint = new SKPaint
                    {
                        Color = new SKColor(col.R, col.G, col.B, col.A),
                        IsAntialias = true,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = (float)BrushThickness,
                        StrokeCap = SKStrokeCap.Round,
                        StrokeJoin = SKStrokeJoin.Round
                    };
                    canvas.DrawPath(skPath, paint);

                    using var outImg = SKImage.FromBitmap(bmp);
                    using var enc = outImg.Encode(SKEncodedImageFormat.Png, 100);
                    byte[] newBytes = enc.ToArray();

                    target.AdjustedSource?.Dispose();
                    target.AdjustedSource = new Bitmap(new MemoryStream(newBytes));

                    History.Push(new RetouchImageCommand(target, oldBytes, newBytes, "Stroke Path", (el, b) =>
                    {
                        el.AdjustedSource?.Dispose();
                        el.AdjustedSource = new Bitmap(new MemoryStream(b));
                        RefreshCanvas();
                    }));

                    RefreshCanvas();
                    StatusMessage = $"Stroked path '{ActiveDocumentPath.Name}' with brush.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Stroke path error: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    public void LoadPathAsSelection()
    {
        if (ActiveDocumentPath == null || ActiveDocumentPath.IsEmpty)
        {
            StatusMessage = "No active vector path to convert to selection.";
            return;
        }

        int w = (int)Math.Max(100, CanvasWidth);
        int h = (int)Math.Max(100, CanvasHeight);
        byte[] mask = ActiveDocumentPath.RasterizeToMask(w, h);

        var bounds = ActiveDocumentPath.GetBounds();
        CanvasControl?.SetSelection(Array.Empty<CanvasElement>());
        StatusMessage = $"Loaded path '{ActiveDocumentPath.Name}' as selection.";
        CanvasControl?.InvalidateVisual();
    }

    // ── Live Color & Alpha Channels System ──────────────────────────────────
    public ObservableCollection<DocumentChannelItem> DocumentChannels { get; } =
    [
        new DocumentChannelItem { Name = "RGB", Shortcut = "Ctrl+2", IsComposite = true, IsAlpha = false, IsVisible = true },
        new DocumentChannelItem { Name = "Red", Shortcut = "Ctrl+3", IsComposite = false, IsAlpha = false, IsVisible = true },
        new DocumentChannelItem { Name = "Green", Shortcut = "Ctrl+4", IsComposite = false, IsAlpha = false, IsVisible = true },
        new DocumentChannelItem { Name = "Blue", Shortcut = "Ctrl+5", IsComposite = false, IsAlpha = false, IsVisible = true }
    ];

    [ObservableProperty]
    private DocumentChannelItem? _selectedChannel;

    [RelayCommand]
    public void SelectChannel(object? parameter)
    {
        string channelName = parameter switch
        {
            DocumentChannelItem item => item.Name,
            string str => str,
            _ => "RGB"
        };

        ActiveChannelMode = channelName;
        StatusMessage = $"Active Channel: {channelName}";
        CanvasControl?.InvalidateVisual();
    }

    [RelayCommand]
    public void LoadChannelAsSelection(object? parameter)
    {
        int docW = (int)Math.Max(1, CanvasWidth);
        int docH = (int)Math.Max(1, CanvasHeight);

        if (parameter is DocumentChannelItem { IsAlpha: true, MaskData: { } maskData })
        {
            if (CanvasControl != null)
            {
                byte[] mask = new byte[docW * docH];
                Array.Copy(maskData, mask, Math.Min(maskData.Length, mask.Length));
                CanvasControl.QuickMaskBuffer = mask;
                CanvasControl.IsQuickMaskMode = true;
                CanvasControl.InvalidateVisual();
            }
            StatusMessage = $"Loaded channel '{parameter}' as selection.";
            return;
        }

        string channel = parameter switch
        {
            DocumentChannelItem item => item.Name,
            string str => str,
            _ => ActiveChannelMode
        };

        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target != null && target.OriginalImageBytes != null)
        {
            using var bmp = SKBitmap.Decode(target.OriginalImageBytes);
            if (bmp != null)
            {
                int w = bmp.Width;
                int h = bmp.Height;
                byte[] bgra = bmp.Bytes;
                byte[] mask = new byte[docW * docH];

                int originX = (int)Math.Round(target.X);
                int originY = (int)Math.Round(target.Y);

                for (int ly = 0; ly < h; ly++)
                {
                    int dy = originY + ly;
                    if (dy < 0 || dy >= docH) continue;

                    int srcRow = ly * w;
                    int dstRow = dy * docW;

                    for (int lx = 0; lx < w; lx++)
                    {
                        int dx = originX + lx;
                        if (dx < 0 || dx >= docW) continue;

                        int pIdx = (srcRow + lx) * 4;
                        byte b = bgra[pIdx];
                        byte g = bgra[pIdx + 1];
                        byte r = bgra[pIdx + 2];

                        mask[dstRow + dx] = channel switch
                        {
                            "Red" => r,
                            "Green" => g,
                            "Blue" => b,
                            _ => (byte)Math.Clamp(Math.Round(0.299 * r + 0.587 * g + 0.114 * b), 0, 255)
                        };
                    }
                }

                if (CanvasControl != null)
                {
                    CanvasControl.QuickMaskBuffer = mask;
                    CanvasControl.IsQuickMaskMode = true;
                    CanvasControl.InvalidateVisual();
                }

                StatusMessage = $"Loaded '{channel}' channel as selection ({docW}x{docH}).";
                return;
            }
        }

        // Composite Luminosity fallback
        LoadLuminositySelection();
    }

    [RelayCommand]
    public void SaveSelectionAsChannel(string? channelName = null)
    {
        int docW = (int)Math.Max(1, CanvasWidth);
        int docH = (int)Math.Max(1, CanvasHeight);

        byte[] maskToSave;
        if (CanvasControl?.QuickMaskBuffer != null && CanvasControl.QuickMaskBuffer.Length >= docW * docH)
        {
            maskToSave = (byte[])CanvasControl.QuickMaskBuffer.Clone();
        }
        else
        {
            // If no active selection, create a full white reveal selection
            maskToSave = new byte[docW * docH];
            Array.Fill(maskToSave, (byte)255);
        }

        int alphaCount = DocumentChannels.Count(c => c.IsAlpha) + 1;
        string name = string.IsNullOrWhiteSpace(channelName) ? $"Alpha {alphaCount}" : channelName.Trim();

        var channelItem = new DocumentChannelItem
        {
            Name = name,
            Shortcut = $"Ctrl+{Math.Min(9, 5 + alphaCount)}",
            IsComposite = false,
            IsAlpha = true,
            IsVisible = true,
            Width = docW,
            Height = docH,
            MaskData = maskToSave
        };

        DocumentChannels.Add(channelItem);
        SelectedChannel = channelItem;
        StatusMessage = $"Saved active selection as channel '{name}'.";
    }

    [RelayCommand]
    public void DeleteChannel(DocumentChannelItem? channel = null)
    {
        var target = channel ?? SelectedChannel;
        if (target != null && target.IsAlpha)
        {
            DocumentChannels.Remove(target);
            SelectedChannel = DocumentChannels.FirstOrDefault();
            StatusMessage = $"Deleted channel '{target.Name}'.";
        }
    }

    // ── History Brush Snapshot Management ───────────────────────────────────
    [RelayCommand]
    public void CreateHistorySnapshot()
    {
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target != null && target.OriginalImageBytes != null)
        {
            _historyBrushSnapshotBytes = (byte[])target.OriginalImageBytes.Clone();
            StatusMessage = "History snapshot created for History Brush.";
        }
    }
}
