using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

/// <summary>
/// Lightweight preview control for rendering a static CanvasElement thumbnail.
/// </summary>
public class ShapePreviewControl : Control
{
    public static readonly StyledProperty<CanvasElement?> ElementProperty =
        AvaloniaProperty.Register<ShapePreviewControl, CanvasElement?>(nameof(Element));

    public CanvasElement? Element
    {
        get => GetValue(ElementProperty);
        set => SetValue(ElementProperty, value);
    }

    static ShapePreviewControl()
    {
        AffectsRender<ShapePreviewControl>(ElementProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Element is not { } el) return;

        var bounds = el.GetRotatedBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        // 0.85 leaves a small margin so a shape's stroke/points aren't clipped at the edge.
        double scale = Math.Min(Bounds.Width / bounds.Width, Bounds.Height / bounds.Height) * 0.85;
        double offsetX = (Bounds.Width - bounds.Width * scale) / 2 - bounds.X * scale;
        double offsetY = (Bounds.Height - bounds.Height * scale) / 2 - bounds.Y * scale;

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            el.Draw(context);
        }
    }
}
