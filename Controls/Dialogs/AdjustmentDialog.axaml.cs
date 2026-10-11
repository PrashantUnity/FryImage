using System;
using Avalonia.Controls;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Dialogs;

public partial class AdjustmentDialog : UserControl
{
    public AdjustmentDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not ImageEditorViewModel vm) return;

        CurvesGraph.CurveChanged += () =>
        {
            vm.NotifyCurveChangedCommand.Execute(null);
        };

        CurvesGraph.SelectedPointChanged += pt =>
        {
            vm.SelectCurvesPointCommand.Execute(pt);
        };
    }
}
