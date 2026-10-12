using System;
using System.Threading.Tasks;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Content-Aware Scale Engine implementing Seam Carving (Avidan &amp; Shamir, SIGGRAPH 2007).
/// Intelligently carves or inserts low-energy paths across an image to resize without distorting
/// prominent subjects or features.
/// </summary>
public static class SeamCarvingEngine
{
    private const float ProtectEnergyBoost = 10000.0f;

    /// <summary>
    /// Resizes an SKBitmap to target dimensions using Content-Aware Seam Carving.
    /// </summary>
    public static SKBitmap ResizeContentAware(
        SKBitmap source,
        int targetWidth,
        int targetHeight,
        byte[]? protectMask = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (targetWidth <= 0 || targetHeight <= 0) throw new ArgumentOutOfRangeException("Dimensions must be positive.");

        int curWidth = source.Width;
        int curHeight = source.Height;

        // If dimensions already match, return a clean clone
        if (curWidth == targetWidth && curHeight == targetHeight)
        {
            return source.Copy();
        }

        // Work in uint[] 32-bit pixel buffer for highest speed
        int totalPixels = curWidth * curHeight;
        var pixels = new uint[totalPixels];
        var srcPixels = source.Pixels;
        for (int i = 0; i < totalPixels; i++)
        {
            pixels[i] = (uint)srcPixels[i];
        }

        byte[]? curMask = protectMask != null && protectMask.Length == totalPixels
            ? (byte[])protectMask.Clone()
            : null;

        // Step 1: Adjust Width (Vertical Seams)
        if (curWidth != targetWidth)
        {
            if (curWidth > targetWidth)
            {
                // Shrink width
                int seamsToRemove = curWidth - targetWidth;
                for (int s = 0; s < seamsToRemove; s++)
                {
                    var energy = ComputeEnergyMap(pixels, curWidth, curHeight, curMask);
                    var seam = FindVerticalSeam(energy, curWidth, curHeight);
                    RemoveVerticalSeam(ref pixels, ref curMask, curWidth, curHeight, seam);
                    curWidth--;
                }
            }
            else
            {
                // Enlarge width
                int seamsToAdd = targetWidth - curWidth;
                pixels = EnlargeWidth(pixels, ref curMask, curWidth, curHeight, seamsToAdd);
                curWidth = targetWidth;
            }
        }

        // Step 2: Adjust Height (Horizontal Seams by Transposing)
        if (curHeight != targetHeight)
        {
            // Transpose image
            var transposed = Transpose(pixels, curWidth, curHeight);
            var transMask = curMask != null ? TransposeMask(curMask, curWidth, curHeight) : null;
            int tWidth = curHeight;
            int tHeight = curWidth;
            int tTargetWidth = targetHeight;

            if (tWidth > tTargetWidth)
            {
                int seamsToRemove = tWidth - tTargetWidth;
                for (int s = 0; s < seamsToRemove; s++)
                {
                    var energy = ComputeEnergyMap(transposed, tWidth, tHeight, transMask);
                    var seam = FindVerticalSeam(energy, tWidth, tHeight);
                    RemoveVerticalSeam(ref transposed, ref transMask, tWidth, tHeight, seam);
                    tWidth--;
                }
            }
            else
            {
                int seamsToAdd = tTargetWidth - tWidth;
                transposed = EnlargeWidth(transposed, ref transMask, tWidth, tHeight, seamsToAdd);
                tWidth = tTargetWidth;
            }

            // Transpose back
            pixels = Transpose(transposed, tWidth, tHeight);
            curWidth = tHeight;
            curHeight = tWidth;
        }

        // Construct final SKBitmap
        var result = new SKBitmap(curWidth, curHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        var resPixels = new SKColor[curWidth * curHeight];
        for (int i = 0; i < curWidth * curHeight; i++)
        {
            resPixels[i] = new SKColor(pixels[i]);
        }
        result.Pixels = resPixels;

        return result;
    }

    /// <summary>
    /// Computes dual-gradient energy map: E(x, y) = |dx| + |dy| across color channels.
    /// </summary>
    private static float[] ComputeEnergyMap(uint[] pixels, int width, int height, byte[]? mask)
    {
        var energy = new float[width * height];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            int prevRowOffset = Math.Max(0, y - 1) * width;
            int nextRowOffset = Math.Min(height - 1, y + 1) * width;

            for (int x = 0; x < width; x++)
            {
                int prevX = Math.Max(0, x - 1);
                int nextX = Math.Min(width - 1, x + 1);

                uint cLeft = pixels[rowOffset + prevX];
                uint cRight = pixels[rowOffset + nextX];
                uint cUp = pixels[prevRowOffset + x];
                uint cDown = pixels[nextRowOffset + x];

                // Gradient across R, G, B channels
                float dx = Math.Abs((cRight & 0xFF) - (cLeft & 0xFF)) +
                           Math.Abs(((cRight >> 8) & 0xFF) - ((cLeft >> 8) & 0xFF)) +
                           Math.Abs(((cRight >> 16) & 0xFF) - ((cLeft >> 16) & 0xFF));

                float dy = Math.Abs((cDown & 0xFF) - (cUp & 0xFF)) +
                           Math.Abs(((cDown >> 8) & 0xFF) - ((cUp >> 8) & 0xFF)) +
                           Math.Abs(((cDown >> 16) & 0xFF) - ((cUp >> 16) & 0xFF));

                float e = dx + dy;

                if (mask != null && mask[rowOffset + x] > 0)
                {
                    e += ProtectEnergyBoost;
                }

                energy[rowOffset + x] = e;
            }
        });

        return energy;
    }

    /// <summary>
    /// Finds vertical 8-connected minimum cumulative energy seam using dynamic programming.
    /// </summary>
    private static int[] FindVerticalSeam(float[] energy, int width, int height)
    {
        var dp = new float[width * height];
        var backtrack = new int[width * height];

        // Initialize first row
        Array.Copy(energy, 0, dp, 0, width);

        // Fill dynamic programming table
        for (int y = 1; y < height; y++)
        {
            int rowOffset = y * width;
            int prevRowOffset = (y - 1) * width;

            for (int x = 0; x < width; x++)
            {
                float minPrev = dp[prevRowOffset + x];
                int bestK = x;

                if (x > 0 && dp[prevRowOffset + x - 1] < minPrev)
                {
                    minPrev = dp[prevRowOffset + x - 1];
                    bestK = x - 1;
                }
                if (x < width - 1 && dp[prevRowOffset + x + 1] < minPrev)
                {
                    minPrev = dp[prevRowOffset + x + 1];
                    bestK = x + 1;
                }

                dp[rowOffset + x] = energy[rowOffset + x] + minPrev;
                backtrack[rowOffset + x] = bestK;
            }
        }

        // Find minimum column in last row
        int lastRowOffset = (height - 1) * width;
        int minCol = 0;
        float minVal = dp[lastRowOffset];
        for (int x = 1; x < width; x++)
        {
            if (dp[lastRowOffset + x] < minVal)
            {
                minVal = dp[lastRowOffset + x];
                minCol = x;
            }
        }

        // Backtrack upwards
        var seam = new int[height];
        int curCol = minCol;
        for (int y = height - 1; y >= 0; y--)
        {
            seam[y] = curCol;
            if (y > 0)
            {
                curCol = backtrack[y * width + curCol];
            }
        }

        return seam;
    }

    /// <summary>
    /// Removes a single vertical seam from the pixel buffer.
    /// </summary>
    private static void RemoveVerticalSeam(
        ref uint[] pixels,
        ref byte[]? mask,
        int width,
        int height,
        int[] seam)
    {
        int newWidth = width - 1;
        var newPixels = new uint[newWidth * height];
        var newMask = mask != null ? new byte[newWidth * height] : null;

        for (int y = 0; y < height; y++)
        {
            int seamX = seam[y];
            int oldRow = y * width;
            int newRow = y * newWidth;

            // Copy left part
            if (seamX > 0)
            {
                Array.Copy(pixels, oldRow, newPixels, newRow, seamX);
                if (mask != null && newMask != null)
                {
                    Array.Copy(mask, oldRow, newMask, newRow, seamX);
                }
            }

            // Copy right part
            int rightCount = width - seamX - 1;
            if (rightCount > 0)
            {
                Array.Copy(pixels, oldRow + seamX + 1, newPixels, newRow + seamX, rightCount);
                if (mask != null && newMask != null)
                {
                    Array.Copy(mask, oldRow + seamX + 1, newMask, newRow + seamX, rightCount);
                }
            }
        }

        pixels = newPixels;
        mask = newMask;
    }

    /// <summary>
    /// Enlarges width by duplicating low-energy seams and blending with neighbors.
    /// </summary>
    private static uint[] EnlargeWidth(
        uint[] pixels,
        ref byte[]? mask,
        int width,
        int height,
        int seamsToAdd)
    {
        // Limit batch enlargement to prevent degenerate duplicate streaks
        int currentW = width;
        var curPixels = (uint[])pixels.Clone();
        var curMask = mask != null ? (byte[])mask.Clone() : null;

        int added = 0;
        while (added < seamsToAdd)
        {
            int batch = Math.Min(seamsToAdd - added, Math.Max(1, currentW / 4));
            var seams = new int[batch][];

            // Extract seams on a temporary shrinking copy
            var tempPixels = (uint[])curPixels.Clone();
            var tempMask = curMask != null ? (byte[])curMask.Clone() : null;
            int tempW = currentW;

            for (int b = 0; b < batch; b++)
            {
                var energy = ComputeEnergyMap(tempPixels, tempW, height, tempMask);
                seams[b] = FindVerticalSeam(energy, tempW, height);
                RemoveVerticalSeam(ref tempPixels, ref tempMask, tempW, height, seams[b]);
                tempW--;
            }

            // Expand curPixels by inserting interpolated pixels along the recorded seams
            int newW = currentW + batch;
            var expanded = new uint[newW * height];
            var expandedMask = curMask != null ? new byte[newW * height] : null;

            for (int y = 0; y < height; y++)
            {
                int oldOffset = y * currentW;
                int newOffset = y * newW;

                // Sort seam indices for this row
                var rowSeams = new int[batch];
                for (int b = 0; b < batch; b++) rowSeams[b] = seams[b][y];
                Array.Sort(rowSeams);

                int srcX = 0;
                int dstX = 0;
                for (int b = 0; b < batch; b++)
                {
                    int sX = rowSeams[b];
                    while (srcX <= sX && srcX < currentW)
                    {
                        expanded[newOffset + dstX] = curPixels[oldOffset + srcX];
                        if (expandedMask != null && curMask != null)
                            expandedMask[newOffset + dstX] = curMask[oldOffset + srcX];
                        srcX++;
                        dstX++;
                    }

                    // Duplicate with average
                    int nextX = Math.Min(currentW - 1, sX + 1);
                    uint p1 = curPixels[oldOffset + sX];
                    uint p2 = curPixels[oldOffset + nextX];
                    uint avg = BlendRgba(p1, p2);
                    expanded[newOffset + dstX] = avg;
                    if (expandedMask != null && curMask != null)
                        expandedMask[newOffset + dstX] = curMask[oldOffset + sX];
                    dstX++;
                }

                while (srcX < currentW && dstX < newW)
                {
                    expanded[newOffset + dstX] = curPixels[oldOffset + srcX];
                    if (expandedMask != null && curMask != null)
                        expandedMask[newOffset + dstX] = curMask[oldOffset + srcX];
                    srcX++;
                    dstX++;
                }
            }

            curPixels = expanded;
            curMask = expandedMask;
            currentW = newW;
            added += batch;
        }

        mask = curMask;
        return curPixels;
    }

    private static uint BlendRgba(uint c1, uint c2)
    {
        uint r = ((c1 & 0xFF) + (c2 & 0xFF)) >> 1;
        uint g = (((c1 >> 8) & 0xFF) + ((c2 >> 8) & 0xFF)) >> 1;
        uint b = (((c1 >> 16) & 0xFF) + ((c2 >> 16) & 0xFF)) >> 1;
        uint a = (((c1 >> 24) & 0xFF) + ((c2 >> 24) & 0xFF)) >> 1;
        return r | (g << 8) | (b << 16) | (a << 24);
    }

    private static uint[] Transpose(uint[] pixels, int width, int height)
    {
        var result = new uint[width * height];
        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                result[x * height + y] = pixels[rowOffset + x];
            }
        });
        return result;
    }

    private static byte[] TransposeMask(byte[] mask, int width, int height)
    {
        var result = new byte[width * height];
        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                result[x * height + y] = mask[rowOffset + x];
            }
        });
        return result;
    }
}
