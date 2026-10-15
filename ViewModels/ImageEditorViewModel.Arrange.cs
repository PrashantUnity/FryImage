using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Models.Session;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // Active tracked floating document windows
    private readonly Dictionary<Guid, Window> _floatingDocWindows = new();
    private readonly Dictionary<string, Window> _floatingPaletteWindows = new();

    // ── Window > Arrange Commands ───────────────────────────────────────────

    [RelayCommand]
    public void ConsolidateAllToTabs()
    {
        SplitLayoutMode = "None";
        SecondaryDocument = null;

        foreach (var doc in Documents)
        {
            doc.IsFloating = false;
            doc.IsSplitView = false;
        }

        // Close all floating document windows
        foreach (var win in _floatingDocWindows.Values.ToList())
        {
            try { win.Close(); } catch { }
        }
        _floatingDocWindows.Clear();

        if (ActiveDocument != null)
        {
            SwitchDocument(ActiveDocument);
        }
        else if (Documents.Count > 0)
        {
            SwitchDocument(Documents[0]);
        }

        StatusMessage = "Consolidated all documents to tabs.";
    }

    [RelayCommand]
    public void FloatInWindow(StudioDocument? doc = null)
    {
        doc ??= ActiveDocument;
        if (doc == null) return;

        doc.IsFloating = true;
        OpenFloatingDocumentWindow(doc);
        StatusMessage = $"Floated '{doc.Title}' in window.";
    }

    [RelayCommand]
    public void FloatAllInWindows()
    {
        SplitLayoutMode = "None";
        foreach (var doc in Documents)
        {
            doc.IsFloating = true;
            OpenFloatingDocumentWindow(doc);
        }
        StatusMessage = $"Floated all {Documents.Count} documents in windows.";
    }

    [RelayCommand]
    public void TwoUpVertical()
    {
        EnsureMultipleDocumentsForSplit();
        SplitLayoutMode = "TwoUpVertical";
        if (Documents.Count > 1)
        {
            SecondaryDocument = Documents.FirstOrDefault(d => d != ActiveDocument) ?? Documents[1];
        }
        StatusMessage = "Arranged viewports: 2-up Vertical.";
    }

    [RelayCommand]
    public void TwoUpHorizontal()
    {
        EnsureMultipleDocumentsForSplit();
        SplitLayoutMode = "TwoUpHorizontal";
        if (Documents.Count > 1)
        {
            SecondaryDocument = Documents.FirstOrDefault(d => d != ActiveDocument) ?? Documents[1];
        }
        StatusMessage = "Arranged viewports: 2-up Horizontal.";
    }

    [RelayCommand]
    public void FourUp()
    {
        EnsureMultipleDocumentsForSplit();
        SplitLayoutMode = "FourUp";
        StatusMessage = "Arranged viewports: 4-up Grid.";
    }

    [RelayCommand]
    public void TileAllHorizontally() => TwoUpHorizontal();

    [RelayCommand]
    public void TileAllVertically() => TwoUpVertical();

    [RelayCommand]
    public void Cascade()
    {
        double x = 100, y = 100;
        foreach (var win in _floatingDocWindows.Values)
        {
            win.Position = new PixelPoint((int)x, (int)y);
            win.Activate();
            x += 35;
            y += 35;
        }
        StatusMessage = "Cascaded floating document windows.";
    }

    [RelayCommand]
    public void MatchZoom()
    {
        if (ActiveDocument == null) return;
        double targetZoom = ActiveDocument.ZoomLevel;
        foreach (var doc in Documents)
        {
            doc.ZoomLevel = targetZoom;
        }
        ZoomLevel = targetZoom;
        CanvasControl?.SetZoom(targetZoom);
        StatusMessage = $"Matched zoom ({targetZoom * 100:0.#}%) across all documents.";
    }

    [RelayCommand]
    public void MatchLocation()
    {
        if (ActiveDocument == null) return;
        Point targetPan = ActiveDocument.PanOffset;
        foreach (var doc in Documents)
        {
            doc.PanOffset = targetPan;
        }
        StatusMessage = "Matched canvas pan location across all documents.";
    }

    [RelayCommand]
    public void MatchRotation()
    {
        if (ActiveDocument == null) return;
        double targetRot = ActiveDocument.RotationDegrees;
        foreach (var doc in Documents)
        {
            doc.RotationDegrees = targetRot;
        }
        StatusMessage = $"Matched rotation ({targetRot:0.#}°) across all documents.";
    }

    [RelayCommand]
    public void MatchAll()
    {
        MatchZoom();
        MatchLocation();
        MatchRotation();
        StatusMessage = "Matched zoom, location, and rotation across all documents.";
    }

    // ── Unity-Style Docking Commands ────────────────────────────────────────

    [RelayCommand]
    public void TearOffPanel(string panelName)
    {
        SetPanelPlacement(panelName, DockZone.Floating);
    }

    [RelayCommand]
    public void RedockPanel(string panelName)
    {
        SetPanelPlacement(panelName, DockZone.Right);
    }

    [RelayCommand]
    public void ResetEssentialsWorkspace()
    {
        ApplyDefaultWorkspace();
        ConsolidateAllToTabs();
        StatusMessage = "Reset to Essentials Studio workspace.";
    }

    // ── Window Management Helpers ───────────────────────────────────────────

    private void EnsureMultipleDocumentsForSplit()
    {
        if (Documents.Count < 2)
        {
            CreateNewDocumentTab();
        }
    }

    private void OpenFloatingDocumentWindow(StudioDocument doc)
    {
        if (_floatingDocWindows.TryGetValue(doc.Id, out var existing))
        {
            existing.Activate();
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            var floatingWin = new Views.FloatingDocumentWindow
            {
                DataContext = this,
                TargetDocument = doc,
                Title = $"{doc.Title} - FryImage Studio",
                Width = 840,
                Height = 600
            };

            floatingWin.Closed += (_, _) =>
            {
                _floatingDocWindows.Remove(doc.Id);
                doc.IsFloating = false;
            };

            _floatingDocWindows[doc.Id] = floatingWin;
            floatingWin.Show();
        }
    }

    private void OpenFloatingPalette(string panelKey)
    {
        if (_floatingPaletteWindows.TryGetValue(panelKey, out var existing))
        {
            existing.Activate();
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            var paletteWin = new Views.FloatingPaletteWindow
            {
                DataContext = this,
                PanelKey = panelKey,
                Title = panelKey,
                Width = panelKey == "Tools" ? 80 : 330,
                Height = panelKey == "Tools" ? 640 : 480
            };

            paletteWin.Closed += (_, _) =>
            {
                _floatingPaletteWindows.Remove(panelKey);
                switch (panelKey)
                {
                    case "Layers": IsLayersDocked = true; break;
                    case "Properties": IsPropertiesDocked = true; break;
                    case "Color & Swatches": IsColorDocked = true; break;
                    case "Tools": IsToolsDocked = true; break;
                }
            };

            _floatingPaletteWindows[panelKey] = paletteWin;
            paletteWin.Show();
        }
    }

    private void CloseFloatingPalette(string panelKey)
    {
        if (_floatingPaletteWindows.TryGetValue(panelKey, out var win))
        {
            try { win.Close(); } catch { }
            _floatingPaletteWindows.Remove(panelKey);
        }
    }
}
