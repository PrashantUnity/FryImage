using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Industry-compatible layer label colors.
/// </summary>
public enum LayerLabelColor
{
    None = 0,
    Red = 1,
    Orange = 2,
    Yellow = 3,
    Green = 4,
    Blue = 5,
    Violet = 6,
    Gray = 7
}

public static class LayerLabelColorExtensions
{
    public static string ToHex(this LayerLabelColor color) => color switch
    {
        LayerLabelColor.Red => "#E84D4D",
        LayerLabelColor.Orange => "#F58220",
        LayerLabelColor.Yellow => "#FFD200",
        LayerLabelColor.Green => "#68B04D",
        LayerLabelColor.Blue => "#3598DC",
        LayerLabelColor.Violet => "#9B59B6",
        LayerLabelColor.Gray => "#95A5A6",
        _ => "#00000000"
    };

    public static Color ToColor(this LayerLabelColor color) => color switch
    {
        LayerLabelColor.Red => Color.FromRgb(0xE8, 0x4D, 0x4D),
        LayerLabelColor.Orange => Color.FromRgb(0xF5, 0x82, 0x20),
        LayerLabelColor.Yellow => Color.FromRgb(0xFF, 0xD2, 0x00),
        LayerLabelColor.Green => Color.FromRgb(0x68, 0xB0, 0x4D),
        LayerLabelColor.Blue => Color.FromRgb(0x35, 0x98, 0xDC),
        LayerLabelColor.Violet => Color.FromRgb(0x9B, 0x59, 0xB6),
        LayerLabelColor.Gray => Color.FromRgb(0x95, 0xA5, 0xA6),
        _ => Colors.Transparent
    };

    public static IBrush ToBrush(this LayerLabelColor color) =>
        color == LayerLabelColor.None
            ? Brushes.Transparent
            : new SolidColorBrush(color.ToColor());

    public static string ToDisplayName(this LayerLabelColor color) => color switch
    {
        LayerLabelColor.None => "None",
        LayerLabelColor.Red => "Red",
        LayerLabelColor.Orange => "Orange",
        LayerLabelColor.Yellow => "Yellow",
        LayerLabelColor.Green => "Green",
        LayerLabelColor.Blue => "Blue",
        LayerLabelColor.Violet => "Violet",
        LayerLabelColor.Gray => "Gray",
        _ => "None"
    };
}
