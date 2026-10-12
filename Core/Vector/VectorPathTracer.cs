using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Vector;

/// <summary>
/// Converts raster selection masks / bitmaps into smooth cubic Bezier vector work paths
/// ("Make Work Path from Selection") using Marching Squares contouring and Schneider's
/// least-squares curve fitting algorithm.
/// </summary>
public static class VectorPathTracer
{
    private record struct P(double X, double Y);

    /// <summary>
    /// Traces the 0.5 iso-contour of a mask buffer and fits cubic Bezier curves within tolerance.
    /// </summary>
    public static VectorDocumentPath TraceMaskToPath(
        float[] maskValues,
        int width,
        int height,
        double tolerance = 2.0,
        string pathName = "Work Path")
    {
        var rawContours = ExtractContours(maskValues, width, height, 0.5f);
        var subpaths = new List<VectorSubpath>();

        for (int i = 0; i < rawContours.Count; i++)
        {
            var loop = rawContours[i];
            if (loop.Count < 3) continue;

            var subpath = FitClosedLoop(loop, Math.Max(0.1, tolerance * 0.5), 1.5);
            subpath.Operation = i == 0 ? PathBooleanOp.Combine : PathBooleanOp.Exclude;
            subpaths.Add(subpath);
        }

        return new VectorDocumentPath(pathName, subpaths);
    }

    /// <summary>
    /// Fits a polygon of points directly into a smooth cubic Bezier vector path.
    /// </summary>
    public static VectorDocumentPath FitPolygonToPath(
        IReadOnlyList<Point> points,
        double tolerance = 2.0,
        bool isClosed = true,
        string pathName = "Work Path")
    {
        if (points.Count < 2) return new VectorDocumentPath(pathName);

        var loop = points.Select(p => new P(p.X, p.Y)).ToList();
        var subpath = isClosed
            ? FitClosedLoop(loop, tolerance, 1.5)
            : FitOpenPolyline(loop, tolerance, 1.5);

        return new VectorDocumentPath(pathName, [subpath]);
    }

    #region Marching Squares Contouring

    private static List<List<P>> ExtractContours(float[] values, int width, int height, float level = 0.5f)
    {
        float Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return 0f;
            return values[y * width + x];
        }

        P EdgePoint((int X, int Y, byte Edge) e)
        {
            var (aX, aY) = (e.X, e.Y);
            var (bX, bY) = e.Edge == 0 ? (e.X + 1, e.Y) : (e.X, e.Y + 1);
            float va = Get(aX, aY);
            float vb = Get(bX, bY);
            double t = Math.Abs(vb - va) > 1e-6 ? Math.Clamp((level - va) / (vb - va), 0.0, 1.0) : 0.5;
            double ax = aX + 0.5, ay = aY + 0.5;
            double bx = bX + 0.5, by = bY + 0.5;
            return new P(ax + (bx - ax) * t, ay + (by - ay) * t);
        }

        var nextMap = new Dictionary<(int, int, byte), (int, int, byte)>();

        for (int cy = -1; cy < height; cy++)
        {
            for (int cx = -1; cx < width; cx++)
            {
                bool tl = Get(cx, cy) >= level;
                bool tr = Get(cx + 1, cy) >= level;
                bool br = Get(cx + 1, cy + 1) >= level;
                bool bl = Get(cx, cy + 1) >= level;

                int idx = (tl ? 1 : 0) | (tr ? 2 : 0) | (br ? 4 : 0) | (bl ? 8 : 0);
                if (idx == 0 || idx == 15) continue;

                var top = (cx, cy, (byte)0);
                var bottom = (cx, cy + 1, (byte)0);
                var left = (cx, cy, (byte)1);
                var right = (cx + 1, cy, (byte)1);

                switch (idx)
                {
                    case 1: nextMap[left] = top; break;
                    case 2: nextMap[top] = right; break;
                    case 3: nextMap[left] = right; break;
                    case 4: nextMap[right] = bottom; break;
                    case 5:
                        float center5 = (Get(cx, cy) + Get(cx + 1, cy) + Get(cx + 1, cy + 1) + Get(cx, cy + 1)) / 4.0f;
                        if (center5 >= level) { nextMap[left] = bottom; nextMap[right] = top; }
                        else { nextMap[left] = top; nextMap[right] = bottom; }
                        break;
                    case 6: nextMap[top] = bottom; break;
                    case 7: nextMap[left] = bottom; break;
                    case 8: nextMap[bottom] = left; break;
                    case 9: nextMap[bottom] = top; break;
                    case 10:
                        float center10 = (Get(cx, cy) + Get(cx + 1, cy) + Get(cx + 1, cy + 1) + Get(cx, cy + 1)) / 4.0f;
                        if (center10 >= level) { nextMap[top] = left; nextMap[bottom] = right; }
                        else { nextMap[top] = right; nextMap[bottom] = left; }
                        break;
                    case 11: nextMap[bottom] = right; break;
                    case 12: nextMap[right] = left; break;
                    case 13: nextMap[right] = top; break;
                    case 14: nextMap[top] = left; break;
                }
            }
        }

        var keys = nextMap.Keys.OrderBy(k => k.Item2).ThenBy(k => k.Item1).ThenBy(k => k.Item3).ToList();
        var contours = new List<List<P>>();

        foreach (var start in keys)
        {
            if (!nextMap.ContainsKey(start)) continue;

            var loop = new List<P>();
            var curr = start;

            while (nextMap.Remove(curr, out var next))
            {
                loop.Add(EdgePoint(curr));
                curr = next;
                if (curr.Equals(start)) break;
            }

            // Deduplicate consecutive identical points
            var deduped = new List<P>();
            foreach (var p in loop)
            {
                if (deduped.Count == 0 || Math.Abs(deduped[^1].X - p.X) > 1e-6 || Math.Abs(deduped[^1].Y - p.Y) > 1e-6)
                    deduped.Add(p);
            }

            if (deduped.Count >= 3)
                contours.Add(deduped);
        }

        return contours;
    }

    #endregion

    #region Schneider Curve Fitting

    private static VectorSubpath FitClosedLoop(List<P> loop, double tolerance, double window)
    {
        int n = loop.Count;
        var cornerIndices = FindCorners(loop, window, isClosed: true);
        var segments = new List<P[]>();
        var cornerAtStart = new List<bool>();

        if (cornerIndices.Count == 0)
        {
            var pts = new List<P>(loop) { loop[0] };
            var tangent = Normalize(Subtract(loop[1 % n], loop[n - 1]));
            int before = segments.Count;
            FitCubic(pts, tangent, Multiply(tangent, -1.0), tolerance * tolerance, segments, 0);
            for (int k = before; k < segments.Count; k++) cornerAtStart.Add(false);
        }
        else
        {
            for (int k = 0; k < cornerIndices.Count; k++)
            {
                int c = cornerIndices[k];
                int end = cornerIndices[(k + 1) % cornerIndices.Count];

                var pts = new List<P> { loop[c] };
                int j = c;
                while (true)
                {
                    j = (j + 1) % n;
                    pts.Add(loop[j]);
                    if (j == end) break;
                }

                int m = pts.Count;
                var t1 = Normalize(Subtract(pts[Math.Min(1, m - 1)], pts[0]));
                var t2 = Normalize(Subtract(pts[Math.Max(0, m - 2)], pts[m - 1]));

                int before = segments.Count;
                FitCubic(pts, t1, t2, tolerance * tolerance, segments, 0);
                for (int q = before; q < segments.Count; q++) cornerAtStart.Add(q == before);
            }
        }

        var knots = new List<PathKnot>(segments.Count);
        for (int i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            knots.Add(new PathKnot(
                new Point(s[0].X, s[0].Y),
                new Point(s[0].X, s[0].Y),
                new Point(s[1].X, s[1].Y),
                !cornerAtStart[i]));
        }

        int segCount = segments.Count;
        for (int i = 0; i < segCount; i++)
        {
            var prevSeg = segments[i];
            knots[(i + 1) % segCount].HandleIn = new Point(prevSeg[2].X, prevSeg[2].Y);
        }

        return new VectorSubpath(knots, isClosed: true);
    }

    private static VectorSubpath FitOpenPolyline(List<P> poly, double tolerance, double window)
    {
        int n = poly.Count;
        var cuts = new List<int> { 0 };
        cuts.AddRange(FindCorners(poly, window, isClosed: false));
        cuts.Add(n - 1);
        cuts = cuts.Distinct().OrderBy(c => c).ToList();

        var segments = new List<P[]>();
        var cornerAtStart = new List<bool>();

        for (int w = 0; w < cuts.Count - 1; w++)
        {
            int startIdx = cuts[w];
            int endIdx = cuts[w + 1];
            var pts = poly.GetRange(startIdx, endIdx - startIdx + 1);
            if (pts.Count < 2) continue;

            int m = pts.Count;
            var t1 = Normalize(Subtract(pts[1], pts[0]));
            var t2 = Normalize(Subtract(pts[m - 2], pts[m - 1]));

            int before = segments.Count;
            FitCubic(pts, t1, t2, tolerance * tolerance, segments, 0);
            for (int q = before; q < segments.Count; q++) cornerAtStart.Add(q == before);
        }

        var knots = new List<PathKnot>(segments.Count + 1);
        for (int i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            var inHandle = i == 0 ? new Point(s[0].X, s[0].Y) : new Point(segments[i - 1][2].X, segments[i - 1][2].Y);
            knots.Add(new PathKnot(
                new Point(s[0].X, s[0].Y),
                inHandle,
                new Point(s[1].X, s[1].Y),
                i > 0 && !cornerAtStart[i]));
        }

        if (segments.Count > 0)
        {
            var lastSeg = segments[^1];
            knots.Add(new PathKnot(
                new Point(lastSeg[3].X, lastSeg[3].Y),
                new Point(lastSeg[2].X, lastSeg[2].Y),
                new Point(lastSeg[3].X, lastSeg[3].Y),
                isSmooth: false));
        }

        return new VectorSubpath(knots, isClosed: false);
    }

    private static List<int> FindCorners(List<P> loop, double window, bool isClosed)
    {
        int n = loop.Count;
        double cosThreshold = Math.Cos(60.0 * Math.PI / 180.0);
        double[] turns = new double[n];

        for (int i = 0; i < n; i++)
        {
            if (!isClosed && (i == 0 || i + 1 == n))
            {
                turns[i] = 1.0;
                continue;
            }
            var a = Normalize(Subtract(loop[i], WalkAlongPolyline(loop, i, window, forward: false, isClosed)));
            var b = Normalize(Subtract(WalkAlongPolyline(loop, i, window, forward: true, isClosed), loop[i]));
            turns[i] = Dot(a, b);
        }

        var corners = new List<int>();
        for (int i = 0; i < n; i++)
        {
            if (turns[i] >= cosThreshold) continue;

            bool isMin = true;
            double acc = 0.0;
            int j = i;
            while (acc < window)
            {
                if (!isClosed && j + 1 >= n) break;
                int nx = (j + 1) % n;
                acc += Length(Subtract(loop[nx], loop[j]));
                j = nx;
                if (j == i) break;
                if (turns[j] < turns[i]) { isMin = false; break; }
            }

            acc = 0.0;
            j = i;
            while (isMin && acc < window)
            {
                if (!isClosed && j == 0) break;
                int pv = (j + n - 1) % n;
                acc += Length(Subtract(loop[j], loop[pv]));
                j = pv;
                if (j == i) break;
                if (turns[j] <= turns[i]) { isMin = false; break; }
            }

            if (isMin) corners.Add(i);
        }

        return corners;
    }

    private static P WalkAlongPolyline(List<P> lp, int i, double window, bool forward, bool isClosed)
    {
        int n = lp.Count;
        double acc = 0.0;
        int cur = i;

        for (int step = 0; step < n; step++)
        {
            if (!isClosed && (forward ? cur + 1 >= n : cur == 0)) break;
            int nx = forward ? (cur + 1) % n : (cur + n - 1) % n;
            double d = Length(Subtract(lp[nx], lp[cur]));
            if (acc + d >= window)
            {
                double t = (window - acc) / Math.Max(1e-12, d);
                return Add(lp[cur], Multiply(Subtract(lp[nx], lp[cur]), t));
            }
            acc += d;
            cur = nx;
        }

        return lp[cur];
    }

    private static void FitCubic(List<P> pts, P t1, P t2, double err2, List<P[]> segments, int depth)
    {
        int n = pts.Count;
        if (n == 2 || depth > 32)
        {
            double d = Length(Subtract(pts[^1], pts[0])) / 3.0;
            segments.Add([pts[0], Add(pts[0], Multiply(t1, d)), Add(pts[^1], Multiply(t2, d)), pts[^1]]);
            return;
        }

        double[] u = ChordParams(pts);
        P[] bez = GenerateBezier(pts, u, t1, t2);
        var (maxErr, splitIdx) = MaxError(pts, bez, u);

        if (maxErr < err2)
        {
            segments.Add(bez);
            return;
        }

        if (maxErr < err2 * 16.0)
        {
            for (int iter = 0; iter < 5; iter++)
            {
                u = Reparameterize(pts, u, bez);
                bez = GenerateBezier(pts, u, t1, t2);
                var (newErr, newSplit) = MaxError(pts, bez, u);
                maxErr = newErr;
                splitIdx = newSplit;
                if (maxErr < err2)
                {
                    segments.Add(bez);
                    return;
                }
            }
        }

        int split = Math.Clamp(splitIdx, 1, n - 2);
        P tc = Normalize(Subtract(pts[split - 1], pts[split + 1]));
        if (Length(tc) < 0.5) tc = Normalize(Subtract(pts[split - 1], pts[split]));

        FitCubic(pts.GetRange(0, split + 1), t1, tc, err2, segments, depth + 1);
        FitCubic(pts.GetRange(split, n - split), Multiply(tc, -1.0), t2, err2, segments, depth + 1);
    }

    private static double[] ChordParams(List<P> pts)
    {
        double[] u = new double[pts.Count];
        for (int i = 1; i < pts.Count; i++)
            u[i] = u[i - 1] + Length(Subtract(pts[i], pts[i - 1]));
        double total = u[^1];
        if (total > 0.0)
        {
            for (int i = 0; i < u.Length; i++) u[i] /= total;
        }
        return u;
    }

    private static P[] GenerateBezier(List<P> pts, double[] u, P t1, P t2)
    {
        P p0 = pts[0], p3 = pts[^1];
        double c00 = 0, c01 = 0, c11 = 0;
        double x0 = 0, x1 = 0;

        for (int i = 0; i < u.Length; i++)
        {
            double t = u[i];
            double mt = 1.0 - t;
            double b1 = 3.0 * mt * mt * t;
            double b2 = 3.0 * mt * t * t;
            P a1 = Multiply(t1, b1);
            P a2 = Multiply(t2, b2);

            c00 += Dot(a1, a1);
            c01 += Dot(a1, a2);
            c11 += Dot(a2, a2);

            double b0 = mt * mt * mt;
            double b3 = t * t * t;
            P tmp = Subtract(pts[i], Add(Multiply(p0, b0 + b1), Multiply(p3, b2 + b3)));
            x0 += Dot(a1, tmp);
            x1 += Dot(a2, tmp);
        }

        double det = c00 * c11 - c01 * c01;
        double det0 = c00 * x1 - c01 * x0;
        double det1 = x0 * c11 - x1 * c01;

        double alphaL = Math.Abs(det) > 1e-12 ? det1 / det : 0.0;
        double alphaR = Math.Abs(det) > 1e-12 ? det0 / det : 0.0;

        double segLen = Length(Subtract(p3, p0));
        double eps = 1e-6 * segLen;
        if (alphaL < eps || alphaR < eps)
        {
            alphaL = segLen / 3.0;
            alphaR = segLen / 3.0;
        }

        return [p0, Add(p0, Multiply(t1, alphaL)), Add(p3, Multiply(t2, alphaR)), p3];
    }

    private static (double MaxError, int SplitIndex) MaxError(List<P> pts, P[] bez, double[] u)
    {
        double maxE = 0.0;
        int split = pts.Count / 2;

        for (int i = 1; i < pts.Count - 1; i++)
        {
            P q = EvalBezier(bez, u[i]);
            P d = Subtract(q, pts[i]);
            double e = Dot(d, d);
            if (e >= maxE)
            {
                maxE = e;
                split = i;
            }
        }
        return (maxE, split);
    }

    private static double[] Reparameterize(List<P> pts, double[] u, P[] bez)
    {
        double[] newU = new double[u.Length];
        for (int i = 0; i < u.Length; i++)
        {
            double t = u[i];
            P q = EvalBezier(bez, t);
            P d1 = EvalBezierDeriv1(bez, t);
            P d2 = EvalBezierDeriv2(bez, t);
            double num = Dot(Subtract(q, pts[i]), d1);
            double den = Dot(d1, d1) + Dot(Subtract(q, pts[i]), d2);
            newU[i] = Math.Abs(den) < 1e-12 ? t : Math.Clamp(t - num / den, 0.0, 1.0);
        }
        return newU;
    }

    private static P EvalBezier(P[] b, double t)
    {
        double mt = 1.0 - t;
        double c0 = mt * mt * mt;
        double c1 = 3.0 * mt * mt * t;
        double c2 = 3.0 * mt * t * t;
        double c3 = t * t * t;
        return new P(
            c0 * b[0].X + c1 * b[1].X + c2 * b[2].X + c3 * b[3].X,
            c0 * b[0].Y + c1 * b[1].Y + c2 * b[2].Y + c3 * b[3].Y);
    }

    private static P EvalBezierDeriv1(P[] b, double t)
    {
        double mt = 1.0 - t;
        P q0 = Multiply(Subtract(b[1], b[0]), 3.0);
        P q1 = Multiply(Subtract(b[2], b[1]), 3.0);
        P q2 = Multiply(Subtract(b[3], b[2]), 3.0);
        return Add(Add(Multiply(q0, mt * mt), Multiply(q1, 2.0 * mt * t)), Multiply(q2, t * t));
    }

    private static P EvalBezierDeriv2(P[] b, double t)
    {
        P r0 = Multiply(Add(Subtract(b[2], Multiply(b[1], 2.0)), b[0]), 6.0);
        P r1 = Multiply(Add(Subtract(b[3], Multiply(b[2], 2.0)), b[1]), 6.0);
        return Add(Multiply(r0, 1.0 - t), Multiply(r1, t));
    }

    private static P Add(P a, P b) => new(a.X + b.X, a.Y + b.Y);
    private static P Subtract(P a, P b) => new(a.X - b.X, a.Y - b.Y);
    private static P Multiply(P a, double k) => new(a.X * k, a.Y * k);
    private static double Dot(P a, P b) => a.X * b.X + a.Y * b.Y;
    private static double Length(P a) => Math.Sqrt(a.X * a.X + a.Y * a.Y);
    private static P Normalize(P a)
    {
        double l = Length(a);
        return l > 1e-12 ? new P(a.X / l, a.Y / l) : new P(0, 0);
    }

    #endregion
}
