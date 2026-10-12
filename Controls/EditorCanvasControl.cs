using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PdfEditorApp.Plugins.ImageEditor.Core.Paint;
using PdfEditorApp.Plugins.ImageEditor.Core.Vector;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

/// <summary>Snapshot of an ImageElement's crop/frame state, used to undo/redo a crop.</summary>
public readonly record struct ImageCropState(Rect? SourceCropRect, double X, double Y, double Width, double Height);

/// <summary>
/// Custom Avalonia Control that renders the canvas elements through a zoom/pan viewport
/// and handles pointer interaction for selecting, moving, resizing, placing new shapes,
/// and cropping images.
/// </summary>
public partial class EditorCanvasControl : Control
{
    // ── Handle geometry (screen-space; divided by zoom wherever compared against
    //    canvas-space coordinates, so handles read as a constant screen size) ──────
    private const double HandleSize = 8;
    private const double MinZoom = 0.1;
    private const double MaxZoom = 5.0;

    // ── State ──────────────────────────────────────────────────────────────
    private List<CanvasElement> _elements = new();
    private readonly List<CanvasElement> _selection = new();
    private Color _backgroundColor = Colors.White;

    // Drag / resize state (all in canvas-space, i.e. post ScreenToCanvas)
    private bool _isDragging;
    private bool _isResizing;
    private bool _dragOccurred;
    private int _resizeHandle = -1; // 0-7 → TL, T, TR, R, BR, B, BL, L
    private Point _dragStart;
    private double _origX, _origY, _origW, _origH;
    private Dictionary<CanvasElement, Point>? _groupDragOrigins;
    private CanvasElement? _pressedElement;

    // Marquee selection
    private bool _isMarqueeSelecting;
    private Point _marqueeStart, _marqueeEnd;

    // Tool-placement state machine
    private bool _isPlacingShape;
    private Point _shapeDragStart, _shapeDragEnd;

    // Freehand drawing (Brush / Pencil / Highlighter)
    private bool _isDrawingDoodle;
    private readonly List<Point> _currentDoodlePoints = new();
    private Point? _cursorCanvasPos;
    public double BrushThickness { get; set; } = 24.0;
    public Color BrushColor { get; set; } = Colors.Black;
    public bool IsHighlighterMode { get; set; }
    public double BrushHardness { get; set; } = 100.0;
    public double BrushSpacing { get; set; } = 25.0;
    public double BrushRoundness { get; set; } = 1.0;
    public double BrushAngle { get; set; } = 0.0;
    public int BrushSmoothing { get; set; } = 15;
    public bool BrushWetEdges { get; set; }
    public bool BrushAliased { get; set; }
    public TipShapeKind BrushTipShape { get; set; } = TipShapeKind.Round;
    public SymmetryMode ActiveSymmetryMode { get; set; } = SymmetryMode.Off;
    public bool PulledStringMode { get; set; } = true;
    public bool CatchUpOnStrokeEnd { get; set; } = true;

    // Real-time brush leash & pulled string dynamics (Photoshop 2026 Parity)
    private Point _smoothedBrushPos;
    private Point? _currentPointerCanvasPos;
    private bool _hasSmoothingLeash;
    private bool _isTildeEraseActive;

    // Retouching tools state (FryImage Studio)
    private bool _isDrawingRetouch;
    private readonly List<Point> _currentRetouchPoints = new();
    private string _currentRetouchTool = "clonestamp";
    public Point? CloneSourcePoint { get; set; }
    public bool CloneAligned { get; set; } = true;
    public Color PasteboardColor { get; set; } = Color.FromRgb(0x20, 0x20, 0x20);
    public System.Collections.ObjectModel.ObservableCollection<List<Point>>? PendingRemoveStrokes { get; set; }

    // Quick Mask mode state
    public bool IsQuickMaskMode { get; set; }
    public byte[]? QuickMaskBuffer { get; set; }
    private bool _isDrawingQuickMask;

    // Move & Transform tool options
    public bool AutoSelectLayers { get; set; } = true;
    public string AutoSelectTarget { get; set; } = "Layer";
    public bool ShowTransformControls { get; set; } = true;

    // Vector Path state (Pen & Path Selection tools)
    public VectorDocumentPath? ActiveDocumentPath { get; set; }
    private bool _isDraggingPenHandle;
    private PathKnot? _penActiveKnot;
    private bool _isDraggingPathKnot;
    private PathKnot? _selectedPathKnot;

    public event Action<Point>? CloneSourceDefined;
    public event Action<string, IReadOnlyList<Point>>? RetouchStrokeCompleted;

    public event Action<double>? BrushSizeAdjusted;
    public event Action<double>? BrushHardnessAdjusted;
    public event Action? SwapColorsRequested;
    public event Action? ResetColorsRequested;
    public event Action<string>? ToolSwitchRequested;
    public event Action? CommandPaletteRequested;
    public event Action? BrushSettingsRequested;

    public event Action<double>? ViewportRotationAngleChanged;
    public event Action<Point, Point>? RulerMeasurementChanged;
    public event Action<Point, Point>? RulerMeasurementCompleted;

    private bool _isRotatingView;
    private Point _rotateViewStartPointer;
    private double _rotateViewStartAngle;

    private bool _isRulerMeasuring;
    private Point _rulerStart;
    private Point _rulerEnd;

    // Crop mode
    private bool _isCropModeActive;
    private Rect _cropPreviewRect;
    private bool _isCropDragging;
    private int _cropResizeHandle = -1;
    private double _cropOrigX, _cropOrigY, _cropOrigW, _cropOrigH;
    private double? _cropLockedAspectRatio;

    // Free-angle rotation (single or multi-selection, pivot = own center for N=1)
    private bool _isRotating;
    private Point _rotationPivot;
    private double _rotationStartAngle;
    private Dictionary<CanvasElement, (double X, double Y, double Rotation)>? _rotationOrigins;

    // Proportional group-resize (2+ selected)
    private bool _isGroupResizing;
    private int _groupResizeHandle = -1;
    private Rect _groupResizeOrigBounds;
    private Dictionary<CanvasElement, Rect>? _groupResizeOrigins;

    // Smart alignment guides (rendered only while dragging)
    private List<(bool IsVertical, double Position)> _activeGuides = new();

    // Persistent document user guides
    private readonly List<(bool IsVertical, double Position, Color? Color)> _userGuides = new();
    public bool ShowUserGuides { get; set; } = true;
    public bool LockUserGuides { get; set; } = false;
    public bool SnapEnabled { get; set; } = true;
    public bool SnapToGuides { get; set; } = true;
    public bool SnapToGrid { get; set; } = true;
    public bool SnapToLayers { get; set; } = true;
    public bool SnapToDocumentBounds { get; set; } = true;

    public void AddUserGuide(bool isVertical, double position, Color? color = null)
    {
        _userGuides.Add((isVertical, position, color));
        InvalidateVisual();
    }

    public void ClearUserGuides()
    {
        _userGuides.Clear();
        InvalidateVisual();
    }

    public void ApplyGuideLayout(
        int columns,
        int rows,
        double colWidth,
        double rowHeight,
        double colGutter,
        double rowGutter,
        double marginTop,
        double marginLeft,
        double marginBottom,
        double marginRight,
        bool clearExisting)
    {
        if (clearExisting) _userGuides.Clear();

        double availWidth = _logicalWidth - marginLeft - marginRight;
        double availHeight = _logicalHeight - marginTop - marginBottom;

        if (columns > 0 && availWidth > 0)
        {
            double computedColWidth = colWidth > 0
                ? colWidth
                : (availWidth - Math.Max(0, columns - 1) * colGutter) / columns;

            double curX = marginLeft;
            for (int c = 0; c < columns; c++)
            {
                _userGuides.Add((true, curX, null));
                curX += computedColWidth;
                _userGuides.Add((true, curX, null));
                curX += colGutter;
            }
        }

        if (rows > 0 && availHeight > 0)
        {
            double computedRowHeight = rowHeight > 0
                ? rowHeight
                : (availHeight - Math.Max(0, rows - 1) * rowGutter) / rows;

            double curY = marginTop;
            for (int r = 0; r < rows; r++)
            {
                _userGuides.Add((false, curY, null));
                curY += computedRowHeight;
                _userGuides.Add((false, curY, null));
                curY += rowGutter;
            }
        }

        InvalidateVisual();
    }

    // Arrow-key nudge (committed as one undo step after a short idle window)
    private Dictionary<CanvasElement, Point>? _nudgeOrigins;
    private readonly DispatcherTimer _nudgeCommitTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    // Zoom / pan viewport state
    private double _logicalWidth = 640, _logicalHeight = 480;
    private double _zoom = 1.0;
    private Point _panOffset;
    private bool _isFitModeActive = true;

    // Panning (screen-space)
    private bool _isPanning;
    private bool _spaceHeld;
    private Point _panLastScreenPos;

    static EditorCanvasControl()
    {
        FocusableProperty.OverrideDefaultValue<EditorCanvasControl>(true);
    }

    public EditorCanvasControl()
    {
        ClipToBounds = true;
        _nudgeCommitTimer.Tick += OnNudgeCommitTick;
    }

    // ── Public events ──────────────────────────────────────────────────────
    public event Action<IReadOnlyList<CanvasElement>>? SelectionChanged;
    public event Action? CanvasChanged;
    public event Action<double>? ZoomChanged;
    public event Action<IReadOnlyList<(CanvasElement Element, Point OldPos, Point NewPos)>>? ElementsMoved;
    public event Action<CanvasElement, Rect, Rect>? ElementResized;

    /// <summary>Fired when the Text tool is active and the user clicks the canvas.</summary>
    public event Action<Point>? PlaceTextRequested;

    /// <summary>Fired when a Rect/Ellipse/Arrow tool placement completes (click or drag-to-size).</summary>
    public event Action<string, Point, Point>? ShapePlaced;

    /// <summary>Fired after a crop is confirmed or reset, carrying before/after state for undo.</summary>
    public event Action<ImageElement, ImageCropState, ImageCropState>? ElementCropped;

    /// <summary>Fired once the Eyedropper tool samples a pixel.</summary>
    public event Action<Color>? ColorSampled;

    public double CanvasOriginScreenX => (Bounds.Width - _logicalWidth * _zoom) / 2 + _panOffset.X;
    public double CanvasOriginScreenY => (Bounds.Height - _logicalHeight * _zoom) / 2 + _panOffset.Y;
    public event Action<Point>? CursorPositionChanged;

    private bool _isGridVisible = true;
    public bool IsGridVisible
    {
        get => _isGridVisible;
        set
        {
            _isGridVisible = value;
            InvalidateVisual();
        }
    }

    /// <summary>Fired after a single-element (or N=1 group) rotation-handle drag completes.</summary>
    public event Action<CanvasElement, (double X, double Y, double Rotation), (double X, double Y, double Rotation)>? ElementRotated;

    /// <summary>Fired after a multi-element rotation-handle drag completes.</summary>
    public event Action<IReadOnlyDictionary<CanvasElement, (double X, double Y, double Rotation)>, IReadOnlyDictionary<CanvasElement, (double X, double Y, double Rotation)>>? GroupRotated;

    /// <summary>Fired after a proportional group-resize drag completes.</summary>
    public event Action<IReadOnlyList<(CanvasElement Element, Rect Old, Rect New)>>? GroupResized;

    /// <summary>Fired when the user double-clicks a TextElement while the Select tool is active.</summary>
    public event Action<TextElement>? TextEditRequested;

    /// <summary>Fired when the user double-clicks any non-text element while the Select tool is active (e.g. to open Layer Styles).</summary>
    public event Action<CanvasElement>? ElementDoubleClicked;

    /// <summary>Fired when a freehand brush or highlighter stroke completes.</summary>
    public event Action<IReadOnlyList<Point>>? DoodlePlaced;

    /// <summary>Fired when the eraser tool clicks or drags over an element.</summary>
    public event Action<CanvasElement>? ElementEraseRequested;

    // ── Tool mode ──────────────────────────────────────────────────────────
    private string _activeToolMode = "select";
    public string ActiveToolMode
    {
        get => _activeToolMode;
        set
        {
            _activeToolMode = value;
            UpdateCursorForTool();
        }
    }

    private void UpdateCursorForTool()
    {
        if (_spaceHeld || _isPanning || ActiveToolMode == "hand")
        {
            Cursor = new Cursor(StandardCursorType.Hand);
            return;
        }
        Cursor = ActiveToolMode switch
        {
            "brush" => new Cursor(StandardCursorType.Cross),
            "eraser" => new Cursor(StandardCursorType.Cross),
            "text" => new Cursor(StandardCursorType.Ibeam),
            "eyedropper" => new Cursor(StandardCursorType.Cross),
            "marquee" or "lasso" or "wand" => new Cursor(StandardCursorType.Cross),
            "zoom" => new Cursor(StandardCursorType.Cross),
            "hand" or "rotateview" => new Cursor(StandardCursorType.Hand),
            "ruler" => new Cursor(StandardCursorType.Cross),
            "redeye" or "patch" => new Cursor(StandardCursorType.Cross),
            "rect" or "ellipse" or "arrow" or "triangle" or "star" or "polygon" or "line" => new Cursor(StandardCursorType.Cross),
            _ => new Cursor(StandardCursorType.Arrow)
        };
    }

    public static readonly DirectProperty<EditorCanvasControl, double> ViewportRotationAngleProperty =
        AvaloniaProperty.RegisterDirect<EditorCanvasControl, double>(
            nameof(ViewportRotationAngle),
            o => o.ViewportRotationAngle,
            (o, v) => o.ViewportRotationAngle = v);

    private double _viewportRotationAngle = 0.0;
    public double ViewportRotationAngle
    {
        get => _viewportRotationAngle;
        set
        {
            if (SetAndRaise(ViewportRotationAngleProperty, ref _viewportRotationAngle, value))
            {
                ViewportRotationAngleChanged?.Invoke(value);
                InvalidateVisual();
            }
        }
    }

    // ── Multi-select ───────────────────────────────────────────────────────
    public IReadOnlyList<CanvasElement> Selection => _selection;

    public void SetSelection(IEnumerable<CanvasElement> elements)
    {
        _selection.Clear();
        _selection.AddRange(elements);
        InvalidateVisual();
    }

    /// <summary>Convenience for single-element selection (also used by the crop mode's
    /// "only one image selected" precondition).</summary>
    public void SetSelected(CanvasElement? element)
    {
        _selection.Clear();
        if (element != null) _selection.Add(element);
        InvalidateVisual();
    }

    // ── Crop mode ──────────────────────────────────────────────────────────
    public bool IsCropModeActive
    {
        get => _isCropModeActive;
        set
        {
            _isCropModeActive = value;
            if (value) InitializeCropRectForSelection();
            InvalidateVisual();
        }
    }

    private void InitializeCropRectForSelection()
    {
        _cropLockedAspectRatio = null;
        if (_selection.Count == 1 && _selection[0] is ImageElement img)
        {
            _cropPreviewRect = img.Bounds;
        }
        else
        {
            _isCropModeActive = false;
        }
    }

    /// <summary>Snaps the pending crop rect to a fixed aspect ratio, centered within the
    /// image's current frame, and locks that ratio for subsequent handle drags. Pass null for
    /// "Original" (the source image's natural aspect ratio).</summary>
    public void SetCropAspectPreset(double? ratio)
    {
        if (!_isCropModeActive || _selection.Count != 1 || _selection[0] is not ImageElement img) return;

        var frame = img.Bounds;
        double r = ratio ?? (img.NaturalPixelWidth > 0 && img.NaturalPixelHeight > 0
            ? (double)img.NaturalPixelWidth / img.NaturalPixelHeight
            : frame.Width / Math.Max(frame.Height, 0.01));
        if (r <= 0) return;

        double w = frame.Width, h = frame.Width / r;
        if (h > frame.Height) { h = frame.Height; w = frame.Height * r; }

        _cropLockedAspectRatio = r;
        _cropPreviewRect = new Rect(frame.X + (frame.Width - w) / 2, frame.Y + (frame.Height - h) / 2, w, h);
        InvalidateVisual();
    }

    public void ConfirmCrop()
    {
        if (_selection.Count != 1 || _selection[0] is not ImageElement img)
        {
            IsCropModeActive = false;
            return;
        }

        var before = new ImageCropState(img.SourceCropRect, img.X, img.Y, img.Width, img.Height);

        var srcRect = img.EffectiveSourceRect;
        double scaleX = img.Width > 0 ? srcRect.Width / img.Width : 1;
        double scaleY = img.Height > 0 ? srcRect.Height / img.Height : 1;
        double localDx = _cropPreviewRect.X - img.X;
        double localDy = _cropPreviewRect.Y - img.Y;

        img.SourceCropRect = new Rect(
            srcRect.X + localDx * scaleX, srcRect.Y + localDy * scaleY,
            _cropPreviewRect.Width * scaleX, _cropPreviewRect.Height * scaleY);
        img.X = _cropPreviewRect.X;
        img.Y = _cropPreviewRect.Y;
        img.Width = _cropPreviewRect.Width;
        img.Height = _cropPreviewRect.Height;

        var after = new ImageCropState(img.SourceCropRect, img.X, img.Y, img.Width, img.Height);

        _isCropModeActive = false;
        _cropLockedAspectRatio = null;
        CanvasChanged?.Invoke();
        ElementCropped?.Invoke(img, before, after);
        InvalidateVisual();
    }

    public void CancelCrop()
    {
        _isCropModeActive = false;
        _cropLockedAspectRatio = null;
        InvalidateVisual();
    }

    public void ResetCrop()
    {
        if (_selection.Count != 1 || _selection[0] is not ImageElement img) return;

        var before = new ImageCropState(img.SourceCropRect, img.X, img.Y, img.Width, img.Height);
        img.SourceCropRect = null;
        var after = new ImageCropState(img.SourceCropRect, img.X, img.Y, img.Width, img.Height);

        _cropLockedAspectRatio = null;
        CanvasChanged?.Invoke();
        ElementCropped?.Invoke(img, before, after);
        InvalidateVisual();
    }

    // ── Public API ─────────────────────────────────────────────────────────
    public void SetElements(List<CanvasElement> elements)
    {
        // Sorted once here (called after every add/remove/Z-order/undo-redo mutation via
        // RefreshCanvas/Elements.CollectionChanged) rather than on every single Render() call,
        // which used to re-run a LINQ sort on every pointer-move tick during a drag.
        _elements = elements.OrderBy(e => e.ZIndex).ToList();
        InvalidateVisual();
    }

    public void SetBackgroundColor(Color color)
    {
        _backgroundColor = color;
        InvalidateVisual();
    }

    public void Refresh() => InvalidateVisual();

    /// <summary>The fixed logical canvas size (independent of the control's rendered
    /// bounds) — this is what PNG export/crop/etc. operate against.</summary>
    public void SetLogicalCanvasSize(double width, double height)
    {
        _logicalWidth = width;
        _logicalHeight = height;
        if (_isFitModeActive) FitToWindow();
        InvalidateVisual();
    }

    public double Zoom => _zoom;

    public void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(Math.Round(zoom, 3), MinZoom, MaxZoom);
        _isFitModeActive = false;
        ZoomChanged?.Invoke(_zoom);
        InvalidateVisual();
    }

    public void ZoomBy(double factor) => SetZoom(_zoom * factor);

    public void ZoomAtPoint(double newZoom, Point screenPos)
    {
        var canvasPtBefore = ScreenToCanvas(screenPos);
        _zoom = Math.Clamp(Math.Round(newZoom, 3), MinZoom, MaxZoom);
        _isFitModeActive = false;

        double centerOffsetX = (Bounds.Width - _logicalWidth * _zoom) / 2;
        double centerOffsetY = (Bounds.Height - _logicalHeight * _zoom) / 2;
        _panOffset = new Point(
            screenPos.X - canvasPtBefore.X * _zoom - centerOffsetX,
            screenPos.Y - canvasPtBefore.Y * _zoom - centerOffsetY);

        ClampPanOffset();
        ZoomChanged?.Invoke(_zoom);
        CanvasChanged?.Invoke();
        InvalidateVisual();
    }

    public void ResetZoomToActual()
    {
        _panOffset = default;
        SetZoom(1.0);
    }

    public void FitToWindow()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0 || _logicalWidth <= 0 || _logicalHeight <= 0) return;

        const double padding = 24;
        double availableW = Math.Max(Bounds.Width - padding * 2, 10);
        double availableH = Math.Max(Bounds.Height - padding * 2, 10);
        double fitZoom = Math.Min(availableW / _logicalWidth, availableH / _logicalHeight);

        _zoom = Math.Clamp(Math.Round(fitZoom, 3), MinZoom, MaxZoom);
        _panOffset = default;
        _isFitModeActive = true;
        ZoomChanged?.Invoke(_zoom);
        InvalidateVisual();
    }

    /// <summary>
    /// Fits the specified layer element bounds on screen with comfortable padding, centered in the viewport.
    /// (Photoshop Alt-Click thumbnail / View › Fit Layer(s) on Screen).
    /// </summary>
    public void FitElementToWindow(Rect elementBounds)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0 || elementBounds.Width <= 0 || elementBounds.Height <= 0) return;

        const double padding = 36;
        double availableW = Math.Max(Bounds.Width - padding * 2, 10);
        double availableH = Math.Max(Bounds.Height - padding * 2, 10);
        double fitZoom = Math.Min(availableW / elementBounds.Width, availableH / elementBounds.Height);

        _zoom = Math.Clamp(Math.Round(fitZoom, 3), MinZoom, MaxZoom);
        _isFitModeActive = false;

        double centerOffsetX = (Bounds.Width - _logicalWidth * _zoom) / 2;
        double centerOffsetY = (Bounds.Height - _logicalHeight * _zoom) / 2;
        var center = elementBounds.Center;
        _panOffset = new Point(
            (Bounds.Width / 2.0) - center.X * _zoom - centerOffsetX,
            (Bounds.Height / 2.0) - center.Y * _zoom - centerOffsetY);

        ClampPanOffset();
        ZoomChanged?.Invoke(_zoom);
        InvalidateVisual();
    }

    // ── Viewport transform ─────────────────────────────────────────────────
    private Matrix GetViewportMatrix()
    {
        double offsetX = (Bounds.Width - _logicalWidth * _zoom) / 2 + _panOffset.X;
        double offsetY = (Bounds.Height - _logicalHeight * _zoom) / 2 + _panOffset.Y;
        var scaleAndTranslate = new Matrix(_zoom, 0, 0, _zoom, offsetX, offsetY);
        if (_viewportRotationAngle != 0.0)
        {
            Point screenCenter = new Point(Bounds.Width / 2 + _panOffset.X, Bounds.Height / 2 + _panOffset.Y);
            double rad = _viewportRotationAngle * Math.PI / 180.0;
            var rot = Matrix.CreateTranslation(-screenCenter.X, -screenCenter.Y)
                    * Matrix.CreateRotation(rad)
                    * Matrix.CreateTranslation(screenCenter.X, screenCenter.Y);
            return scaleAndTranslate * rot;
        }
        return scaleAndTranslate;
    }

    private Point ScreenToCanvas(Point screenPt)
    {
        var m = GetViewportMatrix();
        return m.TryInvert(out var inv) ? screenPt.Transform(inv) : screenPt;
    }

    private void ClampPanOffset()
    {
        double renderedW = _logicalWidth * _zoom;
        double renderedH = _logicalHeight * _zoom;
        const double minVisible = 40;

        double centerOffsetX = (Bounds.Width - renderedW) / 2;
        double centerOffsetY = (Bounds.Height - renderedH) / 2;

        double minPanX = -renderedW - centerOffsetX + minVisible;
        double maxPanX = Bounds.Width - centerOffsetX - minVisible;
        double minPanY = -renderedH - centerOffsetY + minVisible;
        double maxPanY = Bounds.Height - centerOffsetY - minVisible;

        double x = minPanX <= maxPanX ? Math.Clamp(_panOffset.X, minPanX, maxPanX) : _panOffset.X;
        double y = minPanY <= maxPanY ? Math.Clamp(_panOffset.Y, minPanY, maxPanY) : _panOffset.Y;
        _panOffset = new Point(x, y);
    }

    // ── Render ─────────────────────────────────────────────────────────────
    public override void Render(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);

        // Pasteboard behind the logical canvas (visible when zoomed/panned out of frame).
        dc.DrawRectangle(new SolidColorBrush(PasteboardColor), null, bounds);

        using (dc.PushTransform(GetViewportMatrix()))
        {
            var canvasRect = new Rect(0, 0, _logicalWidth, _logicalHeight);

            // Physical artboard drop shadow and depth (gives realistic paper elevation on pasteboard)
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(55, 0, 0, 0)), null, new Rect(-4, 4, _logicalWidth + 8, _logicalHeight + 10));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null, new Rect(-1, 1, _logicalWidth + 2, _logicalHeight + 3));

            // 1. Canvas background — checkerboard is cached to a tile bitmap (regenerated
            // only when the logical canvas size changes) rather than redrawn cell-by-cell on
            // every Render() call, which used to run during every drag/pan/zoom tick.
            if (_backgroundColor.A == 0)
            {
                dc.DrawImage(GetCheckerboardTile(_logicalWidth, _logicalHeight), canvasRect);
            }
            else
            {
                dc.DrawRectangle(new SolidColorBrush(_backgroundColor), null, canvasRect);
            }

            // 2. Canvas grid dots (subtle) — same tile-caching treatment (~7000 DrawEllipse
            // calls on a large canvas, otherwise re-run on every single-pointer-move tick).
            if (_isGridVisible)
            {
                dc.DrawImage(GetGridTile(_logicalWidth, _logicalHeight), canvasRect);
            }

            // Artboard crisp hairline boundary
            var outlineColor = (_backgroundColor.A == 0 || (_backgroundColor.R > 200 && _backgroundColor.G > 200 && _backgroundColor.B > 200))
                ? Color.FromArgb(45, 0, 0, 0)
                : Color.FromArgb(55, 255, 255, 255);
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(outlineColor), 1), canvasRect);

            // 3. Elements (already Z-sorted by SetElements), each with its own rotate/flip transform
            foreach (var el in _elements)
            {
                DrawElement(dc, el);
            }

            // Pixel grid overlay at >= 500% zoom (Studio standard)
            DrawPixelGrid(dc);

            // 4. Selection / marquee / shape-preview / crop overlay
            if (_isCropModeActive && _selection.Count == 1 && _selection[0] is ImageElement cropImg)
            {
                DrawCropOverlay(dc, cropImg, _zoom);
            }
            else if (_selection.Count == 1)
            {
                if (ShowTransformControls && ActiveToolMode == "select")
                {
                    DrawSelectionHandles(dc, _selection[0], _zoom);
                    DrawRotationHandle(dc, _selection[0].Bounds, _selection[0].RotationDegrees, _zoom);
                }
            }
            else if (_selection.Count > 1)
            {
                if (ShowTransformControls && ActiveToolMode == "select")
                {
                    DrawMultiSelectionOutline(dc, _selection, _zoom);
                    var groupBounds = ComputeGroupBounds(_selection);
                    DrawResizeHandles(dc, groupBounds, _zoom);
                    DrawRotationHandle(dc, groupBounds, 0, _zoom);
                }
            }

            if (_isMarqueeSelecting)
            {
                DrawMarquee(dc, _marqueeStart, _marqueeEnd, _zoom);
            }

            if (_isLassoActive || _lassoPoints.Count > 0 || _lassoKnots.Count > 0)
            {
                DrawLassoOverlay(dc, _zoom);
            }

            if (_isPlacingShape)
            {
                DrawShapePlacementPreview(dc, ActiveToolMode, _shapeDragStart, _shapeDragEnd, _zoom);
            }

            // Vector Document Path overlay (Pen & Path Selection tools)
            if (ActiveDocumentPath != null && !ActiveDocumentPath.IsEmpty)
            {
                DrawVectorPathOverlay(dc, ActiveDocumentPath, _zoom);
            }

            // Quick Mask Mode ruby overlay
            if (IsQuickMaskMode && QuickMaskBuffer != null)
            {
                DrawQuickMaskOverlay(dc, QuickMaskBuffer, _logicalWidth, _logicalHeight);
            }

            if (_isDrawingDoodle && _currentDoodlePoints.Count > 0)
            {
                var strokeColor = BrushColor;
                if (IsHighlighterMode && strokeColor.A > 140)
                {
                    strokeColor = Color.FromArgb(120, strokeColor.R, strokeColor.G, strokeColor.B);
                }

                // If symmetry is active, render live reflected passes
                var center = new Point(_logicalWidth / 2.0, _logicalHeight / 2.0);
                var passes = BrushEngine.ApplySymmetry(_currentDoodlePoints, ActiveSymmetryMode, center);

                foreach (var passPoints in passes)
                {
                    if (BrushWetEdges)
                    {
                        var geom = DoodleElement.BuildSmoothGeometry(passPoints);
                        var washColor = Color.FromArgb((byte)(strokeColor.A * 0.45), strokeColor.R, strokeColor.G, strokeColor.B);
                        var washPen = new Pen(new SolidColorBrush(washColor), BrushThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                        dc.DrawGeometry(null, washPen, geom);

                        var rimColor = Color.FromArgb((byte)(strokeColor.A * 0.85), (byte)Math.Max(0, strokeColor.R - 30), (byte)Math.Max(0, strokeColor.G - 30), (byte)Math.Max(0, strokeColor.B - 30));
                        var rimPen = new Pen(new SolidColorBrush(rimColor), Math.Max(1.5, BrushThickness * 0.18), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                        dc.DrawGeometry(null, rimPen, geom);
                    }
                    else if (BrushAliased)
                    {
                        var geom = new StreamGeometry();
                        using (var sctx = geom.Open())
                        {
                            sctx.BeginFigure(passPoints[0], false);
                            for (int pi = 1; pi < passPoints.Count; pi++) sctx.LineTo(passPoints[pi]);
                            sctx.EndFigure(false);
                        }
                        var pen = new Pen(new SolidColorBrush(strokeColor), Math.Max(1.0, Math.Round(BrushThickness)), lineCap: PenLineCap.Square, lineJoin: PenLineJoin.Miter);
                        dc.DrawGeometry(null, pen, geom);
                    }
                    else
                    {
                        var geometry = DoodleElement.BuildSmoothGeometry(passPoints);
                        var pen = new Pen(
                            new SolidColorBrush(strokeColor),
                            BrushThickness,
                            lineCap: PenLineCap.Round,
                            lineJoin: PenLineJoin.Round);
                        dc.DrawGeometry(null, pen, geometry);
                    }
                }

                // Draw live smoothing string (Photoshop pulled string leash mode, #3479 parity)
                if (BrushSmoothing > 0 && _hasSmoothingLeash && _currentPointerCanvasPos.HasValue)
                {
                    var leashBrushPt = _smoothedBrushPos;
                    var leashPtrPt = _currentPointerCanvasPos.Value;
                    if (Point.Distance(leashBrushPt, leashPtrPt) >= 2.0 / _zoom)
                    {
                        // Dark underlay line (black alpha 110, 3px wide)
                        var shadowPen = new Pen(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), 3.0 / _zoom, lineCap: PenLineCap.Round);
                        dc.DrawLine(shadowPen, leashBrushPt, leashPtrPt);

                        // Crisp white line (1px wide)
                        var stringPen = new Pen(Brushes.White, 1.0 / _zoom, lineCap: PenLineCap.Round);
                        dc.DrawLine(stringPen, leashBrushPt, leashPtrPt);

                        // Tip marker circle dot at brush end
                        dc.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), 0.75 / _zoom), leashBrushPt, 2.5 / _zoom, 2.5 / _zoom);
                    }
                }
            }

            // Draw pending remove tool strokes (accumulated when RemoveAfterEachStroke is off)
            if (PendingRemoveStrokes != null && PendingRemoveStrokes.Count > 0)
            {
                var removeStrokePen = new Pen(new SolidColorBrush(Color.FromArgb(170, 255, 60, 115)), BrushThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                foreach (var stroke in PendingRemoveStrokes)
                {
                    if (stroke.Count <= 1)
                    {
                        if (stroke.Count == 1)
                        {
                            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(170, 255, 60, 115)), null, stroke[0], BrushThickness / 2.0, BrushThickness / 2.0);
                        }
                        continue;
                    }
                    var geom = new StreamGeometry();
                    using (var ctx = geom.Open())
                    {
                        ctx.BeginFigure(stroke[0], false);
                        for (int i = 1; i < stroke.Count; i++)
                        {
                            ctx.LineTo(stroke[i]);
                        }
                    }
                    dc.DrawGeometry(null, removeStrokePen, geom);
                }
            }

            // Draw live retouch stroke feedback
            if (_isDrawingRetouch && _currentRetouchPoints.Count > 0)
            {
                var strokeColor = _currentRetouchTool == "remove"
                    ? Color.FromArgb(170, 255, 60, 115)
                    : Color.FromArgb(120, 55, 142, 240);
                var retouchPen = new Pen(new SolidColorBrush(strokeColor), BrushThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                if (_currentRetouchPoints.Count == 1)
                {
                    dc.DrawEllipse(new SolidColorBrush(strokeColor), null, _currentRetouchPoints[0], BrushThickness / 2.0, BrushThickness / 2.0);
                }
                else
                {
                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        ctx.BeginFigure(_currentRetouchPoints[0], false);
                        for (int i = 1; i < _currentRetouchPoints.Count; i++)
                        {
                            ctx.LineTo(_currentRetouchPoints[i]);
                        }
                    }
                    dc.DrawGeometry(null, retouchPen, geometry);
                }
            }

            // Draw symmetry axis guides if active
            if (ActiveSymmetryMode != SymmetryMode.Off && (ActiveToolMode is "brush" or "pencil"))
            {
                DrawSymmetryAxes(dc, _logicalWidth, _logicalHeight, ActiveSymmetryMode, _zoom);
            }

            // Draw brush tip cursor outline under mouse pointer
            if (_cursorCanvasPos is { } cursorPt
                && (ActiveToolMode is "brush" or "pencil" or "eraser" or "clonestamp" or "healing" or "remove" or "historybrush" or "blur" or "sharpen" or "smudge" or "dodge" or "burn" or "sponge" or "bgeraser" or "colorreplacement" or "redeye" or "patch")
                && !_isPanning && !_spaceHeld)
            {
                DrawBrushCursor(dc, cursorPt, BrushThickness, BrushRoundness, BrushAngle, _zoom);
            }

            // Draw Clone Stamp sampled source crosshair marker
            if (_isDrawingRetouch && _currentRetouchTool == "clonestamp" && CloneSourcePoint.HasValue && _currentRetouchPoints.Count > 0)
            {
                Point samplePt = CloneSourcePoint.Value + (_currentRetouchPoints[^1] - _currentRetouchPoints[0]);
                DrawSourceMarker(dc, samplePt, _zoom);
            }
            else if (ActiveToolMode == "clonestamp" && CloneSourcePoint.HasValue && !_isDrawingRetouch)
            {
                DrawSourceMarker(dc, CloneSourcePoint.Value, _zoom);
            }

            if (ShowUserGuides && _userGuides.Count > 0)
            {
                DrawUserGuides(dc, _userGuides, _zoom);
            }

            DrawActiveDragGuideInCanvasSpace(dc);

            if (_activeGuides.Count > 0)
            {
                DrawAlignmentGuides(dc, _activeGuides, _zoom);
            }

            if (ActiveToolMode == "ruler" && (_isRulerMeasuring || _rulerStart != _rulerEnd))
            {
                DrawRulerOverlay(dc, _rulerStart, _rulerEnd, _zoom);
            }
        }

        // Active Guide & Transform Floating HUD Pill Badges (Screen space)
        DrawActiveGuideHudBadge(dc);
        DrawTransformHudBadge(dc);

        // Rotate View On-Canvas Compass HUD (Screen space, top-right)
        if (_viewportRotationAngle != 0.0 || ActiveToolMode == "rotateview")
        {
            DrawRotateViewCompass(dc);
        }

        // Eyedropper Magnifier Loupe (Screen-space floating HUD)
        if (ActiveToolMode == "eyedropper" && _cursorCanvasPos is { } eyeCursor && !_isPanning && !_spaceHeld)
        {
            var screenPos = CanvasToScreen(eyeCursor);
            int cx = (int)Math.Floor(eyeCursor.X);
            int cy = (int)Math.Floor(eyeCursor.Y);
            if (cx >= 0 && cy >= 0 && cx < _logicalWidth && cy < _logicalHeight)
            {
                var grid = SampleLocalColorGrid(cx, cy, 5);
                var sampledColor = grid[5, 5];
                DrawEyedropperLoupe(dc, screenPos, grid, sampledColor);
            }
        }
    }

    /// <summary>Renders a flat, already-Z-ordered element list (background + elements) into an
    /// arbitrary DrawingContext — shared by the live canvas Render() and standalone preview
    /// rendering (the template gallery), so both paths produce identical visual output.</summary>
    public static void RenderElementsStatic(DrawingContext dc, IEnumerable<CanvasElement> elements, Color background, double width, double height)
    {
        var canvasRect = new Rect(0, 0, width, height);
        if (background.A == 0)
        {
            DrawCheckerboardBackground(dc, canvasRect);
        }
        else
        {
            dc.DrawRectangle(new SolidColorBrush(background), null, canvasRect);
        }
        var orderedElements = elements.OrderBy(e => e.ZIndex).ToList();
        for (int i = 0; i < orderedElements.Count; i++)
        {
            var el = orderedElements[i];
            if (!el.IsVisible) continue;

            if (el.IsClippingMask && i > 0)
            {
                // Find base layer below
                CanvasElement? baseEl = null;
                for (int b = i - 1; b >= 0; b--)
                {
                    if (!orderedElements[b].IsClippingMask)
                    {
                        baseEl = orderedElements[b];
                        break;
                    }
                }

                if (baseEl != null)
                {
                    using (dc.PushClip(baseEl.Bounds))
                    {
                        DrawElement(dc, el);
                    }
                    continue;
                }
            }

            DrawElement(dc, el);
        }
    }

    public static void DrawElement(DrawingContext dc, CanvasElement el)
    {
        if (!el.IsVisible) return;

        // Guarded so Opacity==1.0 (the common case) skips pushing an extra Skia layer entirely.
        IDisposable? opacityScope = el.Opacity < 1.0 ? dc.PushOpacity(Math.Clamp(el.Opacity, 0, 1)) : null;
        try
        {
            if (el.RotationDegrees == 0 && !el.FlipX && !el.FlipY)
            {
                DrawElementWithEffects(dc, el);
                return;
            }

            var center = el.Bounds.Center;
            double theta = el.RotationDegrees * Math.PI / 180.0;
            double sx = el.FlipX ? -1.0 : 1.0;
            double sy = el.FlipY ? -1.0 : 1.0;

            // Flip about the element's center, then rotate about the same center.
            var m = Matrix.CreateTranslation(-center.X, -center.Y)
                  * Matrix.CreateScale(sx, sy)
                  * Matrix.CreateTranslation(center.X, center.Y)
                  * Matrix.CreateRotation(theta, center);

            using (dc.PushTransform(m))
            {
                DrawElementWithEffects(dc, el);
            }
        }
        finally
        {
            opacityScope?.Dispose();
        }
    }

    private static void DrawElementWithEffects(DrawingContext dc, CanvasElement el)
    {
        var bodyRect = new Rect(el.X, el.Y, el.Width, el.Height);
        double cornerRadius = (el is RectangleElement r) ? r.CornerRadius : (el is EllipseElement ? Math.Min(el.Width, el.Height) / 2 : 0);

        // 1. Drop Shadow Effect (Rendered under the element)
        if (el.Effects.DropShadow is { IsEnabled: true } shadow)
        {
            double rad = shadow.Angle * Math.PI / 180.0;
            double dx = shadow.Distance * Math.Cos(rad);
            double dy = shadow.Distance * Math.Sin(rad);
            byte sa = (byte)Math.Clamp((int)(shadow.Opacity * shadow.Color.A), 0, 255);

            int passes = Math.Max(1, (int)(shadow.Size / 3));
            double spreadRadius = shadow.Spread;
            for (int p = 1; p <= passes; p++)
            {
                double blurStep = (shadow.Size * p) / passes;
                byte passAlpha = (byte)Math.Max(1, sa / (passes * 1.5));
                var passColor = Color.FromArgb(passAlpha, shadow.Color.R, shadow.Color.G, shadow.Color.B);
                var shadowRect = new Rect(
                    el.X + dx - blurStep / 2 - spreadRadius,
                    el.Y + dy - blurStep / 2 - spreadRadius,
                    el.Width + blurStep + spreadRadius * 2,
                    el.Height + blurStep + spreadRadius * 2);

                double radius = (el is RectangleElement rect) ? rect.CornerRadius : (el is EllipseElement ? Math.Min(shadowRect.Width, shadowRect.Height) / 2 : 4);
                dc.DrawRectangle(new SolidColorBrush(passColor), null, shadowRect, radius, radius);
            }
        }

        // 2. Outer Glow Effect (Rendered around the element perimeter)
        if (el.Effects.OuterGlow is { IsEnabled: true } glow)
        {
            byte ga = (byte)Math.Clamp((int)(glow.Opacity * glow.Color.A), 0, 255);
            int glowPasses = Math.Max(2, (int)(glow.Size / 3));
            for (int p = 1; p <= glowPasses; p++)
            {
                double blurStep = (glow.Size * p) / glowPasses;
                byte passAlpha = (byte)Math.Max(1, ga / (glowPasses * 1.8));
                var glowColor = Color.FromArgb(passAlpha, glow.Color.R, glow.Color.G, glow.Color.B);
                var glowRect = new Rect(
                    el.X - blurStep / 2,
                    el.Y - blurStep / 2,
                    el.Width + blurStep,
                    el.Height + blurStep);

                double radius = (el is RectangleElement rect) ? rect.CornerRadius + blurStep / 2 : (el is EllipseElement ? Math.Min(glowRect.Width, glowRect.Height) / 2 : 4);
                dc.DrawRectangle(new SolidColorBrush(glowColor), null, glowRect, radius, radius);
            }
        }

        // 3. Element Body (at FillOpacity if specified)
        if (el.FillOpacity < 1.0)
        {
            using (dc.PushOpacity(Math.Clamp(el.FillOpacity, 0, 1)))
            {
                el.Draw(dc);
            }
        }
        else
        {
            el.Draw(dc);
        }

        // 4. Bevel & Emboss Effect (Highlight & Shadow 3D relief)
        if (el.Effects.BevelEmboss is { IsEnabled: true } bevel)
        {
            double rad = bevel.Angle * Math.PI / 180.0;
            double dx = bevel.Size * Math.Cos(rad) * 0.5;
            double dy = bevel.Size * Math.Sin(rad) * 0.5;

            byte ha = (byte)Math.Clamp((int)(bevel.HighlightOpacity * 255), 0, 255);
            var hColor = Color.FromArgb(ha, bevel.HighlightColor.R, bevel.HighlightColor.G, bevel.HighlightColor.B);
            var hRect = new Rect(el.X - dx, el.Y - dy, el.Width, 2 + bevel.Size * 0.5);
            dc.DrawRectangle(new SolidColorBrush(hColor), null, hRect, cornerRadius, cornerRadius);

            byte sha = (byte)Math.Clamp((int)(bevel.ShadowOpacity * 255), 0, 255);
            var sColor = Color.FromArgb(sha, bevel.ShadowColor.R, bevel.ShadowColor.G, bevel.ShadowColor.B);
            var sRect = new Rect(el.X + dx, el.Y + el.Height - 2 - bevel.Size * 0.5 + dy, el.Width, 2 + bevel.Size * 0.5);
            dc.DrawRectangle(new SolidColorBrush(sColor), null, sRect, cornerRadius, cornerRadius);
        }

        // 5. Color Overlay Effect
        if (el.Effects.ColorOverlay is { IsEnabled: true } overlay)
        {
            byte oa = (byte)Math.Clamp((int)(overlay.Opacity * overlay.Color.A), 0, 255);
            var overlayColor = Color.FromArgb(oa, overlay.Color.R, overlay.Color.G, overlay.Color.B);
            dc.DrawRectangle(new SolidColorBrush(overlayColor), null, bodyRect, cornerRadius, cornerRadius);
        }

        // 6. Gradient Overlay Effect
        if (el.Effects.GradientOverlay is { IsEnabled: true } gradOverlay)
        {
            byte startA = (byte)Math.Clamp((int)(gradOverlay.Opacity * gradOverlay.StartColor.A), 0, 255);
            byte endA = (byte)Math.Clamp((int)(gradOverlay.Opacity * gradOverlay.EndColor.A), 0, 255);
            var c1 = Color.FromArgb(startA, gradOverlay.StartColor.R, gradOverlay.StartColor.G, gradOverlay.StartColor.B);
            var c2 = Color.FromArgb(endA, gradOverlay.EndColor.R, gradOverlay.EndColor.G, gradOverlay.EndColor.B);

            var gradBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0.0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1.0, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(gradOverlay.Reverse ? c2 : c1, 0.0),
                    new GradientStop(gradOverlay.Reverse ? c1 : c2, 1.0)
                }
            };
            dc.DrawRectangle(gradBrush, null, bodyRect, cornerRadius, cornerRadius);
        }

        // 7. Pattern Overlay Effect
        if (el.Effects.PatternOverlay is { IsEnabled: true } patOverlay)
        {
            byte pa = (byte)Math.Clamp((int)(patOverlay.Opacity * 40), 0, 255);
            var patPen = new Pen(new SolidColorBrush(Color.FromArgb(pa, 255, 255, 255)), 1.0);
            double step = Math.Max(4, 16 * (patOverlay.Scale / 100.0));
            for (double px = el.X; px < el.X + el.Width; px += step)
            {
                dc.DrawLine(patPen, new Point(px, el.Y), new Point(px, el.Y + el.Height));
            }
            for (double py = el.Y; py < el.Y + el.Height; py += step)
            {
                dc.DrawLine(patPen, new Point(el.X, py), new Point(el.X + el.Width, py));
            }
        }

        // 8. Satin Effect (Interior Sheen)
        if (el.Effects.Satin is { IsEnabled: true } satin)
        {
            byte sa = (byte)Math.Clamp((int)(satin.Opacity * 120), 0, 255);
            var satinBrush = new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.7, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.7, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(0, satin.Color.R, satin.Color.G, satin.Color.B), 0.0),
                    new GradientStop(Color.FromArgb(sa, satin.Color.R, satin.Color.G, satin.Color.B), 0.6),
                    new GradientStop(Color.FromArgb(0, satin.Color.R, satin.Color.G, satin.Color.B), 1.0)
                }
            };
            dc.DrawRectangle(satinBrush, null, bodyRect, cornerRadius, cornerRadius);
        }

        // 9. Inner Glow Effect
        if (el.Effects.InnerGlow is { IsEnabled: true } innerGlow)
        {
            byte iga = (byte)Math.Clamp((int)(innerGlow.Opacity * innerGlow.Color.A), 0, 255);
            int igPasses = Math.Max(2, (int)(innerGlow.Size / 2));
            for (int p = 1; p <= igPasses; p++)
            {
                double inset = p * (innerGlow.Size / igPasses);
                byte passAlpha = (byte)Math.Max(1, iga / (igPasses * 1.5));
                var igColor = Color.FromArgb(passAlpha, innerGlow.Color.R, innerGlow.Color.G, innerGlow.Color.B);
                var igRect = new Rect(el.X + inset / 2, el.Y + inset / 2, Math.Max(0, el.Width - inset), Math.Max(0, el.Height - inset));
                var igPen = new Pen(new SolidColorBrush(igColor), 1.5);
                dc.DrawRectangle(null, igPen, igRect, cornerRadius, cornerRadius);
            }
        }

        // 10. Inner Shadow Effect
        if (el.Effects.InnerShadow is { IsEnabled: true } innerShadow)
        {
            double rad = innerShadow.Angle * Math.PI / 180.0;
            double dx = innerShadow.Distance * Math.Cos(rad);
            double dy = innerShadow.Distance * Math.Sin(rad);
            byte isa = (byte)Math.Clamp((int)(innerShadow.Opacity * innerShadow.Color.A), 0, 255);

            var isColor = Color.FromArgb(isa, innerShadow.Color.R, innerShadow.Color.G, innerShadow.Color.B);
            var isRect = new Rect(el.X + Math.Max(0, dx), el.Y + Math.Max(0, dy), Math.Max(0, el.Width - Math.Abs(dx)), 2 + innerShadow.Size);
            dc.DrawRectangle(new SolidColorBrush(isColor), null, isRect, cornerRadius, cornerRadius);
        }

        // 11. Layer Stroke Effect
        if (el.Effects.Stroke is { IsEnabled: true, Size: > 0 } stroke)
        {
            byte sta = (byte)Math.Clamp((int)(stroke.Opacity * stroke.Color.A), 0, 255);
            var strokeColor = Color.FromArgb(sta, stroke.Color.R, stroke.Color.G, stroke.Color.B);
            var pen = new Pen(new SolidColorBrush(strokeColor), stroke.Size);
            var strokeRect = stroke.Position switch
            {
                LayerStrokePosition.Inside => new Rect(el.X + stroke.Size / 2, el.Y + stroke.Size / 2, Math.Max(0, el.Width - stroke.Size), Math.Max(0, el.Height - stroke.Size)),
                LayerStrokePosition.Outside => new Rect(el.X - stroke.Size / 2, el.Y - stroke.Size / 2, el.Width + stroke.Size, el.Height + stroke.Size),
                _ => new Rect(el.X, el.Y, el.Width, el.Height)
            };
            double radius = (el is RectangleElement rect) ? rect.CornerRadius : (el is EllipseElement ? Math.Min(strokeRect.Width, strokeRect.Height) / 2 : 0);
            dc.DrawRectangle(null, pen, strokeRect, radius, radius);
        }
    }

    // ── Background/grid tile caches (regenerated only when the logical canvas size changes,
    // never per-frame) ───────────────────────────────────────────────────────────────────
    private RenderTargetBitmap? _cachedGridTile;
    private double _cachedGridWidth = -1, _cachedGridHeight = -1;
    private RenderTargetBitmap? _cachedCheckerTile;
    private double _cachedCheckerWidth = -1, _cachedCheckerHeight = -1;

    private RenderTargetBitmap GetGridTile(double width, double height)
    {
        if (_cachedGridTile == null || _cachedGridWidth != width || _cachedGridHeight != height)
        {
            _cachedGridTile?.Dispose();
            var pixelSize = new PixelSize(Math.Max(1, (int)Math.Ceiling(width)), Math.Max(1, (int)Math.Ceiling(height)));
            _cachedGridTile = new RenderTargetBitmap(pixelSize);
            using (var dc = _cachedGridTile.CreateDrawingContext())
            {
                DrawGrid(dc, new Rect(0, 0, width, height));
            }
            _cachedGridWidth = width;
            _cachedGridHeight = height;
        }
        return _cachedGridTile;
    }

    private RenderTargetBitmap GetCheckerboardTile(double width, double height)
    {
        if (_cachedCheckerTile == null || _cachedCheckerWidth != width || _cachedCheckerHeight != height)
        {
            _cachedCheckerTile?.Dispose();
            var pixelSize = new PixelSize(Math.Max(1, (int)Math.Ceiling(width)), Math.Max(1, (int)Math.Ceiling(height)));
            _cachedCheckerTile = new RenderTargetBitmap(pixelSize);
            using (var dc = _cachedCheckerTile.CreateDrawingContext())
            {
                DrawCheckerboardBackground(dc, new Rect(0, 0, width, height));
            }
            _cachedCheckerWidth = width;
            _cachedCheckerHeight = height;
        }
        return _cachedCheckerTile;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cachedGridTile?.Dispose();
        _cachedGridTile = null;
        _cachedGridWidth = -1;
        _cachedGridHeight = -1;

        _cachedCheckerTile?.Dispose();
        _cachedCheckerTile = null;
        _cachedCheckerWidth = -1;
        _cachedCheckerHeight = -1;

        _nudgeCommitTimer.Stop();
    }

    private static void DrawCheckerboardBackground(DrawingContext dc, Rect bounds)
    {
        const double cell = 16;
        var dark = new SolidColorBrush(Color.FromRgb(0x2A, 0x2E, 0x35));
        var light = new SolidColorBrush(Color.FromRgb(0x3A, 0x3F, 0x47));
        dc.DrawRectangle(dark, null, bounds);

        int cols = (int)Math.Ceiling(bounds.Width / cell);
        int rows = (int)Math.Ceiling(bounds.Height / cell);
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if ((row + col) % 2 != 0) continue;
                double x = bounds.X + col * cell;
                double y = bounds.Y + row * cell;
                double w = Math.Min(cell, bounds.Right - x);
                double h = Math.Min(cell, bounds.Bottom - y);
                if (w <= 0 || h <= 0) continue;
                dc.DrawRectangle(light, null, new Rect(x, y, w, h));
            }
        }
    }

    private static void DrawGrid(DrawingContext dc, Rect bounds)
    {
        var dotBrush = new SolidColorBrush(Color.FromArgb(30, 150, 150, 150));
        const double spacing = 20;
        for (double x = spacing; x < bounds.Width; x += spacing)
        {
            for (double y = spacing; y < bounds.Height; y += spacing)
            {
                dc.DrawEllipse(dotBrush, null, new Point(x, y), 1, 1);
            }
        }
    }

    private static void DrawSelectionHandles(DrawingContext dc, CanvasElement el, double zoom)
    {
        var bounds = el.Bounds;
        IDisposable? rotScope = null;
        if (el.RotationDegrees != 0)
        {
            rotScope = dc.PushTransform(Matrix.CreateRotation(el.RotationDegrees * Math.PI / 180.0, bounds.Center));
        }

        try
        {
            // Dashed selection border
            var borderPen = new Pen(
                new SolidColorBrush(Color.FromArgb(220, 0, 120, 212)),
                1.5 / zoom,
                new DashStyle(new double[] { 5, 3 }, 0));
            dc.DrawRectangle(null, borderPen, bounds.Inflate(2 / zoom));

            DrawResizeHandles(dc, bounds, zoom);
        }
        finally
        {
            rotScope?.Dispose();
        }
    }

    /// <summary>8 resize handles around a bounds rect — shared by single-element selection
    /// and (with a group's bounding box) group-resize.</summary>
    private static void DrawResizeHandles(DrawingContext dc, Rect bounds, double zoom)
    {
        double handleSize = HandleSize / zoom;
        double halfHandle = handleSize / 2;

        var handleFill = new SolidColorBrush(Colors.White);
        var handlePen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 120, 212)), 1.5 / zoom);

        foreach (var pt in GetHandlePoints(bounds))
        {
            dc.DrawRectangle(handleFill, handlePen,
                new Rect(pt.X - halfHandle, pt.Y - halfHandle, handleSize, handleSize),
                2 / zoom, 2 / zoom);
        }
    }

    private static void DrawMultiSelectionOutline(DrawingContext dc, IReadOnlyList<CanvasElement> selection, double zoom)
    {
        var memberPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 120, 212)), 1 / zoom,
            new DashStyle(new double[] { 4, 3 }, 0));
        foreach (var el in selection)
        {
            dc.DrawRectangle(null, memberPen, el.GetRotatedBounds().Inflate(1 / zoom));
        }

        var groupPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 0, 120, 212)), 1.5 / zoom,
            new DashStyle(new double[] { 6, 3 }, 0));
        dc.DrawRectangle(null, groupPen, ComputeGroupBounds(selection).Inflate(4 / zoom));
    }

    /// <summary>Rotation handle sits a fixed screen-space distance above the (rotated, for a
    /// single element) top-center point. rotationDegrees is 0 for a group handle, since the
    /// synthetic group bounding box never itself carries a rotation.</summary>
    private static void DrawRotationHandle(DrawingContext dc, Rect axisBounds, double rotationDegrees, double zoom)
    {
        var stemStart = GetRotationHandleStemStart(axisBounds, rotationDegrees);
        var handlePt = GetRotationHandlePoint(axisBounds, rotationDegrees, zoom);
        var stemPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 120, 212)), 1.5 / zoom);
        dc.DrawLine(stemPen, stemStart, handlePt);
        double r = 5.5 / zoom;
        dc.DrawEllipse(new SolidColorBrush(Colors.White), stemPen, handlePt, r, r);
    }

    private static Point GetRotationHandleStemStart(Rect axisBounds, double rotationDegrees)
    {
        var localTop = new Point(axisBounds.X + axisBounds.Width / 2, axisBounds.Y);
        if (rotationDegrees == 0) return localTop;
        return localTop.Transform(Matrix.CreateRotation(rotationDegrees * Math.PI / 180.0, axisBounds.Center));
    }

    private static Point GetRotationHandlePoint(Rect axisBounds, double rotationDegrees, double zoom)
    {
        double offset = 28 / zoom;
        var localHandle = new Point(axisBounds.X + axisBounds.Width / 2, axisBounds.Y - offset);
        if (rotationDegrees == 0) return localHandle;
        return localHandle.Transform(Matrix.CreateRotation(rotationDegrees * Math.PI / 180.0, axisBounds.Center));
    }

    private static void DrawUserGuides(DrawingContext dc, List<(bool IsVertical, double Position, Color? Color)> guides, double zoom)
    {
        const double span = 10000;
        var defaultBrush = new SolidColorBrush(Color.FromArgb(240, 0, 229, 255));
        foreach (var (isVertical, pos, color) in guides)
        {
            var brush = color.HasValue ? new SolidColorBrush(color.Value) : defaultBrush;
            var guidePen = new Pen(brush, 1.0 / zoom);
            if (isVertical) dc.DrawLine(guidePen, new Point(pos, -span), new Point(pos, span));
            else dc.DrawLine(guidePen, new Point(-span, pos), new Point(span, pos));
        }
    }

    private static void DrawAlignmentGuides(DrawingContext dc, List<(bool IsVertical, double Position)> guides, double zoom)
    {
        const double span = 4000;
        var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 236, 72, 153)), 1 / zoom,
            new DashStyle(new double[] { 4, 3 }, 0));
        foreach (var (isVertical, pos) in guides)
        {
            if (isVertical) dc.DrawLine(guidePen, new Point(pos, -span), new Point(pos, span));
            else dc.DrawLine(guidePen, new Point(-span, pos), new Point(span, pos));
        }
    }

    private static Rect ComputeGroupBounds(IReadOnlyList<CanvasElement> selection)
    {
        var result = selection[0].GetRotatedBounds();
        for (int i = 1; i < selection.Count; i++)
        {
            result = result.Union(selection[i].GetRotatedBounds());
        }
        return result;
    }

    private static void DrawMarquee(DrawingContext dc, Point start, Point end, double zoom)
    {
        var rect = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
                             Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        if (rect.Width < 1 && rect.Height < 1) return;

        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)), null, rect);

        var blackPen = new Pen(new SolidColorBrush(Colors.Black), 1.0 / zoom, new DashStyle(new double[] { 4, 4 }, 0));
        var whitePen = new Pen(new SolidColorBrush(Colors.White), 1.0 / zoom, new DashStyle(new double[] { 4, 4 }, 4));
        dc.DrawRectangle(null, blackPen, rect);
        dc.DrawRectangle(null, whitePen, rect);
    }

    private static void DrawShapePlacementPreview(DrawingContext dc, string toolType, Point start, Point end, double zoom)
    {
        var rect = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
                             Math.Max(Math.Abs(end.X - start.X), 2), Math.Max(Math.Abs(end.Y - start.Y), 2));
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(220, 0, 120, 212)), 1.5 / zoom,
            new DashStyle(new double[] { 4, 3 }, 0));
        var fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 212));

        switch (toolType)
        {
            case "ellipse":
                dc.DrawEllipse(fill, pen, rect.Center, rect.Width / 2, rect.Height / 2);
                break;
            case "arrow":
                dc.DrawLine(pen, start, end);
                break;
            case "line":
                dc.DrawLine(pen, start, end);
                break;
            case "triangle":
                var triPoints = new Point[]
                {
                    new(rect.Center.X, rect.Y),
                    new(rect.Right, rect.Bottom),
                    new(rect.Left, rect.Bottom)
                };
                dc.DrawGeometry(fill, pen, PolygonGeometry.BuildClosedFigure(triPoints));
                break;
            case "star":
                var starPoints = ComputePreviewStarVertices(rect, 5, 0.5);
                dc.DrawGeometry(fill, pen, PolygonGeometry.BuildClosedFigure(starPoints));
                break;
            case "polygon":
                var polyPoints = ComputePreviewPolygonVertices(rect, 6);
                dc.DrawGeometry(fill, pen, PolygonGeometry.BuildClosedFigure(polyPoints));
                break;
            default:
                dc.DrawRectangle(fill, pen, rect);
                break;
        }
    }

    private static Point[] ComputePreviewPolygonVertices(Rect bounds, int n)
    {
        var points = new Point[n];
        double cx = bounds.Center.X, cy = bounds.Center.Y;
        double rx = bounds.Width / 2, ry = bounds.Height / 2;
        const double startAngle = -Math.PI / 2;
        for (int i = 0; i < n; i++)
        {
            double angle = startAngle + i * (2 * Math.PI / n);
            points[i] = new Point(cx + rx * Math.Cos(angle), cy + ry * Math.Sin(angle));
        }
        return points;
    }

    private static Point[] ComputePreviewStarVertices(Rect bounds, int n, double innerRatio)
    {
        var points = new Point[n * 2];
        double cx = bounds.Center.X, cy = bounds.Center.Y;
        double outerRx = bounds.Width / 2, outerRy = bounds.Height / 2;
        double innerRx = outerRx * innerRatio, innerRy = outerRy * innerRatio;
        double step = Math.PI / n;
        const double startAngle = -Math.PI / 2;
        for (int i = 0; i < n * 2; i++)
        {
            double angle = startAngle + i * step;
            bool isOuter = i % 2 == 0;
            double rx = isOuter ? outerRx : innerRx;
            double ry = isOuter ? outerRy : innerRy;
            points[i] = new Point(cx + rx * Math.Cos(angle), cy + ry * Math.Sin(angle));
        }
        return points;
    }

    private void DrawCropOverlay(DrawingContext dc, ImageElement img, double zoom)
    {
        var frame = img.Bounds;

        // Dark mask over the whole frame, then re-reveal the pending crop region clipped
        // and at full brightness.
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, frame);
        using (dc.PushClip(_cropPreviewRect))
        {
            DrawElement(dc, img);
        }

        // Rule of thirds grid lines inside crop preview (Studio standard)
        if (_cropPreviewRect.Width > 12 && _cropPreviewRect.Height > 12)
        {
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1.0 / zoom);
            double w3 = _cropPreviewRect.Width / 3.0;
            double h3 = _cropPreviewRect.Height / 3.0;
            dc.DrawLine(gridPen, new Point(_cropPreviewRect.X + w3, _cropPreviewRect.Y), new Point(_cropPreviewRect.X + w3, _cropPreviewRect.Bottom));
            dc.DrawLine(gridPen, new Point(_cropPreviewRect.X + 2 * w3, _cropPreviewRect.Y), new Point(_cropPreviewRect.X + 2 * w3, _cropPreviewRect.Bottom));
            dc.DrawLine(gridPen, new Point(_cropPreviewRect.X, _cropPreviewRect.Y + h3), new Point(_cropPreviewRect.Right, _cropPreviewRect.Y + h3));
            dc.DrawLine(gridPen, new Point(_cropPreviewRect.X, _cropPreviewRect.Y + 2 * h3), new Point(_cropPreviewRect.Right, _cropPreviewRect.Y + 2 * h3));
        }

        double handleSize = HandleSize / zoom;
        double halfHandle = handleSize / 2;
        var accentPen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 120, 212)), 1.5 / zoom);
        dc.DrawRectangle(null, accentPen, _cropPreviewRect);

        var handleFill = new SolidColorBrush(Colors.White);
        foreach (var pt in GetHandlePoints(_cropPreviewRect))
        {
            dc.DrawRectangle(handleFill, accentPen,
                new Rect(pt.X - halfHandle, pt.Y - halfHandle, handleSize, handleSize), 2 / zoom, 2 / zoom);
        }
    }

    // TL, T, TR, R, BR, B, BL, L
    private static Point[] GetHandlePoints(Rect r)
    {
        double cx = r.X + r.Width / 2;
        double cy = r.Y + r.Height / 2;
        return
        [
            new Point(r.X,            r.Y),
            new Point(cx,             r.Y),
            new Point(r.Right,        r.Y),
            new Point(r.Right,        cy),
            new Point(r.Right,        r.Bottom),
            new Point(cx,             r.Bottom),
            new Point(r.X,            r.Bottom),
            new Point(r.X,            cy),
        ];
    }

    // ── Pointer Events ─────────────────────────────────────────────────────
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var props = e.GetCurrentPoint(this).Properties;

        // 1. Panning pre-empts every other interaction.
        if (props.IsMiddleButtonPressed || _spaceHeld || ActiveToolMode == "hand")
        {
            _isPanning = true;
            _panLastScreenPos = e.GetPosition(this);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (!props.IsLeftButtonPressed && ActiveToolMode != "zoom") return;

        var canvasPt = ScreenToCanvas(e.GetPosition(this));
        _dragStart = canvasPt;

        // 2. Crop mode is modal.
        if (_isCropModeActive)
        {
            HandleCropPointerPressed(canvasPt);
            e.Handled = true;
            return;
        }

        // Zoom tool
        if (ActiveToolMode == "zoom")
        {
            var screenPos = e.GetPosition(this);
            bool zoomOut = e.KeyModifiers.HasFlag(KeyModifiers.Alt) || props.IsRightButtonPressed;
            double factor = zoomOut ? (1.0 / 1.35) : 1.35;
            ZoomAtPoint(_zoom * factor, screenPos);
            e.Handled = true;
            return;
        }

        // Marquee tool
        if (ActiveToolMode == "marquee")
        {
            _isMarqueeSelecting = true;
            _marqueeStart = canvasPt;
            _marqueeEnd = canvasPt;
            e.Pointer.Capture(this);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        // Lasso tools (Standard, Polygonal, Magnetic)
        if (ActiveToolMode == "lasso")
        {
            HandleLassoPointerPressed(canvasPt, e);
            return;
        }

        // Magic Wand tool
        if (ActiveToolMode == "wand")
        {
            HandleMagicWandPointerPressed(canvasPt, e);
            return;
        }

        // Rotate View tool
        if (ActiveToolMode == "rotateview")
        {
            if (e.ClickCount == 2)
            {
                ViewportRotationAngle = 0.0;
                e.Handled = true;
                InvalidateVisual();
                return;
            }
            _isRotatingView = true;
            _rotateViewStartPointer = e.GetPosition(this);
            _rotateViewStartAngle = ViewportRotationAngle;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // Ruler tool
        if (ActiveToolMode == "ruler")
        {
            _isRulerMeasuring = true;
            _rulerStart = canvasPt;
            _rulerEnd = canvasPt;
            RulerMeasurementChanged?.Invoke(_rulerStart, _rulerEnd);
            e.Pointer.Capture(this);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        // 3. Tool-placement mode.
        if (ActiveToolMode != "select")
        {
            if (ActiveToolMode is "brush" or "pencil")
            {
                if (IsQuickMaskMode && QuickMaskBuffer != null)
                {
                    _isDrawingQuickMask = true;
                    ApplyQuickMaskDab(canvasPt, BrushThickness, BrushColor);
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }
                _isDrawingDoodle = true;
                _smoothedBrushPos = canvasPt;
                _currentPointerCanvasPos = canvasPt;
                _hasSmoothingLeash = false;
                _currentDoodlePoints.Clear();
                _currentDoodlePoints.Add(canvasPt);
                e.Pointer.Capture(this);
                e.Handled = true;
                InvalidateVisual();
                return;
            }
            if (ActiveToolMode == "eraser" && IsQuickMaskMode && QuickMaskBuffer != null)
            {
                _isDrawingQuickMask = true;
                ApplyQuickMaskDab(canvasPt, BrushThickness, Colors.White);
                e.Pointer.Capture(this);
                e.Handled = true;
                InvalidateVisual();
                return;
            }
            if (ActiveToolMode is "remove" or "clonestamp" or "healing" or "historybrush" or "blur" or "sharpen" or "smudge" or "dodge" or "burn" or "sponge" or "bgeraser" or "colorreplacement" or "redeye" or "patch")
            {
                if ((ActiveToolMode is "clonestamp" or "healing") && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    CloneSourcePoint = canvasPt;
                    CloneSourceDefined?.Invoke(canvasPt);
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }

                string effectiveTool = ActiveToolMode switch
                {
                    "dodge" when e.KeyModifiers.HasFlag(KeyModifiers.Alt) => "burn",
                    "burn" when e.KeyModifiers.HasFlag(KeyModifiers.Alt) => "dodge",
                    "blur" when e.KeyModifiers.HasFlag(KeyModifiers.Alt) => "sharpen",
                    "sharpen" when e.KeyModifiers.HasFlag(KeyModifiers.Alt) => "blur",
                    _ => ActiveToolMode
                };

                _currentRetouchTool = effectiveTool;
                _isDrawingRetouch = true;
                _currentRetouchPoints.Clear();
                _currentRetouchPoints.Add(canvasPt);
                e.Pointer.Capture(this);
                e.Handled = true;
                InvalidateVisual();
                return;
            }
            if (ActiveToolMode == "pen")
            {
                ActiveDocumentPath ??= new VectorDocumentPath("Work Path");
                if (ActiveDocumentPath.Subpaths.Count == 0 || ActiveDocumentPath.Subpaths[^1].IsClosed)
                {
                    ActiveDocumentPath.Subpaths.Add(new VectorSubpath());
                }

                var currentSub = ActiveDocumentPath.Subpaths[^1];
                double closeHitRadius = 10.0 / _zoom;

                if (currentSub.Knots.Count > 2 && Point.Distance(canvasPt, currentSub.Knots[0].Anchor) <= closeHitRadius)
                {
                    currentSub.IsClosed = true;
                    _isDraggingPenHandle = false;
                    _penActiveKnot = null;
                }
                else
                {
                    var newKnot = new PathKnot(canvasPt);
                    currentSub.Knots.Add(newKnot);
                    _penActiveKnot = newKnot;
                    _isDraggingPenHandle = true;
                }

                e.Pointer.Capture(this);
                e.Handled = true;
                InvalidateVisual();
                return;
            }
            if (ActiveToolMode == "pathselection")
            {
                if (ActiveDocumentPath != null)
                {
                    double hitRadius = 8.0 / _zoom;
                    _selectedPathKnot = null;
                    foreach (var sub in ActiveDocumentPath.Subpaths)
                    {
                        foreach (var knot in sub.Knots)
                        {
                            if (Point.Distance(canvasPt, knot.Anchor) <= hitRadius)
                            {
                                _selectedPathKnot = knot;
                                _isDraggingPathKnot = true;
                                break;
                            }
                        }
                        if (_selectedPathKnot != null) break;
                    }

                    if (_selectedPathKnot != null)
                    {
                        e.Pointer.Capture(this);
                        e.Handled = true;
                        InvalidateVisual();
                        return;
                    }
                }
            }
            if (ActiveToolMode == "eraser")
            {
                var erasedElement = _elements.OrderByDescending(el => el.ZIndex)
                    .FirstOrDefault(el => !el.IsLocked && el.IsVisible && el.HitTestRotationAware(canvasPt));
                if (erasedElement != null)
                {
                    ElementEraseRequested?.Invoke(erasedElement);
                }
                e.Handled = true;
                return;
            }
            if (ActiveToolMode == "text")
            {
                bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                var hitText = !isShift
                    ? _elements.OfType<TextElement>()
                        .OrderByDescending(el => el.ZIndex)
                        .FirstOrDefault(el => el.IsVisible && !el.IsLocked && el.HitTestRotationAware(canvasPt))
                    : null;

                if (hitText != null)
                {
                    _selection.Clear();
                    _selection.Add(hitText);
                    NotifySelectionChanged();
                    TextEditRequested?.Invoke(hitText);
                }
                else
                {
                    PlaceTextRequested?.Invoke(canvasPt);
                }
            }
            else if (ActiveToolMode is "rect" or "ellipse" or "arrow" or "triangle" or "star" or "polygon" or "line")
            {
                _isPlacingShape = true;
                _shapeDragStart = canvasPt;
                _shapeDragEnd = canvasPt;
                e.Pointer.Capture(this);
            }
            else if (ActiveToolMode == "eyedropper")
            {
                var sampled = SampleColorAtCanvasPoint(canvasPt);
                if (sampled is { } color) ColorSampled?.Invoke(color);
            }
            e.Handled = true;
            return;
        }

        bool shiftHeld = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _dragOccurred = false;

        // Interactive user guide drag (Move Tool)
        if (ShowUserGuides && !LockUserGuides && TryStartGuideDrag(e.GetPosition(this), e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // 4. Resize handle of the current single-element selection.
        if (_selection.Count == 1 && !_selection[0].IsLocked)
        {
            var single = _selection[0];
            double handleSize = HandleSize / _zoom;
            double halfHandle = handleSize / 2;
            double pad = 4 / _zoom;

            Point localPt = canvasPt;
            if (single.RotationDegrees != 0)
            {
                localPt = canvasPt.Transform(Matrix.CreateRotation(-single.RotationDegrees * Math.PI / 180.0, single.Bounds.Center));
            }

            var handles = GetHandlePoints(single.Bounds);
            for (int i = 0; i < handles.Length; i++)
            {
                var hr = new Rect(handles[i].X - halfHandle - pad, handles[i].Y - halfHandle - pad,
                                  handleSize + pad * 2, handleSize + pad * 2);
                if (hr.Contains(localPt))
                {
                    _isResizing = true;
                    _resizeHandle = i;
                    _origX = single.X;
                    _origY = single.Y;
                    _origW = single.Width;
                    _origH = single.Height;
                    // Manual resize takes precedence over auto-fit-to-text from now on,
                    // otherwise the very next Draw() would snap the size back. Only the axes
                    // actually affected by this handle stop auto-fitting (a pure T/B drag
                    // shouldn't block width from still auto-fitting, and vice versa).
                    if (single is TextElement textEl)
                    {
                        // Handle indices: 0=TL 1=T 2=TR 3=R 4=BR 5=B 6=BL 7=L
                        bool affectsWidth = i is not (1 or 5);  // all except pure T/B
                        bool affectsHeight = i is not (3 or 7); // all except pure L/R
                        if (affectsWidth) textEl.IsWidthAutoFitEnabled = false;
                        if (affectsHeight) textEl.IsHeightAutoFitEnabled = false;
                    }
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
            }
        }

        // 5. Group-resize handle (2+ selected, all unlocked).
        if (_selection.Count > 1 && _selection.All(s => !s.IsLocked))
        {
            var groupBounds = ComputeGroupBounds(_selection);
            double handleSize = HandleSize / _zoom;
            double halfHandle = handleSize / 2;
            double pad = 4 / _zoom;
            var handles = GetHandlePoints(groupBounds);
            for (int i = 0; i < handles.Length; i++)
            {
                var hr = new Rect(handles[i].X - halfHandle - pad, handles[i].Y - halfHandle - pad,
                                  handleSize + pad * 2, handleSize + pad * 2);
                if (hr.Contains(canvasPt))
                {
                    _isGroupResizing = true;
                    _groupResizeHandle = i;
                    _groupResizeOrigBounds = groupBounds;
                    _groupResizeOrigins = _selection.ToDictionary(el => el, el => new Rect(el.X, el.Y, el.Width, el.Height));
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
            }
        }

        // 6. Rotation handle (single or multi selection, all unlocked).
        if (_selection.Count >= 1 && _selection.All(s => !s.IsLocked))
        {
            Rect axisBounds; double currentRotation;
            if (_selection.Count == 1) { axisBounds = _selection[0].Bounds; currentRotation = _selection[0].RotationDegrees; }
            else { axisBounds = ComputeGroupBounds(_selection); currentRotation = 0; }

            var handlePt = GetRotationHandlePoint(axisBounds, currentRotation, _zoom);
            double hitRadius = 10 / _zoom;
            if (Point.Distance(handlePt, canvasPt) <= hitRadius)
            {
                _isRotating = true;
                _rotationPivot = axisBounds.Center;
                _rotationStartAngle = Math.Atan2(canvasPt.Y - _rotationPivot.Y, canvasPt.X - _rotationPivot.X);
                _rotationOrigins = _selection.ToDictionary(el => el, el => (el.X, el.Y, el.RotationDegrees));
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
        }

        // 7. Double-click on an element while the Select tool is active.
        if (e.ClickCount >= 2)
        {
            var doubleClickHit = _elements.OrderByDescending(el => el.ZIndex)
                .FirstOrDefault(el => el.IsVisible && !el.IsLocked && el.HitTestRotationAware(canvasPt));
            if (doubleClickHit is TextElement textHit)
            {
                _selection.Clear();
                _selection.Add(textHit);
                NotifySelectionChanged();
                TextEditRequested?.Invoke(textHit);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else if (doubleClickHit != null)
            {
                _selection.Clear();
                _selection.Add(doubleClickHit);
                NotifySelectionChanged();
                ElementDoubleClicked?.Invoke(doubleClickHit);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        // 8. Hit-test elements (top-most Z first), respecting AutoSelectLayers setting
        CanvasElement? hit;
        if (AutoSelectLayers || _selection.Count == 0)
        {
            hit = _elements.OrderByDescending(el => el.ZIndex)
                .FirstOrDefault(el => el.IsVisible && !el.IsLocked && el.HitTestRotationAware(canvasPt));
        }
        else
        {
            hit = _selection.FirstOrDefault(el => el.HitTestRotationAware(canvasPt));
        }

        if (hit != null)
        {
            if (shiftHeld)
            {
                if (_selection.Contains(hit)) _selection.Remove(hit);
                else _selection.Add(hit);
            }
            else if (!_selection.Contains(hit))
            {
                _selection.Clear();
                _selection.Add(hit);
            }
            // else: hit is already part of a multi-selection and shift isn't held — keep the
            // whole selection intact so drag-to-move-group works; narrowed on release only
            // if no actual drag occurs (see OnPointerReleased).

            NotifySelectionChanged();

            if (_selection.Count > 0)
            {
                _isDragging = true;
                _pressedElement = hit;
                _groupDragOrigins = _selection.ToDictionary(el => el, el => new Point(el.X, el.Y));
                e.Pointer.Capture(this);
            }
        }
        else
        {
            // Empty-canvas click: begin marquee selection.
            if (!shiftHeld) _selection.Clear();
            _isMarqueeSelecting = true;
            _marqueeStart = canvasPt;
            _marqueeEnd = canvasPt;
            e.Pointer.Capture(this);
            NotifySelectionChanged();
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            CanvasChanged?.Invoke();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        CursorPositionChanged?.Invoke(new Point(-1, -1));
        _cursorCanvasPos = null;
        if (ActiveToolMode is "brush" or "pencil" or "eraser")
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isPanning)
        {
            var currentScreenPos = e.GetPosition(this);
            var delta = currentScreenPos - _panLastScreenPos;
            _panOffset = new Point(_panOffset.X + delta.X, _panOffset.Y + delta.Y);
            _panLastScreenPos = currentScreenPos;
            ClampPanOffset(); // same unbounded-pan gap as the wheel handler — see its comment
            CursorPositionChanged?.Invoke(ScreenToCanvas(currentScreenPos));
            CanvasChanged?.Invoke();
            InvalidateVisual();
            return;
        }

        var pos = ScreenToCanvas(e.GetPosition(this));
        CursorPositionChanged?.Invoke(pos);
        _cursorCanvasPos = pos;

        if (_isDraggingExistingGuide || _activeDragGuide.HasValue)
        {
            UpdateGuideDragPointer(e.GetPosition(this));
            e.Handled = true;
            return;
        }

        if (ActiveToolMode == "select" && !_isDragging && !_isResizing && !_isRotating)
        {
            if (UpdateTransformHoverCursor(pos, e.GetPosition(this)))
            {
                // Handled by resize or rotation handle
            }
            else if (ShowUserGuides && !LockUserGuides)
            {
                UpdateGuideHoverCursor(e.GetPosition(this));
            }
            else
            {
                Cursor = Cursor.Default;
            }
        }

        if ((ActiveToolMode is "brush" or "pencil" or "eraser" or "clonestamp" or "healing" or "historybrush" or "blur" or "sharpen" or "smudge" or "dodge" or "burn" or "sponge" or "bgeraser" or "colorreplacement" or "redeye" or "patch" or "eyedropper") && !_isDrawingDoodle && !_isDrawingRetouch)
        {
            InvalidateVisual();
        }

        if (_isRotatingView)
        {
            Point pivot = new Point(Bounds.Width / 2 + _panOffset.X, Bounds.Height / 2 + _panOffset.Y);
            Point curr = e.GetPosition(this);
            double startAngle = Math.Atan2(_rotateViewStartPointer.Y - pivot.Y, _rotateViewStartPointer.X - pivot.X) * 180.0 / Math.PI;
            double currAngle = Math.Atan2(curr.Y - pivot.Y, curr.X - pivot.X) * 180.0 / Math.PI;
            double delta = currAngle - startAngle;
            double newAngle = _rotateViewStartAngle + delta;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                newAngle = Math.Round(newAngle / 15.0) * 15.0;
            }
            while (newAngle > 180.0) newAngle -= 360.0;
            while (newAngle <= -180.0) newAngle += 360.0;
            ViewportRotationAngle = Math.Round(newAngle, 1);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        if (_isRulerMeasuring)
        {
            _rulerEnd = pos;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                double rDx = _rulerEnd.X - _rulerStart.X;
                double rDy = _rulerEnd.Y - _rulerStart.Y;
                double angle = Math.Atan2(rDy, rDx) * 180.0 / Math.PI;
                double snapped = Math.Round(angle / 45.0) * 45.0 * Math.PI / 180.0;
                double dist = Math.Sqrt(rDx * rDx + rDy * rDy);
                _rulerEnd = new Point(_rulerStart.X + dist * Math.Cos(snapped), _rulerStart.Y + dist * Math.Sin(snapped));
            }
            RulerMeasurementChanged?.Invoke(_rulerStart, _rulerEnd);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        if (ActiveToolMode == "eyedropper" && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var sampled = SampleColorAtCanvasPoint(pos);
            if (sampled is { } color) ColorSampled?.Invoke(color);
        }

        if (_isCropDragging)
        {
            HandleCropPointerMoved(pos);
            InvalidateVisual();
            return;
        }

        if (_isDrawingQuickMask && QuickMaskBuffer != null)
        {
            Color dabColor = (ActiveToolMode == "eraser") ? Colors.White : BrushColor;
            ApplyQuickMaskDab(pos, BrushThickness, dabColor);
            InvalidateVisual();
            return;
        }

        if (_isDrawingDoodle)
        {
            _currentPointerCanvasPos = pos;
            if (BrushSmoothing > 0)
            {
                double leashRadius = (BrushSmoothing / 100.0) * 35.0 / _zoom;
                double dist = Point.Distance(_smoothedBrushPos, pos);
                if (dist > leashRadius)
                {
                    double angle = Math.Atan2(pos.Y - _smoothedBrushPos.Y, pos.X - _smoothedBrushPos.X);
                    double pullX = pos.X - Math.Cos(angle) * leashRadius;
                    double pullY = pos.Y - Math.Sin(angle) * leashRadius;
                    double smoothRate = Math.Clamp(1.0 - (BrushSmoothing / 100.0) * 0.6, 0.15, 1.0);
                    _smoothedBrushPos = new Point(
                        _smoothedBrushPos.X + (pullX - _smoothedBrushPos.X) * smoothRate,
                        _smoothedBrushPos.Y + (pullY - _smoothedBrushPos.Y) * smoothRate);

                    if (_currentDoodlePoints.Count == 0 || Point.Distance(_currentDoodlePoints[^1], _smoothedBrushPos) >= 1.5 / _zoom)
                    {
                        _currentDoodlePoints.Add(_smoothedBrushPos);
                    }
                    _hasSmoothingLeash = dist >= 2.0 / _zoom;
                }
                else
                {
                    _hasSmoothingLeash = dist >= 2.0 / _zoom;
                }
                InvalidateVisual();
            }
            else
            {
                _smoothedBrushPos = pos;
                _hasSmoothingLeash = false;
                if (_currentDoodlePoints.Count == 0 || Point.Distance(_currentDoodlePoints[^1], pos) >= 1.5 / _zoom)
                {
                    _currentDoodlePoints.Add(pos);
                    InvalidateVisual();
                }
            }
            return;
        }

        if (_isDrawingRetouch)
        {
            if (_currentRetouchPoints.Count == 0 || Point.Distance(_currentRetouchPoints[^1], pos) >= 1.5 / _zoom)
            {
                _currentRetouchPoints.Add(pos);
                InvalidateVisual();
            }
            return;
        }

        if (_isDraggingPenHandle && _penActiveKnot != null)
        {
            _penActiveKnot.SetHandleOutMirrored(pos);
            InvalidateVisual();
            return;
        }

        if (_isDraggingPathKnot && _selectedPathKnot != null)
        {
            Point delta = pos - _dragStart;
            _selectedPathKnot.Anchor = pos;
            _selectedPathKnot.HandleIn = _selectedPathKnot.HandleIn + delta;
            _selectedPathKnot.HandleOut = _selectedPathKnot.HandleOut + delta;
            _dragStart = pos;
            InvalidateVisual();
            return;
        }

        if (ActiveToolMode == "eraser" && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var erasedElement = _elements.OrderByDescending(el => el.ZIndex)
                .FirstOrDefault(el => !el.IsLocked && el.IsVisible && el.HitTestRotationAware(pos));
            if (erasedElement != null)
            {
                ElementEraseRequested?.Invoke(erasedElement);
            }
            return;
        }

        if (_isPlacingShape)
        {
            _shapeDragEnd = pos;
            InvalidateVisual();
            return;
        }

        if (_isMarqueeSelecting)
        {
            _marqueeEnd = pos;
            InvalidateVisual();
            return;
        }

        if (_isLassoActive)
        {
            HandleLassoPointerMoved(pos, e);
            return;
        }

        double dx = pos.X - _dragStart.X;
        double dy = pos.Y - _dragStart.Y;
        if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5) _dragOccurred = true;

        if (_isDragging && _groupDragOrigins != null)
        {
            // Shift constrains the move to a single axis (whichever has the larger delta).
            double moveDx = dx, moveDy = dy;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (Math.Abs(dx) >= Math.Abs(dy)) moveDy = 0; else moveDx = 0;
            }

            var (snapDx, snapDy, guides) = ComputeSnapping(moveDx, moveDy);
            _activeGuides = guides;

            foreach (var (el, origin) in _groupDragOrigins)
            {
                el.X = origin.X + snapDx;
                el.Y = origin.Y + snapDy;
            }
            CanvasChanged?.Invoke();
            InvalidateVisual();
        }
        else if (_isResizing && _selection.Count == 1)
        {
            ApplyResize(dx, dy, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            CanvasChanged?.Invoke();
            InvalidateVisual();
        }
        else if (_isGroupResizing && _groupResizeOrigins != null)
        {
            ApplyGroupResize(dx, dy, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            CanvasChanged?.Invoke();
            InvalidateVisual();
        }
        else if (_isRotating && _rotationOrigins != null)
        {
            double currentAngle = Math.Atan2(pos.Y - _rotationPivot.Y, pos.X - _rotationPivot.X);
            double deltaDegrees = (currentAngle - _rotationStartAngle) * 180.0 / Math.PI;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) deltaDegrees = Math.Round(deltaDegrees / 15.0) * 15.0;
            ApplyRotationDrag(deltaDegrees);
            CanvasChanged?.Invoke();
            InvalidateVisual();
        }
    }

    /// <summary>Snaps a candidate move delta so the dragged selection's edges/center align with
    /// other elements' edges/centers or the canvas edges/center, within a small zoom-compensated
    /// tolerance. Returns the (possibly adjusted) delta plus the guide lines to render.</summary>
    private (double Dx, double Dy, List<(bool IsVertical, double Position)> Guides) ComputeSnapping(double dx, double dy)
    {
        var guides = new List<(bool, double)>();
        if (_groupDragOrigins == null || _groupDragOrigins.Count == 0) return (dx, dy, guides);

        Rect? draggedBounds = null;
        foreach (var (el, origin) in _groupDragOrigins)
        {
            var candidate = new Rect(origin.X + dx, origin.Y + dy, el.Width, el.Height);
            draggedBounds = draggedBounds is { } db ? db.Union(candidate) : candidate;
        }
        if (draggedBounds is not { } bounds) return (dx, dy, guides);

        double tolerance = 6 / _zoom;
        var draggedSet = new HashSet<CanvasElement>(_groupDragOrigins.Keys);

        if (!SnapEnabled) return (dx, dy, guides);

        var targetsX = new List<double>();
        var targetsY = new List<double>();

        if (SnapToDocumentBounds)
        {
            targetsX.Add(0); targetsX.Add(_logicalWidth / 2); targetsX.Add(_logicalWidth);
            targetsY.Add(0); targetsY.Add(_logicalHeight / 2); targetsY.Add(_logicalHeight);
        }

        if (SnapToGuides && ShowUserGuides)
        {
            foreach (var (isVertical, pos, _) in _userGuides)
            {
                if (isVertical) targetsX.Add(pos);
                else targetsY.Add(pos);
            }
        }

        if (SnapToLayers)
        {
            foreach (var other in _elements)
            {
                if (draggedSet.Contains(other) || !other.IsVisible) continue;
                var ob = other.GetRotatedBounds();
                targetsX.Add(ob.X); targetsX.Add(ob.X + ob.Width / 2); targetsX.Add(ob.Right);
                targetsY.Add(ob.Y); targetsY.Add(ob.Y + ob.Height / 2); targetsY.Add(ob.Bottom);
            }
        }

        double snapDx = dx, snapDy = dy;
        var xCandidates = new[] { bounds.X, bounds.X + bounds.Width / 2, bounds.Right };
        var yCandidates = new[] { bounds.Y, bounds.Y + bounds.Height / 2, bounds.Bottom };

        foreach (var xc in xCandidates)
        {
            var match = targetsX.FirstOrDefault(tx => Math.Abs(xc - tx) <= tolerance, double.NaN);
            if (!double.IsNaN(match))
            {
                snapDx += match - xc;
                guides.Add((true, match));
                break;
            }
        }
        foreach (var yc in yCandidates)
        {
            var match = targetsY.FirstOrDefault(ty => Math.Abs(yc - ty) <= tolerance, double.NaN);
            if (!double.IsNaN(match))
            {
                snapDy += match - yc;
                guides.Add((false, match));
                break;
            }
        }

        return (snapDx, snapDy, guides);
    }

    /// <summary>Proportionally resizes every selected element together — each element's new
    /// position/size is derived from its fractional offset within the old vs. new group
    /// bounding box, so the same formula works anchor-agnostically for all 8 handles.</summary>
    private void ApplyGroupResize(double dx, double dy, bool shiftHeld)
    {
        if (_groupResizeOrigins == null) return;

        // Judgment call: the group-level Shift-held lock overrides any individual element's
        // own AspectLocked — only a uniform group-ratio lock applies during group-resize.
        double? lockedRatio = shiftHeld && _groupResizeOrigBounds.Height > 0
            ? _groupResizeOrigBounds.Width / _groupResizeOrigBounds.Height
            : null;

        var newBounds = ComputeResizedRect(_groupResizeOrigBounds, _groupResizeHandle, dx, dy, 20, lockedRatio);
        double sx = _groupResizeOrigBounds.Width > 0 ? newBounds.Width / _groupResizeOrigBounds.Width : 1;
        double sy = _groupResizeOrigBounds.Height > 0 ? newBounds.Height / _groupResizeOrigBounds.Height : 1;

        foreach (var (el, origRect) in _groupResizeOrigins)
        {
            double fracX = _groupResizeOrigBounds.Width > 0 ? (origRect.X - _groupResizeOrigBounds.X) / _groupResizeOrigBounds.Width : 0;
            double fracY = _groupResizeOrigBounds.Height > 0 ? (origRect.Y - _groupResizeOrigBounds.Y) / _groupResizeOrigBounds.Height : 0;
            el.X = newBounds.X + fracX * newBounds.Width;
            el.Y = newBounds.Y + fracY * newBounds.Height;
            el.Width = Math.Max(1, origRect.Width * sx);
            el.Height = Math.Max(1, origRect.Height * sy);
        }
    }

    /// <summary>Rotates every selected element's position around the shared pivot by
    /// deltaDegrees, and adds deltaDegrees to each element's own RotationDegrees. For a single
    /// selection the pivot equals that element's own center, so its position is unchanged and
    /// only RotationDegrees moves — the N=1 special case of the same formula.</summary>
    private void ApplyRotationDrag(double deltaDegrees)
    {
        if (_rotationOrigins == null) return;
        double rad = deltaDegrees * Math.PI / 180.0;
        double cos = Math.Cos(rad), sin = Math.Sin(rad);

        foreach (var (el, origin) in _rotationOrigins)
        {
            double cx = origin.X + el.Width / 2;
            double cy = origin.Y + el.Height / 2;
            double relX = cx - _rotationPivot.X;
            double relY = cy - _rotationPivot.Y;
            double newCx = _rotationPivot.X + relX * cos - relY * sin;
            double newCy = _rotationPivot.Y + relX * sin + relY * cos;

            el.X = newCx - el.Width / 2;
            el.Y = newCy - el.Height / 2;
            el.RotationDegrees = ((origin.Rotation + deltaDegrees) % 360 + 360) % 360;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDraggingExistingGuide)
        {
            CompleteExistingGuideDrag(e.GetPosition(this));
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_isCropDragging)
        {
            _isCropDragging = false;
            _cropResizeHandle = -1;
        }
        else if (_isRotatingView)
        {
            _isRotatingView = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            InvalidateVisual();
        }
        else if (_isRulerMeasuring)
        {
            _isRulerMeasuring = false;
            e.Pointer.Capture(null);
            RulerMeasurementCompleted?.Invoke(_rulerStart, _rulerEnd);
            e.Handled = true;
            InvalidateVisual();
        }
        else if (_isDrawingQuickMask)
        {
            _isDrawingQuickMask = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
        else if (_isDrawingDoodle)
        {
            _isDrawingDoodle = false;
            e.Pointer.Capture(null);
            if (CatchUpOnStrokeEnd && _currentPointerCanvasPos.HasValue && _currentDoodlePoints.Count > 0)
            {
                var finalPtr = _currentPointerCanvasPos.Value;
                if (Point.Distance(_currentDoodlePoints[^1], finalPtr) >= 1.0 / _zoom)
                {
                    _currentDoodlePoints.Add(finalPtr);
                }
            }
            _hasSmoothingLeash = false;
            _currentPointerCanvasPos = null;

            if (_currentDoodlePoints.Count > 0)
            {
                var smoothed = BrushSmoothing > 0
                    ? BrushEngine.SmoothPoints(_currentDoodlePoints, BrushSmoothing)
                    : new List<Point>(_currentDoodlePoints);
                DoodlePlaced?.Invoke(smoothed);
            }
            _currentDoodlePoints.Clear();
            InvalidateVisual();
        }
        else if (_isDrawingRetouch)
        {
            _isDrawingRetouch = false;
            e.Pointer.Capture(null);
            if (_currentRetouchPoints.Count > 0)
            {
                RetouchStrokeCompleted?.Invoke(_currentRetouchTool, _currentRetouchPoints);
                if (_currentRetouchTool == "clonestamp" && CloneAligned && CloneSourcePoint.HasValue && _currentRetouchPoints.Count > 1)
                {
                    CloneSourcePoint = CloneSourcePoint.Value + (_currentRetouchPoints[^1] - _currentRetouchPoints[0]);
                }
            }
            _currentRetouchPoints.Clear();
            InvalidateVisual();
        }
        else if (_isDraggingPenHandle || _isDraggingPathKnot)
        {
            _isDraggingPenHandle = false;
            _penActiveKnot = null;
            _isDraggingPathKnot = false;
            _selectedPathKnot = null;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
        else if (_isPlacingShape)
        {
            double dragDist = Point.Distance(_shapeDragStart, _shapeDragEnd);
            var start = _shapeDragStart;
            var end = dragDist < 4 ? _shapeDragStart : _shapeDragEnd;
            ShapePlaced?.Invoke(ActiveToolMode, start, end);
            _isPlacingShape = false;
        }
        else if (_isMarqueeSelecting)
        {
            FinishMarqueeSelection(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            _isMarqueeSelecting = false;
        }
        else if (_isLassoActive && LassoMode == "standard")
        {
            var canvasRelPt = ScreenToCanvas(e.GetPosition(this));
            HandleLassoPointerReleased(canvasRelPt, e);
        }
        else if (_isDragging)
        {
            if (!_dragOccurred && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                && _pressedElement != null && _selection.Count > 1)
            {
                _selection.Clear();
                _selection.Add(_pressedElement);
                NotifySelectionChanged();
            }
            else if (_dragOccurred && _groupDragOrigins != null)
            {
                var moves = new List<(CanvasElement Element, Point OldPos, Point NewPos)>();
                foreach (var (el, oldPos) in _groupDragOrigins)
                {
                    var newPos = new Point(el.X, el.Y);
                    if (oldPos != newPos) moves.Add((el, oldPos, newPos));
                }
                if (moves.Count > 0) ElementsMoved?.Invoke(moves);
            }
        }
        else if (_isResizing && _selection.Count == 1)
        {
            var single = _selection[0];
            var oldBounds = new Rect(_origX, _origY, _origW, _origH);
            var newBounds = single.Bounds;
            if (oldBounds != newBounds) ElementResized?.Invoke(single, oldBounds, newBounds);
        }
        else if (_isGroupResizing && _groupResizeOrigins != null)
        {
            var changes = new List<(CanvasElement, Rect, Rect)>();
            foreach (var (el, oldRect) in _groupResizeOrigins)
            {
                var newRect = new Rect(el.X, el.Y, el.Width, el.Height);
                if (oldRect != newRect) changes.Add((el, oldRect, newRect));
            }
            if (changes.Count > 0) GroupResized?.Invoke(changes);
        }
        else if (_isRotating && _rotationOrigins != null)
        {
            if (_rotationOrigins.Count == 1)
            {
                var (el, origin) = _rotationOrigins.First();
                var after = (el.X, el.Y, el.RotationDegrees);
                if (origin != after) ElementRotated?.Invoke(el, origin, after);
            }
            else if (_rotationOrigins.Count > 1)
            {
                var before = new Dictionary<CanvasElement, (double X, double Y, double Rotation)>(_rotationOrigins);
                var after = _rotationOrigins.Keys.ToDictionary(el => el, el => (el.X, el.Y, el.RotationDegrees));
                GroupRotated?.Invoke(before, after);
            }
        }

        _isDragging = false;
        _isResizing = false;
        _isGroupResizing = false;
        _isRotating = false;
        _isPanning = false;
        _dragOccurred = false;
        _resizeHandle = -1;
        _groupResizeHandle = -1;
        _groupDragOrigins = null;
        _groupResizeOrigins = null;
        _rotationOrigins = null;
        _pressedElement = null;
        _activeGuides.Clear();

        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    private void FinishMarqueeSelection(bool shiftHeld)
    {
        var marqueeRect = new Rect(
            Math.Min(_marqueeStart.X, _marqueeEnd.X), Math.Min(_marqueeStart.Y, _marqueeEnd.Y),
            Math.Abs(_marqueeEnd.X - _marqueeStart.X), Math.Abs(_marqueeEnd.Y - _marqueeStart.Y));

        var hits = _elements.Where(el => el.IsVisible && !el.IsLocked && marqueeRect.Intersects(el.GetRotatedBounds())).ToList();

        if (!shiftHeld) _selection.Clear();
        foreach (var el in hits)
        {
            if (!_selection.Contains(el)) _selection.Add(el);
        }
        NotifySelectionChanged();
    }

    private void NotifySelectionChanged() => SelectionChanged?.Invoke(_selection.ToList());

    private void HandleCropPointerPressed(Point canvasPt)
    {
        if (_selection.Count != 1 || _selection[0] is not ImageElement)
        {
            IsCropModeActive = false;
            return;
        }

        double handleSize = HandleSize / _zoom;
        double halfHandle = handleSize / 2;
        double pad = 4 / _zoom;
        var handles = GetHandlePoints(_cropPreviewRect);
        for (int i = 0; i < handles.Length; i++)
        {
            var hr = new Rect(handles[i].X - halfHandle - pad, handles[i].Y - halfHandle - pad,
                              handleSize + pad * 2, handleSize + pad * 2);
            if (hr.Contains(canvasPt))
            {
                _isCropDragging = true;
                _cropResizeHandle = i;
                _cropOrigX = _cropPreviewRect.X;
                _cropOrigY = _cropPreviewRect.Y;
                _cropOrigW = _cropPreviewRect.Width;
                _cropOrigH = _cropPreviewRect.Height;
                return;
            }
        }
    }

    private void HandleCropPointerMoved(Point canvasPt)
    {
        if (_selection.Count != 1 || _selection[0] is not ImageElement img) return;

        double dx = canvasPt.X - _dragStart.X;
        double dy = canvasPt.Y - _dragStart.Y;
        var frame = img.Bounds;
        const double minSize = 10;

        var orig = new Rect(_cropOrigX, _cropOrigY, _cropOrigW, _cropOrigH);
        var result = ComputeResizedRect(orig, _cropResizeHandle, dx, dy, minSize, _cropLockedAspectRatio);

        // Clamp within the element's current frame — the crop rect can only shrink, never
        // extend beyond what's currently visible ("Reset Crop" is how you get that back).
        double x = Math.Clamp(result.X, frame.X, frame.Right - minSize);
        double y = Math.Clamp(result.Y, frame.Y, frame.Bottom - minSize);
        double w = Math.Clamp(result.Width, minSize, frame.Right - x);
        double h = Math.Clamp(result.Height, minSize, frame.Bottom - y);

        _cropPreviewRect = new Rect(x, y, w, h);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            // Zoom to cursor: keep the canvas point under the pointer fixed on screen.
            var screenPos = e.GetPosition(this);
            var canvasPtBefore = ScreenToCanvas(screenPos);

            _zoom = Math.Clamp(Math.Round(_zoom * (1.0 + e.Delta.Y * 0.08), 3), MinZoom, MaxZoom);
            _isFitModeActive = false;

            double centerOffsetX = (Bounds.Width - _logicalWidth * _zoom) / 2;
            double centerOffsetY = (Bounds.Height - _logicalHeight * _zoom) / 2;
            _panOffset = new Point(
                screenPos.X - canvasPtBefore.X * _zoom - centerOffsetX,
                screenPos.Y - canvasPtBefore.Y * _zoom - centerOffsetY);

            ZoomChanged?.Invoke(_zoom);
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _panOffset = new Point(_panOffset.X + e.Delta.Y * 40, _panOffset.Y);
            _isFitModeActive = false;
        }
        else
        {
            _panOffset = new Point(_panOffset.X, _panOffset.Y + e.Delta.Y * 40);
            _isFitModeActive = false;
        }

        // Every branch above sets _panOffset directly with no bound — unlike OnSizeChanged,
        // which already clamped after its own pan-affecting change. Without this, repeated
        // scrolling pushes the pan arbitrarily far, and the canvas's own hand-drawn content
        // (Render() draws at whatever the viewport matrix says, with no automatic clip back to
        // this control's own arranged bounds) ends up rendering outside its Grid cell —
        // including over the rows above it.
        ClampPanOffset();

        // Every branch above moves the screen-space mapping of canvas content (pan or zoom-to-
        // cursor) — CanvasChanged is the one signal external listeners (e.g. a floating chrome
        // element anchored to the selection) need regardless of which branch ran.
        CanvasChanged?.Invoke();
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_isFitModeActive) FitToWindow(); // raises ZoomChanged itself, which listeners also key off
        else
        {
            ClampPanOffset();
            CanvasChanged?.Invoke();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Space && !_spaceHeld)
        {
            _spaceHeld = true;
            UpdateCursorForTool();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.OemTilde or Key.Oem3 && !_isTildeEraseActive)
        {
            _isTildeEraseActive = true;
            UpdateCursorForTool();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            if (_isCropModeActive) { ConfirmCrop(); e.Handled = true; return; }
        }

        if (e.Key == Key.Escape)
        {
            if (_isCropModeActive) { CancelCrop(); e.Handled = true; return; }
            if (_isPlacingShape) { _isPlacingShape = false; InvalidateVisual(); e.Handled = true; return; }
            if (ActiveToolMode == "rotateview" || _viewportRotationAngle != 0.0) { ViewportRotationAngle = 0.0; InvalidateVisual(); e.Handled = true; return; }
            if (ActiveToolMode == "ruler" && _isRulerMeasuring) { _isRulerMeasuring = false; _rulerStart = _rulerEnd = default; InvalidateVisual(); e.Handled = true; return; }
        }

        if (e.Key is Key.OemOpenBrackets or Key.Oem4) // '['
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                BrushHardnessAdjusted?.Invoke(-10);
            else
                BrushSizeAdjusted?.Invoke(BrushThickness > 20 ? -5 : -1);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.OemCloseBrackets or Key.Oem6) // ']'
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                BrushHardnessAdjusted?.Invoke(10);
            else
                BrushSizeAdjusted?.Invoke(BrushThickness >= 20 ? 5 : 1);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.X && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            SwapColorsRequested?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.D && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ResetColorsRequested?.Invoke();
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.K || e.Key == Key.F) && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            CommandPaletteRequested?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5)
        {
            BrushSettingsRequested?.Invoke();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.B && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "pencil" : "brush");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.E && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("eraser");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.I && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("eyedropper");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.V && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("select");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("clonestamp");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.J && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("healing");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.R && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("blur");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.O && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("dodge");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.G && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("gradient");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.T && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("text");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.U && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("rect");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Z && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("zoom");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.H && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("hand");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.M && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("marquee");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.L && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("lasso");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.W && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("wand");
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            ToolSwitchRequested?.Invoke("crop");
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            && _selection.Count > 0 && !_isCropModeActive && !_isPlacingShape)
        {
            double step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
            double dx = e.Key switch { Key.Left => -step, Key.Right => step, _ => 0 };
            double dy = e.Key switch { Key.Up => -step, Key.Down => step, _ => 0 };

            var nudgeable = _selection.Where(el => !el.IsLocked).ToList();
            if (nudgeable.Count == 0) return;

            _nudgeOrigins ??= nudgeable.ToDictionary(el => el, el => new Point(el.X, el.Y));
            foreach (var el in nudgeable) { el.X += dx; el.Y += dy; }
            CanvasChanged?.Invoke();
            InvalidateVisual();

            _nudgeCommitTimer.Stop();
            _nudgeCommitTimer.Start();
            e.Handled = true;
        }
    }

    private void OnNudgeCommitTick(object? sender, EventArgs e)
    {
        _nudgeCommitTimer.Stop();
        var origins = _nudgeOrigins;
        _nudgeOrigins = null;
        if (origins == null) return;

        var moves = new List<(CanvasElement Element, Point OldPos, Point NewPos)>();
        foreach (var (el, oldPos) in origins)
        {
            var newPos = new Point(el.X, el.Y);
            if (oldPos != newPos) moves.Add((el, oldPos, newPos));
        }
        if (moves.Count > 0) ElementsMoved?.Invoke(moves);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            _spaceHeld = false;
            UpdateCursorForTool();
            e.Handled = true;
        }

        if (e.Key is Key.OemTilde or Key.Oem3)
        {
            _isTildeEraseActive = false;
            UpdateCursorForTool();
            e.Handled = true;
        }
    }

    /// <summary>Flattens the canvas (background + all elements, ignoring UI-only affordances
    /// like the checkerboard/grid/handles) into an offscreen bitmap at logical resolution and
    /// reads back the single pixel at the given canvas-space point.</summary>
    public Color? SampleColorAtCanvasPoint(Point canvasPt)
    {
        int px = (int)Math.Floor(canvasPt.X);
        int py = (int)Math.Floor(canvasPt.Y);
        if (px < 0 || py < 0 || px >= _logicalWidth || py >= _logicalHeight) return null;

        var pixelSize = new PixelSize((int)Math.Ceiling(_logicalWidth), (int)Math.Ceiling(_logicalHeight));
        using var rtb = new RenderTargetBitmap(pixelSize);
        using (var dc = rtb.CreateDrawingContext())
        {
            var canvasRect = new Rect(0, 0, _logicalWidth, _logicalHeight);
            if (_backgroundColor.A > 0)
            {
                dc.DrawRectangle(new SolidColorBrush(_backgroundColor), null, canvasRect);
            }
            foreach (var el in _elements.OrderBy(e => e.ZIndex))
            {
                DrawElement(dc, el);
            }
        }

        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            rtb.CopyPixels(new PixelRect(px, py, 1, 1), buffer, 4, 4);
            var bytes = new byte[4];
            Marshal.Copy(buffer, bytes, 0, 4);
            // Bgra8888 byte order (matches this codebase's existing SKColorType.Bgra8888 usage).
            byte a = bytes[3];
            return a == 0 ? Colors.Transparent : Color.FromArgb(a, bytes[2], bytes[1], bytes[0]);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public Point CanvasToScreen(Point canvasPt) => canvasPt.Transform(GetViewportMatrix());

    private Color[,] SampleLocalColorGrid(int centerX, int centerY, int halfGrid = 5)
    {
        int size = halfGrid * 2 + 1;
        var grid = new Color[size, size];
        int startX = centerX - halfGrid;
        int startY = centerY - halfGrid;

        var pixelSize = new PixelSize(size, size);
        try
        {
            using var rtb = new RenderTargetBitmap(pixelSize);
            using (var dc = rtb.CreateDrawingContext())
            {
                if (_backgroundColor.A > 0)
                {
                    dc.DrawRectangle(new SolidColorBrush(_backgroundColor), null, new Rect(0, 0, size, size));
                }
                using (dc.PushTransform(Matrix.CreateTranslation(-startX, -startY)))
                {
                    var localRect = new Rect(startX, startY, size, size);
                    foreach (var el in _elements.OrderBy(e => e.ZIndex))
                    {
                        if (el.IsVisible && el.Bounds.Intersects(localRect))
                        {
                            DrawElement(dc, el);
                        }
                    }
                }
            }

            int byteCount = size * size * 4;
            var buffer = Marshal.AllocHGlobal(byteCount);
            try
            {
                rtb.CopyPixels(new PixelRect(0, 0, size, size), buffer, byteCount, size * 4);
                byte[] raw = new byte[byteCount];
                Marshal.Copy(buffer, raw, 0, byteCount);

                for (int gy = 0; gy < size; gy++)
                {
                    for (int gx = 0; gx < size; gx++)
                    {
                        int idx = (gy * size + gx) * 4;
                        byte a = raw[idx + 3];
                        byte r = raw[idx + 2];
                        byte g = raw[idx + 1];
                        byte b = raw[idx];
                        grid[gx, gy] = a == 0 ? Colors.Transparent : Color.FromArgb(a, r, g, b);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            for (int gy = 0; gy < size; gy++)
                for (int gx = 0; gx < size; gx++)
                    grid[gx, gy] = _backgroundColor;
        }

        return grid;
    }

    private void DrawEyedropperLoupe(DrawingContext dc, Point screenPos, Color[,] grid, Color sampledColor)
    {
        const int half = 5;
        const int size = half * 2 + 1; // 11
        const double cell = 6.0;
        const double gridPx = size * cell; // 66.0 px

        double cardW = 124.0;
        double cardH = gridPx + 70.0;

        // Position offset from cursor with viewport boundary clamping
        double targetX = screenPos.X + 24.0;
        double targetY = screenPos.Y + 24.0;

        if (targetX + cardW > Bounds.Width - 10) targetX = screenPos.X - cardW - 16.0;
        if (targetY + cardH > Bounds.Height - 10) targetY = screenPos.Y - cardH - 16.0;

        targetX = Math.Max(10, targetX);
        targetY = Math.Max(10, targetY);

        var cardRect = new Rect(targetX, targetY, cardW, cardH);

        // 1. Drop shadow & card backdrop
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null, cardRect.Inflate(2));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x29)), new Pen(new SolidColorBrush(Color.FromRgb(0x3E, 0x44, 0x51)), 1), cardRect, 4, 4);

        // 2. Magnified pixel grid (11x11)
        double gridLeft = targetX + (cardW - gridPx) / 2.0;
        double gridTop = targetY + 8.0;

        for (int gy = 0; gy < size; gy++)
        {
            for (int gx = 0; gx < size; gx++)
            {
                var c = grid[gx, gy];
                var cellRect = new Rect(gridLeft + gx * cell, gridTop + gy * cell, cell, cell);
                dc.DrawRectangle(new SolidColorBrush(c), null, cellRect);
            }
        }

        // Hairline grid outline
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)), 1), new Rect(gridLeft, gridTop, gridPx, gridPx));

        // Center pixel highlight (1px white inner, 1px black outer)
        var centerCell = new Rect(gridLeft + half * cell, gridTop + half * cell, cell, cell);
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Black), 2), centerCell.Inflate(1));
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.White), 1), centerCell);

        // 3. Swatch & Hex / RGB label
        double infoTop = gridTop + gridPx + 8.0;

        // Color preview bar
        var swatchRect = new Rect(targetX + 10, infoTop, 18, 18);
        dc.DrawRectangle(new SolidColorBrush(sampledColor), new Pen(new SolidColorBrush(Colors.White), 1), swatchRect, 2, 2);

        // Hex Code
        string hex = $"#{sampledColor.R:X2}{sampledColor.G:X2}{sampledColor.B:X2}";
        var hexText = new FormattedText(hex, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Consolas, Courier New, monospace", FontStyle.Normal, FontWeight.Bold), 10, new SolidColorBrush(Colors.White));
        dc.DrawText(hexText, new Point(targetX + 34, infoTop + 1));

        // RGB Values
        string rgb = $"R:{sampledColor.R} G:{sampledColor.G} B:{sampledColor.B}";
        var rgbText = new FormattedText(rgb, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal), 8.5, new SolidColorBrush(Color.FromRgb(0x9D, 0xA5, 0xB4)));
        dc.DrawText(rgbText, new Point(targetX + 10, infoTop + 24));

        // Subtitle
        var subText = new FormattedText("Eyedropper 11×11", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Light), 7.5, new SolidColorBrush(Color.FromRgb(0x6A, 0x73, 0x82)));
        dc.DrawText(subText, new Point(targetX + 10, infoTop + 38));
    }

    private void DrawQuickMaskOverlay(DrawingContext dc, byte[] maskBuffer, double width, double height)
    {
        int w = (int)Math.Max(1, Math.Round(width));
        int h = (int)Math.Max(1, Math.Round(height));
        if (maskBuffer.Length < w * h) return;

        using var bmp = new SkiaSharp.SKBitmap(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
        var pin = Marshal.AllocHGlobal(w * h * 4);
        try
        {
            byte[] bgra = new byte[w * h * 4];
            Parallel.For(0, h, y =>
            {
                int rowOffset = y * w;
                for (int x = 0; x < w; x++)
                {
                    int mIdx = rowOffset + x;
                    int bIdx = mIdx * 4;
                    byte m = maskBuffer[mIdx];
                    if (m < 255)
                    {
                        byte alpha = (byte)((255 - m) * 0.5);
                        bgra[bIdx] = 0x62;     // B
                        bgra[bIdx + 1] = 0x5B; // G
                        bgra[bIdx + 2] = 0xEC; // R
                        bgra[bIdx + 3] = alpha; // A
                    }
                }
            });

            Marshal.Copy(bgra, 0, pin, bgra.Length);
            bmp.InstallPixels(bmp.Info, pin);

            using var img = SkiaSharp.SKImage.FromBitmap(bmp);
            using var enc = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var ms = new System.IO.MemoryStream(enc.ToArray());
            var avBmp = new Bitmap(ms);
            dc.DrawImage(avBmp, new Rect(0, 0, width, height));
        }
        finally
        {
            Marshal.FreeHGlobal(pin);
        }
    }

    private void ApplyQuickMaskDab(Point center, double thickness, Color color)
    {
        int w = (int)Math.Max(1, Math.Round(_logicalWidth));
        int h = (int)Math.Max(1, Math.Round(_logicalHeight));
        if (QuickMaskBuffer == null || QuickMaskBuffer.Length < w * h) return;

        double radius = Math.Max(1.0, thickness / 2.0);
        double radSq = radius * radius;
        byte targetVal = (byte)Math.Clamp(Math.Round(0.299 * color.R + 0.587 * color.G + 0.114 * color.B), 0, 255);

        int minX = (int)Math.Max(0, Math.Floor(center.X - radius));
        int maxX = (int)Math.Min(w - 1, Math.Ceiling(center.X + radius));
        int minY = (int)Math.Max(0, Math.Floor(center.Y - radius));
        int maxY = (int)Math.Min(h - 1, Math.Ceiling(center.Y + radius));

        for (int y = minY; y <= maxY; y++)
        {
            int rowOffset = y * w;
            for (int x = minX; x <= maxX; x++)
            {
                double dx = x - center.X;
                double dy = y - center.Y;
                double distSq = dx * dx + dy * dy;
                if (distSq <= radSq)
                {
                    double dist = Math.Sqrt(distSq);
                    double falloff = 1.0 - (dist / radius);
                    int idx = rowOffset + x;
                    byte cur = QuickMaskBuffer[idx];
                    QuickMaskBuffer[idx] = (byte)Math.Clamp(Math.Round(cur + (targetVal - cur) * falloff), 0, 255);
                }
            }
        }
    }

    /// <summary>Exposes the viewport transform so the view's code-behind can position an
    /// overlay control (the inline text-edit TextBox) directly over a canvas-space rect.</summary>
    public Rect CanvasToScreenRect(Rect canvasRect)
    {
        var m = GetViewportMatrix();
        var topLeft = canvasRect.TopLeft.Transform(m);
        var bottomRight = canvasRect.BottomRight.Transform(m);
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>Screen-space bounding rect of the current selection (single or multi), or null
    /// when nothing is selected. The viewport transform is a pure scale+translate (no
    /// rotation), so unioning each element's own rotated bounds in canvas-space first and
    /// converting once is equivalent to converting-then-unioning — same math
    /// <see cref="ComputeGroupBounds"/> uses for the multi-selection outline.</summary>
    public Rect? GetSelectionScreenBounds()
    {
        if (_selection.Count == 0) return null;
        var bounds = _selection[0].GetRotatedBounds();
        for (int i = 1; i < _selection.Count; i++) bounds = bounds.Union(_selection[i].GetRotatedBounds());
        return CanvasToScreenRect(bounds);
    }

    /// <summary>Where a fixed-size piece of floating chrome (e.g. a quick-actions cluster)
    /// should sit relative to a selection's screen-space bounds: horizontally centered and
    /// clamped so it never renders off either side of the visible viewport; prefers directly
    /// above the selection but flips to below when there isn't room (e.g. the selection sits
    /// flush against the top of the canvas) — the collision-avoidance the reference screenshots'
    /// floating toolbar was missing.</summary>
    public Point GetFloatingChromeAnchor(Rect selectionScreenBounds, Size chromeSize, double gap = 26)
    {
        double x = selectionScreenBounds.Center.X - chromeSize.Width / 2;
        x = Math.Clamp(x, 0, Math.Max(0, Bounds.Width - chromeSize.Width));

        double yAbove = selectionScreenBounds.Y - chromeSize.Height - gap;
        double y = yAbove >= 0 ? yAbove : selectionScreenBounds.Bottom + gap;

        return new Point(x, y);
    }

    private void ApplyResize(double dx, double dy, bool shiftHeld)
    {
        if (_selection.Count != 1) return;
        var selected = _selection[0];

        double? lockedAspectRatio = (selected.AspectLocked ^ shiftHeld) && selected.GetAspectRatio() > 0
            ? selected.GetAspectRatio()
            : null;

        var orig = new Rect(_origX, _origY, _origW, _origH);
        var result = ComputeResizedRect(orig, _resizeHandle, dx, dy, 20, lockedAspectRatio);

        selected.X = result.X;
        selected.Y = result.Y;
        selected.Width = result.Width;
        selected.Height = result.Height;
    }

    /// <summary>
    /// Pure resize math shared by single-element resize, group-resize, and fixed-aspect-ratio
    /// crop presets. Handle indices: 0=TL 1=T 2=TR 3=R 4=BR 5=B 6=BL 7=L.
    /// </summary>
    private static Rect ComputeResizedRect(Rect orig, int handleIndex, double dx, double dy, double minSize, double? lockedAspectRatio)
    {
        double origX = orig.X, origY = orig.Y, origW = orig.Width, origH = orig.Height;
        double newX = origX, newY = origY, newW = origW, newH = origH;

        switch (handleIndex)
        {
            case 0: // TL
                newX = Math.Min(origX + dx, origX + origW - minSize);
                newY = Math.Min(origY + dy, origY + origH - minSize);
                newW = Math.Max(origW - dx, minSize);
                newH = Math.Max(origH - dy, minSize);
                break;
            case 1: // T
                newY = Math.Min(origY + dy, origY + origH - minSize);
                newH = Math.Max(origH - dy, minSize);
                break;
            case 2: // TR
                newY = Math.Min(origY + dy, origY + origH - minSize);
                newW = Math.Max(origW + dx, minSize);
                newH = Math.Max(origH - dy, minSize);
                break;
            case 3: // R
                newW = Math.Max(origW + dx, minSize);
                break;
            case 4: // BR
                newW = Math.Max(origW + dx, minSize);
                newH = Math.Max(origH + dy, minSize);
                break;
            case 5: // B
                newH = Math.Max(origH + dy, minSize);
                break;
            case 6: // BL
                newX = Math.Min(origX + dx, origX + origW - minSize);
                newW = Math.Max(origW - dx, minSize);
                newH = Math.Max(origH + dy, minSize);
                break;
            case 7: // L
                newX = Math.Min(origX + dx, origX + origW - minSize);
                newW = Math.Max(origW - dx, minSize);
                break;
        }

        // Aspect-ratio lock: constrain the drag, re-anchoring so the OPPOSITE corner/edge
        // stays pinned.
        if (lockedAspectRatio is { } ratio && ratio > 0)
        {
            switch (handleIndex)
            {
                case 1: case 5: // pure T/B: height drives width, anchored horizontally already
                    newW = newH * ratio;
                    newX = origX + (origW - newW) / 2;
                    break;
                case 3: case 7: // pure L/R: width drives height, anchored vertically already
                    newH = newW / ratio;
                    newY = origY + (origH - newH) / 2;
                    break;
                default: // corner handles: drive from the larger proportional change
                    double widthRatio = Math.Abs(newW / origW - 1);
                    double heightRatio = Math.Abs(newH / origH - 1);
                    if (widthRatio >= heightRatio) newH = newW / ratio; else newW = newH * ratio;

                    switch (handleIndex)
                    {
                        case 0: newX = origX + origW - newW; newY = origY + origH - newH; break; // TL anchors BR
                        case 2: newY = origY + origH - newH; break; // TR anchors BL
                        case 6: newX = origX + origW - newW; break; // BL anchors TR
                                                                     // case 4 (BR) anchors TL — X/Y already correct
                    }
                    break;
            }
        }

        return new Rect(newX, newY, newW, newH);
    }

    private static void DrawSymmetryAxes(DrawingContext dc, double w, double h, SymmetryMode mode, double zoom)
    {
        double cx = w / 2.0;
        double cy = h / 2.0;
        double strokeW = 1.0 / zoom;
        var dashStyle = new DashStyle(new double[] { 4, 4 }, 0);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(160, 99, 102, 241)), strokeW, dashStyle);

        if (mode is SymmetryMode.Vertical or SymmetryMode.Dual)
        {
            dc.DrawLine(pen, new Point(cx, 0), new Point(cx, h));
        }
        if (mode is SymmetryMode.Horizontal or SymmetryMode.Dual)
        {
            dc.DrawLine(pen, new Point(0, cy), new Point(w, cy));
        }
        if (mode == SymmetryMode.Radial)
        {
            for (int k = 0; k < 6; k++)
            {
                double a = k * Math.PI / 3.0;
                double len = Math.Max(w, h);
                dc.DrawLine(pen, new Point(cx - len * Math.Cos(a), cy - len * Math.Sin(a)),
                                 new Point(cx + len * Math.Cos(a), cy + len * Math.Sin(a)));
            }
        }

        // Center pivot crosshair
        var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 99, 102, 241)), strokeW);
        double s = 6.0 / zoom;
        dc.DrawLine(crossPen, new Point(cx - s, cy), new Point(cx + s, cy));
        dc.DrawLine(crossPen, new Point(cx, cy - s), new Point(cx, cy + s));
    }

    private static void DrawBrushCursor(DrawingContext dc, Point pt, double size, double roundness, double angleDeg, double zoom)
    {
        double radiusX = Math.Max(1.0, size / 2.0);
        double radiusY = Math.Max(1.0, radiusX * roundness);
        double angleRad = angleDeg * Math.PI / 180.0;
        double strokeWidth = 1.0 / zoom;

        var darkPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), strokeWidth);
        var lightPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), strokeWidth);

        using (dc.PushTransform(Matrix.CreateRotation(angleRad) * Matrix.CreateTranslation(pt.X, pt.Y)))
        {
            dc.DrawEllipse(null, darkPen, new Point(0, 0), radiusX, radiusY);
            dc.DrawEllipse(null, lightPen, new Point(0, 0), Math.Max(0.5, radiusX - strokeWidth), Math.Max(0.5, radiusY - strokeWidth));
            if (size > 14.0)
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), null, new Point(0, 0), 1.2 / zoom, 1.2 / zoom);
            }
        }
    }

    private static void DrawSourceMarker(DrawingContext dc, Point pt, double zoom)
    {
        double s = 8.0 / zoom;
        double sw = 1.0 / zoom;
        var darkPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), sw + 1.5);
        var lightPen = new Pen(new SolidColorBrush(Colors.White), sw);

        dc.DrawLine(darkPen, new Point(pt.X - s, pt.Y), new Point(pt.X + s, pt.Y));
        dc.DrawLine(darkPen, new Point(pt.X, pt.Y - s), new Point(pt.X, pt.Y + s));
        dc.DrawLine(lightPen, new Point(pt.X - s, pt.Y), new Point(pt.X + s, pt.Y));
        dc.DrawLine(lightPen, new Point(pt.X, pt.Y - s), new Point(pt.X, pt.Y + s));
    }

    private static void DrawVectorPathOverlay(DrawingContext dc, VectorDocumentPath docPath, double zoom)
    {
        var strokePen = new Pen(new SolidColorBrush(Color.FromRgb(55, 142, 240)), 1.5 / zoom);
        var handlePen = new Pen(new SolidColorBrush(Color.FromArgb(180, 160, 160, 160)), 1.0 / zoom);
        var anchorFill = Brushes.White;
        var anchorPen = new Pen(new SolidColorBrush(Color.FromRgb(55, 142, 240)), 1.2 / zoom);
        var handleFill = new SolidColorBrush(Color.FromRgb(55, 142, 240));

        double knotSize = 6.0 / zoom;
        double handleDotSize = 4.0 / zoom;

        foreach (var sub in docPath.Subpaths)
        {
            if (sub.Knots.Count == 0) continue;
            var geom = sub.ToAvaloniaGeometry();
            dc.DrawGeometry(null, strokePen, geom);

            foreach (var knot in sub.Knots)
            {
                if (knot.HasHandleIn)
                {
                    dc.DrawLine(handlePen, knot.Anchor, knot.HandleIn);
                    dc.DrawEllipse(handleFill, null, knot.HandleIn, handleDotSize / 2, handleDotSize / 2);
                }
                if (knot.HasHandleOut)
                {
                    dc.DrawLine(handlePen, knot.Anchor, knot.HandleOut);
                    dc.DrawEllipse(handleFill, null, knot.HandleOut, handleDotSize / 2, handleDotSize / 2);
                }

                var anchorRect = new Rect(knot.Anchor.X - knotSize / 2, knot.Anchor.Y - knotSize / 2, knotSize, knotSize);
                dc.DrawRectangle(anchorFill, anchorPen, anchorRect);
            }
        }
    }

    private void DrawRulerOverlay(DrawingContext dc, Point start, Point end, double zoom)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(240, 0, 160, 255)), 1.5 / zoom);
        var dashPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 1.0 / zoom);

        dc.DrawLine(pen, start, end);

        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double angle = Math.Atan2(-dy, dx) * 180.0 / Math.PI;

        double markerSize = 6.0 / zoom;
        // Start crosshair
        dc.DrawLine(pen, new Point(start.X - markerSize, start.Y), new Point(start.X + markerSize, start.Y));
        dc.DrawLine(pen, new Point(start.X, start.Y - markerSize), new Point(start.X, start.Y + markerSize));
        // End crosshair
        dc.DrawLine(pen, new Point(end.X - markerSize, end.Y), new Point(end.X + markerSize, end.Y));
        dc.DrawLine(pen, new Point(end.X, end.Y - markerSize), new Point(end.X, end.Y + markerSize));

        // Bounding box delta guides if dx and dy are significant
        if (Math.Abs(dx) > 10 / zoom && Math.Abs(dy) > 10 / zoom)
        {
            dc.DrawLine(dashPen, start, new Point(end.X, start.Y));
            dc.DrawLine(dashPen, new Point(end.X, start.Y), end);
        }

        // Midpoint HUD readout
        Point mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        string text = $"D: {dist:F1} px  ∠: {angle:F1}°  ΔX: {dx:F0}  ΔY: {dy:F0}";
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter,Segoe UI,Arial"),
            11.0 / zoom,
            new SolidColorBrush(Colors.White));

        var bgRect = new Rect(mid.X + 8 / zoom, mid.Y - 18 / zoom, ft.Width + 10 / zoom, ft.Height + 6 / zoom);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(220, 24, 24, 26)), null, bgRect);
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1.0 / zoom), bgRect);
        dc.DrawText(ft, new Point(bgRect.Left + 5 / zoom, bgRect.Top + 3 / zoom));
    }

    private void DrawRotateViewCompass(DrawingContext dc)
    {
        double cx = Bounds.Width - 45;
        double cy = 45;
        double r = 20;

        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(210, 28, 28, 30)), new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1.5), new Point(cx, cy), r, r);

        double rad = (_viewportRotationAngle - 90.0) * Math.PI / 180.0;
        Point northTip = new Point(cx + Math.Cos(rad) * (r - 4), cy + Math.Sin(rad) * (r - 4));
        Point southTip = new Point(cx - Math.Cos(rad) * (r - 4), cy - Math.Sin(rad) * (r - 4));

        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255, 60, 60)), 2.5, lineCap: PenLineCap.Round), new Point(cx, cy), northTip);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(180, 180, 180)), 2.0, lineCap: PenLineCap.Round), new Point(cx, cy), southTip);

        string angleText = $"{_viewportRotationAngle:0}°";
        var ft = new FormattedText(
            angleText,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter,Segoe UI,Arial"),
            10.0,
            new SolidColorBrush(Colors.White));
        dc.DrawText(ft, new Point(cx - ft.Width / 2, cy + r + 3));
    }
}
