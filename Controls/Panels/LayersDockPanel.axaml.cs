using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Panels;

public partial class LayersDockPanel : UserControl
{
    public LayersDockPanel()
    {
        InitializeComponent();
    }

    private void OnLayersListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm)
        {
            var target = vm.SelectedElement;
            if (target != null)
            {
                if (target.HasBackgroundTraits)
                {
                    vm.ConvertBackgroundToLayerCommand.Execute(target);
                }
                else
                {
                    vm.OpenRenameLayerDialogCommand.Execute(target);
                }
            }
        }
    }

    private void OnBlendModePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is ComboBox combo && combo.ItemsSource != null)
        {
            var list = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<object>((System.Collections.IEnumerable)combo.ItemsSource));
            if (list.Count == 0) return;
            int idx = combo.SelectedIndex;
            if (idx < 0) idx = 0;
            if (e.Delta.Y > 0)
                idx = Math.Max(0, idx - 1);
            else if (e.Delta.Y < 0)
                idx = Math.Min(list.Count - 1, idx + 1);

            combo.SelectedIndex = idx;
            e.Handled = true;
        }
    }

    private void OnLayerRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: Models.CanvasElement el } && DataContext is ImageEditorViewModel vm)
        {
            vm.HoveredElement = el;
        }
    }

    private void OnLayerRowPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm)
        {
            vm.HoveredElement = null;
        }
    }

    private void OnNewLayerButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm)
        {
            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsLeftButtonPressed)
            {
                bool isCtrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
                vm.AddNewBlankLayer(below: isCtrlOrCmd);
                e.Handled = true;
            }
        }
    }

    private Point _gripDragStart;
    private bool _isPotentialGripDrag;

    public void OnPanelGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPotentialGripDrag = true;
            _gripDragStart = e.GetPosition(this);
        }
    }

    public void OnPanelGripPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPotentialGripDrag || DataContext is not ImageEditorViewModel vm) return;
        var curPos = e.GetPosition(this);
        var diff = curPos - _gripDragStart;
        if (Math.Abs(diff.X) > 14 || Math.Abs(diff.Y) > 14)
        {
            _isPotentialGripDrag = false;
            vm.StartPanelDrag("Layers", curPos.X, curPos.Y);
        }
    }

    // Retained for backward-compatibility with existing unit test signatures
    public void OnPanelHeaderPointerPressed(object? sender, PointerPressedEventArgs e) { }
    public void OnPanelHeaderPointerMoved(object? sender, PointerEventArgs e) { }
}

