using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// Krita (.kra) composite raster document importer.
/// Reads the official merged preview composite ('mergedimage.png') from the Krita zip archive
/// as an editable raster document layer, strictly validating archive structure and entry bounds.
/// </summary>
public static class KraCodec
{
    private const string KritaMime = "application/x-krita";
    private const int MaxEntrySize = 512 * 1024 * 1024; // 512 MB

    public sealed class KraImage
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public byte[] PngBytes { get; init; } = [];
        public byte[] BgraPixels { get; init; } = [];
        public string Warning { get; init; } = "";
    }

    /// <summary>
    /// Checks whether the stream or bytes represent a valid Krita archive.
    /// </summary>
    public static bool LooksLikeKra(Stream stream)
    {
        long pos = stream.Position;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var mimeEntry = archive.GetEntry("mimetype");
            if (mimeEntry == null) return false;

            using var reader = new StreamReader(mimeEntry.Open(), Encoding.ASCII);
            string mime = reader.ReadToEnd().Trim();
            return string.Equals(mime, KritaMime, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
        finally
        {
            stream.Position = pos;
        }
    }

    /// <summary>
    /// Decodes the Krita merged composite image from raw bytes.
    /// </summary>
    public static KraImage Decode(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return Decode(ms);
    }

    /// <summary>
    /// Decodes the Krita merged composite image from a file path.
    /// </summary>
    public static KraImage Decode(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Decode(stream);
    }

    /// <summary>
    /// Decodes the Krita merged composite image from a stream.
    /// </summary>
    public static KraImage Decode(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var mimeEntry = archive.GetEntry("mimetype");
        if (mimeEntry != null)
        {
            using var mimeReader = new StreamReader(mimeEntry.Open(), Encoding.ASCII);
            string mime = mimeReader.ReadToEnd().Trim();
            if (!string.Equals(mime, KritaMime, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The archive's mimetype is not {KritaMime}.");
            }
        }

        var mergedEntry = archive.GetEntry("mergedimage.png")
                       ?? archive.GetEntry("preview.png");

        if (mergedEntry == null)
            throw new FileNotFoundException("Krita archive does not contain 'mergedimage.png' or preview composite.");

        if (mergedEntry.Length > MaxEntrySize)
            throw new InvalidDataException("Krita composite entry exceeds maximum safe size.");

        byte[] pngBytes;
        using (var entryStream = mergedEntry.Open())
        using (var ms = new MemoryStream())
        {
            entryStream.CopyTo(ms);
            pngBytes = ms.ToArray();
        }

        using var skBmp = SKBitmap.Decode(pngBytes);
        if (skBmp == null)
            throw new InvalidDataException("Failed to decode Krita mergedimage.png preview composite.");

        int w = skBmp.Width;
        int h = skBmp.Height;
        byte[] bgra = skBmp.Bytes;

        return new KraImage
        {
            Width = w,
            Height = h,
            PngBytes = pngBytes,
            BgraPixels = bgra,
            Warning = "Krita composite opened as editable raster document. Complex vector/procedural masks were rendered into merged preview."
        };
    }
}
