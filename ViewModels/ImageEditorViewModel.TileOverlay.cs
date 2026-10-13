using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Seamless Pattern Preview & Developer Tile Grid State (Photoshop Parity) ──
    [ObservableProperty]
    private bool _isPatternPreviewEnabled;

    [ObservableProperty]
    private bool _isTileGridOverlayEnabled;

    [RelayCommand]
    public void TogglePatternPreview()
    {
        IsPatternPreviewEnabled = !IsPatternPreviewEnabled;
        StatusMessage = IsPatternPreviewEnabled
            ? "Pattern Preview enabled (3×3 seamless tiling repeat active)."
            : "Pattern Preview disabled.";
    }

    [RelayCommand]
    public void ToggleTileGridOverlay()
    {
        IsTileGridOverlayEnabled = !IsTileGridOverlayEnabled;
        StatusMessage = IsTileGridOverlayEnabled
            ? "Tile Grid Overlay enabled (showing sparse storage tile boundaries)."
            : "Tile Grid Overlay disabled.";
    }
}
