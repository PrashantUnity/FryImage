using System;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Professional command descriptor for Photoshop / PhotoCraft parity command palette and action routing.
/// </summary>
public sealed class StudioCommand
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public string? Shortcut { get; init; }
    public string IconKind { get; init; } = "CogOutline";
    public required Action Action { get; init; }
}
