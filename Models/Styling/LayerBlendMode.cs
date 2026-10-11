namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Photoshop / standard layer blend modes.
/// </summary>
public enum LayerBlendMode
{
    Normal,
    Dissolve,
    Darken,
    Multiply,
    ColorBurn,
    LinearBurn,
    Lighten,
    Screen,
    ColorDodge,
    LinearDodge,
    Overlay,
    SoftLight,
    HardLight,
    VividLight,
    PinLight,
    HardMix,
    Difference,
    Exclusion,
    Hue,
    Saturation,
    Color,
    Luminosity
}

public static class LayerBlendModeExtensions
{
    public static string ToDisplayName(this LayerBlendMode mode) => mode switch
    {
        LayerBlendMode.Normal => "Normal",
        LayerBlendMode.Dissolve => "Dissolve",
        LayerBlendMode.Darken => "Darken",
        LayerBlendMode.Multiply => "Multiply",
        LayerBlendMode.ColorBurn => "Color Burn",
        LayerBlendMode.LinearBurn => "Linear Burn",
        LayerBlendMode.Lighten => "Lighten",
        LayerBlendMode.Screen => "Screen",
        LayerBlendMode.ColorDodge => "Color Dodge",
        LayerBlendMode.LinearDodge => "Linear Dodge (Add)",
        LayerBlendMode.Overlay => "Overlay",
        LayerBlendMode.SoftLight => "Soft Light",
        LayerBlendMode.HardLight => "Hard Light",
        LayerBlendMode.VividLight => "Vivid Light",
        LayerBlendMode.PinLight => "Pin Light",
        LayerBlendMode.HardMix => "Hard Mix",
        LayerBlendMode.Difference => "Difference",
        LayerBlendMode.Exclusion => "Exclusion",
        LayerBlendMode.Hue => "Hue",
        LayerBlendMode.Saturation => "Saturation",
        LayerBlendMode.Color => "Color",
        LayerBlendMode.Luminosity => "Luminosity",
        _ => mode.ToString()
    };
}
