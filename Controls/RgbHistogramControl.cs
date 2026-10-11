using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

/// <summary>
/// Camera Raw / continuous RGB Ribbon Histogram.
/// Displays 256-bin channel distributions with 5-point binomial kernel smoothing
/// and multi-channel overlap rendering.
/// </summary>
public sealed class RgbHistogramControl : Control
{
    public static readonly StyledProperty<int[]?> RedBinsProperty =
        AvaloniaProperty.Register<RgbHistogramControl, int[]?>(nameof(RedBins));

    public static readonly StyledProperty<int[]?> GreenBinsProperty =
        AvaloniaProperty.Register<RgbHistogramControl, int[]?>(nameof(GreenBins));

    public static readonly StyledProperty<int[]?> BlueBinsProperty =
        AvaloniaProperty.Register<RgbHistogramControl, int[]?>(nameof(BlueBins));

    public static readonly StyledProperty<int[]?> LumBinsProperty =
        AvaloniaProperty.Register<RgbHistogramControl, int[]?>(nameof(LumBins));

    public static readonly StyledProperty<string> ChannelModeProperty =
        AvaloniaProperty.Register<RgbHistogramControl, string>(nameof(ChannelMode), "RGB");

    public int[]? RedBins
    {
        get => GetValue(RedBinsProperty);
        set => SetValue(RedBinsProperty, value);
    }

    public int[]? GreenBins
    {
        get => GetValue(GreenBinsProperty);
        set => SetValue(GreenBinsProperty, value);
    }

    public int[]? BlueBins
    {
        get => GetValue(BlueBinsProperty);
        set => SetValue(BlueBinsProperty, value);
    }

    public int[]? LumBins
    {
        get => GetValue(LumBinsProperty);
        set => SetValue(LumBinsProperty, value);
    }

    public string ChannelMode
    {
        get => GetValue(ChannelModeProperty);
        set => SetValue(ChannelModeProperty, value);
    }

    static RgbHistogramControl()
    {
        AffectsRender<RgbHistogramControl>(RedBinsProperty, GreenBinsProperty, BlueBinsProperty, LumBinsProperty, ChannelModeProperty);
    }

    public override void Render(DrawingContext dc)
    {
        var bounds = Bounds;
        if (bounds.Width < 10 || bounds.Height < 10) return;

        // Container background
        var bgBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x18, 0x22));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
        dc.DrawRectangle(bgBrush, borderPen, bounds, 6, 6);

        // Vertical quartile grid lines
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), 1);
        for (int q = 1; q <= 3; q++)
        {
            double x = bounds.X + bounds.Width * (q / 4.0);
            dc.DrawLine(gridPen, new Point(x, bounds.Y), new Point(x, bounds.Y + bounds.Height));
        }

        bool showRed = ChannelMode is "RGB" or "Red";
        bool showGreen = ChannelMode is "RGB" or "Green";
        bool showBlue = ChannelMode is "RGB" or "Blue";
        bool showLum = ChannelMode is "RGB" or "Luminosity";

        var rBins = RedBins;
        var gBins = GreenBins;
        var bBins = BlueBins;
        var lBins = LumBins;

        if (rBins == null && gBins == null && bBins == null && lBins == null)
        {
            // Fallback placeholder subtle curves
            DrawPlaceholderHistogram(dc, bounds);
            return;
        }

        // Compute peak across active channels (skipping edge clipping spikes to preserve interior details, matching FryImage)
        double peak = 1.0;
        if (showRed && rBins != null) peak = Math.Max(peak, GetInteriorPeak(rBins));
        if (showGreen && gBins != null) peak = Math.Max(peak, GetInteriorPeak(gBins));
        if (showBlue && bBins != null) peak = Math.Max(peak, GetInteriorPeak(bBins));
        if (showLum && lBins != null) peak = Math.Max(peak, GetInteriorPeak(lBins));

        double scale = peak * 1.05;

        // Smooth curves
        if (showRed && rBins != null)
        {
            var smoothed = SmoothBins(rBins, scale);
            DrawRibbon(dc, bounds, smoothed, Color.FromArgb(120, 239, 68, 68), new Pen(new SolidColorBrush(Color.FromArgb(220, 239, 68, 68)), 1.2));
        }

        if (showGreen && gBins != null)
        {
            var smoothed = SmoothBins(gBins, scale);
            DrawRibbon(dc, bounds, smoothed, Color.FromArgb(120, 34, 197, 94), new Pen(new SolidColorBrush(Color.FromArgb(220, 34, 197, 94)), 1.2));
        }

        if (showBlue && bBins != null)
        {
            var smoothed = SmoothBins(bBins, scale);
            DrawRibbon(dc, bounds, smoothed, Color.FromArgb(120, 59, 130, 246), new Pen(new SolidColorBrush(Color.FromArgb(220, 59, 130, 246)), 1.2));
        }

        if (showLum && lBins != null)
        {
            var smoothed = SmoothBins(lBins, scale);
            DrawRibbon(dc, bounds, smoothed, Color.FromArgb(40, 255, 255, 255), new Pen(new SolidColorBrush(Color.FromArgb(240, 248, 250, 252)), 1.5));
        }
    }

    private static double GetInteriorPeak(int[] bins)
    {
        if (bins.Length < 3) return 1.0;
        int max = 0;
        for (int i = 1; i < bins.Length - 1; i++)
        {
            if (bins[i] > max) max = bins[i];
        }
        if (max > 0) return max;
        return Math.Max(bins[0], bins[^1]);
    }

    private static double[] SmoothBins(int[] bins, double scale)
    {
        int count = bins.Length;
        var smoothed = new double[count];
        for (int i = 0; i < count; i++)
        {
            if (i == 0 || i == count - 1)
            {
                smoothed[i] = Math.Clamp(bins[i] / scale, 0, 1);
            }
            else
            {
                int sMinus2 = bins[Math.Max(0, i - 2)];
                int sMinus1 = bins[i - 1];
                int center = bins[i];
                int sPlus1 = bins[i + 1];
                int sPlus2 = bins[Math.Min(count - 1, i + 2)];
                double val = (sMinus2 + 4.0 * sMinus1 + 6.0 * center + 4.0 * sPlus1 + sPlus2) / 16.0;
                smoothed[i] = Math.Clamp(val / scale, 0, 1);
            }
        }
        return smoothed;
    }

    private static void DrawRibbon(DrawingContext dc, Rect bounds, double[] normalized, Color fillColor, Pen strokePen)
    {
        int count = normalized.Length;
        if (count < 2) return;

        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            double bottomY = bounds.Y + bounds.Height - 1;
            ctx.BeginFigure(new Point(bounds.X, bottomY), true);

            for (int i = 0; i < count; i++)
            {
                double px = bounds.X + (i / (double)(count - 1)) * bounds.Width;
                double py = bottomY - normalized[i] * (bounds.Height - 6);
                ctx.LineTo(new Point(px, py));
            }

            ctx.LineTo(new Point(bounds.X + bounds.Width, bottomY));
            ctx.EndFigure(true);
        }

        dc.DrawGeometry(new SolidColorBrush(fillColor), strokePen, geom);
    }

    private static void DrawPlaceholderHistogram(DrawingContext dc, Rect bounds)
    {
        double bottomY = bounds.Y + bounds.Height - 1;
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            ctx.BeginFigure(new Point(bounds.X, bottomY), true);
            for (int i = 0; i < 256; i++)
            {
                double xNorm = i / 255.0;
                double h = Math.Sin(xNorm * Math.PI) * 0.7 * Math.Exp(-Math.Pow((xNorm - 0.45) * 3, 2));
                double px = bounds.X + xNorm * bounds.Width;
                double py = bottomY - h * (bounds.Height - 6);
                ctx.LineTo(new Point(px, py));
            }
            ctx.LineTo(new Point(bounds.X + bounds.Width, bottomY));
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(50, 99, 102, 241)), new Pen(new SolidColorBrush(Color.FromArgb(140, 99, 102, 241)), 1), geom);
    }
}
