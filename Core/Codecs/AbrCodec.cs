using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PdfEditorApp.Plugins.ImageEditor.Core.Paint;
using static PdfEditorApp.Plugins.ImageEditor.Core.Codecs.PsdCodec;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

/// <summary>
/// Professional Studio Brush (.abr) decoder.
/// Supports both legacy v1/v2 computed and sampled brushes, plus modern v6+ sampled tips.
/// Translates Studio brushes into FryImage BrushPreset objects.
/// </summary>
public static class AbrCodec
{
    public sealed class AbrBrush
    {
        public string Name { get; set; } = "Brush";
        public double Diameter { get; set; } = 20.0;
        public double Hardness { get; set; } = 1.0;
        public double Spacing { get; set; } = 0.25;
        public double Roundness { get; set; } = 1.0;
        public double Angle { get; set; } = 0.0;
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[]? AlphaMask { get; set; }
    }

    /// <summary>
    /// Decodes a brush (.abr) stream into a list of parsed brushes.
    /// </summary>
    public static List<AbrBrush> Decode(Stream stream)
    {
        var results = new List<AbrBrush>();
        using var reader = new BigEndianBinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        if (stream.Length < 4) return results;

        ushort version = reader.ReadUInt16();

        if (version is 1 or 2)
        {
            ushort count = reader.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                if (stream.Position + 6 > stream.Length) break;
                ushort type = reader.ReadUInt16(); // 1 = computed, 2 = sampled
                uint len = reader.ReadUInt32();
                long nextPos = stream.Position + len;

                if (type == 1) // Computed
                {
                    reader.ReadUInt32(); // misc
                    ushort spacing = reader.ReadUInt16();
                    ushort diameter = reader.ReadUInt16();
                    ushort roundness = reader.ReadUInt16();
                    short angle = reader.ReadInt16();
                    ushort hardness = reader.ReadUInt16();

                    results.Add(new AbrBrush
                    {
                        Name = $"Computed {diameter}px",
                        Diameter = Math.Max(1.0, (double)diameter),
                        Hardness = Math.Clamp(hardness / 100.0, 0.0, 1.0),
                        Spacing = Math.Clamp(spacing / 100.0, 0.01, 5.0),
                        Roundness = Math.Clamp(roundness / 100.0, 0.01, 1.0),
                        Angle = angle
                    });
                }
                else if (type == 2) // Sampled
                {
                    reader.ReadUInt32(); // misc
                    ushort spacing = reader.ReadUInt16();
                    string brushName = $"Sampled Brush {i + 1}";

                    if (version == 2)
                    {
                        uint nameLen = reader.ReadUInt32();
                        if (nameLen > 0 && stream.Position + nameLen * 2 <= stream.Length)
                        {
                            byte[] nameBytes = reader.ReadBytes((int)nameLen * 2);
                            string n = Encoding.BigEndianUnicode.GetString(nameBytes).TrimEnd('\0');
                            if (!string.IsNullOrWhiteSpace(n)) brushName = n;
                        }
                    }

                    reader.ReadByte(); // anti-alias
                    reader.ReadBytes(8); // short bounds (4 x i16)
                    int top = reader.ReadInt32();
                    int left = reader.ReadInt32();
                    int bottom = reader.ReadInt32();
                    int right = reader.ReadInt32();
                    ushort depth = reader.ReadUInt16();
                    byte compression = reader.ReadByte();

                    int w = Math.Max(0, right - left);
                    int h = Math.Max(0, bottom - top);

                    if (w > 0 && h > 0 && w <= 4096 && h <= 4096)
                    {
                        byte[] mask = new byte[w * h];
                        if (compression == 0) // Raw
                        {
                            int toRead = Math.Min(mask.Length, (int)(stream.Length - stream.Position));
                            reader.Read(mask, 0, toRead);
                        }
                        else if (compression == 1) // RLE PackBits
                        {
                            // Skip line scan offsets (h * 2 bytes)
                            int scanBytes = h * 2;
                            if (stream.Position + scanBytes <= stream.Length)
                            {
                                reader.ReadBytes(scanBytes);
                                DecodePackBits(reader, mask, w, h);
                            }
                        }

                        results.Add(new AbrBrush
                        {
                            Name = brushName,
                            Diameter = Math.Max(w, h),
                            Spacing = Math.Clamp(spacing / 100.0, 0.01, 5.0),
                            Width = w,
                            Height = h,
                            AlphaMask = mask
                        });
                    }
                }

                stream.Position = Math.Min(stream.Length, nextPos);
            }
        }
        else if (version is >= 6 and <= 10) // v6+ modern sectioned format
        {
            ushort subversion = reader.ReadUInt16();

            while (stream.Position + 12 <= stream.Length)
            {
                byte[] sig = reader.ReadBytes(4);
                if (Encoding.ASCII.GetString(sig) != "8BIM")
                {
                    stream.Position -= 3;
                    continue;
                }

                byte[] keyBytes = reader.ReadBytes(4);
                string key = Encoding.ASCII.GetString(keyBytes);
                uint sectionLen = reader.ReadUInt32();
                long sectionEnd = stream.Position + sectionLen;

                if (key == "samp") // Sampled tips
                {
                    while (stream.Position + 12 < sectionEnd && stream.Position < stream.Length)
                    {
                        uint sampleLen = reader.ReadUInt32();
                        long sampleEnd = stream.Position + sampleLen;

                        // Pascal UUID
                        byte uuidLen = reader.ReadByte();
                        if (uuidLen > 0) reader.ReadBytes(uuidLen);

                        // Skip header based on subversion
                        int skipBytes = (subversion == 1) ? 10 : 264;
                        if (stream.Position + skipBytes < stream.Length) reader.ReadBytes(skipBytes);

                        if (stream.Position + 19 <= stream.Length)
                        {
                            int top = reader.ReadInt32();
                            int left = reader.ReadInt32();
                            int bottom = reader.ReadInt32();
                            int right = reader.ReadInt32();
                            short depth = reader.ReadInt16();
                            byte comp = reader.ReadByte();

                            int sw = Math.Max(0, right - left);
                            int sh = Math.Max(0, bottom - top);

                            if (sw > 0 && sh > 0 && sw <= 4096 && sh <= 4096)
                            {
                                byte[] sMask = new byte[sw * sh];
                                if (comp == 0)
                                {
                                    reader.Read(sMask, 0, Math.Min(sMask.Length, (int)(stream.Length - stream.Position)));
                                }
                                else if (comp == 1)
                                {
                                    if (stream.Position + sh * 2 <= stream.Length)
                                    {
                                        reader.ReadBytes(sh * 2);
                                        DecodePackBits(reader, sMask, sw, sh);
                                    }
                                }

                                results.Add(new AbrBrush
                                {
                                    Name = $"Sampled {sw}x{sh}",
                                    Diameter = Math.Max(sw, sh),
                                    Spacing = 0.25,
                                    Width = sw,
                                    Height = sh,
                                    AlphaMask = sMask
                                });
                            }
                        }

                        stream.Position = Math.Min(stream.Length, sampleEnd);
                        // Pad to 4 bytes
                        if ((sampleLen % 4) != 0 && stream.Position < stream.Length)
                        {
                            int p = (int)((4 - (sampleLen % 4)) % 4);
                            reader.ReadBytes(Math.Min(p, (int)(stream.Length - stream.Position)));
                        }
                    }
                }

                stream.Position = Math.Min(stream.Length, sectionEnd);
                if ((sectionLen % 4) != 0 && stream.Position < stream.Length)
                {
                    int p = (int)((4 - (sectionLen % 4)) % 4);
                    reader.ReadBytes(Math.Min(p, (int)(stream.Length - stream.Position)));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// PackBits run-length decompression into single-channel mask.
    /// </summary>
    private static void DecodePackBits(BigEndianBinaryReader reader, byte[] buffer, int width, int height)
    {
        int total = width * height;
        int outIdx = 0;

        while (outIdx < total && reader.BaseStream.Position < reader.BaseStream.Length)
        {
            sbyte n = (sbyte)reader.ReadByte();
            if (n >= 0)
            {
                int count = n + 1;
                for (int i = 0; i < count && outIdx < total && reader.BaseStream.Position < reader.BaseStream.Length; i++)
                {
                    buffer[outIdx++] = reader.ReadByte();
                }
            }
            else if (n > -128)
            {
                int count = -n + 1;
                byte val = reader.ReadByte();
                for (int i = 0; i < count && outIdx < total; i++)
                {
                    buffer[outIdx++] = val;
                }
            }
        }
    }

    /// <summary>
    /// Converts an AbrBrush into a FryImage BrushPreset.
    /// </summary>
    public static BrushPreset ToBrushPreset(AbrBrush abr, string group = "Imported")
    {
        string id = $"abr_{Guid.NewGuid():N}";
        var settings = new BrushSettings
        {
            Size = Math.Clamp(abr.Diameter, 1.0, 500.0),
            Hardness = abr.Hardness,
            Spacing = abr.Spacing,
            Roundness = abr.Roundness,
            Angle = abr.Angle,
            Opacity = 1.0,
            Flow = 1.0,
            Smoothing = 15
        };

        return new BrushPreset(id, abr.Name, group, "Brush", settings);
    }
}
