using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Dialogs;

public partial class RenameLayerDialog : UserControl
{
    public RenameLayerDialog()
    {
        InitializeComponent();

        PropertyChanged += (s, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    NameTextBox?.Focus();
                    NameTextBox?.SelectAll();
                }, DispatcherPriority.Input);
            }
        };

        KeyDown += OnDialogKeyDown;
    }

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ImageEditorViewModel vm) return;

        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            bool cycleUp = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            vm.RenameNextLayer(cycleUp);
            Dispatcher.UIThread.Post(() =>
            {
                NameTextBox?.Focus();
                NameTextBox?.SelectAll();
            }, DispatcherPriority.Input);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            vm.ConfirmRenameLayerDialogCommand.Execute(null);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            vm.CancelRenameLayerDialogCommand.Execute(null);
        }
    }
}
