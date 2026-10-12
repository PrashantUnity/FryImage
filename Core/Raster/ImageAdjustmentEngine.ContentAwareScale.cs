using System;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// High-performance Content-Aware Scaling via Seam Carving algorithm (Photoshop Edit › Content-Aware Scale).
/// Based on Avidan & Shamir (SIGGRAPH 2007) with dynamic programming vertical and horizontal seam tracking.
/// </summary>
public static partial class ImageAdjustmentEngine
{
    public static byte[] ApplyContentAwareScale(byte[] bgra, int width, int height, int targetWidth, int targetHeight)
    {
        if (width <= 0 || height <= 0 || targetWidth <= 0 || targetHeight <= 0)
            return bgra;

        if (width == targetWidth && height == targetHeight)
            return (byte[])bgra.Clone();

        byte[] current = (byte[])bgra.Clone();
        int curW = width;
        int curH = height;

        // 1. Scale width
        if (curW != targetWidth)
        {
            if (targetWidth < curW)
            {
                // Shrink width by removing vertical seams
                int seamsToRemove = curW - targetWidth;
                for (int s = 0; s < seamsToRemove; s++)
                {
                    float[] energy = ComputeEnergyMap(current, curW, curH);
                    int[] seam = FindVerticalSeam(energy, curW, curH);
                    current = RemoveVerticalSeam(current, curW, curH, seam);
                    curW--;
                }
            }
            else
            {
                // Enlarge width by finding seams on copy and interpolating duplicates
                int seamsToAdd = targetWidth - curW;
                int maxBatch = Math.Min(seamsToAdd, curW / 2);
                while (seamsToAdd > 0)
                {
                    int batch = Math.Min(seamsToAdd, Math.Max(1, maxBatch));
                    float[] energy = ComputeEnergyMap(current, curW, curH);
                    int[][] seams = FindMultipleVerticalSeams(energy, curW, curH, batch);
                    current = InsertVerticalSeams(current, curW, curH, seams);
                    curW += batch;
                    seamsToAdd -= batch;
                }
            }
        }

        // 2. Scale height by transposing, scaling width, and transposing back
        if (curH != targetHeight)
        {
            byte[] transposed = TransposeBgra(current, curW, curH);
            int tW = curH;
            int tH = curW;
            int targetTW = targetHeight;

            if (targetTW < tW)
            {
                int seamsToRemove = tW - targetTW;
                for (int s = 0; s < seamsToRemove; s++)
                {
                    float[] energy = ComputeEnergyMap(transposed, tW, tH);
                    int[] seam = FindVerticalSeam(energy, tW, tH);
                    transposed = RemoveVerticalSeam(transposed, tW, tH, seam);
                    tW--;
                }
            }
            else
            {
                int seamsToAdd = targetTW - tW;
                int maxBatch = Math.Min(seamsToAdd, tW / 2);
                while (seamsToAdd > 0)
                {
                    int batch = Math.Min(seamsToAdd, Math.Max(1, maxBatch));
                    float[] energy = ComputeEnergyMap(transposed, tW, tH);
                    int[][] seams = FindMultipleVerticalSeams(energy, tW, tH, batch);
                    transposed = InsertVerticalSeams(transposed, tW, tH, seams);
                    tW += batch;
                    seamsToAdd -= batch;
                }
            }

            current = TransposeBgra(transposed, tW, tH);
        }

        return current;
    }

    #region Seam Carving Core Primitives

    private static float[] ComputeEnergyMap(byte[] bgra, int width, int height)
    {
        float[] energy = new float[width * height];

        Parallel.For(0, height, y =>
        {
            int row = y * width;
            int prevY = Math.Max(0, y - 1);
            int nextY = Math.Min(height - 1, y + 1);

            int rowUp = prevY * width * 4;
            int rowDown = nextY * width * 4;
            int rowCurr = y * width * 4;

            for (int x = 0; x < width; x++)
            {
                int prevX = Math.Max(0, x - 1);
                int nextX = Math.Min(width - 1, x + 1);

                int idxL = rowCurr + prevX * 4;
                int idxR = rowCurr + nextX * 4;
                int idxU = rowUp + x * 4;
                int idxD = rowDown + x * 4;

                // Gradient magnitude across B, G, R channels (|dx| + |dy|)
                float dxB = Math.Abs(bgra[idxR] - bgra[idxL]);
                float dxG = Math.Abs(bgra[idxR + 1] - bgra[idxL + 1]);
                float dxR = Math.Abs(bgra[idxR + 2] - bgra[idxL + 2]);

                float dyB = Math.Abs(bgra[idxD] - bgra[idxU]);
                float dyG = Math.Abs(bgra[idxD + 1] - bgra[idxU + 1]);
                float dyR = Math.Abs(bgra[idxD + 2] - bgra[idxU + 2]);

                energy[row + x] = (dxB + dxG + dxR) + (dyB + dyG + dyR);
            }
        });

        return energy;
    }

    private static int[] FindVerticalSeam(float[] energy, int width, int height)
    {
        // Dynamic programming table for cumulative minimum energy
        float[] dp = new float[width * height];
        int[] seam = new int[height];

        // Copy first row
        Array.Copy(energy, dp, width);

        // Fill DP table row by row
        for (int y = 1; y < height; y++)
        {
            int prevRow = (y - 1) * width;
            int currRow = y * width;

            for (int x = 0; x < width; x++)
            {
                float minAbove = dp[prevRow + x];
                if (x > 0 && dp[prevRow + x - 1] < minAbove)
                    minAbove = dp[prevRow + x - 1];
                if (x < width - 1 && dp[prevRow + x + 1] < minAbove)
                    minAbove = dp[prevRow + x + 1];

                dp[currRow + x] = energy[currRow + x] + minAbove;
            }
        }

        // Find minimum in bottom row
        int lastRow = (height - 1) * width;
        float minEnergy = float.MaxValue;
        int minX = 0;

        for (int x = 0; x < width; x++)
        {
            if (dp[lastRow + x] < minEnergy)
            {
                minEnergy = dp[lastRow + x];
                minX = x;
            }
        }

        seam[height - 1] = minX;

        // Backtrace seam upwards
        for (int y = height - 2; y >= 0; y--)
        {
            int prevRow = y * width;
            int curX = seam[y + 1];

            int bestX = curX;
            float bestVal = dp[prevRow + curX];

            if (curX > 0 && dp[prevRow + curX - 1] < bestVal)
            {
                bestVal = dp[prevRow + curX - 1];
                bestX = curX - 1;
            }
            if (curX < width - 1 && dp[prevRow + curX + 1] < bestVal)
            {
                bestX = curX + 1;
            }

            seam[y] = bestX;
        }

        return seam;
    }

    private static int[][] FindMultipleVerticalSeams(float[] energy, int width, int height, int count)
    {
        float[] workingEnergy = (float[])energy.Clone();
        var seams = new int[count][];

        for (int i = 0; i < count; i++)
        {
            int[] seam = FindVerticalSeam(workingEnergy, width, height);
            seams[i] = seam;

            // Mark seam with huge energy so subsequent seams do not collide
            for (int y = 0; y < height; y++)
            {
                int x = seam[y];
                workingEnergy[y * width + x] = float.MaxValue / 2;
                if (x > 0) workingEnergy[y * width + x - 1] += 5000f;
                if (x < width - 1) workingEnergy[y * width + x + 1] += 5000f;
            }
        }

        return seams;
    }

    private static byte[] RemoveVerticalSeam(byte[] bgra, int width, int height, int[] seam)
    {
        int newWidth = width - 1;
        byte[] output = new byte[newWidth * height * 4];

        Parallel.For(0, height, y =>
        {
            int seamX = seam[y];
            int srcRow = y * width * 4;
            int dstRow = y * newWidth * 4;

            // Copy left of seam
            if (seamX > 0)
            {
                Buffer.BlockCopy(bgra, srcRow, output, dstRow, seamX * 4);
            }

            // Copy right of seam
            int rightCount = width - 1 - seamX;
            if (rightCount > 0)
            {
                Buffer.BlockCopy(bgra, srcRow + (seamX + 1) * 4, output, dstRow + seamX * 4, rightCount * 4);
            }
        });

        return output;
    }

    private static byte[] InsertVerticalSeams(byte[] bgra, int width, int height, int[][] seams)
    {
        int k = seams.Length;
        int newWidth = width + k;
        byte[] output = new byte[newWidth * height * 4];

        Parallel.For(0, height, y =>
        {
            // Collect seam X coords for this row and sort them
            int[] xs = new int[k];
            for (int i = 0; i < k; i++) xs[i] = seams[i][y];
            Array.Sort(xs);

            int srcRow = y * width * 4;
            int dstRow = y * newWidth * 4;

            int srcX = 0;
            int dstX = 0;

            for (int i = 0; i < k; i++)
            {
                int targetX = xs[i];

                // Copy pixels up to targetX
                while (srcX < targetX && srcX < width)
                {
                    int sIdx = srcRow + srcX * 4;
                    int dIdx = dstRow + dstX * 4;
                    output[dIdx] = bgra[sIdx];
                    output[dIdx + 1] = bgra[sIdx + 1];
                    output[dIdx + 2] = bgra[sIdx + 2];
                    output[dIdx + 3] = bgra[sIdx + 3];
                    srcX++;
                    dstX++;
                }

                // Copy original pixel
                if (srcX < width)
                {
                    int sIdx = srcRow + srcX * 4;
                    int dIdx = dstRow + dstX * 4;
                    output[dIdx] = bgra[sIdx];
                    output[dIdx + 1] = bgra[sIdx + 1];
                    output[dIdx + 2] = bgra[sIdx + 2];
                    output[dIdx + 3] = bgra[sIdx + 3];
                    dstX++;

                    // Insert interpolated duplicate pixel
                    int nextX = Math.Min(width - 1, srcX + 1);
                    int sNextIdx = srcRow + nextX * 4;
                    int dDupIdx = dstRow + dstX * 4;
                    output[dDupIdx] = (byte)((bgra[sIdx] + bgra[sNextIdx]) / 2);
                    output[dDupIdx + 1] = (byte)((bgra[sIdx + 1] + bgra[sNextIdx + 1]) / 2);
                    output[dDupIdx + 2] = (byte)((bgra[sIdx + 2] + bgra[sNextIdx + 2]) / 2);
                    output[dDupIdx + 3] = (byte)((bgra[sIdx + 3] + bgra[sNextIdx + 3]) / 2);
                    dstX++;
                    srcX++;
                }
            }

            // Copy remaining pixels
            while (srcX < width && dstX < newWidth)
            {
                int sIdx = srcRow + srcX * 4;
                int dIdx = dstRow + dstX * 4;
                output[dIdx] = bgra[sIdx];
                output[dIdx + 1] = bgra[sIdx + 1];
                output[dIdx + 2] = bgra[sIdx + 2];
                output[dIdx + 3] = bgra[sIdx + 3];
                srcX++;
                dstX++;
            }
        });

        return output;
    }

    private static byte[] TransposeBgra(byte[] bgra, int width, int height)
    {
        byte[] transposed = new byte[bgra.Length];

        Parallel.For(0, height, y =>
        {
            int row = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int srcIdx = row + x * 4;
                int dstIdx = (x * height + y) * 4;
                transposed[dstIdx] = bgra[srcIdx];
                transposed[dstIdx + 1] = bgra[srcIdx + 1];
                transposed[dstIdx + 2] = bgra[srcIdx + 2];
                transposed[dstIdx + 3] = bgra[srcIdx + 3];
            }
        });

        return transposed;
    }

    #endregion
}
