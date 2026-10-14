using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Shell overlay manager contract for showing/hiding floating overlays.
/// </summary>
public interface IOverlayRegistry
{
    void ShowOverlay(string id);
    void HideOverlay(string id);
    void ToggleOverlay(string id);
}

public enum FontPackageCategory
{
    EastAsia,
    SouthAsia,
    MiddleEast,
    Display,
    Handwriting,
    Monospace,
    Serif,
    SansSerif
}

public class FontFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string RelativeUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class FontPackageInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public string FlagEmoji { get; set; } = "🌐";
    public string Region { get; set; } = string.Empty;
    public FontPackageCategory Category { get; set; } = FontPackageCategory.EastAsia;
    public string Description { get; set; } = string.Empty;
    public string SampleText { get; set; } = string.Empty;
    public List<string> SupportedLanguages { get; set; } = new();
    public List<string> IncludedFontFamilies { get; set; } = new();
    public List<FontFileItem> Files { get; set; } = new();
    public long TotalEstimatedSizeBytes { get; set; }
    public string FormattedSize => FormatBytes(TotalEstimatedSizeBytes);
    public bool IsBundledByDefault { get; set; }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 KB";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

public interface IFontPackageService
{
    event Action? FontLibraryChanged;
    IReadOnlyList<FontPackageInfo> GetAllPackages();
    string GetUserFontDirectory();
    bool IsPackageInstalled(FontPackageInfo package);
    Task<bool> DownloadPackageAsync(FontPackageInfo package, IProgress<double>? progress = null, CancellationToken ct = default);
}
