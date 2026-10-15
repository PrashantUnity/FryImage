using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PdfEditorApp.Plugins.ImageEditor.Models.Session;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class DocumentTabStrip : UserControl
{
    private const double TearOffThreshold = 24.0;

    private StudioDocument? _draggedDoc;
    private Point _dragStartPos;
    private bool _isDragging;
    private bool _isTearOff;

    public DocumentTabStrip()
    {
        InitializeComponent();
    }

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (sender is Control { DataContext: StudioDocument doc } && DataContext is ImageEditorViewModel vm)
        {
            _draggedDoc = doc;
            _dragStartPos = e.GetPosition(this);
            _isDragging = false;
            _isTearOff = false;

            if (vm.ActiveDocument != doc)
            {
                vm.SwitchDocument(doc);
            }
        }
    }

    private void OnTabPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedDoc == null) return;

        var currentPos = e.GetPosition(this);
        double dx = currentPos.X - _dragStartPos.X;
        double dy = currentPos.Y - _dragStartPos.Y;

        if (!_isDragging && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            // Vertical movement beyond threshold initiates tear-off gesture
            if (Math.Abs(dy) > TearOffThreshold)
            {
                if (!_isTearOff && DataContext is ImageEditorViewModel vm)
                {
                    _isTearOff = true;
                    vm.StatusMessage = $"Tear-off '{_draggedDoc.Title}': Release to float in window or dock to split.";
                }
            }
            else
            {
                _isTearOff = false;

                // Horizontal re-ordering within the tab strip
                if (DataContext is ImageEditorViewModel vm && sender is Control currentTab)
                {
                    int fromIdx = vm.Documents.IndexOf(_draggedDoc);
                    if (fromIdx >= 0)
                    {
                        // Check if pointer moved past adjacent tab centers
                        if (dx > 40 && fromIdx < vm.Documents.Count - 1)
                        {
                            vm.ReorderDocuments(fromIdx, fromIdx + 1);
                            _dragStartPos = currentPos;
                        }
                        else if (dx < -40 && fromIdx > 0)
                        {
                            vm.ReorderDocuments(fromIdx, fromIdx - 1);
                            _dragStartPos = currentPos;
                        }
                    }
                }
            }
        }
    }

    private void OnTabPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedDoc != null && _isTearOff && DataContext is ImageEditorViewModel vm)
        {
            vm.FloatInWindow(_draggedDoc);
        }

        _draggedDoc = null;
        _isDragging = false;
        _isTearOff = false;
    }
}
