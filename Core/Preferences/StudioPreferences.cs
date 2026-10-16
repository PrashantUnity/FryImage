using System;
using System.IO;
using System.Text.Json;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Preferences;

/// <summary>
/// Persistent studio preferences across application restarts (Studio Parity #3762, #3870, #3863).
/// Remembers:
/// - View › Extras, View › Show (Pixel Grid, Guides, Grid, Document Bounds)
/// - View › Snap To (Guides, Grid, Layers, Bounds)
/// - Workspace › Enable Narrow Options Bar
/// - Type › Missing Glyph Protection (script fallback protection)
/// </summary>
public sealed class StudioPreferences
{
    private static readonly string PrefsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FryImage");

    private static readonly string PrefsFilePath = Path.Combine(PrefsDirectory, "studio_preferences.json");

    public bool ViewExtras { get; set; } = true;
    public bool ShowPixelGrid { get; set; } = true;
    public bool ShowGuides { get; set; } = true;
    public bool LockGuides { get; set; } = false;
    public bool SnapEnabled { get; set; } = true;
    public bool SnapToGuides { get; set; } = true;
    public bool SnapToGrid { get; set; } = true;
    public bool SnapToLayers { get; set; } = true;
    public bool SnapToDocumentBounds { get; set; } = true;
    public bool EnableNarrowOptionsBar { get; set; } = false;
    public bool AutoCollapseIconicPanels { get; set; } = false;
    public int CanvasPasteboardColorIndex { get; set; } = 0; // 0=Default, 1=Black, 2=Dark Gray, 3=Medium Gray, 4=Light Gray, 5=Custom
    public bool MissingGlyphProtection { get; set; } = true;
    public int DefaultBitsPerChannel { get; set; } = 8; // 8, 16, or 32
    public string CurrentTheme { get; set; } = "ProDark";
    public string SelectedTool { get; set; } = "select";

    /// <summary>Saved dock layout tree (tab groups, splits, sizes); null means the Essentials workspace.</summary>
    public string? DockLayoutJson { get; set; }

    /// <summary>Preferences › Performance › Low-Latency Drawing (vsync off / opt-in) (#1343, #5274).</summary>
    public bool LowLatencyDrawing { get; set; } = false;

    /// <summary>Preferences › Tools › Snap Vector Tools and Transforms to Pixel Grid (#5751, #5841).</summary>
    public bool SnapVectorToolsAndTransformsToPixelGrid { get; set; } = true;

    /// <summary>Preferences › Tools › Zoom with Scroll Wheel (Studio Parity #3695).</summary>
    public bool ZoomWithScrollWheel { get; set; } = false;

    /// <summary>Preferences › Units & Rulers › Point/Pica Size: points per inch (PostScript = 72.0, Traditional = 72.27) (#82261398, #5756, #5757).</summary>
    public double PointsPerInch { get; set; } = 72.0;

    public static double PointsToPixels(double pt, double dpi, double ppi = 72.0) => pt / Math.Max(0.001, ppi) * dpi;
    public static double PixelsToPoints(double px, double dpi, double ppi = 72.0) => px / Math.Max(1.0, dpi) * ppi;
    public static double PicasToPixels(double pica, double dpi, double ppi = 72.0) => pica / Math.Max(0.001, ppi) * 12.0 * dpi;
    public static double PixelsToPicas(double px, double dpi, double ppi = 72.0) => px / Math.Max(1.0, dpi) * ppi / 12.0;

    private static StudioPreferences? _instance;
    public static StudioPreferences Instance => _instance ??= Load();

    public static StudioPreferences Load()
    {
        try
        {
            if (File.Exists(PrefsFilePath))
            {
                string json = File.ReadAllText(PrefsFilePath);
                var loaded = JsonSerializer.Deserialize<StudioPreferences>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Fall back cleanly to defaults if corrupted or inaccessible
        }

        return new StudioPreferences();
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(PrefsDirectory))
            {
                Directory.CreateDirectory(PrefsDirectory);
            }
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PrefsFilePath, json);
        }
        catch
        {
            // Ignore write failures gracefully
        }
    }
}
