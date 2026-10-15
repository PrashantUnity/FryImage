using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Panels;

public partial class ColorDockPanel : UserControl
{
    public ColorDockPanel()
    {
        InitializeComponent();
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
            vm.StartPanelDrag("Color", curPos.X, curPos.Y);
        }
    }

    // Retained for backward-compatibility with existing unit test signatures
    public void OnPanelHeaderPointerPressed(object? sender, PointerPressedEventArgs e) { }
    public void OnPanelHeaderPointerMoved(object? sender, PointerEventArgs e) { }
}
