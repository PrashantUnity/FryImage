using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.ImageEditor.Core.Vector;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;

namespace PdfEditorApp.Plugins.ImageEditor.Models.Session;

/// <summary>
/// Represents an open document session in FryImage.
/// Encapsulates per-document elements, history, dimensions, guides, and viewport state.
/// </summary>
public partial class StudioDocument : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty] private string _title = "Untitled-1";
    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private double _canvasWidth = 1920;
    [ObservableProperty] private double _canvasHeight = 1080;
    [ObservableProperty] private Color _backgroundColor = Colors.White;
    [ObservableProperty] private double _zoomLevel = 1.0;
    [ObservableProperty] private Point _panOffset = new(0, 0);
    [ObservableProperty] private double _rotationDegrees;
    [ObservableProperty] private string _colorMode = "RGB";
    [ObservableProperty] private int _bitsPerChannel = 8;
    [ObservableProperty] private bool _isFloating;
    [ObservableProperty] private bool _isSplitView;

    public ObservableCollection<CanvasElement> Elements { get; set; } = [];
    public IEditHistory History { get; set; } = new EditHistory();
    public ObservableCollection<HistoryItemViewModel> HistoryItems { get; } = [];
    public ObservableCollection<VectorDocumentPath> DocumentPaths { get; } = [];
    public List<(bool IsVertical, double Position, Color Color)> UserGuides { get; } = [];

    public StudioDocument()
    {
    }

    public StudioDocument(string title, double width, double height, Color background)
    {
        _title = title;
        _canvasWidth = width;
        _canvasHeight = height;
        _backgroundColor = background;
    }
}
