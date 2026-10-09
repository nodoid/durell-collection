using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Durell.Graphics.Art;

/// <summary>Vector outlines for <see cref="Canvas"/>: lines, Bézier curves, ellipses, rounded boxes.</summary>
internal sealed class Path
{
    internal sealed class Contour
    {
        public readonly List<Vector2> Points = new();
        public bool Closed;
    }

    private readonly List<Contour> _contours = new();
    private Contour? _cur;

    public IReadOnlyList<Contour> Contours => _contours;

    public Path MoveTo(float x, float y) => MoveTo(new Vector2(x, y));

    public Path MoveTo(Vector2 p)
    {
        _cur = new Contour();
        _cur.Points.Add(p);
        _contours.Add(_cur);
        return this;
    }

    public Path LineTo(float x, float y) => LineTo(new Vector2(x, y));

    public Path LineTo(Vector2 p)
    {
        if (_cur == null) return MoveTo(p);
        _cur.Points.Add(p);
        return this;
    }

    private Vector2 Last => _cur!.Points[^1];

    public Path QuadTo(float cx, float cy, float x, float y)
    {
        var p0 = Last;
        var c = new Vector2(cx, cy);
        var p1 = new Vector2(x, y);
        int n = Steps(p0, c, c, p1);
        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)n, u = 1 - t;
            LineTo(u * u * p0 + 2 * u * t * c + t * t * p1);
        }
        return this;
    }

    public Path CubicTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
    {
        var p0 = Last;
        var c1 = new Vector2(c1x, c1y);
        var c2 = new Vector2(c2x, c2y);
        var p1 = new Vector2(x, y);
        int n = Steps(p0, c1, c2, p1);
        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)n, u = 1 - t;
            LineTo(u * u * u * p0 + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * p1);
        }
        return this;
    }

    private static int Steps(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float len = (b - a).Length() + (c - b).Length() + (d - c).Length();
        return Math.Clamp((int)(len / 2), 4, 64);
    }

    public Path Close()
    {
        if (_cur != null) _cur.Closed = true;
        _cur = null;
        return this;
    }

    /// <summary>A smooth closed curve through the points (Catmull-Rom).</summary>
    public Path Smooth(params Vector2[] pts) => Smooth(true, pts);

    public Path Smooth(bool closed, params Vector2[] pts)
    {
        int n = pts.Length;
        if (n < 2) return this;
        MoveTo(pts[0]);
        int segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            var p0 = pts[closed ? (i - 1 + n) % n : Math.Max(0, i - 1)];
            var p1 = pts[i];
            var p2 = pts[(i + 1) % n];
            var p3 = pts[closed ? (i + 2) % n : Math.Min(n - 1, i + 2)];
            var c1 = p1 + (p2 - p0) / 6;
            var c2 = p2 - (p3 - p1) / 6;
            CubicTo(c1.X, c1.Y, c2.X, c2.Y, p2.X, p2.Y);
        }
        if (closed) Close();
        return this;
    }

    public Path AddEllipse(Vector2 c, float rx, float ry, int segments = 0)
    {
        if (segments <= 0) segments = Math.Clamp((int)(MathF.Max(rx, ry) * 1.5f), 12, 96);
        MoveTo(c + new Vector2(rx, 0));
        for (int i = 1; i < segments; i++)
        {
            float a = i * MathF.Tau / segments;
            LineTo(c + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry));
        }
        return Close();
    }

    public Path AddRect(float x, float y, float w, float h) =>
        MoveTo(x, y).LineTo(x + w, y).LineTo(x + w, y + h).LineTo(x, y + h).Close();

    public Path AddRoundRect(float x, float y, float w, float h, float r)
    {
        r = MathF.Min(r, MathF.Min(w, h) / 2);
        MoveTo(x + r, y).LineTo(x + w - r, y).QuadTo(x + w, y, x + w, y + r)
            .LineTo(x + w, y + h - r).QuadTo(x + w, y + h, x + w - r, y + h)
            .LineTo(x + r, y + h).QuadTo(x, y + h, x, y + h - r)
            .LineTo(x, y + r).QuadTo(x, y, x + r, y);
        return Close();
    }

    public Path AddPolygon(params Vector2[] pts)
    {
        MoveTo(pts[0]);
        for (int i = 1; i < pts.Length; i++) LineTo(pts[i]);
        return Close();
    }

    public static Path Ellipse(float cx, float cy, float rx, float ry) => new Path().AddEllipse(new Vector2(cx, cy), rx, ry);
    public static Path Circle(float cx, float cy, float r) => Ellipse(cx, cy, r, r);
    public static Path Rect(float x, float y, float w, float h) => new Path().AddRect(x, y, w, h);
    public static Path RoundRect(float x, float y, float w, float h, float r) => new Path().AddRoundRect(x, y, w, h, r);
    public static Path Polygon(params Vector2[] pts) => new Path().AddPolygon(pts);

    public static Path Poly(params float[] xy)
    {
        var p = new Path();
        p.MoveTo(xy[0], xy[1]);
        for (int i = 2; i < xy.Length; i += 2) p.LineTo(xy[i], xy[i + 1]);
        return p.Close();
    }

    /// <summary>A transformed copy (scale, rotate, mirror, move).</summary>
    public Path Transform(Matrix m)
    {
        var p = new Path();
        foreach (var c in _contours)
        {
            var nc = new Contour { Closed = c.Closed };
            foreach (var pt in c.Points) nc.Points.Add(Vector2.Transform(pt, m));
            p._contours.Add(nc);
        }
        return p;
    }

    public Path Append(Path other)
    {
        foreach (var c in other._contours) _contours.Add(c);
        _cur = null;
        return this;
    }

    internal List<(Vector2 A, Vector2 B)> Edges()
    {
        var edges = new List<(Vector2, Vector2)>();
        foreach (var c in _contours)
        {
            var pts = c.Points;
            if (pts.Count < 2) continue;
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];   // fills always close the outline
                edges.Add((a, b));
            }
        }
        return edges;
    }
}
