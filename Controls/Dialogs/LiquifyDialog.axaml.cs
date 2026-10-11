using System;
using Avalonia.Controls;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Dialogs;

public partial class LiquifyDialog : UserControl
{
    public LiquifyDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not ImageEditorViewModel vm) return;

        vm.LiquifySessionStarted += (bgra, width, height) =>
        {
            LiquifyCanvas.Initialize(bgra, width, height);
        };

        vm.LiquifyResultRequested += () => LiquifyCanvas.GetResultBgra();

        vm.LiquifyReconstructRequested += percentage =>
        {
            LiquifyCanvas.ReconstructAll(percentage);
        };

        vm.LiquifyResetRequested += () =>
        {
            LiquifyCanvas.ResetAll();
        };
    }
}
