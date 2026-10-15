using System;
using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Serialization;

/// <summary>
/// Separate DTO hierarchy for project save/load — kept independent of the runtime
/// CanvasElement model (rather than annotating it directly) to avoid attribute-collision
/// risk as the runtime model keeps evolving. Colors/enums are stored as strings so the JSON
/// is human-diffable; <see cref="Models.Serialization.CanvasElementMapper"/> converts both ways.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RectangleElementDto), "rectangle")]
[JsonDerivedType(typeof(EllipseElementDto), "ellipse")]
[JsonDerivedType(typeof(ArrowElementDto), "arrow")]
[JsonDerivedType(typeof(TextElementDto), "text")]
[JsonDerivedType(typeof(ImageElementDto), "image")]
[JsonDerivedType(typeof(PolygonElementDto), "polygon")]
[JsonDerivedType(typeof(StarElementDto), "star")]
[JsonDerivedType(typeof(DoodleElementDto), "doodle")]
public abstract class CanvasElementDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int ZIndex { get; set; }
    public double RotationDegrees { get; set; }
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    public double Opacity { get; set; } = 1.0;
    public double FillOpacity { get; set; } = 1.0;
    public string? BlendMode { get; set; }
    public LayerStyleEffectsDto? Effects { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public bool IsTransparencyLocked { get; set; }
    public bool IsPixelsLocked { get; set; }
    public bool IsPositionLocked { get; set; }
    public bool IsBackground { get; set; }
    public string? Name { get; set; }
    public Guid? GroupId { get; set; }
    public bool AspectLocked { get; set; }
    public bool IsClippingMask { get; set; }
    public string? LabelColor { get; set; }
    public byte BlendIfThisMin { get; set; } = 0;
    public byte BlendIfThisMax { get; set; } = 255;
    public byte BlendIfUnderMin { get; set; } = 0;
    public byte BlendIfUnderMax { get; set; } = 255;
}

public sealed class LayerStyleEffectsDto
{
    public DropShadowStyleDto? DropShadow { get; set; }
    public OuterGlowStyleDto? OuterGlow { get; set; }
    public InnerShadowStyleDto? InnerShadow { get; set; }
    public InnerGlowStyleDto? InnerGlow { get; set; }
    public BevelEmbossStyleDto? BevelEmboss { get; set; }
    public SatinStyleDto? Satin { get; set; }
    public ColorOverlayStyleDto? ColorOverlay { get; set; }
    public GradientOverlayStyleDto? GradientOverlay { get; set; }
    public PatternOverlayStyleDto? PatternOverlay { get; set; }
    public LayerStrokeStyleDto? Stroke { get; set; }
}

public sealed class DropShadowStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string Color { get; set; } = "#FF000000";
    public double Opacity { get; set; } = 0.75;
    public double Angle { get; set; } = 120.0;
    public double Distance { get; set; } = 6.0;
    public double Size { get; set; } = 8.0;
    public double Spread { get; set; }
    public string BlendMode { get; set; } = "Multiply";
}

public sealed class OuterGlowStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string Color { get; set; } = "#FFFFE650";
    public double Opacity { get; set; } = 0.75;
    public double Size { get; set; } = 12.0;
    public double Spread { get; set; }
    public string BlendMode { get; set; } = "Screen";
}

public sealed class InnerShadowStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string Color { get; set; } = "#FF000000";
    public double Opacity { get; set; } = 0.60;
    public double Angle { get; set; } = 120.0;
    public double Distance { get; set; } = 4.0;
    public double Size { get; set; } = 6.0;
    public string BlendMode { get; set; } = "Multiply";
}

public sealed class InnerGlowStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string Color { get; set; } = "#FFFFFFBE";
    public double Opacity { get; set; } = 0.75;
    public double Size { get; set; } = 8.0;
    public double Spread { get; set; }
    public string BlendMode { get; set; } = "Screen";
    public bool CenterSource { get; set; }
}

public sealed class BevelEmbossStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string Style { get; set; } = "InnerBevel";
    public string Technique { get; set; } = "Smooth";
    public double Depth { get; set; } = 100.0;
    public double Size { get; set; } = 5.0;
    public double Soften { get; set; }
    public double Angle { get; set; } = 120.0;
    public double Altitude { get; set; } = 30.0;
    public string HighlightColor { get; set; } = "#FFFFFFFF";
    public double HighlightOpacity { get; set; } = 0.75;
    public string HighlightMode { get; set; } = "Screen";
    public string ShadowColor { get; set; } = "#FF000000";
    public double ShadowOpacity { get; set; } = 0.75;
    public string ShadowMode { get; set; } = "Multiply";
}

public sealed class SatinStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string Color { get; set; } = "#FF000000";
    public double Opacity { get; set; } = 0.5;
    public string BlendMode { get; set; } = "Multiply";
    public double Angle { get; set; } = 19.0;
    public double Distance { get; set; } = 11.0;
    public double Size { get; set; } = 14.0;
    public bool Invert { get; set; } = true;
}

public sealed class ColorOverlayStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string Color { get; set; } = "#FF6366F1";
    public double Opacity { get; set; } = 0.5;
    public string BlendMode { get; set; } = "Normal";
}

public sealed class GradientOverlayStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string BlendMode { get; set; } = "Normal";
    public double Opacity { get; set; } = 1.0;
    public string StartColor { get; set; } = "#FF000000";
    public string EndColor { get; set; } = "#FFFFFFFF";
    public double Angle { get; set; } = 90.0;
    public double Scale { get; set; } = 100.0;
    public bool Reverse { get; set; }
}

public sealed class PatternOverlayStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string BlendMode { get; set; } = "Normal";
    public double Opacity { get; set; } = 1.0;
    public double Scale { get; set; } = 100.0;
    public string PatternName { get; set; } = "Grid";
}

public sealed class LayerStrokeStyleDto
{
    public bool IsEnabled { get; set; } = true;
    public string Color { get; set; } = "#FF000000";
    public double Size { get; set; } = 3.0;
    public double Opacity { get; set; } = 1.0;
    public string Position { get; set; } = "Outside";
}

public sealed class GradientStopDto
{
    public double Offset { get; set; }
    public string Color { get; set; } = "#FF000000";
}

public sealed class GradientFillDto
{
    public string Kind { get; set; } = "Linear";
    public double AngleDegrees { get; set; }
    public System.Collections.Generic.List<GradientStopDto> Stops { get; set; } = new();
}

public sealed class RectangleElementDto : CanvasElementDto
{
    public string FillColor { get; set; } = "#FFFFFFFF";
    public GradientFillDto? Gradient { get; set; }
    public string StrokeColor { get; set; } = "#FF000000";
    public double StrokeThickness { get; set; }
    public string DashStyle { get; set; } = "Solid";
    public double CornerRadius { get; set; }
}

public sealed class EllipseElementDto : CanvasElementDto
{
    public string FillColor { get; set; } = "#FFFFFFFF";
    public GradientFillDto? Gradient { get; set; }
    public string StrokeColor { get; set; } = "#FF000000";
    public double StrokeThickness { get; set; }
    public string DashStyle { get; set; } = "Solid";
}

public sealed class PolygonElementDto : CanvasElementDto
{
    public string FillColor { get; set; } = "#FFFFFFFF";
    public GradientFillDto? Gradient { get; set; }
    public string StrokeColor { get; set; } = "#FF000000";
    public double StrokeThickness { get; set; }
    public string DashStyle { get; set; } = "Solid";
    public int SideCount { get; set; } = 3;
}

public sealed class StarElementDto : CanvasElementDto
{
    public string FillColor { get; set; } = "#FFFFFFFF";
    public GradientFillDto? Gradient { get; set; }
    public string StrokeColor { get; set; } = "#FF000000";
    public double StrokeThickness { get; set; }
    public string DashStyle { get; set; } = "Solid";
    public int PointCount { get; set; } = 5;
    public double InnerRadiusRatio { get; set; } = 0.5;
}

public sealed class ArrowElementDto : CanvasElementDto
{
    public string StrokeColor { get; set; } = "#FF000000";
    public double StrokeThickness { get; set; }
    public string DashStyle { get; set; } = "Solid";
    public double ArrowHeadSize { get; set; } = 12;
}

public sealed class PointDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class DoodleElementDto : CanvasElementDto
{
    public string StrokeColor { get; set; } = "#FF6366F1";
    public double StrokeThickness { get; set; } = 4.0;
    public string DashStyle { get; set; } = "Solid";
    public bool IsHighlighter { get; set; }
    public double OriginalWidth { get; set; } = 100;
    public double OriginalHeight { get; set; } = 100;
    public string? BrushPresetId { get; set; }
    public double Hardness { get; set; } = 1.0;
    public double Spacing { get; set; } = 0.25;
    public double Roundness { get; set; } = 1.0;
    public double Angle { get; set; }
    public double Flow { get; set; } = 1.0;
    public bool IsAliased { get; set; }
    public bool HasWetEdges { get; set; }
    public bool HasNoise { get; set; }
    public string TipShape { get; set; } = "Round";
    public string SymmetryMode { get; set; } = "Off";
    public System.Collections.Generic.List<PointDto> Points { get; set; } = new();
}

public sealed class TextElementDto : CanvasElementDto
{
    public string Text { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Inter,Segoe UI,Arial,sans-serif";
    public double FontSize { get; set; } = 24;
    public string FontWeight { get; set; } = "Normal";
    public string FontStyle { get; set; } = "Normal";
    public string ForegroundColor { get; set; } = "#FF000000";
    public string? BackgroundColor { get; set; }
    public string Alignment { get; set; } = "Left";
    public bool IsUnderline { get; set; }
    public bool IsStrikethrough { get; set; }
    public string TextCaseTransform { get; set; } = "None";
    public bool IsWidthAutoFitEnabled { get; set; } = true;
    public bool IsHeightAutoFitEnabled { get; set; } = true;
    public bool IsWrapEnabled { get; set; }
    public double LineHeight { get; set; }
    public string? WarpStyle { get; set; }
    public double WarpBend { get; set; }
    public double WarpHorizontalDistortion { get; set; }
    public double WarpVerticalDistortion { get; set; }
    public bool WarpIsHorizontal { get; set; } = true;

    // OpenType feature flags
    public bool StandardLigatures { get; set; } = true;
    public bool ContextualAlternates { get; set; } = true;
    public bool DiscretionaryLigatures { get; set; }
    public bool Swash { get; set; }
    public bool StylisticAlternates { get; set; }
    public bool TitlingAlternates { get; set; }
    public bool Ordinals { get; set; }
    public bool Fractions { get; set; }
    public bool FauxSmallCaps { get; set; }
    public bool FauxSuperscript { get; set; }
    public bool FauxSubscript { get; set; }
}

public sealed class ImageElementDto : CanvasElementDto
{
    public string? FilePath { get; set; }
    public int NaturalPixelWidth { get; set; }
    public int NaturalPixelHeight { get; set; }
    public double? CropX { get; set; }
    public double? CropY { get; set; }
    public double? CropWidth { get; set; }
    public double? CropHeight { get; set; }
    public double Brightness { get; set; }
    public double Contrast { get; set; }
    public double Saturation { get; set; }
    public double Hue { get; set; }
    public double Temperature { get; set; }
    public double Tint { get; set; }
    public string? ActiveFilterPreset { get; set; }
    public double BlurRadius { get; set; }
    public double SharpenAmount { get; set; }

    /// <summary>The original imported file's raw encoded bytes, Base64-encoded — this is what
    /// makes a .fryimg file self-contained (no dependency on the original file still existing
    /// on disk at the recorded FilePath).</summary>
    public string? ImageBytesBase64 { get; set; }
}

public sealed class ProjectFileDto
{
    public int SchemaVersion { get; set; } = 1;
    public double CanvasWidth { get; set; } = 640;
    public double CanvasHeight { get; set; } = 480;
    public string BackgroundColor { get; set; } = "#FFFFFFFF";
    public System.Collections.Generic.List<CanvasElementDto> Elements { get; set; } = new();
}
