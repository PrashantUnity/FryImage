using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Coloring;
using PdfEditorApp.Plugins.ImageEditor.Core.Codecs;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Color state ────────────────────────────────────────────────────────
    [ObservableProperty]
    private Color _primaryColor = Colors.Black;

    [ObservableProperty]
    private Color _secondaryColor = Colors.White;

    [ObservableProperty]
    private Color _backgroundColor = Colors.White;

    [ObservableProperty]
    private string _backgroundColorName = "White";

    [ObservableProperty]
    private string _activeColorDockTab = "Color"; // Color | Swatches | Gradients

    public string PrimaryColorHex => ColorConversions.ToHexRgb(PrimaryColor);

    private static readonly Color[] DefaultSwatches =
    [
        // Row 1: Grayscale
        Color.FromRgb(0, 0, 0), Color.FromRgb(32, 32, 32), Color.FromRgb(64, 64, 64),
        Color.FromRgb(128, 128, 128), Color.FromRgb(192, 192, 192), Color.FromRgb(255, 255, 255),

        // Row 2: Primaries & Secondaries
        Color.FromRgb(236, 91, 98), Color.FromRgb(249, 115, 22), Color.FromRgb(234, 179, 8),
        Color.FromRgb(34, 197, 94), Color.FromRgb(59, 130, 246), Color.FromRgb(168, 85, 247),

        // Row 3: Pastels
        Color.FromRgb(254, 202, 202), Color.FromRgb(254, 215, 170), Color.FromRgb(254, 240, 138),
        Color.FromRgb(187, 247, 208), Color.FromRgb(191, 219, 254), Color.FromRgb(233, 213, 255),

        // Row 4: Deep / Earth
        Color.FromRgb(153, 27, 27), Color.FromRgb(154, 52, 18), Color.FromRgb(133, 77, 14),
        Color.FromRgb(22, 101, 52), Color.FromRgb(30, 64, 175), Color.FromRgb(107, 33, 168),

        // Row 5: Creative
        Color.FromRgb(244, 63, 94), Color.FromRgb(236, 72, 153), Color.FromRgb(20, 184, 166),
        Color.FromRgb(6, 182, 212), Color.FromRgb(99, 102, 241), Color.FromRgb(139, 92, 246)
    ];

    public ObservableCollection<Color> SwatchesList { get; } = new(DefaultSwatches);

    [RelayCommand]
    public async Task ImportSwatches()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Swatches (.aco, .ase)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Color Swatches (*.aco, *.ase)") { Patterns = ["*.aco", "*.ase"] },
                    new FilePickerFileType("Color Swatch (*.aco)") { Patterns = ["*.aco"] },
                    new FilePickerFileType("Swatch Exchange (*.ase)") { Patterns = ["*.ase"] }
                ]
            });

            if (files.Count == 0) return;

            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();

            List<SwatchItem> items = ext == ".ase" ? SwatchCodec.DecodeAse(stream) : SwatchCodec.DecodeAco(stream);

            if (items.Count > 0)
            {
                SwatchesList.Clear();
                foreach (var item in items)
                {
                    SwatchesList.Add(item.Color);
                }
                StatusMessage = $"Imported {items.Count} swatches from {file.Name}.";
            }
            else
            {
                StatusMessage = "No swatches found in selected file.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Swatch import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ExportSwatches()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Color Swatches (.aco)",
                SuggestedFileName = "palette.aco",
                DefaultExtension = ".aco",
                FileTypeChoices =
                [
                    new FilePickerFileType("Color Swatch (*.aco)") { Patterns = ["*.aco"] }
                ]
            });

            if (file == null) return;

            var swatchItems = new List<SwatchItem>();
            for (int i = 0; i < SwatchesList.Count; i++)
            {
                swatchItems.Add(new SwatchItem($"Swatch {i + 1}", SwatchesList[i]));
            }

            await using var stream = await file.OpenWriteAsync();
            SwatchCodec.EncodeAco(stream, swatchItems);
            StatusMessage = $"Exported {swatchItems.Count} swatches to {file.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Swatch export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ResetDefaultSwatches()
    {
        SwatchesList.Clear();
        foreach (var c in DefaultSwatches)
        {
            SwatchesList.Add(c);
        }
        StatusMessage = "Reset palette to 30 studio swatches.";
    }

    [RelayCommand]
    private void SelectColorDockTab(string tab) => ActiveColorDockTab = tab;

    [RelayCommand]
    private void PickSwatchColor(Color c)
    {
        PrimaryColor = c;
        if (SelectedElement is IHasFill fillable)
            FillColorField = PrimaryColor;
        else if (SelectedElement is IHasStroke strokeable)
            StrokeColorField = PrimaryColor;
        OnPropertyChanged(nameof(PrimaryColorHex));
    }

    [RelayCommand]
    private void SwapColors()
    {
        var temp = PrimaryColor;
        PrimaryColor = SecondaryColor;
        SecondaryColor = temp;
        if (SelectedElement is IHasFill fillable)
        {
            FillColorField = PrimaryColor;
        }
        else if (SelectedElement is IHasStroke strokeable)
        {
            StrokeColorField = PrimaryColor;
        }
        OnPropertyChanged(nameof(PrimaryColorHex));
    }

    [RelayCommand]
    private void ResetColors() => SetDefaultColors();

    [RelayCommand]
    private void SetDefaultColors()
    {
        PrimaryColor = Colors.Black;
        SecondaryColor = Colors.White;
        OnPropertyChanged(nameof(PrimaryColorHex));
    }

    [RelayCommand]
    private void SetPrimaryColorFromHex(string hex)
    {
        if (ColorConversions.TryParseHex(hex, out var c))
        {
            PrimaryColor = c;
            OnPropertyChanged(nameof(PrimaryColorHex));
        }
    }
}
