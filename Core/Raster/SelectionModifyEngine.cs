using System;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Professional Selection Modification Engine matching Studio's exact algorithms:
/// - Select › Modify › Border: Studio's soft band, 2r wide, with falloff 1 - (D - 1) / r
///   using exact Euclidean distance transform (not a hard binary ring).
/// - Select › Modify › Smooth: Sharp thresholding at 0.5 of box-average filter, producing
///   pure 0 and 255 masks without blurry edges.
/// - Select › Modify › Expand & Contract: Morphological dilation and erosion via distance transform.
/// - Select › Modify › Feather: Separable cosine/Gaussian filter with scanline buffer reuse.
/// </summary>
public static class SelectionModifyEngine
{
    /// <summary>
    /// Select › Modify › Border (Calibrated Studio parity):
    /// Produces a soft band of width 2r (r pixels outside, r pixels inside the boundary).
    /// A pixel at Euclidean distance D (center-to-center) from the opposite region is
    /// selected with coverage 1 - (D - 1) / r, clamped to [0..1].
    /// </summary>
    public static byte[] Border(byte[] mask, int width, int height, int radius)
    {
        if (mask == null) throw new ArgumentNullException(nameof(mask));
        if (width <= 0 || height <= 0 || mask.Length != width * height)
            throw new ArgumentException("Invalid mask dimensions.");
        if (radius <= 0) return (byte[])mask.Clone();

        int n = width * height;
        // Compute distance from inside pixels to outside (distance from background)
        // and distance from outside pixels to inside (distance from foreground)
        float[] distToBg = ComputeDistanceTransform(mask, width, height, isForeground: true);
        float[] distToFg = ComputeDistanceTransform(mask, width, height, isForeground: false);

        byte[] result = new byte[n];
        float r = radius;

        Parallel.For(0, height, y =>
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int idx = row + x;
                float d = mask[idx] >= 128 ? distToBg[idx] : distToFg[idx];

                if (d <= r)
                {
                    // Studio formula: 1.0 - (D - 1.0) / r
                    // When D = 1 (directly adjacent across edge), coverage = 1.0 (255)
                    // When D = r, coverage = 1.0 / r
                    // When D > r, coverage = 0.0
                    float coverage = 1.0f - (d - 1.0f) / r;
                    result[idx] = (byte)Math.Clamp((int)Math.Round(coverage * 255.0f), 0, 255);
                }
                else
                {
                    result[idx] = 0;
                }
            }
        });

        return result;
    }

    /// <summary>
    /// Select › Modify › Smooth (Calibrated Studio parity):
    /// Box-averages the selection with radius r, and thresholds strictly at 0.5 (128).
    /// Straight edges stay in place and corners round smoothly, yielding crisp 0 and 255 values.
    /// </summary>
    public static byte[] Smooth(byte[] mask, int width, int height, int radius)
    {
        if (mask == null) throw new ArgumentNullException(nameof(mask));
        if (width <= 0 || height <= 0 || mask.Length != width * height)
            throw new ArgumentException("Invalid mask dimensions.");
        if (radius <= 0) return (byte[])mask.Clone();

        int n = width * height;
        float[] src = new float[n];
        for (int i = 0; i < n; i++)
        {
            src[i] = mask[i] >= 128 ? 1.0f : 0.0f;
        }

        float[] averaged = BoxFilter2D(src, width, height, radius);
        byte[] result = new byte[n];

        // Studio strict threshold at 0.5
        for (int i = 0; i < n; i++)
        {
            result[i] = averaged[i] >= 0.5f ? (byte)255 : (byte)0;
        }

        return result;
    }

    /// <summary>
    /// Select › Modify › Expand:
    /// Morphological dilation by Euclidean distance radius r.
    /// </summary>
    public static byte[] Expand(byte[] mask, int width, int height, int radius)
    {
        if (mask == null) throw new ArgumentNullException(nameof(mask));
        if (width <= 0 || height <= 0 || mask.Length != width * height)
            throw new ArgumentException("Invalid mask dimensions.");
        if (radius <= 0) return (byte[])mask.Clone();

        float[] distToFg = ComputeDistanceTransform(mask, width, height, isForeground: false);
        byte[] result = new byte[width * height];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (mask[i] >= 128 || distToFg[i] <= radius) ? (byte)255 : (byte)0;
        }

        return result;
    }

    /// <summary>
    /// Select › Modify › Contract:
    /// Morphological erosion by Euclidean distance radius r.
    /// </summary>
    public static byte[] Contract(byte[] mask, int width, int height, int radius)
    {
        if (mask == null) throw new ArgumentNullException(nameof(mask));
        if (width <= 0 || height <= 0 || mask.Length != width * height)
            throw new ArgumentException("Invalid mask dimensions.");
        if (radius <= 0) return (byte[])mask.Clone();

        float[] distToBg = ComputeDistanceTransform(mask, width, height, isForeground: true);
        byte[] result = new byte[width * height];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (mask[i] >= 128 && distToBg[i] > radius) ? (byte)255 : (byte)0;
        }

        return result;
    }

    /// <summary>
    /// Select › Modify › Feather:
    /// Separable Gaussian / cosine blur with buffer reuse.
    /// </summary>
    public static byte[] Feather(byte[] mask, int width, int height, double radius)
    {
        if (mask == null) throw new ArgumentNullException(nameof(mask));
        if (width <= 0 || height <= 0 || mask.Length != width * height)
            throw new ArgumentException("Invalid mask dimensions.");
        if (radius <= 0.05) return (byte[])mask.Clone();

        int n = width * height;
        float[] src = new float[n];
        for (int i = 0; i < n; i++)
        {
            src[i] = mask[i] / 255.0f;
        }

        // Gaussian blur via 3-pass box blur approximation (sigma = radius)
        int r = Math.Max(1, (int)Math.Round(radius * 0.75));
        float[] p1 = BoxFilter2D(src, width, height, r);
        float[] p2 = BoxFilter2D(p1, width, height, r);
        float[] p3 = BoxFilter2D(p2, width, height, r);

        byte[] result = new byte[n];
        for (int i = 0; i < n; i++)
        {
            result[i] = (byte)Math.Clamp((int)Math.Round(p3[i] * 255.0f), 0, 255);
        }

        return result;
    }

    // ── Exact Euclidean Distance Transform (Felzenszwalb & Huttenlocher linear-time algorithm) ──

    private static float[] ComputeDistanceTransform(byte[] mask, int w, int h, bool isForeground)
    {
        int n = w * h;
        float[] d = new float[n];
        const float inf = 1e9f;

        // Initialize grid: 0 where condition holds (seed), inf elsewhere
        for (int i = 0; i < n; i++)
        {
            bool match = isForeground ? (mask[i] < 128) : (mask[i] >= 128);
            d[i] = match ? 0.0f : inf;
        }

        // Pass 1: 1D distance transform along columns
        float[] colD = new float[n];
        Parallel.For(0, w, x =>
        {
            int[] v = new int[h];
            float[] z = new float[h + 1];
            int k = 0;
            v[0] = 0;
            z[0] = -inf;
            z[1] = inf;

            for (int q = 1; q < h; q++)
            {
                float fq = d[q * w + x];
                float s = ((fq + q * q) - (d[v[k] * w + x] + v[k] * v[k])) / (2 * q - 2 * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((fq + q * q) - (d[v[k] * w + x] + v[k] * v[k])) / (2 * q - 2 * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = inf;
            }

            k = 0;
            for (int q = 0; q < h; q++)
            {
                while (z[k + 1] < q) k++;
                int vk = v[k];
                float diff = q - vk;
                colD[q * w + x] = diff * diff + d[vk * w + x];
            }
        });

        // Pass 2: 1D distance transform along rows
        float[] dist = new float[n];
        Parallel.For(0, h, y =>
        {
            int row = y * w;
            int[] v = new int[w];
            float[] z = new float[w + 1];
            int k = 0;
            v[0] = 0;
            z[0] = -inf;
            z[1] = inf;

            for (int q = 1; q < w; q++)
            {
                float fq = colD[row + q];
                float s = ((fq + q * q) - (colD[row + v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((fq + q * q) - (colD[row + v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = inf;
            }

            k = 0;
            for (int q = 0; q < w; q++)
            {
                while (z[k + 1] < q) k++;
                int vk = v[k];
                float diff = q - vk;
                float dSq = diff * diff + colD[row + vk];
                dist[row + q] = (float)Math.Sqrt(Math.Max(0.0f, dSq));
            }
        });

        return dist;
    }

    private static float[] BoxFilter2D(float[] src, int w, int h, int r)
    {
        float[] hPass = new float[src.Length];
        float[] outPass = new float[src.Length];

        // Horizontal pass
        Parallel.For(0, h, y =>
        {
            int row = y * w;
            float sum = 0;
            int winSize = 2 * r + 1;

            for (int x = -r; x <= r; x++)
            {
                int cx = Math.Clamp(x, 0, w - 1);
                sum += src[row + cx];
            }

            for (int x = 0; x < w; x++)
            {
                hPass[row + x] = sum / winSize;
                int left = Math.Clamp(x - r, 0, w - 1);
                int right = Math.Clamp(x + r + 1, 0, w - 1);
                sum += src[row + right] - src[row + left];
            }
        });

        // Vertical pass
        Parallel.For(0, w, x =>
        {
            float sum = 0;
            int winSize = 2 * r + 1;

            for (int y = -r; y <= r; y++)
            {
                int cy = Math.Clamp(y, 0, h - 1);
                sum += hPass[cy * w + x];
            }

            for (int y = 0; y < h; y++)
            {
                outPass[y * w + x] = sum / winSize;
                int top = Math.Clamp(y - r, 0, h - 1);
                int bot = Math.Clamp(y + r + 1, 0, h - 1);
                sum += hPass[bot * w + x] - hPass[top * w + x];
            }
        });

        return outPass;
    }
}
