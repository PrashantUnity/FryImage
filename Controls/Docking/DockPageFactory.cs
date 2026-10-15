using System;
using Avalonia.Controls;
using Material.Icons;
using PdfEditorApp.Plugins.ImageEditor.Controls.Panels.Pages;
using PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Docking;

/// <summary>Creates the view for each dockable page id.</summary>
public static class DockPageFactory
{
    public static Control? Create(string pageId) => pageId switch
    {
        "Layers" => new LayersPage(),
        "Channels" => new ChannelsPage(),
        "Paths" => new PathsPage(),
        "History" => new HistoryPage(),
        "Actions" => new ActionsPage(),
        "Properties" => new PropertiesPage(),
        "Histogram" => new AdjustmentsPage(),
        "Navigator" => new NavigatorPage(),
        "Info" => new InfoPage(),
        "Brushes" => new BrushesPage(),
        "Color" => new ColorPage(),
        "Swatches" => new SwatchesPage(),
        "Gradients" => new GradientsPage(),
        _ => null
    };

    public static MaterialIconKind IconOf(DockPageDescriptor page) =>
        TryParseIcon(page.Icon, out var kind) ? kind : MaterialIconKind.TuneVertical;

    public static bool TryParseIcon(string name, out MaterialIconKind kind) => Enum.TryParse(name, out kind);

    /// <summary>Removes a control from whatever pane currently hosts it so it can be re-parented.</summary>
    public static void Detach(Control control)
    {
        switch (control.Parent)
        {
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, control):
                contentControl.Content = null;
                break;
        }
    }
}
