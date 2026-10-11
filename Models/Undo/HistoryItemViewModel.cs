namespace PdfEditorApp.Plugins.ImageEditor.Models.Undo;

/// <summary>
/// Represents a discrete state entry in the FryImage History panel.
/// </summary>
public sealed class HistoryItemViewModel
{
    public int UndoDepth { get; init; }
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "History";
    public bool IsActive { get; init; }
    public bool IsFuture { get; init; }
}
