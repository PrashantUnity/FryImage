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
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.Open(button);
                e.Handled = true;
            }
        }
    }
}
