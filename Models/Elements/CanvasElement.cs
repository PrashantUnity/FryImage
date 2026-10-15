using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Abstract base for all canvas elements in the Image Editor.
/// Each element knows how to draw itself via Avalonia DrawingContext.
/// </summary>
public abstract class CanvasElement : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Top-left X position on the canvas.</summary>
    public double X { get; set; }

    /// <summary>Top-left Y position on the canvas.</summary>
    public double Y { get; set; }

    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>Render order — higher = drawn on top.</summary>
    public int ZIndex { get; set; }

    /// <summary>Clockwise rotation in degrees about the element's <see cref="Bounds"/> center.</summary>
    public double RotationDegrees { get; set; }

    /// <summary>Mirrors the element horizontally about its <see cref="Bounds"/> center, applied before rotation.</summary>
    public bool FlipX { get; set; }

    /// <summary>Mirrors the element vertically about its <see cref="Bounds"/> center, applied before rotation.</summary>
    public bool FlipY { get; set; }

    private double _skewH;
    /// <summary>Horizontal skew angle in degrees (-89..89) about the element's Bounds center.</summary>
    public double SkewH
    {
        get => _skewH;
        set => _skewH = Math.Clamp(value, -89.0, 89.0);
    }

    private double _skewV;
    /// <summary>Vertical skew angle in degrees (-89..89) about the element's Bounds center.</summary>
    public double SkewV
    {
        get => _skewV;
        set => _skewV = Math.Clamp(value, -89.0, 89.0);
    }

    /// <summary>0 (fully transparent) .. 1 (fully opaque, default). Applied via dc.PushOpacity().</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>Fill opacity (0..1) affecting the layer contents without diminishing exterior effects like drop shadow.</summary>
    public double FillOpacity { get; set; } = 1.0;

    /// <summary>Layer blend mode.</summary>
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Normal;

    /// <summary>Layer styles (Drop Shadow, Outer Glow, Inner Shadow, Color Overlay, Stroke).</summary>
    public LayerStyleEffects Effects { get; set; } = new();

    /// <summary>When false, the element is skipped by rendering, hit-testing, and marquee-select
    /// (but remains selectable/togglable from the Layers panel).</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>When true, the element cannot be selected via canvas click/marquee, moved,
    /// resized, or deleted (but remains selectable from the Layers panel to inspect/unlock).</summary>
    public bool IsLocked { get; set; }

    /// <summary>Visual selection state for UI layer row binding.</summary>
    public bool IsSelected { get; set; }

    /// <summary>User-assigned layer name. Null falls back to <see cref="ElementType"/> via <see cref="DisplayName"/>.</summary>
    public string? Name { get; set; }

    /// <summary>Non-null Guid shared by every member of a persistent group; null means ungrouped.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>When true, resizing preserves <see cref="GetAspectRatio"/> unless the Shift
    /// modifier is held during the drag. Defaults false for shapes; ImageElement defaults true.</summary>
    public bool AspectLocked { get; set; }

    /// <summary>When true, this layer acts as a Studio clipping mask, clipped to the layer directly below it.</summary>
    public bool IsClippingMask { get; set; }

    /// <summary>Studio layer label color tag (None, Red, Orange, Yellow, Green, Blue, Violet, Gray).</summary>
    public LayerLabelColor LabelColor { get; set; } = LayerLabelColor.None;

    /// <summary>Convenience brush for UI layer row binding.</summary>
    public IBrush LabelColorBrush => LabelColor.ToBrush();

    /// <summary>Studio Blend If: This Layer luminance range (0..255).</summary>
    public byte BlendIfThisMin { get; set; } = 0;
    public byte BlendIfThisMax { get; set; } = 255;

    /// <summary>Studio Blend If: Underlying Layer luminance range (0..255).</summary>
    public byte BlendIfUnderMin { get; set; } = 0;
    public byte BlendIfUnderMax { get; set; } = 255;

    /// <summary>8-bit raster mask alpha buffer (0..255).</summary>
    public byte[]? MaskData { get; set; }

    /// <summary>Whether the raster mask is enabled.</summary>
    public bool IsMaskEnabled { get; set; } = true;

    /// <summary>Bounds of the raster mask if distinct from element Bounds.</summary>
    public Rect? MaskBounds { get; set; }

    /// <summary>Optional vector mask path attached to the element.</summary>
    public PdfEditorApp.Plugins.ImageEditor.Core.Vector.VectorDocumentPath? VectorMask { get; set; }

    /// <summary>Whether the vector mask is enabled.</summary>
    public bool IsVectorMaskEnabled { get; set; } = true;

    private bool _isTransparencyLocked;
    /// <summary>When true, transparency of this layer is locked against modification (Studio Lock Transparent Pixels, '/').</summary>
    public bool IsTransparencyLocked
    {
        get => _isTransparencyLocked;
        set
        {
            _isTransparencyLocked = value;
            if (value && _isPixelsLocked)
            {
                _isPixelsLocked = false;
            }
        }
    }

    private bool _isPixelsLocked;
    /// <summary>When true, pixel content of this layer is locked against modification (Studio Lock Pixels). Locking pixels clears transparency lock (#4892).</summary>
    public bool IsPixelsLocked
    {
        get => _isPixelsLocked;
        set
        {
            _isPixelsLocked = value;
            if (value)
            {
                // #4892: locking the pixels clears the transparency lock, as in Studio
                _isTransparencyLocked = false;
            }
        }
    }

    /// <summary>When true, spatial position and transforms of this layer are locked.</summary>
    public bool IsPositionLocked { get; set; }

    /// <summary>Whether this element serves structurally as the document's Background layer.</summary>
    public bool IsBackground { get; set; }

    /// <summary>When true, this layer is a Smart Object container with non-destructive Smart Filters.</summary>
    public bool IsSmartObject { get; set; }

    private bool _isCanvasHovered;
    /// <summary>When true, this layer is hovered on canvas by the Move tool (#4718, #4719).</summary>
    public bool IsCanvasHovered
    {
        get => _isCanvasHovered;
        set
        {
            if (_isCanvasHovered != value)
            {
                _isCanvasHovered = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isVectorShape;
    /// <summary>When true, this layer is a vector shape layer and displays the shape badge (#4584).</summary>
    public virtual bool IsVectorShape
    {
        get => _isVectorShape || this is VectorShapeElement;
        set
        {
            if (_isVectorShape != value)
            {
                _isVectorShape = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>When true, this layer is a group container (folder) with children.</summary>
    public virtual bool IsGroup => false;

    public virtual Material.Icons.MaterialIconKind ExpandIconKind => Material.Icons.MaterialIconKind.ChevronRight;
    public virtual Material.Icons.MaterialIconKind FolderIconKind => Material.Icons.MaterialIconKind.FolderOutline;

    private int _groupIndentLevel;
    /// <summary>Hierarchy indent level (0 = top-level, 1 = direct child of group, 2 = nested).</summary>
    public int GroupIndentLevel
    {
        get => _groupIndentLevel;
        set
        {
            if (_groupIndentLevel != value)
            {
                _groupIndentLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TreeIndentMargin));
                OnPropertyChanged(nameof(HasTreeGuideline));
            }
        }
    }

    private bool _isTextLayer;
    /// <summary>Whether this element is a text layer (e.g. from Studio TySh block or text tool).</summary>
    public virtual bool IsTextLayer
    {
        get => _isTextLayer || this is TextElement;
        set
        {
            if (_isTextLayer != value)
            {
                _isTextLayer = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Margin for hierarchical tree view indentation in Layers panel.</summary>
    public Thickness TreeIndentMargin => new Thickness(Math.Max(0, GroupIndentLevel * 14), 0, 0, 0);

    /// <summary>Whether this element is indented inside a tree structure.</summary>
    public bool HasTreeGuideline => GroupIndentLevel > 0;

    /// <summary>Whether this element has an attached raster layer mask (#4575).</summary>
    public bool HasMask => MaskData != null && MaskData.Length > 0;

    /// <summary>Non-destructive Smart Filters applied to this layer.</summary>
    public System.Collections.ObjectModel.ObservableCollection<SmartFilter> SmartFilters { get; set; } = [];

    /// <summary>Whether smart filters section is expanded in the layers dock panel.</summary>
    public bool AreSmartFiltersExpanded { get; set; } = true;

    /// <summary>
    /// Whether this layer has Studio Background traits: an unclipped layer at the bottom with
    /// position and transparency locked, regardless of its localized name.
    /// </summary>
    public bool HasBackgroundTraits => IsBackground || (IsTransparencyLocked && IsPositionLocked && !IsClippingMask && this is ImageElement);

    /// <summary>Copies common base properties to a newly cloned element.</summary>
    protected void CopyBasePropertiesTo(CanvasElement target)
    {
        target.X = X;
        target.Y = Y;
        target.Width = Width;
        target.Height = Height;
        target.ZIndex = ZIndex;
        target.RotationDegrees = RotationDegrees;
        target.FlipX = FlipX;
        target.FlipY = FlipY;
        target.Opacity = Opacity;
        target.FillOpacity = FillOpacity;
        target.BlendMode = BlendMode;
        target.Effects = Effects.Clone();
        target.IsVisible = IsVisible;
        target.IsLocked = IsLocked;
        target.IsTransparencyLocked = IsTransparencyLocked;
        target.IsPixelsLocked = IsPixelsLocked;
        target.IsPositionLocked = IsPositionLocked;
        target.IsBackground = IsBackground;
        target.IsSmartObject = IsSmartObject;
        target.AreSmartFiltersExpanded = AreSmartFiltersExpanded;
        target.SmartFilters = new System.Collections.ObjectModel.ObservableCollection<SmartFilter>();
        foreach (var sf in SmartFilters) target.SmartFilters.Add(sf.Clone());
        target.Name = Name;
        target.GroupId = GroupId;
        target.GroupIndentLevel = GroupIndentLevel;
        target.AspectLocked = AspectLocked;
        target.IsClippingMask = IsClippingMask;
        target.LabelColor = LabelColor;
        target.BlendIfThisMin = BlendIfThisMin;
        target.BlendIfThisMax = BlendIfThisMax;
        target.BlendIfUnderMin = BlendIfUnderMin;
        target.BlendIfUnderMax = BlendIfUnderMax;
        target.MaskData = MaskData != null ? (byte[])MaskData.Clone() : null;
        target.IsMaskEnabled = IsMaskEnabled;
        target.MaskBounds = MaskBounds;
        target.VectorMask = VectorMask?.Clone();
        target.IsVectorMaskEnabled = IsVectorMaskEnabled;
    }

    /// <summary>Display name shown in the layer panel.</summary>
    public abstract string ElementType { get; }

    /// <summary>The name shown in the Layers panel — <see cref="Name"/> if set, else <see cref="ElementType"/>.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? ElementType : Name!;

    /// <summary>Bounding rectangle for hit-testing and selection handles.</summary>
    public virtual Rect Bounds => new(X, Y, Width, Height);

    /// <summary>
    /// Bounding rectangle accounting for <see cref="RotationDegrees"/> — the true on-screen
    /// extent of a rotated element. Defaults to <see cref="Bounds"/> when not rotated.
    /// </summary>
    public virtual Rect GetRotatedBounds()
    {
        if (RotationDegrees == 0) return Bounds;

        var b = Bounds;
        var center = b.Center;
        double theta = RotationDegrees * Math.PI / 180.0;
        double cos = Math.Abs(Math.Cos(theta));
        double sin = Math.Abs(Math.Sin(theta));
        double rotatedWidth = b.Width * cos + b.Height * sin;
        double rotatedHeight = b.Width * sin + b.Height * cos;
        return new Rect(center.X - rotatedWidth / 2, center.Y - rotatedHeight / 2, rotatedWidth, rotatedHeight);
    }

    /// <summary>Aspect ratio used by aspect-locked resize. Default reads current Width/Height;
    /// ImageElement overrides this to its crop-aware CurrentAspectRatio.</summary>
    public virtual double GetAspectRatio() => Width > 0 && Height > 0 ? Width / Height : 1.0;

    /// <summary>
    /// Draw this element. Called from EditorCanvasControl.Render().
    /// </summary>
    public abstract void Draw(DrawingContext dc);

    /// <summary>
    /// Returns true if the point (in canvas coordinates) is inside this element.
    /// Default uses bounding box; override for non-rectangular shapes.
    /// </summary>
    public virtual bool HitTest(Point pt) => Bounds.Contains(pt);

    /// <summary>
    /// Rotation/flip-aware hit test — transforms the click point into the element's own local
    /// (unrotated/unflipped) space via the inverse of the same matrix DrawElement uses to
    /// render it, then delegates to the ordinary per-type <see cref="HitTest"/>. Precise even
    /// at arbitrary rotation angles (axis-aligned Bounds.Contains was only correct by
    /// coincidence at 0/90/180/270).
    /// </summary>
    public bool HitTestRotationAware(Point canvasPt)
    {
        if (RotationDegrees == 0 && !FlipX && !FlipY && SkewH == 0 && SkewV == 0) return HitTest(canvasPt);

        var center = Bounds.Center;
        double theta = RotationDegrees * Math.PI / 180.0;
        double sx = FlipX ? -1.0 : 1.0;
        double sy = FlipY ? -1.0 : 1.0;
        double skewHRad = Math.Clamp(SkewH, -89.0, 89.0) * Math.PI / 180.0;
        double skewVRad = Math.Clamp(SkewV, -89.0, 89.0) * Math.PI / 180.0;
        var skewMatrix = new Matrix(1, Math.Tan(skewVRad), Math.Tan(skewHRad), 1, 0, 0);

        var forward = Matrix.CreateTranslation(-center.X, -center.Y)
                    * Matrix.CreateScale(sx, sy)
                    * skewMatrix
                    * Matrix.CreateTranslation(center.X, center.Y)
                    * Matrix.CreateRotation(theta, center);

        return forward.TryInvert(out var inverse) ? HitTest(canvasPt.Transform(inverse)) : HitTest(canvasPt);
    }

    /// <summary>Creates an independent copy of this element (new Id). Subclasses copy every
    /// public settable property except Id; ImageElement shares its immutable OriginalImageBytes
    /// by reference but always independently re-decodes its Bitmap objects.</summary>
    public abstract CanvasElement Clone();
}
