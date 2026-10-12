using System;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    private bool _isWarpTextDialogOpen;
    private TextWarpStyle _warpTextStyle = TextWarpStyle.Arc;
    private double _warpTextBend = 50.0;
    private double _warpTextHorizontalDistortion = 0.0;
    private double _warpTextVerticalDistortion = 0.0;
    private bool _warpTextIsHorizontal = true;
    private bool _warpTextPreview = true;
    private TextWarpSettings? _originalWarpBackup;

    public bool IsWarpTextDialogOpen
    {
        get => _isWarpTextDialogOpen;
        set => SetProperty(ref _isWarpTextDialogOpen, value);
    }

    public TextWarpStyle[] AvailableWarpStyles { get; } = Enum.GetValues<TextWarpStyle>();

    public TextWarpStyle WarpTextStyle
    {
        get => _warpTextStyle;
        set
        {
            if (SetProperty(ref _warpTextStyle, value))
            {
                ApplyLiveWarpPreview();
            }
        }
    }

    public double WarpTextBend
    {
        get => _warpTextBend;
        set
        {
            if (SetProperty(ref _warpTextBend, value))
            {
                ApplyLiveWarpPreview();
            }
        }
    }

    public double WarpTextHorizontalDistortion
    {
        get => _warpTextHorizontalDistortion;
        set
        {
            if (SetProperty(ref _warpTextHorizontalDistortion, value))
            {
                ApplyLiveWarpPreview();
            }
        }
    }

    public double WarpTextVerticalDistortion
    {
        get => _warpTextVerticalDistortion;
        set
        {
            if (SetProperty(ref _warpTextVerticalDistortion, value))
            {
                ApplyLiveWarpPreview();
            }
        }
    }

    public bool WarpTextIsHorizontal
    {
        get => _warpTextIsHorizontal;
        set
        {
            if (SetProperty(ref _warpTextIsHorizontal, value))
            {
                OnPropertyChanged(nameof(WarpTextOrientation));
                ApplyLiveWarpPreview();
            }
        }
    }

    public string WarpTextOrientation
    {
        get => _warpTextIsHorizontal ? "Horizontal" : "Vertical";
        set => WarpTextIsHorizontal = string.Equals(value, "Horizontal", StringComparison.OrdinalIgnoreCase);
    }

    public bool WarpTextPreview
    {
        get => _warpTextPreview;
        set
        {
            if (SetProperty(ref _warpTextPreview, value))
            {
                if (value) ApplyLiveWarpPreview();
                else RevertLiveWarpPreview();
            }
        }
    }

    private TextElement? GetActiveTextElement()
    {
        if (SelectedElement is TextElement te) return te;
        return Elements.OfType<TextElement>().LastOrDefault();
    }

    public void OpenWarpTextDialog()
    {
        var targetText = GetActiveTextElement();
        if (targetText == null)
        {
            StatusMessage = "Please select or create a Text layer to warp.";
            return;
        }

        SelectedElement = targetText;
        _originalWarpBackup = targetText.Warp?.Clone() ?? new TextWarpSettings();

        // Initialize dialog values from layer's current warp settings
        _warpTextStyle = _originalWarpBackup.Style != TextWarpStyle.None ? _originalWarpBackup.Style : TextWarpStyle.Arc;
        _warpTextBend = _originalWarpBackup.Bend;
        _warpTextHorizontalDistortion = _originalWarpBackup.HorizontalDistortion;
        _warpTextVerticalDistortion = _originalWarpBackup.VerticalDistortion;
        _warpTextIsHorizontal = _originalWarpBackup.IsHorizontal;
        _warpTextPreview = true;

        OnPropertyChanged(nameof(WarpTextStyle));
        OnPropertyChanged(nameof(WarpTextBend));
        OnPropertyChanged(nameof(WarpTextHorizontalDistortion));
        OnPropertyChanged(nameof(WarpTextVerticalDistortion));
        OnPropertyChanged(nameof(WarpTextIsHorizontal));
        OnPropertyChanged(nameof(WarpTextOrientation));
        OnPropertyChanged(nameof(WarpTextPreview));

        IsWarpTextDialogOpen = true;
        StatusMessage = "Warp Text dialog open.";
    }

    private void ApplyLiveWarpPreview()
    {
        if (!_isWarpTextDialogOpen || !_warpTextPreview) return;
        var targetText = GetActiveTextElement();
        if (targetText == null) return;

        targetText.Warp = new TextWarpSettings
        {
            Style = WarpTextStyle,
            Bend = WarpTextBend,
            HorizontalDistortion = WarpTextHorizontalDistortion,
            VerticalDistortion = WarpTextVerticalDistortion,
            IsHorizontal = WarpTextIsHorizontal
        };

        CanvasControl?.InvalidateVisual();
    }

    private void RevertLiveWarpPreview()
    {
        var targetText = GetActiveTextElement();
        if (targetText == null || _originalWarpBackup == null) return;

        targetText.Warp = _originalWarpBackup.Clone();
        CanvasControl?.InvalidateVisual();
    }

    public void ConfirmWarpTextDialog()
    {
        var targetText = GetActiveTextElement();
        if (targetText != null)
        {
            var oldWarp = _originalWarpBackup?.Clone() ?? new TextWarpSettings();
            var newWarp = new TextWarpSettings
            {
                Style = WarpTextStyle,
                Bend = WarpTextBend,
                HorizontalDistortion = WarpTextHorizontalDistortion,
                VerticalDistortion = WarpTextVerticalDistortion,
                IsHorizontal = WarpTextIsHorizontal
            };
            targetText.Warp = newWarp;

            if (!_isApplyingHistory)
            {
                History.Push(new Models.Undo.GenericPropertyCommand<TextElement, TextWarpSettings>(
                    targetText,
                    (el, w) => { el.Warp = w?.Clone() ?? new TextWarpSettings(); CanvasControl?.InvalidateVisual(); },
                    oldWarp,
                    newWarp.Clone(),
                    $"Warp Text: {WarpTextStyle}"));
            }

            CanvasControl?.InvalidateVisual();
        }

        IsWarpTextDialogOpen = false;
        StatusMessage = $"Applied text warp: {WarpTextStyle}.";
    }

    public void CancelWarpTextDialog()
    {
        RevertLiveWarpPreview();
        IsWarpTextDialogOpen = false;
        StatusMessage = "Warp text canceled.";
    }

    public ICommand OpenWarpTextDialogCommand => new RelayCommand(OpenWarpTextDialog);
    public ICommand ConfirmWarpTextDialogCommand => new RelayCommand(ConfirmWarpTextDialog);
    public ICommand CancelWarpTextDialogCommand => new RelayCommand(CancelWarpTextDialog);
}
