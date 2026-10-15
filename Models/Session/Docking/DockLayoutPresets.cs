using System.Linq;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

/// <summary>Ready-made workspace layouts (Window › Workspace).</summary>
public static class DockLayoutPresets
{
    /// <summary>Essentials: canvas, then a right column stacking Color / Properties / Layers (1 : 1.2 : 1.6).</summary>
    public static DockNode Essentials() =>
        Row(Host(),
            Column(ColorGroup(),
                Column(PropertiesGroup(), LayersGroup(), ratio: 1.2 / 2.8),
                ratio: 1.0 / 3.8),
            panelSize: 310);

    /// <summary>2-by-3: Layers column on the left, Properties over Color on the right.</summary>
    public static DockNode TwoByThree() =>
        Row(LayersGroup(),
            Row(Host(), Column(PropertiesGroup(), ColorGroup(), ratio: 0.6), panelSize: 310),
            panelSize: 280);

    /// <summary>4-split: Layers left, Color below the canvas, Properties right.</summary>
    public static DockNode FourSplit() =>
        Row(LayersGroup(),
            Row(Column(Host(), ColorGroup(), panelSize: 220), PropertiesGroup(), panelSize: 310),
            panelSize: 280);

    /// <summary>Tall dual sidebar: Layers over Properties on the left, Color on the right.</summary>
    public static DockNode Tall() =>
        Row(Column(LayersGroup(), PropertiesGroup(), ratio: 0.55),
            Row(Host(), ColorGroup(), panelSize: 300),
            panelSize: 300);

    /// <summary>Tabbed: every page as a tab of one right-hand group.</summary>
    public static DockNode Tabbed() =>
        Row(Host(), Group("Layers", DockPageRegistry.AllIds.ToArray()), panelSize: 320);

    private static DockGroupNode Host() => DockGroupNode.CreateDocumentHost();

    private static DockGroupNode LayersGroup() =>
        Group("Layers", DockPageRegistry.PagesInFamily(DockPageRegistry.LayersFamily).ToArray());

    private static DockGroupNode PropertiesGroup() =>
        Group("Properties", DockPageRegistry.PagesInFamily(DockPageRegistry.PropertiesFamily).ToArray());

    private static DockGroupNode ColorGroup() =>
        Group("Color", DockPageRegistry.PagesInFamily(DockPageRegistry.ColorFamily).ToArray());

    private static DockGroupNode Group(string active, params string[] pages)
    {
        var group = new DockGroupNode { ActivePage = active };
        group.Pages.AddRange(pages);
        return group;
    }

    private static DockSplitNode Row(DockNode first, DockNode second, double ratio = 0.5, double panelSize = DockLayoutOps.DefaultSidePanelWidth) =>
        new(DockOrientation.Horizontal, first, second) { Ratio = ratio, PanelSize = panelSize };

    private static DockSplitNode Column(DockNode first, DockNode second, double ratio = 0.5, double panelSize = DockLayoutOps.DefaultBottomPanelHeight) =>
        new(DockOrientation.Vertical, first, second) { Ratio = ratio, PanelSize = panelSize };
}
