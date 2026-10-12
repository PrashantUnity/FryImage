using System;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Undo;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    [ObservableProperty] private bool _isFreeTransformActive;
    [ObservableProperty] private double _transformX;
    [ObservableProperty] private double _transformY;
    [ObservableProperty] private double _transformWPercent = 100.0;
    [ObservableProperty] private double _transformHPercent = 100.0;
    [ObservableProperty] private bool _transformAspectLocked = true;
    [ObservableProperty] private double _transformAngle = 0.0;
    [ObservableProperty] private double _transformSkewH = 0.0;
    [ObservableProperty] private double _transformSkewV = 0.0;
    [ObservableProperty] private string _transformInterpolation = "Bicubic";

    private CanvasElement? _transformTargetElement;
    private double _origX;
    private double _origY;
    private double _origW;
    private double _origH;
    private double _origRot;
    private bool _isUpdatingTransformFields;

    [RelayCommand]
    public void BeginFreeTransform()
    {
        var target = SelectedElement ?? Elements.LastOrDefault();
        if (target == null)
        {
            StatusMessage = "No element selected to transform.";
            return;
        }

        if (target.IsLocked)
        {
            StatusMessage = "Cannot transform a locked layer.";
            return;
        }

        // If another session was active, commit it first
        if (IsFreeTransformActive)
        {
            CommitFreeTransform();
        }

        _transformTargetElement = target;
        _origX = target.X;
        _origY = target.Y;
        _origW = Math.Max(target.Width, 1.0);
        _origH = Math.Max(target.Height, 1.0);
        _origRot = target.RotationDegrees;

        _isUpdatingTransformFields = true;
        try
        {
            TransformX = Math.Round(target.X, 1);
            TransformY = Math.Round(target.Y, 1);
            TransformWPercent = 100.0;
            TransformHPercent = 100.0;
            TransformAspectLocked = target.AspectLocked;
            TransformAngle = Math.Round(target.RotationDegrees, 1);
            TransformSkewH = 0.0;
            TransformSkewV = 0.0;
            TransformAnchor = "Center";
        }
        finally
        {
            _isUpdatingTransformFields = false;
        }

        IsFreeTransformActive = true;
        StatusMessage = "Free Transform: Enter to commit, Esc to cancel.";
    }

    [RelayCommand]
    public void CommitFreeTransform()
    {
        if (!IsFreeTransformActive || _transformTargetElement == null)
        {
            IsFreeTransformActive = false;
            return;
        }

        var el = _transformTargetElement;
        var before = (_origX, _origY, _origW, _origH, _origRot);
        var after = (el.X, el.Y, el.Width, el.Height, el.RotationDegrees);

        if (before != after && !_isApplyingHistory)
        {
            History.Push(new FreeTransformCommand(el, before, after));
        }

        IsFreeTransformActive = false;
        _transformTargetElement = null;
        StatusMessage = "Transform committed.";
        RefreshCanvas();
    }

    [RelayCommand]
    public void CancelFreeTransform()
    {
        if (!IsFreeTransformActive || _transformTargetElement == null)
        {
            IsFreeTransformActive = false;
            return;
        }

        var el = _transformTargetElement;
        el.X = _origX;
        el.Y = _origY;
        el.Width = _origW;
        el.Height = _origH;
        el.RotationDegrees = _origRot;

        IsFreeTransformActive = false;
        _transformTargetElement = null;
        StatusMessage = "Transform canceled.";
        RefreshCanvas();
    }

    [RelayCommand]
    public void ToggleTransformAspectLock()
    {
        TransformAspectLocked = !TransformAspectLocked;
    }

    partial void OnTransformXChanged(double value) => ApplyTransformValuesToElement();
    partial void OnTransformYChanged(double value) => ApplyTransformValuesToElement();

    partial void OnTransformWPercentChanged(double value)
    {
        if (_isUpdatingTransformFields) return;
        if (TransformAspectLocked)
        {
            _isUpdatingTransformFields = true;
            TransformHPercent = value;
            _isUpdatingTransformFields = false;
        }
        ApplyTransformValuesToElement();
    }

    partial void OnTransformHPercentChanged(double value)
    {
        if (_isUpdatingTransformFields) return;
        if (TransformAspectLocked)
        {
            _isUpdatingTransformFields = true;
            TransformWPercent = value;
            _isUpdatingTransformFields = false;
        }
        ApplyTransformValuesToElement();
    }

    partial void OnTransformAngleChanged(double value) => ApplyTransformValuesToElement();

    private void ApplyTransformValuesToElement()
    {
        if (_isUpdatingTransformFields || !IsFreeTransformActive || _transformTargetElement == null)
            return;

        var el = _transformTargetElement;
        double newW = Math.Max(1.0, _origW * (TransformWPercent / 100.0));
        double newH = Math.Max(1.0, _origH * (TransformHPercent / 100.0));

        var (nx, ny) = TransformAnchor switch
        {
            "TopLeft" => (0.0, 0.0),
            "TopCenter" => (0.5, 0.0),
            "TopRight" => (1.0, 0.0),
            "MidLeft" => (0.0, 0.5),
            "Center" => (0.5, 0.5),
            "MidRight" => (1.0, 0.5),
            "BotLeft" => (0.0, 1.0),
            "BotCenter" => (0.5, 1.0),
            "BotRight" => (1.0, 1.0),
            _ => (0.5, 0.5)
        };

        // Anchor location in original space
        double anchorOrigX = _origX + nx * _origW;
        double anchorOrigY = _origY + ny * _origH;

        // Position delta if user adjusted TransformX/TransformY
        double offsetX = TransformX - _origX;
        double offsetY = TransformY - _origY;

        double targetAnchorX = anchorOrigX + offsetX;
        double targetAnchorY = anchorOrigY + offsetY;

        el.X = targetAnchorX - nx * newW;
        el.Y = targetAnchorY - ny * newH;
        el.Width = newW;
        el.Height = newH;
        el.RotationDegrees = TransformAngle;

        RefreshCanvas();
    }

    /// <summary>
    /// Synchronizes Free Transform session fields if canvas handles drag or rotate.
    /// </summary>
    public void SyncTransformSessionFromElement(CanvasElement el)
    {
        if (!IsFreeTransformActive || _transformTargetElement != el) return;
        _isUpdatingTransformFields = true;
        try
        {
            TransformX = Math.Round(el.X, 1);
            TransformY = Math.Round(el.Y, 1);
            TransformWPercent = Math.Round((el.Width / Math.Max(1.0, _origW)) * 100.0, 1);
            TransformHPercent = Math.Round((el.Height / Math.Max(1.0, _origH)) * 100.0, 1);
            TransformAngle = Math.Round(el.RotationDegrees, 1);
        }
        finally
        {
            _isUpdatingTransformFields = false;
        }
    }
}
