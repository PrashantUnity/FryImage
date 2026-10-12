using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
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

    // ── New Layer Modal (Photoshop Parity) ─────────────────
    [ObservableProperty] private bool _isNewLayerDialogOpen;
    [ObservableProperty] private string _newLayerName = "Layer 1";
    [ObservableProperty] private bool _newLayerIsClippingMask;
    [ObservableProperty] private int _newLayerColorIndex = 0; // 0=None, 1=Red, 2=Orange, 3=Yellow, 4=Green, 5=Blue, 6=Violet, 7=Gray
    [ObservableProperty] private string _newLayerBlendModeStr = "Normal";
    [ObservableProperty] private double _newLayerOpacityPercent = 100.0;
    [ObservableProperty] private bool _newLayerFillWithNeutral;

    public bool NewLayerHasNeutralColor
    {
        get
        {
            var mode = ParseBlendMode(NewLayerBlendModeStr);
            return GetNeutralColorForBlendMode(mode).hasNeutral;
        }
    }

    public string NewLayerNeutralColorPrompt
    {
        get
        {
            var mode = ParseBlendMode(NewLayerBlendModeStr);
            var (hasNeutral, _, name) = GetNeutralColorForBlendMode(mode);
            return hasNeutral
                ? $"Fill with {NewLayerBlendModeStr}-neutral color ({name})"
                : $"(No neutral color exists for {NewLayerBlendModeStr} mode.)";
        }
    }

    partial void OnNewLayerBlendModeStrChanged(string value)
    {
        OnPropertyChanged(nameof(NewLayerHasNeutralColor));
        OnPropertyChanged(nameof(NewLayerNeutralColorPrompt));
        if (!NewLayerHasNeutralColor)
        {
            NewLayerFillWithNeutral = false;
        }
    }

    [RelayCommand]
    public void OpenNewLayerDialog()
    {
        int nextNum = Elements.Count + 1;
        NewLayerName = $"Layer {nextNum}";
        NewLayerIsClippingMask = false;
        NewLayerColorIndex = 0;
        NewLayerBlendModeStr = "Normal";
        NewLayerOpacityPercent = 100.0;
        NewLayerFillWithNeutral = false;
        OnPropertyChanged(nameof(NewLayerHasNeutralColor));
        OnPropertyChanged(nameof(NewLayerNeutralColorPrompt));
        IsNewLayerDialogOpen = true;
    }

    [RelayCommand]
    public void CancelNewLayerDialog()
    {
        IsNewLayerDialogOpen = false;
    }

    [RelayCommand]
    public void ConfirmNewLayerDialog()
    {
        IsNewLayerDialogOpen = false;
        var mode = ParseBlendMode(NewLayerBlendModeStr);
        var labelColor = (LayerLabelColor)Math.Clamp(NewLayerColorIndex, 0, 7);

        var (hasNeutral, neutralColor, _) = GetNeutralColorForBlendMode(mode);
        var fillColor = (NewLayerFillWithNeutral && hasNeutral)
            ? neutralColor
            : Color.FromArgb(0, 0, 0, 0); // Transparent raster plane / rectangle

        var el = new RectangleElement
        {
            Name = string.IsNullOrWhiteSpace(NewLayerName) ? $"Layer {Elements.Count + 1}" : NewLayerName.Trim(),
            X = 0,
            Y = 0,
            Width = CanvasWidth,
            Height = CanvasHeight,
            ZIndex = Elements.Count + 1,
            Opacity = Math.Clamp(NewLayerOpacityPercent / 100.0, 0.0, 1.0),
            BlendMode = mode,
            IsClippingMask = NewLayerIsClippingMask,
            LabelColor = labelColor,
            FillColor = fillColor,
            StrokeThickness = 0
        };

        CommitNewElement(el);
        StatusMessage = $"Created layer '{el.DisplayName}'.";
    }

    [RelayCommand]
    public void SetLayerLabelColor(object? parameter)
    {
        if (parameter is LayerLabelColor color && SelectedElement != null)
        {
            SelectedElement.LabelColor = color;
            OnPropertyChanged(nameof(SelectedElement));
            OnPropertyChanged(nameof(LayersDescendingZ));
            StatusMessage = $"Set layer '{SelectedElement.DisplayName}' color to {color.ToDisplayName()}.";
        }
        else if (parameter is string colorStr && Enum.TryParse<LayerLabelColor>(colorStr, true, out var parsed) && SelectedElement != null)
        {
            SelectedElement.LabelColor = parsed;
            OnPropertyChanged(nameof(SelectedElement));
            OnPropertyChanged(nameof(LayersDescendingZ));
            StatusMessage = $"Set layer '{SelectedElement.DisplayName}' color to {parsed.ToDisplayName()}.";
        }
    }

    private static LayerBlendMode ParseBlendMode(string name) =>
        Enum.TryParse<LayerBlendMode>(name, true, out var bm) ? bm : LayerBlendMode.Normal;

    private static (bool hasNeutral, Color color, string name) GetNeutralColorForBlendMode(LayerBlendMode mode) => mode switch
    {
        LayerBlendMode.Multiply or LayerBlendMode.ColorBurn or LayerBlendMode.LinearBurn or
        LayerBlendMode.Darken or LayerBlendMode.DarkerColor or LayerBlendMode.Divide
            => (true, Colors.White, "White"),

        LayerBlendMode.Screen or LayerBlendMode.ColorDodge or LayerBlendMode.LinearDodge or
        LayerBlendMode.Lighten or LayerBlendMode.LighterColor or LayerBlendMode.Difference or LayerBlendMode.Exclusion
            => (true, Colors.Black, "Black"),

        LayerBlendMode.Overlay or LayerBlendMode.SoftLight or LayerBlendMode.HardLight or
        LayerBlendMode.VividLight or LayerBlendMode.LinearLight or LayerBlendMode.PinLight or LayerBlendMode.HardMix
            => (true, Color.FromRgb(128, 128, 128), "50% Gray"),

        _ => (false, Colors.Transparent, "none")
    };
}
