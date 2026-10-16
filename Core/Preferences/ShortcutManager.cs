using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Preferences;

/// <summary>
/// Manages application keyboard shortcut bindings, conflict resolution, and reserved system key protection (#5349, #5801).
/// </summary>
public sealed class ShortcutManager
{
    private static readonly HashSet<string> MacReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cmd+Tab",
        "Cmd+`",
        "Cmd+Opt+Esc",
        "Meta+Tab",
        "Meta+`",
        "Meta+Alt+Esc"
    };

    private static readonly HashSet<string> WinLinuxReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alt+Tab",
        "Alt+F4",
        "Ctrl+Alt+Delete",
        "Ctrl+Alt+Del",
        "Ctrl+Esc",
        "Ctrl+Shift+Esc"
    };

    private readonly Dictionary<string, string> _commandToKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _keyToCommand = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _originalCommandToKey = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether a given key combination is reserved by the host operating system.
    /// </summary>
    public static bool IsReservedKey(string keyGesture)
    {
        if (string.IsNullOrWhiteSpace(keyGesture)) return false;

        string normalized = NormalizeGesture(keyGesture);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return MacReservedKeys.Contains(normalized);
        }
        else
        {
            return WinLinuxReservedKeys.Contains(normalized);
        }
    }

    /// <summary>
    /// Registers or updates a command's shortcut.
    /// </summary>
    public bool TryAssignShortcut(
        string commandId,
        string keyGesture,
        out string? conflictCommandId,
        out string? errorMessage)
    {
        conflictCommandId = null;
        errorMessage = null;

        string normalized = NormalizeGesture(keyGesture);
        if (IsReservedKey(normalized))
        {
            errorMessage = "This shortcut is reserved by the operating system.";
            return false;
        }

        if (!_originalCommandToKey.ContainsKey(commandId))
        {
            _originalCommandToKey[commandId] = _commandToKey.TryGetValue(commandId, out var existing) ? existing : "";
        }

        if (_keyToCommand.TryGetValue(normalized, out var existingHolder) && !string.Equals(existingHolder, commandId, StringComparison.OrdinalIgnoreCase))
        {
            conflictCommandId = existingHolder;
            return false;
        }

        SetBinding(commandId, normalized);
        return true;
    }

    /// <summary>
    /// Accepts a conflicting shortcut for commandId, removing it from conflictCommandId and focusing it.
    /// </summary>
    public void AcceptAndGoToConflict(string commandId, string keyGesture, string conflictCommandId)
    {
        string normalized = NormalizeGesture(keyGesture);
        _commandToKey.Remove(conflictCommandId);
        _keyToCommand.Remove(normalized);
        SetBinding(commandId, normalized);
    }

    /// <summary>
    /// Reverts the command's shortcut back to its original value before editing.
    /// </summary>
    public void UndoChanges(string commandId)
    {
        if (_originalCommandToKey.TryGetValue(commandId, out var origKey))
        {
            if (string.IsNullOrEmpty(origKey))
            {
                if (_commandToKey.TryGetValue(commandId, out var curKey))
                {
                    _keyToCommand.Remove(curKey);
                    _commandToKey.Remove(commandId);
                }
            }
            else
            {
                SetBinding(commandId, origKey);
            }
        }
    }

    public string? GetShortcut(string commandId) => _commandToKey.TryGetValue(commandId, out var key) ? key : null;

    private void SetBinding(string commandId, string key)
    {
        if (_commandToKey.TryGetValue(commandId, out var oldKey))
        {
            _keyToCommand.Remove(oldKey);
        }
        _commandToKey[commandId] = key;
        _keyToCommand[key] = commandId;
    }

    private static string NormalizeGesture(string keyGesture)
    {
        return keyGesture.Trim()
            .Replace(" ", "")
            .Replace("Command", "Cmd")
            .Replace("Control", "Ctrl")
            .Replace("Option", "Opt");
    }
}
