using System;
using System.IO;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// Encoder and validator for Windows Icon (.ico) format (#4744 parity).
/// Supported dimensions: up to 256 × 256 pixels.
/// </summary>
public static class IcoCodec
{
    public const int MaxDimension = 256;

    /// <summary>
    /// Validates whether the given dimensions can be encoded as an ICO file.
    /// Returns true if valid, or false if larger than 256x256.
    /// </summary>
    public static bool ValidateDimensions(int width, int height, out string? errorMessage)
    {
        if (width <= 0 || height <= 0)
        {
            errorMessage = "Dimensions must be positive.";
            return false;
        }

        if (width > MaxDimension || height > MaxDimension)
        {
            errorMessage = "ICO is limited to 256 × 256 pixels";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Encodes a SkiaSharp bitmap into a Windows .ico byte array.
    /// </summary>
    public static byte[] Encode(SKBitmap bitmap)
    {
        using var ms = new MemoryStream();
        Encode(bitmap, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Encodes a SkiaSharp bitmap into a Windows .ico stream.
    /// </summary>
    public static void Encode(SKBitmap bitmap, Stream output)
    {
        if (bitmap.Width > MaxDimension || bitmap.Height > MaxDimension)
        {
            throw new ArgumentException($"ICO export is limited to {MaxDimension} × {MaxDimension} pixels; reduce the export scale.");
        }

        // Encode image as PNG bytes (Standard Vista+ modern ICO payload)
        using var img = SKImage.FromBitmap(bitmap);
        using var pngData = img.Encode(SKEncodedImageFormat.Png, 100);
        byte[] pngBytes = pngData.ToArray();

        using var bw = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);

        // ICONDIR header
        bw.Write((ushort)0); // Reserved, must be 0
        bw.Write((ushort)1); // Image type: 1 = ICO
        bw.Write((ushort)1); // Number of images

        // ICONDIRENTRY (1 entry)
        byte bWidth = (byte)(bitmap.Width == 256 ? 0 : bitmap.Width);
        byte bHeight = (byte)(bitmap.Height == 256 ? 0 : bitmap.Height);

        bw.Write(bWidth);               // Width in pixels (0 means 256)
        bw.Write(bHeight);              // Height in pixels (0 means 256)
        bw.Write((byte)0);              // Color palette count (0 for 32bpp)
        bw.Write((byte)0);              // Reserved
        bw.Write((ushort)1);            // Color planes
        bw.Write((ushort)32);           // Bits per pixel
        bw.Write((uint)pngBytes.Length);// Size of image data in bytes
        bw.Write((uint)22);             // Offset of image data (6 header + 16 dir entry = 22)

        // Write PNG image payload
        bw.Write(pngBytes);
        bw.Flush();
    }
}
