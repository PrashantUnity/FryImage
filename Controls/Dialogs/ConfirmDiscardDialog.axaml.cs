using Avalonia.Controls;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Dialogs;

public partial class ConfirmDiscardDialog : UserControl
{
    public ConfirmDiscardDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm && vm.IsConfirmingDiscard)
        {
            if (e.Key is Key.C or Key.Escape)
            {
                vm.CancelDiscardCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.S && (e.KeyModifiers & KeyModifiers.Control) == 0 && (e.KeyModifiers & KeyModifiers.Meta) == 0)
            {
                vm.SaveAndDiscardCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.D)
            {
                vm.ConfirmDiscardCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
