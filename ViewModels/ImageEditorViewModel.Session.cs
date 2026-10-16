using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Models.Session;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Multi-Document Session State ────────────────────────────────────────
    public ObservableCollection<StudioDocument> Documents { get; } = [];

    [ObservableProperty]
    private StudioDocument? _activeDocument;

    [ObservableProperty]
    private StudioDocument? _secondaryDocument;

    [ObservableProperty]
    private string _splitLayoutMode = "None"; // "None", "TwoUpVertical", "TwoUpHorizontal", "FourUp"


    private int _documentCounter = 1;

    public void InitializeDocumentSession()
    {
        if (Documents.Count == 0)
        {
            var initialDoc = new StudioDocument(
                string.IsNullOrWhiteSpace(DocumentTitle) ? "Untitled-1" : DocumentTitle,
                CanvasWidth > 0 ? CanvasWidth : 1920,
                CanvasHeight > 0 ? CanvasHeight : 1080,
                BackgroundColor
            )
            {
                ColorMode = DocumentColorMode ?? "RGB",
                BitsPerChannel = DocumentBitsPerChannel,
                ZoomLevel = ZoomLevel > 0 ? ZoomLevel : 1.0,
                Elements = Elements,
                History = History
            };
            Documents.Add(initialDoc);
            ActiveDocument = initialDoc;
            initialDoc.History.HistoryChanged += OnDocumentHistoryChanged;
        }
    }

    [ObservableProperty]
    private double _layersScrollOffset;

    [RelayCommand]
    public void SwitchDocument(StudioDocument? doc)
    {
        if (doc == null || doc == ActiveDocument) return;

        if (IsInlineEditingText)
        {
            RequestCommitInlineTextEdit();
        }

        if (IsCropping)
        {
            CancelCrop();
        }

        // Drop open distort, transform, and filter sessions on departing document (Studio Parity 94fdd279, #5092, #5123)
        if (IsLiquifyDialogOpen)
        {
            CancelLiquifyDialog();
        }

        if (IsFreeTransformActive)
        {
            CommitFreeTransform();
        }

        if (IsContentAwareScaleDialogOpen)
        {
            CancelContentAwareScaleDialog();
        }

        if (IsFilterDialogOpen)
        {
            CancelFilterDialog();
        }

        if (IsAdjustmentDialogOpen)
        {
            CancelAdjustmentDialog();
        }

        if (IsLayerStyleDialogOpen)
        {
            CloseLayerStyleDialog();
        }

        // Cancel stale polygon / lasso operations from previous document (#cd35577c, #5536)
        CanvasControl?.CancelLasso();

        // Persist current canvas state into departing active document
        if (ActiveDocument != null)
        {
            ActiveDocument.CanvasWidth = CanvasWidth;
            ActiveDocument.CanvasHeight = CanvasHeight;
            ActiveDocument.BackgroundColor = BackgroundColor;
            ActiveDocument.ZoomLevel = ZoomLevel;
            ActiveDocument.Title = DocumentTitle;
            ActiveDocument.ColorMode = DocumentColorMode;
            ActiveDocument.BitsPerChannel = DocumentBitsPerChannel;
            ActiveDocument.Elements = Elements;
            ActiveDocument.History = History;
            ActiveDocument.History.HistoryChanged -= OnDocumentHistoryChanged;
            ActiveDocument.LayersScrollOffset = LayersScrollOffset;
            ActiveDocument.ActiveVectorPath = CanvasControl?.ActiveDocumentPath?.Clone();
            ActiveDocument.ActiveSelectionBounds = CanvasControl?.ActiveSelectionBounds;
            ActiveDocument.SelectedElement = SelectedElement;
        }

        // Activate new document
        ActiveDocument = doc;

        Elements = doc.Elements;
        CanvasWidth = doc.CanvasWidth;
        CanvasHeight = doc.CanvasHeight;
        BackgroundColor = doc.BackgroundColor;
        DocumentTitle = doc.Title;
        DocumentColorMode = doc.ColorMode;
        DocumentBitsPerChannel = doc.BitsPerChannel;
        ZoomLevel = doc.ZoomLevel;
        LayersScrollOffset = doc.LayersScrollOffset;
        History = doc.History;
        History.HistoryChanged += OnDocumentHistoryChanged;

        // Sync view canvas control
        CanvasControl?.SetElements(doc.Elements.ToList());
        CanvasControl?.SetLogicalCanvasSize(doc.CanvasWidth, doc.CanvasHeight);
        CanvasControl?.SetBackgroundColor(doc.BackgroundColor);
        CanvasControl?.SetZoom(doc.ZoomLevel);
        if (CanvasControl != null)
        {
            CanvasControl.ActiveDocumentPath = doc.ActiveVectorPath?.Clone();
            CanvasControl.ActiveSelectionBounds = doc.ActiveSelectionBounds;
        }
        SelectedElement = doc.SelectedElement;

        OnPropertyChanged(nameof(LayersDescendingZ));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        RebuildHistoryItems();
        _ = UpdateHistogramAsync();

        StatusMessage = $"Switched to document '{doc.Title}'.";
    }

    private void OnDocumentHistoryChanged(object? sender, EventArgs e)
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        RebuildHistoryItems();
        _ = UpdateHistogramAsync();
        if (ActiveDocument != null)
        {
            ActiveDocument.IsDirty = History.CanUndo;
        }
    }

    [RelayCommand]
    public void CreateNewDocumentTab()
    {
        // An unfinished Pen path belongs to the current document, finish/commit before creating new doc (#9b59a8ca, #5460)
        if (CanvasControl?.ActiveDocumentPath != null && !CanvasControl.ActiveDocumentPath.IsEmpty)
        {
            ActiveDocument?.DocumentPaths.Add(CanvasControl.ActiveDocumentPath.Clone());
            CanvasControl.ActiveDocumentPath = null;
        }

        _documentCounter++;
        string title = $"Untitled-{_documentCounter}";
        var newDoc = new StudioDocument(title, 1920, 1080, Colors.White)
        {
            ColorMode = "RGB",
            BitsPerChannel = 8,
            ZoomLevel = 1.0
        };
        Documents.Add(newDoc);
        SwitchDocument(newDoc);
        StatusMessage = $"Created new document '{newDoc.Title}'.";
    }

    [RelayCommand]
    public void CloseDocument(StudioDocument? doc)
    {
        doc ??= ActiveDocument;
        if (doc == null) return;

        int index = Documents.IndexOf(doc);
        if (index < 0) return;

        Documents.Remove(doc);

        if (doc == ActiveDocument)
        {
            if (Documents.Count > 0)
            {
                int newIdx = Math.Clamp(index, 0, Documents.Count - 1);
                SwitchDocument(Documents[newIdx]);
            }
            else
            {
                CreateNewDocumentTab();
            }
        }

        StatusMessage = $"Closed document '{doc.Title}'.";
    }

    public void ReorderDocuments(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Documents.Count ||
            toIndex < 0 || toIndex >= Documents.Count ||
            fromIndex == toIndex) return;

        Documents.Move(fromIndex, toIndex);
    }
}
