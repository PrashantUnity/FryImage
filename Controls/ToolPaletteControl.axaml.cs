using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class ToolPaletteControl : UserControl
{
    public ToolPaletteControl()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual sourceVisual)
        {
            var button = sourceVisual.FindAncestorOfType<Button>();
            if (button?.ContextMenu != null && button.ContextMenu.IsOpen)
            {
                // Re-clicking a tool button whose flyout is open closes it (#334d378f, #4932).
                button.ContextMenu.Close();
                e.Handled = true;
            }
        }
    }

    private void OnCornerTrianglePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            var button = control.FindAncestorOfType<Button>();
            if (button != null)
            {
                if (button.ContextMenu != null && button.ContextMenu.IsOpen)
                {
                    button.ContextMenu.Close();
                    e.Handled = true;
                    return;
                }

                // Holding or clicking the corner flyout selects the tool it shows, as a click would (#4453).
                button.Command?.Execute(button.CommandParameter);
                if (button.ContextMenu != null)
                {
                    button.ContextMenu.Open(button);
                    e.Handled = true;
                }
            }
        }
    }
}
