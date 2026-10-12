using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Represents a color channel or user-saved Alpha channel in the Channels dock panel.
/// </summary>
public partial class DocumentChannelItem : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty] private string _name = "Alpha 1";
    [ObservableProperty] private string _shortcut = "";
    [ObservableProperty] private bool _isComposite;
    [ObservableProperty] private bool _isAlpha = true;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private bool _isSelected;

    public int Width { get; set; }
    public int Height { get; set; }
    public byte[]? MaskData { get; set; }

    public IBrush ColorBadgeBrush => Name switch
    {
        "Red" => new SolidColorBrush(Color.FromRgb(236, 91, 98)),
        "Green" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        "Blue" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
        "RGB" => new SolidColorBrush(Color.FromRgb(180, 180, 180)),
        _ => new SolidColorBrush(Color.FromRgb(128, 128, 128))
    };
}
