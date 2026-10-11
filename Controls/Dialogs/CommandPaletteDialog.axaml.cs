using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Dialogs;

public partial class CommandPaletteDialog : UserControl
{
    public CommandPaletteDialog()
    {
        InitializeComponent();
        PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(IsVisible) && IsVisible)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SearchBox?.Focus();
                    SearchBox?.SelectAll();
                }, DispatcherPriority.Loaded);
            }
        };
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ImageEditorViewModel vm) return;

        switch (e.Key)
        {
            case Key.Down:
                vm.SelectNextCommandPaletteItem();
                if (vm.SelectedStudioCommand != null)
                {
                    CommandListBox?.ScrollIntoView(vm.SelectedStudioCommand);
                }
                e.Handled = true;
                break;

            case Key.Up:
                vm.SelectPreviousCommandPaletteItem();
                if (vm.SelectedStudioCommand != null)
                {
                    CommandListBox?.ScrollIntoView(vm.SelectedStudioCommand);
                }
                e.Handled = true;
                break;

            case Key.Enter:
                vm.ExecuteSelectedCommandPaletteItem();
                e.Handled = true;
                break;

            case Key.Escape:
                vm.CloseCommandPalette();
                e.Handled = true;
                break;
        }
    }

    private void CommandListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm)
        {
            vm.ExecuteSelectedCommandPaletteItem();
        }
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Close if click was on the semi-transparent backdrop itself
        if (e.Source == sender && DataContext is ImageEditorViewModel vm)
        {
            vm.CloseCommandPalette();
        }
    }
}
