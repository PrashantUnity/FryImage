using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Media;
using PdfEditorApp.Plugins.ImageEditor.Core.Codecs;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Models.Shapes;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Actions;

/// <summary>
/// Execution engine for Actions (.atn) and macro batch sequences.
/// Translates recorded steps into ImageEditorViewModel commands and Skia adjustment filters.
/// </summary>
public static class ActionPlaybackEngine
{
    public static List<AtnCodec.AtnFile> CreateDefaultActionSets()
    {
        var defaultSet = new AtnCodec.AtnFile
        {
            Name = "Default Actions",
            Expanded = true
        };

        // 1. Vignette (Selection)
        var vignetteAction = new AtnCodec.AtnAction { Name = "Vignette (Selection)", Expanded = false };
        vignetteAction.Items.Add(new AtnCodec.AtnItem { Dictionary = "Vignette", EventName = "Vignette" });
        defaultSet.Actions.Add(vignetteAction);

        // 2. Wood Frame - 50 px
        var frameAction = new AtnCodec.AtnAction { Name = "Wood Frame - 50 px", Expanded = false };
        frameAction.Items.Add(new AtnCodec.AtnItem { Dictionary = "Add Frame", EventName = "Frame" });
        defaultSet.Actions.Add(frameAction);

        // 3. Sepia Toning (layer)
        var sepiaAction = new AtnCodec.AtnAction { Name = "Sepia Toning (layer)", Expanded = false };
        sepiaAction.Items.Add(new AtnCodec.AtnItem { Dictionary = "Sepia Tone", EventName = "Sepia" });
        defaultSet.Actions.Add(sepiaAction);

        // 4. High Pass Sharpen
        var sharpenAction = new AtnCodec.AtnAction { Name = "High Pass Sharpen", Expanded = false };
        sharpenAction.Items.Add(new AtnCodec.AtnItem { Dictionary = "High Pass...", EventName = "HighPass" });
        defaultSet.Actions.Add(sharpenAction);

        // 5. Invert & Desaturate
        var invertAction = new AtnCodec.AtnAction { Name = "Invert & Desaturate", Expanded = false };
        invertAction.Items.Add(new AtnCodec.AtnItem { Dictionary = "Invert", EventName = "Invert" });
        invertAction.Items.Add(new AtnCodec.AtnItem { Dictionary = "Desaturate", EventName = "Desaturate" });
        defaultSet.Actions.Add(invertAction);

        // 6. Cast Shadow (Layer)
        var shadowAction = new AtnCodec.AtnAction { Name = "Cast Shadow (Layer)", Expanded = false };
        shadowAction.Items.Add(new AtnCodec.AtnItem { Dictionary = "Duplicate Layer", EventName = "Duplicate" });
        shadowAction.Items.Add(new AtnCodec.AtnItem { Dictionary = "Gaussian Blur...", EventName = "GaussianBlur" });
        defaultSet.Actions.Add(shadowAction);

        // 7. Make Snapshot
        var snapAction = new AtnCodec.AtnAction { Name = "Make Snapshot", Expanded = false };
        snapAction.Items.Add(new AtnCodec.AtnItem { Dictionary = "Snapshot", EventName = "Snapshot" });
        defaultSet.Actions.Add(snapAction);

        return [defaultSet];
    }

    /// <summary>
    /// Checks whether an action step command is a shell save command that batch processing should override.
    /// </summary>
    public static bool IsSaveCommand(string cmd)
    {
        return cmd.Contains("save as") || cmd.Contains("saveas") || cmd.Contains("shell_save") || cmd == "save";
    }

    /// <summary>
    /// Checks whether an action step command is a Quick Export command that should be skipped during batch processing (#4747).
    /// </summary>
    public static bool IsQuickExportCommand(string cmd)
    {
        return cmd.Contains("quick export") || cmd.Contains("quickexport") || cmd.Contains("file.export.quickexport");
    }

    /// <summary>
    /// Filters steps for batch action execution: skips view commands, save commands, and Quick Export steps (#4747).
    /// </summary>
    public static List<AtnCodec.AtnItem> FilterBatchSteps(IEnumerable<AtnCodec.AtnItem> items)
    {
        return items.Where(item =>
        {
            if (!item.Enabled) return false;
            string cmd = (item.Dictionary ?? item.EventName ?? "").Trim().ToLowerInvariant();
            if (IsSaveCommand(cmd)) return false;
            if (IsQuickExportCommand(cmd)) return false;
            if (cmd.Contains("fit on screen") || cmd.Contains("zoom in") || cmd.Contains("zoom out") || cmd.Contains("actual pixels")) return false;
            return true;
        }).ToList();
    }

    public static async Task ExecuteActionAsync(AtnCodec.AtnAction action, ImageEditorViewModel vm, bool isBatch = false)
    {
        vm.StatusMessage = $"Playing action: {action.Name}...";

        var items = isBatch ? FilterBatchSteps(action.Items) : action.Items.Where(it => it.Enabled).ToList();

        foreach (var item in items)
        {
            string cmd = (item.Dictionary ?? item.EventName ?? "").Trim().ToLowerInvariant();

            // When executing in batch mode, skip recorded Quick Export and Save commands (#4747)
            if (isBatch && (IsQuickExportCommand(cmd) || IsSaveCommand(cmd)))
            {
                continue;
            }

            if (IsQuickExportCommand(cmd))
            {
                await vm.QuickExportAsPngAsync();
                continue;
            }

            if (cmd.Contains("gaussian blur") || cmd.Contains("blur"))
            {
                if (vm.SelectedElement is ImageElement img)
                {
                    img.BlurRadius = 10.0;
                    vm.RefreshCanvas();
                }
            }
            else if (cmd.Contains("sepia"))
            {
                vm.SetFilterPresetCommand.Execute("Sepia");
            }
            else if (cmd.Contains("vignette"))
            {
                vm.SetFilterPresetCommand.Execute("Vignette");
            }
            else if (cmd.Contains("invert"))
            {
                vm.ApplyInvertCommand.Execute(null);
            }
            else if (cmd.Contains("black & white") || cmd.Contains("black and white") || cmd.Contains("desaturate"))
            {
                vm.SetFilterPresetCommand.Execute("BlackAndWhite");
            }
            else if (cmd.Contains("sharpen") || cmd.Contains("high pass"))
            {
                if (vm.SelectedElement is ImageElement img)
                {
                    img.SharpenAmount = 30.0;
                    vm.RefreshCanvas();
                }
            }
            else if (cmd.Contains("vintage"))
            {
                vm.SetFilterPresetCommand.Execute("Vintage");
            }
            else if (cmd.Contains("vivid"))
            {
                vm.SetFilterPresetCommand.Execute("Vivid");
            }
            else if (cmd.Contains("noir"))
            {
                vm.SetFilterPresetCommand.Execute("Noir");
            }
            else if (cmd.Contains("duplicate"))
            {
                vm.DuplicateCommand.Execute(null);
            }
            else if (cmd.Contains("snapshot"))
            {
                vm.CreateNewSnapshotCommand.Execute(null);
            }
            else if (cmd.Contains("select all"))
            {
                vm.SelectAllElementsCommand.Execute(null);
            }
            else if (cmd.Contains("refineselectionedge") || cmd.Contains("select and mask"))
            {
                vm.OpenSelectAndMaskDialogCommand.Execute(null);
            }
            else if (cmd.Contains("gefc") || cmd.Contains("spatter"))
            {
                vm.OpenFilterDialogCommand.Execute("Spatter");
            }
            else if (cmd.Contains("rppl") || cmd.Contains("ripple"))
            {
                vm.OpenFilterDialogCommand.Execute("Ripple");
            }
            else if (cmd.Contains("crst") || cmd.Contains("crystallize"))
            {
                vm.OpenFilterDialogCommand.Execute("Crystallize");
            }
            else if (cmd.Contains("layerfrombackground") || cmd.Contains("layer from background"))
            {
                vm.ConvertBackgroundToLayerCommand.Execute(null);
            }
            else if (cmd.Contains("stepbackward") || cmd.Contains("step backward"))
            {
                vm.UndoCommand.Execute(null);
            }
            else if (cmd.Contains("stepforward") || cmd.Contains("step forward"))
            {
                vm.RedoCommand.Execute(null);
            }
            else if (cmd.Contains("removefromselection"))
            {
                vm.SelectionCombineMode = "Subtract";
            }
            else if (cmd.Contains("loadselection"))
            {
                vm.LoadChannelAsSelectionCommand.Execute(null);
            }
            else if (cmd.Contains("fillforeground") || cmd.Contains("fill foreground"))
            {
                vm.FillWithForegroundColorCommand.Execute(null);
            }
            else if (cmd.Contains("fillbackground") || cmd.Contains("fill background"))
            {
                vm.FillWithBackgroundColorCommand.Execute(null);
            }
            else if (cmd.Contains("translate") || cmd.Contains("nudge"))
            {
                if (vm.SelectedElement != null)
                {
                    vm.SelectedElement.X += 5;
                    vm.SelectedElement.Y += 5;
                    vm.RefreshCanvas();
                }
            }
            else if (cmd.Contains("imagesize") || cmd.Contains("image size"))
            {
                vm.RefreshCanvas();
            }
            else if (cmd.Contains("frame"))
            {
                // Add decorative rectangular frame element around canvas bounds
                var frame = new RectangleElement
                {
                    Name = "Frame Border",
                    X = 20,
                    Y = 20,
                    Width = Math.Max(50, vm.CanvasWidth - 40),
                    Height = Math.Max(50, vm.CanvasHeight - 40),
                    FillColor = Colors.Transparent,
                    StrokeColor = Color.FromRgb(139, 69, 19), // SaddleBrown frame
                    StrokeThickness = 14,
                    CornerRadius = 4,
                    ZIndex = vm.Elements.Count + 1
                };
                vm.Elements.Add(frame);
                vm.SelectedElement = frame;
            }

        }

        vm.StatusMessage = $"Action '{action.Name}' finished.";
        await Task.CompletedTask;
    }
}
