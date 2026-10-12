using System;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Undo;

public interface IEditHistory
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    int UndoCount { get; }
    int RedoCount { get; }
    IReadOnlyList<IUndoableCommand> UndoStack { get; }
    IReadOnlyList<IUndoableCommand> RedoStack { get; }

    /// <summary>Raised whenever <see cref="CanUndo"/>/<see cref="CanRedo"/> may have changed.</summary>
    event EventHandler? HistoryChanged;

    /// <summary>Records a mutation that has ALREADY been applied. Clears the redo stack.</summary>
    void Push(IUndoableCommand command);

    void Undo();
    void Redo();
    void JumpToUndoDepth(int targetUndoCount);

    /// <summary>Discards all history without undoing anything (e.g. on ClearCanvas).</summary>
    void Clear();

    /// <summary>Deletes the current state, rolling back to the previous state without keeping redo (Photoshop linear history delete).</summary>
    void DeleteCurrentState();
}
