using Avalonia.Controls;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.ImageEditor.Controls.Dialogs;

public partial class ColorPickerDialog : UserControl
{
    public ColorPickerDialog()
    {
        InitializeComponent();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Right-click dismisses / cancels the picker (Photoshop parity #4291, #4636)
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            if (DataContext is ImageEditorViewModel vm)
            {
                vm.CancelColorPicker();
                e.Handled = true;
            }
        }
    }
}
