using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Controls;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Rasterize Prompt Modal State (Studio Parity #4303 & crates/ui-egui/src/rasterize_prompt.rs) ──
    [ObservableProperty] private bool _isRasterizePromptOpen;
    [ObservableProperty] private string _rasterizePromptTitle = "Rasterize Layer";
    [ObservableProperty] private string _rasterizePromptMessage = "";
    [ObservableProperty] private bool _showConvertToSmartObjectOption;

    private CanvasElement? _pendingRasterizeTarget;
    private Action? _pendingOperationAfterRasterize;

    /// <summary>
    /// Checks if the element is non-raster (text or vector shape) that requires rasterization
    /// before applying pixel-based tools (Brush, Pencil, Eraser, Filters, Retouch).
    /// If so, shows the prompt dialog and returns true (preventing direct pixel draw until confirmed).
    /// </summary>
    public bool CheckAndPromptRasterize(CanvasElement? el, string operationName, Action? continueAction = null, bool allowSmartObject = false)
    {
        if (el == null) return false;

        // ImageElement is already raster pixels
        if (el is ImageElement) return false;

        _pendingRasterizeTarget = el;
        _pendingOperationAfterRasterize = continueAction;
        ShowConvertToSmartObjectOption = allowSmartObject;

        if (el is TextElement)
        {
            RasterizePromptTitle = "Rasterize Type";
            RasterizePromptMessage = "This type layer must be rasterized before proceeding. Its text will no longer be editable. Rasterize the type?";
        }
        else
        {
            RasterizePromptTitle = "Rasterize Shape";
            RasterizePromptMessage = "This shape layer must be rasterized before proceeding. It will no longer be editable as a vector shape. Rasterize the shape?";
        }

        IsRasterizePromptOpen = true;
        return true;
    }

    [RelayCommand]
    public void ConfirmRasterize()
    {
        IsRasterizePromptOpen = false;
        var el = _pendingRasterizeTarget;
        _pendingRasterizeTarget = null;
        var action = _pendingOperationAfterRasterize;
        _pendingOperationAfterRasterize = null;

        if (el == null) return;

        var rasterized = RasterizeElementToImage(el);
        if (rasterized != null)
        {
            StatusMessage = $"Rasterized '{el.Name}'.";
            action?.Invoke();
        }
    }

    [RelayCommand]
    public void ConvertToSmartObject()
    {
        IsRasterizePromptOpen = false;
        var el = _pendingRasterizeTarget;
        _pendingRasterizeTarget = null;
        var action = _pendingOperationAfterRasterize;
        _pendingOperationAfterRasterize = null;

        if (el == null) return;

        // Wrap or tag as smart object
        el.IsSmartObject = true;
        StatusMessage = $"Converted '{el.Name}' to Smart Object.";
        action?.Invoke();
    }

    [RelayCommand]
    public void CancelRasterize()
    {
        IsRasterizePromptOpen = false;
        _pendingRasterizeTarget = null;
        _pendingOperationAfterRasterize = null;
    }

    /// <summary>
    /// Converts any vector or text element into an equivalent raster ImageElement.
    /// </summary>
    public ImageElement? RasterizeElementToImage(CanvasElement el)
    {
        try
        {
            int w = (int)Math.Max(4, Math.Ceiling(el.Width));
            int h = (int)Math.Max(4, Math.Ceiling(el.Height));

            var rtb = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            using (var dc = rtb.CreateDrawingContext())
            {
                using (dc.PushTransform(Matrix.CreateTranslation(-el.X, -el.Y)))
                {
                    EditorCanvasControl.DrawElement(dc, el);
                }
            }

            using var ms = new MemoryStream();
            rtb.Save(ms, new PngBitmapEncoderOptions());
            byte[] pngBytes = ms.ToArray();

            using var decodeStream = new MemoryStream(pngBytes);
            var avaloniaBitmap = new Bitmap(decodeStream);

            var imgEl = new ImageElement
            {
                Name = $"{el.Name} (Rasterized)",
                X = el.X,
                Y = el.Y,
                Width = el.Width,
                Height = el.Height,
                NaturalPixelWidth = w,
                NaturalPixelHeight = h,
                RotationDegrees = el.RotationDegrees,
                Opacity = el.Opacity,
                ZIndex = el.ZIndex,
                Source = avaloniaBitmap,
                OriginalImageBytes = pngBytes,
                BlendMode = el.BlendMode,
                IsVisible = el.IsVisible,
                IsLocked = el.IsLocked
            };

            int idx = Elements.IndexOf(el);
            if (idx >= 0)
            {
                History.Push(new ReplaceElementUndoableCommand(this, el, imgEl, idx));
                Elements[idx] = imgEl;
                SelectedElement = imgEl;
            }

            return imgEl;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rasterization error: {ex.Message}";
            return null;
        }
    }

    [RelayCommand]
    public void ConvertSelectedToSmartObject(CanvasElement? el = null)
    {
        var target = el ?? SelectedElement;
        if (target == null) return;
        target.IsSmartObject = true;
        StatusMessage = $"Converted '{target.Name}' to Smart Object.";
        RefreshCanvas();
    }


    [ObservableProperty]
    private SmartFilter? _selectedSmartFilter;

    [RelayCommand]
    public void SelectSmartFilter(SmartFilter? filter)
    {
        if (SelectedElement != null)
        {
            foreach (var sf in SelectedElement.SmartFilters) sf.IsSelected = (sf == filter);
        }
        SelectedSmartFilter = filter;
    }

    [RelayCommand]
    public void ToggleSmartFilterVisibility(SmartFilter? filter)
    {
        if (filter == null) return;
        filter.IsVisible = !filter.IsVisible;
        RefreshCanvas();
    }

    [RelayCommand]
    public void MoveSmartFilterUp(SmartFilter? filter)
    {
        if (filter == null || SelectedElement == null) return;
        var list = SelectedElement.SmartFilters;
        int idx = list.IndexOf(filter);
        if (idx > 0)
        {
            list.Move(idx, idx - 1);
            RefreshCanvas();
            StatusMessage = $"Moved '{filter.Name}' up.";
        }
    }

    [RelayCommand]
    public void MoveSmartFilterDown(SmartFilter? filter)
    {
        if (filter == null || SelectedElement == null) return;
        var list = SelectedElement.SmartFilters;
        int idx = list.IndexOf(filter);
        if (idx >= 0 && idx < list.Count - 1)
        {
            list.Move(idx, idx + 1);
            RefreshCanvas();
            StatusMessage = $"Moved '{filter.Name}' down.";
        }
    }

    [RelayCommand]
    public void OpenSmartFilterEditor(SmartFilter? filter)
    {
        if (filter == null) return;
        SelectSmartFilter(filter);

        if (filter.FilterType.Equals("BrightnessContrast", StringComparison.OrdinalIgnoreCase) ||
            filter.FilterType.Equals("Levels", StringComparison.OrdinalIgnoreCase) ||
            filter.FilterType.Equals("Curves", StringComparison.OrdinalIgnoreCase) ||
            filter.FilterType.Equals("HueSaturation", StringComparison.OrdinalIgnoreCase))
        {
            OpenAdjustmentDialog(filter.FilterType);
        }
        else
        {
            OpenFilterDialog(filter.FilterType);
        }
        StatusMessage = $"Editing Smart Filter '{filter.Name}'.";
    }

    [RelayCommand]
    public void DeleteSmartFilter(SmartFilter? filter)
    {
        if (filter == null || SelectedElement == null) return;
        SelectedElement.SmartFilters.Remove(filter);
        if (SelectedSmartFilter == filter) SelectedSmartFilter = null;
        RefreshCanvas();
        StatusMessage = $"Removed Smart Filter '{filter.Name}'.";
    }
}

