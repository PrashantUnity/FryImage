using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Undo;

/// <summary>
/// Capped undo/redo stack. One instance lives per <see cref="ImageEditorViewModel"/> —
/// never shared across overlay instances, and never re-created by any sub-panel, so every
/// mutation (shapes, crop, rotate, adjustments) shares a single, coherent history.
/// </summary>
public sealed class EditHistory : IEditHistory
{
    private const int MaxHistoryDepth = 100;

    // Used as stacks via Add()/RemoveAt(Count-1) rather than Stack&lt;T&gt; so trimming the
    // oldest entry (RemoveAt(0)) is straightforward once the depth cap is hit.
    private readonly List<IUndoableCommand> _undo = new();
    private readonly List<IUndoableCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public IReadOnlyList<IUndoableCommand> UndoStack => _undo;
    public IReadOnlyList<IUndoableCommand> RedoStack => _redo;

    public event EventHandler? HistoryChanged;

    public void JumpToUndoDepth(int targetUndoCount)
    {
        if (targetUndoCount < 0) targetUndoCount = 0;
        int max = _undo.Count + _redo.Count;
        if (targetUndoCount > max) targetUndoCount = max;

        while (_undo.Count > targetUndoCount && _undo.Count > 0)
        {
            var cmd = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            cmd.Undo();
            _redo.Add(cmd);
        }

        while (_undo.Count < targetUndoCount && _redo.Count > 0)
        {
            var cmd = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            cmd.Redo();
            _undo.Add(cmd);
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Push(IUndoableCommand command)
    {
        _undo.Add(command);
        if (_undo.Count > MaxHistoryDepth) _undo.RemoveAt(0);
        _redo.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var cmd = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        cmd.Undo();
        _redo.Add(cmd);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var cmd = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        cmd.Redo();
        _undo.Add(cmd);
        if (_undo.Count > MaxHistoryDepth) _undo.RemoveAt(0);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteCurrentState()
    {
        if (_undo.Count == 0) return;
        var cmd = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        cmd.Undo();
        _redo.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
