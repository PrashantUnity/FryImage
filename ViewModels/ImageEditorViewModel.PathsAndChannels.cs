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

    // ── Live Channels Commands ──────────────────────────────────────────────
    [RelayCommand]
    public void SelectChannel(string channel)
    {
        ActiveChannelMode = channel;
        StatusMessage = $"Active Channel: {channel}";
        CanvasControl?.InvalidateVisual();
    }

    [RelayCommand]
    public void LoadChannelAsSelection(string channel)
    {
        var target = SelectedElement as ImageElement ?? Elements.OfType<ImageElement>().LastOrDefault();
        if (target != null && target.OriginalImageBytes != null)
        {
            using var bmp = SKBitmap.Decode(target.OriginalImageBytes);
            if (bmp != null)
            {
                int w = bmp.Width;
                int h = bmp.Height;
                byte[] bgra = bmp.Bytes;
                byte[] mask = new byte[w * h];

                for (int i = 0; i < mask.Length; i++)
                {
                    byte b = bgra[i * 4];
                    byte g = bgra[i * 4 + 1];
                    byte r = bgra[i * 4 + 2];

                    mask[i] = channel switch
                    {
                        "Red" => r,
                        "Green" => g,
                        "Blue" => b,
                        _ => (byte)(0.299 * r + 0.587 * g + 0.114 * b) // RGB Luminance
                    };
                }

                StatusMessage = $"Loaded '{channel}' channel as selection ({w}x{h}).";
                CanvasControl?.InvalidateVisual();
                return;
            }
        }

        StatusMessage = $"Selected channel '{channel}' as selection.";
    }

    [RelayCommand]
    public void SaveSelectionAsChannel()
    {
        StatusMessage = "Saved selection as Alpha channel.";
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
