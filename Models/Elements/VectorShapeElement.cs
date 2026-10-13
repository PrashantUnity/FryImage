using System;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Core.Vector;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// A vector shape layer represented by arbitrary cubic Bézier subpaths, knots, and boolean ops.
/// Created via "Convert Selection to Shape" or vector drawing tools.
/// </summary>
public class VectorShapeElement : CanvasElement, IHasFill, IHasStroke
{
    public override string ElementType => "Shape";

    public VectorDocumentPath Path { get; set; } = new("Shape Path");

    public Color FillColor { get; set; } = Color.FromArgb(255, 59, 130, 246);
    public GradientFill? Gradient { get; set; }
    public Color StrokeColor { get; set; } = Colors.Transparent;
    public double StrokeThickness { get; set; } = 0;
    public StrokeDashStyle DashStyle { get; set; } = StrokeDashStyle.Solid;

    public VectorShapeElement()
    {
        Width = 100;
        Height = 100;
    }

    public VectorShapeElement(VectorDocumentPath path, Color fill)
    {
        Path = path;
        FillColor = fill;
        var b = path.GetBounds();
        if (b.Width > 0 && b.Height > 0)
        {
            X = b.X;
            Y = b.Y;
            Width = b.Width;
            Height = b.Height;
        }
    }

    public override void Draw(DrawingContext dc)
    {
        if (Path == null || Path.IsEmpty) return;

        IBrush fill = Gradient?.ToBrush() ?? new SolidColorBrush(FillColor);
        var pen = StrokeThickness > 0
            ? new Pen(new SolidColorBrush(StrokeColor), StrokeThickness, ShapeDashStyles.ToAvaloniaDashStyle(DashStyle))
            : null;

        var geom = Path.ToAvaloniaGeometry();
        dc.DrawGeometry(fill, pen, geom);
    }

    public override bool HitTest(Point pt)
    {
        if (Path == null || Path.IsEmpty) return Bounds.Contains(pt);
        var geom = Path.ToAvaloniaGeometry();
        return geom.FillContains(pt);
    }

    public override CanvasElement Clone()
    {
        var copy = new VectorShapeElement
        {
            Path = Path.Clone(),
            FillColor = FillColor,
            Gradient = Gradient,
            StrokeColor = StrokeColor,
            StrokeThickness = StrokeThickness,
            DashStyle = DashStyle,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            RotationDegrees = RotationDegrees,
            Opacity = Opacity,
            Name = Name != null ? $"{Name} copy" : null,
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };
        return copy;
    }
}
