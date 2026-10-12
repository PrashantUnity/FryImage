using System;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Represents a color stop on an interactive gradient map ramp.
/// </summary>
public sealed class GradientMapRampStop
{
    public float Position { get; set; } // 0.0 to 1.0
    public Color Color { get; set; }

    public GradientMapRampStop()
    {
        Position = 0.0f;
        Color = Colors.Black;
    }

    public GradientMapRampStop(float position, Color color)
    {
        Position = Math.Clamp(position, 0.0f, 1.0f);
        Color = color;
    }

    public GradientMapRampStop Clone() => new(Position, Color);

    public override string ToString() => $"Stop({Position:P0}, #{Color.A:X2}{Color.R:X2}{Color.G:X2}{Color.B:X2})";
}
