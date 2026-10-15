using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Vector;

/// <summary>
/// Type on a Path engine (Studio text curve deformation specification).
/// Evaluates cubic Bezier subpaths by arc-length, placing glyphs rigidly on the curve
/// aligned to tangents and normals.
/// </summary>
public static class PathTextEngine
{
    public sealed class GlyphPlacement
    {
        public char Character { get; init; }
        public Point Position { get; init; }
        public double AngleRadians { get; init; }
        public double AngleDegrees => AngleRadians * (180.0 / Math.PI);
        public bool IsVisible { get; init; }
    }

    public sealed class FlattenedPathFrame
    {
        private readonly List<Point> _pts = [];
        private readonly List<double> _cum = [];
        private readonly double _startOffset;

        public double TotalLength => _cum.Count > 0 ? _cum[^1] : 0.0;

        public FlattenedPathFrame(IEnumerable<PathKnot> knots, bool isClosed, double startOffset = 0.0, bool flip = false)
        {
            _startOffset = startOffset;
            var knotList = new List<PathKnot>(knots);
            if (knotList.Count < 2) return;

            int segCount = isClosed ? knotList.Count : knotList.Count - 1;
            for (int s = 0; s < segCount; s++)
            {
                var k0 = knotList[s];
                var k1 = knotList[(s + 1) % knotList.Count];

                Point p0 = k0.Anchor;
                Point p1 = k0.HasHandleOut ? k0.HandleOut : k0.Anchor;
                Point p2 = k1.HasHandleIn ? k1.HandleIn : k1.Anchor;
                Point p3 = k1.Anchor;

                double poly = Distance(p0, p1) + Distance(p1, p2) + Distance(p2, p3);
                int n = (int)Math.Clamp(Math.Ceiling(poly / 3.0), 6, 96);

                if (_pts.Count == 0) _pts.Add(p0);

                for (int i = 1; i <= n; i++)
                {
                    double t = (double)i / n;
                    double u = 1.0 - t;
                    double a = u * u * u;
                    double b = 3.0 * u * u * t;
                    double c = 3.0 * u * t * t;
                    double d = t * t * t;

                    double x = a * p0.X + b * p1.X + c * p2.X + d * p3.X;
                    double y = a * p0.Y + b * p1.Y + c * p2.Y + d * p3.Y;
                    _pts.Add(new Point(x, y));
                }
            }

            if (flip)
            {
                _pts.Reverse();
            }

            double acc = 0.0;
            _cum.Add(0.0);
            for (int i = 1; i < _pts.Count; i++)
            {
                acc += Distance(_pts[i - 1], _pts[i]);
                _cum.Add(acc);
            }
        }

        private static double Distance(Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public (Point Position, Point Tangent) SampleAt(double s)
        {
            if (_pts.Count == 0) return (default, new Point(1, 0));
            if (_pts.Count == 1) return (_pts[0], new Point(1, 0));

            int last = _pts.Count - 1;
            int idx = _cum.BinarySearch(s);
            if (idx < 0) idx = ~idx;
            idx = Math.Clamp(idx, 1, last);

            Point a = _pts[idx - 1];
            Point b = _pts[idx];
            double segLen = Math.Max(1e-9, (_cum[idx] - _cum[idx - 1]));
            Point t = new Point((b.X - a.X) / segLen, (b.Y - a.Y) / segLen);
            double along = s - _cum[idx - 1];

            return (new Point(a.X + t.X * along, a.Y + t.Y * along), t);
        }

        public List<GlyphPlacement> LayoutText(string text, double fontSize, double charSpacing = 0.0)
        {
            var results = new List<GlyphPlacement>();
            if (string.IsNullOrEmpty(text) || TotalLength <= 1e-4) return results;

            double advanceEstimate = fontSize * 0.58 + charSpacing;
            double currentS = _startOffset;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                double charAdvance = ch == ' ' ? fontSize * 0.35 : advanceEstimate;
                double glyphCenterS = currentS + charAdvance * 0.5;

                bool visible = glyphCenterS >= 0.0 && glyphCenterS <= TotalLength;
                var (pos, tan) = SampleAt(glyphCenterS);

                double angle = Math.Atan2(tan.Y, tan.X);

                results.Add(new GlyphPlacement
                {
                    Character = ch,
                    Position = pos,
                    AngleRadians = angle,
                    IsVisible = visible
                });

                currentS += charAdvance;
            }

            return results;
        }
    }
}
