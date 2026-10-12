using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

public static partial class ImageAdjustmentEngine
{
    #region Color To Alpha (Filter > Other > Color to Alpha)

    /// <summary>
    /// Removes a known color key from the pixels, turning it into transparency while keeping
    /// what was mixed with it and decontaminating the color fringe.
    /// </summary>
    public static byte[] ApplyColorToAlpha(
        byte[] bgraPixels,
        Color keyColor,
        double transparencyThreshold = 0.0,
        double opacityThreshold = 1.0)
    {
        byte[] result = new byte[bgraPixels.Length];

        double keyR = keyColor.R / 255.0;
        double keyG = keyColor.G / 255.0;
        double keyB = keyColor.B / 255.0;

        double trans = Math.Clamp(transparencyThreshold, 0.0, 1.0);
        double opac = Math.Clamp(opacityThreshold, 0.0, 1.0);

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            double b = bgraPixels[idx] / 255.0;
            double g = bgraPixels[idx + 1] / 255.0;
            double r = bgraPixels[idx + 2] / 255.0;
            double a = bgraPixels[idx + 3] / 255.0;

            if (a <= 0.0)
            {
                result[idx] = bgraPixels[idx];
                result[idx + 1] = bgraPixels[idx + 1];
                result[idx + 2] = bgraPixels[idx + 2];
                result[idx + 3] = 0;
                return;
            }

            // Calculate required opacity for each channel to reach P from key C
            double ar = r > keyR ? (keyR < 1.0 ? (r - keyR) / (1.0 - keyR) : 1.0) : (r < keyR ? (keyR > 0.0 ? (keyR - r) / keyR : 1.0) : 0.0);
            double ag = g > keyG ? (keyG < 1.0 ? (g - keyG) / (1.0 - keyG) : 1.0) : (g < keyG ? (keyG > 0.0 ? (keyG - g) / keyG : 1.0) : 0.0);
            double ab = b > keyB ? (keyB < 1.0 ? (b - keyB) / (1.0 - keyB) : 1.0) : (b < keyB ? (keyB > 0.0 ? (keyB - b) / keyB : 1.0) : 0.0);

            double alphaRequired = Math.Clamp(Math.Max(ar, Math.Max(ag, ab)), 0.0, 1.0);

            // Remap thresholds
            double remappedAlpha;
            if (alphaRequired <= trans)
            {
                remappedAlpha = 0.0;
            }
            else if (alphaRequired >= opac)
            {
                remappedAlpha = 1.0;
            }
            else
            {
                remappedAlpha = (alphaRequired - trans) / Math.Max(opac - trans, 1e-6);
            }
            remappedAlpha = Math.Clamp(remappedAlpha, 0.0, 1.0);

            // Unmix color
            double outR, outG, outB;
            if (remappedAlpha >= 1.0)
            {
                outR = r;
                outG = g;
                outB = b;
            }
            else if (remappedAlpha <= 0.0)
            {
                outR = keyR;
                outG = keyG;
                outB = keyB;
            }
            else
            {
                outR = Math.Clamp(keyR + (r - keyR) / remappedAlpha, 0.0, 1.0);
                outG = Math.Clamp(keyG + (g - keyG) / remappedAlpha, 0.0, 1.0);
                outB = Math.Clamp(keyB + (b - keyB) / remappedAlpha, 0.0, 1.0);
            }

            double finalA = a * remappedAlpha;

            result[idx] = (byte)Math.Clamp((int)Math.Round(outB * 255.0), 0, 255);
            result[idx + 1] = (byte)Math.Clamp((int)Math.Round(outG * 255.0), 0, 255);
            result[idx + 2] = (byte)Math.Clamp((int)Math.Round(outR * 255.0), 0, 255);
            result[idx + 3] = (byte)Math.Clamp((int)Math.Round(finalA * 255.0), 0, 255);
        });

        return result;
    }

    #endregion

    #region Crop & Straighten Photos (File > Automate > Crop and Straighten Photos)

    /// <summary>
    /// Information about a detected photograph on a flatbed scan.
    /// </summary>
    public readonly record struct ScannedPhotoInfo(
        Point Center,
        double Width,
        double Height,
        double AngleDegrees,
        double AreaFraction)
    {
        public Point[] GetCorners()
        {
            double rad = AngleDegrees * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double hw = Width / 2.0;
            double hh = Height / 2.0;

            Point[] local =
            [
                new(-hw, -hh),
                new(hw, -hh),
                new(hw, hh),
                new(-hw, hh)
            ];

            Point[] res = new Point[4];
            for (int i = 0; i < 4; i++)
            {
                res[i] = new Point(
                    Center.X + cos * local[i].X - sin * local[i].Y,
                    Center.Y + sin * local[i].X + cos * local[i].Y);
            }
            return res;
        }
    }

    /// <summary>
    /// Finds individual rotated photos on a flatbed scan.
    /// </summary>
    public static List<ScannedPhotoInfo> DetectScannedPhotos(byte[] bgraPixels, int width, int height)
    {
        if (width < 8 || height < 8) return [];

        int k = Math.Max(1, (Math.Max(width, height) + 799) / 800);
        int sw = (width + k - 1) / k;
        int sh = (height + k - 1) / k;

        double[] smallR = new double[sw * sh];
        double[] smallG = new double[sw * sh];
        double[] smallB = new double[sw * sh];
        int[] counts = new int[sw * sh];

        for (int y = 0; y < height; y++)
        {
            int sy = y / k;
            for (int x = 0; x < width; x++)
            {
                int sx = x / k;
                int si = sy * sw + sx;
                int pxIdx = (y * width + x) * 4;

                smallB[si] += bgraPixels[pxIdx] / 255.0;
                smallG[si] += bgraPixels[pxIdx + 1] / 255.0;
                smallR[si] += bgraPixels[pxIdx + 2] / 255.0;
                counts[si]++;
            }
        }

        for (int i = 0; i < sw * sh; i++)
        {
            int c = Math.Max(1, counts[i]);
            smallR[i] /= c;
            smallG[i] /= c;
            smallB[i] /= c;
        }

        // Background = median of image border
        List<double> borderR = [];
        List<double> borderG = [];
        List<double> borderB = [];

        for (int x = 0; x < sw; x++)
        {
            int top = x;
            int bot = (sh - 1) * sw + x;
            borderR.Add(smallR[top]); borderR.Add(smallR[bot]);
            borderG.Add(smallG[top]); borderG.Add(smallG[bot]);
            borderB.Add(smallB[top]); borderB.Add(smallB[bot]);
        }
        for (int y = 0; y < sh; y++)
        {
            int left = y * sw;
            int right = y * sw + sw - 1;
            borderR.Add(smallR[left]); borderR.Add(smallR[right]);
            borderG.Add(smallG[left]); borderG.Add(smallG[right]);
            borderB.Add(smallB[left]); borderB.Add(smallB[right]);
        }

        borderR.Sort();
        borderG.Sort();
        borderB.Sort();

        double bgR = borderR[borderR.Count / 2];
        double bgG = borderG[borderG.Count / 2];
        double bgB = borderB[borderB.Count / 2];

        double[] dist = new double[sw * sh];
        double sqrt3 = Math.Sqrt(3.0);
        for (int i = 0; i < sw * sh; i++)
        {
            double dr = smallR[i] - bgR;
            double dg = smallG[i] - bgG;
            double db = smallB[i] - bgB;
            dist[i] = Math.Sqrt(dr * dr + dg * dg + db * db) / sqrt3;
        }

        // Otsu's thresholding
        double threshold = Math.Max(0.04, ComputeOtsuThreshold(dist));
        bool[] mask = new bool[sw * sh];
        for (int i = 0; i < sw * sh; i++)
        {
            mask[i] = dist[i] > threshold;
        }

        // Morphological closing then opening
        int morphR = Math.Max(1, Math.Max(sw, sh) / 200);
        mask = MorphologicalOperation(sw, sh, mask, morphR, dilate: true);
        mask = MorphologicalOperation(sw, sh, mask, morphR, dilate: false);
        mask = MorphologicalOperation(sw, sh, mask, 1, dilate: false);
        mask = MorphologicalOperation(sw, sh, mask, 1, dilate: true);

        // 8-connected components
        int[] labels = new int[sw * sh];
        Array.Fill(labels, -1);
        List<List<int>> components = [];

        for (int s = 0; s < sw * sh; s++)
        {
            if (!mask[s] || labels[s] != -1) continue;

            int componentId = components.Count;
            List<int> compPixels = [];
            Queue<int> queue = new();
            labels[s] = componentId;
            queue.Enqueue(s);

            while (queue.Count > 0)
            {
                int curr = queue.Dequeue();
                compPixels.Add(curr);
                int cx = curr % sw;
                int cy = curr / sw;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= sw || ny >= sh) continue;
                        int nIdx = ny * sw + nx;
                        if (mask[nIdx] && labels[nIdx] == -1)
                        {
                            labels[nIdx] = componentId;
                            queue.Enqueue(nIdx);
                        }
                    }
                }
            }
            components.Add(compPixels);
        }

        double totalArea = sw * sh;
        List<ScannedPhotoInfo> photos = [];

        foreach (var comp in components)
        {
            // Drop tiny noise specks (< 1% total scan area)
            if (comp.Count < totalArea * 0.01) continue;

            // Collect boundary pixels
            List<Point> boundaryPoints = [];
            foreach (int i in comp)
            {
                int px = i % sw;
                int py = i / sw;

                bool isBoundary = px == 0 || py == 0 || px + 1 == sw || py + 1 == sh ||
                                  !mask[i - 1] || !mask[i + 1] || !mask[i - sw] || !mask[i + sw];

                if (isBoundary)
                {
                    double bx = px * k;
                    double by = py * k;
                    double step = k;
                    boundaryPoints.Add(new Point(bx, by));
                    boundaryPoints.Add(new Point(bx + step, by));
                    boundaryPoints.Add(new Point(bx, by + step));
                    boundaryPoints.Add(new Point(bx + step, by + step));
                }
            }

            var hull = ComputeConvexHull(boundaryPoints);
            if (hull.Count < 3) continue;

            var (center, wRect, hRect, angle) = ComputeMinimumAreaRectangle(hull);
            double inset = k;

            photos.Add(new ScannedPhotoInfo(
                center,
                Math.Max(1.0, wRect - 2.0 * inset),
                Math.Max(1.0, hRect - 2.0 * inset),
                angle,
                comp.Count / totalArea));
        }

        // Sort in reading order: bands of a third of smallest photo, then by X
        if (photos.Count > 1)
        {
            double minDim = photos.Min(p => Math.Min(p.Width, p.Height));
            double band = Math.Max(1.0, minDim / 3.0);
            photos.Sort((a, b) =>
            {
                double bandA = Math.Floor(a.Center.Y / band);
                double bandB = Math.Floor(b.Center.Y / band);
                int cmp = bandA.CompareTo(bandB);
                return cmp != 0 ? cmp : a.Center.X.CompareTo(b.Center.X);
            });
        }

        return photos;
    }

    /// <summary>
    /// Crops and straightens a photo candidate into an upright BGRA bitmap buffer.
    /// </summary>
    public static (byte[] Pixels, int Width, int Height) ExtractStraightenedPhoto(
        byte[] bgraPixels,
        int srcWidth,
        int srcHeight,
        ScannedPhotoInfo photo)
    {
        int outW = Math.Max(1, (int)Math.Round(photo.Width));
        int outH = Math.Max(1, (int)Math.Round(photo.Height));
        byte[] outPixels = new byte[outW * outH * 4];

        double rad = photo.AngleDegrees * Math.PI / 180.0;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);

        double cx = photo.Center.X;
        double cy = photo.Center.Y;
        double hw = outW / 2.0;
        double hh = outH / 2.0;

        Parallel.For(0, outH, y =>
        {
            double localY = y - hh + 0.5;
            for (int x = 0; x < outW; x++)
            {
                double localX = x - hw + 0.5;

                double sx = cx + cos * localX - sin * localY;
                double sy = cy + sin * localX + cos * localY;

                SampleBgraBilinear(bgraPixels, srcWidth, srcHeight, sx, sy,
                    out double b, out double g, out double r, out double a);

                int outIdx = (y * outW + x) * 4;
                outPixels[outIdx] = (byte)Math.Clamp((int)Math.Round(b), 0, 255);
                outPixels[outIdx + 1] = (byte)Math.Clamp((int)Math.Round(g), 0, 255);
                outPixels[outIdx + 2] = (byte)Math.Clamp((int)Math.Round(r), 0, 255);
                outPixels[outIdx + 3] = (byte)Math.Clamp((int)Math.Round(a), 0, 255);
            }
        });

        return (outPixels, outW, outH);
    }

    private static double ComputeOtsuThreshold(double[] values)
    {
        const int Bins = 256;
        double[] hist = new double[Bins];
        foreach (double v in values)
        {
            int bin = Math.Clamp((int)(Math.Clamp(v, 0.0, 1.0) * (Bins - 1)), 0, Bins - 1);
            hist[bin]++;
        }

        double total = hist.Sum();
        double sum = 0.0;
        for (int i = 0; i < Bins; i++) sum += i * hist[i];

        double weightB = 0.0;
        double sumB = 0.0;
        double bestVariance = 0.0;
        int bestThreshold = 0;

        for (int i = 0; i < Bins; i++)
        {
            weightB += hist[i];
            if (weightB == 0.0) continue;
            double weightF = total - weightB;
            if (weightF == 0.0) break;

            sumB += i * hist[i];
            double meanB = sumB / weightB;
            double meanF = (sum - sumB) / weightF;
            double between = weightB * weightF * (meanB - meanF) * (meanB - meanF);

            if (between > bestVariance)
            {
                bestVariance = between;
                bestThreshold = i;
            }
        }

        return (bestThreshold + 0.5) / (Bins - 1);
    }

    private static bool[] MorphologicalOperation(int w, int h, bool[] src, int radius, bool dilate)
    {
        bool[] pass1 = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool acc = !dilate;
                for (int d = -radius; d <= radius; d++)
                {
                    int xx = x + d;
                    bool v = xx >= 0 && xx < w && src[y * w + xx];
                    if (dilate) acc |= v; else acc &= v;
                }
                pass1[y * w + x] = acc;
            }
        }

        bool[] pass2 = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool acc = !dilate;
                for (int d = -radius; d <= radius; d++)
                {
                    int yy = y + d;
                    bool v = yy >= 0 && yy < h && pass1[yy * w + x];
                    if (dilate) acc |= v; else acc &= v;
                }
                pass2[y * w + x] = acc;
            }
        }

        return pass2;
    }

    private static List<Point> ComputeConvexHull(List<Point> points)
    {
        var pts = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (pts.Count < 3) return pts;

        static double Cross(Point o, Point a, Point b) =>
            (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        List<Point> lower = [];
        foreach (var p in pts)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0.0)
                lower.RemoveAt(lower.Count - 1);
            lower.Add(p);
        }

        List<Point> upper = [];
        for (int i = pts.Count - 1; i >= 0; i--)
        {
            var p = pts[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0.0)
                upper.RemoveAt(upper.Count - 1);
            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static (Point Center, double Width, double Height, double Angle) ComputeMinimumAreaRectangle(List<Point> hull)
    {
        double bestArea = double.MaxValue;
        Point bestCenter = default;
        double bestW = 0.0;
        double bestH = 0.0;
        double bestAngle = 0.0;

        int n = hull.Count;
        for (int i = 0; i < n; i++)
        {
            var a = hull[i];
            var b = hull[(i + 1) % n];

            double th = Math.Atan2(b.Y - a.Y, b.X - a.X);
            double cos = Math.Cos(th);
            double sin = Math.Sin(th);

            double x0 = double.MaxValue, y0 = double.MaxValue;
            double x1 = double.MinValue, y1 = double.MinValue;

            foreach (var p in hull)
            {
                double u = cos * p.X + sin * p.Y;
                double v = -sin * p.X + cos * p.Y;
                x0 = Math.Min(x0, u);
                x1 = Math.Max(x1, u);
                y0 = Math.Min(y0, v);
                y1 = Math.Max(y1, v);
            }

            double area = (x1 - x0) * (y1 - y0);
            if (area < bestArea)
            {
                bestArea = area;
                double cu = (x0 + x1) / 2.0;
                double cv = (y0 + y1) / 2.0;
                bestCenter = new Point(cos * cu - sin * cv, sin * cu + cos * cv);
                bestW = x1 - x0;
                bestH = y1 - y0;
                bestAngle = th * 180.0 / Math.PI;
            }
        }

        // Normalize to -45..45 degrees, swapping dimensions on 90 degree turns
        while (bestAngle > 45.0)
        {
            bestAngle -= 90.0;
            (bestW, bestH) = (bestH, bestW);
        }
        while (bestAngle < -45.0)
        {
            bestAngle += 90.0;
            (bestW, bestH) = (bestH, bestW);
        }

        return (bestCenter, bestW, bestH, bestAngle);
    }

    #endregion
}
