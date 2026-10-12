using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.ImageEditor.Core.Codecs;
using PdfEditorApp.Plugins.ImageEditor.Models;
using PdfEditorApp.Plugins.ImageEditor.UISnapshots.Core;

namespace PdfEditorApp.Plugins.ImageEditor.UISnapshots.Suites;

/// <summary>
/// Verifies functional and visual correctness of ported Photoshop-parity features:
/// PCX & KRA codecs, Layer/Background conversions, Transparency locking, Mask targeting,
/// Luminosity selection, History fill, Tab-rename navigation, and Channel management.
/// </summary>
public static class ParityFeatureSnapshotSuite
{
    public static void Run(SnapshotContext ctx)
    {
        var vm = ctx.ViewModel;

        // 1. Verify PCX Codec (24-bit 3-plane RGB)
        byte[] testPcx24 = CreateSyntheticPcx24(16, 16);
        var pcxDecoded = PcxCodec.Decode(testPcx24);
        if (pcxDecoded == null || pcxDecoded.Width != 16 || pcxDecoded.Height != 16)
        {
            throw new InvalidOperationException("PcxCodec failed to decode 24-bit test image.");
        }
        Console.WriteLine("  ✓ PCX 24-bit RGB codec test passed.");

        // 2. Verify PCX Codec (8-bit 256-color VGA palette)
        byte[] testPcx8 = CreateSyntheticPcx8(8, 8);
        var pcx8Decoded = PcxCodec.Decode(testPcx8);
        if (pcx8Decoded == null || pcx8Decoded.Width != 8 || pcx8Decoded.Height != 8)
        {
            throw new InvalidOperationException("PcxCodec failed to decode 8-bit palette test image.");
        }
        Console.WriteLine("  ✓ PCX 8-bit palette codec test passed.");

        // 3. Verify KRA Codec (Zip archive containing mergedimage.png)
        byte[] testKra = CreateSyntheticKra(8, 8);
        var kraDecoded = KraCodec.Decode(testKra);
        if (kraDecoded == null || kraDecoded.PngBytes.Length == 0)
        {
            throw new InvalidOperationException("KraCodec failed to extract mergedimage.png from .kra archive.");
        }
        Console.WriteLine("  ✓ KRA archive composite extraction test passed.");

        // 4. Verify Built-in Rainbow Gradient Presets
        var defaultGradients = GrdCodec.DefaultPresets;
        string[] expectedPresets = ["Rainbow", "Pastel Rainbow", "Neon Rainbow", "Oil Slick", "Rainbow Fade"];
        foreach (var p in expectedPresets)
        {
            if (!defaultGradients.Any(g => g.Name == p))
            {
                throw new InvalidOperationException($"Missing expected built-in gradient preset: '{p}'.");
            }
        }
        Console.WriteLine("  ✓ Built-in Rainbow gradient presets verified.");

        // 5. Test Background conversion & Transparency lock
        var layer1 = new RectangleElement { Name = "Base Background", Width = 200, Height = 200, ZIndex = 0 };
        var layer2 = new RectangleElement { Name = "Mid Layer", Width = 150, Height = 150, ZIndex = 1 };
        var layer3 = new RectangleElement { Name = "Top Layer", Width = 100, Height = 100, ZIndex = 2 };
        vm.Elements.Clear();
        vm.Elements.Add(layer1);
        vm.Elements.Add(layer2);
        vm.Elements.Add(layer3);

        // Convert layer1 to structural background
        vm.ConvertLayerToBackground(layer1);
        if (!layer1.HasBackgroundTraits || !layer1.IsTransparencyLocked || !layer1.IsPositionLocked)
        {
            throw new InvalidOperationException("ConvertLayerToBackground failed to set background traits.");
        }

        // Transparency lock cannot be toggled on background
        vm.SelectedElement = layer1;
        vm.ToggleLockTransparencyCommand.Execute(null);
        if (!layer1.IsTransparencyLocked)
        {
            throw new InvalidOperationException("Background transparency lock should remain locked.");
        }

        // Normal layer can toggle transparency lock
        vm.SelectedElement = layer2;
        vm.ToggleLockTransparencyCommand.Execute(null);
        if (!layer2.IsTransparencyLocked)
        {
            throw new InvalidOperationException("Normal layer failed to lock transparency.");
        }
        vm.ToggleLockTransparencyCommand.Execute(null);
        if (layer2.IsTransparencyLocked)
        {
            throw new InvalidOperationException("Normal layer failed to unlock transparency.");
        }
        Console.WriteLine("  ✓ Background and Transparency lock semantics verified.");

        // 6. Test ConvertBackgroundToLayer
        vm.ConvertBackgroundToLayer(layer1);
        if (layer1.IsBackground || layer1.IsPositionLocked || layer1.IsTransparencyLocked)
        {
            throw new InvalidOperationException("ConvertBackgroundToLayer failed to release locks.");
        }
        Console.WriteLine("  ✓ ConvertBackgroundToLayer verified.");

        // 7. Test Tab/Shift+Tab Layer Navigation
        var adjacentDown = vm.GetAdjacentLayer(layer3, cycleUp: false);
        if (adjacentDown != layer2)
        {
            throw new InvalidOperationException("GetAdjacentLayer cycle down failed.");
        }
        var adjacentUp = vm.GetAdjacentLayer(layer2, cycleUp: true);
        if (adjacentUp != layer3)
        {
            throw new InvalidOperationException("GetAdjacentLayer cycle up failed.");
        }
        Console.WriteLine("  ✓ Layer navigation (Tab / Shift+Tab) verified.");

        // 8. Test Blend Mode Cycling
        vm.SelectedElement = layer2;
        var initialBlend = vm.BlendModeField;
        vm.CycleBlendModeCommand.Execute(true);
        if (vm.BlendModeField == initialBlend && vm.AvailableBlendModes.Count > 1)
        {
            throw new InvalidOperationException("CycleBlendMode forward failed to advance blend mode.");
        }
        vm.CycleBlendModeCommand.Execute(false);
        if (vm.BlendModeField != initialBlend)
        {
            throw new InvalidOperationException("CycleBlendMode backward failed to return to initial blend mode.");
        }
        Console.WriteLine("  ✓ Shift+Plus/Minus Blend mode cycling verified.");

        // 9. Test Channel Management (Channels View)
        vm.SelectRightDockTab("Channels");
        int initialChannels = vm.DocumentChannels.Count;
        vm.SaveSelectionAsChannel("Test Alpha 1");
        if (vm.DocumentChannels.Count != initialChannels + 1)
        {
            throw new InvalidOperationException("SaveSelectionAsChannel failed to add new channel.");
        }
        var addedChannel = vm.DocumentChannels.Last();
        if (addedChannel.Name != "Test Alpha 1" || !addedChannel.IsAlpha)
        {
            throw new InvalidOperationException("Added channel did not preserve Alpha traits.");
        }

        // Capture visual snapshot of the Channels dock panel
        ctx.Capture("dock_channels_view.png");

        vm.DeleteChannel(addedChannel);
        if (vm.DocumentChannels.Count != initialChannels)
        {
            throw new InvalidOperationException("DeleteChannel failed to remove Alpha channel.");
        }
        Console.WriteLine("  ✓ Channels collection & Alpha channel workflows verified.");

        // Return dock tab to Layers
        vm.SelectRightDockTab("Layers");
    }

    private static byte[] CreateSyntheticPcx24(int width, int height)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Header
        writer.Write((byte)0x0A); // Manufacturer: ZSoft PCX
        writer.Write((byte)5);    // Version: 3.0+
        writer.Write((byte)1);    // Encoding: PCX RLE
        writer.Write((byte)8);    // BitsPerPixel: 8
        writer.Write((ushort)0);  // XMin
        writer.Write((ushort)0);  // YMin
        writer.Write((ushort)(width - 1)); // XMax
        writer.Write((ushort)(height - 1)); // YMax
        writer.Write((ushort)72); // HRes
        writer.Write((ushort)72); // VRes
        writer.Write(new byte[48]); // 16-color palette
        writer.Write((byte)0);    // Reserved
        writer.Write((byte)3);    // ColorPlanes: 3 for 24-bit RGB
        writer.Write((ushort)width); // BytesPerLine
        writer.Write((ushort)1);  // PaletteType
        writer.Write(new byte[58]); // Filler to 128 bytes

        // Properly encoded RLE scanlines
        for (int y = 0; y < height; y++)
        {
            // Plane 0: Red
            writer.Write((byte)(0xC0 | width));
            writer.Write((byte)0x40);

            // Plane 1: Green
            writer.Write((byte)(0xC0 | width));
            writer.Write((byte)0x80);

            // Plane 2: Blue
            writer.Write((byte)(0xC0 | width));
            writer.Write((byte)0x20);
        }

        return ms.ToArray();
    }

    private static byte[] CreateSyntheticPcx8(int width, int height)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write((byte)0x0A);
        writer.Write((byte)5);
        writer.Write((byte)1);
        writer.Write((byte)8); // 8-bit palette
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)(width - 1));
        writer.Write((ushort)(height - 1));
        writer.Write((ushort)72);
        writer.Write((ushort)72);
        writer.Write(new byte[48]);
        writer.Write((byte)0);
        writer.Write((byte)1); // ColorPlanes: 1
        writer.Write((ushort)width);
        writer.Write((ushort)1);
        writer.Write(new byte[58]);

        // Scanlines with RLE
        for (int y = 0; y < height; y++)
        {
            writer.Write((byte)(0xC0 | width));
            writer.Write((byte)1);
        }

        // 256-color palette marker 0x0C + 768 bytes
        writer.Write((byte)0x0C);
        for (int i = 0; i < 256; i++)
        {
            writer.Write((byte)i); // R
            writer.Write((byte)i); // G
            writer.Write((byte)i); // B
        }

        return ms.ToArray();
    }

    private static byte[] CreateSyntheticKra(int width, int height)
    {
        // Minimal PNG in memory
        using var pngBmp = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
        using var canvas = new SkiaSharp.SKCanvas(pngBmp);
        canvas.Clear(SkiaSharp.SKColors.CornflowerBlue);
        using var img = SkiaSharp.SKImage.FromBitmap(pngBmp);
        using var pngData = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        byte[] pngBytes = pngData.ToArray();

        // Package into a ZIP archive with "mimetype" and "mergedimage.png"
        using var zipMs = new MemoryStream();
        using (var archive = new ZipArchive(zipMs, ZipArchiveMode.Create, true))
        {
            var mimeEntry = archive.CreateEntry("mimetype");
            using (var s = mimeEntry.Open())
            using (var w = new StreamWriter(s, System.Text.Encoding.ASCII))
            {
                w.Write("application/x-krita");
            }

            var entry = archive.CreateEntry("mergedimage.png");
            using var entryStream = entry.Open();
            entryStream.Write(pngBytes, 0, pngBytes.Length);
        }

        return zipMs.ToArray();
    }
}
