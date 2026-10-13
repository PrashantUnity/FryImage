using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.ImageEditor.Models;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// OpenRaster (.ora) interchange format codec (OpenRaster Specification v0.0.6).
/// Supports reading and writing layered raster image stacks with zip archives,
/// stack.xml layer manifests, composite rendering, and feature loss detection (#3806).
/// </summary>
public static class OraCodec
{
    public sealed class OraLayer
    {
        public string Name { get; set; } = "Layer";
        public int X { get; set; }
        public int Y { get; set; }
        public double Opacity { get; set; } = 1.0;
        public bool IsVisible { get; set; } = true;
        public byte[] PngBytes { get; set; } = [];
    }

    public sealed class OraDocument
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<OraLayer> Layers { get; } = new();
        public byte[]? MergedImageBytes { get; set; }
    }

    /// <summary>
    /// Checks what features in a document would be lost when saved to OpenRaster (#3806).
    /// OpenRaster drops adjustment layers, layer styles, vector masks, and smart objects.
    /// </summary>
    public static List<string> CheckLosses(IEnumerable<CanvasElement> elements)
    {
        var losses = new List<string>();

        foreach (var el in elements)
        {
            if (el.IsSmartObject)
            {
                losses.Add($"Smart Object '{el.DisplayName}' will be flattened to raster pixels.");
            }
            if (el.VectorMask != null)
            {
                losses.Add($"Vector mask on '{el.DisplayName}' will be omitted.");
            }
            if (el.Effects?.HasAnyActiveEffect == true)
            {
                losses.Add($"Layer styles on '{el.DisplayName}' will be baked or omitted.");
            }
            if (el.IsClippingMask)
            {
                losses.Add($"Clipping mask grouping on '{el.DisplayName}' is not supported in OpenRaster.");
            }
        }

        return losses;
    }

    public static bool HoldsAt(IEnumerable<CanvasElement> elements) => CheckLosses(elements).Count == 0;

    /// <summary>
    /// Decodes an OpenRaster archive from a stream.
    /// </summary>
    public static OraDocument Decode(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var doc = new OraDocument();

        var mergedEntry = archive.GetEntry("mergedimage.png");
        if (mergedEntry != null)
        {
            using var ms = new MemoryStream();
            using var s = mergedEntry.Open();
            s.CopyTo(ms);
            doc.MergedImageBytes = ms.ToArray();
        }

        var stackEntry = archive.GetEntry("stack.xml");
        if (stackEntry != null)
        {
            using var stackStream = stackEntry.Open();
            var xdoc = XDocument.Load(stackStream);
            var imageElem = xdoc.Element("image");
            if (imageElem != null)
            {
                doc.Width = int.TryParse(imageElem.Attribute("w")?.Value, out int w) ? w : 100;
                doc.Height = int.TryParse(imageElem.Attribute("h")?.Value, out int h) ? h : 100;

                var stack = imageElem.Element("stack");
                if (stack != null)
                {
                    foreach (var layerElem in stack.Elements("layer"))
                    {
                        string name = layerElem.Attribute("name")?.Value ?? "Layer";
                        int x = int.TryParse(layerElem.Attribute("x")?.Value, out int lx) ? lx : 0;
                        int y = int.TryParse(layerElem.Attribute("y")?.Value, out int ly) ? ly : 0;
                        double op = double.TryParse(layerElem.Attribute("opacity")?.Value, System.Globalization.CultureInfo.InvariantCulture, out double lop) ? lop : 1.0;
                        string vis = layerElem.Attribute("visibility")?.Value ?? "visible";
                        string src = layerElem.Attribute("src")?.Value ?? "";

                        byte[] png = [];
                        if (!string.IsNullOrEmpty(src))
                        {
                            var layerFileEntry = archive.GetEntry(src);
                            if (layerFileEntry != null)
                            {
                                using var lms = new MemoryStream();
                                using var ls = layerFileEntry.Open();
                                ls.CopyTo(lms);
                                png = lms.ToArray();
                            }
                        }

                        doc.Layers.Add(new OraLayer
                        {
                            Name = name,
                            X = x,
                            Y = y,
                            Opacity = op,
                            IsVisible = vis != "hidden",
                            PngBytes = png
                        });
                    }
                }
            }
        }

        return doc;
    }

    /// <summary>
    /// Encodes an OpenRaster archive to a stream.
    /// </summary>
    public static void Encode(OraDocument doc, Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        // 1. mimetype (must be uncompressed stored entry)
        var mimeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var mimeWriter = new StreamWriter(mimeEntry.Open(), Encoding.ASCII))
        {
            mimeWriter.Write("image/openraster");
        }

        // 2. Build and write stack.xml
        var xImage = new XElement("image",
            new XAttribute("w", doc.Width),
            new XAttribute("h", doc.Height),
            new XAttribute("version", "0.0.6"));

        var xStack = new XElement("stack");
        for (int i = 0; i < doc.Layers.Count; i++)
        {
            var l = doc.Layers[i];
            string srcPath = $"data/layer{i}.png";
            var xLayer = new XElement("layer",
                new XAttribute("name", l.Name),
                new XAttribute("x", l.X),
                new XAttribute("y", l.Y),
                new XAttribute("opacity", l.Opacity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
                new XAttribute("visibility", l.IsVisible ? "visible" : "hidden"),
                new XAttribute("src", srcPath));
            xStack.Add(xLayer);
        }
        xImage.Add(xStack);
        var xDoc = new XDocument(new XDeclaration("1.0", "UTF-8", null), xImage);

        var stackEntry = archive.CreateEntry("stack.xml", CompressionLevel.Fastest);
        using (var stackStream = stackEntry.Open())
        {
            xDoc.Save(stackStream);
        }

        // 3. Write layer PNG entries sequentially
        for (int i = 0; i < doc.Layers.Count; i++)
        {
            var l = doc.Layers[i];
            if (l.PngBytes.Length > 0)
            {
                string srcPath = $"data/layer{i}.png";
                var layerEntry = archive.CreateEntry(srcPath, CompressionLevel.Fastest);
                using var ls = layerEntry.Open();
                ls.Write(l.PngBytes, 0, l.PngBytes.Length);
            }
        }

        // 3. mergedimage.png composite
        if (doc.MergedImageBytes != null && doc.MergedImageBytes.Length > 0)
        {
            var mergedEntry = archive.CreateEntry("mergedimage.png", CompressionLevel.Fastest);
            using var ms = mergedEntry.Open();
            ms.Write(doc.MergedImageBytes, 0, doc.MergedImageBytes.Length);
        }
    }
}
