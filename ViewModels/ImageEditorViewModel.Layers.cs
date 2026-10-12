using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Controls;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;
using System.IO;

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

    // ── Layer Keys & Structural Background Parity ───────────────────────────
    [ObservableProperty] private bool _isLayerMaskTargeted;

    public CanvasElement? BackgroundLayer =>
        Elements.FirstOrDefault(e => e.IsBackground || (Elements.Count > 0 && e == Elements[0] && e.HasBackgroundTraits));

    public bool IsSelectedElementBackground =>
        SelectedElement != null && (SelectedElement.IsBackground || (Elements.Count > 0 && SelectedElement == Elements[0] && SelectedElement.HasBackgroundTraits));

    [RelayCommand]
    public void ToggleLockTransparency(CanvasElement? element = null)
    {
        var targets = element != null
            ? [element]
            : (SelectedElements.Count > 0
                ? SelectedElements.ToList()
                : (SelectedElement != null ? [SelectedElement] : (Elements.Count > 0 ? [Elements[^1]] : [])));

        // Background cannot lock transparency
        targets.RemoveAll(t => t.IsBackground || (Elements.Count > 0 && t == Elements[0] && t.HasBackgroundTraits));

        if (targets.Count == 0)
        {
            StatusMessage = "The Background layer cannot lock transparent pixels.";
            return;
        }

        bool allLocked = targets.All(t => t.IsTransparencyLocked);
        bool newLock = !allLocked;

        foreach (var t in targets)
        {
            t.IsTransparencyLocked = newLock;
        }

        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        OnPropertyChanged(nameof(SelectedElement));
        StatusMessage = newLock ? "Locked transparent pixels." : "Unlocked transparent pixels.";
    }

    [RelayCommand]
    public void TargetLayerMask()
    {
        var target = SelectedElement ?? Elements.LastOrDefault();
        if (target == null) return;

        IsLayerMaskTargeted = !IsLayerMaskTargeted;
        StatusMessage = IsLayerMaskTargeted ? $"Targeted layer mask on '{target.DisplayName}'." : $"Targeted composite on '{target.DisplayName}'.";
        OnPropertyChanged(nameof(IsLayerMaskTargeted));
    }

    [RelayCommand]
    public void FitLayerToScreen(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement ?? Elements.LastOrDefault();
        if (target == null)
        {
            StatusMessage = "No layer to fit on screen.";
            return;
        }

        var bounds = target.GetRotatedBounds();
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            CanvasControl?.FitElementToWindow(bounds);
            StatusMessage = $"Fitted '{target.DisplayName}' on screen.";
        }
    }

    [RelayCommand]
    public void ConvertBackgroundToLayer(CanvasElement? element = null)
    {
        var target = element ?? BackgroundLayer ?? (Elements.Count > 0 ? Elements[0] : null);
        if (target == null) return;

        target.IsBackground = false;
        target.IsPositionLocked = false;
        target.IsTransparencyLocked = false;
        if (string.Equals(target.Name, "Background", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(target.Name))
        {
            target.Name = "Layer 0";
        }

        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(IsSelectedElementBackground));
        StatusMessage = $"Converted Background to '{target.DisplayName}'.";
    }

    [RelayCommand]
    public void ConvertLayerToBackground(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement ?? (Elements.Count > 0 ? Elements[0] : null);
        if (target == null) return;

        foreach (var el in Elements)
        {
            if (el != target && el.IsBackground) el.IsBackground = false;
        }

        target.IsBackground = true;
        target.IsPositionLocked = true;
        target.IsTransparencyLocked = true;
        target.ZIndex = 0;

        Elements.Remove(target);
        Elements.Insert(0, target);
        for (int i = 0; i < Elements.Count; i++)
        {
            Elements[i].ZIndex = i;
        }

        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(IsSelectedElementBackground));
        StatusMessage = $"Converted '{target.DisplayName}' to Background.";
    }

    /// <summary>
    /// Navigates to the adjacent layer in Z-order for keyboard layer navigation.
    /// cycleUp=false selects next layer down; cycleUp=true selects next layer up.
    /// </summary>
    public CanvasElement? GetAdjacentLayer(CanvasElement current, bool cycleUp)
    {
        var list = LayersDescendingZ.ToList();
        int idx = list.IndexOf(current);
        if (idx < 0) return null;

        int targetIdx = cycleUp ? idx - 1 : idx + 1;
        if (targetIdx < 0 || targetIdx >= list.Count) return null;

        var next = list[targetIdx];
        if (next.IsBackground || (Elements.Count > 0 && next == Elements[0] && next.HasBackgroundTraits))
        {
            // Background ends inline renaming
            return null;
        }

        return next;
    }

    // ── Rename Layer Dialog (Photoshop Parity) ──────────────────────────────
    [ObservableProperty] private bool _isRenameLayerDialogOpen;
    [ObservableProperty] private string _renameLayerName = "";
    [ObservableProperty] private int _renameLayerColorIndex = 0;
    private CanvasElement? _renameLayerTargetElement;

    [RelayCommand]
    public void OpenRenameLayerDialog(CanvasElement? target = null)
    {
        var element = target ?? SelectedElement ?? Elements.LastOrDefault();
        if (element == null)
        {
            StatusMessage = "No layer to rename.";
            return;
        }

        if (element.HasBackgroundTraits)
        {
            ConvertBackgroundToLayer(element);
            return;
        }

        _renameLayerTargetElement = element;
        RenameLayerName = element.DisplayName;
        RenameLayerColorIndex = (int)element.LabelColor;
        IsRenameLayerDialogOpen = true;
    }

    [RelayCommand]
    public void CancelRenameLayerDialog()
    {
        IsRenameLayerDialogOpen = false;
        _renameLayerTargetElement = null;
    }

    [RelayCommand]
    public void ConfirmRenameLayerDialog()
    {
        if (_renameLayerTargetElement != null)
        {
            if (!string.IsNullOrWhiteSpace(RenameLayerName))
            {
                _renameLayerTargetElement.Name = RenameLayerName.Trim();
            }
            if (RenameLayerColorIndex >= 0 && RenameLayerColorIndex <= 7)
            {
                _renameLayerTargetElement.LabelColor = (LayerLabelColor)RenameLayerColorIndex;
            }
            RefreshCanvas();
            OnPropertyChanged(nameof(LayersDescendingZ));
            OnPropertyChanged(nameof(SelectedElement));
            StatusMessage = $"Renamed layer to '{_renameLayerTargetElement.DisplayName}'.";
        }

        IsRenameLayerDialogOpen = false;
        _renameLayerTargetElement = null;
    }

    public void RenameNextLayer(bool cycleUp)
    {
        if (_renameLayerTargetElement == null) return;

        if (!string.IsNullOrWhiteSpace(RenameLayerName))
        {
            _renameLayerTargetElement.Name = RenameLayerName.Trim();
        }
        if (RenameLayerColorIndex >= 0 && RenameLayerColorIndex <= 7)
        {
            _renameLayerTargetElement.LabelColor = (LayerLabelColor)RenameLayerColorIndex;
        }
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));

        var next = GetAdjacentLayer(_renameLayerTargetElement, cycleUp);
        if (next != null)
        {
            SelectedElement = next;
            _renameLayerTargetElement = next;
            RenameLayerName = next.DisplayName;
            RenameLayerColorIndex = (int)next.LabelColor;
            StatusMessage = $"Editing layer '{next.DisplayName}'.";
        }
        else
        {
            IsRenameLayerDialogOpen = false;
            _renameLayerTargetElement = null;
        }
    }

    [RelayCommand]
    public void CycleBlendMode(bool forward)
    {
        if (SelectedElement == null) return;
        var modes = AvailableBlendModes;
        if (modes.Count == 0) return;
        int idx = -1;
        for (int i = 0; i < modes.Count; i++)
        {
            if (modes[i] == BlendModeField)
            {
                idx = i;
                break;
            }
        }
        if (idx < 0) idx = 0;
        int nextIdx = forward ? (idx + 1) % modes.Count : (idx - 1 + modes.Count) % modes.Count;
        BlendModeField = modes[nextIdx];
        StatusMessage = $"Blend Mode: {BlendModeField}";
    }

    // ── Layer Merging & Flattening (Photoshop Parity #3855) ─────────────────────

    [RelayCommand]
    public void FlattenImage()
    {
        if (Elements.Count <= 1 && Elements.FirstOrDefault()?.IsBackground == true)
        {
            StatusMessage = "Document is already flattened.";
            return;
        }

        int w = Math.Max(1, (int)_canvasWidth);
        int h = Math.Max(1, (int)_canvasHeight);
        var rtb = new RenderTargetBitmap(new PixelSize(w, h));
        using (var dc = rtb.CreateDrawingContext())
        {
            if (BackgroundColor.A > 0)
            {
                dc.DrawRectangle(new SolidColorBrush(BackgroundColor), null, new Rect(0, 0, w, h));
            }
            foreach (var el in Elements.OrderBy(e => e.ZIndex))
            {
                if (el.IsVisible)
                {
                    EditorCanvasControl.DrawElement(dc, el);
                }
            }
        }

        using var ms = new MemoryStream();
        rtb.Save(ms, new PngBitmapEncoderOptions());
        ms.Position = 0;
        var flatBmp = new Bitmap(ms);

        var bgElement = new ImageElement
        {
            Name = "Background",
            Width = w,
            Height = h,
            X = 0,
            Y = 0,
            ZIndex = 0,
            Source = flatBmp,
            NaturalPixelWidth = w,
            NaturalPixelHeight = h,
            OriginalImageBytes = ms.ToArray(),
            IsBackground = true,
            IsPositionLocked = true,
            IsTransparencyLocked = true,
            IsVisible = true
        };

        var oldElements = Elements.ToList();
        Elements.Clear();
        Elements.Add(bgElement);
        SelectedElement = bgElement;

        History.Push(new RestoreSnapshotCommand(Elements, oldElements, new List<CanvasElement> { bgElement }, "Flatten Image", () => RefreshCanvas()));
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = "Image flattened to single Background.";
    }

    [RelayCommand]
    public void MergeDown()
    {
        var target = SelectedElement;
        if (target == null)
        {
            StatusMessage = "No layer selected to merge down.";
            return;
        }

        var sorted = Elements.OrderBy(e => e.ZIndex).ToList();
        int idx = sorted.IndexOf(target);
        if (idx <= 0)
        {
            StatusMessage = "Selected layer cannot be merged down.";
            return;
        }

        var lower = sorted[idx - 1];

        double minX = Math.Min(target.X, lower.X);
        double minY = Math.Min(target.Y, lower.Y);
        double maxX = Math.Max(target.X + target.Width, lower.X + lower.Width);
        double maxY = Math.Max(target.Y + target.Height, lower.Y + lower.Height);
        int w = Math.Max(1, (int)Math.Ceiling(maxX - minX));
        int h = Math.Max(1, (int)Math.Ceiling(maxY - minY));

        var rtb = new RenderTargetBitmap(new PixelSize(w, h));
        using (var dc = rtb.CreateDrawingContext())
        {
            using (dc.PushTransform(Matrix.CreateTranslation(-minX, -minY)))
            {
                EditorCanvasControl.DrawElement(dc, lower);
                EditorCanvasControl.DrawElement(dc, target);
            }
        }

        using var ms = new MemoryStream();
        rtb.Save(ms, new PngBitmapEncoderOptions());
        ms.Position = 0;
        var mergedBmp = new Bitmap(ms);

        var merged = new ImageElement
        {
            Name = lower.Name,
            Width = w,
            Height = h,
            X = minX,
            Y = minY,
            ZIndex = lower.ZIndex,
            Source = mergedBmp,
            NaturalPixelWidth = w,
            NaturalPixelHeight = h,
            OriginalImageBytes = ms.ToArray(),
            IsVisible = true,
            Opacity = 1.0
        };

        var oldList = Elements.ToList();
        Elements.Remove(target);
        int lowerIdx = Elements.IndexOf(lower);
        Elements[lowerIdx] = merged;
        SelectedElement = merged;

        History.Push(new RestoreSnapshotCommand(Elements, oldList, Elements.ToList(), "Merge Down", () => RefreshCanvas()));
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Merged '{target.DisplayName}' down into '{lower.DisplayName}'.";
    }

    [RelayCommand]
    public void MergeVisible()
    {
        var visible = Elements.Where(e => e.IsVisible).OrderBy(e => e.ZIndex).ToList();
        if (visible.Count <= 1)
        {
            StatusMessage = "Not enough visible layers to merge.";
            return;
        }

        int w = Math.Max(1, (int)_canvasWidth);
        int h = Math.Max(1, (int)_canvasHeight);
        var rtb = new RenderTargetBitmap(new PixelSize(w, h));
        using (var dc = rtb.CreateDrawingContext())
        {
            foreach (var el in visible)
            {
                EditorCanvasControl.DrawElement(dc, el);
            }
        }

        using var ms = new MemoryStream();
        rtb.Save(ms, new PngBitmapEncoderOptions());
        ms.Position = 0;
        var mergedBmp = new Bitmap(ms);

        int minZ = visible.Min(e => e.ZIndex);
        var merged = new ImageElement
        {
            Name = "Merged Layers",
            Width = w,
            Height = h,
            X = 0,
            Y = 0,
            ZIndex = minZ,
            Source = mergedBmp,
            NaturalPixelWidth = w,
            NaturalPixelHeight = h,
            OriginalImageBytes = ms.ToArray(),
            IsVisible = true,
            Opacity = 1.0
        };

        var oldList = Elements.ToList();
        foreach (var el in visible) Elements.Remove(el);
        Elements.Add(merged);
        SelectedElement = merged;

        History.Push(new RestoreSnapshotCommand(Elements, oldList, Elements.ToList(), "Merge Visible", () => RefreshCanvas()));
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Merged {visible.Count} visible layers.";
    }
}
