using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.Descriptors;
using FryWorld.Sdk.Models;
using FryWorld.Sdk.Plugins;
using FryWorld.Sdk.Services;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.Views;

namespace PdfEditorApp.Plugins.ImageEditor;

/// <summary>
/// FryWorld plugin registration for the Image Editor (Canva-style canvas studio and overlay).
/// Contributes full-viewport navigation rail page, shell overlay, Command Palette, Status Bar, and Ribbon.
/// </summary>
public class ImageEditorPlugin : IFryPlugin
{
    public string Id => "com.fryworld.imageeditor";
    public string Name => "Image Editor";
    public Version Version => new(3, 2, 1);
    public bool AutoOpenOverlay => false;
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public IReadOnlyDictionary<string, PluginSettingDefinition>? SettingsSchema =>
        new Dictionary<string, PluginSettingDefinition>
        {
            ["DefaultBackground"] = new()
            {
                Label = "Default Canvas Background",
                Description = "Starting canvas background color when the editor opens.",
                Type = PluginSettingType.Select,
                Options = new() { "White", "Black", "Transparent" },
                DefaultValue = "White"
            },
            ["DefaultCanvasWidth"] = new()
            {
                Label = "Canvas Width (px)",
                Description = "Default canvas width in pixels.",
                Type = PluginSettingType.Integer,
                DefaultValue = 640
            },
            ["DefaultCanvasHeight"] = new()
            {
                Label = "Canvas Height (px)",
                Description = "Default canvas height in pixels.",
                Type = PluginSettingType.Integer,
                DefaultValue = 480
            }
        };

    public Task ApplyAsync(IFryPluginContext ctx, CancellationToken ct = default)
    {
        // Opens the studio page in whichever shell hosts the plugin: FryWorld, FryPDF or the standalone runner.
        void NavigateToStudio()
        {
            if (ctx.TryGetService<IShellService>(out var shell)) shell.NavigateTo("com.fryworld.imageeditor");
        }

        if (ctx.TryGetService<FryWorld.Sdk.Theming.IThemeManager>(out var themeManager))
        {
            var tokenReg = themeManager.RegisterTokenContributor(new FryWorld.Sdk.Theming.Contributors.PhotoStudioTokenContributor());
            ctx.RegisterDisposable(tokenReg);
        }

        // 0. Register Full-Viewport Workspace Studio Page in Left Navigation Rail
        ctx.RegisterNavigationItem(new NavigationItemDescriptor
        {
            Id = "com.fryworld.imageeditor",
            Title = "Image Editor",
            Category = "Creative",
            IconKind = "ImageEditOutline",
            AccentColor = "#DC2626",
            Order = 35,
            ViewFactory = () => new ImageEditorView
            {
                DataContext = new ImageEditorViewModel(ctx)
            }
        });

        // 1. Register Shell Overlay
        ctx.RegisterOverlay(new OverlayDescriptor
        {
            Id = Id,
            Title = "Image Editor",
            IconKind = "ImageEditOutline",
            DefaultWidth = 900,
            DefaultHeight = 640,
            MinWidth = 680,
            MinHeight = 520,
            IsResizable = true,
            AutoOpenOnMount = false,
            ViewFactory = () => new ImageEditorView
            {
                DataContext = new ImageEditorViewModel(ctx)
            }
        });

        // 2. Command Palette
        ctx.RegisterCommand(new CommandPaletteDescriptor
        {
            Id = "cmd.overlay.imageeditor",
            Title = "Open Image Editor",
            Subtitle = "Launch Canva-style image editor with shapes, text, filters, and PNG export",
            Category = "Creative Tools",
            IconKind = "ImageEditOutline",
            ShortcutKey = OperatingSystem.IsMacOS() ? "Cmd+Alt+I" : "Ctrl+Alt+I",
            Command = new RelayCommand(NavigateToStudio)
        });

        // 3. Status Bar Widget
        ctx.RegisterStatusBarWidget(new StatusBarWidgetDescriptor
        {
            Id = "fryworld.status.imageeditor",
            Label = "🎨 Image Editor",
            Tooltip = "Open Image Editor",
            AlignRight = true,
            Order = 12,
            ClickCommand = new RelayCommand(NavigateToStudio)
        });

        // 4. Dynamic Ribbon Tab
        var ribbonTab = new RibbonTabDescriptor
        {
            Id = "image",
            Header = "Image",
            Order = 45,
            Actions = new()
            {
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.action.imageeditor.open",
                    GroupId = "studio",
                    Label = "Image Editor",
                    Tooltip = "Open Canva-Style Image Editor",
                    IconKind = "ImageEditOutline",
                    Order = 10,
                    Command = new RelayCommand(NavigateToStudio)
                },
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.action.imageeditor.new",
                    GroupId = "studio",
                    Label = "New Canvas",
                    Tooltip = "Create a fresh design canvas in the Image Editor",
                    IconKind = "Artboard",
                    Order = 20,
                    Command = new RelayCommand(NavigateToStudio)
                },
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.action.imageeditor.text",
                    GroupId = "draw",
                    Label = "Add Text",
                    Tooltip = "Add styled text and typography in Image Editor",
                    IconKind = "FormatText",
                    Order = 10,
                    Command = new RelayCommand(NavigateToStudio)
                },
                new RibbonActionDescriptor
                {
                    Id = "fryworld.ribbon.action.imageeditor.export",
                    GroupId = "export",
                    Label = "Export PNG",
                    Tooltip = "Export canvas graphics as high-res PNG image",
                    IconKind = "Download",
                    Order = 10,
                    Command = new RelayCommand(NavigateToStudio)
                }
            }
        };
        ctx.RegisterRibbonTab(ribbonTab);

        // Everything registered through ctx is removed automatically when the plugin unmounts.

        return Task.CompletedTask;
    }
}
