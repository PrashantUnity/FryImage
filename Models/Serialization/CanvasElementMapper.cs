using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.ImageEditor.Utils;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Serialization;

/// <summary>Converts between the runtime <see cref="CanvasElement"/> model and its DTO
/// counterpart for project save/load.</summary>
public static class CanvasElementMapper
{
    public static CanvasElementDto ToDto(CanvasElement element) => element switch
    {
        RectangleElement r => ToRectangleDto(r),
        EllipseElement e => ToEllipseDto(e),
        ArrowElement a => ToArrowDto(a),
        TextElement t => ToTextDto(t),
        ImageElement img => ToImageDto(img),
        PolygonElement p => ToPolygonDto(p),
        StarElement s => ToStarDto(s),
        DoodleElement d => ToDoodleDto(d),
        _ => throw new NotSupportedException($"No DTO mapping for element type '{element.GetType().Name}'."),
    };

    public static CanvasElement FromDto(CanvasElementDto dto) => dto switch
    {
        RectangleElementDto r => FromRectangleDto(r),
        EllipseElementDto e => FromEllipseDto(e),
        ArrowElementDto a => FromArrowDto(a),
        TextElementDto t => FromTextDto(t),
        ImageElementDto img => FromImageDto(img),
        PolygonElementDto p => FromPolygonDto(p),
        StarElementDto s => FromStarDto(s),
        DoodleElementDto d => FromDoodleDto(d),
        _ => throw new NotSupportedException($"No element mapping for DTO type '{dto.GetType().Name}'."),
    };

    private static void CopyBaseToDto(CanvasElement el, CanvasElementDto dto)
    {
        dto.X = el.X; dto.Y = el.Y; dto.Width = el.Width; dto.Height = el.Height;
        dto.ZIndex = el.ZIndex;
        dto.RotationDegrees = el.RotationDegrees; dto.FlipX = el.FlipX; dto.FlipY = el.FlipY;
        dto.Opacity = el.Opacity; dto.IsVisible = el.IsVisible; dto.IsLocked = el.IsLocked;
        dto.Name = el.Name; dto.GroupId = el.GroupId; dto.AspectLocked = el.AspectLocked;
        dto.FillOpacity = el.FillOpacity;
        dto.BlendMode = el.BlendMode.ToString();
        dto.IsClippingMask = el.IsClippingMask;
        dto.BlendIfThisMin = el.BlendIfThisMin;
        dto.BlendIfThisMax = el.BlendIfThisMax;
        dto.BlendIfUnderMin = el.BlendIfUnderMin;
        dto.BlendIfUnderMax = el.BlendIfUnderMax;
        dto.Effects = ToEffectsDto(el.Effects);
    }

    private static void CopyBaseFromDto(CanvasElementDto dto, CanvasElement el)
    {
        el.X = dto.X; el.Y = dto.Y; el.Width = dto.Width; el.Height = dto.Height;
        el.ZIndex = dto.ZIndex;
        el.RotationDegrees = dto.RotationDegrees; el.FlipX = dto.FlipX; el.FlipY = dto.FlipY;
        el.Opacity = dto.Opacity; el.IsVisible = dto.IsVisible; el.IsLocked = dto.IsLocked;
        el.Name = dto.Name; el.GroupId = dto.GroupId; el.AspectLocked = dto.AspectLocked;
        el.FillOpacity = dto.FillOpacity;
        el.BlendMode = Enum.TryParse<LayerBlendMode>(dto.BlendMode, true, out var bm) ? bm : LayerBlendMode.Normal;
        el.IsClippingMask = dto.IsClippingMask;
        el.BlendIfThisMin = dto.BlendIfThisMin;
        el.BlendIfThisMax = dto.BlendIfThisMax;
        el.BlendIfUnderMin = dto.BlendIfUnderMin;
        el.BlendIfUnderMax = dto.BlendIfUnderMax;
        el.Effects = FromEffectsDto(dto.Effects);
    }

    private static LayerStyleEffectsDto? ToEffectsDto(LayerStyleEffects? effects)
    {
        if (effects == null || !effects.HasAnyActiveEffect) return null;
        return new LayerStyleEffectsDto
        {
            DropShadow = effects.DropShadow == null ? null : new DropShadowStyleDto
            {
                IsEnabled = effects.DropShadow.IsEnabled,
                Color = effects.DropShadow.Color.ToString(),
                Opacity = effects.DropShadow.Opacity,
                Angle = effects.DropShadow.Angle,
                Distance = effects.DropShadow.Distance,
                Size = effects.DropShadow.Size,
                Spread = effects.DropShadow.Spread,
                BlendMode = effects.DropShadow.BlendMode.ToString()
            },
            OuterGlow = effects.OuterGlow == null ? null : new OuterGlowStyleDto
            {
                IsEnabled = effects.OuterGlow.IsEnabled,
                Color = effects.OuterGlow.Color.ToString(),
                Opacity = effects.OuterGlow.Opacity,
                Size = effects.OuterGlow.Size,
                Spread = effects.OuterGlow.Spread,
                BlendMode = effects.OuterGlow.BlendMode.ToString()
            },
            InnerShadow = effects.InnerShadow == null ? null : new InnerShadowStyleDto
            {
                IsEnabled = effects.InnerShadow.IsEnabled,
                Color = effects.InnerShadow.Color.ToString(),
                Opacity = effects.InnerShadow.Opacity,
                Angle = effects.InnerShadow.Angle,
                Distance = effects.InnerShadow.Distance,
                Size = effects.InnerShadow.Size,
                BlendMode = effects.InnerShadow.BlendMode.ToString()
            },
            InnerGlow = effects.InnerGlow == null ? null : new InnerGlowStyleDto
            {
                IsEnabled = effects.InnerGlow.IsEnabled,
                Color = effects.InnerGlow.Color.ToString(),
                Opacity = effects.InnerGlow.Opacity,
                Size = effects.InnerGlow.Size,
                Spread = effects.InnerGlow.Spread,
                BlendMode = effects.InnerGlow.BlendMode.ToString(),
                CenterSource = effects.InnerGlow.CenterSource
            },
            BevelEmboss = effects.BevelEmboss == null ? null : new BevelEmbossStyleDto
            {
                IsEnabled = effects.BevelEmboss.IsEnabled,
                Style = effects.BevelEmboss.Style.ToString(),
                Depth = effects.BevelEmboss.Depth,
                Size = effects.BevelEmboss.Size,
                Soften = effects.BevelEmboss.Soften,
                Angle = effects.BevelEmboss.Angle,
                Altitude = effects.BevelEmboss.Altitude,
                HighlightColor = effects.BevelEmboss.HighlightColor.ToString(),
                HighlightOpacity = effects.BevelEmboss.HighlightOpacity,
                HighlightMode = effects.BevelEmboss.HighlightMode.ToString(),
                ShadowColor = effects.BevelEmboss.ShadowColor.ToString(),
                ShadowOpacity = effects.BevelEmboss.ShadowOpacity,
                ShadowMode = effects.BevelEmboss.ShadowMode.ToString()
            },
            Satin = effects.Satin == null ? null : new SatinStyleDto
            {
                IsEnabled = effects.Satin.IsEnabled,
                Color = effects.Satin.Color.ToString(),
                Opacity = effects.Satin.Opacity,
                BlendMode = effects.Satin.BlendMode.ToString(),
                Angle = effects.Satin.Angle,
                Distance = effects.Satin.Distance,
                Size = effects.Satin.Size,
                Invert = effects.Satin.Invert
            },
            ColorOverlay = effects.ColorOverlay == null ? null : new ColorOverlayStyleDto
            {
                IsEnabled = effects.ColorOverlay.IsEnabled,
                Color = effects.ColorOverlay.Color.ToString(),
                Opacity = effects.ColorOverlay.Opacity,
                BlendMode = effects.ColorOverlay.BlendMode.ToString()
            },
            GradientOverlay = effects.GradientOverlay == null ? null : new GradientOverlayStyleDto
            {
                IsEnabled = effects.GradientOverlay.IsEnabled,
                BlendMode = effects.GradientOverlay.BlendMode.ToString(),
                Opacity = effects.GradientOverlay.Opacity,
                StartColor = effects.GradientOverlay.StartColor.ToString(),
                EndColor = effects.GradientOverlay.EndColor.ToString(),
                Angle = effects.GradientOverlay.Angle,
                Scale = effects.GradientOverlay.Scale,
                Reverse = effects.GradientOverlay.Reverse
            },
            PatternOverlay = effects.PatternOverlay == null ? null : new PatternOverlayStyleDto
            {
                IsEnabled = effects.PatternOverlay.IsEnabled,
                BlendMode = effects.PatternOverlay.BlendMode.ToString(),
                Opacity = effects.PatternOverlay.Opacity,
                Scale = effects.PatternOverlay.Scale,
                PatternName = effects.PatternOverlay.PatternName
            },
            Stroke = effects.Stroke == null ? null : new LayerStrokeStyleDto
            {
                IsEnabled = effects.Stroke.IsEnabled,
                Color = effects.Stroke.Color.ToString(),
                Size = effects.Stroke.Size,
                Opacity = effects.Stroke.Opacity,
                Position = effects.Stroke.Position.ToString()
            }
        };
    }

    private static Color SafeParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        return Color.TryParse(hex, out var c) ? c : fallback;
    }

    private static LayerStyleEffects FromEffectsDto(LayerStyleEffectsDto? dto)
    {
        var fx = new LayerStyleEffects();
        if (dto == null) return fx;

        if (dto.DropShadow != null)
        {
            fx.DropShadow = new DropShadowStyle
            {
                IsEnabled = dto.DropShadow.IsEnabled,
                Color = SafeParseColor(dto.DropShadow.Color, Colors.Black),
                Opacity = dto.DropShadow.Opacity,
                Angle = dto.DropShadow.Angle,
                Distance = dto.DropShadow.Distance,
                Size = dto.DropShadow.Size,
                Spread = dto.DropShadow.Spread,
                BlendMode = Enum.TryParse<LayerBlendMode>(dto.DropShadow.BlendMode, true, out var bm) ? bm : LayerBlendMode.Multiply
            };
        }

        if (dto.OuterGlow != null)
        {
            fx.OuterGlow = new OuterGlowStyle
            {
                IsEnabled = dto.OuterGlow.IsEnabled,
                Color = SafeParseColor(dto.OuterGlow.Color, Color.FromRgb(255, 230, 80)),
                Opacity = dto.OuterGlow.Opacity,
                Size = dto.OuterGlow.Size,
                Spread = dto.OuterGlow.Spread,
                BlendMode = Enum.TryParse<LayerBlendMode>(dto.OuterGlow.BlendMode, true, out var bm) ? bm : LayerBlendMode.Screen
            };
        }

        if (dto.InnerShadow != null)
        {
            fx.InnerShadow = new InnerShadowStyle
            {
                IsEnabled = dto.InnerShadow.IsEnabled,
                Color = SafeParseColor(dto.InnerShadow.Color, Colors.Black),
                Opacity = dto.InnerShadow.Opacity,
                Angle = dto.InnerShadow.Angle,
                Distance = dto.InnerShadow.Distance,
                Size = dto.InnerShadow.Size,
                BlendMode = Enum.TryParse<LayerBlendMode>(dto.InnerShadow.BlendMode, true, out var bm) ? bm : LayerBlendMode.Multiply
            };
        }

        if (dto.InnerGlow != null)
        {
            fx.InnerGlow = new InnerGlowStyle
            {
                IsEnabled = dto.InnerGlow.IsEnabled,
                Color = SafeParseColor(dto.InnerGlow.Color, Color.FromRgb(255, 255, 190)),
                Opacity = dto.InnerGlow.Opacity,
                Size = dto.InnerGlow.Size,
                Spread = dto.InnerGlow.Spread,
                BlendMode = Enum.TryParse<LayerBlendMode>(dto.InnerGlow.BlendMode, true, out var bm) ? bm : LayerBlendMode.Screen,
                CenterSource = dto.InnerGlow.CenterSource
            };
        }

        if (dto.BevelEmboss != null)
        {
            fx.BevelEmboss = new BevelEmbossStyle
            {
                IsEnabled = dto.BevelEmboss.IsEnabled,
                Style = Enum.TryParse<BevelEmbossType>(dto.BevelEmboss.Style, true, out var bs) ? bs : BevelEmbossType.InnerBevel,
                Depth = dto.BevelEmboss.Depth,
                Size = dto.BevelEmboss.Size,
                Soften = dto.BevelEmboss.Soften,
                Angle = dto.BevelEmboss.Angle,
                Altitude = dto.BevelEmboss.Altitude,
                HighlightColor = SafeParseColor(dto.BevelEmboss.HighlightColor, Colors.White),
                HighlightOpacity = dto.BevelEmboss.HighlightOpacity,
                HighlightMode = Enum.TryParse<LayerBlendMode>(dto.BevelEmboss.HighlightMode, true, out var hm) ? hm : LayerBlendMode.Screen,
                ShadowColor = SafeParseColor(dto.BevelEmboss.ShadowColor, Colors.Black),
                ShadowOpacity = dto.BevelEmboss.ShadowOpacity,
                ShadowMode = Enum.TryParse<LayerBlendMode>(dto.BevelEmboss.ShadowMode, true, out var sm) ? sm : LayerBlendMode.Multiply
            };
        }

        if (dto.Satin != null)
        {
            fx.Satin = new SatinStyle
            {
                IsEnabled = dto.Satin.IsEnabled,
                Color = SafeParseColor(dto.Satin.Color, Colors.Black),
                Opacity = dto.Satin.Opacity,
                BlendMode = Enum.TryParse<LayerBlendMode>(dto.Satin.BlendMode, true, out var bm) ? bm : LayerBlendMode.Multiply,
                Angle = dto.Satin.Angle,
                Distance = dto.Satin.Distance,
                Size = dto.Satin.Size,
                Invert = dto.Satin.Invert
            };
        }

        if (dto.ColorOverlay != null)
        {
            fx.ColorOverlay = new ColorOverlayStyle
            {
                IsEnabled = dto.ColorOverlay.IsEnabled,
                Color = SafeParseColor(dto.ColorOverlay.Color, Color.FromRgb(99, 102, 241)),
                Opacity = dto.ColorOverlay.Opacity,
                BlendMode = Enum.TryParse<LayerBlendMode>(dto.ColorOverlay.BlendMode, true, out var bm) ? bm : LayerBlendMode.Normal
            };
        }

        if (dto.GradientOverlay != null)
        {
            fx.GradientOverlay = new GradientOverlayStyle
            {
                IsEnabled = dto.GradientOverlay.IsEnabled,
                BlendMode = Enum.TryParse<LayerBlendMode>(dto.GradientOverlay.BlendMode, true, out var bm) ? bm : LayerBlendMode.Normal,
                Opacity = dto.GradientOverlay.Opacity,
                StartColor = SafeParseColor(dto.GradientOverlay.StartColor, Colors.Black),
                EndColor = SafeParseColor(dto.GradientOverlay.EndColor, Colors.White),
                Angle = dto.GradientOverlay.Angle,
                Scale = dto.GradientOverlay.Scale,
                Reverse = dto.GradientOverlay.Reverse
            };
        }

        if (dto.PatternOverlay != null)
        {
            fx.PatternOverlay = new PatternOverlayStyle
            {
                IsEnabled = dto.PatternOverlay.IsEnabled,
                BlendMode = Enum.TryParse<LayerBlendMode>(dto.PatternOverlay.BlendMode, true, out var bm) ? bm : LayerBlendMode.Normal,
                Opacity = dto.PatternOverlay.Opacity,
                Scale = dto.PatternOverlay.Scale,
                PatternName = dto.PatternOverlay.PatternName ?? "Grid"
            };
        }

        if (dto.Stroke != null)
        {
            fx.Stroke = new LayerStrokeStyle
            {
                IsEnabled = dto.Stroke.IsEnabled,
                Color = SafeParseColor(dto.Stroke.Color, Colors.Black),
                Size = dto.Stroke.Size,
                Opacity = dto.Stroke.Opacity,
                Position = Enum.TryParse<LayerStrokePosition>(dto.Stroke.Position, true, out var pos) ? pos : LayerStrokePosition.Outside
            };
        }

        return fx;
    }

    private static GradientFillDto? ToGradientDto(GradientFill? gradient)
    {
        if (gradient == null) return null;
        var dto = new GradientFillDto { Kind = gradient.Kind.ToString(), AngleDegrees = gradient.AngleDegrees };
        foreach (var stop in gradient.Stops)
        {
            dto.Stops.Add(new GradientStopDto { Offset = stop.Offset, Color = stop.Color.ToString() });
        }
        return dto;
    }

    private static GradientFill? FromGradientDto(GradientFillDto? dto)
    {
        if (dto == null) return null;
        var stops = new GradientStopValue[dto.Stops.Count];
        for (int i = 0; i < dto.Stops.Count; i++)
        {
            stops[i] = new GradientStopValue(dto.Stops[i].Offset, Color.Parse(dto.Stops[i].Color));
        }
        return new GradientFill
        {
            Kind = Enum.Parse<GradientKind>(dto.Kind),
            AngleDegrees = dto.AngleDegrees,
            Stops = stops,
        };
    }

    private static RectangleElementDto ToRectangleDto(RectangleElement r)
    {
        var dto = new RectangleElementDto
        {
            FillColor = r.FillColor.ToString(),
            Gradient = ToGradientDto(r.Gradient),
            StrokeColor = r.StrokeColor.ToString(),
            StrokeThickness = r.StrokeThickness,
            DashStyle = r.DashStyle.ToString(),
            CornerRadius = r.CornerRadius,
        };
        CopyBaseToDto(r, dto);
        return dto;
    }

    private static RectangleElement FromRectangleDto(RectangleElementDto dto)
    {
        var r = new RectangleElement
        {
            FillColor = Color.Parse(dto.FillColor),
            Gradient = FromGradientDto(dto.Gradient),
            StrokeColor = Color.Parse(dto.StrokeColor),
            StrokeThickness = dto.StrokeThickness,
            DashStyle = Enum.Parse<StrokeDashStyle>(dto.DashStyle),
            CornerRadius = dto.CornerRadius,
        };
        CopyBaseFromDto(dto, r);
        return r;
    }

    private static EllipseElementDto ToEllipseDto(EllipseElement e)
    {
        var dto = new EllipseElementDto
        {
            FillColor = e.FillColor.ToString(),
            Gradient = ToGradientDto(e.Gradient),
            StrokeColor = e.StrokeColor.ToString(),
            StrokeThickness = e.StrokeThickness,
            DashStyle = e.DashStyle.ToString(),
        };
        CopyBaseToDto(e, dto);
        return dto;
    }

    private static EllipseElement FromEllipseDto(EllipseElementDto dto)
    {
        var e = new EllipseElement
        {
            FillColor = Color.Parse(dto.FillColor),
            Gradient = FromGradientDto(dto.Gradient),
            StrokeColor = Color.Parse(dto.StrokeColor),
            StrokeThickness = dto.StrokeThickness,
            DashStyle = Enum.Parse<StrokeDashStyle>(dto.DashStyle),
        };
        CopyBaseFromDto(dto, e);
        return e;
    }

    private static PolygonElementDto ToPolygonDto(PolygonElement p)
    {
        var dto = new PolygonElementDto
        {
            FillColor = p.FillColor.ToString(),
            Gradient = ToGradientDto(p.Gradient),
            StrokeColor = p.StrokeColor.ToString(),
            StrokeThickness = p.StrokeThickness,
            DashStyle = p.DashStyle.ToString(),
            SideCount = p.SideCount,
        };
        CopyBaseToDto(p, dto);
        return dto;
    }

    private static PolygonElement FromPolygonDto(PolygonElementDto dto)
    {
        var p = new PolygonElement
        {
            FillColor = Color.Parse(dto.FillColor),
            Gradient = FromGradientDto(dto.Gradient),
            StrokeColor = Color.Parse(dto.StrokeColor),
            StrokeThickness = dto.StrokeThickness,
            DashStyle = Enum.Parse<StrokeDashStyle>(dto.DashStyle),
            SideCount = dto.SideCount,
        };
        CopyBaseFromDto(dto, p);
        return p;
    }

    private static StarElementDto ToStarDto(StarElement s)
    {
        var dto = new StarElementDto
        {
            FillColor = s.FillColor.ToString(),
            Gradient = ToGradientDto(s.Gradient),
            StrokeColor = s.StrokeColor.ToString(),
            StrokeThickness = s.StrokeThickness,
            DashStyle = s.DashStyle.ToString(),
            PointCount = s.PointCount,
            InnerRadiusRatio = s.InnerRadiusRatio,
        };
        CopyBaseToDto(s, dto);
        return dto;
    }

    private static StarElement FromStarDto(StarElementDto dto)
    {
        var s = new StarElement
        {
            FillColor = Color.Parse(dto.FillColor),
            Gradient = FromGradientDto(dto.Gradient),
            StrokeColor = Color.Parse(dto.StrokeColor),
            StrokeThickness = dto.StrokeThickness,
            DashStyle = Enum.Parse<StrokeDashStyle>(dto.DashStyle),
            PointCount = dto.PointCount,
            InnerRadiusRatio = dto.InnerRadiusRatio,
        };
        CopyBaseFromDto(dto, s);
        return s;
    }

    private static DoodleElementDto ToDoodleDto(DoodleElement d)
    {
        var dto = new DoodleElementDto
        {
            StrokeColor = d.StrokeColor.ToString(),
            StrokeThickness = d.StrokeThickness,
            DashStyle = d.DashStyle.ToString(),
            IsHighlighter = d.IsHighlighter,
            OriginalWidth = d.OriginalWidth,
            OriginalHeight = d.OriginalHeight,
            BrushPresetId = d.BrushPresetId,
            Hardness = d.Hardness,
            Spacing = d.Spacing,
            Roundness = d.Roundness,
            Angle = d.Angle,
            Flow = d.Flow,
            IsAliased = d.IsAliased,
            HasWetEdges = d.HasWetEdges,
            HasNoise = d.HasNoise,
            TipShape = d.TipShape.ToString(),
            SymmetryMode = d.SymmetryMode.ToString(),
            Points = d.Points.Select(p => new PointDto { X = p.X, Y = p.Y }).ToList(),
        };
        CopyBaseToDto(d, dto);
        return dto;
    }

    private static DoodleElement FromDoodleDto(DoodleElementDto dto)
    {
        var d = new DoodleElement
        {
            StrokeColor = Color.Parse(dto.StrokeColor),
            StrokeThickness = dto.StrokeThickness,
            DashStyle = Enum.Parse<StrokeDashStyle>(dto.DashStyle),
            IsHighlighter = dto.IsHighlighter,
            OriginalWidth = dto.OriginalWidth > 0 ? dto.OriginalWidth : dto.Width,
            OriginalHeight = dto.OriginalHeight > 0 ? dto.OriginalHeight : dto.Height,
            BrushPresetId = dto.BrushPresetId,
            Hardness = dto.Hardness,
            Spacing = dto.Spacing,
            Roundness = dto.Roundness,
            Angle = dto.Angle,
            Flow = dto.Flow,
            IsAliased = dto.IsAliased,
            HasWetEdges = dto.HasWetEdges,
            HasNoise = dto.HasNoise,
            TipShape = Enum.TryParse<Core.Paint.TipShapeKind>(dto.TipShape, out var ts) ? ts : Core.Paint.TipShapeKind.Round,
            SymmetryMode = Enum.TryParse<Core.Paint.SymmetryMode>(dto.SymmetryMode, out var sm) ? sm : Core.Paint.SymmetryMode.Off,
            Points = dto.Points.Select(p => new Avalonia.Point(p.X, p.Y)).ToList(),
        };
        CopyBaseFromDto(dto, d);
        return d;
    }

    private static ArrowElementDto ToArrowDto(ArrowElement a)
    {
        var dto = new ArrowElementDto
        {
            StrokeColor = a.StrokeColor.ToString(),
            StrokeThickness = a.StrokeThickness,
            DashStyle = a.DashStyle.ToString(),
            ArrowHeadSize = a.ArrowHeadSize,
        };
        CopyBaseToDto(a, dto);
        return dto;
    }

    private static ArrowElement FromArrowDto(ArrowElementDto dto)
    {
        var a = new ArrowElement
        {
            StrokeColor = Color.Parse(dto.StrokeColor),
            StrokeThickness = dto.StrokeThickness,
            DashStyle = Enum.Parse<StrokeDashStyle>(dto.DashStyle),
            ArrowHeadSize = dto.ArrowHeadSize,
        };
        CopyBaseFromDto(dto, a);
        return a;
    }

    private static TextElementDto ToTextDto(TextElement t)
    {
        var dto = new TextElementDto
        {
            Text = t.Text,
            FontFamily = t.FontFamily.Name,
            FontSize = t.FontSize,
            FontWeight = t.FontWeight.ToString(),
            FontStyle = t.FontStyle.ToString(),
            ForegroundColor = t.ForegroundColor.ToString(),
            BackgroundColor = t.BackgroundColor?.ToString(),
            Alignment = t.Alignment.ToString(),
            IsUnderline = t.IsUnderline,
            IsStrikethrough = t.IsStrikethrough,
            TextCaseTransform = t.TextCaseTransform.ToString(),
            IsWidthAutoFitEnabled = t.IsWidthAutoFitEnabled,
            IsHeightAutoFitEnabled = t.IsHeightAutoFitEnabled,
            IsWrapEnabled = t.IsWrapEnabled,
            LineHeight = t.LineHeight,
            WarpStyle = t.Warp?.Style.ToString(),
            WarpBend = t.Warp?.Bend ?? 50.0,
            WarpHorizontalDistortion = t.Warp?.HorizontalDistortion ?? 0.0,
            WarpVerticalDistortion = t.Warp?.VerticalDistortion ?? 0.0,
            WarpIsHorizontal = t.Warp?.IsHorizontal ?? true,
        };
        CopyBaseToDto(t, dto);
        return dto;
    }

    private static TextElement FromTextDto(TextElementDto dto)
    {
        // Guard auto-fit off first so setting Text/FontSize/etc. below (which normally trigger
        // a remeasure) don't clobber the dimensions restored by CopyBaseFromDto — mirrors
        // TextElement.Clone()'s same construction-order guard.
        var t = new TextElement { IsWidthAutoFitEnabled = false, IsHeightAutoFitEnabled = false };
        // FontHelper.CreateFontFamily (not a bare `new FontFamily(name)`) re-resolves through the
        // embedded-asset/user-downloaded-cache chain — a plain constructor call only matches an
        // OS-installed font of that exact name, so a project saved with a Typefaces-picker font
        // would silently fall back to the default face on reload without this.
        t.FontFamily = FontHelper.CreateFontFamily(dto.FontFamily);
        t.Text = dto.Text;
        t.FontSize = dto.FontSize;
        t.FontWeight = Enum.Parse<FontWeight>(dto.FontWeight);
        t.FontStyle = Enum.Parse<FontStyle>(dto.FontStyle);
        t.ForegroundColor = Color.Parse(dto.ForegroundColor);
        t.BackgroundColor = dto.BackgroundColor != null ? Color.Parse(dto.BackgroundColor) : null;
        t.Alignment = Enum.Parse<TextAlignment>(dto.Alignment);
        t.IsUnderline = dto.IsUnderline;
        t.IsStrikethrough = dto.IsStrikethrough;
        t.TextCaseTransform = Enum.Parse<TextCaseTransform>(dto.TextCaseTransform);
        t.IsWrapEnabled = dto.IsWrapEnabled;
        t.LineHeight = dto.LineHeight;

        if (!string.IsNullOrEmpty(dto.WarpStyle) && Enum.TryParse<TextWarpStyle>(dto.WarpStyle, true, out var ws))
        {
            t.Warp = new TextWarpSettings
            {
                Style = ws,
                Bend = dto.WarpBend,
                HorizontalDistortion = dto.WarpHorizontalDistortion,
                VerticalDistortion = dto.WarpVerticalDistortion,
                IsHorizontal = dto.WarpIsHorizontal
            };
        }

        CopyBaseFromDto(dto, t);

        // Restore the REAL auto-fit flags only now that every text-affecting property is set.
        t.IsWidthAutoFitEnabled = dto.IsWidthAutoFitEnabled;
        t.IsHeightAutoFitEnabled = dto.IsHeightAutoFitEnabled;
        return t;
    }

    private static ImageElementDto ToImageDto(ImageElement img)
    {
        var dto = new ImageElementDto
        {
            FilePath = img.FilePath,
            NaturalPixelWidth = img.NaturalPixelWidth,
            NaturalPixelHeight = img.NaturalPixelHeight,
            CropX = img.SourceCropRect?.X,
            CropY = img.SourceCropRect?.Y,
            CropWidth = img.SourceCropRect?.Width,
            CropHeight = img.SourceCropRect?.Height,
            Brightness = img.Brightness,
            Contrast = img.Contrast,
            Saturation = img.Saturation,
            Hue = img.Hue,
            Temperature = img.Temperature,
            Tint = img.Tint,
            ActiveFilterPreset = img.ActiveFilterPreset,
            BlurRadius = img.BlurRadius,
            SharpenAmount = img.SharpenAmount,
            ImageBytesBase64 = img.OriginalImageBytes is { Length: > 0 } bytes ? Convert.ToBase64String(bytes) : null,
        };
        CopyBaseToDto(img, dto);
        return dto;
    }

    private static ImageElement FromImageDto(ImageElementDto dto)
    {
        var img = new ImageElement
        {
            FilePath = dto.FilePath,
            NaturalPixelWidth = dto.NaturalPixelWidth,
            NaturalPixelHeight = dto.NaturalPixelHeight,
            SourceCropRect = dto.CropX is { } cx && dto.CropY is { } cy && dto.CropWidth is { } cw && dto.CropHeight is { } ch
                ? new Rect(cx, cy, cw, ch)
                : null,
            Brightness = dto.Brightness,
            Contrast = dto.Contrast,
            Saturation = dto.Saturation,
            Hue = dto.Hue,
            Temperature = dto.Temperature,
            Tint = dto.Tint,
            ActiveFilterPreset = dto.ActiveFilterPreset,
            BlurRadius = dto.BlurRadius,
            SharpenAmount = dto.SharpenAmount,
        };

        if (!string.IsNullOrEmpty(dto.ImageBytesBase64))
        {
            var bytes = Convert.FromBase64String(dto.ImageBytesBase64);
            img.OriginalImageBytes = bytes;
            using var ms = new MemoryStream(bytes);
            img.Source = new Bitmap(ms);
        }

        CopyBaseFromDto(dto, img);
        return img;
    }
}
