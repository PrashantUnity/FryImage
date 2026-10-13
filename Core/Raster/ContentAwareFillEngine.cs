using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Content-Aware Fill Engine implementing patch-based texture synthesis and exemplar inpainting
/// (Barnes et al., PatchMatch SIGGRAPH 2009; Wexler et al., Space-Time Completion TPAMI 2007).
/// Reconstructs masked hole regions from surrounding contextual textures with seamless boundary matching.
/// </summary>
public static class ContentAwareFillEngine
{
    private const int PatchRadius = 4; // 9x9 patch
    private const int PatchSize = PatchRadius * 2 + 1;

    /// <summary>
    /// Fills the masked region of an image using exemplar texture synthesis.
    /// </summary>
    public static SKBitmap ApplyContentAwareFill(
        SKBitmap source,
        byte[] mask,
        bool colorAdaptation = true)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mask == null) throw new ArgumentNullException(nameof(mask));

        int width = source.Width;
        int height = source.Height;
        if (mask.Length != width * height)
            throw new ArgumentException("Mask length must match image pixel count.");

        // Copy source pixels to working uint[] buffer
        var pixels = new uint[width * height];
        var srcPixels = source.Pixels;
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (uint)srcPixels[i];
        }

        // Identify hole bounding box
        int minX = width, maxX = 0, minY = height, maxY = 0;
        int holeCount = 0;
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (mask[row + x] > 0)
                {
                    holeCount++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (holeCount == 0) return source.Copy();

        // Expand bounding box with search margin
        const int searchMargin = 64;
        int searchMinX = Math.Max(0, minX - searchMargin);
        int searchMaxX = Math.Min(width - 1, maxX + searchMargin);
        int searchMinY = Math.Max(0, minY - searchMargin);
        int searchMaxY = Math.Min(height - 1, maxY + searchMargin);

        // Collect valid source candidate sample patches (patches with 0 mask pixels)
        var sourceCandidates = new List<(int X, int Y)>();
        const int sampleStep = 2; // Step for speed
        for (int sy = searchMinY + PatchRadius; sy <= searchMaxY - PatchRadius; sy += sampleStep)
        {
            for (int sx = searchMinX + PatchRadius; sx <= searchMaxX - PatchRadius; sx += sampleStep)
            {
                if (IsCleanSourcePatch(mask, width, sx, sy, PatchRadius))
                {
                    sourceCandidates.Add((sx, sy));
                }
            }
        }

        // Fallback: if no candidates in margin, search entire image
        if (sourceCandidates.Count == 0)
        {
            for (int sy = PatchRadius; sy < height - PatchRadius; sy += 4)
            {
                for (int sx = PatchRadius; sx < width - PatchRadius; sx += 4)
                {
                    if (IsCleanSourcePatch(mask, width, sx, sy, PatchRadius))
                    {
                        sourceCandidates.Add((sx, sy));
                    }
                }
            }
        }

        if (sourceCandidates.Count == 0) return source.Copy();

        // Working hole mask (decreases as hole is peeled inward)
        var currentHole = (byte[])mask.Clone();
        int remainingHolePixels = holeCount;

        var rng = new Random(42);

        // Onion-peeling inpainting loop
        while (remainingHolePixels > 0)
        {
            // Find boundary pixels (hole pixels adjacent to at least one filled pixel)
            var boundaryPixels = new List<(int X, int Y)>();
            for (int y = minY; y <= maxY; y++)
            {
                int row = y * width;
                for (int x = minX; x <= maxX; x++)
                {
                    if (currentHole[row + x] == 0) continue;

                    bool hasKnownNeighbor =
                        (x > 0 && currentHole[row + x - 1] == 0) ||
                        (x < width - 1 && currentHole[row + x + 1] == 0) ||
                        (y > 0 && currentHole[(y - 1) * width + x] == 0) ||
                        (y < height - 1 && currentHole[(y + 1) * width + x] == 0);

                    if (hasKnownNeighbor)
                    {
                        boundaryPixels.Add((x, y));
                    }
                }
            }

            if (boundaryPixels.Count == 0) break;

            // Fill current layer of boundary pixels
            var filledThisRound = new (int X, int Y, uint Color)[boundaryPixels.Count];

            Parallel.For(0, boundaryPixels.Count, i =>
            {
                var (bx, by) = boundaryPixels[i];
                var (bestSx, bestSy) = FindBestPatch(
                    pixels,
                    currentHole,
                    width,
                    height,
                    bx,
                    by,
                    sourceCandidates,
                    PatchRadius,
                    rng);

                uint sampleColor = pixels[bestSy * width + bestSx];
                if (colorAdaptation)
                {
                    sampleColor = AdaptPatchColor(pixels, currentHole, width, height, bx, by, bestSx, bestSy, PatchRadius, sampleColor);
                }
                filledThisRound[i] = (bx, by, sampleColor);
            });

            // Commit round
            foreach (var (fx, fy, col) in filledThisRound)
            {
                pixels[fy * width + fx] = col;
                currentHole[fy * width + fx] = 0;
                remainingHolePixels--;
            }
        }

        // Optional Color Adaptation / Seam Smoothing
        if (colorAdaptation)
        {
            SmoothSeamBoundaries(pixels, mask, width, height, minX, maxX, minY, maxY);
        }

        // Construct final SKBitmap
        var result = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var resPixels = new SKColor[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            resPixels[i] = new SKColor(pixels[i]);
        }
        result.Pixels = resPixels;

        return result;
    }

    private static bool IsCleanSourcePatch(byte[] mask, int width, int cx, int cy, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int row = (cy + dy) * width;
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (mask[row + cx + dx] > 0) return false;
            }
        }
        return true;
    }

    private static (int X, int Y) FindBestPatch(
        uint[] pixels,
        byte[] hole,
        int width,
        int height,
        int targetX,
        int targetY,
        List<(int X, int Y)> candidates,
        int radius,
        Random rng)
    {
        long bestCost = long.MaxValue;
        (int X, int Y) bestMatch = candidates[0];

        // Sample up to 64 candidate patches for optimal balance of speed and visual texture match
        int sampleCount = Math.Min(candidates.Count, 48);
        int startOffset = rng.Next(candidates.Count);

        for (int c = 0; c < sampleCount; c++)
        {
            var cand = candidates[(startOffset + c) % candidates.Count];
            long cost = 0;
            int matchedPixels = 0;

            for (int dy = -radius; dy <= radius; dy++)
            {
                int ty = targetY + dy;
                int sy = cand.Y + dy;
                if (ty < 0 || ty >= height || sy < 0 || sy >= height) continue;

                int tRow = ty * width;
                int sRow = sy * width;

                for (int dx = -radius; dx <= radius; dx++)
                {
                    int tx = targetX + dx;
                    int sx = cand.X + dx;
                    if (tx < 0 || tx >= width || sx < 0 || sx >= width) continue;

                    // Only compare against known (unmasked) pixels in the target patch
                    if (hole[tRow + tx] == 0)
                    {
                        uint c1 = pixels[tRow + tx];
                        uint c2 = pixels[sRow + sx];

                        long dr = (c1 & 0xFF) - (c2 & 0xFF);
                        long dg = ((c1 >> 8) & 0xFF) - ((c2 >> 8) & 0xFF);
                        long db = ((c1 >> 16) & 0xFF) - ((c2 >> 16) & 0xFF);

                        cost += (dr * dr) + (dg * dg) + (db * db);
                        matchedPixels++;
                    }
                }
            }

            // Normalise cost by number of compared known pixels
            if (matchedPixels > 0)
            {
                cost = (cost * 100) / matchedPixels;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestMatch = cand;
                }
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Fits channel gain and bias with gradient/sky tint protection (Commit 09db9f14 parity).
    /// Prevents tinted blotches in smooth gradients when variance ratio is dominated by noise.
    /// </summary>
    public static (double Gain, double Bias) FitChannel(double mt, double vt, double ms, double vs, double biasLimit = 20.0)
    {
        double g = vs > 1e-6 ? Math.Sqrt(vt / vs) : 1.0;
        double lo = 0.8;
        double hi = 1.25;

        const double flatVar = 25.0; // (5 levels / 255)^2 in [0..255] space = ~25.0
        if (Math.Max(vt, vs) < flatVar && Math.Abs(mt - ms) <= biasLimit && ms > 1e-4)
        {
            double glo = (mt - biasLimit) / ms;
            double ghi = (mt + biasLimit) / ms;
            if (Math.Max(glo, lo) <= Math.Min(ghi, hi))
            {
                lo = Math.Max(glo, lo);
                hi = Math.Min(ghi, hi);
            }
        }

        g = Math.Clamp(g, lo, hi);
        double b = Math.Clamp(mt - g * ms, -biasLimit, biasLimit);
        return (g, b);
    }

    private static uint AdaptPatchColor(
        uint[] pixels,
        byte[] hole,
        int width,
        int height,
        int targetX,
        int targetY,
        int sourceX,
        int sourceY,
        int radius,
        uint sampleColor)
    {
        double sumTr = 0, sumTg = 0, sumTb = 0;
        double sumTTr = 0, sumTTg = 0, sumTTb = 0;
        double sumSr = 0, sumSg = 0, sumSb = 0;
        double sumSSr = 0, sumSSg = 0, sumSSb = 0;
        int count = 0;

        for (int dy = -radius; dy <= radius; dy++)
        {
            int ty = targetY + dy;
            int sy = sourceY + dy;
            if (ty < 0 || ty >= height || sy < 0 || sy >= height) continue;

            int tRow = ty * width;
            int sRow = sy * width;

            for (int dx = -radius; dx <= radius; dx++)
            {
                int tx = targetX + dx;
                int sx = sourceX + dx;
                if (tx < 0 || tx >= width || sx < 0 || sx >= width) continue;

                if (hole[tRow + tx] == 0)
                {
                    uint ct = pixels[tRow + tx];
                    uint cs = pixels[sRow + sx];

                    double tr = ct & 0xFF;
                    double tg = (ct >> 8) & 0xFF;
                    double tb = (ct >> 16) & 0xFF;

                    double sr = cs & 0xFF;
                    double sg = (cs >> 8) & 0xFF;
                    double sb = (cs >> 16) & 0xFF;

                    sumTr += tr; sumTg += tg; sumTb += tb;
                    sumTTr += tr * tr; sumTTg += tg * tg; sumTTb += tb * tb;

                    sumSr += sr; sumSg += sg; sumSb += sb;
                    sumSSr += sr * sr; sumSSg += sg * sg; sumSSb += sb * sb;
                    count++;
                }
            }
        }

        if (count < 4) return sampleColor;

        double mTr = sumTr / count, mTg = sumTg / count, mTb = sumTb / count;
        double vTr = Math.Max(0.0, sumTTr / count - mTr * mTr);
        double vTg = Math.Max(0.0, sumTTg / count - mTg * mTg);
        double vTb = Math.Max(0.0, sumTTb / count - mTb * mTb);

        double mSr = sumSr / count, mSg = sumSg / count, mSb = sumSb / count;
        double vSr = Math.Max(0.0, sumSSr / count - mSr * mSr);
        double vSg = Math.Max(0.0, sumSSg / count - mSg * mSg);
        double vSb = Math.Max(0.0, sumSSb / count - mSb * mSb);

        var (gr, br) = FitChannel(mTr, vTr, mSr, vSr);
        var (gg, bg) = FitChannel(mTg, vTg, mSg, vSg);
        var (gb, bb) = FitChannel(mTb, vTb, mSb, vSb);

        double r = Math.Clamp(gr * (sampleColor & 0xFF) + br, 0.0, 255.0);
        double g = Math.Clamp(gg * ((sampleColor >> 8) & 0xFF) + bg, 0.0, 255.0);
        double b = Math.Clamp(gb * ((sampleColor >> 16) & 0xFF) + bb, 0.0, 255.0);
        uint a = (sampleColor >> 24) & 0xFF;

        return (uint)Math.Round(r) | ((uint)Math.Round(g) << 8) | ((uint)Math.Round(b) << 16) | (a << 24);
    }

    private static void SmoothSeamBoundaries(
        uint[] pixels,
        byte[] originalMask,
        int width,
        int height,
        int minX,
        int maxX,
        int minY,
        int maxY)
    {
        // 3-pass subtle box blur along original mask transition seam
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = Math.Max(1, minY); y <= Math.Min(height - 2, maxY); y++)
            {
                int row = y * width;
                for (int x = Math.Max(1, minX); x <= Math.Min(width - 2, maxX); x++)
                {
                    // Check if on seam boundary
                    if (originalMask[row + x] > 0)
                    {
                        bool isNearBoundary =
                            originalMask[row + x - 1] == 0 ||
                            originalMask[row + x + 1] == 0 ||
                            originalMask[(y - 1) * width + x] == 0 ||
                            originalMask[(y + 1) * width + x] == 0;

                        if (isNearBoundary)
                        {
                            uint c = pixels[row + x];
                            uint cl = pixels[row + x - 1];
                            uint cr = pixels[row + x + 1];
                            uint cu = pixels[(y - 1) * width + x];
                            uint cd = pixels[(y + 1) * width + x];

                            uint r = (((c & 0xFF) * 2) + (cl & 0xFF) + (cr & 0xFF) + (cu & 0xFF) + (cd & 0xFF)) / 6;
                            uint g = ((((c >> 8) & 0xFF) * 2) + ((cl >> 8) & 0xFF) + ((cr >> 8) & 0xFF) + ((cu >> 8) & 0xFF) + ((cd >> 8) & 0xFF)) / 6;
                            uint b = ((((c >> 16) & 0xFF) * 2) + ((cl >> 16) & 0xFF) + ((cr >> 16) & 0xFF) + ((cu >> 16) & 0xFF) + ((cd >> 16) & 0xFF)) / 6;
                            uint a = (c >> 24) & 0xFF;

                            pixels[row + x] = r | (g << 8) | (b << 16) | (a << 24);
                        }
                    }
                }
            }
        }
    }
}
