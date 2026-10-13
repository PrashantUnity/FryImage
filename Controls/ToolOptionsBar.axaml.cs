using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.ImageEditor.Controls;

public partial class ToolOptionsBar : UserControl
{
    public ToolOptionsBar()
    {
        InitializeComponent();
    }

    private void OnBlendModePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is ComboBox combo && combo.ItemsSource != null)
        {
            var list = combo.ItemsSource.Cast<object>().ToList();
            if (list.Count == 0) return;
            int idx = combo.SelectedIndex;
            if (idx < 0) idx = 0;
            if (e.Delta.Y > 0)
                idx = Math.Max(0, idx - 1);
            else if (e.Delta.Y < 0)
                idx = Math.Min(list.Count - 1, idx + 1);

            combo.SelectedIndex = idx;
            e.Handled = true;
        }
    }
}
