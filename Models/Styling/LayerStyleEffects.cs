using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// FryImage Layer Styles (Layer Effects).
/// </summary>
public sealed class LayerStyleEffects
{
    public bool HasAnyActiveEffect =>
        (DropShadow != null && DropShadow.IsEnabled) ||
        (OuterGlow != null && OuterGlow.IsEnabled) ||
        (InnerShadow != null && InnerShadow.IsEnabled) ||
        (ColorOverlay != null && ColorOverlay.IsEnabled) ||
        (Stroke != null && Stroke.IsEnabled);

    public DropShadowStyle DropShadow { get; set; } = new() { IsEnabled = false };
    public OuterGlowStyle OuterGlow { get; set; } = new() { IsEnabled = false };
    public InnerShadowStyle InnerShadow { get; set; } = new() { IsEnabled = false };
    public ColorOverlayStyle ColorOverlay { get; set; } = new() { IsEnabled = false };
    public LayerStrokeStyle Stroke { get; set; } = new() { IsEnabled = false };

    public LayerStyleEffects Clone() => new()
    {
        DropShadow = DropShadow.Clone(),
        OuterGlow = OuterGlow.Clone(),
        InnerShadow = InnerShadow.Clone(),
        ColorOverlay = ColorOverlay.Clone(),
        Stroke = Stroke.Clone()
    };
}

public sealed class DropShadowStyle
{
    public bool IsEnabled { get; set; } = true;
    public Color Color { get; set; } = Colors.Black;
    public double Opacity { get; set; } = 0.75;
    public double Angle { get; set; } = 120.0;
    public double Distance { get; set; } = 6.0;
    public double Size { get; set; } = 8.0; // Blur radius
    public double Spread { get; set; } = 0.0;
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Multiply;

    public DropShadowStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Opacity = Opacity,
        Angle = Angle,
        Distance = Distance,
        Size = Size,
        Spread = Spread,
        BlendMode = BlendMode
    };
}

public sealed class OuterGlowStyle
{
    public bool IsEnabled { get; set; } = true;
    public Color Color { get; set; } = Color.FromRgb(255, 230, 80);
    public double Opacity { get; set; } = 0.75;
    public double Size { get; set; } = 12.0; // Blur radius
    public double Spread { get; set; } = 0.0;
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Screen;

    public OuterGlowStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Opacity = Opacity,
        Size = Size,
        Spread = Spread,
        BlendMode = BlendMode
    };
}

public sealed class InnerShadowStyle
{
    public bool IsEnabled { get; set; } = true;
    public Color Color { get; set; } = Colors.Black;
    public double Opacity { get; set; } = 0.60;
    public double Angle { get; set; } = 120.0;
    public double Distance { get; set; } = 4.0;
    public double Size { get; set; } = 6.0;
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Multiply;

    public InnerShadowStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Opacity = Opacity,
        Angle = Angle,
        Distance = Distance,
        Size = Size,
        BlendMode = BlendMode
    };
}

public sealed class ColorOverlayStyle
{
    public bool IsEnabled { get; set; } = true;
    public Color Color { get; set; } = Color.FromRgb(99, 102, 241);
    public double Opacity { get; set; } = 0.5;
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Normal;

    public ColorOverlayStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Opacity = Opacity,
        BlendMode = BlendMode
    };
}

public sealed class LayerStrokeStyle
{
    public bool IsEnabled { get; set; } = true;
    public Color Color { get; set; } = Colors.Black;
    public double Size { get; set; } = 3.0;
    public double Opacity { get; set; } = 1.0;
    public LayerStrokePosition Position { get; set; } = LayerStrokePosition.Outside;

    public LayerStrokeStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Size = Size,
        Opacity = Opacity,
        Position = Position
    };
}

public enum LayerStrokePosition
{
    Outside,
    Inside,
    Center
}
