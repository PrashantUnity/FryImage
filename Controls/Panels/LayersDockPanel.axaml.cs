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
            vm.OpenLayerStyleDialogCommand.Execute(null);
        }
    }
}

