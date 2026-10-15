using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Non-destructive Smart Filter applied to a Smart Object or layer (Studio specification parity).
/// Allows toggling visibility, re-ordering, re-editing parameters, and dragging/duplicating between layers.
/// </summary>
public sealed partial class SmartFilter : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = "Gaussian Blur";

    [ObservableProperty]
    private string _filterType = "GaussianBlur";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private double _radius = 5.0;

    [ObservableProperty]
    private double _amount = 1.0;

    [ObservableProperty]
    private bool _isSelected;

    public Dictionary<string, object> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SmartFilter Clone()
    {
        var copy = new SmartFilter
        {
            Name = Name,
            FilterType = FilterType,
            IsVisible = IsVisible,
            Radius = Radius,
            Amount = Amount,
            IsSelected = IsSelected
        };
        foreach (var kvp in Parameters)
            copy.Parameters[kvp.Key] = kvp.Value;
        return copy;
    }
}

