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

    [ObservableProperty] private bool _isVectorMaskTargeted;

    [RelayCommand]
    public void TargetLayerMask()
    {
        var target = SelectedElement ?? Elements.LastOrDefault();
        if (target == null) return;

        IsLayerMaskTargeted = !IsLayerMaskTargeted;
        if (IsLayerMaskTargeted) IsVectorMaskTargeted = false;
        StatusMessage = IsLayerMaskTargeted ? $"Targeted layer mask on '{target.DisplayName}'." : $"Targeted composite on '{target.DisplayName}'.";
        OnPropertyChanged(nameof(IsLayerMaskTargeted));
    }

    [RelayCommand]
    public void TargetVectorMask()
    {
        var target = SelectedElement ?? Elements.LastOrDefault();
        if (target == null) return;

        IsVectorMaskTargeted = !IsVectorMaskTargeted;
        if (IsVectorMaskTargeted) IsLayerMaskTargeted = false;
        StatusMessage = IsVectorMaskTargeted ? $"Targeted vector mask on '{target.DisplayName}'." : $"Targeted composite on '{target.DisplayName}'.";
        OnPropertyChanged(nameof(IsVectorMaskTargeted));
    }

    [RelayCommand]
    public void AddLayerMask(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement ?? Elements.LastOrDefault();
        if (target == null) return;

        // If a vector path is active/selected, create vector mask from that path and target it (#4728, #4740)
        var path = CanvasControl?.ActiveDocumentPath;
        if (path != null && !path.IsEmpty)
        {
            target.VectorMask = path.Clone();
            target.IsVectorMaskEnabled = true;
            IsVectorMaskTargeted = true;
            IsLayerMaskTargeted = false;
            RefreshCanvas();
            OnPropertyChanged(nameof(LayersDescendingZ));
            StatusMessage = $"Added vector mask from path onto '{target.DisplayName}'.";
            return;
        }

        // Otherwise create raster layer mask (from active selection or reveal all)
        byte[]? selMask = CanvasControl?.QuickMaskBuffer;
        if (selMask != null && selMask.Length >= CanvasWidth * CanvasHeight)
        {
            target.MaskData = (byte[])selMask.Clone();
            target.MaskBounds = new Rect(0, 0, CanvasWidth, CanvasHeight);
            target.IsMaskEnabled = true;
            IsLayerMaskTargeted = true;
            IsVectorMaskTargeted = false;
            RefreshCanvas();
            OnPropertyChanged(nameof(LayersDescendingZ));
            StatusMessage = $"Added layer mask from selection onto '{target.DisplayName}'.";
            return;
        }

        // Reveal all (white mask)
        int w = Math.Max(1, (int)target.Width);
        int h = Math.Max(1, (int)target.Height);
        var fullMask = new byte[w * h];
        Array.Fill(fullMask, (byte)255);
        target.MaskData = fullMask;
        target.MaskBounds = new Rect(0, 0, target.Width, target.Height);
        target.IsMaskEnabled = true;
        IsLayerMaskTargeted = true;
        IsVectorMaskTargeted = false;
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Added reveal-all layer mask onto '{target.DisplayName}'.";
    }

    [RelayCommand]
    public void ToggleLayerMaskEnabled(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement ?? Elements.LastOrDefault();
        if (target == null) return;
        target.IsMaskEnabled = !target.IsMaskEnabled;
        RefreshCanvas();
        StatusMessage = target.IsMaskEnabled ? $"Enabled layer mask on '{target.DisplayName}'." : $"Disabled layer mask on '{target.DisplayName}'.";
    }

    [RelayCommand]
    public void DeleteLayerMask(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement ?? Elements.LastOrDefault();
        if (target == null) return;
        target.MaskData = null;
        target.MaskBounds = null;
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Deleted layer mask from '{target.DisplayName}'.";
    }

    [RelayCommand]
    public void ApplyLayerMask(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement ?? Elements.LastOrDefault();
        if (target == null || target.MaskData == null) return;
        target.MaskData = null;
        target.MaskBounds = null;
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Applied layer mask on '{target.DisplayName}'.";
    }

    [RelayCommand]
    public void AddMaskToSelection(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement ?? Elements.LastOrDefault();
        if (target == null) return;
        SelectedElement = target;
        SelectionCombineMode = "Add";
        StatusMessage = $"Added mask of '{target.DisplayName}' to selection.";
    }

    [RelayCommand]
    public void SubtractMaskFromSelection(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement ?? Elements.LastOrDefault();
        if (target == null) return;
        SelectedElement = target;
        SelectionCombineMode = "Subtract";
        StatusMessage = $"Subtracted mask of '{target.DisplayName}' from selection.";
    }

    [RelayCommand]
    public void IntersectMaskWithSelection(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement ?? Elements.LastOrDefault();
        if (target == null) return;
        SelectedElement = target;
        SelectionCombineMode = "Intersect";
        StatusMessage = $"Intersected mask of '{target.DisplayName}' with selection.";
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

        if (target.IsBackground || string.Equals(target.Name, "Background", StringComparison.OrdinalIgnoreCase) || (target.Name != null && target.Name.StartsWith("Background", StringComparison.OrdinalIgnoreCase)) || string.IsNullOrEmpty(target.Name))
        {
            target.Name = "Layer 0";
        }

        target.IsBackground = false;
        target.IsLocked = false;
        target.IsPositionLocked = false;
        target.IsTransparencyLocked = false;

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

    // ── Flatten All Layer Effects (Photoshop Parity #3804 / #3835) ─────────────
    [RelayCommand]
    public void FlattenAllLayerEffects()
    {
        var targets = Elements.Where(e => e.Effects.HasAnyActiveEffect).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "No layer effects to flatten.";
            return;
        }

        var oldList = Elements.Select(e => e.Clone()).ToList();
        int flattenedCount = 0;

        foreach (var el in targets)
        {
            int w = Math.Max(1, (int)Math.Ceiling(el.Width));
            int h = Math.Max(1, (int)Math.Ceiling(el.Height));

            var rtb = new RenderTargetBitmap(new PixelSize(w, h));
            using (var dc = rtb.CreateDrawingContext())
            {
                // Temporarily disable masks during effect baking so masks are kept separate
                bool origMaskEnabled = el.IsMaskEnabled;
                bool origVecMaskEnabled = el.IsVectorMaskEnabled;
                el.IsMaskEnabled = false;
                el.IsVectorMaskEnabled = false;

                try
                {
                    double origX = el.X;
                    double origY = el.Y;
                    el.X = 0;
                    el.Y = 0;
                    EditorCanvasControl.DrawElement(dc, el);
                    el.X = origX;
                    el.Y = origY;
                }
                finally
                {
                    el.IsMaskEnabled = origMaskEnabled;
                    el.IsVectorMaskEnabled = origVecMaskEnabled;
                }
            }

            using var ms = new MemoryStream();
            rtb.Save(ms, new PngBitmapEncoderOptions());
            ms.Position = 0;
            var bakedBmp = new Bitmap(ms);

            var bakedEl = new ImageElement
            {
                Name = el.Name,
                Width = el.Width,
                Height = el.Height,
                X = el.X,
                Y = el.Y,
                ZIndex = el.ZIndex,
                Source = bakedBmp,
                NaturalPixelWidth = w,
                NaturalPixelHeight = h,
                OriginalImageBytes = ms.ToArray(),
                IsVisible = el.IsVisible,
                Opacity = el.Opacity,
                FillOpacity = 1.0,
                BlendMode = el.BlendMode,
                // KEEP MASKS INTACT!
                MaskData = el.MaskData != null ? (byte[])el.MaskData.Clone() : null,
                IsMaskEnabled = el.IsMaskEnabled,
                MaskBounds = el.MaskBounds,
                VectorMask = el.VectorMask?.Clone(),
                IsVectorMaskEnabled = el.IsVectorMaskEnabled,
                // CLEAR EFFECTS
                Effects = new LayerStyleEffects()
            };

            int idx = Elements.IndexOf(el);
            if (idx >= 0)
            {
                Elements[idx] = bakedEl;
                if (SelectedElement == el) SelectedElement = bakedEl;
            }
            flattenedCount++;
        }

        History.Push(new RestoreSnapshotCommand(Elements, oldList, Elements.ToList(), "Flatten All Layer Effects", () => RefreshCanvas()));
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Flattened layer effects on {flattenedCount} layers (masks preserved).";
    }

    [RelayCommand]
    public void FlattenAllMasks()
    {
        var targets = Elements.Where(e => e.MaskData != null || e.VectorMask != null).ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "No layer masks to flatten.";
            return;
        }

        var oldList = Elements.Select(e => e.Clone()).ToList();
        int flattenedCount = 0;

        foreach (var el in targets)
        {
            int w = Math.Max(1, (int)Math.Ceiling(el.Width));
            int h = Math.Max(1, (int)Math.Ceiling(el.Height));

            var rtb = new RenderTargetBitmap(new PixelSize(w, h));
            using (var dc = rtb.CreateDrawingContext())
            {
                var origEffects = el.Effects;
                el.Effects = new LayerStyleEffects();

                try
                {
                    double origX = el.X;
                    double origY = el.Y;
                    el.X = 0;
                    el.Y = 0;
                    EditorCanvasControl.DrawElement(dc, el);
                    el.X = origX;
                    el.Y = origY;
                }
                finally
                {
                    el.Effects = origEffects;
                }
            }

            using var ms = new MemoryStream();
            rtb.Save(ms, new PngBitmapEncoderOptions());
            ms.Position = 0;
            var bakedBmp = new Bitmap(ms);

            var bakedEl = new ImageElement
            {
                Name = el.Name,
                Width = el.Width,
                Height = el.Height,
                X = el.X,
                Y = el.Y,
                ZIndex = el.ZIndex,
                Source = bakedBmp,
                NaturalPixelWidth = w,
                NaturalPixelHeight = h,
                OriginalImageBytes = ms.ToArray(),
                IsVisible = el.IsVisible,
                Opacity = el.Opacity,
                FillOpacity = el.FillOpacity,
                BlendMode = el.BlendMode,
                Effects = el.Effects.Clone(),
                MaskData = null,
                VectorMask = null
            };

            int idx = Elements.IndexOf(el);
            if (idx >= 0)
            {
                Elements[idx] = bakedEl;
                if (SelectedElement == el) SelectedElement = bakedEl;
            }
            flattenedCount++;
        }

        History.Push(new RestoreSnapshotCommand(Elements, oldList, Elements.ToList(), "Flatten All Masks", () => RefreshCanvas()));
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Flattened masks on {flattenedCount} layers.";
    }

    [RelayCommand]
    public void RasterizeLayer(CanvasElement? element = null)
    {
        var target = element ?? SelectedElement;
        if (target == null)
        {
            StatusMessage = "No layer selected to rasterize.";
            return;
        }

        if (target is ImageElement img && !img.Effects.HasAnyActiveEffect)
        {
            StatusMessage = $"Layer '{target.DisplayName}' is already a raster layer.";
            return;
        }

        var oldList = Elements.Select(e => e.Clone()).ToList();
        int w = Math.Max(1, (int)Math.Ceiling(target.Width));
        int h = Math.Max(1, (int)Math.Ceiling(target.Height));

        var rtb = new RenderTargetBitmap(new PixelSize(w, h));
        using (var dc = rtb.CreateDrawingContext())
        {
            double origX = target.X;
            double origY = target.Y;
            target.X = 0;
            target.Y = 0;
            EditorCanvasControl.DrawElement(dc, target);
            target.X = origX;
            target.Y = origY;
        }

        using var ms = new MemoryStream();
        rtb.Save(ms, new PngBitmapEncoderOptions());
        ms.Position = 0;
        var bakedBmp = new Bitmap(ms);

        var baked = new ImageElement
        {
            Name = target.Name,
            Width = target.Width,
            Height = target.Height,
            X = target.X,
            Y = target.Y,
            ZIndex = target.ZIndex,
            Source = bakedBmp,
            NaturalPixelWidth = w,
            NaturalPixelHeight = h,
            OriginalImageBytes = ms.ToArray(),
            IsVisible = target.IsVisible,
            Opacity = target.Opacity,
            FillOpacity = target.FillOpacity,
            BlendMode = target.BlendMode,
            MaskData = target.MaskData != null ? (byte[])target.MaskData.Clone() : null,
            IsMaskEnabled = target.IsMaskEnabled,
            VectorMask = target.VectorMask?.Clone(),
            IsVectorMaskEnabled = target.IsVectorMaskEnabled
        };

        int idx = Elements.IndexOf(target);
        if (idx >= 0)
        {
            Elements[idx] = baked;
            SelectedElement = baked;
        }

        History.Push(new RestoreSnapshotCommand(Elements, oldList, Elements.ToList(), $"Rasterize {target.DisplayName}", () => RefreshCanvas()));
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Rasterized '{target.DisplayName}'.";
    }

    [RelayCommand]
    public void RasterizeAllLayers()
    {
        var nonRaster = Elements.Where(e => e is not ImageElement || e.Effects.HasAnyActiveEffect).ToList();
        if (nonRaster.Count == 0)
        {
            StatusMessage = "All layers are already rasterized.";
            return;
        }

        var oldList = Elements.Select(e => e.Clone()).ToList();
        foreach (var el in nonRaster)
        {
            int w = Math.Max(1, (int)Math.Ceiling(el.Width));
            int h = Math.Max(1, (int)Math.Ceiling(el.Height));

            var rtb = new RenderTargetBitmap(new PixelSize(w, h));
            using (var dc = rtb.CreateDrawingContext())
            {
                double origX = el.X;
                double origY = el.Y;
                el.X = 0;
                el.Y = 0;
                EditorCanvasControl.DrawElement(dc, el);
                el.X = origX;
                el.Y = origY;
            }

            using var ms = new MemoryStream();
            rtb.Save(ms, new PngBitmapEncoderOptions());
            ms.Position = 0;
            var bakedBmp = new Bitmap(ms);

            var baked = new ImageElement
            {
                Name = el.Name,
                Width = el.Width,
                Height = el.Height,
                X = el.X,
                Y = el.Y,
                ZIndex = el.ZIndex,
                Source = bakedBmp,
                NaturalPixelWidth = w,
                NaturalPixelHeight = h,
                OriginalImageBytes = ms.ToArray(),
                IsVisible = el.IsVisible,
                Opacity = el.Opacity,
                FillOpacity = el.FillOpacity,
                BlendMode = el.BlendMode,
                MaskData = el.MaskData != null ? (byte[])el.MaskData.Clone() : null,
                IsMaskEnabled = el.IsMaskEnabled,
                VectorMask = el.VectorMask?.Clone(),
                IsVectorMaskEnabled = el.IsVectorMaskEnabled
            };

            int idx = Elements.IndexOf(el);
            if (idx >= 0) Elements[idx] = baked;
        }

        History.Push(new RestoreSnapshotCommand(Elements, oldList, Elements.ToList(), "Rasterize All Layers", () => RefreshCanvas()));
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Rasterized {nonRaster.Count} layers.";
    }

    [RelayCommand]
    public void StampVisible()
    {
        var visible = Elements.Where(e => e.IsVisible).OrderBy(e => e.ZIndex).ToList();
        if (visible.Count == 0)
        {
            StatusMessage = "No visible layers to stamp.";
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
            foreach (var el in visible)
            {
                EditorCanvasControl.DrawElement(dc, el);
            }
        }

        using var ms = new MemoryStream();
        rtb.Save(ms, new PngBitmapEncoderOptions());
        ms.Position = 0;
        var stampedBmp = new Bitmap(ms);

        int maxZ = Elements.Count > 0 ? Elements.Max(e => e.ZIndex) + 1 : 1;
        var stamped = new ImageElement
        {
            Name = "Merged Stamp",
            Width = w,
            Height = h,
            X = 0,
            Y = 0,
            ZIndex = maxZ,
            Source = stampedBmp,
            NaturalPixelWidth = w,
            NaturalPixelHeight = h,
            OriginalImageBytes = ms.ToArray(),
            IsVisible = true,
            Opacity = 1.0
        };

        var oldList = Elements.ToList();
        Elements.Add(stamped);
        SelectedElement = stamped;

        History.Push(new RestoreSnapshotCommand(Elements, oldList, Elements.ToList(), "Stamp Visible", () => RefreshCanvas()));
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = "Stamped visible layers into new top layer.";
    }

    [RelayCommand]
    public void StampDown()
    {
        var target = SelectedElement;
        if (target == null)
        {
            StatusMessage = "No layer selected to stamp down.";
            return;
        }

        var sorted = Elements.OrderBy(e => e.ZIndex).ToList();
        int idx = sorted.IndexOf(target);
        if (idx <= 0)
        {
            StatusMessage = "Selected layer cannot be stamped down.";
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
        var stampedBmp = new Bitmap(ms);

        var stamped = new ImageElement
        {
            Name = $"{lower.Name} (Stamped)",
            Width = w,
            Height = h,
            X = minX,
            Y = minY,
            ZIndex = target.ZIndex + 1,
            Source = stampedBmp,
            NaturalPixelWidth = w,
            NaturalPixelHeight = h,
            OriginalImageBytes = ms.ToArray(),
            IsVisible = true,
            Opacity = 1.0
        };

        var oldList = Elements.ToList();
        Elements.Add(stamped);
        SelectedElement = stamped;

        History.Push(new RestoreSnapshotCommand(Elements, oldList, Elements.ToList(), "Stamp Down", () => RefreshCanvas()));
        RefreshCanvas();
        OnPropertyChanged(nameof(LayersDescendingZ));
        StatusMessage = $"Stamped '{target.DisplayName}' down into new layer.";
    }
}
