using System;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Preset deformation styles for text warping.
/// </summary>
public enum TextWarpStyle
{
    None,
    Arc,
    ArcLower,
    ArcUpper,
    Arch,
    Bulge,
    ShellLower,
    ShellUpper,
    Flag,
    Wave,
    Fish,
    Rise,
    Fisheye,
    Inflate,
    Squeeze,
    Twist
}

/// <summary>
/// Configuration for non-destructive text mesh warp deformation.
/// </summary>
public sealed class TextWarpSettings
{
    public TextWarpStyle Style { get; set; } = TextWarpStyle.None;

    /// <summary>
    /// Bend percentage from -100 to +100.
    /// </summary>
    public double Bend { get; set; } = 50.0;

    /// <summary>
    /// Horizontal perspective distortion from -100 to +100.
    /// </summary>
    public double HorizontalDistortion { get; set; } = 0.0;

    /// <summary>
    /// Vertical perspective distortion from -100 to +100.
    /// </summary>
    public double VerticalDistortion { get; set; } = 0.0;

    /// <summary>
    /// True for horizontal deformation axis, false for vertical axis.
    /// </summary>
    public bool IsHorizontal { get; set; } = true;

    public bool IsActive => Style != TextWarpStyle.None && Math.Abs(Bend) > 0.001;

    public TextWarpSettings Clone()
    {
        return new TextWarpSettings
        {
            Style = Style,
            Bend = Bend,
            HorizontalDistortion = HorizontalDistortion,
            VerticalDistortion = VerticalDistortion,
            IsHorizontal = IsHorizontal
        };
    }
}
