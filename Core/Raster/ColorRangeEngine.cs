using System;
using System.Threading.Tasks;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Professional studio Color Range Selection Engine.
/// Generates 8-bit soft selection masks by analyzing RGB and perceptual color distances,
/// hue families, and tonal ranges (Highlights, Midtones, Shadows).
/// </summary>
public static class ColorRangeEngine
{
    public static byte[] ComputeMask(
        byte[] bgraPixels,
        int width,
        int height,
        string selectMode,
        double fuzziness,
        bool invert,
        Color sampledColor)
    {
        if (width <= 0 || height <= 0 || bgraPixels.Length < width * height * 4)
            return [];

        byte[] mask = new byte[width * height];
        double f = Math.Clamp(fuzziness, 1.0, 200.0);
        double maxDist = (f / 200.0) * 441.67; // sqrt(255^2 * 3)

        byte targetB = sampledColor.B;
        byte targetG = sampledColor.G;
        byte targetR = sampledColor.R;

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            int pixelOffset = rowOffset * 4;

            for (int x = 0; x < width; x++)
            {
                int p = pixelOffset + x * 4;
                byte b = bgraPixels[p];
                byte g = bgraPixels[p + 1];
                byte r = bgraPixels[p + 2];
                byte a = bgraPixels[p + 3];

                if (a == 0)
                {
                    mask[rowOffset + x] = invert ? (byte)255 : (byte)0;
                    continue;
                }

                double val = 0.0;

                switch (selectMode)
                {
                    case "Reds":
                    {
                        double diff = r - Math.Max(g, b);
                        val = diff > 0 ? Math.Clamp(diff / 128.0, 0.0, 1.0) : 0.0;
                        break;
                    }

                    case "Greens":
                    {
                        double diff = g - Math.Max(r, b);
                        val = diff > 0 ? Math.Clamp(diff / 128.0, 0.0, 1.0) : 0.0;
                        break;
                    }

                    case "Blues":
                    {
                        double diff = b - Math.Max(r, g);
                        val = diff > 0 ? Math.Clamp(diff / 128.0, 0.0, 1.0) : 0.0;
                        break;
                    }

                    case "Yellows":
                    {
                        double minRG = Math.Min(r, g);
                        double diff = minRG - b;
                        val = diff > 0 ? Math.Clamp(diff / 128.0, 0.0, 1.0) : 0.0;
                        break;
                    }

                    case "Cyans":
                    {
                        double minGB = Math.Min(g, b);
                        double diff = minGB - r;
                        val = diff > 0 ? Math.Clamp(diff / 128.0, 0.0, 1.0) : 0.0;
                        break;
                    }

                    case "Magentas":
                    {
                        double minRB = Math.Min(r, b);
                        double diff = minRB - g;
                        val = diff > 0 ? Math.Clamp(diff / 128.0, 0.0, 1.0) : 0.0;
                        break;
                    }

                    case "Highlights":
                    {
                        double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                        if (lum >= 192.0) val = 1.0;
                        else if (lum >= 128.0) val = (lum - 128.0) / 64.0;
                        else val = 0.0;
                        break;
                    }

                    case "Shadows":
                    {
                        double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                        if (lum <= 64.0) val = 1.0;
                        else if (lum <= 128.0) val = (128.0 - lum) / 64.0;
                        else val = 0.0;
                        break;
                    }

                    case "Midtones":
                    {
                        double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                        double distMid = Math.Abs(lum - 128.0);
                        val = distMid <= 64.0 ? 1.0 - (distMid / 64.0) : 0.0;
                        break;
                    }

                    case "SampledColors":
                    default:
                    {
                        double db = b - targetB;
                        double dg = g - targetG;
                        double dr = r - targetR;
                        double dist = Math.Sqrt(dr * dr + dg * dg + db * db);

                        if (dist <= maxDist * 0.4)
                        {
                            val = 1.0;
                        }
                        else if (dist < maxDist)
                        {
                            double t = (dist - maxDist * 0.4) / (maxDist * 0.6);
                            val = Math.Clamp(1.0 - t, 0.0, 1.0);
                        }
                        else
                        {
                            val = 0.0;
                        }
                        break;
                    }
                }

                byte byteVal = (byte)Math.Clamp(Math.Round(val * 255.0), 0, 255);
                if (invert) byteVal = (byte)(255 - byteVal);

                mask[rowOffset + x] = byteVal;
            }
        });

        return mask;
    }
}
