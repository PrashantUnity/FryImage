using System;
using CommunityToolkit.Mvvm.Input;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    public void PromptConfirmDiscard(string message, Action onDiscard)
    {
        DiscardConfirmMessage = message;
        _pendingDiscardAction = onDiscard;
        IsConfirmingDiscard = true;
    }

    [RelayCommand]
    private void SaveAndDiscard()
    {
        _ = SaveProjectJsonAsync();
        ConfirmDiscard();
    }
}
