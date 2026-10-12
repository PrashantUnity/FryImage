using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.ImageEditor.Controls;

namespace PdfEditorApp.Plugins.ImageEditor.Views;

/// <summary>
/// Code-behind for ImageEditorView: keyboard and canvas wiring. Window chrome (drag,
/// minimize, close, resize) is provided automatically by the host's StandardCard overlay
/// chrome — see ImageEditorPlugin.cs's OverlayDescriptor.
/// </summary>
public partial class ImageEditorView : UserControl
{
    public ImageEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not ImageEditorViewModel vm) return;

        // Wire canvas control into the ViewModel so it can call Refresh()
        vm.CanvasControl = CanvasControl;

        // Wire canvas selection (single or multi) back to ViewModel
        CanvasControl.SelectionChanged += selection => vm.UpdateSelectionFromCanvas(selection);

        // Keep the zoom-percentage readout in sync with the canvas's own zoom state.
        CanvasControl.ZoomChanged += zoom => vm.ZoomLevel = zoom;

        // Wire cursor position tracking for rulers
        CanvasControl.CursorPositionChanged += pt =>
        {
            vm.CursorCanvasX = pt.X;
            vm.CursorCanvasY = pt.Y;
        };

        void SyncRulerOrigins()
        {
            vm.CanvasOriginScreenX = CanvasControl.CanvasOriginScreenX;
            vm.CanvasOriginScreenY = CanvasControl.CanvasOriginScreenY;
        }

        CanvasControl.CanvasChanged += SyncRulerOrigins;
        CanvasControl.ZoomChanged += _ => SyncRulerOrigins();
        CanvasControl.SizeChanged += (_, _) => SyncRulerOrigins();
        CanvasControl.GuideStatusChanged += msg => vm.StatusMessage = msg;
        SyncRulerOrigins();

        // Keep the floating quick-actions cluster anchored to the selection whenever anything
        // that could move its screen position happens (selection change, a live drag, pan, or
        // zoom), plus once more as soon as the cluster's own size is first known (it starts at
        // zero size before its first layout pass, so the very first appearance would otherwise
        // anchor from the top-left corner instead of centered for one frame).
        CanvasControl.SelectionChanged += _ => RepositionQuickActionsCluster();
        CanvasControl.CanvasChanged += RepositionQuickActionsCluster;
        CanvasControl.ZoomChanged += _ => RepositionQuickActionsCluster();
        QuickActionsCluster.SizeChanged += (_, _) => RepositionQuickActionsCluster();

        // Move/resize commit into undo history once per completed drag (not per-tick).
        CanvasControl.ElementsMoved += moves =>
        {
            vm.RecordElementsMoved(moves);
            if (vm.IsFreeTransformActive && moves.Count > 0)
                vm.SyncTransformSessionFromElement(moves[0].Element);
        };
        CanvasControl.ElementResized += (el, oldBounds, newBounds) =>
        {
            vm.RecordElementResized(el, oldBounds, newBounds);
            if (vm.IsFreeTransformActive)
                vm.SyncTransformSessionFromElement(el);
        };
        CanvasControl.GroupResized += changes => vm.RecordGroupResized(changes);
        CanvasControl.ElementRotated += (el, before, after) =>
        {
            vm.RecordElementRotated(el, before, after);
            if (vm.IsFreeTransformActive)
                vm.SyncTransformSessionFromElement(el);
        };
        CanvasControl.GroupRotated += (before, after) => vm.RecordGroupRotated(before, after);

        // Click-to-place: canvas reports where the user clicked/dragged for the active tool.
        CanvasControl.PlaceTextRequested += at => vm.PlaceTextElement(at);
        CanvasControl.ShapePlaced += (toolType, start, end) => vm.PlaceShapeElement(toolType, start, end);
        CanvasControl.DoodlePlaced += pts => vm.PlaceDoodleElement(pts);
        CanvasControl.ElementEraseRequested += el => vm.EraseElement(el);

        // Crop confirm/reset reports before/after state so the ViewModel can push undo.
        CanvasControl.ElementCropped += (el, before, after) => vm.RecordElementCropped(el, before, after);

        // Eyedropper samples color into primary color (sticky tool mode matching Photoshop).
        CanvasControl.ColorSampled += color =>
        {
            vm.PrimaryColor = color;
        };

        // Inline double-click text editing: overlay a real TextBox positioned/sized to match
        // the element, live only while editing.
        CanvasControl.TextEditRequested += BeginInlineTextEdit;
        CanvasControl.ElementDoubleClicked += _ => vm.OpenLayerStyleDialogCommand.Execute(null);

        // Brush shortcuts and events
        CanvasControl.BrushSizeAdjusted += delta => vm.AdjustBrushSize(delta);
        CanvasControl.BrushHardnessAdjusted += delta => vm.AdjustBrushHardness(delta);
        CanvasControl.SwapColorsRequested += () => vm.SwapColorsCommand.Execute(null);
        CanvasControl.ResetColorsRequested += () => vm.ResetColorsCommand.Execute(null);
        CanvasControl.ToolSwitchRequested += tool => vm.SetToolCommand.Execute(tool);

        // Retouching tool integration
        CanvasControl.CloneSourceDefined += pt => vm.SetCloneSource(pt);
        CanvasControl.RetouchStrokeCompleted += (tool, pts) => vm.ApplyRetouchStroke(tool, pts);
        CanvasControl.ViewportRotationAngleChanged += angle => vm.ViewportRotationAngle = angle;
        CanvasControl.RulerMeasurementChanged += (start, end) => vm.UpdateRulerMeasurement(start, end);

        // Command Palette & Brush Settings hotkey integration
        CanvasControl.CommandPaletteRequested += () => vm.ToggleCommandPalette();
        CanvasControl.BrushSettingsRequested += () => vm.SelectRightDockTab("Brushes");

        // Keep the canvas's tool mode in sync with the toolbar's selected tool. Also keep the
        // inline text-edit overlay's own formatting in sync with ribbon toggles — without this,
        // clicking Bold/Italic/Alignment while InlineTextEditBox is open updates the underlying
        // model immediately but leaves the visible overlay showing the stale formatting until
        // the user commits (Enter/click-away) and the canvas re-renders from the model.
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ImageEditorViewModel.ActiveToolMode))
            {
                CanvasControl.ActiveToolMode = vm.ActiveToolMode;
                CanvasControl.BrushAliased = vm.ActiveToolMode == "pencil" || (vm.SelectedBrushPreset?.Settings.Aliased ?? false);
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.BrushThickness))
            {
                CanvasControl.BrushThickness = vm.BrushThickness;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.BrushHardness))
            {
                CanvasControl.BrushHardness = vm.BrushHardness;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.BrushRoundness))
            {
                CanvasControl.BrushRoundness = vm.BrushRoundness / 100.0;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.BrushAngle))
            {
                CanvasControl.BrushAngle = vm.BrushAngle;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.BrushSmoothing))
            {
                CanvasControl.BrushSmoothing = vm.BrushSmoothing;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.BrushPulledStringMode))
            {
                CanvasControl.PulledStringMode = vm.BrushPulledStringMode;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.BrushCatchUpOnStrokeEnd))
            {
                CanvasControl.CatchUpOnStrokeEnd = vm.BrushCatchUpOnStrokeEnd;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.BrushWetEdges))
            {
                CanvasControl.BrushWetEdges = vm.BrushWetEdges;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.ActiveSymmetryMode))
            {
                CanvasControl.ActiveSymmetryMode = vm.ActiveSymmetryMode;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.SelectedBrushPreset))
            {
                if (vm.SelectedBrushPreset is { } preset)
                {
                    CanvasControl.BrushTipShape = preset.Settings.TipShape;
                    CanvasControl.BrushAliased = preset.Settings.Aliased || vm.ActiveToolMode == "pencil";
                    CanvasControl.BrushWetEdges = preset.Settings.WetEdges;
                }
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.IsHighlighterMode))
            {
                CanvasControl.IsHighlighterMode = vm.IsHighlighterMode;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.PrimaryColor))
            {
                CanvasControl.BrushColor = vm.PrimaryColor;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.CloneSourcePoint))
            {
                CanvasControl.CloneSourcePoint = vm.CloneSourcePoint;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.CloneAligned))
            {
                CanvasControl.CloneAligned = vm.CloneAligned;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.ViewportRotationAngle))
            {
                CanvasControl.ViewportRotationAngle = vm.ViewportRotationAngle;
            }
            else if (args.PropertyName == nameof(ImageEditorViewModel.ActiveDocumentPath))
            {
                CanvasControl.ActiveDocumentPath = vm.ActiveDocumentPath;
                CanvasControl.InvalidateVisual();
            }
            else if (_editingTextElement is { } editing &&
                     (args.PropertyName == nameof(ImageEditorViewModel.IsBold) ||
                      args.PropertyName == nameof(ImageEditorViewModel.IsItalic) ||
                      args.PropertyName == nameof(ImageEditorViewModel.TextAlignmentField)))
            {
                InlineTextEditBox.FontWeight = editing.FontWeight;
                InlineTextEditBox.FontStyle = editing.FontStyle;
                InlineTextEditBox.TextAlignment = editing.Alignment;
            }
        };
        CanvasControl.ActiveToolMode = vm.ActiveToolMode;
        CanvasControl.ActiveDocumentPath = vm.ActiveDocumentPath;
        CanvasControl.BrushThickness = vm.BrushThickness;
        CanvasControl.BrushHardness = vm.BrushHardness;
        CanvasControl.BrushRoundness = vm.BrushRoundness / 100.0;
        CanvasControl.BrushAngle = vm.BrushAngle;
        CanvasControl.BrushSmoothing = vm.BrushSmoothing;
        CanvasControl.PulledStringMode = vm.BrushPulledStringMode;
        CanvasControl.CatchUpOnStrokeEnd = vm.BrushCatchUpOnStrokeEnd;
        CanvasControl.BrushWetEdges = vm.BrushWetEdges;
        CanvasControl.BrushAliased = vm.ActiveToolMode == "pencil" || (vm.SelectedBrushPreset?.Settings.Aliased ?? false);
        CanvasControl.ActiveSymmetryMode = vm.ActiveSymmetryMode;
        CanvasControl.IsHighlighterMode = vm.IsHighlighterMode;
        CanvasControl.BrushColor = vm.PrimaryColor;

        // Initial canvas push
        CanvasControl.SetElements([.. vm.Elements]);
        CanvasControl.SetBackgroundColor(vm.BackgroundColor);
        CanvasControl.SetLogicalCanvasSize(vm.CanvasWidth, vm.CanvasHeight);

        // When element collection changes, refresh canvas
        vm.Elements.CollectionChanged += (_, _) =>
        {
            CanvasControl.SetElements([.. vm.Elements]);
        };
    }

    // ── Keyboard handling ──────────────────────────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not ImageEditorViewModel vm) return;

        // Never steal keystrokes from a focused text-entry control (e.g. the text-content
        // TextBox or the font-size NumericUpDown) — otherwise typing/backspacing there would
        // also delete the currently-selected canvas element or trigger undo/redo.
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox or NumericUpDown) return;

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        // Escape: dismiss any open modal dialogs, cancel Free Transform, cancel crop, deselect, or reset tool
        if (e.Key == Key.Escape)
        {
            if (vm.IsFreeTransformActive) { vm.CancelFreeTransformCommand.Execute(null); e.Handled = true; return; }
            if (vm.IsNewDocumentDialogOpen) { vm.IsNewDocumentDialogOpen = false; e.Handled = true; return; }
            if (vm.IsAdjustmentDialogOpen) { vm.CancelAdjustmentDialogCommand.Execute(null); e.Handled = true; return; }
            if (vm.IsFilterDialogOpen) { vm.CancelFilterDialogCommand.Execute(null); e.Handled = true; return; }
            if (vm.IsLayerStyleDialogOpen) { vm.IsLayerStyleDialogOpen = false; e.Handled = true; return; }
            if (vm.SelectedElements.Count > 0) { vm.DeselectAllCommand.Execute(null); e.Handled = true; return; }
            vm.SetToolCommand.Execute("select");
            e.Handled = true;
            return;
        }

        // Enter: commit Free Transform if active
        if (e.Key == Key.Enter && vm.IsFreeTransformActive)
        {
            vm.CommitFreeTransformCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // File Operations
        if (ctrl && !shift && !alt && e.Key == Key.N)
        {
            vm.OpenNewDocumentDialogCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && !alt && e.Key == Key.O)
        {
            vm.OpenPsdOrImageCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && !alt && e.Key == Key.S)
        {
            vm.SavePsdFileCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && (shift || alt) && e.Key == Key.S)
        {
            vm.OpenExportDialogCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Adjustments
        if (ctrl && !shift && !alt && e.Key == Key.L)
        {
            vm.OpenAdjustmentDialogCommand.Execute("Levels");
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && !alt && e.Key == Key.U)
        {
            vm.OpenAdjustmentDialogCommand.Execute("HueSaturation");
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && !alt && e.Key == Key.M)
        {
            vm.OpenAdjustmentDialogCommand.Execute("Curves");
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && !alt && e.Key == Key.B)
        {
            vm.OpenAdjustmentDialogCommand.Execute("ColorBalance");
            e.Handled = true;
            return;
        }
        if (ctrl && shift && !alt && e.Key == Key.X)
        {
            vm.OpenLiquifyDialogCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Undo / Redo
        if (ctrl && shift && e.Key == Key.Z)
        {
            vm.RedoCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && e.Key == Key.Z)
        {
            vm.UndoCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && e.Key == Key.Y)
        {
            vm.RedoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Selection & Hierarchy
        if (ctrl && !shift && e.Key == Key.A)
        {
            vm.SelectAllElementsCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && e.Key == Key.D)
        {
            vm.DeselectAllCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && (e.Key == Key.J || e.Key == Key.D))
        {
            vm.DuplicateCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && e.Key == Key.T)
        {
            vm.BeginFreeTransformCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && !shift && e.Key == Key.G)
        {
            vm.GroupSelectionCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && shift && e.Key == Key.G)
        {
            vm.UngroupSelectionCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Layer Arrangement
        if (ctrl && (e.Key == Key.OemOpenBrackets || e.Key == Key.Oem4))
        {
            vm.SendBackwardCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && (e.Key == Key.OemCloseBrackets || e.Key == Key.Oem6))
        {
            vm.BringForwardCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Zoom Viewport
        if (ctrl && (e.Key == Key.D0 || e.Key == Key.NumPad0))
        {
            vm.FitToWindowCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && (e.Key == Key.D1 || e.Key == Key.NumPad1))
        {
            vm.ResetZoomCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Clipboard
        if (ctrl && e.Key == Key.C)
        {
            vm.CopyElementsCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (ctrl && e.Key == Key.V)
        {
            vm.PasteElementsCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Delete
        if (e.Key is Key.Delete or Key.Back)
        {
            vm.DeleteSelectedElementCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (ctrl) return; // avoid stealing other Ctrl+letter shortcuts as tool switches

        // Bold/Italic only act when a TextElement is selected
        if (e.Key == Key.B && vm.SelectedTextElement != null)
        {
            vm.IsBold = !vm.IsBold;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.I && vm.SelectedTextElement != null)
        {
            vm.IsItalic = !vm.IsItalic;
            e.Handled = true;
            return;
        }

        // Sub-tool Cycling with Shift + Key (Photoshop Standard: Shift+W, Shift+M, Shift+L, Shift+B, etc.)
        if (shift)
        {
            switch (e.Key)
            {
                case Key.W: vm.CycleSubToolInSlot("w"); e.Handled = true; return;
                case Key.M: vm.CycleSubToolInSlot("m"); e.Handled = true; return;
                case Key.L: vm.CycleSubToolInSlot("l"); e.Handled = true; return;
                case Key.B: if (vm.SelectedTextElement == null) { vm.CycleSubToolInSlot("b"); e.Handled = true; return; } break;
                case Key.E: vm.CycleSubToolInSlot("e"); e.Handled = true; return;
                case Key.G: vm.CycleSubToolInSlot("g"); e.Handled = true; return;
                case Key.C: vm.CycleSubToolInSlot("c"); e.Handled = true; return;
                case Key.I: if (vm.SelectedTextElement == null) { vm.CycleSubToolInSlot("i"); e.Handled = true; return; } break;
                case Key.U: vm.CycleSubToolInSlot("u"); e.Handled = true; return;
                case Key.P: vm.CycleSubToolInSlot("p"); e.Handled = true; return;
                case Key.O: vm.CycleSubToolInSlot("o"); e.Handled = true; return;
                case Key.R: if (vm.SelectedTextElement == null) { vm.CycleSubToolInSlot("r"); e.Handled = true; return; } break;
                case Key.V: vm.CycleSubToolInSlot("v"); e.Handled = true; return;
            }
        }

        // Single-key FryImage tools (activates the active sub-tool in that slot)
        switch (e.Key)
        {
            case Key.V: vm.SelectMoveSlotCommand.Execute(null); e.Handled = true; break;
            case Key.M: vm.SelectMarqueeSlotCommand.Execute(null); e.Handled = true; break;
            case Key.L: vm.SelectLassoSlotCommand.Execute(null); e.Handled = true; break;
            case Key.W: vm.SelectWandSlotCommand.Execute(null); e.Handled = true; break;
            case Key.C: vm.SelectCropSlotCommand.Execute(null); e.Handled = true; break;
            case Key.I: if (vm.SelectedTextElement == null) { vm.SelectEyedropperSlotCommand.Execute(null); e.Handled = true; } break;
            case Key.B: if (vm.SelectedTextElement == null) { vm.SelectBrushSlotCommand.Execute(null); e.Handled = true; } break;
            case Key.E: vm.SelectEraserSlotCommand.Execute(null); e.Handled = true; break;
            case Key.G: vm.SelectGradientSlotCommand.Execute(null); e.Handled = true; break;
            case Key.T: vm.SetToolCommand.Execute("text"); e.Handled = true; break;
            case Key.U: vm.SelectShapeSlotCommand.Execute(null); e.Handled = true; break;
            case Key.H: vm.SelectHandSlotCommand.Execute(null); e.Handled = true; break;
            case Key.R: if (vm.SelectedTextElement == null) { vm.SetToolCommand.Execute("rotateview"); e.Handled = true; } break;
            case Key.Z: vm.SelectZoomSlotCommand.Execute(null); e.Handled = true; break;
            case Key.P: vm.SelectPenSlotCommand.Execute(null); e.Handled = true; break;
            case Key.O: vm.SelectToningSlotCommand.Execute(null); e.Handled = true; break;
            case Key.X: vm.SwapColorsCommand.Execute(null); e.Handled = true; break;
            case Key.D: vm.ResetColorsCommand.Execute(null); e.Handled = true; break;
            case Key.Q: vm.ToggleQuickMaskCommand.Execute(null); e.Handled = true; break;
            case Key.F: vm.FitToWindowCommand.Execute(null); e.Handled = true; break;
        }
    }

    // ── Floating quick-actions cluster ──────────────────────────────────────
    /// <summary>Positions QuickActionsCluster relative to the current selection's screen-space
    /// bounds — a no-op when nothing is selected (the cluster is hidden via its IsVisible
    /// binding in that case, so there's nothing to position).</summary>
    private void RepositionQuickActionsCluster()
    {
        if (CanvasControl.GetSelectionScreenBounds() is not { } selectionBounds) return;

        var anchor = CanvasControl.GetFloatingChromeAnchor(selectionBounds, QuickActionsCluster.Bounds.Size, 26);
        QuickActionsCluster.Margin = new Thickness(anchor.X, anchor.Y, 0, 0);
    }

    // ── Inline double-click text editing ────────────────────────────────────
    private Models.TextElement? _editingTextElement;

    private void BeginInlineTextEdit(Models.TextElement textEl)
    {
        _editingTextElement = textEl;
        if (DataContext is ImageEditorViewModel beginVm) beginVm.IsInlineEditingText = true;

        var screenRect = CanvasControl.CanvasToScreenRect(textEl.Bounds);

        InlineTextEditBox.Margin = new Thickness(screenRect.X, screenRect.Y, 0, 0);
        InlineTextEditBox.Width = Math.Max(screenRect.Width, 20);
        InlineTextEditBox.Height = Math.Max(screenRect.Height, 20);
        InlineTextEditBox.FontSize = Math.Max(textEl.FontSize * CanvasControl.Zoom, 1);
        InlineTextEditBox.FontFamily = textEl.FontFamily;
        InlineTextEditBox.FontWeight = textEl.FontWeight;
        InlineTextEditBox.FontStyle = textEl.FontStyle;
        InlineTextEditBox.Foreground = new SolidColorBrush(textEl.ForegroundColor);
        InlineTextEditBox.Text = textEl.Text;
        InlineTextEditBox.IsVisible = true;
        InlineTextEditBox.Focus();
        InlineTextEditBox.SelectAll();
    }

    private void CommitInlineTextEdit(bool cancel)
    {
        var el = _editingTextElement;
        _editingTextElement = null;
        InlineTextEditBox.IsVisible = false;

        if (DataContext is ImageEditorViewModel vm)
        {
            vm.IsInlineEditingText = false;
            if (el != null && !cancel)
            {
                vm.CommitTextEdit(el, InlineTextEditBox.Text ?? string.Empty);
            }
        }
        CanvasControl.Focus();
    }

    private void InlineTextEditBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            CommitInlineTextEdit(cancel: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CommitInlineTextEdit(cancel: true);
            e.Handled = true;
        }
    }

    private void InlineTextEditBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_editingTextElement != null) CommitInlineTextEdit(cancel: false);
    }

    // ── Clean teardown ─────────────────────────────────────────────────────
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Note: Do NOT dispose DataContext here as the view is cached by HomeViewModel
        // for instant tab switching. Teardown is managed by plugin unmount effect.
    }
}

