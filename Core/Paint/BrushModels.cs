using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Paint;

/// <summary>
/// What drives a dynamic brush parameter (Control pop-ups).
/// </summary>
public enum BrushControl
{
    Off,
    Fade,
    PenPressure,
    PenTilt,
    StylusWheel,
    Rotation,
    InitialDirection,
    Direction
}

/// <summary>
/// One dynamic brush parameter: random variation jitter plus controller and minimum floor.
/// </summary>
public class BrushDynamic
{
    /// <summary>0..1 random variation per dab (for scatter: up to 10.0).</summary>
    public double Jitter { get; set; } = 0.0;

    public BrushControl Control { get; set; } = BrushControl.Off;

    /// <summary>Steps for Fade control.</summary>
    public int FadeSteps { get; set; } = 25;

    /// <summary>0..1 minimum floor value that the control or jitter may reduce to.</summary>
    public double Minimum { get; set; } = 0.0;

    public BrushDynamic() { }

    public BrushDynamic(double jitter, BrushControl control = BrushControl.Off, double minimum = 0.0, int fadeSteps = 25)
    {
        Jitter = jitter;
        Control = control;
        Minimum = minimum;
        FadeSteps = fadeSteps;
    }

    public bool IsActive => Jitter > 0.0 || Control != BrushControl.Off;

    public BrushDynamic Clone() => new(Jitter, Control, Minimum, FadeSteps);
}

/// <summary>
/// Available brush tip shape geometry profiles.
/// </summary>
public enum TipShapeKind
{
    Round,
    Calligraphy,
    Bristle,
    Chalk,
    Charcoal,
    Spatter,
    Sponge,
    Star,
    Foliage,
    Sampled
}

/// <summary>
/// Straight painting symmetry modes (vertical, horizontal, dual 4-quadrant, radial mandala).
/// </summary>
public enum SymmetryMode
{
    Off,
    Vertical,
    Horizontal,
    Dual,
    Radial
}

/// <summary>
/// Shape Dynamics settings (size, angle, roundness jitter).
/// </summary>
public class BrushShapeDynamics
{
    public bool Enabled { get; set; }
    public BrushDynamic Size { get; set; } = new();
    public double TiltScale { get; set; } = 1.0;
    public BrushDynamic Angle { get; set; } = new();
    public BrushDynamic Roundness { get; set; } = new();
    public bool FlipXJitter { get; set; }
    public bool FlipYJitter { get; set; }

    public BrushShapeDynamics Clone() => new()
    {
        Enabled = Enabled,
        Size = Size.Clone(),
        TiltScale = TiltScale,
        Angle = Angle.Clone(),
        Roundness = Roundness.Clone(),
        FlipXJitter = FlipXJitter,
        FlipYJitter = FlipYJitter
    };
}

/// <summary>
/// Brush Scattering settings.
/// </summary>
public class BrushScattering
{
    public bool Enabled { get; set; }
    public BrushDynamic Scatter { get; set; } = new();
    public bool BothAxes { get; set; }
    public int Count { get; set; } = 1;
    public BrushDynamic CountJitter { get; set; } = new();

    public BrushScattering Clone() => new()
    {
        Enabled = Enabled,
        Scatter = Scatter.Clone(),
        BothAxes = BothAxes,
        Count = Count,
        CountJitter = CountJitter.Clone()
    };
}

/// <summary>
/// Color Dynamics settings (foreground/background mix, hue, saturation, brightness jitter).
/// </summary>
public class BrushColorDynamics
{
    public bool Enabled { get; set; }
    public bool PerTip { get; set; } = true;
    public BrushDynamic ForegroundBackground { get; set; } = new();
    public double HueJitter { get; set; } = 0.0;
    public double SaturationJitter { get; set; } = 0.0;
    public double BrightnessJitter { get; set; } = 0.0;
    public double Purity { get; set; } = 0.0;

    public BrushColorDynamics Clone() => new()
    {
        Enabled = Enabled,
        PerTip = PerTip,
        ForegroundBackground = ForegroundBackground.Clone(),
        HueJitter = HueJitter,
        SaturationJitter = SaturationJitter,
        BrightnessJitter = BrightnessJitter,
        Purity = Purity
    };
}

/// <summary>
/// Transfer settings (per-dab opacity and flow dynamics).
/// </summary>
public class BrushTransfer
{
    public bool Enabled { get; set; }
    public BrushDynamic Opacity { get; set; } = new();
    public BrushDynamic Flow { get; set; } = new();
    public BrushDynamic Wetness { get; set; } = new();
    public BrushDynamic Mix { get; set; } = new();

    public BrushTransfer Clone() => new()
    {
        Enabled = Enabled,
        Opacity = Opacity.Clone(),
        Flow = Flow.Clone(),
        Wetness = Wetness.Clone(),
        Mix = Mix.Clone()
    };
}

/// <summary>
/// One individual rendered brush dab along a stroke.
/// </summary>
public readonly struct BrushDab
{
    public Point Center { get; init; }
    public double RadiusX { get; init; }
    public double RadiusY { get; init; }
    public double AngleDegrees { get; init; }
    public double Hardness { get; init; }
    public double Opacity { get; init; }
    public Color Color { get; init; }
    public bool Aliased { get; init; }
    public bool WetEdges { get; init; }
    public TipShapeKind TipShape { get; init; }
    public int Seed { get; init; }
    public byte[]? SampledMask { get; init; }
    public int SampledWidth { get; init; }
    public int SampledHeight { get; init; }

    public BrushDab(
        Point center,
        double radiusX,
        double radiusY,
        double angleDegrees,
        double hardness,
        double opacity,
        Color color,
        bool aliased = false,
        bool wetEdges = false,
        TipShapeKind tipShape = TipShapeKind.Round,
        int seed = 0,
        byte[]? sampledMask = null,
        int sampledWidth = 0,
        int sampledHeight = 0)
    {
        Center = center;
        RadiusX = radiusX;
        RadiusY = radiusY;
        AngleDegrees = angleDegrees;
        Hardness = Math.Clamp(hardness, 0.0, 1.0);
        Opacity = Math.Clamp(opacity, 0.0, 1.0);
        Color = color;
        Aliased = aliased;
        WetEdges = wetEdges;
        TipShape = tipShape;
        Seed = seed;
        SampledMask = sampledMask;
        SampledWidth = sampledWidth;
        SampledHeight = sampledHeight;
    }
}

/// <summary>
/// A complete, fully parameterized brush specification.
/// Holds geometry, dynamics, tip profile, and rendering traits.
/// </summary>
public class BrushSettings
{
    public double Size { get; set; } = 24.0;
    public double Hardness { get; set; } = 1.0;
    public double Spacing { get; set; } = 0.25; // 25% of brush diameter standard
    public double Opacity { get; set; } = 1.0;
    public double Flow { get; set; } = 1.0;
    public int Smoothing { get; set; } = 15; // 0..100%

    public TipShapeKind TipShape { get; set; } = TipShapeKind.Round;
    public byte[]? SampledMaskBytes { get; set; }
    public int SampledWidth { get; set; }
    public int SampledHeight { get; set; }
    public double Angle { get; set; } = 0.0;
    public double Roundness { get; set; } = 1.0; // 0.05..1.0
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }

    /// <summary>Pencil mode: binary 0/1 coverage without anti-aliasing (crisp pixel art style).</summary>
    public bool Aliased { get; set; }

    /// <summary>Watercolour look: translucent interior with darker perimeter rim.</summary>
    public bool WetEdges { get; set; }

    /// <summary>Noise grain in soft falloff parts of the tip.</summary>
    public bool Noise { get; set; }

    /// <summary>Airbrush mode: continuous paint buildup over time.</summary>
    public bool BuildUp { get; set; }
    public double BuildUpRate { get; set; } = 25.0; // dabs per second

    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Normal;

    public BrushShapeDynamics ShapeDynamics { get; set; } = new();
    public BrushScattering Scattering { get; set; } = new();
    public BrushColorDynamics ColorDynamics { get; set; } = new();
    public BrushTransfer Transfer { get; set; } = new();

    public BrushSettings Clone() => new()
    {
        Size = Size,
        Hardness = Hardness,
        Spacing = Spacing,
        Opacity = Opacity,
        Flow = Flow,
        Smoothing = Smoothing,
        TipShape = TipShape,
        SampledMaskBytes = SampledMaskBytes != null ? (byte[])SampledMaskBytes.Clone() : null,
        SampledWidth = SampledWidth,
        SampledHeight = SampledHeight,
        Angle = Angle,
        Roundness = Roundness,
        FlipX = FlipX,
        FlipY = FlipY,
        Aliased = Aliased,
        WetEdges = WetEdges,
        Noise = Noise,
        BuildUp = BuildUp,
        BuildUpRate = BuildUpRate,
        BlendMode = BlendMode,
        ShapeDynamics = ShapeDynamics.Clone(),
        Scattering = Scattering.Clone(),
        ColorDynamics = ColorDynamics.Clone(),
        Transfer = Transfer.Clone(),
        Texture = Texture.Clone()
    };

    public BrushTexture Texture { get; set; } = new();
}

/// <summary>
/// Studio texture combination modes for Brush Settings › Texture.
/// </summary>
public enum BrushTextureMode
{
    Multiply,
    Subtract,
    Darken,
    ColorDodge,
    ColorBurn,
    LinearBurn,
    HardMix,
    LinearHeight,
    Height
}

/// <summary>
/// Brush Settings › Texture matching Studio 2026 specification.
/// Supports Height, Linear Height, Subtract, Color Dodge/Burn, Brightness and Contrast levels.
/// </summary>
public class BrushTexture
{
    public const double HeightReach = 12.0;

    public bool Enabled { get; set; }
    public BrushTextureMode Mode { get; set; } = BrushTextureMode.Multiply;
    public double Depth { get; set; } = 1.0; // 0..1
    public double Scale { get; set; } = 1.0;
    public bool Invert { get; set; }
    public double Brightness { get; set; } = 0.0; // -255..255
    public double Contrast { get; set; } = 0.0;   // -100..100
    public bool EachTip { get; set; }

    public double Level(double v)
    {
        double bShift = Brightness / 255.0;
        double x;
        if (Contrast > 0.0)
        {
            double slope = 1.0 / Math.Max(0.001, 1.0 - Math.Min(0.999, Contrast / 100.0));
            x = (v + bShift - 0.5) * slope + 0.5;
        }
        else
        {
            x = (v - 0.5) * (1.0 + Contrast / 100.0) + 0.5 + bShift;
        }
        x = Math.Clamp(x, 0.0, 1.0);
        return Invert ? 1.0 - x : x;
    }

    public static double TextureCombine(BrushTextureMode mode, double v, double t, double d)
    {
        if (v <= 0.0) return 0.0;
        double r = mode switch
        {
            BrushTextureMode.Subtract => v - d * t,
            BrushTextureMode.ColorDodge => v / Math.Max(1.0 - d * t, 1e-3),
            BrushTextureMode.ColorBurn => 1.0 - (1.0 - v) / Math.Max(1.0 - d + d * t, 1e-3),
            BrushTextureMode.Height => HeightReach * (Math.Round(d * 255.0) / 255.0) * v - t,
            BrushTextureMode.LinearHeight => (HeightReach * (Math.Round(d * 255.0) / 255.0) * v) is var reach
                && reach < 1.0 ? reach * (1.0 - t) : reach - t,
            BrushTextureMode.Multiply => v * t,
            BrushTextureMode.Darken => Math.Min(v, t),
            BrushTextureMode.LinearBurn => v + t - 1.0,
            BrushTextureMode.HardMix => (v + t >= 1.0) ? 1.0 : 0.0,
            _ => v * t
        };
        return double.IsFinite(r) ? Math.Clamp(r, 0.0, 1.0) : 0.0;
    }

    public BrushTexture Clone() => new()
    {
        Enabled = Enabled,
        Mode = Mode,
        Depth = Depth,
        Scale = Scale,
        Invert = Invert,
        Brightness = Brightness,
        Contrast = Contrast,
        EachTip = EachTip
    };
}

/// <summary>
/// A named brush preset grouped by medium category and optional nested folder path.
/// </summary>
public class BrushPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = "General";
    public List<string> Folder { get; set; } = new();
    public string IconKind { get; set; } = "Brush";
    public BrushSettings Settings { get; set; } = new();
    public bool CaptureSize { get; set; } = true;
    public bool IncludeToolSettings { get; set; } = false;
    public bool IncludeColor { get; set; } = false;
    public Color? PresetColor { get; set; }

    public BrushPreset() { }

    public BrushPreset(string id, string name, string group, string iconKind, BrushSettings settings, IEnumerable<string>? folder = null)
    {
        Id = id;
        Name = name;
        Group = group;
        IconKind = iconKind;
        Settings = settings;
        if (folder != null) Folder.AddRange(folder);
    }

    public BrushPreset Clone() => new(Id, Name, Group, IconKind, Settings.Clone(), Folder)
    {
        CaptureSize = CaptureSize,
        IncludeToolSettings = IncludeToolSettings,
        IncludeColor = IncludeColor,
        PresetColor = PresetColor
    };
}

/// <summary>
/// Helper for folder drag & drop in the Brushes panel matching Studio (#3851).
/// </summary>
public static class FolderDropHelper
{
    public record FolderDropResult(string Action, string SourceGroup, List<string> SourceFolder, string? TargetGroup, List<string>? TargetFolder, string? Before);

    public static FolderDropResult? ComputeDrop(
        string srcGroup,
        IReadOnlyList<string> srcFolder,
        string targetGroup,
        IReadOnlyList<string> targetFolder,
        bool isUpperEdge)
    {
        if (srcGroup == targetGroup && StartsWith(targetFolder, srcFolder))
        {
            return null;
        }

        if (!isUpperEdge)
        {
            if (srcGroup == targetGroup && ParentEquals(srcFolder, targetFolder))
            {
                return null;
            }
            return new FolderDropResult("MoveInto", srcGroup, srcFolder.ToList(), targetGroup, targetFolder.ToList(), null);
        }

        if (targetFolder.Count == 0)
        {
            if (srcFolder.Count == 0)
                return new FolderDropResult("MoveGroup", srcGroup, srcFolder.ToList(), targetGroup, null, targetGroup);
            else
                return new FolderDropResult("MoveInto", srcGroup, srcFolder.ToList(), null, null, targetGroup);
        }
        else
        {
            string leaf = targetFolder[^1];
            var parent = targetFolder.Take(targetFolder.Count - 1).ToList();
            bool isSibling = srcGroup == targetGroup && ParentEquals(srcFolder, parent);
            if (isSibling)
                return new FolderDropResult("MoveGroup", srcGroup, srcFolder.ToList(), targetGroup, parent, leaf);
            else
                return new FolderDropResult("MoveInto", srcGroup, srcFolder.ToList(), targetGroup, parent, leaf);
        }
    }

    private static bool StartsWith(IReadOnlyList<string> full, IReadOnlyList<string> prefix)
    {
        if (prefix.Count > full.Count) return false;
        for (int i = 0; i < prefix.Count; i++)
        {
            if (full[i] != prefix[i]) return false;
        }
        return true;
    }

    private static bool ParentEquals(IReadOnlyList<string> folder, IReadOnlyList<string> parent)
    {
        if (folder.Count == 0 && parent.Count == 0) return true;
        if (folder.Count <= 1 && parent.Count == 0) return true;
        if (folder.Count - 1 != parent.Count) return false;
        for (int i = 0; i < parent.Count; i++)
        {
            if (folder[i] != parent[i]) return false;
        }
        return true;
    }
}
