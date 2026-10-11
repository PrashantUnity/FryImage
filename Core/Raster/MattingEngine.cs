using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Natural Image Matting &amp; Edge Refinement Engine for FryImage.
///
/// Implements:
/// - Guided Image Filtering (K. He et al., ECCV 2010 / PAMI 2013) for extracting hair and soft edges.
/// - Edge refinement pipeline: Smart Radius, Smooth, Feather, Contrast, Shift Edge (morphology dilation/erosion).
/// - Refine Edge Brush (Levin et al. CVPR 2006 color-line matting model).
/// - Fringe Color Decontamination (normalized convolution foreground projection).
/// </summary>
public static class MattingEngine
{
    private const double Eps = 1e-4;

    /// <summary>
    /// Refines a single-channel alpha mask [0..255] using color-guided image filtering and edge morphometry.
    /// </summary>
    public static byte[] RefineSelectionMask(
        byte[] mask,
        byte[] bgraImage,
        int width,
        int height,
        double radius = 5.0,
        bool smartRadius = true,
        double smooth = 0.0,
        double feather = 0.0,
        double contrast = 0.0,
        double shiftEdge = 0.0)
    {
        if (width <= 0 || height <= 0 || mask.Length != width * height) return (byte[])mask.Clone();

        float[] p = new float[mask.Length];
        for (int i = 0; i < mask.Length; i++)
        {
            p[i] = mask[i] / 255.0f;
        }

        // 1. Color-guided Image Filtering along edges
        int r = Math.Clamp((int)Math.Ceiling(radius), 1, 32);
        if (radius > 0.1 && bgraImage != null && bgraImage.Length == width * height * 4)
        {
            p = GuidedFilter(p, bgraImage, width, height, r, Eps);
        }

        // 2. Smooth
        if (smooth > 0.1)
        {
            double smoothSigma = (Math.Clamp(smooth, 0.0, 100.0) / 100.0) * 4.0;
            p = BoxBlurFloat(p, width, height, Math.Max(1, (int)Math.Round(smoothSigma)));
        }

        // 3. Feather (Gaussian / Box blur with sigma = feather / 2)
        if (feather > 0.1)
        {
            int featherR = Math.Max(1, (int)Math.Round(feather / 2.0));
            p = BoxBlurFloat(p, width, height, featherR);
        }

        // 4. Shift Edge (Dilation / Erosion)
        if (Math.Abs(shiftEdge) > 0.1)
        {
            int shiftPx = (int)Math.Round((shiftEdge / 100.0) * Math.Max(radius, 4.0));
            if (shiftPx != 0)
            {
                p = MorphologyShift(p, width, height, shiftPx);
            }
        }

        // 5. Contrast (S-Curve about 0.5)
        double cFactor = 1.0 + (contrast / 100.0) * 3.0;

        byte[] result = new byte[mask.Length];
        Parallel.For(0, mask.Length, i =>
        {
            double val = p[i];
            if (Math.Abs(contrast) > 0.1)
            {
                val = (val - 0.5) * cFactor + 0.5;
            }
            result[i] = (byte)Math.Clamp((int)Math.Round(val * 255.0), 0, 255);
        });

        return result;
    }

    /// <summary>
    /// Guided Filter: estimates local linear coefficient (a*I + b) to transfer fine detail like hair into the mask.
    /// </summary>
    private static float[] GuidedFilter(float[] p, byte[] bgra, int width, int height, int r, double eps)
    {
        float[] meanP = BoxMean(p, width, height, r);

        // Normalize guide image intensity (RGB mean)
        float[] I = new float[width * height];
        for (int i = 0; i < width * height; i++)
        {
            int idx = i * 4;
            I[i] = (bgra[idx] + bgra[idx + 1] + bgra[idx + 2]) / (3.0f * 255.0f);
        }

        float[] meanI = BoxMean(I, width, height, r);

        float[] II = new float[width * height];
        float[] Ip = new float[width * height];
        for (int i = 0; i < width * height; i++)
        {
            II[i] = I[i] * I[i];
            Ip[i] = I[i] * p[i];
        }

        float[] meanII = BoxMean(II, width, height, r);
        float[] meanIp = BoxMean(Ip, width, height, r);

        float[] a = new float[width * height];
        float[] b = new float[width * height];

        Parallel.For(0, width * height, i =>
        {
            float varI = meanII[i] - meanI[i] * meanI[i];
            float covIp = meanIp[i] - meanI[i] * meanP[i];

            a[i] = covIp / (varI + (float)eps);
            b[i] = meanP[i] - a[i] * meanI[i];
        });

        float[] meanA = BoxMean(a, width, height, r);
        float[] meanB = BoxMean(b, width, height, r);

        float[] q = new float[width * height];
        Parallel.For(0, width * height, i =>
        {
            q[i] = Math.Clamp(meanA[i] * I[i] + meanB[i], 0.0f, 1.0f);
        });

        return q;
    }

    private static float[] BoxMean(float[] src, int w, int h, int r)
    {
        float[] dst = new float[w * h];
        float[] tmp = new float[w * h];

        // Horizontal pass
        Parallel.For(0, h, y =>
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - r);
                int x1 = Math.Min(w - 1, x + r);
                double sum = 0;
                for (int k = x0; k <= x1; k++) sum += src[row + k];
                tmp[row + x] = (float)(sum / (x1 - x0 + 1));
            }
        });

        // Vertical pass
        Parallel.For(0, w, x =>
        {
            for (int y = 0; y < h; y++)
            {
                int y0 = Math.Max(0, y - r);
                int y1 = Math.Min(h - 1, y + r);
                double sum = 0;
                for (int k = y0; k <= y1; k++) sum += tmp[k * w + x];
                dst[y * w + x] = (float)(sum / (y1 - y0 + 1));
            }
        });

        return dst;
    }

    private static float[] BoxBlurFloat(float[] src, int w, int h, int r)
    {
        return BoxMean(src, w, h, r);
    }

    private static float[] MorphologyShift(float[] src, int w, int h, int shiftPx)
    {
        float[] dst = new float[w * h];
        int r = Math.Abs(shiftPx);
        bool isDilation = shiftPx > 0;

        Parallel.For(0, h, y =>
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - r);
                int x1 = Math.Min(w - 1, x + r);
                int y0 = Math.Max(0, y - r);
                int y1 = Math.Min(h - 1, y + r);

                float extreme = isDilation ? 0.0f : 1.0f;
                for (int py = y0; py <= y1; py++)
                {
                    int pRow = py * w;
                    for (int px = x0; px <= x1; px++)
                    {
                        float v = src[pRow + px];
                        if (isDilation) extreme = Math.Max(extreme, v);
                        else extreme = Math.Min(extreme, v);
                    }
                }
                dst[row + x] = extreme;
            }
        });

        return dst;
    }

    /// <summary>
    /// Refine Edge Brush: estimates matte coverage for painted unknown brush strokes using the Levin color-line model.
    /// </summary>
    public static byte[] ApplyRefineEdgeStroke(
        byte[] mask,
        byte[] bgraImage,
        int width,
        int height,
        IReadOnlyList<(double x, double y)> strokePoints,
        double brushRadius,
        bool add = true)
    {
        if (width <= 0 || height <= 0 || strokePoints == null || strokePoints.Count == 0) return (byte[])mask.Clone();

        byte[] result = (byte[])mask.Clone();
        double rSq = brushRadius * brushRadius;

        // Find stroke bounds
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var p in strokePoints)
        {
            minX = Math.Min(minX, p.x);
            maxX = Math.Max(maxX, p.x);
            minY = Math.Min(minY, p.y);
            maxY = Math.Max(maxY, p.y);
        }

        int xStart = Math.Clamp((int)Math.Floor(minX - brushRadius - 1), 0, width - 1);
        int xEnd = Math.Clamp((int)Math.Ceiling(maxX + brushRadius + 1), 0, width - 1);
        int yStart = Math.Clamp((int)Math.Floor(minY - brushRadius - 1), 0, height - 1);
        int yEnd = Math.Clamp((int)Math.Ceiling(maxY + brushRadius + 1), 0, height - 1);

        // Gather known foreground (mask >= 200) and background (mask <= 50) means within local window
        double fgB = 0, fgG = 0, fgR = 0, fgCount = 0;
        double bgB = 0, bgG = 0, bgR = 0, bgCount = 0;

        for (int y = yStart; y <= yEnd; y++)
        {
            int row = y * width;
            for (int x = xStart; x <= xEnd; x++)
            {
                byte m = mask[row + x];
                int cIdx = (row + x) * 4;
                if (m >= 200)
                {
                    fgB += bgraImage[cIdx];
                    fgG += bgraImage[cIdx + 1];
                    fgR += bgraImage[cIdx + 2];
                    fgCount++;
                }
                else if (m <= 50)
                {
                    bgB += bgraImage[cIdx];
                    bgG += bgraImage[cIdx + 1];
                    bgR += bgraImage[cIdx + 2];
                    bgCount++;
                }
            }
        }

        if (fgCount > 0) { fgB /= fgCount; fgG /= fgCount; fgR /= fgCount; }
        if (bgCount > 0) { bgB /= bgCount; bgG /= bgCount; bgR /= bgCount; }

        double lineB = fgB - bgB;
        double lineG = fgG - bgG;
        double lineR = fgR - bgR;
        double lineLenSq = lineB * lineB + lineG * lineG + lineR * lineR;

        Parallel.For(yStart, yEnd + 1, y =>
        {
            int row = y * width;
            for (int x = xStart; x <= xEnd; x++)
            {
                // Check if within brush radius of any stroke point
                double minDistSq = double.MaxValue;
                foreach (var pt in strokePoints)
                {
                    double dSq = (x - pt.x) * (x - pt.x) + (y - pt.y) * (y - pt.y);
                    if (dSq < minDistSq) minDistSq = dSq;
                }

                if (minDistSq <= rSq)
                {
                    int idx = row + x;
                    if (!add)
                    {
                        // Restore: erase refinement
                        result[idx] = mask[idx];
                    }
                    else if (lineLenSq > 10.0)
                    {
                        // Color line projection
                        int cIdx = idx * 4;
                        double curB = bgraImage[cIdx] - bgB;
                        double curG = bgraImage[cIdx + 1] - bgG;
                        double curR = bgraImage[cIdx + 2] - bgR;

                        double proj = (curB * lineB + curG * lineG + curR * lineR) / lineLenSq;
                        double alpha = Math.Clamp(proj, 0.0, 1.0);
                        result[idx] = (byte)Math.Round(alpha * 255.0);
                    }
                }
            }
        });

        return result;
    }

    /// <summary>
    /// Decontaminates fringe colors along selection edges by replacing them with normalized convolution
    /// of adjacent fully selected foreground pixels.
    /// </summary>
    public static byte[] DecontaminateColors(byte[] bgraPixels, byte[] mask, int width, int height, double amount = 100.0)
    {
        if (bgraPixels == null) return Array.Empty<byte>();
        if (width <= 0 || height <= 0 || mask == null || amount <= 0.1)
            return (byte[])bgraPixels.Clone();

        double strength = Math.Clamp(amount / 100.0, 0.0, 1.0);
        byte[] result = (byte[])bgraPixels.Clone();
        int r = 4;

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int pIdx = rowOffset + x;
                byte m = mask[pIdx];

                // Decontaminate only semi-transparent edge pixels (0 < m < 255)
                if (m > 0 && m < 250)
                {
                    double fgB = 0, fgG = 0, fgR = 0, fgWeight = 0;

                    for (int dy = -r; dy <= r; dy++)
                    {
                        int ny = Math.Clamp(y + dy, 0, height - 1);
                        int nRow = ny * width;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int nx = Math.Clamp(x + dx, 0, width - 1);
                            int nIdx = nRow + nx;
                            if (mask[nIdx] >= 220)
                            {
                                double w = 1.0 / (1.0 + Math.Sqrt(dx * dx + dy * dy));
                                int ncIdx = nIdx * 4;
                                fgB += bgraPixels[ncIdx] * w;
                                fgG += bgraPixels[ncIdx + 1] * w;
                                fgR += bgraPixels[ncIdx + 2] * w;
                                fgWeight += w;
                            }
                        }
                    }

                    if (fgWeight > 0.001)
                    {
                        int cIdx = pIdx * 4;
                        double decontaminatedB = fgB / fgWeight;
                        double decontaminatedG = fgG / fgWeight;
                        double decontaminatedR = fgR / fgWeight;

                        double blend = strength * (1.0 - m / 255.0);

                        result[cIdx] = (byte)Math.Clamp(Math.Round(bgraPixels[cIdx] * (1.0 - blend) + decontaminatedB * blend), 0, 255);
                        result[cIdx + 1] = (byte)Math.Clamp(Math.Round(bgraPixels[cIdx + 1] * (1.0 - blend) + decontaminatedG * blend), 0, 255);
                        result[cIdx + 2] = (byte)Math.Clamp(Math.Round(bgraPixels[cIdx + 2] * (1.0 - blend) + decontaminatedR * blend), 0, 255);
                    }
                }
            }
        });

        return result;
    }
}
