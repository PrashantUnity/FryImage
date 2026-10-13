using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class ToolPaletteControl : UserControl
{
    public ToolPaletteControl()
    {
        InitializeComponent();
    }

    private void OnCornerTrianglePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            var button = control.FindAncestorOfType<Button>();
            if (button != null)
            {
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
