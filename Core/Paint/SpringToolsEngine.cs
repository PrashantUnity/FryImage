using System;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Paint;

/// <summary>
/// Spring-loaded tool shortcuts engine (Studio parity #2672).
/// Holding down a tool shortcut letter (V, M, L, W, C, I, J, B, E, G, T, U, H, Z, etc.)
/// temporarily switches to that tool. If the key is held for > 300ms or used on the canvas,
/// releasing the key springs back to the previously active tool.
/// A quick tap with no canvas usage remains a permanent switch.
/// Clean-room implementation matching Studio spring-loaded tool keys specification.
/// </summary>
public sealed class SpringToolsEngine
{
    public const double SpringHoldThresholdSeconds = 0.30; // 300ms

    public sealed class SpringState
    {
        public string PreviousTool { get; set; } = "select";
        public string TemporaryTool { get; set; } = "select";
        public Key Key { get; set; }
        public DateTime PressTime { get; set; }
        public bool UsedOnCanvas { get; set; }
    }

    private SpringState? _activeSpring;

    public bool HasActiveSpring => _activeSpring != null;
    public string? CurrentTemporaryTool => _activeSpring?.TemporaryTool;

    /// <summary>
    /// Called on KeyDown when a tool shortcut is pressed.
    /// </summary>
    public void OnToolKeyDown(Key key, string currentTool, string newTool)
    {
        if (currentTool == newTool)
        {
            // Already using this tool, don't spring
            _activeSpring = null;
            return;
        }

        if (_activeSpring != null)
        {
            // Already holding another tool; update temporary tool and mark used
            _activeSpring.TemporaryTool = newTool;
            _activeSpring.Key = key;
            _activeSpring.UsedOnCanvas = true;
            return;
        }

        _activeSpring = new SpringState
        {
            PreviousTool = currentTool,
            TemporaryTool = newTool,
            Key = key,
            PressTime = DateTime.UtcNow,
            UsedOnCanvas = false
        };
    }

    /// <summary>
    /// Called when the canvas receives a pointer press or drag.
    /// </summary>
    public void NoteCanvasUsage()
    {
        if (_activeSpring != null)
        {
            _activeSpring.UsedOnCanvas = true;
        }
    }

    /// <summary>
    /// Called on KeyUp. If the held key meets spring criteria, returns the previous tool to restore;
    /// otherwise returns null (indicating permanent switch).
    /// </summary>
    public string? OnToolKeyUp(Key key, string currentTool)
    {
        if (_activeSpring == null || _activeSpring.Key != key)
            return null;

        var spring = _activeSpring;
        _activeSpring = null;

        // If the tool was changed in another way, don't revert
        if (currentTool != spring.TemporaryTool)
            return null;

        double holdDuration = (DateTime.UtcNow - spring.PressTime).TotalSeconds;

        if (holdDuration >= SpringHoldThresholdSeconds || spring.UsedOnCanvas)
        {
            // Hold criteria met: spring back to previous tool!
            return spring.PreviousTool;
        }

        // Tap: permanent switch
        return null;
    }

    public void Cancel()
    {
        _activeSpring = null;
    }
}
