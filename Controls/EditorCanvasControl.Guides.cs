using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class EditorCanvasControl
{
    // ── Interactive Guide State ─────────────────────────────────────────────
    private (bool IsVertical, double Position, bool IsOverCanvas)? _activeDragGuide;
    private Point _activeDragGuideScreenPos;
    private int _hoveredGuideIndex = -1;
    private int _draggingGuideIndex = -1;
    private bool _isDraggingExistingGuide;
    private bool _isAltDuplicatingGuide;

    public event Action<string>? GuideStatusChanged;
    public event Action<bool, double?>? GuideDraggingChanged;

    /// <summary>
    /// Checks if a screen-space coordinate falls within the logical canvas viewport bounds.
    /// </summary>
    public bool IsPointOverCanvas(Point screenPt)
    {
        return screenPt.X >= 0 && screenPt.Y >= 0 &&
               screenPt.X <= Bounds.Width && screenPt.Y <= Bounds.Height;
    }

    /// <summary>
    /// Hit-tests persistent user guides against a screen coordinate with a given pixel tolerance.
    /// </summary>
    public int HitTestUserGuide(Point screenPt, double tolerancePixels = 6.0)
    {
        if (!ShowUserGuides || _userGuides.Count == 0) return -1;

        for (int i = _userGuides.Count - 1; i >= 0; i--)
        {
            var (isVert, pos, _) = _userGuides[i];
            Point guideScreenPt = CanvasToScreen(new Point(isVert ? pos : 0, isVert ? 0 : pos));

            if (isVert)
            {
                if (Math.Abs(screenPt.X - guideScreenPt.X) <= tolerancePixels)
                    return i;
            }
            else
            {
                if (Math.Abs(screenPt.Y - guideScreenPt.Y) <= tolerancePixels)
                    return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Updates the hover cursor when the pointer moves near an existing guide.
    /// </summary>
    public void UpdateGuideHoverCursor(Point screenPt)
    {
        if (!ShowUserGuides || LockUserGuides)
        {
            _hoveredGuideIndex = -1;
            return;
        }

        int hitIdx = HitTestUserGuide(screenPt);
        _hoveredGuideIndex = hitIdx;

        if (hitIdx >= 0)
        {
            bool isVert = _userGuides[hitIdx].IsVertical;
            Cursor = new Cursor(isVert ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth);
        }
        else
        {
            Cursor = Cursor.Default;
        }
    }

    /// <summary>
    /// Attempts to start dragging an existing guide when pointer is pressed on the canvas.
    /// </summary>
    public bool TryStartGuideDrag(Point screenPt, bool altHeld)
    {
        if (!ShowUserGuides || LockUserGuides) return false;

        int hitIdx = HitTestUserGuide(screenPt);
        if (hitIdx < 0) return false;

        _draggingGuideIndex = hitIdx;
        _isDraggingExistingGuide = true;
        _isAltDuplicatingGuide = altHeld;

        var guide = _userGuides[hitIdx];
        if (altHeld)
        {
            _userGuides.Add(guide);
            _draggingGuideIndex = _userGuides.Count - 1;
        }

        _activeDragGuide = (guide.IsVertical, guide.Position, true);
        _activeDragGuideScreenPos = screenPt;
        InvalidateVisual();
        return true;
    }

    /// <summary>
    /// Begins pulling a new guide from an outer ruler control into the canvas.
    /// </summary>
    public void BeginGuideDragFromRuler(bool isVertical, Point canvasScreenPt)
    {
        if (!ShowUserGuides) return;

        Point canvasPt = ScreenToCanvas(canvasScreenPt);
        double rawPos = isVertical ? canvasPt.X : canvasPt.Y;
        double snappedPos = SnapGuidePosition(isVertical, rawPos);
        bool isInside = IsPointOverCanvas(canvasScreenPt);

        _activeDragGuide = (isVertical, snappedPos, isInside);
        _activeDragGuideScreenPos = canvasScreenPt;
        InvalidateVisual();
    }

    /// <summary>
    /// Updates position and snapping during an ongoing ruler-pulled or canvas guide drag.
    /// </summary>
    public void UpdateGuideDrag(bool isVertical, Point canvasScreenPt)
    {
        Point canvasPt = ScreenToCanvas(canvasScreenPt);
        double rawPos = isVertical ? canvasPt.X : canvasPt.Y;
        double snappedPos = SnapGuidePosition(isVertical, rawPos);
        bool isInside = IsPointOverCanvas(canvasScreenPt);

        _activeDragGuide = (isVertical, snappedPos, isInside);
        _activeDragGuideScreenPos = canvasScreenPt;
        GuideDraggingChanged?.Invoke(isVertical, snappedPos);
        InvalidateVisual();
    }

    /// <summary>
    /// Updates an ongoing guide drag triggered by mouse movement over canvas.
    /// </summary>
    public void UpdateGuideDragPointer(Point screenPt)
    {
        if (_activeDragGuide is { } guide)
        {
            UpdateGuideDrag(guide.IsVertical, screenPt);
        }
    }

    /// <summary>
    /// Commits or cancels an active guide drag from a ruler.
    /// </summary>
    public void EndGuideDrag(bool commit, Point? canvasScreenPt = null)
    {
        if (_activeDragGuide is { } guide)
        {
            bool isInside = canvasScreenPt.HasValue
                ? IsPointOverCanvas(canvasScreenPt.Value)
                : guide.IsOverCanvas;

            if (commit && isInside)
            {
                AddUserGuide(guide.IsVertical, guide.Position);
                GuideStatusChanged?.Invoke($"Added {(guide.IsVertical ? "vertical" : "horizontal")} guide at {guide.Position:0.#} px.");
            }

            _activeDragGuide = null;
            GuideDraggingChanged?.Invoke(false, null);
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Completes dragging an existing guide on canvas. If dragged off canvas, the guide is deleted.
    /// </summary>
    public void CompleteExistingGuideDrag(Point canvasScreenPt)
    {
        if (_isDraggingExistingGuide && _draggingGuideIndex >= 0 && _draggingGuideIndex < _userGuides.Count)
        {
            bool isOver = IsPointOverCanvas(canvasScreenPt);
            if (!isOver)
            {
                _userGuides.RemoveAt(_draggingGuideIndex);
                GuideStatusChanged?.Invoke("Guide removed.");
            }
            else if (_activeDragGuide is { } guide)
            {
                var currentColor = _userGuides[_draggingGuideIndex].Color;
                _userGuides[_draggingGuideIndex] = (guide.IsVertical, guide.Position, currentColor);
                GuideStatusChanged?.Invoke($"Guide moved to {guide.Position:0.#} px.");
            }

            _isDraggingExistingGuide = false;
            _draggingGuideIndex = -1;
            _activeDragGuide = null;
            GuideDraggingChanged?.Invoke(false, null);
            Cursor = Cursor.Default;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Cancels any active guide drag immediately without committing.
    /// </summary>
    public void CancelGuideDrag()
    {
        if (_isDraggingExistingGuide && _isAltDuplicatingGuide && _draggingGuideIndex >= 0 && _draggingGuideIndex < _userGuides.Count)
        {
            _userGuides.RemoveAt(_draggingGuideIndex);
        }

        _isDraggingExistingGuide = false;
        _draggingGuideIndex = -1;
        _activeDragGuide = null;
        GuideDraggingChanged?.Invoke(false, null);
        Cursor = Cursor.Default;
        InvalidateVisual();
    }

    /// <summary>
    /// Deletes a user guide by index.
    /// </summary>
    public void DeleteUserGuide(int index)
    {
        if (index >= 0 && index < _userGuides.Count)
        {
            _userGuides.RemoveAt(index);
            GuideStatusChanged?.Invoke("Guide removed.");
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Clears all user guides on the canvas.
    /// </summary>
    public void ClearUserGuides()
    {
        _userGuides.Clear();
        GuideStatusChanged?.Invoke("Canvas guides cleared.");
        InvalidateVisual();
    }

    /// <summary>
    /// Shows the Guide context menu (Delete Guide, Lock/Unlock Guides, Clear Canvas Guides) (#3734).
    /// </summary>
    public bool ShowGuideContextMenu(Point screenPt)
    {
        int hit = HitTestUserGuide(screenPt);
        if (hit < 0) return false;

        var menu = new Avalonia.Controls.ContextMenu();
        var deleteItem = new Avalonia.Controls.MenuItem { Header = "Delete Guide" };
        deleteItem.Click += (_, _) => DeleteUserGuide(hit);
        menu.Items.Add(deleteItem);

        var lockItem = new Avalonia.Controls.MenuItem { Header = LockUserGuides ? "Unlock Guides" : "Lock Guides" };
        lockItem.Click += (_, _) => { LockUserGuides = !LockUserGuides; InvalidateVisual(); };
        menu.Items.Add(lockItem);

        var clearItem = new Avalonia.Controls.MenuItem { Header = "Clear Canvas Guides" };
        clearItem.Click += (_, _) => ClearUserGuides();
        menu.Items.Add(clearItem);

        menu.Open(this);
        return true;
    }

    /// <summary>
    /// Snaps a candidate guide position against document edges, center, layers, and existing guides.
    /// </summary>
    public double SnapGuidePosition(bool isVertical, double rawPos)
    {
        if (!SnapEnabled) return rawPos;

        double snapDistance = 8.0 / _zoom;
        double bestSnap = rawPos;
        double minDelta = snapDistance;

        void CheckSnap(double target)
        {
            double d = Math.Abs(rawPos - target);
            if (d < minDelta)
            {
                minDelta = d;
                bestSnap = target;
            }
        }

        // 1. Snap to document bounds & center
        if (SnapToDocumentBounds)
        {
            double maxDim = isVertical ? _logicalWidth : _logicalHeight;
            CheckSnap(0.0);
            CheckSnap(maxDim / 2.0);
            CheckSnap(maxDim);
        }

        // 2. Snap to other existing guides
        if (SnapToGuides)
        {
            for (int i = 0; i < _userGuides.Count; i++)
            {
                if (i == _draggingGuideIndex) continue;
                var (otherIsVert, otherPos, _) = _userGuides[i];
                if (otherIsVert == isVertical) CheckSnap(otherPos);
            }
        }

        // 3. Snap to layer bounds
        if (SnapToLayers && _elements != null)
        {
            foreach (var el in _elements)
            {
                if (!el.IsVisible) continue;
                if (isVertical)
                {
                    CheckSnap(el.X);
                    CheckSnap(el.X + el.Width / 2.0);
                    CheckSnap(el.X + el.Width);
                }
                else
                {
                    CheckSnap(el.Y);
                    CheckSnap(el.Y + el.Height / 2.0);
                    CheckSnap(el.Y + el.Height);
                }
            }
        }

        return bestSnap;
    }

    /// <summary>
    /// Snaps a 2D point against active guides and grid (Studio Parity 28e58d20, #3928, #3632).
    /// Distort corners snap to guides and their intersections.
    /// Excludes guides if ShowUserGuides is false or ViewExtras is false.
    /// </summary>
    public Point SnapPointToGuidesAndGrid(Point pt, bool allowGuideSnapping = true)
    {
        if (!SnapEnabled) return pt;

        double snapRadius = 8.0 / _zoom;
        double x = pt.X;
        double y = pt.Y;

        if (allowGuideSnapping && ShowUserGuides && SnapToGuides && _userGuides.Count > 0)
        {
            double bestDistX = snapRadius;
            double bestX = x;
            double bestDistY = snapRadius;
            double bestY = y;

            foreach (var (isVert, pos, _) in _userGuides)
            {
                if (isVert)
                {
                    double d = Math.Abs(x - pos);
                    if (d < bestDistX)
                    {
                        bestDistX = d;
                        bestX = pos;
                    }
                }
                else
                {
                    double d = Math.Abs(y - pos);
                    if (d < bestDistY)
                    {
                        bestDistY = d;
                        bestY = pos;
                    }
                }
            }

            x = bestX;
            y = bestY;
        }

        if (SnapToGrid && ShowGrid && GridCellSize > 0)
        {
            double gridX = Math.Round(x / GridCellSize) * GridCellSize;
            double gridY = Math.Round(y / GridCellSize) * GridCellSize;
            if (Math.Abs(x - gridX) <= snapRadius) x = gridX;
            if (Math.Abs(y - gridY) <= snapRadius) y = gridY;
        }

        return new Point(x, y);
    }

    /// <summary>
    /// Renders the active preview guide line in canvas space.
    /// </summary>
    public void DrawActiveDragGuideInCanvasSpace(DrawingContext dc)
    {
        if (_activeDragGuide is not { } guide || !guide.IsOverCanvas) return;

        const double span = 10000;
        var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(255, 0, 240, 255)), 1.2 / _zoom);
        if (guide.IsVertical)
        {
            dc.DrawLine(guidePen, new Point(guide.Position, -span), new Point(guide.Position, span));
        }
        else
        {
            dc.DrawLine(guidePen, new Point(-span, guide.Position), new Point(span, guide.Position));
        }
    }

    // ── HUD Coordinate Badge Resources ─────────────────────────────────────
    private static readonly IBrush s_hudBgBrush = new SolidColorBrush(Color.FromArgb(230, 24, 24, 24));
    private static readonly IPen s_hudBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 70, 70, 70)), 1);
    private static readonly IBrush s_hudAccentBrush = new SolidColorBrush(Color.Parse("#00E5FF"));
    private static readonly IBrush s_hudTextBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly Typeface s_hudTypeface = new("Segoe UI, -apple-system, sans-serif");

    /// <summary>
    /// Renders a floating HUD pill badge in screen space showing the current coordinate.
    /// </summary>
    public void DrawActiveGuideHudBadge(DrawingContext dc)
    {
        if (_activeDragGuide is not { } guide || !guide.IsOverCanvas) return;

        string label = guide.IsVertical
            ? $"X: {guide.Position:0.#} px"
            : $"Y: {guide.Position:0.#} px";

        var formattedText = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            s_hudTypeface,
            11.0,
            s_hudTextBrush);

        double badgeWidth = formattedText.Width + 24;
        double badgeHeight = 22;

        double x = _activeDragGuideScreenPos.X + 14;
        double y = _activeDragGuideScreenPos.Y + 14;

        if (x + badgeWidth > Bounds.Width) x = _activeDragGuideScreenPos.X - badgeWidth - 10;
        if (y + badgeHeight > Bounds.Height) y = _activeDragGuideScreenPos.Y - badgeHeight - 10;

        var badgeRect = new Rect(x, y, badgeWidth, badgeHeight);
        var rounded = new RoundedRect(badgeRect, 4);

        dc.DrawRectangle(s_hudBgBrush, s_hudBorderPen, rounded);

        // Accent indicator dot
        dc.DrawEllipse(s_hudAccentBrush, null, new Point(x + 8, y + badgeHeight / 2), 2.5, 2.5);

        // Text
        dc.DrawText(formattedText, new Point(x + 15, y + (badgeHeight - formattedText.Height) / 2));
    }
}
