using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// Lightweight, zero-dependency PSD file format codec.
/// Supports reading and writing layered 8-bit RGB PSD documents (8BPS),
/// extracting layers with bounds, names, opacities, and pixel data.
/// </summary>
public static class PsdCodec
{
    private static readonly byte[] Signature8Bps = "8BPS"u8.ToArray();
    private static readonly byte[] Signature8Bim = "8BIM"u8.ToArray();

    public sealed class PsdLayerInfo
    {
        public string Name { get; set; } = "Layer";
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public int Width => Math.Max(0, Right - Left);
        public int Height => Math.Max(0, Bottom - Top);
        public byte Opacity { get; set; } = 255;
        public bool IsVisible { get; set; } = true;
        public string BlendMode { get; set; } = "norm";
        public byte[]? BgraPixels { get; set; }
    }

    public sealed class PsdDocument
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Channels { get; set; } = 3;
        public int BitsPerChannel { get; set; } = 8;
        public List<PsdLayerInfo> Layers { get; set; } = new();
        public byte[]? CompositeBgraPixels { get; set; }
    }

    /// <summary>
    /// Reads a layered PSD document from a stream.
    /// </summary>
    public static PsdDocument Decode(Stream stream)
    {
        using var reader = new BigEndianBinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        // 1. Header
        byte[] sig = reader.ReadBytes(4);
        if (!SpanEquals(sig, Signature8Bps))
            throw new InvalidDataException("Invalid PSD signature, expected '8BPS'.");

        ushort version = reader.ReadUInt16();
        if (version != 1)
            throw new InvalidDataException($"Unsupported PSD version: {version}.");

        reader.ReadBytes(6); // 6 reserved bytes
        ushort channels = reader.ReadUInt16();
        uint height = reader.ReadUInt32();
        uint width = reader.ReadUInt32();
        ushort depth = reader.ReadUInt16();
        ushort colorMode = reader.ReadUInt16(); // 3 = RGB

        var doc = new PsdDocument
        {
            Width = (int)width,
            Height = (int)height,
            Channels = channels,
            BitsPerChannel = depth
        };

        // 2. Color Mode Data
        uint colorModeLen = reader.ReadUInt32();
        if (colorModeLen > 0) reader.ReadBytes((int)colorModeLen);

        // 3. Image Resources
        uint imgResLen = reader.ReadUInt32();
        if (imgResLen > 0) reader.ReadBytes((int)imgResLen);

        // 4. Layer and Mask Information
        uint layerSectionLen = reader.ReadUInt32();
        if (layerSectionLen > 0)
        {
            long layerSectionStart = stream.Position;
            uint layerInfoLen = reader.ReadUInt32();
            if (layerInfoLen > 0)
            {
                short layerCountRaw = reader.ReadInt16();
                int layerCount = Math.Abs((int)layerCountRaw);

                var layers = new List<PsdLayerInfo>(layerCount);

                // Layer Records
                for (int i = 0; i < layerCount; i++)
                {
                    int top = reader.ReadInt32();
                    int left = reader.ReadInt32();
                    int bottom = reader.ReadInt32();
                    int right = reader.ReadInt32();
                    ushort layerChannels = reader.ReadUInt16();

                    var layer = new PsdLayerInfo
                    {
                        Top = top,
                        Left = left,
                        Bottom = bottom,
                        Right = right
                    };

                    // Channel info records (6 bytes each: 2 byte id, 4 byte data length)
                    var channelIds = new short[layerChannels];
                    var channelLens = new uint[layerChannels];
                    for (int c = 0; c < layerChannels; c++)
                    {
                        channelIds[c] = reader.ReadInt16();
                        channelLens[c] = reader.ReadUInt32();
                    }

                    // Blend mode signature
                    byte[] blendSig = reader.ReadBytes(4);
                    byte[] blendKey = reader.ReadBytes(4);
                    layer.BlendMode = Encoding.ASCII.GetString(blendKey).Trim();

                    layer.Opacity = reader.ReadByte();
                    byte clipping = reader.ReadByte();
                    byte flags = reader.ReadByte();
                    layer.IsVisible = (flags & 0x02) == 0;
                    reader.ReadByte(); // filler

                    // Extra data
                    uint extraLen = reader.ReadUInt32();
                    long extraStart = stream.Position;

                    // Layer mask data
                    uint maskLen = reader.ReadUInt32();
                    if (maskLen > 0) reader.ReadBytes((int)maskLen);

                    // Layer blending ranges
                    uint blendRangesLen = reader.ReadUInt32();
                    if (blendRangesLen > 0) reader.ReadBytes((int)blendRangesLen);

                    // Layer name (Pascal string padded to multiple of 4 bytes)
                    byte nameLen = reader.ReadByte();
                    if (nameLen > 0)
                    {
                        byte[] nameBytes = reader.ReadBytes(nameLen);
                        layer.Name = Encoding.UTF8.GetString(nameBytes);
                    }
                    int nameTotal = 1 + nameLen;
                    int pad = (4 - (nameTotal % 4)) % 4;
                    if (pad > 0) reader.ReadBytes(pad);

                    // Skip any remaining extra data
                    long extraRead = stream.Position - extraStart;
                    if (extraRead < extraLen)
                    {
                        reader.ReadBytes((int)(extraLen - extraRead));
                    }

                    layers.Add(layer);
                }

                // Channel Image Data for each layer
                foreach (var layer in layers)
                {
                    int lw = layer.Width;
                    int lh = layer.Height;
                    if (lw <= 0 || lh <= 0) continue;

                    byte[] r = new byte[lw * lh];
                    byte[] g = new byte[lw * lh];
                    byte[] b = new byte[lw * lh];
                    byte[] a = new byte[lw * lh];
                    Array.Fill(a, (byte)255);

                    // Typically channels are: 0=R, 1=G, 2=B, -1=A (Transparency mask)
                    // The channels appear in order of the layer records
                    for (int c = 0; c < 4; c++)
                    {
                        if (stream.Position >= layerSectionStart + layerSectionLen) break;

                        ushort comp = reader.ReadUInt16(); // 0 = Raw, 1 = RLE
                        byte[] target = c switch
                        {
                            0 => r,
                            1 => g,
                            2 => b,
                            _ => a
                        };

                        if (comp == 0) // Raw
                        {
                            reader.Read(target, 0, target.Length);
                        }
                        else if (comp == 1) // RLE (PackBits)
                        {
                            // Row byte counts
                            var rowCounts = new ushort[lh];
                            for (int row = 0; row < lh; row++)
                            {
                                rowCounts[row] = reader.ReadUInt16();
                            }

                            // Decode scanlines
                            int offset = 0;
                            for (int row = 0; row < lh; row++)
                            {
                                int byteCount = rowCounts[row];
                                byte[] compressedRow = reader.ReadBytes(byteCount);
                                offset += DecodePackBits(compressedRow, target, offset, lw);
                            }
                        }
                    }

                    // Assemble BGRA buffer
                    byte[] bgra = new byte[lw * lh * 4];
                    for (int p = 0; p < lw * lh; p++)
                    {
                        int baseIdx = p * 4;
                        bgra[baseIdx] = b[p];
                        bgra[baseIdx + 1] = g[p];
                        bgra[baseIdx + 2] = r[p];
                        bgra[baseIdx + 3] = a[p];
                    }
                    layer.BgraPixels = bgra;
                }

                doc.Layers = layers;
            }

            // Seek past layer section if needed
            long totalRead = stream.Position - layerSectionStart;
            if (totalRead < layerSectionLen)
            {
                reader.ReadBytes((int)(layerSectionLen - totalRead));
            }
        }

        // 5. Image Data (Composite)
        if (stream.Position < stream.Length)
        {
            try
            {
                ushort comp = reader.ReadUInt16();
                int cw = doc.Width;
                int ch = doc.Height;
                int totalPixels = cw * ch;

                if (totalPixels > 0 && totalPixels < 50_000_000)
                {
                    byte[] cr = new byte[totalPixels];
                    byte[] cg = new byte[totalPixels];
                    byte[] cb = new byte[totalPixels];

                    if (comp == 0) // Raw
                    {
                        reader.Read(cr, 0, totalPixels);
                        reader.Read(cg, 0, totalPixels);
                        reader.Read(cb, 0, totalPixels);
                    }
                    else if (comp == 1) // RLE
                    {
                        int numScanlines = ch * doc.Channels;
                        var rowLens = new ushort[numScanlines];
                        for (int s = 0; s < numScanlines; s++)
                            rowLens[s] = reader.ReadUInt16();

                        int scanlineIdx = 0;
                        for (int c = 0; c < Math.Min(3, doc.Channels); c++)
                        {
                            byte[] target = c == 0 ? cr : (c == 1 ? cg : cb);
                            int off = 0;
                            for (int row = 0; row < ch; row++)
                            {
                                int rLen = rowLens[scanlineIdx++];
                                byte[] compRow = reader.ReadBytes(rLen);
                                off += DecodePackBits(compRow, target, off, cw);
                            }
                        }
                    }

                    byte[] compositeBgra = new byte[totalPixels * 4];
                    for (int p = 0; p < totalPixels; p++)
                    {
                        int baseIdx = p * 4;
                        compositeBgra[baseIdx] = cb[p];
                        compositeBgra[baseIdx + 1] = cg[p];
                        compositeBgra[baseIdx + 2] = cr[p];
                        compositeBgra[baseIdx + 3] = 255;
                    }
                    doc.CompositeBgraPixels = compositeBgra;
                }
            }
            catch
            {
                // Composite is optional if layer records were read
            }
        }

        return doc;
    }

    /// <summary>
    /// Writes a layered PSD document to a stream.
    /// </summary>
    public static void Encode(PsdDocument doc, Stream stream)
    {
        using var writer = new BigEndianBinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // 1. Header
        writer.Write(Signature8Bps);
        writer.Write((ushort)1); // Version 1
        writer.Write(new byte[6]); // Reserved
        writer.Write((ushort)3); // 3 channels (RGB)
        writer.Write((uint)doc.Height);
        writer.Write((uint)doc.Width);
        writer.Write((ushort)8); // 8-bit depth
        writer.Write((ushort)3); // ColorMode: RGB

        // 2. Color Mode Data
        writer.Write((uint)0);

        // 3. Image Resources (Resolution Info 300 DPI)
        using (var resMs = new MemoryStream())
        using (var resWriter = new BigEndianBinaryWriter(resMs))
        {
            resWriter.Write(Signature8Bim);
            resWriter.Write((ushort)0x03ED); // ResolutionInfo
            resWriter.Write((byte)0); // Empty Pascal name
            resWriter.Write((byte)0); // Padding
            resWriter.Write((uint)16); // Data length
            resWriter.Write((uint)(300 << 16)); // HRes: 300 DPI fixed-point
            resWriter.Write((ushort)1); // PixelsPerInch
            resWriter.Write((ushort)1); // Width unit: in
            resWriter.Write((uint)(300 << 16)); // VRes: 300 DPI
            resWriter.Write((ushort)1);
            resWriter.Write((ushort)1);

            byte[] resBytes = resMs.ToArray();
            writer.Write((uint)resBytes.Length);
            writer.Write(resBytes);
        }

        // 4. Layer and Mask Information
        using (var layerMs = new MemoryStream())
        using (var layerWriter = new BigEndianBinaryWriter(layerMs))
        {
            var layers = doc.Layers;
            short layerCount = (short)Math.Max(1, layers.Count);
            layerWriter.Write(layerCount);

            // Channel payload memory streams for RLE encoded image data
            var channelPayloads = new List<byte[]>();

            // Layer Records
            foreach (var layer in layers)
            {
                int lw = Math.Max(1, layer.Width);
                int lh = Math.Max(1, layer.Height);

                layerWriter.Write(layer.Top);
                layerWriter.Write(layer.Left);
                layerWriter.Write(layer.Bottom);
                layerWriter.Write(layer.Right);
                layerWriter.Write((ushort)4); // 4 channels: R, G, B, A

                // Prepare channel payloads (0=R, 1=G, 2=B, -1=A)
                byte[] r = new byte[lw * lh];
                byte[] g = new byte[lw * lh];
                byte[] b = new byte[lw * lh];
                byte[] a = new byte[lw * lh];

                if (layer.BgraPixels != null && layer.BgraPixels.Length >= lw * lh * 4)
                {
                    for (int p = 0; p < lw * lh; p++)
                    {
                        int baseIdx = p * 4;
                        b[p] = layer.BgraPixels[baseIdx];
                        g[p] = layer.BgraPixels[baseIdx + 1];
                        r[p] = layer.BgraPixels[baseIdx + 2];
                        a[p] = layer.BgraPixels[baseIdx + 3];
                    }
                }
                else
                {
                    Array.Fill(a, (byte)255);
                }

                // Compress channels
                byte[] rData = EncodeChannelRle(r, lw, lh);
                byte[] gData = EncodeChannelRle(g, lw, lh);
                byte[] bData = EncodeChannelRle(b, lw, lh);
                byte[] aData = EncodeChannelRle(a, lw, lh);

                // Channel Info: 0=R, 1=G, 2=B, -1=A
                layerWriter.Write((short)0);
                layerWriter.Write((uint)rData.Length);
                layerWriter.Write((short)1);
                layerWriter.Write((uint)gData.Length);
                layerWriter.Write((short)2);
                layerWriter.Write((uint)bData.Length);
                layerWriter.Write((short)-1); // Alpha
                layerWriter.Write((uint)aData.Length);

                channelPayloads.Add(rData);
                channelPayloads.Add(gData);
                channelPayloads.Add(bData);
                channelPayloads.Add(aData);

                // Blend mode
                layerWriter.Write(Signature8Bim);
                byte[] blendBytes = Encoding.ASCII.GetBytes((layer.BlendMode + "    ")[..4]);
                layerWriter.Write(blendBytes);

                layerWriter.Write(layer.Opacity);
                layerWriter.Write((byte)0); // clipping
                byte flags = (byte)(layer.IsVisible ? 0 : 0x02);
                layerWriter.Write(flags);
                layerWriter.Write((byte)0); // filler

                // Extra Data
                using var extraMs = new MemoryStream();
                using var extraWriter = new BigEndianBinaryWriter(extraMs);

                extraWriter.Write((uint)0); // Layer mask data len
                extraWriter.Write((uint)0); // Layer blending ranges len

                // Layer name (Pascal string padded to 4)
                byte[] nameUtf8 = Encoding.UTF8.GetBytes(layer.Name);
                byte nLen = (byte)Math.Min(255, nameUtf8.Length);
                extraWriter.Write(nLen);
                extraWriter.Write(nameUtf8, 0, nLen);
                int nTotal = 1 + nLen;
                int pad = (4 - (nTotal % 4)) % 4;
                if (pad > 0) extraWriter.Write(new byte[pad]);

                byte[] extraBytes = extraMs.ToArray();
                layerWriter.Write((uint)extraBytes.Length);
                layerWriter.Write(extraBytes);
            }

            // Write Channel Data for each layer
            foreach (var payload in channelPayloads)
            {
                layerWriter.Write(payload);
            }

            byte[] layerBytes = layerMs.ToArray();
            writer.Write((uint)(layerBytes.Length + 4)); // Layer section len
            writer.Write((uint)layerBytes.Length); // Layer info len
            writer.Write(layerBytes);
        }

        // 5. Merged Composite Image Data (Raw RGB)
        writer.Write((ushort)0); // Compression 0 = Raw
        int w = doc.Width;
        int h = doc.Height;
        int total = w * h;

        byte[] compR = new byte[total];
        byte[] compG = new byte[total];
        byte[] compB = new byte[total];

        if (doc.CompositeBgraPixels != null && doc.CompositeBgraPixels.Length >= total * 4)
        {
            for (int i = 0; i < total; i++)
            {
                int idx = i * 4;
                compB[i] = doc.CompositeBgraPixels[idx];
                compG[i] = doc.CompositeBgraPixels[idx + 1];
                compR[i] = doc.CompositeBgraPixels[idx + 2];
            }
        }
        else
        {
            Array.Fill(compR, (byte)255);
            Array.Fill(compG, (byte)255);
            Array.Fill(compB, (byte)255);
        }

        writer.Write(compR);
        writer.Write(compG);
        writer.Write(compB);
    }

    private static byte[] EncodeChannelRle(byte[] channel, int width, int height)
    {
        using var ms = new MemoryStream();
        using var bw = new BigEndianBinaryWriter(ms);

        bw.Write((ushort)1); // Compression: 1 = RLE

        var encodedRows = new List<byte[]>(height);
        for (int y = 0; y < height; y++)
        {
            byte[] row = EncodePackBitsRow(channel, y * width, width);
            encodedRows.Add(row);
        }

        // Scanline lengths table
        foreach (var r in encodedRows)
        {
            bw.Write((ushort)r.Length);
        }

        // Scanlines
        foreach (var r in encodedRows)
        {
            bw.Write(r);
        }

        return ms.ToArray();
    }

    private static byte[] EncodePackBitsRow(byte[] src, int offset, int length)
    {
        using var ms = new MemoryStream();
        int i = 0;
        while (i < length)
        {
            int run = 1;
            while (i + run < length && src[offset + i + run] == src[offset + i] && run < 128)
            {
                run++;
            }

            if (run >= 3)
            {
                // Run of identical bytes
                ms.WriteByte((byte)(1 - run + 256));
                ms.WriteByte(src[offset + i]);
                i += run;
            }
            else
            {
                // Literal sequence
                int lit = 0;
                while (i + lit < length && lit < 128)
                {
                    if (i + lit + 2 < length &&
                        src[offset + i + lit] == src[offset + i + lit + 1] &&
                        src[offset + i + lit] == src[offset + i + lit + 2])
                    {
                        break;
                    }
                    lit++;
                }
                if (lit == 0) lit = 1;

                ms.WriteByte((byte)(lit - 1));
                ms.Write(src, offset + i, lit);
                i += lit;
            }
        }
        return ms.ToArray();
    }

    private static int DecodePackBits(byte[] compressed, byte[] target, int targetOffset, int maxExpected)
    {
        int srcIdx = 0;
        int written = 0;

        while (srcIdx < compressed.Length && written < maxExpected)
        {
            sbyte header = (sbyte)compressed[srcIdx++];
            if (header >= 0)
            {
                // Literal run of (header + 1) bytes
                int count = header + 1;
                for (int i = 0; i < count && srcIdx < compressed.Length && written < maxExpected; i++)
                {
                    target[targetOffset + written++] = compressed[srcIdx++];
                }
            }
            else if (header != -128)
            {
                // Repeat byte (1 - header) times
                int count = 1 - header;
                if (srcIdx < compressed.Length)
                {
                    byte val = compressed[srcIdx++];
                    for (int i = 0; i < count && written < maxExpected; i++)
                    {
                        target[targetOffset + written++] = val;
                    }
                }
            }
        }

        return written;
    }

    private static bool SpanEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    internal sealed class BigEndianBinaryReader : BinaryReader
    {
        public BigEndianBinaryReader(Stream stream, Encoding encoding, bool leaveOpen)
            : base(stream, encoding, leaveOpen) { }

        public override ushort ReadUInt16()
        {
            byte[] b = ReadBytes(2);
            return (ushort)((b[0] << 8) | b[1]);
        }

        public override short ReadInt16()
        {
            byte[] b = ReadBytes(2);
            return (short)((b[0] << 8) | b[1]);
        }

        public override uint ReadUInt32()
        {
            byte[] b = ReadBytes(4);
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }

        public override int ReadInt32()
        {
            byte[] b = ReadBytes(4);
            return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        }

        public override float ReadSingle()
        {
            byte[] b = ReadBytes(4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(b);
            return BitConverter.ToSingle(b, 0);
        }
    }

    internal sealed class BigEndianBinaryWriter : BinaryWriter
    {
        public BigEndianBinaryWriter(Stream stream, Encoding encoding = null!, bool leaveOpen = false)
            : base(stream, encoding ?? Encoding.UTF8, leaveOpen) { }

        public override void Write(ushort value)
        {
            Write((byte)(value >> 8));
            Write((byte)(value & 0xFF));
        }

        public override void Write(short value)
        {
            Write((ushort)value);
        }

        public override void Write(uint value)
        {
            Write((byte)(value >> 24));
            Write((byte)((value >> 16) & 0xFF));
            Write((byte)((value >> 8) & 0xFF));
            Write((byte)(value & 0xFF));
        }

        public override void Write(int value)
        {
            Write((uint)value);
        }
    }
}
