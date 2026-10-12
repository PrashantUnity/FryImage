using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Duplicate Layer Modal (Photoshop Parity #3348 / #3440) ───────────────
    [ObservableProperty] private bool _isDuplicateLayerDialogOpen;
    [ObservableProperty] private string _duplicateLayerPrompt = "Duplicate Layer";
    [ObservableProperty] private string _duplicateLayerAsName = "";
    [ObservableProperty] private int _duplicateLayerDestinationIndex = 0; // 0 = Current Document, 1 = New Document
    [ObservableProperty] private string _duplicateLayerNewDocName = "Untitled-1";

    private List<CanvasElement>? _duplicateTargetElements;

    [RelayCommand]
    public void OpenDuplicateLayerDialog()
    {
        var targets = SelectedElements.Count > 0
            ? SelectedElements.ToList()
            : (Elements.Count > 0 ? new List<CanvasElement> { Elements[^1] } : new List<CanvasElement>());

        if (targets.Count == 0)
        {
            StatusMessage = "No layer to duplicate.";
            return;
        }

        _duplicateTargetElements = targets;

        if (targets.Count == 1)
        {
            var el = targets[0];
            DuplicateLayerPrompt = $"Duplicate: {el.DisplayName}";
            DuplicateLayerAsName = $"{el.DisplayName} copy";
        }
        else
        {
            DuplicateLayerPrompt = $"Duplicate {targets.Count} Selected Layers";
            DuplicateLayerAsName = $"{targets[0].DisplayName} copy";
        }

        DuplicateLayerDestinationIndex = 0;
        DuplicateLayerNewDocName = "Untitled-1";
        IsDuplicateLayerDialogOpen = true;
    }

    [RelayCommand]
    public void CancelDuplicateLayerDialog()
    {
        IsDuplicateLayerDialogOpen = false;
        _duplicateTargetElements = null;
    }

    [RelayCommand]
    public void ConfirmDuplicateLayerDialog()
    {
        IsDuplicateLayerDialogOpen = false;
        var targets = _duplicateTargetElements ?? (SelectedElements.Count > 0 ? SelectedElements.ToList() : null);
        _duplicateTargetElements = null;

        if (targets == null || targets.Count == 0) return;

        if (DuplicateLayerDestinationIndex == 0)
        {
            // Duplicate into current document
            var clones = CloneWithOffset(targets);
            if (clones.Count == 1 && !string.IsNullOrWhiteSpace(DuplicateLayerAsName))
            {
                clones[0].Name = DuplicateLayerAsName.Trim();
            }

            AddClonesAndSelect(clones, "Duplicated");
            StatusMessage = clones.Count == 1
                ? $"Duplicated layer '{clones[0].DisplayName}'."
                : $"Duplicated {clones.Count} layers.";
        }
        else
        {
            // Duplicate into a New Document
            var newElements = targets.Select(e =>
            {
                var c = e.Clone();
                c.X = Math.Max(0, c.X);
                c.Y = Math.Max(0, c.Y);
                return c;
            }).ToList();

            if (newElements.Count == 1 && !string.IsNullOrWhiteSpace(DuplicateLayerAsName))
            {
                newElements[0].Name = DuplicateLayerAsName.Trim();
            }

            ApplyCanvasState(CanvasWidth, CanvasHeight, BackgroundColor, newElements, $"Created new document '{DuplicateLayerNewDocName}' from duplicated layer(s).");
            StatusMessage = $"Created document '{DuplicateLayerNewDocName}' with duplicated layer(s).";
        }
    }
}
