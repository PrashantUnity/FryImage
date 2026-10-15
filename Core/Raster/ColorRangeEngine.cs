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
        Color sampledColor,
        bool localized = false,
        double rangePercent = 100.0,
        (double X, double Y)[]? samplePoints = null)
    {
        if (width <= 0 || height <= 0 || bgraPixels.Length < width * height * 4)
            return [];

        byte[] mask = new byte[width * height];
        double f = Math.Clamp(fuzziness, 1.0, 200.0);
        double maxDist = (f / 200.0) * 441.67; // sqrt(255^2 * 3)

        byte targetB = sampledColor.B;
        byte targetG = sampledColor.G;
        byte targetR = sampledColor.R;

        // Chai & Ngan (1999) Skin Tones ellipse constants
        const double SkinCb = 104.0;
        const double SkinCr = 158.0;
        const double SkinAlong = 22.0;
        const double SkinAcross = 14.0;
        const double SkinAngleDeg = -40.0;
        double skinRad = SkinAngleDeg * Math.PI / 180.0;
        double skinCos = Math.Cos(skinRad);
        double skinSin = Math.Sin(skinRad);
        double skinRamp = Math.Max(f / 100.0, 1e-3);

        double clusterRadius = (Math.Clamp(rangePercent, 0.0, 100.0) / 100.0) * Math.Max(width, height);
        bool hasClusters = localized && samplePoints != null && samplePoints.Length > 0;

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
                        val = ComputeHueFamily(r, g, b, 0.0);
                        break;

                    case "Yellows":
                        val = ComputeHueFamily(r, g, b, 60.0);
                        break;

                    case "Greens":
                        val = ComputeHueFamily(r, g, b, 120.0);
                        break;

                    case "Cyans":
                        val = ComputeHueFamily(r, g, b, 180.0);
                        break;

                    case "Blues":
                        val = ComputeHueFamily(r, g, b, 240.0);
                        break;

                    case "Magentas":
                        val = ComputeHueFamily(r, g, b, 300.0);
                        break;

                    case "Skin Tones":
                    case "SkinTones":
                    {
                        double yLum = 0.299 * r + 0.587 * g + 0.114 * b;
                        double cb = 128.0 - 0.168736 * r - 0.331264 * g + 0.5 * b - SkinCb;
                        double cr = 128.0 + 0.5 * r - 0.418688 * g - 0.081312 * b - SkinCr;

                        double u = (skinCos * cb + skinSin * cr) / SkinAlong;
                        double v = (skinCos * cr - skinSin * cb) / SkinAcross;
                        double d = Math.Sqrt(u * u + v * v);

                        double chroma = Math.Clamp(1.0 - (d - 1.0) / skinRamp, 0.0, 1.0);
                        double dark = Math.Clamp((yLum - 25.0) / 30.0, 0.0, 1.0);
                        double bright = Math.Clamp((250.0 - yLum) / 20.0, 0.0, 1.0);

                        val = chroma * dark * bright;
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

                    case "Sampled Colors":
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

                // Apply Localized Color Clusters distance falloff if requested
                if (hasClusters && val > 0.0)
                {
                    double maxNear = 0.0;
                    for (int i = 0; i < samplePoints!.Length; i++)
                    {
                        var (sx, sy) = samplePoints[i];
                        double dx = x - sx;
                        double dy = y - sy;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        double near = Math.Clamp(1.0 - dist / Math.Max(clusterRadius, 1.0), 0.0, 1.0);
                        if (near > maxNear) maxNear = near;
                    }
                    val *= maxNear;
                }

                byte byteVal = (byte)Math.Clamp(Math.Round(val * 255.0), 0, 255);
                if (invert) byteVal = (byte)(255 - byteVal);

                mask[rowOffset + x] = byteVal;
            }
        });

        return mask;
    }

    /// <summary>
    /// Computes hue family selection matching Studio 2025 measured calibration:
    /// (1 - dh/60) * chroma * (2 - max).
    /// </summary>
    private static double ComputeHueFamily(byte r, byte g, byte b, double centerDegrees)
    {
        double rf = r / 255.0;
        double gf = g / 255.0;
        double bf = b / 255.0;

        double mx = Math.Max(rf, Math.Max(gf, bf));
        double mn = Math.Min(rf, Math.Min(gf, bf));
        double chroma = mx - mn;
        if (chroma <= 0.0) return 0.0;

        double h;
        if (mx == rf)
        {
            h = ((gf - bf) / chroma) % 6.0;
            if (h < 0.0) h += 6.0;
        }
        else if (mx == gf)
        {
            h = ((bf - rf) / chroma) + 2.0;
        }
        else
        {
            h = ((rf - gf) / chroma) + 4.0;
        }

        double hue = h * 60.0;
        double dh = Math.Abs(hue - centerDegrees) % 360.0;
        dh = Math.Min(dh, 360.0 - dh);

        return Math.Clamp(1.0 - (dh / 60.0), 0.0, 1.0) * chroma * (2.0 - mx);
    }
}
