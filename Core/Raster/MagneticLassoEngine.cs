using System;
using System.Collections.Generic;
using Avalonia;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Intelligent Scissors / Magnetic Lasso Engine (Mortensen &amp; Barrett, SIGGRAPH 1995).
/// Snaps selection boundaries to high-contrast edges using Sobel gradient cost maps and
/// Dijkstra shortest-path live-wire tracking.
/// </summary>
public static class MagneticLassoEngine
{
    /// <summary>
    /// Computes local edge cost map from an SKBitmap using Sobel gradient magnitude.
    /// Edge pixels have cost approaching 0.0, flat uniform pixels have cost 1.0.
    /// </summary>
    public static float[] ComputeEdgeCostMap(SKBitmap bitmap)
    {
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

        int width = bitmap.Width;
        int height = bitmap.Height;
        var costs = new float[width * height];
        var pixels = bitmap.Pixels;

        // Compute grayscale intensities
        var gray = new byte[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            gray[i] = (byte)((c.Red * 77 + c.Green * 150 + c.Blue * 29) >> 8);
        }

        float maxGrad = 1.0f;
        var grad = new float[width * height];

        // 3x3 Sobel operator
        for (int y = 1; y < height - 1; y++)
        {
            int row = y * width;
            int prevRow = (y - 1) * width;
            int nextRow = (y + 1) * width;

            for (int x = 1; x < width - 1; x++)
            {
                // Sobel horizontal Gx
                int gx = -gray[prevRow + x - 1] + gray[prevRow + x + 1]
                         - 2 * gray[row + x - 1] + 2 * gray[row + x + 1]
                         - gray[nextRow + x - 1] + gray[nextRow + x + 1];

                // Sobel vertical Gy
                int gy = -gray[prevRow + x - 1] - 2 * gray[prevRow + x] - gray[prevRow + x + 1]
                         + gray[nextRow + x - 1] + 2 * gray[nextRow + x] + gray[nextRow + x + 1];

                float mag = (float)Math.Sqrt(gx * gx + gy * gy);
                grad[row + x] = mag;
                if (mag > maxGrad) maxGrad = mag;
            }
        }

        // Invert so strongest edges have lowest cost
        for (int i = 0; i < costs.Length; i++)
        {
            costs[i] = 1.0f - (grad[i] / maxGrad);
        }

        return costs;
    }

    /// <summary>
    /// Finds the optimal "live-wire" path between an anchor and current cursor using Dijkstra search.
    /// </summary>
    public static List<Point> FindLiveWirePath(
        float[] costs,
        int width,
        int height,
        Point startPt,
        Point endPt,
        int searchRadius = 160)
    {
        var path = new List<Point>();

        int sx = Math.Clamp((int)Math.Round(startPt.X), 0, width - 1);
        int sy = Math.Clamp((int)Math.Round(startPt.Y), 0, height - 1);
        int ex = Math.Clamp((int)Math.Round(endPt.X), 0, width - 1);
        int ey = Math.Clamp((int)Math.Round(endPt.Y), 0, height - 1);

        if (sx == ex && sy == ey)
        {
            path.Add(new Point(sx, sy));
            return path;
        }

        // Search bounding box
        int minX = Math.Max(0, Math.Min(sx, ex) - searchRadius);
        int maxX = Math.Min(width - 1, Math.Max(sx, ex) + searchRadius);
        int minY = Math.Max(0, Math.Min(sy, ey) - searchRadius);
        int maxY = Math.Min(height - 1, Math.Max(sy, ey) + searchRadius);

        int bbWidth = maxX - minX + 1;
        int bbHeight = maxY - minY + 1;
        int bbTotal = bbWidth * bbHeight;

        var dist = new float[bbTotal];
        var parent = new int[bbTotal];
        Array.Fill(dist, float.MaxValue);
        Array.Fill(parent, -1);

        int startIdx = (sy - minY) * bbWidth + (sx - minX);
        dist[startIdx] = 0;

        var pq = new PriorityQueue<int, float>();
        pq.Enqueue(startIdx, 0);

        int targetIdx = (ey - minY) * bbWidth + (ex - minX);

        // 8-connected neighbors
        int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };
        float sqrt2 = (float)Math.Sqrt(2);
        float[] stepCost = { 1.0f, sqrt2, 1.0f, sqrt2, 1.0f, sqrt2, 1.0f, sqrt2 };

        while (pq.Count > 0)
        {
            int u = pq.Dequeue();
            if (u == targetIdx) break;

            int ux = (u % bbWidth) + minX;
            int uy = (u / bbWidth) + minY;
            float d = dist[u];

            for (int k = 0; k < 8; k++)
            {
                int nx = ux + dx[k];
                int ny = uy + dy[k];

                if (nx < minX || nx > maxX || ny < minY || ny > maxY) continue;

                int v = (ny - minY) * bbWidth + (nx - minX);
                float edgeCost = costs[ny * width + nx];
                float newDist = d + (edgeCost * stepCost[k]);

                if (newDist < dist[v])
                {
                    dist[v] = newDist;
                    parent[v] = u;
                    pq.Enqueue(v, newDist);
                }
            }
        }

        // Backtrack from end to start
        int curr = targetIdx;
        while (curr != -1)
        {
            int px = (curr % bbWidth) + minX;
            int py = (curr / bbWidth) + minY;
            path.Add(new Point(px, py));
            if (curr == startIdx) break;
            curr = parent[curr];
        }

        path.Reverse();
        return path;
    }
}
