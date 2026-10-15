using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.ImageEditor.Controls;
using PdfEditorApp.Plugins.ImageEditor.Models.Session;

namespace PdfEditorApp.Plugins.ImageEditor.Views;

public partial class FloatingDocumentWindow : Window
{
    private StudioDocument? _targetDocument;

    public StudioDocument? TargetDocument
    {
        get => _targetDocument;
        set
        {
            _targetDocument = value;
            if (_targetDocument != null)
            {
                Title = $"{_targetDocument.Title} - FryImage Studio";
                TitleTextBlock.Text = _targetDocument.Title;
                CanvasControl.SetElements(_targetDocument.Elements.ToList());
                CanvasControl.SetLogicalCanvasSize(_targetDocument.CanvasWidth, _targetDocument.CanvasHeight);
                CanvasControl.SetBackgroundColor(_targetDocument.BackgroundColor);
                CanvasControl.SetZoom(_targetDocument.ZoomLevel);
            }
        }
    }

    public FloatingDocumentWindow()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ImageEditorViewModel vm && TargetDocument != null)
        {
            vm.SwitchDocument(TargetDocument);
        }
    }

    private void OnDockBackClicked(object? sender, RoutedEventArgs e)
    {
        if (TargetDocument != null)
        {
            TargetDocument.IsFloating = false;
        }

        if (DataContext is ImageEditorViewModel vm && TargetDocument != null)
        {
            vm.SwitchDocument(TargetDocument);
        }

        Close();
    }
}
