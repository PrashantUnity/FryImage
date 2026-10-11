using System;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor;

/// <summary>Case-insensitive string equality — used for "is this the active tool/preset" style bindings.</summary>
public class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>True when the bound value is an <see cref="ImageElement"/> — gates
/// visibility of image-only controls (crop, adjustments, aspect-lock).</summary>
public class IsImageElementConverter : IValueConverter
{
    public static readonly IsImageElementConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is ImageElement;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Maps CanvasElement.ElementType to a Material Design icon kind.</summary>
public class ElementTypeToIconConverter : IValueConverter
{
    public static readonly ElementTypeToIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Text" => Material.Icons.MaterialIconKind.FormatText,
            "Rectangle" => Material.Icons.MaterialIconKind.RectangleOutline,
            "Ellipse" => Material.Icons.MaterialIconKind.EllipseOutline,
            "Arrow" => Material.Icons.MaterialIconKind.ArrowTopRight,
            "Image" => Material.Icons.MaterialIconKind.ImageOutline,
            "Polygon" => Material.Icons.MaterialIconKind.HexagonOutline,
            "Star" => Material.Icons.MaterialIconKind.StarOutline,
            "Drawing" => Material.Icons.MaterialIconKind.Brush,
            "Highlighter" => Material.Icons.MaterialIconKind.Marker,
            _ => Material.Icons.MaterialIconKind.ShapeOutline
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

public class IsLockedToIconConverter : IValueConverter
{
    public static readonly IsLockedToIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is true ? Material.Icons.MaterialIconKind.LockOutline : Material.Icons.MaterialIconKind.LockOpenOutline;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

public class IsVisibleToIconConverter : IValueConverter
{
    public static readonly IsVisibleToIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is false ? Material.Icons.MaterialIconKind.EyeOffOutline : Material.Icons.MaterialIconKind.EyeOutline;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

public class ZoomLevelToPercentConverter : IValueConverter
{
    public static readonly ZoomLevelToPercentConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double zoom) return $"{(int)Math.Round(zoom * 100)}%";
        return "100%";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

public class EqualityToBooleanConverter : IValueConverter
{
    public static readonly EqualityToBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null && parameter == null) return true;
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

public class BooleanToStringConverter : IValueConverter
{
    public static readonly BooleanToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b && parameter is string param)
        {
            var parts = param.Split('|', ':');
            if (parts.Length == 2) return b ? parts[0] : parts[1];
        }
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

public class ColorToBrushConverter : IValueConverter
{
    public static readonly ColorToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is Color c) return new SolidColorBrush(c);
        if (value is string s && Color.TryParse(s, out var parsed)) return new SolidColorBrush(parsed);
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => (value as ISolidColorBrush)?.Color ?? Colors.Transparent;
}
