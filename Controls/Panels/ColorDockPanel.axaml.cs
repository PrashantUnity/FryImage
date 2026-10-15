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

    private Point _headerDragStart;
    private bool _isPotentialHeaderDrag;

    public void OnPanelHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPotentialHeaderDrag = true;
            _headerDragStart = e.GetPosition(this);
        }
    }

    public void OnPanelHeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPotentialHeaderDrag || DataContext is not ImageEditorViewModel vm) return;
        var curPos = e.GetPosition(this);
        var diff = curPos - _headerDragStart;
        if (Math.Abs(diff.X) > 6 || Math.Abs(diff.Y) > 6)
        {
            _isPotentialHeaderDrag = false;
            vm.StartPanelDrag("Color", curPos.X, curPos.Y);
        }
    }
}
