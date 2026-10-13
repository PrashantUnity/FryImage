using System;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Preferences;

/// <summary>
/// Design system theme manager for FryImage Studio (Photoshop color themes parity).
/// Supports Pro Dark, Pro Medium Gray, Pro Light, Soft Dark, Soft Light, and Solarized Dark palettes.
/// </summary>
public static class StudioThemeManager
{
    public enum StudioTheme
    {
        ProDark,
        ProMedium,
        ProLight,
        SoftDark,
        SoftLight,
        SolarizedDark
    }

    public static StudioTheme CurrentTheme { get; private set; } = StudioTheme.ProDark;

    public static void ApplyTheme(Avalonia.Controls.Control rootControl, StudioTheme theme)
    {
        CurrentTheme = theme;
        var res = rootControl.Resources;

        switch (theme)
        {
            case StudioTheme.ProDark:
                SetBrush(res, "PsChromeBrush", "#1F1F1F");
                SetBrush(res, "PsOptionsBrush", "#282828");
                SetBrush(res, "PsToolbarBrush", "#2D2D2D");
                SetBrush(res, "PsPanelBrush", "#2A2A2B");
                SetBrush(res, "PsPanelHeaderBrush", "#242425");
                SetBrush(res, "PsCanvasBrush", "#1E1E1E");
                SetBrush(res, "PsFieldBrush", "#1E1E1E");
                SetBrush(res, "PsBorderBrush", "#181818");
                SetBrush(res, "PsBorderSubtleBrush", "#333333");
                SetBrush(res, "PsTextBrush", "#DEDEDE");
                SetBrush(res, "PsTextDimBrush", "#B2B2B2");
                SetBrush(res, "PsTextFaintBrush", "#808080");
                SetBrush(res, "PsAccentBrush", "#378EF0");
                SetBrush(res, "PsAccentHoverBrush", "#4FA3F7");
                SetBrush(res, "PsHoverBrush", "#383838");
                break;

            case StudioTheme.ProMedium:
                SetBrush(res, "PsChromeBrush", "#383838");
                SetBrush(res, "PsOptionsBrush", "#484848");
                SetBrush(res, "PsToolbarBrush", "#4F4F4F");
                SetBrush(res, "PsPanelBrush", "#535353");
                SetBrush(res, "PsPanelHeaderBrush", "#464646");
                SetBrush(res, "PsCanvasBrush", "#282828");
                SetBrush(res, "PsFieldBrush", "#323232");
                SetBrush(res, "PsBorderBrush", "#2E2E2E");
                SetBrush(res, "PsBorderSubtleBrush", "#5E5E5E");
                SetBrush(res, "PsTextBrush", "#EAEAEA");
                SetBrush(res, "PsTextDimBrush", "#C2C2C2");
                SetBrush(res, "PsTextFaintBrush", "#929292");
                SetBrush(res, "PsAccentBrush", "#378EF0");
                SetBrush(res, "PsAccentHoverBrush", "#4FA3F7");
                SetBrush(res, "PsHoverBrush", "#606060");
                break;

            case StudioTheme.ProLight:
                SetBrush(res, "PsChromeBrush", "#E0E0E0");
                SetBrush(res, "PsOptionsBrush", "#EBEBEB");
                SetBrush(res, "PsToolbarBrush", "#ECECEC");
                SetBrush(res, "PsPanelBrush", "#F0F0F0");
                SetBrush(res, "PsPanelHeaderBrush", "#E6E6E6");
                SetBrush(res, "PsCanvasBrush", "#C8C8C8");
                SetBrush(res, "PsFieldBrush", "#FFFFFF");
                SetBrush(res, "PsBorderBrush", "#C0C0C0");
                SetBrush(res, "PsBorderSubtleBrush", "#D0D0D0");
                SetBrush(res, "PsTextBrush", "#181818");
                SetBrush(res, "PsTextDimBrush", "#484848");
                SetBrush(res, "PsTextFaintBrush", "#787878");
                SetBrush(res, "PsAccentBrush", "#0B66C3");
                SetBrush(res, "PsAccentHoverBrush", "#2380DF");
                SetBrush(res, "PsHoverBrush", "#D8D8D8");
                break;

            case StudioTheme.SoftDark:
                SetBrush(res, "PsChromeBrush", "#1B1D24");
                SetBrush(res, "PsOptionsBrush", "#22252F");
                SetBrush(res, "PsToolbarBrush", "#272A35");
                SetBrush(res, "PsPanelBrush", "#282B37");
                SetBrush(res, "PsPanelHeaderBrush", "#21242E");
                SetBrush(res, "PsCanvasBrush", "#16171E");
                SetBrush(res, "PsFieldBrush", "#1A1C23");
                SetBrush(res, "PsBorderBrush", "#181A22");
                SetBrush(res, "PsBorderSubtleBrush", "#363A4B");
                SetBrush(res, "PsTextBrush", "#E2E6F2");
                SetBrush(res, "PsTextDimBrush", "#AAB2C8");
                SetBrush(res, "PsTextFaintBrush", "#768098");
                SetBrush(res, "PsAccentBrush", "#7C8CF8");
                SetBrush(res, "PsAccentHoverBrush", "#98A5FF");
                SetBrush(res, "PsHoverBrush", "#353948");
                break;

            case StudioTheme.SoftLight:
                SetBrush(res, "PsChromeBrush", "#E4E7EE");
                SetBrush(res, "PsOptionsBrush", "#ECEEF4");
                SetBrush(res, "PsToolbarBrush", "#EFF2F7");
                SetBrush(res, "PsPanelBrush", "#F4F6FA");
                SetBrush(res, "PsPanelHeaderBrush", "#E8EAF1");
                SetBrush(res, "PsCanvasBrush", "#D3D7E2");
                SetBrush(res, "PsFieldBrush", "#FFFFFF");
                SetBrush(res, "PsBorderBrush", "#C8CCD8");
                SetBrush(res, "PsBorderSubtleBrush", "#D8DCE6");
                SetBrush(res, "PsTextBrush", "#1D212D");
                SetBrush(res, "PsTextDimBrush", "#4C5368");
                SetBrush(res, "PsTextFaintBrush", "#7B839B");
                SetBrush(res, "PsAccentBrush", "#5A6AE8");
                SetBrush(res, "PsAccentHoverBrush", "#7482F2");
                SetBrush(res, "PsHoverBrush", "#E0E4EE");
                break;

            case StudioTheme.SolarizedDark:
                SetBrush(res, "PsChromeBrush", "#00212B");
                SetBrush(res, "PsOptionsBrush", "#042A36");
                SetBrush(res, "PsToolbarBrush", "#073642");
                SetBrush(res, "PsPanelBrush", "#073642");
                SetBrush(res, "PsPanelHeaderBrush", "#002B36");
                SetBrush(res, "PsCanvasBrush", "#001820");
                SetBrush(res, "PsFieldBrush", "#002B36");
                SetBrush(res, "PsBorderBrush", "#001E27");
                SetBrush(res, "PsBorderSubtleBrush", "#0D4958");
                SetBrush(res, "PsTextBrush", "#93A1A1");
                SetBrush(res, "PsTextDimBrush", "#657B83");
                SetBrush(res, "PsTextFaintBrush", "#586E75");
                SetBrush(res, "PsAccentBrush", "#268BD2");
                SetBrush(res, "PsAccentHoverBrush", "#2AA198");
                SetBrush(res, "PsHoverBrush", "#0A4656");
                break;
        }
    }

    private static void SetBrush(Avalonia.Controls.IResourceDictionary dict, string key, string hex)
    {
        if (Color.TryParse(hex, out var color))
        {
            dict[key] = new SolidColorBrush(color);
        }
    }
}
