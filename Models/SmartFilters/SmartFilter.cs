using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

/// <summary>
/// Non-destructive Smart Filter applied to a Smart Object or layer (Photoshop specification parity).
/// Allows toggling visibility, re-ordering, re-editing parameters, and dragging/duplicating between layers.
/// </summary>
public sealed class SmartFilter
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "Gaussian Blur";
    public string FilterType { get; set; } = "GaussianBlur";
    public bool IsVisible { get; set; } = true;
    public double Radius { get; set; } = 5.0;
    public double Amount { get; set; } = 1.0;
    public Dictionary<string, object> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SmartFilter Clone()
    {
        var copy = new SmartFilter
        {
            Name = Name,
            FilterType = FilterType,
            IsVisible = IsVisible,
            Radius = Radius,
            Amount = Amount
        };
        foreach (var kvp in Parameters)
            copy.Parameters[kvp.Key] = kvp.Value;
        return copy;
    }
}
