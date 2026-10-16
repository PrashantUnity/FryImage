using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Material.Icons;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// A layer group (folder) containing nested child elements in the layer hierarchy.
/// Implements standard hierarchical layer group semantics.
/// </summary>
public class GroupElement : CanvasElement
{
    public override string ElementType => "Group";
    public override bool IsGroup => true;

    private bool _isExpanded = true;
    /// <summary>Whether the group folder is expanded in the Layers panel.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpandIconKind));
                OnPropertyChanged(nameof(FolderIconKind));
            }
        }
    }

    public override MaterialIconKind ExpandIconKind => IsExpanded ? MaterialIconKind.ChevronDown : MaterialIconKind.ChevronRight;
    public override MaterialIconKind FolderIconKind => IsExpanded ? MaterialIconKind.FolderOpenOutline : MaterialIconKind.FolderOutline;

    /// <summary>Direct child elements belonging to this group.</summary>
    public ObservableCollection<CanvasElement> Children { get; set; } = new();

    public override Rect Bounds
    {
        get
        {
            if (Children.Count == 0) return new Rect(X, Y, Width, Height);
            double minX = Children.Min(c => c.X);
            double minY = Children.Min(c => c.Y);
            double maxX = Children.Max(c => c.X + c.Width);
            double maxY = Children.Max(c => c.Y + c.Height);
            return new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
        }
    }

    public override void Draw(DrawingContext dc)
    {
        // When children are part of the canvas Elements list, they are rendered at their respective Z-indices.
    }

    public override bool HitTest(Point pt)
    {
        return Bounds.Contains(pt) || Children.Any(c => c.HitTest(pt));
    }

    public override CanvasElement Clone()
    {
        var copy = new GroupElement
        {
            IsExpanded = IsExpanded
        };
        CopyBasePropertiesTo(copy);
        foreach (var child in Children)
        {
            copy.Children.Add(child.Clone());
        }
        return copy;
    }
}
