using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.Media;
using static PdfEditorApp.Plugins.ImageEditor.Core.Codecs.PsdCodec;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Codecs;

public readonly record struct SwatchItem(string Name, Color Color);

/// <summary>
/// Professional Swatch Codec supporting Color Swatch (.aco) and Swatch Exchange (.ase).
/// </summary>
public static class SwatchCodec
{
    private static readonly byte[] AseSignature = "ASEF"u8.ToArray();

    /// <summary>
    /// Decodes a Color Swatch (.aco) file.
    /// Supports version 1 (unnamed) and version 2 (named UTF-16 swatches).
    /// </summary>
    public static List<SwatchItem> DecodeAco(Stream stream)
    {
        var results = new List<SwatchItem>();
        using var reader = new BigEndianBinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        long startPos = stream.Position;
        if (stream.Length - startPos < 4) return results;

        ushort version = reader.ReadUInt16();
        ushort count = reader.ReadUInt16();

        if (version == 1)
        {
            // Read version 1 entries
            var v1Items = new List<Color>(count);
            for (int i = 0; i < count; i++)
            {
                if (stream.Position + 10 > stream.Length) break;
                ushort colorSpace = reader.ReadUInt16();
                ushort w = reader.ReadUInt16();
                ushort x = reader.ReadUInt16();
                ushort y = reader.ReadUInt16();
                ushort z = reader.ReadUInt16();
                v1Items.Add(ConvertAcoColor(colorSpace, w, x, y, z));
            }

            // Check if version 2 section follows
            if (stream.Position + 4 <= stream.Length)
            {
                ushort v2Version = reader.ReadUInt16();
                if (v2Version == 2)
                {
                    ushort v2Count = reader.ReadUInt16();
                    for (int i = 0; i < v2Count; i++)
                    {
                        if (stream.Position + 14 > stream.Length) break;
                        ushort colorSpace = reader.ReadUInt16();
                        ushort w = reader.ReadUInt16();
                        ushort x = reader.ReadUInt16();
                        ushort y = reader.ReadUInt16();
                        ushort z = reader.ReadUInt16();

                        // Read UTF-16 string (length includes trailing null)
                        uint nameLen = reader.ReadUInt32();
                        string name = $"Swatch {i + 1}";
                        if (nameLen > 0 && stream.Position + (nameLen * 2) <= stream.Length)
                        {
                            byte[] nameBytes = reader.ReadBytes((int)nameLen * 2);
                            string raw = Encoding.BigEndianUnicode.GetString(nameBytes).TrimEnd('\0');
                            if (!string.IsNullOrWhiteSpace(raw)) name = raw;
                        }

                        results.Add(new SwatchItem(name, ConvertAcoColor(colorSpace, w, x, y, z)));
                    }
                }
            }

            // If no valid v2 found, fallback to v1 results
            if (results.Count == 0)
            {
                for (int i = 0; i < v1Items.Count; i++)
                {
                    results.Add(new SwatchItem($"Color {i + 1}", v1Items[i]));
                }
            }
        }
        else if (version == 2)
        {
            // Direct version 2 file
            for (int i = 0; i < count; i++)
            {
                if (stream.Position + 14 > stream.Length) break;
                ushort colorSpace = reader.ReadUInt16();
                ushort w = reader.ReadUInt16();
                ushort x = reader.ReadUInt16();
                ushort y = reader.ReadUInt16();
                ushort z = reader.ReadUInt16();

                uint nameLen = reader.ReadUInt32();
                string name = $"Swatch {i + 1}";
                if (nameLen > 0 && stream.Position + (nameLen * 2) <= stream.Length)
                {
                    byte[] nameBytes = reader.ReadBytes((int)nameLen * 2);
                    string raw = Encoding.BigEndianUnicode.GetString(nameBytes).TrimEnd('\0');
                    if (!string.IsNullOrWhiteSpace(raw)) name = raw;
                }

                results.Add(new SwatchItem(name, ConvertAcoColor(colorSpace, w, x, y, z)));
            }
        }

        return results;
    }

    /// <summary>
    /// Decodes a Swatch Exchange (.ase) file.
    /// </summary>
    public static List<SwatchItem> DecodeAse(Stream stream)
    {
        var results = new List<SwatchItem>();
        using var reader = new BigEndianBinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        if (stream.Length < 12) return results;

        byte[] sig = reader.ReadBytes(4);
        if (sig[0] != 'A' || sig[1] != 'S' || sig[2] != 'E' || sig[3] != 'F')
            return results;

        ushort verMajor = reader.ReadUInt16();
        ushort verMinor = reader.ReadUInt16();
        uint blockCount = reader.ReadUInt32();

        for (int b = 0; b < blockCount; b++)
        {
            if (stream.Position + 6 > stream.Length) break;
            ushort blockType = reader.ReadUInt16();
            uint blockLength = reader.ReadUInt32();

            long blockEnd = stream.Position + blockLength;
            if (blockEnd > stream.Length) break;

            if (blockType == 0x0001) // Color entry
            {
                ushort nameLen = reader.ReadUInt16();
                string name = "Swatch";
                if (nameLen > 0)
                {
                    byte[] nameBytes = reader.ReadBytes(nameLen * 2);
                    name = Encoding.BigEndianUnicode.GetString(nameBytes).TrimEnd('\0');
                }

                byte[] modelBytes = reader.ReadBytes(4);
                string model = Encoding.ASCII.GetString(modelBytes);

                Color col = Colors.Black;
                if (model == "RGB ")
                {
                    float r = reader.ReadSingle();
                    float g = reader.ReadSingle();
                    float bl = reader.ReadSingle();
                    col = Color.FromRgb((byte)Math.Clamp(r * 255f, 0, 255), (byte)Math.Clamp(g * 255f, 0, 255), (byte)Math.Clamp(bl * 255f, 0, 255));
                }
                else if (model == "CMYK")
                {
                    float c = reader.ReadSingle();
                    float m = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float k = reader.ReadSingle();
                    // CMYK -> RGB conversion
                    float r = 1f - Math.Min(1f, c * (1f - k) + k);
                    float g = 1f - Math.Min(1f, m * (1f - k) + k);
                    float bl = 1f - Math.Min(1f, y * (1f - k) + k);
                    col = Color.FromRgb((byte)Math.Clamp(r * 255f, 0, 255), (byte)Math.Clamp(g * 255f, 0, 255), (byte)Math.Clamp(bl * 255f, 0, 255));
                }
                else if (model == "Gray")
                {
                    float gray = reader.ReadSingle();
                    byte gVal = (byte)Math.Clamp(gray * 255f, 0, 255);
                    col = Color.FromRgb(gVal, gVal, gVal);
                }

                results.Add(new SwatchItem(string.IsNullOrWhiteSpace(name) ? $"Color {results.Count + 1}" : name, col));
            }

            stream.Position = blockEnd;
        }

        return results;
    }

    /// <summary>
    /// Encodes a list of swatches into a standard Color Swatch (.aco) format (v1 + v2).
    /// </summary>
    public static void EncodeAco(Stream stream, IReadOnlyList<SwatchItem> swatches)
    {
        using var writer = new BigEndianBinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        ushort count = (ushort)Math.Min(swatches.Count, ushort.MaxValue);

        // Section 1: Version 1 (Raw 16-bit values)
        writer.Write((ushort)1);
        writer.Write(count);
        for (int i = 0; i < count; i++)
        {
            var c = swatches[i].Color;
            writer.Write((ushort)0); // RGB space
            writer.Write((ushort)(c.R * 257));
            writer.Write((ushort)(c.G * 257));
            writer.Write((ushort)(c.B * 257));
            writer.Write((ushort)0);
        }

        // Section 2: Version 2 (Named UTF-16)
        writer.Write((ushort)2);
        writer.Write(count);
        for (int i = 0; i < count; i++)
        {
            var item = swatches[i];
            var c = item.Color;
            writer.Write((ushort)0); // RGB space
            writer.Write((ushort)(c.R * 257));
            writer.Write((ushort)(c.G * 257));
            writer.Write((ushort)(c.B * 257));
            writer.Write((ushort)0);

            string name = string.IsNullOrEmpty(item.Name) ? $"Swatch {i + 1}" : item.Name;
            byte[] nameBytes = Encoding.BigEndianUnicode.GetBytes(name + '\0');
            writer.Write((uint)(name.Length + 1));
            writer.Write(nameBytes);
        }
    }

    private static Color ConvertAcoColor(ushort space, ushort w, ushort x, ushort y, ushort z)
    {
        return space switch
        {
            0 => Color.FromRgb((byte)(w >> 8), (byte)(x >> 8), (byte)(y >> 8)), // RGB
            1 => ColorFromHsb(w / 65535.0, x / 65535.0, y / 65535.0),          // HSB
            2 => ColorFromCmyk(w / 65535.0, x / 65535.0, y / 65535.0, z / 65535.0), // CMYK
            8 => Color.FromRgb((byte)Math.Clamp(w / 39.0625, 0, 255), (byte)Math.Clamp(w / 39.0625, 0, 255), (byte)Math.Clamp(w / 39.0625, 0, 255)), // Gray
            _ => Color.FromRgb((byte)(w >> 8), (byte)(x >> 8), (byte)(y >> 8))
        };
    }

    private static Color ColorFromHsb(double h, double s, double b)
    {
        double r = b, g = b, bl = b;
        if (s > 0)
        {
            double hue = (h * 360.0) % 360.0;
            int sector = (int)(hue / 60.0);
            double fract = (hue / 60.0) - sector;
            double p = b * (1.0 - s);
            double q = b * (1.0 - s * fract);
            double t = b * (1.0 - s * (1.0 - fract));

            switch (sector)
            {
                case 0: r = b; g = t; bl = p; break;
                case 1: r = q; g = b; bl = p; break;
                case 2: r = p; g = b; bl = t; break;
                case 3: r = p; g = q; bl = b; break;
                case 4: r = t; g = p; bl = b; break;
                default: r = b; g = p; bl = q; break;
            }
        }
        return Color.FromRgb((byte)Math.Clamp(r * 255, 0, 255), (byte)Math.Clamp(g * 255, 0, 255), (byte)Math.Clamp(bl * 255, 0, 255));
    }

    private static Color ColorFromCmyk(double c, double m, double y, double k)
    {
        double r = (1.0 - c) * (1.0 - k);
        double g = (1.0 - m) * (1.0 - k);
        double b = (1.0 - y) * (1.0 - k);
        return Color.FromRgb((byte)Math.Clamp(r * 255, 0, 255), (byte)Math.Clamp(g * 255, 0, 255), (byte)Math.Clamp(b * 255, 0, 255));
    }
}
