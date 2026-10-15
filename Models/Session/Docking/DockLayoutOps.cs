using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

/// <summary>Which side of a split keeps a fixed pixel size.</summary>
public enum DockFixedSide
{
    /// <summary>Neither side holds the document host: the split is proportional.</summary>
    None,

    /// <summary>The first child is a panel column/row of <see cref="DockSplitNode.PanelSize"/> pixels.</summary>
    First,

    /// <summary>The second child is a panel column/row of <see cref="DockSplitNode.PanelSize"/> pixels.</summary>
    Second
}

/// <summary>
/// Pure operations on the dock layout tree. They mutate nodes in place and return the (possibly new) root.
/// Removing the last tab of a group collapses it and lets its sibling take the parent's place;
/// dropping on a pane edge wraps that pane in a new split — the same rules as the LeetCode workspace.
/// </summary>
public static class DockLayoutOps
{
    public const double DefaultSidePanelWidth = 300;
    public const double DefaultBottomPanelHeight = 240;

    // ── Queries ─────────────────────────────────────────────────────────────

    public static IEnumerable<DockNode> Nodes(DockNode root)
    {
        var stack = new Stack<DockNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            if (node is DockSplitNode split)
            {
                stack.Push(split.Second);
                stack.Push(split.First);
            }
        }
    }

    public static IEnumerable<DockGroupNode> Groups(DockNode root) => Nodes(root).OfType<DockGroupNode>();

    public static IEnumerable<DockSplitNode> Splits(DockNode root) => Nodes(root).OfType<DockSplitNode>();

    public static DockGroupNode? FindGroup(DockNode root, string? groupId) =>
        groupId == null ? null : Groups(root).FirstOrDefault(g => g.Id == groupId);

    public static DockSplitNode? FindSplit(DockNode root, string? splitId) =>
        splitId == null ? null : Splits(root).FirstOrDefault(s => s.Id == splitId);

    public static DockGroupNode? FindGroupOf(DockNode root, string pageId) =>
        Groups(root).FirstOrDefault(g => g.Pages.Contains(pageId));

    public static DockGroupNode? DocumentHost(DockNode root) => Groups(root).FirstOrDefault(g => g.IsDocumentHost);

    public static DockSplitNode? FindParent(DockNode root, DockNode child) =>
        Splits(root).FirstOrDefault(s => ReferenceEquals(s.First, child) || ReferenceEquals(s.Second, child));

    public static bool ContainsDocumentHost(DockNode node) => Groups(node).Any(g => g.IsDocumentHost);

    public static IEnumerable<string> DockedPages(DockNode root) => Groups(root).SelectMany(g => g.Pages);

    public static DockFixedSide FixedSide(DockSplitNode split)
    {
        if (ContainsDocumentHost(split.First)) return DockFixedSide.Second;
        if (ContainsDocumentHost(split.Second)) return DockFixedSide.First;
        return DockFixedSide.None;
    }

    /// <summary>Which side of the canvas a group sits on (the first split up the tree that separates them).</summary>
    public static DockZone ZoneOf(DockNode root, DockGroupNode group)
    {
        DockNode child = group;
        var parent = FindParent(root, child);
        while (parent != null)
        {
            bool isFirst = ReferenceEquals(parent.First, child);
            var other = isFirst ? parent.Second : parent.First;
            if (ContainsDocumentHost(other))
            {
                return parent.Orientation == DockOrientation.Horizontal
                    ? (isFirst ? DockZone.Left : DockZone.Right)
                    : (isFirst ? DockZone.Top : DockZone.Bottom);
            }

            child = parent;
            parent = FindParent(root, child);
        }

        return DockZone.Right;
    }

    // ── Mutations ───────────────────────────────────────────────────────────

    public static void Activate(DockGroupNode group, string pageId)
    {
        if (group.Pages.Contains(pageId))
        {
            group.ActivePage = pageId;
        }
    }

    /// <summary>Removes a page; a non-host group left empty collapses into its sibling.</summary>
    public static DockNode RemovePage(DockNode root, string pageId)
    {
        var group = FindGroupOf(root, pageId);
        if (group == null) return root;

        int index = group.Pages.IndexOf(pageId);
        group.Pages.RemoveAt(index);
        if (group.ActivePage == pageId)
        {
            group.ActivePage = group.Pages.Count == 0 ? null : group.Pages[Math.Min(index, group.Pages.Count - 1)];
        }

        if (group.Pages.Count > 0 || group.IsDocumentHost) return root;
        return CollapseGroup(root, group);
    }

    /// <summary>Removes a whole group (never the document host); its sibling takes the parent's place.</summary>
    public static DockNode RemoveGroup(DockNode root, DockGroupNode group)
    {
        if (group.IsDocumentHost) return root;
        group.Pages.Clear();
        group.ActivePage = null;
        return CollapseGroup(root, group);
    }

    /// <summary>
    /// Puts a page into <paramref name="target"/>: <see cref="DockDropPosition.Center"/> joins its tabs
    /// (at <paramref name="index"/>, or appended when negative); an edge wraps the target in a new split.
    /// </summary>
    public static DockNode InsertPage(DockNode root, string pageId, DockGroupNode target, DockDropPosition position, int index, out DockGroupNode placedIn)
    {
        if (position == DockDropPosition.Center && !target.IsDocumentHost)
        {
            if (!target.Pages.Contains(pageId))
            {
                if (index < 0 || index > target.Pages.Count) target.Pages.Add(pageId);
                else target.Pages.Insert(index, pageId);
            }

            target.ActivePage = pageId;
            placedIn = target;
            return root;
        }

        if (position == DockDropPosition.Center)
        {
            position = DockDropPosition.Right; // the canvas never holds tabs
        }

        var group = NewGroup(pageId);
        placedIn = group;
        return ReplaceNode(root, target, Wrap(target, group, position));
    }

    /// <summary>Docks a page along an outer edge of the whole workspace (a full-height column or full-width row).</summary>
    public static DockNode InsertAtRootEdge(DockNode root, string pageId, DockDropPosition edge, out DockGroupNode placedIn)
    {
        if (edge == DockDropPosition.Center) edge = DockDropPosition.Right;
        var group = NewGroup(pageId);
        placedIn = group;
        return Wrap(root, group, edge);
    }

    /// <summary>Moves a page to a group (or an edge of it). <paramref name="changed"/> is false for no-op drops.</summary>
    public static DockNode MovePage(DockNode root, string pageId, string targetGroupId, DockDropPosition position, int index, out bool changed)
    {
        changed = false;
        var target = FindGroup(root, targetGroupId);
        if (target == null) return root;
        if (target.IsDocumentHost && position == DockDropPosition.Center) return root;

        var source = FindGroupOf(root, pageId);
        if (source != null && ReferenceEquals(source, target))
        {
            if (position == DockDropPosition.Center)
            {
                int from = source.Pages.IndexOf(pageId);
                int to = index < 0 ? from : Math.Clamp(index > from ? index - 1 : index, 0, source.Pages.Count - 1);
                if (to == from && source.ActivePage == pageId) return root;
                source.Pages.RemoveAt(from);
                source.Pages.Insert(to, pageId);
                source.ActivePage = pageId;
                changed = true;
                return root;
            }

            // Splitting a group's only tab off itself would just recreate the same pane.
            if (source.Pages.Count <= 1) return root;
        }

        // Only the source group can collapse, and it is either a different group or keeps other tabs,
        // so the target survives the removal.
        root = RemovePage(root, pageId);
        root = InsertPage(root, pageId, target, position, index, out _);
        changed = true;
        return root;
    }

    /// <summary>Moves a page to an outer edge of the workspace.</summary>
    public static DockNode MovePageToRootEdge(DockNode root, string pageId, DockDropPosition edge, out bool changed)
    {
        changed = false;
        if (edge == DockDropPosition.Center) return root;

        var source = FindGroupOf(root, pageId);
        if (source is { Pages.Count: 1 } && root is DockSplitNode rootSplit && IsAtRootEdge(rootSplit, source, edge))
        {
            return root;
        }

        root = RemovePage(root, pageId);
        root = InsertAtRootEdge(root, pageId, edge, out _);
        changed = true;
        return root;
    }

    /// <summary>
    /// Repairs a loaded tree: drops unknown and duplicate pages, fixes active tabs and ids, collapses empty
    /// groups and clamps sizes. Returns null when the tree has no single document host (caller falls back).
    /// </summary>
    public static DockNode? Normalize(DockNode root, IReadOnlyCollection<string> knownPages)
    {
        var groups = Groups(root).ToList();
        if (groups.Count(g => g.IsDocumentHost) != 1) return null;

        var seenPages = new HashSet<string>();
        var seenIds = new HashSet<string> { DockGroupNode.DocumentHostId };
        foreach (var node in Nodes(root))
        {
            if (node is DockGroupNode { IsDocumentHost: true } host)
            {
                host.Id = DockGroupNode.DocumentHostId;
                host.Pages.Clear();
                host.ActivePage = null;
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Id) || !seenIds.Add(node.Id))
            {
                node.Id = DockNode.NewId(node is DockSplitNode ? "split" : "group");
                seenIds.Add(node.Id);
            }

            if (node is DockGroupNode group)
            {
                group.Pages.RemoveAll(p => !knownPages.Contains(p) || !seenPages.Add(p));
                if (group.ActivePage == null || !group.Pages.Contains(group.ActivePage))
                {
                    group.ActivePage = group.Pages.FirstOrDefault();
                }
            }
        }

        foreach (var empty in groups.Where(g => !g.IsDocumentHost && g.Pages.Count == 0))
        {
            root = CollapseGroup(root, empty);
        }

        foreach (var split in Splits(root))
        {
            split.Ratio = double.IsFinite(split.Ratio) ? Math.Clamp(split.Ratio, 0.05, 0.95) : 0.5;
            split.PanelSize = double.IsFinite(split.PanelSize) ? Math.Clamp(split.PanelSize, 80, 4000) : DefaultSidePanelWidth;
        }

        return root;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static DockGroupNode NewGroup(string pageId)
    {
        var group = new DockGroupNode();
        group.Pages.Add(pageId);
        group.ActivePage = pageId;
        return group;
    }

    private static DockSplitNode Wrap(DockNode existing, DockGroupNode incoming, DockDropPosition position)
    {
        bool horizontal = position is DockDropPosition.Left or DockDropPosition.Right;
        bool incomingFirst = position is DockDropPosition.Left or DockDropPosition.Top;
        return new DockSplitNode(
            horizontal ? DockOrientation.Horizontal : DockOrientation.Vertical,
            incomingFirst ? incoming : existing,
            incomingFirst ? existing : incoming)
        {
            Ratio = 0.5,
            PanelSize = horizontal ? DefaultSidePanelWidth : DefaultBottomPanelHeight
        };
    }

    private static DockNode ReplaceNode(DockNode root, DockNode oldNode, DockNode newNode)
    {
        if (ReferenceEquals(root, oldNode)) return newNode;
        var parent = FindParent(root, oldNode);
        if (parent == null) return root;
        if (ReferenceEquals(parent.First, oldNode)) parent.First = newNode;
        else parent.Second = newNode;
        return root;
    }

    private static DockNode CollapseGroup(DockNode root, DockGroupNode group)
    {
        var parent = FindParent(root, group);
        if (parent == null) return root;
        var sibling = ReferenceEquals(parent.First, group) ? parent.Second : parent.First;
        return ReplaceNode(root, parent, sibling);
    }

    private static bool IsAtRootEdge(DockSplitNode root, DockGroupNode group, DockDropPosition edge) => edge switch
    {
        DockDropPosition.Left => root.Orientation == DockOrientation.Horizontal && ReferenceEquals(root.First, group),
        DockDropPosition.Right => root.Orientation == DockOrientation.Horizontal && ReferenceEquals(root.Second, group),
        DockDropPosition.Top => root.Orientation == DockOrientation.Vertical && ReferenceEquals(root.First, group),
        DockDropPosition.Bottom => root.Orientation == DockOrientation.Vertical && ReferenceEquals(root.Second, group),
        _ => false
    };
}
