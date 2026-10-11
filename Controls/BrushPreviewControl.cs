using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Core.Paint;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

/// <summary>
/// Real-time FryImage Brush Tip and S-Curve Stroke preview control.
/// </summary>
public class BrushPreviewControl : Control
{
    public static readonly StyledProperty<double> BrushSizeProperty =
        AvaloniaProperty.Register<BrushPreviewControl, double>(nameof(BrushSize), 30.0);

    public static readonly StyledProperty<double> BrushHardnessProperty =
        AvaloniaProperty.Register<BrushPreviewControl, double>(nameof(BrushHardness), 100.0);

    public static readonly StyledProperty<double> BrushSpacingProperty =
        AvaloniaProperty.Register<BrushPreviewControl, double>(nameof(BrushSpacing), 25.0);

    public static readonly StyledProperty<double> BrushRoundnessProperty =
        AvaloniaProperty.Register<BrushPreviewControl, double>(nameof(BrushRoundness), 100.0);

    public static readonly StyledProperty<double> BrushAngleProperty =
        AvaloniaProperty.Register<BrushPreviewControl, double>(nameof(BrushAngle), 0.0);

    public static readonly StyledProperty<double> BrushOpacityProperty =
        AvaloniaProperty.Register<BrushPreviewControl, double>(nameof(BrushOpacity), 100.0);

    public static readonly StyledProperty<double> BrushFlowProperty =
        AvaloniaProperty.Register<BrushPreviewControl, double>(nameof(BrushFlow), 100.0);

    public static readonly StyledProperty<Color> BrushColorProperty =
        AvaloniaProperty.Register<BrushPreviewControl, Color>(nameof(BrushColor), Color.FromRgb(220, 225, 235));

    public static readonly StyledProperty<bool> BrushWetEdgesProperty =
        AvaloniaProperty.Register<BrushPreviewControl, bool>(nameof(BrushWetEdges), false);

    public static readonly StyledProperty<bool> BrushNoiseProperty =
        AvaloniaProperty.Register<BrushPreviewControl, bool>(nameof(BrushNoise), false);

    public static readonly StyledProperty<bool> BrushAirbrushProperty =
        AvaloniaProperty.Register<BrushPreviewControl, bool>(nameof(BrushAirbrush), false);

    public static readonly StyledProperty<bool> BrushAliasedProperty =
        AvaloniaProperty.Register<BrushPreviewControl, bool>(nameof(BrushAliased), false);

    public static readonly StyledProperty<TipShapeKind> TipShapeProperty =
        AvaloniaProperty.Register<BrushPreviewControl, TipShapeKind>(nameof(TipShape), TipShapeKind.Round);

    public static readonly StyledProperty<string?> BrushPresetIdProperty =
        AvaloniaProperty.Register<BrushPreviewControl, string?>(nameof(BrushPresetId), null);

    public double BrushSize
    {
        get => GetValue(BrushSizeProperty);
        set => SetValue(BrushSizeProperty, value);
    }

    public double BrushHardness
    {
        get => GetValue(BrushHardnessProperty);
        set => SetValue(BrushHardnessProperty, value);
    }

    public double BrushSpacing
    {
        get => GetValue(BrushSpacingProperty);
        set => SetValue(BrushSpacingProperty, value);
    }

    public double BrushRoundness
    {
        get => GetValue(BrushRoundnessProperty);
        set => SetValue(BrushRoundnessProperty, value);
    }

    public double BrushAngle
    {
        get => GetValue(BrushAngleProperty);
        set => SetValue(BrushAngleProperty, value);
    }

    public double BrushOpacity
    {
        get => GetValue(BrushOpacityProperty);
        set => SetValue(BrushOpacityProperty, value);
    }

    public double BrushFlow
    {
        get => GetValue(BrushFlowProperty);
        set => SetValue(BrushFlowProperty, value);
    }

    public Color BrushColor
    {
        get => GetValue(BrushColorProperty);
        set => SetValue(BrushColorProperty, value);
    }

    public bool BrushWetEdges
    {
        get => GetValue(BrushWetEdgesProperty);
        set => SetValue(BrushWetEdgesProperty, value);
    }

    public bool BrushNoise
    {
        get => GetValue(BrushNoiseProperty);
        set => SetValue(BrushNoiseProperty, value);
    }

    public bool BrushAirbrush
    {
        get => GetValue(BrushAirbrushProperty);
        set => SetValue(BrushAirbrushProperty, value);
    }

    public bool BrushAliased
    {
        get => GetValue(BrushAliasedProperty);
        set => SetValue(BrushAliasedProperty, value);
    }

    public TipShapeKind TipShape
    {
        get => GetValue(TipShapeProperty);
        set => SetValue(TipShapeProperty, value);
    }

    public string? BrushPresetId
    {
        get => GetValue(BrushPresetIdProperty);
        set => SetValue(BrushPresetIdProperty, value);
    }

    static BrushPreviewControl()
    {
        AffectsRender<BrushPreviewControl>(
            BrushSizeProperty,
            BrushHardnessProperty,
            BrushSpacingProperty,
            BrushRoundnessProperty,
            BrushAngleProperty,
            BrushOpacityProperty,
            BrushFlowProperty,
            BrushColorProperty,
            BrushWetEdgesProperty,
            BrushNoiseProperty,
            BrushAirbrushProperty,
            BrushAliasedProperty,
            TipShapeProperty,
            BrushPresetIdProperty);
    }

    public override void Render(DrawingContext dc)
    {
        base.Render(dc);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 10 || h <= 10) return;

        // Background
        var bgBrush = new SolidColorBrush(Color.FromRgb(20, 24, 30));
        var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(45, 52, 64)), 1);
        dc.DrawRectangle(bgBrush, borderPen, new Rect(0, 0, w, h), 3, 3);

        // Subtle center guideline for S-curve
        var centerLinePen = new Pen(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1, DashStyle.Dash);
        dc.DrawLine(centerLinePen, new Point(48, h / 2.0), new Point(w - 10, h / 2.0));

        // Separator between tip thumbnail and S-curve
        var sepPen = new Pen(new SolidColorBrush(Color.FromRgb(36, 42, 52)), 1);
        dc.DrawLine(sepPen, new Point(44, 4), new Point(44, h - 4));

        // 1. Tip Thumbnail on the left (40x40 area)
        DrawTipThumbnail(dc, 22, h / 2.0 - 4);

        // 2. S-Curve Stroke Preview on the right
        DrawSCurveStroke(dc, w, h);
    }

    private void DrawTipThumbnail(DrawingContext dc, double cx, double cy)
    {
        double size = Math.Clamp(BrushSize, 2.0, 500.0);
        // Scale tip preview down so it fits in a 32px box
        double scale = Math.Min(1.0, 28.0 / Math.Max(size, 1.0));
        double rX = (size * scale) / 2.0;
        double roundness = Math.Clamp(BrushRoundness / 100.0, 0.05, 1.0);
        double rY = Math.Max(1.0, rX * roundness);
        double angleRad = BrushAngle * Math.PI / 180.0;

        var tipColor = BrushColor;
        byte alpha = (byte)Math.Clamp((int)(BrushOpacity * 2.55), 30, 255);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, tipColor.R, tipColor.G, tipColor.B));

        using (dc.PushTransform(Matrix.CreateTranslation(cx, cy)))
        {
            using (dc.PushTransform(Matrix.CreateRotation(angleRad)))
            {
                if (BrushHardness > 85 || BrushAliased)
                {
                    dc.DrawEllipse(brush, null, new Point(0, 0), rX, rY);
                }
                else
                {
                    // Soft falloff ring
                    double hardnessNorm = Math.Clamp(BrushHardness / 100.0, 0.0, 1.0);
                    double innerRx = Math.Max(1.0, rX * hardnessNorm);
                    double innerRy = Math.Max(1.0, rY * hardnessNorm);

                    var outerBrush = new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.4), tipColor.R, tipColor.G, tipColor.B));
                    dc.DrawEllipse(outerBrush, null, new Point(0, 0), rX, rY);
                    dc.DrawEllipse(brush, null, new Point(0, 0), innerRx, innerRy);
                }
            }
        }

        // Size readout text
        var typeface = new Typeface(FontFamily.Default);
        var ft = new FormattedText(
            $"{Math.Round(BrushSize)} px",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            9.0,
            new SolidColorBrush(Color.FromRgb(140, 150, 170)));

        dc.DrawText(ft, new Point(cx - ft.Width / 2.0, cy + 16));
    }

    private void DrawSCurveStroke(DrawingContext dc, double width, double height)
    {
        double startX = 52.0;
        double endX = width - 12.0;
        double strokeLen = endX - startX;
        if (strokeLen < 20) return;

        // Build settings for stroke generator
        var settings = new BrushSettings
        {
            Size = BrushSize,
            Hardness = BrushHardness / 100.0,
            Spacing = Math.Max(0.04, BrushSpacing / 100.0),
            Roundness = Math.Clamp(BrushRoundness / 100.0, 0.05, 1.0),
            Angle = BrushAngle,
            Opacity = BrushOpacity / 100.0,
            Flow = BrushFlow / 100.0,
            Aliased = BrushAliased,
            WetEdges = BrushWetEdges,
            Noise = BrushNoise,
            BuildUp = BrushAirbrush,
            TipShape = TipShape
        };

        // Enable pressure size dynamics for preview taper
        settings.ShapeDynamics.Enabled = true;
        settings.ShapeDynamics.Size = new BrushDynamic(0.0, BrushControl.PenPressure, minimum: 0.15);

        // Generate 64-point S-curve
        int sampleCount = 64;
        var points = new List<Point>(sampleCount + 1);
        double centerY = height / 2.0;
        double amplitude = Math.Min(height * 0.26, 18.0);

        // Auto-scale brush size to fit in preview height
        double maxAllowedRadius = height * 0.42;
        double scaleFactor = settings.Size > maxAllowedRadius ? maxAllowedRadius / settings.Size : 1.0;
        settings.Size *= scaleFactor;

        for (int i = 0; i <= sampleCount; i++)
        {
            double s = (double)i / sampleCount;
            double px = startX + s * strokeLen;
            double py = centerY - Math.Sin(s * 2.0 * Math.PI) * amplitude;
            points.Add(new Point(px, py));
        }

        var dabs = BrushEngine.GenerateDabs(points, settings, BrushColor, Colors.White, randomSeed: 42);
        if (dabs.Count == 0) return;

        // Render dabs with clipping to preview area
        using (dc.PushClip(new Rect(48, 2, width - 50, height - 4)))
        {
            foreach (var dab in dabs)
            {
                byte dabAlpha = (byte)Math.Clamp((int)(dab.Color.A * dab.Opacity * (BrushFlow / 100.0)), 1, 255);
                var dabColor = Color.FromArgb(dabAlpha, dab.Color.R, dab.Color.G, dab.Color.B);
                var dabBrush = new SolidColorBrush(dabColor);

                double rX = Math.Max(0.5, dab.RadiusX);
                double rY = Math.Max(0.5, dab.RadiusY);
                double angleRad = dab.AngleDegrees * Math.PI / 180.0;

                using (dc.PushTransform(Matrix.CreateTranslation(dab.Center.X, dab.Center.Y)))
                {
                    using (dc.PushTransform(Matrix.CreateRotation(angleRad)))
                    {
                        if (BrushWetEdges)
                        {
                            // 2-pass wet edge: soft body + darker perimeter rim
                            var bodyBrush = new SolidColorBrush(Color.FromArgb((byte)(dabAlpha * 0.35), dabColor.R, dabColor.G, dabColor.B));
                            var rimPen = new Pen(new SolidColorBrush(Color.FromArgb(dabAlpha, dabColor.R, dabColor.G, dabColor.B)), 1.2);
                            dc.DrawEllipse(bodyBrush, rimPen, new Point(0, 0), rX, rY);
                        }
                        else if (dab.Hardness >= 0.85 || BrushAliased)
                        {
                            dc.DrawEllipse(dabBrush, null, new Point(0, 0), rX, rY);
                        }
                        else
                        {
                            double innerRx = Math.Max(0.5, rX * dab.Hardness);
                            double innerRy = Math.Max(0.5, rY * dab.Hardness);
                            var outerBrush = new SolidColorBrush(Color.FromArgb((byte)(dabAlpha * 0.3), dabColor.R, dabColor.G, dabColor.B));

                            dc.DrawEllipse(outerBrush, null, new Point(0, 0), rX, rY);
                            dc.DrawEllipse(dabBrush, null, new Point(0, 0), innerRx, innerRy);
                        }
                    }
                }
            }
        }
    }
}
