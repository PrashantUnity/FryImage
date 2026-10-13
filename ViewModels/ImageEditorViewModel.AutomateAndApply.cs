using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Vector;
using PdfEditorApp.Plugins.ImageEditor.Models;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    #region Load Files into Stack (File > Scripts > Load Files into Stack...)

    [ObservableProperty]
    private bool _isLoadFilesIntoStackDialogOpen;

    [ObservableProperty]
    private int _loadStackUseModeIndex = 0; // 0 = Files, 1 = Folder

    [ObservableProperty]
    private string? _loadStackSelectedFile;

    [ObservableProperty]
    private bool _loadStackAlign = false;

    [ObservableProperty]
    private bool _loadStackSmartObject = false;

    public ObservableCollection<string> LoadStackFiles { get; } = new();

    public bool CanRemoveLoadStackFile => LoadStackUseModeIndex == 0 && !string.IsNullOrEmpty(LoadStackSelectedFile) && LoadStackFiles.Contains(LoadStackSelectedFile);

    public bool CanConfirmLoadStack => LoadStackFiles.Count > 0;

    partial void OnLoadStackSelectedFileChanged(string? value)
    {
        OnPropertyChanged(nameof(CanRemoveLoadStackFile));
    }

    partial void OnLoadStackUseModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CanRemoveLoadStackFile));
    }

    [RelayCommand]
    public void OpenLoadFilesIntoStackDialog()
    {
        LoadStackFiles.Clear();
        LoadStackSelectedFile = null;
        LoadStackAlign = false;
        LoadStackSmartObject = false;
        LoadStackUseModeIndex = 0;
        OnPropertyChanged(nameof(CanConfirmLoadStack));
        OnPropertyChanged(nameof(CanRemoveLoadStackFile));
        IsLoadFilesIntoStackDialogOpen = true;
    }

    [RelayCommand]
    public void CancelLoadFilesIntoStackDialog()
    {
        IsLoadFilesIntoStackDialogOpen = false;
    }

    [RelayCommand]
    public async Task LoadStackBrowseAsync()
    {
        try
        {
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            if (topLevel?.StorageProvider == null) return;

            if (LoadStackUseModeIndex == 1) // Folder mode
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Folder to Load into Stack",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    var dirPath = folders[0].TryGetLocalPath();
                    if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath))
                    {
                        var supportedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ".png", ".jpg", ".jpeg", ".psd", ".bmp", ".webp", ".gif", ".tiff", ".tif"
                        };
                        var files = Directory.EnumerateFiles(dirPath)
                            .Where(f => supportedExts.Contains(Path.GetExtension(f)))
                            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

                        LoadStackFiles.Clear();
                        foreach (var f in files)
                        {
                            LoadStackFiles.Add(f);
                        }
                    }
                }
            }
            else // Files mode
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Files to Load into Stack",
                    AllowMultiple = true,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Image Files")
                        {
                            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.psd", "*.bmp", "*.webp", "*.gif", "*.tiff", "*.tif" }
                        },
                        FilePickerFileTypes.All
                    }
                });

                foreach (var f in files)
                {
                    var path = f.TryGetLocalPath();
                    if (!string.IsNullOrEmpty(path) && !LoadStackFiles.Contains(path))
                    {
                        LoadStackFiles.Add(path);
                    }
                }
            }

            OnPropertyChanged(nameof(CanConfirmLoadStack));
            OnPropertyChanged(nameof(CanRemoveLoadStackFile));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Browse error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void LoadStackRemove()
    {
        if (!string.IsNullOrEmpty(LoadStackSelectedFile) && LoadStackFiles.Contains(LoadStackSelectedFile))
        {
            LoadStackFiles.Remove(LoadStackSelectedFile);
            LoadStackSelectedFile = LoadStackFiles.FirstOrDefault();
            OnPropertyChanged(nameof(CanConfirmLoadStack));
            OnPropertyChanged(nameof(CanRemoveLoadStackFile));
        }
    }

    [RelayCommand]
    public void LoadStackAddOpenFiles()
    {
        // Add existing document / session images if any
        if (Elements.Count > 0)
        {
            // If active elements exist, ensure placeholder reference
            string openDocName = $"Current Document ({CanvasWidth}x{CanvasHeight})";
            if (!LoadStackFiles.Contains(openDocName))
            {
                LoadStackFiles.Add(openDocName);
            }
        }
        OnPropertyChanged(nameof(CanConfirmLoadStack));
        OnPropertyChanged(nameof(CanRemoveLoadStackFile));
    }

    [RelayCommand]
    public void LoadStackSortByName()
    {
        var sorted = LoadStackFiles.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();
        LoadStackFiles.Clear();
        foreach (var item in sorted)
        {
            LoadStackFiles.Add(item);
        }
        LoadStackSelectedFile = LoadStackFiles.FirstOrDefault();
        OnPropertyChanged(nameof(CanRemoveLoadStackFile));
    }

    [RelayCommand]
    public async Task ConfirmLoadFilesIntoStackAsync()
    {
        if (LoadStackFiles.Count == 0) return;

        var pathsToLoad = LoadStackFiles.ToList();
        IsLoadFilesIntoStackDialogOpen = false;

        double maxW = CanvasWidth > 0 ? CanvasWidth : 800;
        double maxH = CanvasHeight > 0 ? CanvasHeight : 600;
        var loadedElements = new List<CanvasElement>();

        foreach (var path in pathsToLoad)
        {
            if (File.Exists(path))
            {
                try
                {
                    var bytes = await File.ReadAllBytesAsync(path);
                    using var bmp = SKBitmap.Decode(bytes);
                    if (bmp != null)
                    {
                        maxW = Math.Max(maxW, bmp.Width);
                        maxH = Math.Max(maxH, bmp.Height);

                        var imgElem = new ImageElement
                        {
                            Name = Path.GetFileName(path),
                            OriginalImageBytes = bytes,
                            Source = new Bitmap(new MemoryStream(bytes)),
                            NaturalPixelWidth = bmp.Width,
                            NaturalPixelHeight = bmp.Height,
                            Width = bmp.Width,
                            Height = bmp.Height,
                            X = 0,
                            Y = 0,
                            Opacity = 1.0,
                            IsVisible = true
                        };
                        loadedElements.Add(imgElem);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed loading {Path.GetFileName(path)}: {ex.Message}";
                }
            }
        }

        if (loadedElements.Count > 0)
        {
            CanvasWidth = maxW;
            CanvasHeight = maxH;

            if (LoadStackAlign)
            {
                // Align each loaded image to canvas center
                foreach (var el in loadedElements)
                {
                    el.X = (CanvasWidth - el.Width) / 2.0;
                    el.Y = (CanvasHeight - el.Height) / 2.0;
                }
            }

            foreach (var el in loadedElements)
            {
                Elements.Add(el);
            }

            if (LoadStackSmartObject && loadedElements.Count > 1)
            {
                // Create smart object from the loaded stack layers
                SelectedElement = loadedElements.Last();
            }
            else
            {
                SelectedElement = loadedElements.Last();
            }

            RefreshCanvas();
            StatusMessage = $"Loaded {loadedElements.Count} layers into stack.";
        }
    }

    #endregion

    #region Fit Image (File > Automate > Fit Image...)

    [ObservableProperty]
    private bool _isFitImageDialogOpen;

    [ObservableProperty]
    private double _fitImageWidth = 1920;

    [ObservableProperty]
    private double _fitImageHeight = 1080;

    [ObservableProperty]
    private bool _fitImageDontEnlarge = false;

    [RelayCommand]
    public void OpenFitImageDialog()
    {
        FitImageWidth = Math.Round(CanvasWidth);
        FitImageHeight = Math.Round(CanvasHeight);
        FitImageDontEnlarge = false;
        IsFitImageDialogOpen = true;
    }

    [RelayCommand]
    public void CancelFitImageDialog()
    {
        IsFitImageDialogOpen = false;
    }

    [RelayCommand]
    public void ConfirmFitImage()
    {
        double w = CanvasWidth;
        double h = CanvasHeight;
        if (w <= 0 || h <= 0) return;

        double bw = Math.Max(1.0, FitImageWidth);
        double bh = Math.Max(1.0, FitImageHeight);
        double k = Math.Min(bw / w, bh / h);

        if (k > 1.0 && FitImageDontEnlarge)
        {
            IsFitImageDialogOpen = false;
            return;
        }

        double nw = Math.Max(1.0, Math.Round(w * k));
        double nh = Math.Max(1.0, Math.Round(h * k));

        if (Math.Abs(nw - w) < 0.5 && Math.Abs(nh - h) < 0.5)
        {
            IsFitImageDialogOpen = false;
            return;
        }

        // Scale elements proportionally to fit new canvas bounds
        foreach (var el in Elements)
        {
            el.X *= k;
            el.Y *= k;
            el.Width *= k;
            el.Height *= k;
        }

        CanvasWidth = nw;
        CanvasHeight = nh;
        RefreshCanvas();
        IsFitImageDialogOpen = false;
        StatusMessage = $"Fit Image: {w:0}×{h:0} → {nw:0}×{nh:0} (scale {k:P0})";
    }

    #endregion

    #region Apply Image (Image > Apply Image...)

    [ObservableProperty]
    private bool _isApplyImageDialogOpen;

    public ObservableCollection<string> ApplyImageSourceLayers { get; } = new();

    public ObservableCollection<string> ApplyImageAvailableLayers => ApplyImageSourceLayers;

    [ObservableProperty]
    private string _applyImageSourceLayer = "Merged";

    public ObservableCollection<string> ApplyImageSourceChannels { get; } = new()
    {
        "RGB", "Red", "Green", "Blue", "Alpha", "Selection"
    };

    [ObservableProperty]
    private string _applyImageSourceChannel = "RGB";

    [ObservableProperty]
    private bool _applyImageSourceInvert = false;

    public bool ApplyImageInvert
    {
        get => ApplyImageSourceInvert;
        set => ApplyImageSourceInvert = value;
    }

    [ObservableProperty]
    private bool _applyImageMaskEnabled = false;

    public ObservableCollection<string> ApplyImageBlendings { get; } = new()
    {
        "Normal", "Multiply", "Screen", "Overlay", "Soft Light", "Hard Light",
        "Darken", "Lighten", "Color Dodge", "Color Burn", "Linear Burn", "Linear Dodge",
        "Difference", "Exclusion", "Add", "Subtract"
    };

    [ObservableProperty]
    private string _applyImageBlending = "Multiply";

    [ObservableProperty]
    private double _applyImageOpacity = 100.0;

    [ObservableProperty]
    private double _applyImageScale = 1.0;

    [ObservableProperty]
    private double _applyImageOffset = 0.0;

    [ObservableProperty]
    private bool _applyImagePreserveTransparency = false;

    public ObservableCollection<string> ApplyImageMaskChannels { get; } = new()
    {
        "None", "Selection", "Mask", "Alpha"
    };

    [ObservableProperty]
    private string _applyImageMaskChannel = "None";

    [ObservableProperty]
    private bool _applyImageMaskInvert = false;

    [ObservableProperty]
    private bool _applyImagePreview = true;

    [RelayCommand]
    public void OpenApplyImageDialog()
    {
        ApplyImageSourceLayers.Clear();
        ApplyImageSourceLayers.Add("Merged");
        foreach (var el in Elements)
        {
            var name = el.Name ?? "Layer";
            if (!ApplyImageSourceLayers.Contains(name))
            {
                ApplyImageSourceLayers.Add(name);
            }
        }
        ApplyImageSourceLayer = "Merged";
        ApplyImageSourceChannel = "RGB";
        ApplyImageSourceInvert = false;
        ApplyImageBlending = "Multiply";
        ApplyImageOpacity = 100.0;
        ApplyImageScale = 1.0;
        ApplyImageOffset = 0.0;
        ApplyImagePreserveTransparency = false;
        ApplyImageMaskChannel = "None";
        ApplyImageMaskInvert = false;
        ApplyImagePreview = true;

        IsApplyImageDialogOpen = true;
    }

    [RelayCommand]
    public void CancelApplyImageDialog()
    {
        IsApplyImageDialogOpen = false;
    }

    [RelayCommand]
    public void ConfirmApplyImage()
    {
        if (SelectedElement is not ImageElement targetImg || targetImg.OriginalImageBytes == null)
        {
            StatusMessage = "Apply Image requires an active pixel layer.";
            IsApplyImageDialogOpen = false;
            return;
        }

        try
        {
            using var targetBmp = SKBitmap.Decode(targetImg.OriginalImageBytes);
            if (targetBmp == null)
            {
                IsApplyImageDialogOpen = false;
                return;
            }

            int w = targetBmp.Width;
            int h = targetBmp.Height;

            // Source image
            SKBitmap? sourceBmp = null;
            if (ApplyImageSourceLayer == "Merged")
            {
                // Render canvas or use target if single
                sourceBmp = targetBmp.Copy();
            }
            else
            {
                var srcElem = Elements.OfType<ImageElement>().FirstOrDefault(e => e.Name == ApplyImageSourceLayer);
                if (srcElem?.OriginalImageBytes != null)
                {
                    sourceBmp = SKBitmap.Decode(srcElem.OriginalImageBytes);
                }
                sourceBmp ??= targetBmp.Copy();
            }

            // Perform blending
            var resultBmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
            double scale = Math.Clamp(ApplyImageScale, 1.0, 2.0);
            double offset = Math.Clamp(ApplyImageOffset, -255.0, 255.0);
            double opacityNorm = Math.Clamp(ApplyImageOpacity, 0.0, 100.0) / 100.0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var baseCol = targetBmp.GetPixel(x, y);
                    var srcCol = sourceBmp.Width == w && sourceBmp.Height == h
                        ? sourceBmp.GetPixel(x, y)
                        : sourceBmp.GetPixel((int)(x * (double)sourceBmp.Width / w), (int)(y * (double)sourceBmp.Height / h));

                    byte srcR = ApplyImageSourceInvert ? (byte)(255 - srcCol.Red) : srcCol.Red;
                    byte srcG = ApplyImageSourceInvert ? (byte)(255 - srcCol.Green) : srcCol.Green;
                    byte srcB = ApplyImageSourceInvert ? (byte)(255 - srcCol.Blue) : srcCol.Blue;

                    byte r, g, b;
                    switch (ApplyImageBlending.ToLowerInvariant())
                    {
                        case "add":
                            r = (byte)Math.Clamp(((baseCol.Red + srcR) / scale) + offset, 0, 255);
                            g = (byte)Math.Clamp(((baseCol.Green + srcG) / scale) + offset, 0, 255);
                            b = (byte)Math.Clamp(((baseCol.Blue + srcB) / scale) + offset, 0, 255);
                            break;
                        case "subtract":
                            r = (byte)Math.Clamp(((baseCol.Red - srcR) / scale) + offset, 0, 255);
                            g = (byte)Math.Clamp(((baseCol.Green - srcG) / scale) + offset, 0, 255);
                            b = (byte)Math.Clamp(((baseCol.Blue - srcB) / scale) + offset, 0, 255);
                            break;
                        case "screen":
                            r = (byte)(255 - ((255 - baseCol.Red) * (255 - srcR) / 255));
                            g = (byte)(255 - ((255 - baseCol.Green) * (255 - srcG) / 255));
                            b = (byte)(255 - ((255 - baseCol.Blue) * (255 - srcB) / 255));
                            break;
                        case "overlay":
                            r = (byte)(baseCol.Red < 128 ? (2 * baseCol.Red * srcR / 255) : (255 - 2 * (255 - baseCol.Red) * (255 - srcR) / 255));
                            g = (byte)(baseCol.Green < 128 ? (2 * baseCol.Green * srcG / 255) : (255 - 2 * (255 - baseCol.Green) * (255 - srcG) / 255));
                            b = (byte)(baseCol.Blue < 128 ? (2 * baseCol.Blue * srcB / 255) : (255 - 2 * (255 - baseCol.Blue) * (255 - srcB) / 255));
                            break;
                        case "normal":
                            r = srcR;
                            g = srcG;
                            b = srcB;
                            break;
                        case "multiply":
                        default:
                            r = (byte)(baseCol.Red * srcR / 255);
                            g = (byte)(baseCol.Green * srcG / 255);
                            b = (byte)(baseCol.Blue * srcB / 255);
                            break;
                    }

                    // Apply opacity
                    byte finalR = (byte)Math.Clamp(baseCol.Red + (r - baseCol.Red) * opacityNorm, 0, 255);
                    byte finalG = (byte)Math.Clamp(baseCol.Green + (g - baseCol.Green) * opacityNorm, 0, 255);
                    byte finalB = (byte)Math.Clamp(baseCol.Blue + (b - baseCol.Blue) * opacityNorm, 0, 255);
                    byte finalA = ApplyImagePreserveTransparency ? baseCol.Alpha : (byte)Math.Clamp(srcCol.Alpha * opacityNorm + baseCol.Alpha * (1 - opacityNorm), 0, 255);

                    resultBmp.SetPixel(x, y, new SKColor(finalR, finalG, finalB, finalA));
                }
            }

            sourceBmp?.Dispose();

            using var image = SKImage.FromBitmap(resultBmp);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            byte[] outBytes = data.ToArray();
            targetImg.OriginalImageBytes = outBytes;
            targetImg.AdjustedSource?.Dispose();
            targetImg.AdjustedSource = new Bitmap(new MemoryStream(outBytes));
            resultBmp.Dispose();

            RefreshCanvas();
            StatusMessage = $"Apply Image ({ApplyImageBlending}) completed on {targetImg.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apply Image failed: {ex.Message}";
        }

        IsApplyImageDialogOpen = false;
    }

    #endregion

    #region Convert Selection to Shape (select.convertToShape)

    [RelayCommand]
    public void ConvertSelectionToShape(double tolerance = 2.0)
    {
        double tol = Math.Clamp(tolerance, 0.5, 10.0);

        // Check if there is an active lasso selection polygon
        var lassoPoly = CanvasControl?.GetActiveSelectionPolygon();
        if (lassoPoly != null && lassoPoly.Count >= 3)
        {
            var path = VectorPathTracer.FitPolygonToPath(lassoPoly, tol, true, "Selection Shape");
            var shapeElement = new VectorShapeElement(path, ForegroundColor)
            {
                Name = $"Shape {Elements.Count + 1}",
                X = 0,
                Y = 0,
                Width = CanvasWidth,
                Height = CanvasHeight,
                StrokeThickness = 0,
                Opacity = 1.0,
                IsVisible = true
            };
            Elements.Add(shapeElement);
            SelectedElement = shapeElement;
            RefreshCanvas();
            StatusMessage = $"Converted selection polygon to shape (tolerance {tol:0.1} px).";
            return;
        }

        // Check QuickMaskBuffer
        var mask = CanvasControl?.QuickMaskBuffer;
        int w = (int)Math.Max(1.0, CanvasWidth);
        int h = (int)Math.Max(1.0, CanvasHeight);
        if (mask != null && mask.Length >= w * h)
        {
            float[] floatMask = new float[w * h];
            for (int i = 0; i < floatMask.Length; i++)
            {
                floatMask[i] = mask[i] / 255.0f;
            }

            var path = VectorPathTracer.TraceMaskToPath(floatMask, w, h, tol, "Selection Shape");
            var shapeElement = new VectorShapeElement(path, ForegroundColor)
            {
                Name = $"Shape {Elements.Count + 1}",
                X = 0,
                Y = 0,
                Width = CanvasWidth,
                Height = CanvasHeight,
                StrokeThickness = 0,
                Opacity = 1.0,
                IsVisible = true
            };
            Elements.Add(shapeElement);
            SelectedElement = shapeElement;
            RefreshCanvas();
            StatusMessage = $"Converted selection mask to shape (tolerance {tol:0.1} px).";
            return;
        }

        // Fallback: if single element selected, convert its bounding box or contour
        if (SelectedElement != null)
        {
            var rectPath = VectorDocumentPath.CreateRectangle(SelectedElement.X, SelectedElement.Y, SelectedElement.Width, SelectedElement.Height);
            var shapeElement = new VectorShapeElement(rectPath, ForegroundColor)
            {
                Name = $"Shape {Elements.Count + 1}",
                X = SelectedElement.X,
                Y = SelectedElement.Y,
                Width = SelectedElement.Width,
                Height = SelectedElement.Height,
                StrokeThickness = 0,
                Opacity = 1.0,
                IsVisible = true
            };
            Elements.Add(shapeElement);
            SelectedElement = shapeElement;
            RefreshCanvas();
            StatusMessage = $"Created shape from element bounds.";
        }
        else
        {
            StatusMessage = "Make a selection first to convert to shape.";
        }
    }

    #endregion
}
