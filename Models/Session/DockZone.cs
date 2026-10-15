namespace PdfEditorApp.Plugins.ImageEditor.Models.Session;

/// <summary>
/// Target docking zones for panels in the studio workspace (Unity Editor style).
/// </summary>
public enum DockZone
{
    /// <summary>Docked in the left sidebar (between tool palette and canvas).</summary>
    Left,

    /// <summary>Docked in the right sidebar (classic studio dock).</summary>
    Right,

    /// <summary>Docked in the bottom shelf below the canvas.</summary>
    Bottom,

    /// <summary>Detached in an independent floating window.</summary>
    Floating,

    /// <summary>Hidden from the workspace.</summary>
    Hidden
}
