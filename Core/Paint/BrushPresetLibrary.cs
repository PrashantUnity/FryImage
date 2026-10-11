using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Paint;

/// <summary>
/// Built-in Photoshop / Photocraft brush preset library.
/// Categorized into General, Dry Media, Wet Media, and Special Effects.
/// </summary>
public static class BrushPresetLibrary
{
    public static readonly IReadOnlyList<string> Groups = new[]
    {
        "General",
        "Dry Media",
        "Wet Media",
        "Special Effects"
    };

    private static readonly List<BrushPreset> _allPresets = new();

    static BrushPresetLibrary()
    {
        InitializePresets();
    }

    public static IReadOnlyList<BrushPreset> Presets => _allPresets;

    public static IEnumerable<BrushPreset> GetPresetsByGroup(string group)
    {
        if (string.Equals(group, "All", StringComparison.OrdinalIgnoreCase))
            return _allPresets;
        return _allPresets.Where(p => string.Equals(p.Group, group, StringComparison.OrdinalIgnoreCase));
    }

    public static BrushPreset? FindById(string id)
    {
        return _allPresets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static BrushPreset DefaultPreset => _allPresets[0];

    private static void InitializePresets()
    {
        // ── 1. General Presets ──────────────────────────────────────────────────
        _allPresets.Add(new BrushPreset(
            "hard_round",
            "Hard Round",
            "General",
            "Circle",
            new BrushSettings
            {
                Size = 24.0,
                Hardness = 1.0,
                Spacing = 0.15,
                Opacity = 1.0,
                Flow = 1.0,
                Roundness = 1.0,
                Angle = 0.0,
                Smoothing = 15
            }));

        _allPresets.Add(new BrushPreset(
            "soft_round",
            "Soft Round",
            "General",
            "CircleOutline",
            new BrushSettings
            {
                Size = 45.0,
                Hardness = 0.0,
                Spacing = 0.15,
                Opacity = 1.0,
                Flow = 1.0,
                Roundness = 1.0,
                Angle = 0.0,
                Smoothing = 15
            }));

        _allPresets.Add(new BrushPreset(
            "hard_round_fine",
            "Hard Round Fine",
            "General",
            "CircleSmall",
            new BrushSettings
            {
                Size = 4.0,
                Hardness = 1.0,
                Spacing = 0.08,
                Opacity = 1.0,
                Flow = 1.0,
                Roundness = 1.0,
                Angle = 0.0,
                Smoothing = 20
            }));

        _allPresets.Add(new BrushPreset(
            "soft_round_large",
            "Soft Round Large",
            "General",
            "Blur",
            new BrushSettings
            {
                Size = 120.0,
                Hardness = 0.0,
                Spacing = 0.15,
                Opacity = 1.0,
                Flow = 0.8,
                Roundness = 1.0,
                Angle = 0.0,
                Smoothing = 10
            }));

        _allPresets.Add(new BrushPreset(
            "hard_pressure_size",
            "Hard Pressure Size",
            "General",
            "GestureTap",
            new BrushSettings
            {
                Size = 24.0,
                Hardness = 1.0,
                Spacing = 0.12,
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Size = new BrushDynamic(0.0, BrushControl.PenPressure, minimum: 0.2)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "soft_pressure_opacity",
            "Soft Pressure Opacity",
            "General",
            "Opacity",
            new BrushSettings
            {
                Size = 45.0,
                Hardness = 0.0,
                Spacing = 0.15,
                Transfer = new BrushTransfer
                {
                    Enabled = true,
                    Opacity = new BrushDynamic(0.0, BrushControl.PenPressure, minimum: 0.1)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "airbrush_soft",
            "Airbrush Soft",
            "General",
            "WeatherWindy",
            new BrushSettings
            {
                Size = 75.0,
                Hardness = 0.0,
                Spacing = 0.10,
                Flow = 0.20,
                BuildUp = true,
                BuildUpRate = 25.0,
                Smoothing = 15
            }));

        _allPresets.Add(new BrushPreset(
            "airbrush_pressure",
            "Airbrush Pressure Flow",
            "General",
            "Spray",
            new BrushSettings
            {
                Size = 90.0,
                Hardness = 0.0,
                Spacing = 0.10,
                Flow = 0.25,
                BuildUp = true,
                Transfer = new BrushTransfer
                {
                    Enabled = true,
                    Flow = new BrushDynamic(0.0, BrushControl.PenPressure, minimum: 0.05)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "calligraphy_flat",
            "Calligraphy Flat",
            "General",
            "Draw",
            new BrushSettings
            {
                Size = 30.0,
                Hardness = 1.0,
                Roundness = 0.20,
                Angle = 45.0,
                Spacing = 0.05,
                TipShape = TipShapeKind.Calligraphy,
                Smoothing = 25
            }));

        // ── 2. Dry Media Presets ────────────────────────────────────────────────
        _allPresets.Add(new BrushPreset(
            "hard_pencil",
            "Hard Pencil (1-bit Pixel)",
            "Dry Media",
            "Pencil",
            new BrushSettings
            {
                Size = 2.0,
                Hardness = 1.0,
                Spacing = 0.05,
                Aliased = true,
                Smoothing = 5
            }));

        _allPresets.Add(new BrushPreset(
            "soft_pencil",
            "Soft Pencil",
            "Dry Media",
            "LeadPencil",
            new BrushSettings
            {
                Size = 6.0,
                Hardness = 0.70,
                Spacing = 0.08,
                Noise = true,
                Opacity = 0.85,
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Size = new BrushDynamic(0.0, BrushControl.PenPressure, minimum: 0.4)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "chalk",
            "Chalk",
            "Dry Media",
            "BorderColor",
            new BrushSettings
            {
                Size = 36.0,
                Hardness = 0.85,
                Spacing = 0.18,
                TipShape = TipShapeKind.Chalk,
                Noise = true,
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Angle = new BrushDynamic(0.8),
                    Size = new BrushDynamic(0.2, minimum: 0.7)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "charcoal",
            "Charcoal",
            "Dry Media",
            "TextureBox",
            new BrushSettings
            {
                Size = 42.0,
                Hardness = 0.75,
                Spacing = 0.15,
                TipShape = TipShapeKind.Charcoal,
                Noise = true,
                Flow = 0.85,
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Angle = new BrushDynamic(1.0),
                    Roundness = new BrushDynamic(0.3, minimum: 0.5)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "crayon",
            "Crayon",
            "Dry Media",
            "PaletteOutline",
            new BrushSettings
            {
                Size = 22.0,
                Hardness = 0.80,
                Spacing = 0.10,
                Noise = true,
                Opacity = 0.90,
                Flow = 0.85
            }));

        _allPresets.Add(new BrushPreset(
            "pastel",
            "Soft Pastel",
            "Dry Media",
            "Artboard",
            new BrushSettings
            {
                Size = 34.0,
                Hardness = 0.40,
                Spacing = 0.12,
                Noise = true,
                Flow = 0.70,
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Size = new BrushDynamic(0.15, minimum: 0.6)
                }
            }));

        // ── 3. Wet Media Presets ────────────────────────────────────────────────
        _allPresets.Add(new BrushPreset(
            "watercolor_wash",
            "Watercolour Wash",
            "Wet Media",
            "Water",
            new BrushSettings
            {
                Size = 52.0,
                Hardness = 0.05,
                Spacing = 0.12,
                Opacity = 0.65,
                Flow = 0.50,
                WetEdges = true,
                Smoothing = 20
            }));

        _allPresets.Add(new BrushPreset(
            "watercolor_wet_edges",
            "Watercolour Wet Edges",
            "Wet Media",
            "WaterOutline",
            new BrushSettings
            {
                Size = 38.0,
                Hardness = 0.25,
                Spacing = 0.10,
                Opacity = 0.75,
                Flow = 0.60,
                WetEdges = true,
                Smoothing = 25
            }));

        _allPresets.Add(new BrushPreset(
            "acrylic_flat",
            "Acrylic Flat",
            "Wet Media",
            "FormatPaint",
            new BrushSettings
            {
                Size = 32.0,
                Hardness = 0.90,
                Roundness = 0.35,
                Angle = 30.0,
                Spacing = 0.08,
                Opacity = 0.95,
                Flow = 0.90
            }));

        _allPresets.Add(new BrushPreset(
            "oil_bristle",
            "Oil Bristle",
            "Wet Media",
            "BrushOutline",
            new BrushSettings
            {
                Size = 40.0,
                Hardness = 0.70,
                TipShape = TipShapeKind.Bristle,
                Spacing = 0.10,
                Flow = 0.85,
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Angle = new BrushDynamic(0.2, BrushControl.Direction)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "inking_pen",
            "Inking Pen",
            "Wet Media",
            "Pen",
            new BrushSettings
            {
                Size = 10.0,
                Hardness = 1.0,
                Spacing = 0.04,
                Smoothing = 30,
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Size = new BrushDynamic(0.0, BrushControl.PenPressure, minimum: 0.15)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "marker_chisel",
            "Marker Chisel",
            "Wet Media",
            "Marker",
            new BrushSettings
            {
                Size = 28.0,
                Hardness = 0.90,
                Roundness = 0.25,
                Angle = -45.0,
                Spacing = 0.06,
                Opacity = 0.75,
                Smoothing = 20
            }));

        // ── 4. Special Effects Presets ──────────────────────────────────────────
        _allPresets.Add(new BrushPreset(
            "spatter_drops",
            "Spatter Droplets",
            "Special Effects",
            "ScatterPlot",
            new BrushSettings
            {
                Size = 65.0,
                Hardness = 0.90,
                TipShape = TipShapeKind.Spatter,
                Spacing = 0.40,
                Scattering = new BrushScattering
                {
                    Enabled = true,
                    Scatter = new BrushDynamic(1.5),
                    BothAxes = true,
                    Count = 4
                },
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Size = new BrushDynamic(0.6, minimum: 0.2),
                    Angle = new BrushDynamic(1.0)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "sponge_texture",
            "Sponge Stipple",
            "Special Effects",
            "DotsHexagon",
            new BrushSettings
            {
                Size = 55.0,
                Hardness = 0.50,
                TipShape = TipShapeKind.Sponge,
                Spacing = 0.30,
                Noise = true,
                Scattering = new BrushScattering
                {
                    Enabled = true,
                    Scatter = new BrushDynamic(0.8),
                    BothAxes = true,
                    Count = 3
                }
            }));

        _allPresets.Add(new BrushPreset(
            "grass_foliage",
            "Grass & Foliage",
            "Special Effects",
            "SproutOutline",
            new BrushSettings
            {
                Size = 48.0,
                Hardness = 0.85,
                TipShape = TipShapeKind.Foliage,
                Spacing = 0.25,
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Size = new BrushDynamic(0.3, minimum: 0.5),
                    Angle = new BrushDynamic(0.4, BrushControl.Direction)
                },
                Scattering = new BrushScattering
                {
                    Enabled = true,
                    Scatter = new BrushDynamic(0.5)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "star_field",
            "Star Field",
            "Special Effects",
            "StarFourPointsOutline",
            new BrushSettings
            {
                Size = 60.0,
                Hardness = 0.90,
                TipShape = TipShapeKind.Star,
                Spacing = 0.45,
                Scattering = new BrushScattering
                {
                    Enabled = true,
                    Scatter = new BrushDynamic(1.8),
                    BothAxes = true,
                    Count = 3
                },
                ShapeDynamics = new BrushShapeDynamics
                {
                    Enabled = true,
                    Size = new BrushDynamic(0.8, minimum: 0.15),
                    Angle = new BrushDynamic(1.0)
                }
            }));

        _allPresets.Add(new BrushPreset(
            "noise_grain",
            "Noise Grain Texture",
            "Special Effects",
            "Grain",
            new BrushSettings
            {
                Size = 50.0,
                Hardness = 0.0,
                Spacing = 0.20,
                Noise = true,
                Flow = 0.60
            }));
    }
}
