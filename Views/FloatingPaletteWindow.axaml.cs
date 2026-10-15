using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.ImageEditor.Controls;
using PdfEditorApp.Plugins.ImageEditor.Controls.Docking;
using PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

namespace PdfEditorApp.Plugins.ImageEditor.Views;

/// <summary>A torn-off dock page (or the tool palette) in its own window.</summary>
public partial class FloatingPaletteWindow : Window
{
    private string _panelKey = "Palette";

    /// <summary>A dock page id ("Layers", "Swatches", …) or "Tools".</summary>
    public string PanelKey
    {
        get => _panelKey;
        set
        {
            _panelKey = value;
            string title = DockPageRegistry.Find(value)?.Title ?? value;
            Title = $"{title} - FryImage Studio";
            TitleTextBlock.Text = title;
            PanelContentHost.Content = value == "Tools" ? new ToolPaletteControl() : DockPageFactory.Create(value);
        }
    }

    public FloatingPaletteWindow()
    {
        InitializeComponent();
    }

    private void OnDockBackClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm)
        {
            vm.RedockPanel(PanelKey);
        }

        if (IsVisible) Close();
    }
}
