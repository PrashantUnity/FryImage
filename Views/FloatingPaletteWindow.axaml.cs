using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.ImageEditor.Controls;
using PdfEditorApp.Plugins.ImageEditor.Controls.Panels;

namespace PdfEditorApp.Plugins.ImageEditor.Views;

public partial class FloatingPaletteWindow : Window
{
    private string _panelKey = "Palette";

    public string PanelKey
    {
        get => _panelKey;
        set
        {
            _panelKey = value;
            Title = $"{value} - FryImage Studio";
            TitleTextBlock.Text = value;
            UpdatePanelContent();
        }
    }

    public FloatingPaletteWindow()
    {
        InitializeComponent();
    }

    private void UpdatePanelContent()
    {
        switch (_panelKey)
        {
            case "Layers":
                PanelContentHost.Content = new LayersDockPanel();
                break;
            case "Properties":
                PanelContentHost.Content = new PropertiesDockPanel();
                break;
            case "Color & Swatches":
                PanelContentHost.Content = new ColorDockPanel();
                break;
            case "Tools":
                PanelContentHost.Content = new ToolPaletteControl();
                break;
        }
    }

    private void OnRedockClicked(object? sender, RoutedEventArgs e)
    {
        OnDockRightClicked(sender, e);
    }

    private void OnDockLeftClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm)
        {
            vm.SetPanelPlacement(PanelKey, Models.Session.DockZone.Left);
        }
        Close();
    }

    private void OnDockRightClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm)
        {
            vm.SetPanelPlacement(PanelKey, Models.Session.DockZone.Right);
        }
        Close();
    }

    private void OnDockBottomClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm)
        {
            vm.SetPanelPlacement(PanelKey, Models.Session.DockZone.Bottom);
        }
        Close();
    }
}
