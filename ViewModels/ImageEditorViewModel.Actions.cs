using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Actions;
using PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Actions Panel State (Studio .atn Parity) ─────────────────────────
    [ObservableProperty]
    private ObservableCollection<AtnCodec.AtnFile> _actionSets = [];

    [ObservableProperty]
    private AtnCodec.AtnAction? _selectedAction;

    [ObservableProperty]
    private AtnCodec.AtnFile? _selectedActionSet;

    [ObservableProperty]
    private bool _isActionRecording;

    [ObservableProperty]
    private bool _isActionPlaying;

    public void InitializeDefaultActions()
    {
        if (ActionSets.Count > 0) return;
        var sets = ActionPlaybackEngine.CreateDefaultActionSets();
        foreach (var s in sets) ActionSets.Add(s);
        SelectedActionSet = ActionSets.FirstOrDefault();
        SelectedAction = SelectedActionSet?.Actions.FirstOrDefault();
    }

    [RelayCommand]
    public void SelectAction(AtnCodec.AtnAction? action)
    {
        SelectedAction = action;
        if (action != null)
            StatusMessage = $"Selected action: {action.Name}";
    }

    [RelayCommand]
    public async Task PlaySelectedActionAsync()
    {
        if (SelectedAction == null || IsActionPlaying) return;

        try
        {
            IsActionPlaying = true;
            await ActionPlaybackEngine.ExecuteActionAsync(SelectedAction, this);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error playing action: {ex.Message}";
        }
        finally
        {
            IsActionPlaying = false;
        }
    }

    [RelayCommand]
    public void ToggleActionRecording()
    {
        IsActionRecording = !IsActionRecording;
        StatusMessage = IsActionRecording ? "Recording actions..." : "Action recording stopped.";
    }

    [RelayCommand]
    public void StopAction()
    {
        IsActionRecording = false;
        IsActionPlaying = false;
        StatusMessage = "Action stopped.";
    }

    [RelayCommand]
    public async Task LoadActionsFileAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load Actions (.atn)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Actions (*.atn)") { Patterns = ["*.atn"] },
                    new FilePickerFileType("All Files") { Patterns = ["*.*"] }
                ]
            });

            if (files.Count == 0) return;

            await using var stream = await files[0].OpenReadAsync();
            var atnFile = AtnCodec.Decode(stream);
            ActionSets.Add(atnFile);
            SelectedActionSet = atnFile;
            SelectedAction = atnFile.Actions.FirstOrDefault();
            StatusMessage = $"Loaded action set '{atnFile.Name}' with {atnFile.Actions.Count} actions.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load actions file: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveActionsFileAsync()
    {
        try
        {
            var targetSet = SelectedActionSet ?? ActionSets.FirstOrDefault();
            if (targetSet == null) return;

            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is not { } storage) return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Actions (.atn)",
                DefaultExtension = "atn",
                SuggestedFileName = $"{targetSet.Name}.atn",
                FileTypeChoices =
                [
                    new FilePickerFileType("Actions (*.atn)") { Patterns = ["*.atn"] }
                ]
            });

            if (file == null) return;

            byte[] bytes = AtnCodec.Encode(targetSet);
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(bytes);
            StatusMessage = $"Saved action set '{targetSet.Name}' to {file.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save actions file: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CreateNewAction()
    {
        var targetSet = SelectedActionSet ?? ActionSets.FirstOrDefault();
        if (targetSet == null)
        {
            targetSet = new AtnCodec.AtnFile { Name = "Custom Actions", Expanded = true };
            ActionSets.Add(targetSet);
            SelectedActionSet = targetSet;
        }

        var newAction = new AtnCodec.AtnAction
        {
            Name = $"Action {targetSet.Actions.Count + 1}",
            Expanded = true
        };
        targetSet.Actions.Add(newAction);
        SelectedAction = newAction;
        StatusMessage = $"Created action '{newAction.Name}'.";
    }

    [RelayCommand]
    public void CreateNewActionSet()
    {
        var newSet = new AtnCodec.AtnFile
        {
            Name = $"Action Set {ActionSets.Count + 1}",
            Expanded = true
        };
        ActionSets.Add(newSet);
        SelectedActionSet = newSet;
        StatusMessage = $"Created action set '{newSet.Name}'.";
    }

    [RelayCommand]
    public void DeleteSelectedAction()
    {
        if (SelectedAction == null) return;

        foreach (var set in ActionSets)
        {
            if (set.Actions.Remove(SelectedAction))
            {
                StatusMessage = $"Deleted action '{SelectedAction.Name}'.";
                SelectedAction = set.Actions.FirstOrDefault();
                return;
            }
        }
    }
}
