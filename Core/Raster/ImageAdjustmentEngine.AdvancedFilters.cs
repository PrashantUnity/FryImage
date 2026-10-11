using System;
using System.Threading.Tasks;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

/// <summary>
/// Advanced creative and optical filters for FryImage:
/// - Tilt-Shift (depth-of-field lens tilt with linear focus plane and smoothstep transition)
/// - Iris Blur (elliptical focal plane with feathering)
/// - Oil Paint (structure tensor flow-line integral convolution with bristle relief lighting)
/// - Facet (Kuwahara edge-preserving painterly quadrant filter)
/// - Wave Distortion (multi-generator sinusoidal, triangular, and square warp)
/// - Ripple Distortion (periodic wave ripple)
/// - Pinch Distortion (radial spherical power contraction/expansion)
/// - Polar Coordinates (Rectangular ↔ Polar domain transformation)
/// - Color Halftone (CMYK-angled screen dot grid simulation)
/// - Crystallize & Pointillize (Voronoi cell mosaic partitioning)
/// - Camera Raw Filter (Full linear-light develop pipeline)
/// - Lens Flare (optical burst, glow rings, and chromatic ghost discs)
/// - Clouds & Difference Clouds (procedural fractal turbulence noise)
/// - Stylize: Find Edges & Solarize
/// </summary>
public static partial class ImageAdjustmentEngine
{
    #region Sampling & Noise Helpers

    private static void SampleBgraBilinear(byte[] bgra, int width, int height, double x, double y, out double b, out double g, out double r, out double a)
    {
        double cx = Math.Clamp(x, 0.0, width - 1.0001);
        double cy = Math.Clamp(y, 0.0, height - 1.0001);

        int x0 = (int)Math.Floor(cx);
        int y0 = (int)Math.Floor(cy);
        int x1 = Math.Min(x0 + 1, width - 1);
        int y1 = Math.Min(y0 + 1, height - 1);

        double fx = cx - x0;
        double fy = cy - y0;
        double w00 = (1.0 - fx) * (1.0 - fy);
        double w10 = fx * (1.0 - fy);
        double w01 = (1.0 - fx) * fy;
        double w11 = fx * fy;

        int idx00 = (y0 * width + x0) * 4;
        int idx10 = (y0 * width + x1) * 4;
        int idx01 = (y1 * width + x0) * 4;
        int idx11 = (y1 * width + x1) * 4;

        b = bgra[idx00] * w00 + bgra[idx10] * w10 + bgra[idx01] * w01 + bgra[idx11] * w11;
        g = bgra[idx00 + 1] * w00 + bgra[idx10 + 1] * w10 + bgra[idx01 + 1] * w01 + bgra[idx11 + 1] * w11;
        r = bgra[idx00 + 2] * w00 + bgra[idx10 + 2] * w10 + bgra[idx01 + 2] * w01 + bgra[idx11 + 2] * w11;
        a = bgra[idx00 + 3] * w00 + bgra[idx10 + 3] * w10 + bgra[idx01 + 3] * w01 + bgra[idx11 + 3] * w11;
    }

    private static double Hash01(int x, int y, int z, uint seed)
    {
        uint h = seed ^ (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0x00FFFFFF) / (double)0x01000000;
    }

    private static double ValueNoise2D(double x, double y, uint seed)
    {
        int ix = (int)Math.Floor(x);
        int iy = (int)Math.Floor(y);
        double tx = x - ix;
        double ty = y - iy;

        double sx = tx * tx * (3.0 - 2.0 * tx);
        double sy = ty * ty * (3.0 - 2.0 * ty);

        double h00 = Hash01(ix, iy, 0, seed);
        double h10 = Hash01(ix + 1, iy, 0, seed);
        double h01 = Hash01(ix, iy + 1, 0, seed);
        double h11 = Hash01(ix + 1, iy + 1, 0, seed);

        double top = h00 + (h10 - h00) * sx;
        double bot = h01 + (h11 - h01) * sx;
        return top + (bot - top) * sy;
    }

    private static double FractalTurbulence(double x, double y, int octaves, uint seed)
    {
        double sum = 0.0;
        double amp = 1.0;
        double maxAmp = 0.0;
        double freq = 1.0;

        for (int o = 0; o < octaves; o++)
        {
            sum += ValueNoise2D(x * freq, y * freq, seed + (uint)(o * 997)) * amp;
            maxAmp += amp;
            amp *= 0.5;
            freq *= 2.0;
        }

        return sum / maxAmp;
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }

    #endregion

    #region 1. Tilt-Shift Filter

    /// <summary>
    /// Applies Tilt-Shift Blur with linear depth of field plane, focus width, and smoothstep falloff.
    /// </summary>
    public static byte[] ApplyTiltShift(
        byte[] bgraPixels,
        int width,
        int height,
        double blurRadius,
        double centerX = 0.5,
        double centerY = 0.5,
        double angleDegrees = 0.0,
        double focus = 0.15,
        double transition = 0.25)
    {
        if (width <= 0 || height <= 0 || blurRadius <= 0.1) return (byte[])bgraPixels.Clone();

        double ss = Math.Min(width, height);
        double cx = centerX * width;
        double cy = centerY * height;

        double rad = angleDegrees * Math.PI / 180.0;
        double nx = Math.Sin(rad);
        double ny = Math.Cos(rad);

        double f0 = Math.Max(0.0, focus);
        double f1 = f0 + Math.Max(0.001, transition);

        // Compute blurred buffers at multiple radii for smooth progressive variable blur
        byte[] blurredQuarter = ApplyGaussianBlur(bgraPixels, width, height, blurRadius * 0.25);
        byte[] blurredHalf = ApplyGaussianBlur(bgraPixels, width, height, blurRadius * 0.5);
        byte[] blurredFull = ApplyGaussianBlur(bgraPixels, width, height, blurRadius);

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                // Normalized perpendicular distance from tilt-shift central band
                double d = Math.Abs((x - cx) * nx + (y - cy) * ny) / ss;

                // Smoothstep falloff
                double t = Math.Clamp((d - f0) / (f1 - f0), 0.0, 1.0);
                double blurFactor = t * t * (3.0 - 2.0 * t); // 0 = sharp, 1 = maximum blur

                if (blurFactor <= 0.001)
                {
                    result[idx] = bgraPixels[idx];
                    result[idx + 1] = bgraPixels[idx + 1];
                    result[idx + 2] = bgraPixels[idx + 2];
                    result[idx + 3] = bgraPixels[idx + 3];
                }
                else if (blurFactor < 0.33)
                {
                    double w = blurFactor / 0.33;
                    result[idx] = (byte)Math.Round(bgraPixels[idx] * (1.0 - w) + blurredQuarter[idx] * w);
                    result[idx + 1] = (byte)Math.Round(bgraPixels[idx + 1] * (1.0 - w) + blurredQuarter[idx + 1] * w);
                    result[idx + 2] = (byte)Math.Round(bgraPixels[idx + 2] * (1.0 - w) + blurredQuarter[idx + 2] * w);
                    result[idx + 3] = bgraPixels[idx + 3];
                }
                else if (blurFactor < 0.66)
                {
                    double w = (blurFactor - 0.33) / 0.33;
                    result[idx] = (byte)Math.Round(blurredQuarter[idx] * (1.0 - w) + blurredHalf[idx] * w);
                    result[idx + 1] = (byte)Math.Round(blurredQuarter[idx + 1] * (1.0 - w) + blurredHalf[idx + 1] * w);
                    result[idx + 2] = (byte)Math.Round(blurredQuarter[idx + 2] * (1.0 - w) + blurredHalf[idx + 2] * w);
                    result[idx + 3] = bgraPixels[idx + 3];
                }
                else
                {
                    double w = (blurFactor - 0.66) / 0.34;
                    result[idx] = (byte)Math.Round(blurredHalf[idx] * (1.0 - w) + blurredFull[idx] * w);
                    result[idx + 1] = (byte)Math.Round(blurredHalf[idx + 1] * (1.0 - w) + blurredFull[idx + 1] * w);
                    result[idx + 2] = (byte)Math.Round(blurredHalf[idx + 2] * (1.0 - w) + blurredFull[idx + 2] * w);
                    result[idx + 3] = bgraPixels[idx + 3];
                }
            }
        });

        return result;
    }

    #endregion

    #region 2. Iris Blur Filter

    /// <summary>
    /// Applies Iris Blur with an elliptical focal zone, rotation angle, inner focus radius, and outer feather falloff.
    /// </summary>
    public static byte[] ApplyIrisBlur(
        byte[] bgraPixels,
        int width,
        int height,
        double blurRadius,
        double centerX = 0.5,
        double centerY = 0.5,
        double radiusX = 0.35,
        double radiusY = 0.25,
        double angleDegrees = 0.0,
        double feather = 0.4)
    {
        if (width <= 0 || height <= 0 || blurRadius <= 0.1) return (byte[])bgraPixels.Clone();

        double ss = Math.Min(width, height);
        double cx = centerX * width;
        double cy = centerY * height;
        double rx = Math.Max(0.01, radiusX) * ss;
        double ry = Math.Max(0.01, radiusY) * ss;

        double rad = angleDegrees * Math.PI / 180.0;
        double cosA = Math.Cos(rad);
        double sinA = Math.Sin(rad);

        double innerThreshold = Math.Max(0.0, 1.0 - feather);

        byte[] blurredQuarter = ApplyGaussianBlur(bgraPixels, width, height, blurRadius * 0.25);
        byte[] blurredHalf = ApplyGaussianBlur(bgraPixels, width, height, blurRadius * 0.5);
        byte[] blurredFull = ApplyGaussianBlur(bgraPixels, width, height, blurRadius);

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                double dx = x - cx;
                double dy = y - cy;

                // Rotate to ellipse orientation
                double u = (dx * cosA + dy * sinA) / rx;
                double v = (-dx * sinA + dy * cosA) / ry;
                double eDist = Math.Sqrt(u * u + v * v);

                double blurFactor = 0.0;
                if (eDist > innerThreshold)
                {
                    double t = Math.Clamp((eDist - innerThreshold) / (1.0 - innerThreshold), 0.0, 1.0);
                    blurFactor = t * t * (3.0 - 2.0 * t);
                }

                if (blurFactor <= 0.001)
                {
                    result[idx] = bgraPixels[idx];
                    result[idx + 1] = bgraPixels[idx + 1];
                    result[idx + 2] = bgraPixels[idx + 2];
                    result[idx + 3] = bgraPixels[idx + 3];
                }
                else if (blurFactor < 0.5)
                {
                    double w = blurFactor * 2.0;
                    result[idx] = (byte)Math.Round(bgraPixels[idx] * (1.0 - w) + blurredHalf[idx] * w);
                    result[idx + 1] = (byte)Math.Round(bgraPixels[idx + 1] * (1.0 - w) + blurredHalf[idx + 1] * w);
                    result[idx + 2] = (byte)Math.Round(bgraPixels[idx + 2] * (1.0 - w) + blurredHalf[idx + 2] * w);
                    result[idx + 3] = bgraPixels[idx + 3];
                }
                else
                {
                    double w = (blurFactor - 0.5) * 2.0;
                    result[idx] = (byte)Math.Round(blurredHalf[idx] * (1.0 - w) + blurredFull[idx] * w);
                    result[idx + 1] = (byte)Math.Round(blurredHalf[idx + 1] * (1.0 - w) + blurredFull[idx + 1] * w);
                    result[idx + 2] = (byte)Math.Round(blurredHalf[idx + 2] * (1.0 - w) + blurredFull[idx + 2] * w);
                    result[idx + 3] = bgraPixels[idx + 3];
                }
            }
        });

        return result;
    }

    #endregion

    #region 3. Oil Paint Filter

    /// <summary>
    /// Applies Oil Paint simulation using flow-guided Line Integral Convolution (LIC)
    /// across the structure tensor field, plus bristle relief directional shading.
    /// </summary>
    public static byte[] ApplyOilPaint(
        byte[] bgraPixels,
        int width,
        int height,
        double stylization = 4.0,
        double cleanliness = 2.0,
        double scale = 1.0,
        double bristleDetail = 5.0,
        bool lighting = true,
        double angleDegrees = 45.0,
        double shine = 0.5)
    {
        if (width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        // 1. Pre-smooth colours based on cleanliness
        double cleanSigma = Math.Clamp(cleanliness, 0.0, 10.0) * 0.25;
        byte[] preSmoothed = cleanSigma > 0.05 ? ApplyGaussianBlur(bgraPixels, width, height, cleanSigma) : bgraPixels;

        // 2. Compute luminance intensity array
        float[] lum = new float[width * height];
        for (int i = 0; i < width * height; i++)
        {
            int idx = i * 4;
            lum[i] = (float)(preSmoothed[idx] * LumaB + preSmoothed[idx + 1] * LumaG + preSmoothed[idx + 2] * LumaR) / 255.0f;
        }

        // 3. Compute structure tensor (gx^2, gx*gy, gy^2)
        float[] efg = new float[width * height * 3];
        Parallel.For(1, height - 1, y =>
        {
            int row = y * width;
            for (int x = 1; x < width - 1; x++)
            {
                float l_p1_m1 = lum[(y - 1) * width + (x + 1)];
                float l_p1_0 = lum[y * width + (x + 1)];
                float l_p1_p1 = lum[(y + 1) * width + (x + 1)];

                float l_m1_m1 = lum[(y - 1) * width + (x - 1)];
                float l_m1_0 = lum[y * width + (x - 1)];
                float l_m1_p1 = lum[(y + 1) * width + (x - 1)];

                float l_0_p1 = lum[(y + 1) * width + x];
                float l_0_m1 = lum[(y - 1) * width + x];

                float gx = (l_p1_m1 + 2.0f * l_p1_0 + l_p1_p1) - (l_m1_m1 + 2.0f * l_m1_0 + l_m1_p1);
                float gy = (l_m1_p1 + 2.0f * l_0_p1 + l_p1_p1) - (l_m1_m1 + 2.0f * l_0_m1 + l_p1_m1);

                int o = (row + x) * 3;
                efg[o] = gx * gx;
                efg[o + 1] = gx * gy;
                efg[o + 2] = gy * gy;
            }
        });

        // 4. Compute stroke direction tangents from minor eigenvector
        float[] tangentX = new float[width * height];
        float[] tangentY = new float[width * height];
        float invSqrt2 = 0.70710678f;

        Parallel.For(0, width * height, i =>
        {
            int o = i * 3;
            float e = efg[o];
            float f = efg[o + 1];
            float g = efg[o + 2];

            float l1 = 0.5f * (e + g + (float)Math.Sqrt((e - g) * (e - g) + 4.0f * f * f));
            float tx = l1 - e;
            float ty = -f;
            float m = (float)Math.Sqrt(tx * tx + ty * ty);

            if (m < 1e-6f || (l1 - (e + g - l1)) < 1e-6f)
            {
                tangentX[i] = invSqrt2;
                tangentY[i] = invSqrt2;
            }
            else
            {
                tangentX[i] = tx / m;
                tangentY[i] = ty / m;
            }
        });

        // 5. Line Integral Convolution along flow tangents
        double strokeLen = 1.5 + Math.Clamp(stylization, 0.1, 10.0) * 2.0;
        int steps = Math.Max(2, (int)Math.Ceiling(strokeLen));
        double stepSize = strokeLen / steps;

        byte[] result = new byte[bgraPixels.Length];
        float[] bristleMap = new float[width * height];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int pIdx = rowOffset + x;
                float tx = tangentX[pIdx];
                float ty = tangentY[pIdx];

                double accB = 0.0, accG = 0.0, accR = 0.0, totalWeight = 0.0;
                double bristleAcc = 0.0;

                // Center sample
                int cIdx = pIdx * 4;
                accB += preSmoothed[cIdx];
                accG += preSmoothed[cIdx + 1];
                accR += preSmoothed[cIdx + 2];
                totalWeight += 1.0;
                bristleAcc += Hash01(x, y, 71, 0);

                // Forward and backward along tangent
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    double curX = x;
                    double curY = y;
                    for (int s = 1; s <= steps; s++)
                    {
                        curX += tx * stepSize * dir;
                        curY += ty * stepSize * dir;

                        if (curX < 0 || curX >= width || curY < 0 || curY >= height) break;

                        SampleBgraBilinear(preSmoothed, width, height, curX, curY, out double sb, out double sg, out double sr, out _);
                        double w = 1.0 - (s / (double)(steps + 1));
                        accB += sb * w;
                        accG += sg * w;
                        accR += sr * w;
                        totalWeight += w;

                        bristleAcc += Hash01((int)curX, (int)curY, 71 + s, 0) * w;
                    }
                }

                double finalB = accB / totalWeight;
                double finalG = accG / totalWeight;
                double finalR = accR / totalWeight;

                bristleMap[pIdx] = (float)(bristleAcc / totalWeight);

                result[cIdx] = (byte)Math.Clamp(Math.Round(finalB), 0, 255);
                result[cIdx + 1] = (byte)Math.Clamp(Math.Round(finalG), 0, 255);
                result[cIdx + 2] = (byte)Math.Clamp(Math.Round(finalR), 0, 255);
                result[cIdx + 3] = bgraPixels[cIdx + 3];
            }
        });

        // 6. Optional bristle relief directional shading
        if (lighting && shine > 0.01)
        {
            double lightRad = angleDegrees * Math.PI / 180.0;
            double lx = Math.Cos(lightRad);
            double ly = -Math.Sin(lightRad);
            double shineFactor = shine * (bristleDetail / 5.0) * 80.0;

            Parallel.For(1, height - 1, y =>
            {
                int row = y * width;
                for (int x = 1; x < width - 1; x++)
                {
                    int idx = (row + x) * 4;
                    float hL = bristleMap[row + (x - 1)];
                    float hR = bristleMap[row + (x + 1)];
                    float hT = bristleMap[(y - 1) * width + x];
                    float hB = bristleMap[(y + 1) * width + x];

                    double gradX = hR - hL;
                    double gradY = hB - hT;
                    double dot = gradX * lx + gradY * ly;
                    double highlight = dot * shineFactor;

                    result[idx] = (byte)Math.Clamp(Math.Round(result[idx] + highlight), 0, 255);
                    result[idx + 1] = (byte)Math.Clamp(Math.Round(result[idx + 1] + highlight), 0, 255);
                    result[idx + 2] = (byte)Math.Clamp(Math.Round(result[idx + 2] + highlight), 0, 255);
                }
            });
        }

        return result;
    }

    #endregion

    #region 4. Facet Filter (Kuwahara)

    /// <summary>
    /// Applies Kuwahara filter: edge-preserving painterly clustering selecting the least-varied of 4 overlapping quadrants.
    /// </summary>
    public static byte[] ApplyFacet(byte[] bgraPixels, int width, int height, int radius = 2)
    {
        if (width <= 0 || height <= 0 || radius <= 0) return (byte[])bgraPixels.Clone();

        int r = Math.Clamp(radius, 1, 6);
        byte[] result = new byte[bgraPixels.Length];
        int count = (r + 1) * (r + 1);

        (int qx, int qy)[] quadrants = { (-r, -r), (0, -r), (-r, 0), (0, 0) };

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                double bestVar = double.MaxValue;
                double bestB = bgraPixels[idx];
                double bestG = bgraPixels[idx + 1];
                double bestR = bgraPixels[idx + 2];

                foreach (var (qx, qy) in quadrants)
                {
                    double sumB = 0, sumG = 0, sumR = 0;
                    double sumLum = 0, sumLumSq = 0;

                    for (int dy = 0; dy <= r; dy++)
                    {
                        int py = Math.Clamp(y + qy + dy, 0, height - 1);
                        int pRow = py * width * 4;
                        for (int dx = 0; dx <= r; dx++)
                        {
                            int px = Math.Clamp(x + qx + dx, 0, width - 1);
                            int pIdx = pRow + px * 4;

                            double bVal = bgraPixels[pIdx];
                            double gVal = bgraPixels[pIdx + 1];
                            double rVal = bgraPixels[pIdx + 2];

                            sumB += bVal;
                            sumG += gVal;
                            sumR += rVal;

                            double lum = bVal * LumaB + gVal * LumaG + rVal * LumaR;
                            sumLum += lum;
                            sumLumSq += lum * lum;
                        }
                    }

                    double meanLum = sumLum / count;
                    double variance = (sumLumSq / count) - (meanLum * meanLum);

                    if (variance < bestVar)
                    {
                        bestVar = variance;
                        bestB = sumB / count;
                        bestG = sumG / count;
                        bestR = sumR / count;
                    }
                }

                result[idx] = (byte)Math.Clamp(Math.Round(bestB), 0, 255);
                result[idx + 1] = (byte)Math.Clamp(Math.Round(bestG), 0, 255);
                result[idx + 2] = (byte)Math.Clamp(Math.Round(bestR), 0, 255);
                result[idx + 3] = bgraPixels[idx + 3];
            }
        });

        return result;
    }

    #endregion

    #region 5. Wave Distortion Filter

    /// <summary>
    /// Applies multi-generator harmonic wave distortion (Sine, Triangle, Square).
    /// </summary>
    public static byte[] ApplyWave(
        byte[] bgraPixels,
        int width,
        int height,
        int generators = 5,
        double minWavelength = 10,
        double maxWavelength = 120,
        double minAmplitude = 5,
        double maxAmplitude = 35,
        int waveType = 0, // 0 = Sine, 1 = Triangle, 2 = Square
        uint seed = 42)
    {
        if (width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        int n = Math.Clamp(generators, 1, 32);
        var waves = new (double len, double amp, double p1, double p2)[n];

        for (int i = 0; i < n; i++)
        {
            double r1 = Hash01(i, 1, 7, seed);
            double r2 = Hash01(i, 2, 7, seed);
            double r3 = Hash01(i, 3, 7, seed);
            double r4 = Hash01(i, 4, 7, seed);

            double wlen = Math.Max(1.0, minWavelength + (maxWavelength - minWavelength) * r1);
            double wamp = minAmplitude + (maxAmplitude - minAmplitude) * r2;
            double p1 = r3 * Math.PI * 2.0;
            double p2 = r4 * Math.PI * 2.0;

            waves[i] = (wlen, wamp, p1, p2);
        }

        static double ShapeFunc(int type, double phase)
        {
            double p = (phase % (Math.PI * 2.0) + Math.PI * 2.0) % (Math.PI * 2.0) / (Math.PI * 2.0);
            return type switch
            {
                1 => 1.0 - 4.0 * Math.Abs(p - 0.5), // Triangle
                2 => p < 0.5 ? 1.0 : -1.0,          // Square
                _ => Math.Sin(phase)                // Sine
            };
        }

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                double dx = 0.0;
                double dy = 0.0;

                for (int i = 0; i < n; i++)
                {
                    var (wlen, wamp, p1, p2) = waves[i];
                    dx += wamp * ShapeFunc(waveType, Math.PI * 2.0 * y / wlen + p1);
                    dy += wamp * ShapeFunc(waveType, Math.PI * 2.0 * x / wlen + p2);
                }

                dx /= n;
                dy /= n;

                SampleBgraBilinear(bgraPixels, width, height, x + dx, y + dy, out double b, out double g, out double r, out double a);

                result[idx] = (byte)Math.Clamp(Math.Round(b), 0, 255);
                result[idx + 1] = (byte)Math.Clamp(Math.Round(g), 0, 255);
                result[idx + 2] = (byte)Math.Clamp(Math.Round(r), 0, 255);
                result[idx + 3] = (byte)Math.Clamp(Math.Round(a), 0, 255);
            }
        });

        return result;
    }

    #endregion

    #region 6. Ripple Distortion Filter

    /// <summary>
    /// Applies Ripple distortion with wavelength scale (Small=8px, Medium=16px, Large=32px).
    /// </summary>
    public static byte[] ApplyRipple(
        byte[] bgraPixels,
        int width,
        int height,
        double amount = 50.0,
        int rippleSize = 1) // 0=Small, 1=Medium, 2=Large
    {
        if (width <= 0 || height <= 0 || Math.Abs(amount) < 0.1) return (byte[])bgraPixels.Clone();

        double len = rippleSize switch
        {
            0 => 8.0,
            2 => 32.0,
            _ => 16.0
        };

        double amp = (amount / 100.0) * (len / 8.0) * 4.0;
        double tau = Math.PI * 2.0;

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                double sx = x + amp * Math.Sin(tau * y / len);
                double sy = y + amp * Math.Sin(tau * x / len);

                SampleBgraBilinear(bgraPixels, width, height, sx, sy, out double b, out double g, out double r, out double a);

                result[idx] = (byte)Math.Clamp(Math.Round(b), 0, 255);
                result[idx + 1] = (byte)Math.Clamp(Math.Round(g), 0, 255);
                result[idx + 2] = (byte)Math.Clamp(Math.Round(r), 0, 255);
                result[idx + 3] = (byte)Math.Clamp(Math.Round(a), 0, 255);
            }
        });

        return result;
    }

    #endregion

    #region 7. Pinch Distortion Filter

    /// <summary>
    /// Applies Pinch distortion: radial exponential contraction (&gt;0) or expansion (&lt;0).
    /// </summary>
    public static byte[] ApplyPinch(byte[] bgraPixels, int width, int height, double amount = 50.0)
    {
        if (width <= 0 || height <= 0 || Math.Abs(amount) < 0.1) return (byte[])bgraPixels.Clone();

        double cx = width / 2.0;
        double cy = height / 2.0;
        double rr = Math.Min(width, height) / 2.0;
        double a = Math.Clamp(amount / 100.0, -1.0, 1.0);
        double power = 1.0 - a * 0.5;

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                double dx = x - cx;
                double dy = y - cy;
                double r = Math.Sqrt(dx * dx + dy * dy);

                if (r >= rr || r <= 0.0001)
                {
                    result[idx] = bgraPixels[idx];
                    result[idx + 1] = bgraPixels[idx + 1];
                    result[idx + 2] = bgraPixels[idx + 2];
                    result[idx + 3] = bgraPixels[idx + 3];
                }
                else
                {
                    double rs = rr * Math.Pow(r / rr, power);
                    double sx = cx + dx * rs / r;
                    double sy = cy + dy * rs / r;

                    SampleBgraBilinear(bgraPixels, width, height, sx, sy, out double b, out double g, out double rVal, out double aVal);

                    result[idx] = (byte)Math.Clamp(Math.Round(b), 0, 255);
                    result[idx + 1] = (byte)Math.Clamp(Math.Round(g), 0, 255);
                    result[idx + 2] = (byte)Math.Clamp(Math.Round(rVal), 0, 255);
                    result[idx + 3] = (byte)Math.Clamp(Math.Round(aVal), 0, 255);
                }
            }
        });

        return result;
    }

    #endregion

    #region 8. Polar Coordinates Filter

    /// <summary>
    /// Applies Polar Coordinates conversion: Rectangular ↔ Polar.
    /// </summary>
    public static byte[] ApplyPolarCoordinates(byte[] bgraPixels, int width, int height, bool rectangularToPolar = true)
    {
        if (width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        double cx = width / 2.0;
        double cy = height / 2.0;
        double rmax = Math.Min(width, height) / 2.0;
        double tau = Math.PI * 2.0;

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;
                double sx, sy;

                if (rectangularToPolar)
                {
                    double dx = x - cx;
                    double dy = y - cy;
                    double theta = (Math.Atan2(dx, -dy) % tau + tau) % tau;
                    double r = Math.Sqrt(dx * dx + dy * dy);

                    sx = (theta / tau) * width;
                    sy = (r / rmax) * height;
                }
                else
                {
                    double theta = (x / (double)width) * tau;
                    double r = (y / (double)height) * rmax;

                    sx = cx + r * Math.Sin(theta);
                    sy = cy - r * Math.Cos(theta);
                }

                SampleBgraBilinear(bgraPixels, width, height, sx, sy, out double b, out double g, out double rVal, out double aVal);

                result[idx] = (byte)Math.Clamp(Math.Round(b), 0, 255);
                result[idx + 1] = (byte)Math.Clamp(Math.Round(g), 0, 255);
                result[idx + 2] = (byte)Math.Clamp(Math.Round(rVal), 0, 255);
                result[idx + 3] = (byte)Math.Clamp(Math.Round(aVal), 0, 255);
            }
        });

        return result;
    }

    #endregion

    #region 9. Color Halftone Filter

    /// <summary>
    /// Simulates halftone offset printing screens: each channel is screened at its own angle into anti-aliased dots.
    /// </summary>
    public static byte[] ApplyColorHalftone(
        byte[] bgraPixels,
        int width,
        int height,
        double maxRadius = 8.0,
        double angleC = 108.0,
        double angleM = 162.0,
        double angleY = 90.0,
        double angleK = 45.0)
    {
        if (width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        double rmax = Math.Max(1.0, maxRadius);
        double cell = rmax * Math.Sqrt(2.0);

        double[] angles = { angleK, angleC, angleM, angleY }; // Maps to B, G, R, Gray
        double[][] rot = new double[4][];
        for (int i = 0; i < 4; i++)
        {
            double rad = angles[i] * Math.PI / 180.0;
            rot[i] = new[] { Math.Sin(rad), Math.Cos(rad) };
        }

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                for (int c = 0; c < 3; c++)
                {
                    double s = rot[c][0];
                    double co = rot[c][1];

                    double u = x * co + y * s;
                    double w = -x * s + y * co;

                    double cu = Math.Floor(u / cell);
                    double cv = Math.Floor(w / cell);

                    double mu = (cu + 0.5) * cell;
                    double mv = (cv + 0.5) * cell;

                    double d = Math.Sqrt((u - mu) * (u - mu) + (w - mv) * (w - mv));

                    // Sample ink level (1 - value)
                    double sampleX = mu * co - mv * s;
                    double sampleY = mu * s + mv * co;
                    SampleBgraBilinear(bgraPixels, width, height, sampleX, sampleY, out double sb, out double sg, out double sr, out _);

                    double val = c switch { 0 => sb, 1 => sg, _ => sr } / 255.0;
                    double ink = 1.0 - val;
                    double dotRadius = rmax * Math.Sqrt(Math.Clamp(ink, 0.0, 1.0));

                    double coverage = Math.Clamp(dotRadius + 0.5 - d, 0.0, 1.0);
                    double outVal = (1.0 - coverage) * 255.0;

                    result[idx + c] = (byte)Math.Clamp(Math.Round(outVal), 0, 255);
                }

                result[idx + 3] = bgraPixels[idx + 3];
            }
        });

        return result;
    }

    #endregion

    #region 10. Crystallize & Pointillize Filters

    /// <summary>
    /// Applies Crystallize / Pointillize Voronoi cell mosaic clustering.
    /// </summary>
    public static byte[] ApplyCrystallize(byte[] bgraPixels, int width, int height, int cellSize = 20)
    {
        if (width <= 0 || height <= 0 || cellSize <= 1) return (byte[])bgraPixels.Clone();

        int cs = Math.Max(2, cellSize);
        int gridW = (width + cs - 1) / cs + 2;
        int gridH = (height + cs - 1) / cs + 2;

        // Precompute jittered cell sites
        var sites = new (double x, double y)[gridW * gridH];
        for (int gy = 0; gy < gridH; gy++)
        {
            for (int gx = 0; gx < gridW; gx++)
            {
                double jx = Hash01(gx, gy, 1, 991);
                double jy = Hash01(gx, gy, 2, 991);
                sites[gy * gridW + gx] = ((gx - 1 + jx) * cs, (gy - 1 + jy) * cs);
            }
        }

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            int gy = (y / cs) + 1;

            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;
                int gx = (x / cs) + 1;

                double bestDist = double.MaxValue;
                double bestSiteX = x;
                double bestSiteY = y;

                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = Math.Clamp(gy + dy, 0, gridH - 1);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = Math.Clamp(gx + dx, 0, gridW - 1);
                        var site = sites[ny * gridW + nx];
                        double distSq = (x - site.x) * (x - site.x) + (y - site.y) * (y - site.y);
                        if (distSq < bestDist)
                        {
                            bestDist = distSq;
                            bestSiteX = site.x;
                            bestSiteY = site.y;
                        }
                    }
                }

                SampleBgraBilinear(bgraPixels, width, height, bestSiteX, bestSiteY, out double b, out double g, out double r, out double a);

                result[idx] = (byte)Math.Clamp(Math.Round(b), 0, 255);
                result[idx + 1] = (byte)Math.Clamp(Math.Round(g), 0, 255);
                result[idx + 2] = (byte)Math.Clamp(Math.Round(r), 0, 255);
                result[idx + 3] = (byte)Math.Clamp(Math.Round(a), 0, 255);
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Pointillize filter: smaller Voronoi dot cells on canvas background.
    /// </summary>
    public static byte[] ApplyPointillize(byte[] bgraPixels, int width, int height, int cellSize = 10)
    {
        return ApplyCrystallize(bgraPixels, width, height, cellSize);
    }

    #endregion

    #region 11. Camera Raw Filter

    /// <summary>
    /// Applies comprehensive Camera Raw develop pipeline: Exposure, Temperature, Tint, Contrast,
    /// Highlights, Shadows, Whites, Blacks, Clarity, Dehaze, Vibrance, Saturation.
    /// </summary>
    public static byte[] ApplyCameraRaw(
        byte[] bgraPixels,
        int width,
        int height,
        double exposure = 0.0,       // -5.0 .. +5.0 EV
        double temperature = 0.0,    // -100 .. +100
        double tint = 0.0,           // -100 .. +100
        double contrast = 0.0,       // -100 .. +100
        double highlights = 0.0,     // -100 .. +100
        double shadows = 0.0,        // -100 .. +100
        double whites = 0.0,         // -100 .. +100
        double blacks = 0.0,         // -100 .. +100
        double clarity = 0.0,        // -100 .. +100
        double dehaze = 0.0,         // -100 .. +100
        double vibrance = 0.0,       // -100 .. +100
        double saturation = 0.0)     // -100 .. +100
    {
        if (width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        double expScale = Math.Pow(2.0, exposure);
        double tempShift = temperature / 100.0 * 0.3; // Warm/Cool
        double tintShift = tint / 100.0 * 0.3;       // Green/Magenta

        double contrastFactor = Math.Pow((100.0 + contrast) / 100.0, 2.0);
        double highWeight = highlights / 100.0 * 0.4;
        double shadWeight = shadows / 100.0 * 0.4;
        double whiteShift = whites / 100.0 * 0.2;
        double blackShift = blacks / 100.0 * 0.2;

        // Clarity / Dehaze base blur
        byte[]? clarityBase = Math.Abs(clarity) > 1.0 ? ApplyGaussianBlur(bgraPixels, width, height, 16.0) : null;
        double clarityAmount = clarity / 100.0 * 0.5;
        double dehazeAmount = dehaze / 100.0 * 0.4;

        double satFactor = (100.0 + saturation) / 100.0;
        double vibFactor = vibrance / 100.0;

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;

            // 1. Decode sRGB to Linear float [0..1]
            double bLin = Math.Pow(bgraPixels[idx] / 255.0, 2.2);
            double gLin = Math.Pow(bgraPixels[idx + 1] / 255.0, 2.2);
            double rLin = Math.Pow(bgraPixels[idx + 2] / 255.0, 2.2);

            // 2. Exposure & White Balance
            rLin *= expScale * (1.0 + tempShift);
            gLin *= expScale * (1.0 - tintShift);
            bLin *= expScale * (1.0 - tempShift);

            // 3. Dehaze
            if (Math.Abs(dehazeAmount) > 0.001)
            {
                double darkChannel = Math.Min(rLin, Math.Min(gLin, bLin));
                rLin += (rLin - darkChannel) * dehazeAmount;
                gLin += (gLin - darkChannel) * dehazeAmount;
                bLin += (bLin - darkChannel) * dehazeAmount;
            }

            // 4. Clarity bandpass
            if (clarityBase != null)
            {
                double bBlur = Math.Pow(clarityBase[idx] / 255.0, 2.2);
                double gBlur = Math.Pow(clarityBase[idx + 1] / 255.0, 2.2);
                double rBlur = Math.Pow(clarityBase[idx + 2] / 255.0, 2.2);

                rLin += (rLin - rBlur) * clarityAmount;
                gLin += (gLin - gBlur) * clarityAmount;
                bLin += (bLin - bBlur) * clarityAmount;
            }

            // 5. Highlights, Shadows, Whites, Blacks
            double lum = rLin * LumaR + gLin * LumaG + bLin * LumaB;
            double highMask = SmoothStep(0.5, 1.0, lum);
            double shadMask = 1.0 - SmoothStep(0.0, 0.5, lum);

            double toneAdjust = 1.0 + highMask * highWeight + shadMask * shadWeight + whiteShift - blackShift;
            rLin = Math.Max(0.0, rLin * toneAdjust);
            gLin = Math.Max(0.0, gLin * toneAdjust);
            bLin = Math.Max(0.0, bLin * toneAdjust);

            // 6. Contrast S-curve
            rLin = (rLin - 0.5) * contrastFactor + 0.5;
            gLin = (gLin - 0.5) * contrastFactor + 0.5;
            bLin = (bLin - 0.5) * contrastFactor + 0.5;

            // 7. Encode back to perceptual sRGB [0..255]
            double r = Math.Pow(Math.Clamp(rLin, 0.0, 1.0), 1.0 / 2.2) * 255.0;
            double g = Math.Pow(Math.Clamp(gLin, 0.0, 1.0), 1.0 / 2.2) * 255.0;
            double b = Math.Pow(Math.Clamp(bLin, 0.0, 1.0), 1.0 / 2.2) * 255.0;

            // 8. Vibrance and Saturation
            double maxC = Math.Max(r, Math.Max(g, b));
            double minC = Math.Min(r, Math.Min(g, b));
            double currentSat = maxC > 0 ? (maxC - minC) / maxC : 0;
            double gray = r * LumaR + g * LumaG + b * LumaB;

            double vibMult = 1.0 + vibFactor * (1.0 - currentSat);
            double totalSat = satFactor * vibMult;

            r = gray + (r - gray) * totalSat;
            g = gray + (g - gray) * totalSat;
            b = gray + (b - gray) * totalSat;

            result[idx] = (byte)Math.Clamp(Math.Round(b), 0, 255);
            result[idx + 1] = (byte)Math.Clamp(Math.Round(g), 0, 255);
            result[idx + 2] = (byte)Math.Clamp(Math.Round(r), 0, 255);
            result[idx + 3] = bgraPixels[idx + 3];
        });

        return result;
    }

    #endregion

    #region 12. Lens Flare Filter

    /// <summary>
    /// Applies procedural Lens Flare: core burst, glow halo, rays, and chromatic ghost reflections.
    /// </summary>
    public static byte[] ApplyLensFlare(
        byte[] bgraPixels,
        int width,
        int height,
        double brightness = 100.0,
        double centerX = 0.5,
        double centerY = 0.5,
        int lensType = 0) // 0=Zoom (50-300mm), 1=35mm Prime, 2=105mm Prime, 3=Movie Prime
    {
        if (width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        double bw = width;
        double bh = height;
        double diag = Math.Sqrt(bw * bw + bh * bh);
        double cx = centerX * bw;
        double cy = centerY * bh;
        double mx = bw / 2.0;
        double my = bh / 2.0;

        double bright = Math.Clamp(brightness, 10.0, 300.0) / 100.0;
        double s = diag / 1000.0;

        var (coreR, glowR, ringR, raysStrength) = lensType switch
        {
            1 => (12.0 * s, 70.0 * s, 90.0 * s, 0.3),
            2 => (22.0 * s, 130.0 * s, 0.0, 0.8),
            3 => (10.0 * s, 60.0 * s, 0.0, 0.2),
            _ => (14.0 * s, 90.0 * s, 0.0, 0.5)
        };

        // Ghost reflection discs along axis from flare to center
        var ghosts = new (double t, double r, double cr, double cg, double cb, double k)[]
        {
            (0.35, 10.0 * s, 0.6, 0.8, 1.0, 0.18),
            (0.60, 26.0 * s, 0.5, 1.0, 0.6, 0.10),
            (0.85, 6.0 * s,  1.0, 0.7, 0.4, 0.25),
            (1.25, 40.0 * s, 0.7, 0.5, 1.0, 0.08),
            (1.50, 16.0 * s, 1.0, 0.6, 0.5, 0.15),
            (1.90, 70.0 * s, 0.5, 0.7, 1.0, 0.06),
        };

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                double dx = x - cx;
                double dy = y - cy;
                double d = Math.Sqrt(dx * dx + dy * dy);

                // Core burst & glow
                double core = Math.Exp(-d / coreR) * 2.0 * bright;
                double glow = Math.Exp(-d / glowR) * 0.8 * bright;

                // Optical rays
                double angle = Math.Atan2(dy, dx);
                double rayPattern = Math.Pow(Math.Abs(Math.Sin(angle * 8.0)), 12.0) * raysStrength;
                double rays = Math.Exp(-d / (glowR * 1.5)) * rayPattern * bright;

                double totalWhite = core + glow + rays;

                double ghostR = 0, ghostG = 0, ghostB = 0;
                foreach (var g in ghosts)
                {
                    double gx = cx + (mx - cx) * g.t;
                    double gy = cy + (my - cy) * g.t;
                    double gd = Math.Sqrt((x - gx) * (x - gx) + (y - gy) * (y - gy));
                    double intensity = Math.Exp(-gd / g.r) * g.k * bright;
                    ghostR += intensity * g.cr;
                    ghostG += intensity * g.cg;
                    ghostB += intensity * g.cb;
                }

                // Additive screen blend
                double addR = (totalWhite + ghostR) * 255.0;
                double addG = (totalWhite + ghostG) * 255.0;
                double addB = (totalWhite + ghostB) * 255.0;

                result[idx] = (byte)Math.Min(255, bgraPixels[idx] + (int)Math.Round(addB));
                result[idx + 1] = (byte)Math.Min(255, bgraPixels[idx + 1] + (int)Math.Round(addG));
                result[idx + 2] = (byte)Math.Min(255, bgraPixels[idx + 2] + (int)Math.Round(addR));
                result[idx + 3] = bgraPixels[idx + 3];
            }
        });

        return result;
    }

    #endregion

    #region 13. Clouds & Difference Clouds Filters

    /// <summary>
    /// Generates procedural Clouds / Difference Clouds using fractal value noise.
    /// </summary>
    public static byte[] ApplyClouds(
        byte[] bgraPixels,
        int width,
        int height,
        Color foregroundColor,
        Color backgroundColor,
        bool difference = false,
        uint seed = 12345)
    {
        if (width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        double scale = Math.Min(width, height) / 8.0;
        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x * 4;

                double noise = FractalTurbulence(x / scale, y / scale, 6, seed);
                double t = Math.Clamp(noise, 0.0, 1.0);

                byte r = (byte)Math.Round(backgroundColor.R * (1.0 - t) + foregroundColor.R * t);
                byte g = (byte)Math.Round(backgroundColor.G * (1.0 - t) + foregroundColor.G * t);
                byte b = (byte)Math.Round(backgroundColor.B * (1.0 - t) + foregroundColor.B * t);

                if (difference)
                {
                    result[idx] = (byte)Math.Abs(bgraPixels[idx] - b);
                    result[idx + 1] = (byte)Math.Abs(bgraPixels[idx + 1] - g);
                    result[idx + 2] = (byte)Math.Abs(bgraPixels[idx + 2] - r);
                }
                else
                {
                    result[idx] = b;
                    result[idx + 1] = g;
                    result[idx + 2] = r;
                }

                result[idx + 3] = bgraPixels[idx + 3];
            }
        });

        return result;
    }

    #endregion

    #region 14. Stylize Filters: Find Edges & Solarize

    /// <summary>
    /// Applies Find Edges filter: 3x3 Sobel edge detection across all colour channels, producing dark lines on white paper.
    /// </summary>
    public static byte[] ApplyFindEdges(byte[] bgraPixels, int width, int height)
    {
        if (width <= 0 || height <= 0) return (byte[])bgraPixels.Clone();

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(1, height - 1, y =>
        {
            int rowOffset = y * width * 4;
            for (int x = 1; x < width - 1; x++)
            {
                int idx = rowOffset + x * 4;

                for (int c = 0; c < 3; c++)
                {
                    int get(int dx, int dy) => bgraPixels[((y + dy) * width + (x + dx)) * 4 + c];

                    int gx = (get(1, -1) + 2 * get(1, 0) + get(1, 1)) - (get(-1, -1) + 2 * get(-1, 0) + get(-1, 1));
                    int gy = (get(-1, 1) + 2 * get(0, 1) + get(1, 1)) - (get(-1, -1) + 2 * get(0, -1) + get(1, -1));

                    double mag = Math.Sqrt(gx * gx + gy * gy) / 4.0;
                    double outVal = Math.Clamp(255.0 - mag, 0.0, 255.0);

                    result[idx + c] = (byte)Math.Round(outVal);
                }

                result[idx + 3] = bgraPixels[idx + 3];
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Solarize filter: inverts channel values above a specified threshold.
    /// </summary>
    public static byte[] ApplySolarize(byte[] bgraPixels, int threshold = 128)
    {
        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = bgraPixels[idx] > threshold ? (byte)(255 - bgraPixels[idx]) : bgraPixels[idx];
            result[idx + 1] = bgraPixels[idx + 1] > threshold ? (byte)(255 - bgraPixels[idx + 1]) : bgraPixels[idx + 1];
            result[idx + 2] = bgraPixels[idx + 2] > threshold ? (byte)(255 - bgraPixels[idx + 2]) : bgraPixels[idx + 2];
            result[idx + 3] = bgraPixels[idx + 3];
        });
        return result;
    }

    #endregion

    #region 13. Studio Color Adjustments (Exposure, Selective Color, 3D LUT, Gradient Map, Photo Filter)

    /// <summary>
    /// Applies linear-light Exposure adjustment with offset and gamma correction.
    /// </summary>
    public static byte[] ApplyExposure(byte[] bgraPixels, double exposure, double offset = 0.0, double gamma = 1.0)
    {
        double evScale = Math.Pow(2.0, exposure);
        double invGamma = 1.0 / Math.Max(0.01, gamma);

        byte[] lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double srgbNorm = i / 255.0;
            // sRGB to linear
            double lin = srgbNorm <= 0.04045 ? srgbNorm / 12.92 : Math.Pow((srgbNorm + 0.055) / 1.055, 2.4);
            // Apply exposure and offset
            double linAdj = Math.Max(0.0, lin * evScale + offset);
            // Apply gamma
            double gamAdj = Math.Pow(linAdj, invGamma);
            // Linear back to sRGB
            double srgbOut = gamAdj <= 0.0031308 ? gamAdj * 12.92 : 1.055 * Math.Pow(gamAdj, 1.0 / 2.4) - 0.055;
            lut[i] = (byte)Math.Clamp((int)Math.Round(srgbOut * 255.0), 0, 255);
        }

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            result[idx] = lut[bgraPixels[idx]];         // B
            result[idx + 1] = lut[bgraPixels[idx + 1]]; // G
            result[idx + 2] = lut[bgraPixels[idx + 2]]; // R
            result[idx + 3] = bgraPixels[idx + 3];     // A
        });
        return result;
    }

    /// <summary>
    /// Applies Selective Color adjustment modifying CMYK inks across 9 tonal/chroma ranges.
    /// </summary>
    public static byte[] ApplySelectiveColor(byte[] bgraPixels, bool relative, double[,] adjustments)
    {
        // adjustments: [9, 4] where row is range, columns are C, M, Y, K in [-100..100]
        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            double b = bgraPixels[idx] / 255.0;
            double g = bgraPixels[idx + 1] / 255.0;
            double r = bgraPixels[idx + 2] / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double mid = r + g + b - max - min;

            // 9 Range Weights: Reds, Yellows, Greens, Cyans, Blues, Magentas, Whites, Neutrals, Blacks
            Span<double> w = stackalloc double[9];
            w[0] = r >= max ? max - mid : 0.0; // Reds
            w[1] = b <= min ? mid - min : 0.0; // Yellows
            w[2] = g >= max ? max - mid : 0.0; // Greens
            w[3] = r <= min ? mid - min : 0.0; // Cyans
            w[4] = b >= max ? max - mid : 0.0; // Blues
            w[5] = g <= min ? mid - min : 0.0; // Magentas
            w[6] = Math.Max(0.0, (min - 0.5) * 2.0); // Whites
            w[7] = Math.Clamp(1.0 - Math.Abs(max - 0.5) - Math.Abs(min - 0.5), 0.0, 1.0); // Neutrals
            w[8] = Math.Max(0.0, (0.5 - max) * 2.0); // Blacks

            double deltaR = 0.0, deltaG = 0.0, deltaB = 0.0;

            for (int rng = 0; rng < 9; rng++)
            {
                double weight = w[rng];
                if (weight <= 0.0) continue;

                double aC = adjustments[rng, 0] / 100.0;
                double aM = adjustments[rng, 1] / 100.0;
                double aY = adjustments[rng, 2] / 100.0;
                double aK = adjustments[rng, 3] / 100.0;

                double inkR = 1.0 - r;
                double inkG = 1.0 - g;
                double inkB = 1.0 - b;

                double dC = relative ? (aC + aK) * inkR : aC + aK;
                double dM = relative ? (aM + aK) * inkG : aM + aK;
                double dY = relative ? (aY + aK) * inkB : aY + aK;

                deltaR += dC * weight;
                deltaG += dM * weight;
                deltaB += dY * weight;
            }

            result[idx + 2] = (byte)Math.Clamp((int)Math.Round((r - deltaR) * 255.0), 0, 255); // R
            result[idx + 1] = (byte)Math.Clamp((int)Math.Round((g - deltaG) * 255.0), 0, 255); // G
            result[idx]     = (byte)Math.Clamp((int)Math.Round((b - deltaB) * 255.0), 0, 255); // B
            result[idx + 3] = bgraPixels[idx + 3]; // A
        });

        return result;
    }

    /// <summary>
    /// Applies 3D LUT Color Lookup with tetrahedral or trilinear interpolation.
    /// </summary>
    public static byte[] ApplyColorLookup(byte[] bgraPixels, int lutSize, float[] lutData, bool tetrahedral = true)
    {
        if (lutData == null || lutData.Length < lutSize * lutSize * lutSize * 3)
            return (byte[])bgraPixels.Clone();

        int n = lutSize;
        float m = n - 1.0f;
        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            float b = bgraPixels[idx] / 255.0f;
            float g = bgraPixels[idx + 1] / 255.0f;
            float r = bgraPixels[idx + 2] / 255.0f;

            float pr = Math.Clamp(r * m, 0f, m);
            float pg = Math.Clamp(g * m, 0f, m);
            float pb = Math.Clamp(b * m, 0f, m);

            int ir = Math.Min((int)Math.Floor(pr), n - 2);
            int ig = Math.Min((int)Math.Floor(pg), n - 2);
            int ib = Math.Min((int)Math.Floor(pb), n - 2);

            float fr = pr - ir;
            float fg = pg - ig;
            float fb = pb - ib;

            (float r, float g, float b) at(int dr, int dg, int db)
            {
                int lutIdx = (((ib + db) * n + (ig + dg)) * n + (ir + dr)) * 3;
                return (lutData[lutIdx], lutData[lutIdx + 1], lutData[lutIdx + 2]);
            }

            (float outR, float outG, float outB) outRgb;

            if (tetrahedral)
            {
                var c000 = at(0, 0, 0);
                var c111 = at(1, 1, 1);

                if (fr > fg)
                {
                    if (fg > fb)
                    {
                        var c100 = at(1, 0, 0);
                        var c110 = at(1, 1, 0);
                        outRgb.outR = (1f - fr) * c000.r + (fr - fg) * c100.r + (fg - fb) * c110.r + fb * c111.r;
                        outRgb.outG = (1f - fr) * c000.g + (fr - fg) * c100.g + (fg - fb) * c110.g + fb * c111.g;
                        outRgb.outB = (1f - fr) * c000.b + (fr - fg) * c100.b + (fg - fb) * c110.b + fb * c111.b;
                    }
                    else if (fr > fb)
                    {
                        var c100 = at(1, 0, 0);
                        var c101 = at(1, 0, 1);
                        outRgb.outR = (1f - fr) * c000.r + (fr - fb) * c100.r + (fb - fg) * c101.r + fg * c111.r;
                        outRgb.outG = (1f - fr) * c000.g + (fr - fb) * c100.g + (fb - fg) * c101.g + fg * c111.g;
                        outRgb.outB = (1f - fr) * c000.b + (fr - fb) * c100.b + (fb - fg) * c101.b + fg * c111.b;
                    }
                    else
                    {
                        var c001 = at(0, 0, 1);
                        var c101 = at(1, 0, 1);
                        outRgb.outR = (1f - fb) * c000.r + (fb - fr) * c001.r + (fr - fg) * c101.r + fg * c111.r;
                        outRgb.outG = (1f - fb) * c000.g + (fb - fr) * c001.g + (fr - fg) * c101.g + fg * c111.g;
                        outRgb.outB = (1f - fb) * c000.b + (fb - fr) * c001.b + (fr - fg) * c101.b + fg * c111.b;
                    }
                }
                else if (fb > fg)
                {
                    var c001 = at(0, 0, 1);
                    var c011 = at(0, 1, 1);
                    outRgb.outR = (1f - fb) * c000.r + (fb - fg) * c001.r + (fg - fr) * c011.r + fr * c111.r;
                    outRgb.outG = (1f - fb) * c000.g + (fb - fg) * c001.g + (fg - fr) * c011.g + fr * c111.g;
                    outRgb.outB = (1f - fb) * c000.b + (fb - fg) * c001.b + (fg - fr) * c011.b + fr * c111.b;
                }
                else if (fb > fr)
                {
                    var c010 = at(0, 1, 0);
                    var c011 = at(0, 1, 1);
                    outRgb.outR = (1f - fg) * c000.r + (fg - fb) * c010.r + (fb - fr) * c011.r + fr * c111.r;
                    outRgb.outG = (1f - fg) * c000.g + (fg - fb) * c010.g + (fb - fr) * c011.g + fr * c111.g;
                    outRgb.outB = (1f - fg) * c000.b + (fg - fb) * c010.b + (fb - fr) * c011.b + fr * c111.b;
                }
                else
                {
                    var c010 = at(0, 1, 0);
                    var c110 = at(1, 1, 0);
                    outRgb.outR = (1f - fg) * c000.r + (fg - fr) * c010.r + (fr - fb) * c110.r + fb * c111.r;
                    outRgb.outG = (1f - fg) * c000.g + (fg - fr) * c010.g + (fr - fb) * c110.g + fb * c111.g;
                    outRgb.outB = (1f - fg) * c000.b + (fg - fr) * c010.b + (fr - fb) * c110.b + fb * c111.b;
                }
            }
            else
            {
                // Trilinear interpolation
                var c000 = at(0, 0, 0); var c100 = at(1, 0, 0);
                var c010 = at(0, 1, 0); var c110 = at(1, 1, 0);
                var c001 = at(0, 0, 1); var c101 = at(1, 0, 1);
                var c011 = at(0, 1, 1); var c111 = at(1, 1, 1);

                float c00R = c000.r + (c100.r - c000.r) * fr;
                float c10R = c010.r + (c110.r - c010.r) * fr;
                float c01R = c001.r + (c101.r - c001.r) * fr;
                float c11R = c011.r + (c111.r - c011.r) * fr;
                float c0R = c00R + (c10R - c00R) * fg;
                float c1R = c01R + (c11R - c01R) * fg;
                outRgb.outR = c0R + (c1R - c0R) * fb;

                float c00G = c000.g + (c100.g - c000.g) * fr;
                float c10G = c010.g + (c110.g - c010.g) * fr;
                float c01G = c001.g + (c101.g - c001.g) * fr;
                float c11G = c011.g + (c111.g - c011.g) * fr;
                float c0G = c00G + (c10G - c00G) * fg;
                float c1G = c01G + (c11G - c01G) * fg;
                outRgb.outG = c0G + (c1G - c0G) * fb;

                float c00B = c000.b + (c100.b - c000.b) * fr;
                float c10B = c010.b + (c110.b - c010.b) * fr;
                float c01B = c001.b + (c101.b - c001.b) * fr;
                float c11B = c011.b + (c111.b - c011.b) * fr;
                float c0B = c00B + (c10B - c00B) * fg;
                float c1B = c01B + (c11B - c01B) * fg;
                outRgb.outB = c0B + (c1B - c0B) * fb;
            }

            result[idx + 2] = (byte)Math.Clamp((int)Math.Round(outRgb.outR * 255.0f), 0, 255); // R
            result[idx + 1] = (byte)Math.Clamp((int)Math.Round(outRgb.outG * 255.0f), 0, 255); // G
            result[idx]     = (byte)Math.Clamp((int)Math.Round(outRgb.outB * 255.0f), 0, 255); // B
            result[idx + 3] = bgraPixels[idx + 3];
        });

        return result;
    }

    /// <summary>
    /// Applies Gradient Map adjustment: maps pixel luminosity to a multi-stop color gradient ramp.
    /// </summary>
    public static byte[] ApplyGradientMap(byte[] bgraPixels, Color[] colors, float[] stops)
    {
        if (colors == null || colors.Length == 0 || stops == null || stops.Length != colors.Length)
            return (byte[])bgraPixels.Clone();

        // Precompute 256-entry gradient LUT
        Color[] lut = new Color[256];
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255.0f;
            if (t <= stops[0]) { lut[i] = colors[0]; continue; }
            if (t >= stops[^1]) { lut[i] = colors[^1]; continue; }

            int sIdx = 0;
            while (sIdx < stops.Length - 1 && stops[sIdx + 1] < t) sIdx++;

            float span = stops[sIdx + 1] - stops[sIdx];
            float frac = span > 0.0001f ? (t - stops[sIdx]) / span : 0f;

            var c0 = colors[sIdx];
            var c1 = colors[sIdx + 1];

            byte r = (byte)Math.Round(c0.R + (c1.R - c0.R) * frac);
            byte g = (byte)Math.Round(c0.G + (c1.G - c0.G) * frac);
            byte b = (byte)Math.Round(c0.B + (c1.B - c0.B) * frac);
            lut[i] = Color.FromRgb(r, g, b);
        }

        byte[] result = new byte[bgraPixels.Length];
        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            byte b = bgraPixels[idx];
            byte g = bgraPixels[idx + 1];
            byte r = bgraPixels[idx + 2];

            int lum = (int)Math.Round(0.2126 * r + 0.7152 * g + 0.0722 * b);
            lum = Math.Clamp(lum, 0, 255);

            var mapColor = lut[lum];
            result[idx]     = mapColor.B;
            result[idx + 1] = mapColor.G;
            result[idx + 2] = mapColor.R;
            result[idx + 3] = bgraPixels[idx + 3];
        });

        return result;
    }

    /// <summary>
    /// Applies Photo Filter with density and luminosity preservation.
    /// </summary>
    public static byte[] ApplyPhotoFilter(byte[] bgraPixels, Color filterColor, int density = 25, bool preserveLuminosity = true)
    {
        double factor = Math.Clamp(density, 1, 100) / 100.0;
        double fR = filterColor.R / 255.0;
        double fG = filterColor.G / 255.0;
        double fB = filterColor.B / 255.0;

        byte[] result = new byte[bgraPixels.Length];

        Parallel.For(0, bgraPixels.Length / 4, p =>
        {
            int idx = p * 4;
            double b = bgraPixels[idx] / 255.0;
            double g = bgraPixels[idx + 1] / 255.0;
            double r = bgraPixels[idx + 2] / 255.0;

            double origLum = 0.2126 * r + 0.7152 * g + 0.0722 * b;

            // Optical filter tinting
            double tR = r * (1.0 - factor + factor * fR);
            double tG = g * (1.0 - factor + factor * fG);
            double tB = b * (1.0 - factor + factor * fB);

            if (preserveLuminosity)
            {
                double tintLum = 0.2126 * tR + 0.7152 * tG + 0.0722 * tB;
                if (tintLum > 0.001)
                {
                    double ratio = origLum / tintLum;
                    tR = Math.Min(1.0, tR * ratio);
                    tG = Math.Min(1.0, tG * ratio);
                    tB = Math.Min(1.0, tB * ratio);
                }
            }

            result[idx + 2] = (byte)Math.Clamp((int)Math.Round(tR * 255.0), 0, 255);
            result[idx + 1] = (byte)Math.Clamp((int)Math.Round(tG * 255.0), 0, 255);
            result[idx]     = (byte)Math.Clamp((int)Math.Round(tB * 255.0), 0, 255);
            result[idx + 3] = bgraPixels[idx + 3];
        });

        return result;
    }

    #endregion

    #region 14. 3D LUT Helper (.cube parser and built-in studio presets)

    public static class Lut3DHelper
    {
        /// <summary>
        /// Parses an Adobe/DaVinci .cube format 3D LUT string.
        /// </summary>
        public static (int size, float[] data) ParseCube(string text)
        {
            int size = 0;
            var dataList = new List<float>();

            using var reader = new System.IO.StringReader(text);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;

                if (line.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int s))
                        size = s;
                    continue;
                }

                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 3 &&
                    float.TryParse(tokens[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r) &&
                    float.TryParse(tokens[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g) &&
                    float.TryParse(tokens[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float b))
                {
                    dataList.Add(r);
                    dataList.Add(g);
                    dataList.Add(b);
                }
            }

            if (size == 0 && dataList.Count > 0)
            {
                int tripletCount = dataList.Count / 3;
                size = (int)Math.Round(Math.Cbrt(tripletCount));
            }

            return (size, dataList.ToArray());
        }

        /// <summary>
        /// Generates a cinematic Studio 3D LUT preset table (e.g. "TealAndOrange", "WarmSunset", "CoolNoir", "Vintage70s", "BleachBypass").
        /// </summary>
        public static (int size, float[] data) CreatePresetLut(string preset, int size = 17)
        {
            int n = Math.Clamp(size, 8, 64);
            float[] data = new float[n * n * n * 3];

            for (int b = 0; b < n; b++)
            {
                float bNorm = b / (float)(n - 1);
                for (int g = 0; g < n; g++)
                {
                    float gNorm = g / (float)(n - 1);
                    for (int r = 0; r < n; r++)
                    {
                        float rNorm = r / (float)(n - 1);
                        int idx = ((b * n + g) * n + r) * 3;

                        float outR = rNorm, outG = gNorm, outB = bNorm;

                        switch (preset)
                        {
                            case "TealAndOrange":
                                outR = (float)Math.Pow(rNorm, 0.85f) * 1.05f;
                                outG = (float)Math.Pow(gNorm, 0.95f);
                                outB = (float)Math.Pow(bNorm, 1.25f) * 0.9f + (rNorm > 0.5f ? 0f : (0.5f - rNorm) * 0.2f);
                                break;
                            case "WarmSunset":
                                outR = (float)Math.Min(1.0, rNorm * 1.15f + 0.05f);
                                outG = (float)Math.Min(1.0, gNorm * 1.05f);
                                outB = (float)Math.Max(0.0, bNorm * 0.82f - 0.02f);
                                break;
                            case "CoolNoir":
                                float gray = 0.2126f * rNorm + 0.7152f * gNorm + 0.0722f * bNorm;
                                outR = gray * 0.85f;
                                outG = gray * 0.92f;
                                outB = gray * 1.10f;
                                break;
                            case "BleachBypass":
                                float lum = 0.2126f * rNorm + 0.7152f * gNorm + 0.0722f * bNorm;
                                outR = (rNorm + lum) * 0.55f;
                                outG = (gNorm + lum) * 0.55f;
                                outB = (bNorm + lum) * 0.55f;
                                break;
                            case "Vintage70s":
                                outR = (float)Math.Pow(rNorm, 0.9f) * 1.05f + 0.04f;
                                outG = (float)Math.Pow(gNorm, 0.95f) * 0.98f;
                                outB = (float)Math.Pow(bNorm, 1.15f) * 0.85f + 0.06f;
                                break;
                        }

                        data[idx]     = Math.Clamp(outR, 0f, 1f);
                        data[idx + 1] = Math.Clamp(outG, 0f, 1f);
                        data[idx + 2] = Math.Clamp(outB, 0f, 1f);
                    }
                }
            }

            return (n, data);
        }
    }

    #endregion
}
