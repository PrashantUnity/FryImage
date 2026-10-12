using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Professional studio Magic Wand Selection Engine.
/// Performs flood-fill (contiguous) and global (non-contiguous) color tolerance segmentation
/// with optional boundary anti-aliasing.
/// </summary>
public static class MagicWandEngine
{
    /// <summary>
    /// Computes an 8-bit selection mask (0 = unselected, 255 = selected) starting from seed point.
    /// </summary>
    public static byte[] ComputeSelectionMask(
        SKBitmap bitmap,
        int seedX,
        int seedY,
        double tolerance,
        bool contiguous = true,
        bool antiAlias = true)
    {
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

        int width = bitmap.Width;
        int height = bitmap.Height;
        byte[] mask = new byte[width * height];

        if (seedX < 0 || seedX >= width || seedY < 0 || seedY >= height)
            return mask;

        var pixels = bitmap.Pixels;
        var seedColor = pixels[seedY * width + seedX];
        byte targetR = seedColor.Red;
        byte targetG = seedColor.Green;
        byte targetB = seedColor.Blue;
        byte targetA = seedColor.Alpha;

        double tolSq = tolerance * tolerance * 3.0;

        if (!contiguous)
        {
            // Global color selection
            Parallel.For(0, height, y =>
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    var c = pixels[row + x];
                    double dr = c.Red - targetR;
                    double dg = c.Green - targetG;
                    double db = c.Blue - targetB;
                    double distSq = dr * dr + dg * dg + db * db;

                    if (distSq <= tolSq)
                    {
                        mask[row + x] = 255;
                    }
                }
            });
        }
        else
        {
            // Contiguous 4-connected BFS flood fill
            var visited = new bool[width * height];
            var queue = new Queue<int>();

            int startIdx = seedY * width + seedX;
            queue.Enqueue(startIdx);
            visited[startIdx] = true;
            mask[startIdx] = 255;

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                int curr = queue.Dequeue();
                int cx = curr % width;
                int cy = curr / width;

                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i];
                    int ny = cy + dy[i];

                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                    int nIdx = ny * width + nx;
                    if (visited[nIdx]) continue;
                    visited[nIdx] = true;

                    var c = pixels[nIdx];
                    double dr = c.Red - targetR;
                    double dg = c.Green - targetG;
                    double db = c.Blue - targetB;
                    double distSq = dr * dr + dg * dg + db * db;

                    if (distSq <= tolSq)
                    {
                        mask[nIdx] = 255;
                        queue.Enqueue(nIdx);
                    }
                }
            }
        }

        if (antiAlias)
        {
            ApplyAntiAliasing(mask, width, height);
        }

        return mask;
    }

    /// <summary>
    /// Smooths mask transition boundaries using 3x3 local box filter.
    /// </summary>
    private static void ApplyAntiAliasing(byte[] mask, int width, int height)
    {
        byte[] copy = (byte[])mask.Clone();

        for (int y = 1; y < height - 1; y++)
        {
            int row = y * width;
            int prevRow = (y - 1) * width;
            int nextRow = (y + 1) * width;

            for (int x = 1; x < width - 1; x++)
            {
                byte val = copy[row + x];
                if (val == 0 || val == 255)
                {
                    // Check if adjacent to an edge
                    int sum = copy[prevRow + x - 1] + copy[prevRow + x] + copy[prevRow + x + 1] +
                              copy[row + x - 1]     + copy[row + x]     + copy[row + x + 1] +
                              copy[nextRow + x - 1] + copy[nextRow + x] + copy[nextRow + x + 1];

                    if (sum > 0 && sum < 255 * 9)
                    {
                        mask[row + x] = (byte)(sum / 9);
                    }
                }
            }
        }
    }
}
