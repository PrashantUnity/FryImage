using System;
using System.Collections.Generic;
using Avalonia;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Classical sky segmentation engine ported from photocraft-algo (Select › Sky).
/// Implements colour prior (blue sky, overcast/clouds, sunset glow), gradient smoothness,
/// 4-connected region growing from the top edge, and studio backdrop rejection (#4638).
/// </summary>
public static class SkySegmentationEngine
{
    public sealed class SkyParams
    {
        public float Threshold { get; set; } = 50.0f; // 0..100
        public float Softness { get; set; } = 50.0f;  // 0..100
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float Smooth(float e0, float e1, float x)
    {
        float t = Math.Clamp((x - e0) / (e1 - e0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static float Luma(float r, float g, float b) => 0.299f * r + 0.587f * g + 0.114f * b;

    /// <summary>
    /// Computes how sky-like an isolated color is on its own (0.0 .. 1.0).
    /// </summary>
    public static float ColourPrior(float r, float g, float b)
    {
        float y = Luma(r, g, b);
        float mx = Math.Max(r, Math.Max(g, b));
        float mn = Math.Min(r, Math.Min(g, b));
        float sat = mx > 1e-4f ? (mx - mn) / mx : 0.0f;

        // Blue sky: blue dominates red clearly and isn't far below green
        float blue = Smooth(0.02f, 0.12f, b - r) * Smooth(-0.12f, 0.0f, b - g) * Smooth(0.18f, 0.4f, y);

        // Overcast / clouds / haze: bright and nearly neutral
        float white = Smooth(0.5f, 0.75f, y) * (1.0f - Smooth(0.12f, 0.3f, sat));

        // Sunset glow: bright and warm
        float warm = 0.6f * Smooth(0.55f, 0.8f, y) * Smooth(0.0f, 0.1f, r - b) * (1.0f - Smooth(0.05f, 0.25f, g - b - 0.2f));

        return Math.Max(blue, Math.Max(white, warm));
    }

    /// <summary>
    /// Determines whether a segmented region that touches the bottom rows is actually a studio backdrop.
    /// Port of upstream #4638: a neutral region (mean saturation &lt; 0.1) covering &gt;= 60% of the
    /// bottom rows is a studio backdrop, not an overcast sky.
    /// </summary>
    public static bool IsBackdrop(float[] rImg, float[] gImg, float[] bImg, bool[] mask, int w, int h, int rows)
    {
        int checkRows = Math.Min(rows, h);
        int startIdx = (h - checkRows) * w;
        int totalBottom = checkRows * w;
        if (totalBottom <= 0 || startIdx < 0 || startIdx >= mask.Length) return false;

        int covered = 0;
        for (int i = startIdx; i < mask.Length; i++)
        {
            if (mask[i]) covered++;
        }

        // Must cover at least 60% of the bottom rows
        if (covered * 10 < totalBottom * 6) return false;

        // Check mean saturation of the masked region
        float satSum = 0.0f;
        int n = 0;
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i])
            {
                float r = rImg[i];
                float g = gImg[i];
                float b = bImg[i];
                float mx = Math.Max(r, Math.Max(g, b));
                float mn = Math.Min(r, Math.Min(g, b));
                float sat = mx > 1e-4f ? (mx - mn) / mx : 0.0f;
                satSum += sat;
                n++;
            }
        }

        return n > 0 && (satSum / n) < 0.1f;
    }

    /// <summary>
    /// Computes a binary sky mask from an image buffer (normalized 0..1 float RGB arrays).
    /// Returns null if no sky touches the top of the image or if it is identified as a studio backdrop.
    /// </summary>
    public static bool[]? SegmentSky(float[] rImg, float[] gImg, float[] bImg, int w, int h, SkyParams? parameters = null)
    {
        if (w < 2 || h < 2) return null;

        var p = parameters ?? new SkyParams();
        int totalPixels = w * h;

        // 1. Compute likelihood (colour prior * smoothness)
        float[] like = new float[totalPixels];
        for (int i = 0; i < totalPixels; i++)
        {
            like[i] = ColourPrior(rImg[i], gImg[i], bImg[i]);
        }

        float threshold = 0.12f + 0.6f * (Math.Clamp(p.Threshold, 0.0f, 100.0f) / 100.0f);

        // 2. Seeds: likely sky in the top rows
        int topRows = Math.Max(1, h / 40);
        bool[] mask = new bool[totalPixels];
        var stack = new Stack<int>();

        for (int i = 0; i < topRows * w; i++)
        {
            if (like[i] > threshold)
            {
                mask[i] = true;
                stack.Push(i);
            }
        }

        // Less than 5% of top edge looks like sky
        if (stack.Count * 20 < topRows * w) return null;

        // 3. Region growing with gradual color change constraint
        const float maxStep = 0.12f;
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int x = i % w;
            int y = i / w;

            int[] neighbours = [
                x > 0 ? i - 1 : -1,
                x + 1 < w ? i + 1 : -1,
                y > 0 ? i - w : -1,
                y + 1 < h ? i + w : -1
            ];

            foreach (int j in neighbours)
            {
                if (j < 0 || mask[j] || like[j] <= threshold) continue;

                float dr = rImg[i] - rImg[j];
                float dg = gImg[i] - gImg[j];
                float db = bImg[i] - bImg[j];
                float d = MathF.Sqrt(dr * dr + dg * dg + db * db);

                if (d < maxStep)
                {
                    mask[j] = true;
                    stack.Push(j);
                }
            }
        }

        // 4. Studio backdrop rejection test (#4638)
        if (IsBackdrop(rImg, gImg, bImg, mask, w, h, topRows))
        {
            return null;
        }

        bool any = false;
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i]) { any = true; break; }
        }

        return any ? mask : null;
    }

    /// <summary>
    /// Generates an 8-bit grayscale selection mask (0 or 255) from an SKBitmap.
    /// </summary>
    public static byte[]? CreateSkySelectionMask(SKBitmap bitmap, SkyParams? parameters = null)
    {
        int w = bitmap.Width;
        int h = bitmap.Height;
        int total = w * h;

        float[] r = new float[total];
        float[] g = new float[total];
        float[] b = new float[total];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var color = bitmap.GetPixel(x, y);
                int idx = y * w + x;
                r[idx] = color.Red / 255.0f;
                g[idx] = color.Green / 255.0f;
                b[idx] = color.Blue / 255.0f;
            }
        }

        var boolMask = SegmentSky(r, g, b, w, h, parameters);
        if (boolMask == null) return null;

        byte[] byteMask = new byte[total];
        for (int i = 0; i < total; i++)
        {
            byteMask[i] = boolMask[i] ? (byte)255 : (byte)0;
        }

        return byteMask;
    }
}
