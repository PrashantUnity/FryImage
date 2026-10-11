using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Raster;

public enum LiquifyTool
{
    ForwardWarp,
    Reconstruct,
    Smooth,
    TwirlCw,
    TwirlCcw,
    Pucker,
    Bloat,
    PushLeft
}

/// <summary>
/// High-performance Studio Liquify Displacement Grid Engine.
/// Implements dense regular-mesh displacement fields with bilinear sub-pixel inverse mapping
/// and multi-threaded rendering for real-time deformation.
/// </summary>
public sealed class LiquifyMesh
{
    public const int CellSize = 12; // grid resolution in document pixels

    public int ImageWidth { get; }
    public int ImageHeight { get; }
    public int Cols { get; }
    public int Rows { get; }

    // Displacement offsets (Dx, Dy) at each grid intersection node:
    // Output pixel p samples source at p + D(p).
    public float[] Dx { get; }
    public float[] Dy { get; }

    public LiquifyMesh(int width, int height)
    {
        ImageWidth = Math.Max(1, width);
        ImageHeight = Math.Max(1, height);
        Cols = (ImageWidth + CellSize - 1) / CellSize + 2;
        Rows = (ImageHeight + CellSize - 1) / CellSize + 2;

        int totalNodes = Cols * Rows;
        Dx = new float[totalNodes];
        Dy = new float[totalNodes];
    }

    public void Reset()
    {
        Array.Clear(Dx, 0, Dx.Length);
        Array.Clear(Dy, 0, Dy.Length);
    }

    public void ReconstructAll(double percentage)
    {
        float factor = (float)Math.Clamp(1.0 - (percentage / 100.0), 0.0, 1.0);
        for (int i = 0; i < Dx.Length; i++)
        {
            Dx[i] *= factor;
            Dy[i] *= factor;
        }
    }

    /// <summary>
    /// Applies a brush dab stroke segment of the selected Liquify tool to the displacement grid.
    /// </summary>
    public void ApplyDab(
        LiquifyTool tool,
        Point prevPt,
        Point currPt,
        double size,
        double density = 50.0,
        double pressure = 100.0,
        double rate = 80.0)
    {
        double radius = Math.Max(2.0, size / 2.0);
        double radSq = radius * radius;

        // Density affects edge steepness: density 0 = linear falloff, 100 = steep plateaus
        double densityExp = 0.5 + (density / 100.0) * 2.5;
        double str = Math.Clamp(pressure / 100.0, 0.01, 1.0);
        double stepRate = Math.Clamp(rate / 100.0, 0.01, 1.0);

        double deltaX = currPt.X - prevPt.X;
        double deltaY = currPt.Y - prevPt.Y;
        double moveDist = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        Point center = currPt;
        int minCol = Math.Max(0, (int)Math.Floor((center.X - radius) / CellSize));
        int maxCol = Math.Min(Cols - 1, (int)Math.Ceiling((center.X + radius) / CellSize));
        int minRow = Math.Max(0, (int)Math.Floor((center.Y - radius) / CellSize));
        int maxRow = Math.Min(Rows - 1, (int)Math.Ceiling((center.Y + radius) / CellSize));

        for (int row = minRow; row <= maxRow; row++)
        {
            double ny = row * CellSize;
            double dy = ny - center.Y;

            for (int col = minCol; col <= maxCol; col++)
            {
                double nx = col * CellSize;
                double dx = nx - center.X;

                double dSq = dx * dx + dy * dy;
                if (dSq >= radSq) continue;

                double dNorm = Math.Sqrt(dSq) / radius;
                double falloff = Math.Pow(1.0 - dNorm, densityExp);
                double weight = falloff * str;
                if (weight <= 0.0) continue;

                int nodeIdx = row * Cols + col;

                switch (tool)
                {
                    case LiquifyTool.ForwardWarp:
                    {
                        // Forward warp pushes source sampling opposite to mouse motion
                        // so target pixels follow the brush stroke direction.
                        Dx[nodeIdx] -= (float)(deltaX * weight);
                        Dy[nodeIdx] -= (float)(deltaY * weight);
                        break;
                    }

                    case LiquifyTool.Reconstruct:
                    {
                        // Decays displacement toward zero
                        double decay = Math.Clamp(1.0 - (weight * stepRate * 0.25), 0.0, 1.0);
                        Dx[nodeIdx] = (float)(Dx[nodeIdx] * decay);
                        Dy[nodeIdx] = (float)(Dy[nodeIdx] * decay);
                        break;
                    }

                    case LiquifyTool.Smooth:
                    {
                        // Relaxes node displacement toward neighbor average
                        int left = row * Cols + Math.Max(0, col - 1);
                        int right = row * Cols + Math.Min(Cols - 1, col + 1);
                        int up = Math.Max(0, row - 1) * Cols + col;
                        int down = Math.Min(Rows - 1, row + 1) * Cols + col;

                        float avgX = (Dx[left] + Dx[right] + Dx[up] + Dx[down]) * 0.25f;
                        float avgY = (Dy[left] + Dy[right] + Dy[up] + Dy[down]) * 0.25f;

                        double blend = Math.Clamp(weight * stepRate * 0.35, 0.0, 1.0);
                        Dx[nodeIdx] = (float)(Dx[nodeIdx] + (avgX - Dx[nodeIdx]) * blend);
                        Dy[nodeIdx] = (float)(Dy[nodeIdx] + (avgY - Dy[nodeIdx]) * blend);
                        break;
                    }

                    case LiquifyTool.Pucker:
                    {
                        // Sucks surrounding pixels toward brush center:
                        // output at p samples from p + (p - center) * k
                        double puckerMag = weight * stepRate * 0.15;
                        Dx[nodeIdx] += (float)(dx * puckerMag);
                        Dy[nodeIdx] += (float)(dy * puckerMag);
                        break;
                    }

                    case LiquifyTool.Bloat:
                    {
                        // Pushes pixels away from brush center:
                        // output at p samples from p - (p - center) * k
                        double bloatMag = weight * stepRate * 0.15;
                        Dx[nodeIdx] -= (float)(dx * bloatMag);
                        Dy[nodeIdx] -= (float)(dy * bloatMag);
                        break;
                    }

                    case LiquifyTool.TwirlCw:
                    {
                        // Clockwise rotation about brush center:
                        // Samples counter-clockwise relative offset
                        double angle = weight * stepRate * 0.25;
                        double cos = Math.Cos(-angle);
                        double sin = Math.Sin(-angle);
                        double rotX = dx * cos - dy * sin;
                        double rotY = dx * sin + dy * cos;
                        Dx[nodeIdx] += (float)(rotX - dx);
                        Dy[nodeIdx] += (float)(rotY - dy);
                        break;
                    }

                    case LiquifyTool.TwirlCcw:
                    {
                        // Counter-clockwise rotation about brush center
                        double angle = weight * stepRate * 0.25;
                        double cos = Math.Cos(angle);
                        double sin = Math.Sin(angle);
                        double rotX = dx * cos - dy * sin;
                        double rotY = dx * sin + dy * cos;
                        Dx[nodeIdx] += (float)(rotX - dx);
                        Dy[nodeIdx] += (float)(rotY - dy);
                        break;
                    }

                    case LiquifyTool.PushLeft:
                    {
                        // Displaces perpendicular to motion vector (normal vector rotated 90 deg left)
                        if (moveDist > 0.001)
                        {
                            double perpX = -deltaY / moveDist;
                            double perpY = deltaX / moveDist;
                            double pushAmount = moveDist * weight;
                            Dx[nodeIdx] += (float)(perpX * pushAmount);
                            Dy[nodeIdx] += (float)(perpY * pushAmount);
                        }
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Deforms the source BGRA byte buffer using bilinear sub-pixel inverse mapping.
    /// </summary>
    public void RenderDeformedImage(byte[] sourceBgra, byte[] outputBgra)
    {
        int width = ImageWidth;
        int height = ImageHeight;

        Parallel.For(0, height, y =>
        {
            double gy = y / (double)CellSize;
            int r0 = (int)Math.Floor(gy);
            int r1 = Math.Min(Rows - 1, r0 + 1);
            float fy = (float)(gy - r0);
            float invFy = 1.0f - fy;

            int rowOffset0 = r0 * Cols;
            int rowOffset1 = r1 * Cols;

            for (int x = 0; x < width; x++)
            {
                double gx = x / (double)CellSize;
                int c0 = (int)Math.Floor(gx);
                int c1 = Math.Min(Cols - 1, c0 + 1);
                float fx = (float)(gx - c0);
                float invFx = 1.0f - fx;

                // Bilinear interpolation of field displacement (dx, dy)
                int i00 = rowOffset0 + c0;
                int i10 = rowOffset0 + c1;
                int i01 = rowOffset1 + c0;
                int i11 = rowOffset1 + c1;

                float dx = (Dx[i00] * invFx + Dx[i10] * fx) * invFy +
                           (Dx[i01] * invFx + Dx[i11] * fx) * fy;

                float dy = (Dy[i00] * invFx + Dy[i10] * fx) * invFy +
                           (Dy[i01] * invFx + Dy[i11] * fx) * fy;

                int outIdx = (y * width + x) * 4;

                // Fast path: untouched nodes
                if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                {
                    outputBgra[outIdx] = sourceBgra[outIdx];
                    outputBgra[outIdx + 1] = sourceBgra[outIdx + 1];
                    outputBgra[outIdx + 2] = sourceBgra[outIdx + 2];
                    outputBgra[outIdx + 3] = sourceBgra[outIdx + 3];
                    continue;
                }

                // Sample source at (x + dx, y + dy) with bilinear filtering
                float sx = Math.Clamp(x + dx, 0.0f, width - 1.0f);
                float sy = Math.Clamp(y + dy, 0.0f, height - 1.0f);

                int x0 = (int)Math.Floor(sx);
                int x1 = Math.Min(width - 1, x0 + 1);
                int y0 = (int)Math.Floor(sy);
                int y1 = Math.Min(height - 1, y0 + 1);

                float sfx = sx - x0;
                float sfy = sy - y0;

                float w00 = (1.0f - sfx) * (1.0f - sfy);
                float w10 = sfx * (1.0f - sfy);
                float w01 = (1.0f - sfx) * sfy;
                float w11 = sfx * sfy;

                int idx00 = (y0 * width + x0) * 4;
                int idx10 = (y0 * width + x1) * 4;
                int idx01 = (y1 * width + x0) * 4;
                int idx11 = (y1 * width + x1) * 4;

                outputBgra[outIdx] = (byte)Math.Clamp(Math.Round(
                    sourceBgra[idx00] * w00 + sourceBgra[idx10] * w10 +
                    sourceBgra[idx01] * w01 + sourceBgra[idx11] * w11), 0, 255);

                outputBgra[outIdx + 1] = (byte)Math.Clamp(Math.Round(
                    sourceBgra[idx00 + 1] * w00 + sourceBgra[idx10 + 1] * w10 +
                    sourceBgra[idx01 + 1] * w01 + sourceBgra[idx11 + 1] * w11), 0, 255);

                outputBgra[outIdx + 2] = (byte)Math.Clamp(Math.Round(
                    sourceBgra[idx00 + 2] * w00 + sourceBgra[idx10 + 2] * w10 +
                    sourceBgra[idx01 + 2] * w01 + sourceBgra[idx11 + 2] * w11), 0, 255);

                outputBgra[outIdx + 3] = (byte)Math.Clamp(Math.Round(
                    sourceBgra[idx00 + 3] * w00 + sourceBgra[idx10 + 3] * w10 +
                    sourceBgra[idx01 + 3] * w01 + sourceBgra[idx11 + 3] * w11), 0, 255);
            }
        });
    }
}
