using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Undo;

/// <summary>
/// Undo/redo history engine scoped to open parameter dialogs (#4697, #4715).
/// Allows ⌘Z / Ctrl+Z and ⇧⌘Z / Ctrl+Y to step through a dialog's own settings while open,
/// leaving the document history underneath untouched.
/// </summary>
public sealed class DialogEditHistory<T> where T : class
{
    private readonly List<T> _undoStack = new();
    private readonly List<T> _redoStack = new();
    private T _settled;
    private const int MaxHistory = 50;

    public DialogEditHistory(T initial)
    {
        _settled = initial ?? throw new ArgumentNullException(nameof(initial));
    }

    public void Settle(T current) => Settle(current, false, (a, b) => EqualityComparer<T>.Default.Equals(a, b));

    /// <summary>
    /// Records a new settings snapshot if it differs from the last settled snapshot.
    /// Ignored if a slider drag is currently busy.
    /// </summary>
    public void Settle(T current, bool busy, Func<T, T, bool> areEqual)
    {
        if (busy || areEqual(current, _settled)) return;

        if (_undoStack.Count >= MaxHistory)
        {
            _undoStack.RemoveAt(0);
        }

        _undoStack.Add(_settled);
        _settled = current;
        _redoStack.Clear();
    }

    public T? Undo() => Undo(_settled);

    /// <summary>
    /// Steps settings backwards (Undo). Returns the restored state or null if at beginning.
    /// </summary>
    public T? Undo(T current)
    {
        if (_undoStack.Count == 0) return null;

        var prev = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _redoStack.Add(current);
        _settled = prev;
        return prev;
    }

    public T? Redo() => Redo(_settled);

    /// <summary>
    /// Steps settings forwards (Redo). Returns the restored state or null if at end.
    /// </summary>
    public T? Redo(T current)
    {
        if (_redoStack.Count == 0) return null;

        var next = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _undoStack.Add(current);
        _settled = next;
        return next;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
}
