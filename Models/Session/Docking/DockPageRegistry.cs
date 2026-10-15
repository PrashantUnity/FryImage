using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Session.Docking;

/// <summary>Describes one dockable page (tab). <see cref="Icon"/> is a Material icon kind name.</summary>
public sealed record DockPageDescriptor(string Id, string Title, string Icon, string Family);

/// <summary>
/// The dockable pages of the studio. Page ids match the tab names the rest of the editor already
/// uses (<c>SelectRightDockTab("Channels")</c>, <c>ActiveColorDockTab = "Swatches"</c>, …).
/// </summary>
public static class DockPageRegistry
{
    public const string LayersFamily = "Layers";
    public const string PropertiesFamily = "Properties";
    public const string ColorFamily = "Color";

    public static IReadOnlyList<DockPageDescriptor> All { get; } =
    [
        new("Layers", "Layers", "LayersTripleOutline", LayersFamily),
        new("Channels", "Channels", "TuneVariant", LayersFamily),
        new("Paths", "Paths", "VectorCurve", LayersFamily),
        new("History", "History", "History", LayersFamily),
        new("Actions", "Actions", "PlayBoxOutline", LayersFamily),
        new("Properties", "Properties", "TuneVertical", PropertiesFamily),
        new("Histogram", "Adjustments", "ChartHistogram", PropertiesFamily),
        new("Navigator", "Navigator", "CompassOutline", PropertiesFamily),
        new("Info", "Info", "InformationOutline", PropertiesFamily),
        new("Brushes", "Brushes", "BrushVariant", PropertiesFamily),
        new("Color", "Color", "PaletteOutline", ColorFamily),
        new("Swatches", "Swatches", "PaletteSwatchOutline", ColorFamily),
        new("Gradients", "Gradients", "GradientHorizontal", ColorFamily),
    ];

    public static IReadOnlyList<string> AllIds { get; } = All.Select(p => p.Id).ToArray();

    public static DockPageDescriptor? Find(string? id) =>
        id == null ? null : All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    public static IEnumerable<string> PagesInFamily(string family) =>
        All.Where(p => p.Family == family).Select(p => p.Id);

    /// <summary>
    /// Maps a legacy panel name ("Layers", "Properties", "Color", "Color &amp; Swatches") to its family.
    /// </summary>
    public static string? FamilyOfPanel(string panelName) => panelName switch
    {
        "Layers" => LayersFamily,
        "Properties" => PropertiesFamily,
        "Color" or "Color & Swatches" => ColorFamily,
        _ => null
    };
}
