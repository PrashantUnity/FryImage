using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

/// <summary>Direction in which a <see cref="DockSplitNode"/> lays out its two children.</summary>
public enum DockOrientation
{
    /// <summary>Children sit side by side (left | right).</summary>
    Horizontal,

    /// <summary>Children are stacked (top / bottom).</summary>
    Vertical
}

/// <summary>Where a dragged tab lands relative to the pane under the pointer.</summary>
public enum DockDropPosition
{
    /// <summary>Joins the pane's tab group.</summary>
    Center,

    /// <summary>Splits the pane and takes its left side.</summary>
    Left,

    /// <summary>Splits the pane and takes its right side.</summary>
    Right,

    /// <summary>Splits the pane and takes its top side.</summary>
    Top,

    /// <summary>Splits the pane and takes its bottom side.</summary>
    Bottom
}

/// <summary>
/// A node of the binary dock layout tree (LeetCode / VS Code style workspace).
/// Leaves are <see cref="DockGroupNode"/> tab groups; inner nodes are <see cref="DockSplitNode"/> splits.
/// </summary>
public abstract class DockNode
{
    public string Id { get; set; } = NewId("node");

    internal static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];
}

/// <summary>Two panes separated by a draggable splitter.</summary>
public sealed class DockSplitNode : DockNode
{
    public DockSplitNode(DockOrientation orientation, DockNode first, DockNode second)
    {
        Id = NewId("split");
        Orientation = orientation;
        First = first;
        Second = second;
    }

    public DockOrientation Orientation { get; set; }

    public DockNode First { get; set; }

    public DockNode Second { get; set; }

    /// <summary>Share (0–1) of <see cref="First"/> when neither side holds the document host.</summary>
    public double Ratio { get; set; } = 0.5;

    /// <summary>
    /// Pixel size of the side that does NOT hold the document host. Panel columns keep their
    /// width when the window resizes; the canvas absorbs the difference.
    /// </summary>
    public double PanelSize { get; set; } = 300;
}

/// <summary>A pane with a tab strip. The single document host pane holds the canvas instead of pages.</summary>
public sealed class DockGroupNode : DockNode
{
    public const string DocumentHostId = "document-host";

    public DockGroupNode()
    {
        Id = NewId("group");
    }

    public List<string> Pages { get; } = new();

    public string? ActivePage { get; set; }

    /// <summary>True for the canvas pane: never closed, never dragged, accepts only edge drops.</summary>
    public bool IsDocumentHost { get; init; }

    public static DockGroupNode CreateDocumentHost() => new() { Id = DocumentHostId, IsDocumentHost = true };
}
