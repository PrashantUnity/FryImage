using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class StudioMenuBar : UserControl
{
    public StudioMenuBar()
    {
        InitializeComponent();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Do not intercept if click originated on an interactive child control
        if (e.Source is Visual sourceVisual)
        {
            if (sourceVisual.FindAncestorOfType<Button>(includeSelf: true) != null ||
                sourceVisual.FindAncestorOfType<MenuItem>(includeSelf: true) != null ||
                sourceVisual.FindAncestorOfType<TextBox>(includeSelf: true) != null ||
                sourceVisual.FindAncestorOfType<ComboBox>(includeSelf: true) != null)
            {
                return;
            }
        }

        var window = VisualRoot as Window ?? TopLevel.GetTopLevel(this) as Window;
        if (window == null)
            return;

        if (e.ClickCount == 2)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            e.Handled = true;
        }
        else
        {
            window.BeginMoveDrag(e);
            e.Handled = true;
        }
    }
}
