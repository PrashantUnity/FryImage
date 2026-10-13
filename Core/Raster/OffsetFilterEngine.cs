using System;
using Avalonia.Media;
using SkiaSharp;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Offset filter engine supporting signed coordinate limits and Euclidean modulo wrapping (#4738).
/// Shifts bitmap content by whole pixels with Wrap Around, Repeat Edge, or Transparent/Background undefined areas.
/// </summary>
public static class OffsetFilterEngine
{
    public enum UndefinedAreaMode
    {
        Wrap = 0,
        WrapAround = 0,
        Repeat = 1,
        RepeatEdgePixels = 1,
        Transparent = 2,
        SetToTransparent = 2,
        Background = 3,
        SetToBackground = 3
    }

    /// <summary>
    /// Euclidean modulo remainder that maps any signed integer (including negative or extreme limits)
    /// into [0, size - 1].
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int RemEuclid(long value, int size)
    {
        if (size <= 0) return 0;
        long rem = value % size;
        return (int)(rem < 0 ? rem + size : rem);
    }

    /// <summary>
    /// Maps a 1D pixel position under an offset shift using Euclidean bounds arithmetic (#4738).
    /// Prevents arithmetic overflow with extreme signed 32-bit values.
    /// </summary>
    public static int? MapCoordinate(int position, int distance, int start, int end, UndefinedAreaMode mode)
    {
        if (start >= end) return null;
        int size = end - start;
        long p = (long)position - (long)distance;

        return mode switch
        {
            UndefinedAreaMode.Wrap => start + RemEuclid(p - start, size),
            UndefinedAreaMode.Repeat => (int)Math.Clamp(p, start, end - 1),
            _ => (p >= start && p < end) ? (int)p : null
        };
    }

    /// <summary>
    /// Applies the Offset filter to raw BGRA byte buffer.
    /// </summary>
    public static byte[] ApplyOffset(
        byte[] bgra, int width, int height,
        int dx, int dy,
        UndefinedAreaMode mode,
        Color backgroundColor = default)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
            return bgra;

        var dst = new byte[bgra.Length];
        byte bgB = backgroundColor.B;
        byte bgG = backgroundColor.G;
        byte bgR = backgroundColor.R;
        byte bgA = mode is UndefinedAreaMode.Transparent ? (byte)0 : (backgroundColor.A == 0 ? (byte)255 : backgroundColor.A);

        for (int y = 0; y < height; y++)
        {
            int? sy = MapCoordinate(y, dy, 0, height, mode);
            for (int x = 0; x < width; x++)
            {
                int dstIdx = (y * width + x) * 4;
                int? sx = MapCoordinate(x, dx, 0, width, mode);

                if (sx.HasValue && sy.HasValue)
                {
                    int srcIdx = (sy.Value * width + sx.Value) * 4;
                    dst[dstIdx + 0] = bgra[srcIdx + 0];
                    dst[dstIdx + 1] = bgra[srcIdx + 1];
                    dst[dstIdx + 2] = bgra[srcIdx + 2];
                    dst[dstIdx + 3] = bgra[srcIdx + 3];
                }
                else
                {
                    dst[dstIdx + 0] = bgB;
                    dst[dstIdx + 1] = bgG;
                    dst[dstIdx + 2] = bgR;
                    dst[dstIdx + 3] = bgA;
                }
            }
        }

        return dst;
    }

    /// <summary>
    /// Applies the Offset filter to an SKBitmap.
    /// </summary>
    public static SKBitmap ApplyOffset(SKBitmap src, int dx, int dy, UndefinedAreaMode mode, SKColor? background = null)
    {
        int w = src.Width;
        int h = src.Height;
        var dst = new SKBitmap(w, h, src.ColorType, src.AlphaType);
        var bg = background ?? SKColors.Transparent;

        for (int y = 0; y < h; y++)
        {
            int? sy = MapCoordinate(y, dy, 0, h, mode);
            for (int x = 0; x < w; x++)
            {
                int? sx = MapCoordinate(x, dx, 0, w, mode);
                if (sx.HasValue && sy.HasValue)
                {
                    dst.SetPixel(x, y, src.GetPixel(sx.Value, sy.Value));
                }
                else
                {
                    dst.SetPixel(x, y, bg);
                }
            }
        }

        return dst;
    }
}
