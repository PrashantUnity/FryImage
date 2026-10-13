using System;
using System.Threading.Tasks;
using Avalonia;

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
    PushLeft,
    Freeze,
    Thaw
}

/// <summary>
/// Professional Liquify 2D displacement mesh.
/// Implements sub-pixel inverse mapping, stationary and dragging deformation tools,
/// and fast multithreaded image resampling.
/// </summary>
public sealed class LiquifyMesh
{
    public const double CellSize = 8.0;

    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private readonly int _cols;
    private readonly int _rows;

    private readonly float[] _dx;
    private readonly float[] _dy;
    private readonly float[] _freeze;

    public int Cols => _cols;
    public int Rows => _rows;
    public float[] Dx => _dx;
    public float[] Dy => _dy;
    public float[] Freeze => _freeze;

    public LiquifyMesh(int imageWidth, int imageHeight)
    {
        _imageWidth = Math.Max(1, imageWidth);
        _imageHeight = Math.Max(1, imageHeight);

        _cols = (int)Math.Ceiling(_imageWidth / CellSize) + 2;
        _rows = (int)Math.Ceiling(_imageHeight / CellSize) + 2;

        int totalNodes = _cols * _rows;
        _dx = new float[totalNodes];
        _dy = new float[totalNodes];
        _freeze = new float[totalNodes];
    }

    /// <summary>
    /// Gets document coordinate for grid node (c, r).
    /// </summary>
    public Point NodePos(int c, int r) =>
        new(c * CellSize, r * CellSize);

    /// <summary>
    /// Converts document coordinate to grid coordinates.
    /// </summary>
    public (double Gc, double Gr) ToGrid(double x, double y) =>
        (x / CellSize, y / CellSize);

    private Point? _lastStrokeEnd;
    private double _carry = 0.0;

    /// <summary>
    /// Resets continuous stroke spacing accumulator.
    /// </summary>
    public void ResetStroke()
    {
        _lastStrokeEnd = null;
        _carry = 0.0;
    }

    /// <summary>
    /// Photoshop falloff curve: blends (1 - t^2)^3 at Density 0 towards (1 - t^4)^2 at Density 100.
    /// Fades strictly to 0 at the rim with 0 slope, preventing sawtooth edge tears (Commit 599aea00 parity).
    /// </summary>
    public static double Falloff(double t2, double density)
    {
        t2 = Math.Clamp(t2, 0.0, 1.0);
        density = double.IsNaN(density) ? 0.5 : Math.Clamp(density, 0.0, 1.0);
        double oneMinusT2 = 1.0 - t2;
        double feathered = oneMinusT2 * oneMinusT2 * oneMinusT2;
        double oneMinusT4 = 1.0 - t2 * t2;
        double dense = oneMinusT4 * oneMinusT4;
        return feathered + (dense - feathered) * density;
    }

    /// <summary>
    /// Applies a brush stroke dab or segment between startPt and endPt.
    /// Implements stroke-continuous spacing (Commit 32552a61 parity) so fast and slow pointer movements
    /// produce identical smooth deformation fields.
    /// </summary>
    public void ApplyDab(
        LiquifyTool tool,
        Point startPt,
        Point endPt,
        double brushSize,
        double density = 50.0,
        double pressure = 80.0,
        double rate = 80.0)
    {
        double radius = Math.Max(1.0, brushSize / 2.0);
        double dx = endPt.X - startPt.X;
        double dy = endPt.Y - startPt.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        bool isStationary = tool != LiquifyTool.ForwardWarp && tool != LiquifyTool.PushLeft;

        if (dist <= 0.5)
        {
            ApplySingleDab(tool, endPt, new Avalonia.Vector(0, 0), radius, density, pressure, rate, 1.0);
            _lastStrokeEnd = endPt;
            return;
        }

        double spacing = Math.Max(0.5, radius * 0.05);
        double weight = isStationary ? (spacing / Math.Max(0.5, radius * 0.2)) : 1.0;

        if (_lastStrokeEnd == null || Point.Distance(_lastStrokeEnd.Value, startPt) > 1.0)
        {
            _carry = 0.0;
        }

        double first = Math.Clamp(spacing - _carry, 0.0, spacing);
        double slack = spacing * 1e-6;

        if (first > dist + slack)
        {
            _carry += dist;
            _lastStrokeEnd = endPt;
            return;
        }

        double lastDabDist = 0;
        for (double s = first; s <= dist + slack; s += spacing)
        {
            double t = Math.Clamp(s / dist, 0.0, 1.0);
            Point curr = new(startPt.X + dx * t, startPt.Y + dy * t);
            Avalonia.Vector delta = new(dx * (spacing / dist), dy * (spacing / dist));
            Point center = tool == LiquifyTool.ForwardWarp ? new(curr.X - delta.X, curr.Y - delta.Y) : curr;

            ApplySingleDab(tool, center, delta, radius, density, pressure, rate, weight);
            lastDabDist = s;
        }

        _carry = Math.Max(0.0, dist - lastDabDist);
        _lastStrokeEnd = endPt;
    }

    private void ApplySingleDab(
        LiquifyTool tool,
        Point center,
        Avalonia.Vector delta,
        double radius,
        double density,
        double pressure,
        double rate,
        double weight = 1.0)
    {
        double strength = Math.Clamp(pressure / 100.0, 0.0, 1.0) * weight;
        double rateFactor = Math.Clamp(rate / 100.0, 0.0, 1.0);
        double dens = Math.Clamp(density / 100.0, 0.0, 1.0);

        if (strength <= 0.0) return;

        var (minGc, minGr) = ToGrid(center.X - radius, center.Y - radius);
        var (maxGc, maxGr) = ToGrid(center.X + radius, center.Y + radius);

        int c0 = Math.Max(0, (int)Math.Floor(minGc));
        int r0 = Math.Max(0, (int)Math.Floor(minGr));
        int c1 = Math.Min(_cols - 1, (int)Math.Ceiling(maxGc));
        int r1 = Math.Min(_rows - 1, (int)Math.Ceiling(maxGr));

        if (c1 < c0 || r1 < r0) return;

        for (int r = r0; r <= r1; r++)
        {
            int row = r * _cols;
            for (int c = c0; c <= c1; c++)
            {
                var p = NodePos(c, r);
                double distSq = (p.X - center.X) * (p.X - center.X) + (p.Y - center.Y) * (p.Y - center.Y);
                double t2 = distSq / (radius * radius);
                if (t2 >= 1.0) continue;

                double fall = Falloff(t2, dens);

                int idx = row + c;
                double kRaw = strength * fall;

                if (tool == LiquifyTool.Freeze)
                {
                    _freeze[idx] = (float)Math.Min(1.0, _freeze[idx] + kRaw);
                    continue;
                }
                if (tool == LiquifyTool.Thaw)
                {
                    _freeze[idx] = (float)Math.Max(0.0, _freeze[idx] - kRaw);
                    continue;
                }

                double k = kRaw * (1.0 - _freeze[idx]);
                if (k <= 0.0) continue;

                float curX = _dx[idx];
                float curY = _dy[idx];

                if (tool == LiquifyTool.Reconstruct)
                {
                    float a = (float)Math.Max(0.0, 1.0 - k * (0.1 + 0.4 * rateFactor));
                    _dx[idx] = curX * a;
                    _dy[idx] = curY * a;
                }
                else if (tool == LiquifyTool.Smooth)
                {
                    float sumX = 0, sumY = 0;
                    int count = 0;
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            if (dr == 0 && dc == 0) continue;
                            int nc = Math.Clamp(c + dc, 0, _cols - 1);
                            int nr = Math.Clamp(r + dr, 0, _rows - 1);
                            int nidx = nr * _cols + nc;
                            sumX += _dx[nidx];
                            sumY += _dy[nidx];
                            count++;
                        }
                    }
                    float meanX = sumX / count;
                    float meanY = sumY / count;
                    float blend = (float)(k * (0.1 + 0.4 * rateFactor));
                    _dx[idx] = curX + (meanX - curX) * blend;
                    _dy[idx] = curY + (meanY - curY) * blend;
                }
                else
                {
                    double qX, qY;
                    double dx = p.X - center.X;
                    double dy = p.Y - center.Y;

                    switch (tool)
                    {
                        case LiquifyTool.ForwardWarp:
                            qX = p.X - k * delta.X;
                            qY = p.Y - k * delta.Y;
                            break;

                        case LiquifyTool.PushLeft:
                            qX = p.X - k * delta.Y;
                            qY = p.Y + k * delta.X;
                            break;

                        case LiquifyTool.TwirlCw:
                        case LiquifyTool.TwirlCcw:
                            double th = k * rateFactor * 0.12 * (tool == LiquifyTool.TwirlCw ? 1.0 : -1.0);
                            double cos = Math.Cos(-th);
                            double sin = Math.Sin(-th);
                            qX = center.X + dx * cos - dy * sin;
                            qY = center.Y + dx * sin + dy * cos;
                            break;

                        case LiquifyTool.Pucker:
                            double puckerScale = 1.0 + k * rateFactor * 0.06;
                            qX = center.X + dx * puckerScale;
                            qY = center.Y + dy * puckerScale;
                            break;

                        case LiquifyTool.Bloat:
                        default:
                            double bloatScale = 1.0 - k * rateFactor * 0.06;
                            qX = center.X + dx * bloatScale;
                            qY = center.Y + dy * bloatScale;
                            break;
                    }

                    var (qGc, qGr) = ToGrid(qX, qY);
                    SampleDisplacementBilinear(qGc, qGr, out double sampleDx, out double sampleDy);

                    _dx[idx] = (float)(qX + sampleDx - p.X);
                    _dy[idx] = (float)(qY + sampleDy - p.Y);
                }
            }
        }
    }

    /// <summary>
    /// Deforms the source BGRA buffer into outputBgra using sub-pixel bilinear sampling through the displacement field.
    /// </summary>
    public void RenderDeformedImage(byte[] sourceBgra, byte[] outputBgra)
    {
        double invCell = 1.0 / CellSize;

        Parallel.For(0, _imageHeight, y =>
        {
            double py = y + 0.5;
            double gy = py * invCell;

            for (int x = 0; x < _imageWidth; x++)
            {
                double px = x + 0.5;
                double gx = px * invCell;

                SampleDisplacementBilinear(gx, gy, out double dispX, out double dispY);

                double sx = Math.Clamp(px + dispX, 0.0, _imageWidth - 1.0001);
                double sy = Math.Clamp(py + dispY, 0.0, _imageHeight - 1.0001);

                int x0 = (int)Math.Floor(sx);
                int y0 = (int)Math.Floor(sy);
                int x1 = Math.Min(x0 + 1, _imageWidth - 1);
                int y1 = Math.Min(y0 + 1, _imageHeight - 1);

                double fx = sx - x0;
                double fy = sy - y0;

                int idx00 = (y0 * _imageWidth + x0) * 4;
                int idx10 = (y0 * _imageWidth + x1) * 4;
                int idx01 = (y1 * _imageWidth + x0) * 4;
                int idx11 = (y1 * _imageWidth + x1) * 4;

                double w00 = (1.0 - fx) * (1.0 - fy);
                double w10 = fx * (1.0 - fy);
                double w01 = (1.0 - fx) * fy;
                double w11 = fx * fy;

                int outIdx = (y * _imageWidth + x) * 4;

                outputBgra[outIdx] = (byte)Math.Clamp((int)Math.Round(
                    sourceBgra[idx00] * w00 + sourceBgra[idx10] * w10 + sourceBgra[idx01] * w01 + sourceBgra[idx11] * w11), 0, 255);
                outputBgra[outIdx + 1] = (byte)Math.Clamp((int)Math.Round(
                    sourceBgra[idx00 + 1] * w00 + sourceBgra[idx10 + 1] * w10 + sourceBgra[idx01 + 1] * w01 + sourceBgra[idx11 + 1] * w11), 0, 255);
                outputBgra[outIdx + 2] = (byte)Math.Clamp((int)Math.Round(
                    sourceBgra[idx00 + 2] * w00 + sourceBgra[idx10 + 2] * w10 + sourceBgra[idx01 + 2] * w01 + sourceBgra[idx11 + 2] * w11), 0, 255);
                outputBgra[outIdx + 3] = (byte)Math.Clamp((int)Math.Round(
                    sourceBgra[idx00 + 3] * w00 + sourceBgra[idx10 + 3] * w10 + sourceBgra[idx01 + 3] * w01 + sourceBgra[idx11 + 3] * w11), 0, 255);
            }
        });
    }

    public void ReconstructAll(double percentage)
    {
        float factor = (float)Math.Clamp(percentage / 100.0, 0.0, 1.0);
        for (int i = 0; i < _dx.Length; i++)
        {
            float m = 1.0f - factor * (1.0f - _freeze[i]);
            _dx[i] *= m;
            _dy[i] *= m;
        }
    }

    public void Reset()
    {
        Array.Clear(_dx);
        Array.Clear(_dy);
        Array.Clear(_freeze);
    }

    private void SampleDisplacementBilinear(double gc, double gr, out double dispX, out double dispY)
    {
        double cgc = Math.Clamp(gc, 0.0, _cols - 1.0001);
        double cgr = Math.Clamp(gr, 0.0, _rows - 1.0001);

        int c0 = (int)Math.Floor(cgc);
        int r0 = (int)Math.Floor(cgr);
        int c1 = Math.Min(c0 + 1, _cols - 1);
        int r1 = Math.Min(r0 + 1, _rows - 1);

        double fc = cgc - c0;
        double fr = cgr - r0;

        int idx00 = r0 * _cols + c0;
        int idx10 = r0 * _cols + c1;
        int idx01 = r1 * _cols + c0;
        int idx11 = r1 * _cols + c1;

        double w00 = (1.0 - fc) * (1.0 - fr);
        double w10 = fc * (1.0 - fr);
        double w01 = (1.0 - fc) * fr;
        double w11 = fc * fr;

        dispX = _dx[idx00] * w00 + _dx[idx10] * w10 + _dx[idx01] * w01 + _dx[idx11] * w11;
        dispY = _dy[idx00] * w00 + _dy[idx10] * w10 + _dy[idx01] * w01 + _dy[idx11] * w11;
    }
}
