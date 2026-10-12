using System;
using Avalonia.Media.Imaging;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Undo;

/// <summary>
/// Represents a named document snapshot in the FryImage History panel.
/// Snapshots capture a complete state of canvas elements and document dimensions,
/// allowing non-destructive time-travel back to any milestone.
/// </summary>
public sealed class HistorySnapshotViewModel
{
    public int Id { get; init; }
    public string Name { get; set; } = "Snapshot";
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public string ElementsJson { get; init; } = "";
    public double CanvasWidth { get; init; }
    public double CanvasHeight { get; init; }
    public bool IsActive { get; set; }
    public string FormattedTime => CreatedAt.ToString("HH:mm:ss");
}
