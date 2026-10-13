using Avalonia.Controls;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Panels;

public partial class LayersDockPanel : UserControl
{
    public LayersDockPanel()
    {
        InitializeComponent();
    }

    private void OnLayersListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm)
        {
            var target = vm.SelectedElement;
            if (target != null)
            {
                if (target.HasBackgroundTraits)
                {
                    vm.ConvertBackgroundToLayerCommand.Execute(target);
                }
                else
                {
                    vm.OpenRenameLayerDialogCommand.Execute(target);
                }
            }
        }
    }

    private void OnBlendModePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is ComboBox combo && combo.ItemsSource != null)
        {
            var list = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<object>((System.Collections.IEnumerable)combo.ItemsSource));
            if (list.Count == 0) return;
            int idx = combo.SelectedIndex;
            if (idx < 0) idx = 0;
            if (e.Delta.Y > 0)
                idx = Math.Max(0, idx - 1);
            else if (e.Delta.Y < 0)
                idx = Math.Min(list.Count - 1, idx + 1);

            combo.SelectedIndex = idx;
            e.Handled = true;
        }
    }
}

