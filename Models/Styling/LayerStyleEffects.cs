using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// FryImage Complete Studio Layer Styles (10 Layer Effects Stack).
/// Bottom to top render order: Drop Shadow, Outer Glow, Bevel & Emboss, Color Overlay,
/// Gradient Overlay, Pattern Overlay, Satin, Inner Glow, Inner Shadow, Stroke.
/// </summary>
public sealed class LayerStyleEffects
{
    public bool HasAnyActiveEffect =>
        (DropShadow != null && DropShadow.IsEnabled) ||
        (OuterGlow != null && OuterGlow.IsEnabled) ||
        (InnerShadow != null && InnerShadow.IsEnabled) ||
        (InnerGlow != null && InnerGlow.IsEnabled) ||
        (BevelEmboss != null && BevelEmboss.IsEnabled) ||
        (Satin != null && Satin.IsEnabled) ||
        (ColorOverlay != null && ColorOverlay.IsEnabled) ||
        (GradientOverlay != null && GradientOverlay.IsEnabled) ||
        (PatternOverlay != null && PatternOverlay.IsEnabled) ||
        (Stroke != null && Stroke.IsEnabled);

    public DropShadowStyle DropShadow { get; set; } = new() { IsEnabled = false };
    public OuterGlowStyle OuterGlow { get; set; } = new() { IsEnabled = false };
    public InnerShadowStyle InnerShadow { get; set; } = new() { IsEnabled = false };
    public InnerGlowStyle InnerGlow { get; set; } = new() { IsEnabled = false };
    public BevelEmbossStyle BevelEmboss { get; set; } = new() { IsEnabled = false };
    public SatinStyle Satin { get; set; } = new() { IsEnabled = false };
    public ColorOverlayStyle ColorOverlay { get; set; } = new() { IsEnabled = false };
    public GradientOverlayStyle GradientOverlay { get; set; } = new() { IsEnabled = false };
    public PatternOverlayStyle PatternOverlay { get; set; } = new() { IsEnabled = false };
    public LayerStrokeStyle Stroke { get; set; } = new() { IsEnabled = false };

    public LayerStyleEffects Clone() => new()
    {
        DropShadow = DropShadow.Clone(),
        OuterGlow = OuterGlow.Clone(),
        InnerShadow = InnerShadow.Clone(),
        InnerGlow = InnerGlow.Clone(),
        BevelEmboss = BevelEmboss.Clone(),
        Satin = Satin.Clone(),
        ColorOverlay = ColorOverlay.Clone(),
        GradientOverlay = GradientOverlay.Clone(),
        PatternOverlay = PatternOverlay.Clone(),
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
    public string ContourName { get; set; } = "Linear";

    public DropShadowStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Opacity = Opacity,
        Angle = Angle,
        Distance = Distance,
        Size = Size,
        Spread = Spread,
        BlendMode = BlendMode,
        ContourName = ContourName
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
    public string ContourName { get; set; } = "Linear";

    public OuterGlowStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Opacity = Opacity,
        Size = Size,
        Spread = Spread,
        BlendMode = BlendMode,
        ContourName = ContourName
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
    public string ContourName { get; set; } = "Linear";

    public InnerShadowStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Opacity = Opacity,
        Angle = Angle,
        Distance = Distance,
        Size = Size,
        BlendMode = BlendMode,
        ContourName = ContourName
    };
}

public sealed class InnerGlowStyle
{
    public bool IsEnabled { get; set; } = true;
    public Color Color { get; set; } = Color.FromRgb(255, 255, 190);
    public double Opacity { get; set; } = 0.75;
    public double Size { get; set; } = 8.0;
    public double Spread { get; set; } = 0.0;
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Screen;
    public bool CenterSource { get; set; } = false;
    public string ContourName { get; set; } = "Linear";

    public InnerGlowStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Opacity = Opacity,
        Size = Size,
        Spread = Spread,
        BlendMode = BlendMode,
        CenterSource = CenterSource,
        ContourName = ContourName
    };
}

public enum BevelEmbossType
{
    InnerBevel,
    OuterBevel,
    Emboss,
    PillowEmboss,
    StrokeEmboss
}

public enum BevelTechnique
{
    Smooth,
    ChiselHard,
    ChiselSoft
}

public sealed class BevelEmbossStyle
{
    public bool IsEnabled { get; set; } = true;
    public BevelEmbossType Style { get; set; } = BevelEmbossType.InnerBevel;
    public BevelTechnique Technique { get; set; } = BevelTechnique.Smooth;
    public double Depth { get; set; } = 100.0;
    public double Size { get; set; } = 5.0;
    public double Soften { get; set; } = 0.0;
    public double Angle { get; set; } = 120.0;
    public double Altitude { get; set; } = 30.0;
    public Color HighlightColor { get; set; } = Colors.White;
    public double HighlightOpacity { get; set; } = 0.75;
    public LayerBlendMode HighlightMode { get; set; } = LayerBlendMode.Screen;
    public Color ShadowColor { get; set; } = Colors.Black;
    public double ShadowOpacity { get; set; } = 0.75;
    public LayerBlendMode ShadowMode { get; set; } = LayerBlendMode.Multiply;
    public string ContourName { get; set; } = "Linear";

    public BevelEmbossStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Style = Style,
        Technique = Technique,
        Depth = Depth,
        Size = Size,
        Soften = Soften,
        Angle = Angle,
        Altitude = Altitude,
        HighlightColor = HighlightColor,
        HighlightOpacity = HighlightOpacity,
        HighlightMode = HighlightMode,
        ShadowColor = ShadowColor,
        ShadowOpacity = ShadowOpacity,
        ShadowMode = ShadowMode,
        ContourName = ContourName
    };
}

public sealed class SatinStyle
{
    public bool IsEnabled { get; set; } = true;
    public Color Color { get; set; } = Colors.Black;
    public double Opacity { get; set; } = 0.5;
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Multiply;
    public double Angle { get; set; } = 19.0;
    public double Distance { get; set; } = 11.0;
    public double Size { get; set; } = 14.0;
    public bool Invert { get; set; } = true;
    public string ContourName { get; set; } = "Linear";

    public SatinStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        Color = Color,
        Opacity = Opacity,
        BlendMode = BlendMode,
        Angle = Angle,
        Distance = Distance,
        Size = Size,
        Invert = Invert,
        ContourName = ContourName
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

public sealed class GradientOverlayStyle
{
    public bool IsEnabled { get; set; } = true;
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Normal;
    public double Opacity { get; set; } = 1.0;
    public Color StartColor { get; set; } = Colors.Black;
    public Color EndColor { get; set; } = Colors.White;
    public double Angle { get; set; } = 90.0;
    public double Scale { get; set; } = 100.0;
    public bool Reverse { get; set; } = false;

    public GradientOverlayStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        BlendMode = BlendMode,
        Opacity = Opacity,
        StartColor = StartColor,
        EndColor = EndColor,
        Angle = Angle,
        Scale = Scale,
        Reverse = Reverse
    };
}

public sealed class PatternOverlayStyle
{
    public bool IsEnabled { get; set; } = true;
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Normal;
    public double Opacity { get; set; } = 1.0;
    public double Scale { get; set; } = 100.0;
    public string PatternName { get; set; } = "Grid";

    public PatternOverlayStyle Clone() => new()
    {
        IsEnabled = IsEnabled,
        BlendMode = BlendMode,
        Opacity = Opacity,
        Scale = Scale,
        PatternName = PatternName
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
