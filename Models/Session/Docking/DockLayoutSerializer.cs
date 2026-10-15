using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

/// <summary>Saves and restores the dock layout tree as JSON (stored in the studio preferences).</summary>
public static class DockLayoutSerializer
{
    public const int SchemaVersion = 1;
    private const int MaxDepth = 64;

    public static string Serialize(DockNode root) =>
        JsonSerializer.Serialize(new LayoutDto { Version = SchemaVersion, Root = ToDto(root) });

    /// <summary>
    /// Parses a saved layout. Unknown or duplicate pages are dropped and missing pages count as closed.
    /// Returns false (and a null root) for corrupt JSON, a newer schema, or a tree without one document host.
    /// </summary>
    public static bool TryDeserialize(string? json, IReadOnlyCollection<string> knownPages, out DockNode? root)
    {
        root = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            var dto = JsonSerializer.Deserialize<LayoutDto>(json);
            if (dto?.Root == null || dto.Version > SchemaVersion) return false;
            var node = FromDto(dto.Root, 0);
            root = node == null ? null : DockLayoutOps.Normalize(node, knownPages);
            return root != null;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return false;
        }
    }

    private static NodeDto ToDto(DockNode node) => node switch
    {
        DockSplitNode split => new NodeDto
        {
            Kind = "split",
            Id = split.Id,
            Orientation = split.Orientation.ToString(),
            Ratio = split.Ratio,
            PanelSize = split.PanelSize,
            First = ToDto(split.First),
            Second = ToDto(split.Second)
        },
        DockGroupNode group => new NodeDto
        {
            Kind = "group",
            Id = group.Id,
            Host = group.IsDocumentHost,
            Pages = [.. group.Pages],
            Active = group.ActivePage
        },
        _ => throw new NotSupportedException(node.GetType().Name)
    };

    private static DockNode? FromDto(NodeDto dto, int depth)
    {
        if (depth > MaxDepth) return null;

        if (dto.Kind == "split")
        {
            if (dto.First == null || dto.Second == null) return null;
            if (!Enum.TryParse<DockOrientation>(dto.Orientation, out var orientation)) return null;
            var first = FromDto(dto.First, depth + 1);
            var second = FromDto(dto.Second, depth + 1);
            if (first == null || second == null) return null;
            return new DockSplitNode(orientation, first, second)
            {
                Id = dto.Id ?? "",
                Ratio = dto.Ratio,
                PanelSize = dto.PanelSize
            };
        }

        if (dto.Kind == "group")
        {
            var group = dto.Host
                ? DockGroupNode.CreateDocumentHost()
                : new DockGroupNode { Id = dto.Id ?? "" };
            if (dto.Pages != null) group.Pages.AddRange(dto.Pages);
            group.ActivePage = dto.Active;
            return group;
        }

        return null;
    }

    private sealed class LayoutDto
    {
        public int Version { get; set; }
        public NodeDto? Root { get; set; }
    }

    private sealed class NodeDto
    {
        public string? Kind { get; set; }
        public string? Id { get; set; }
        public string? Orientation { get; set; }
        public double Ratio { get; set; }
        public double PanelSize { get; set; }
        public NodeDto? First { get; set; }
        public NodeDto? Second { get; set; }
        public bool Host { get; set; }
        public List<string>? Pages { get; set; }
        public string? Active { get; set; }
    }
}
