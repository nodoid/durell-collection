using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Graphics.Art;

/// <summary>
/// A software vector canvas for drawing the ENHANCED look's artwork in code, once, when a game is
/// first shown: anti-aliased filled paths (non-zero winding, 4 sub-scanlines with exact horizontal
/// coverage), strokes, gradient and procedural brushes, blur, and export to a texture.
/// Pixels are premultiplied RGBA floats.
/// </summary>
internal sealed class Canvas
{
    private const int Sub = 4;
    private readonly float[] _px;
    private readonly float[] _cov;
    private readonly List<(float X, int Dir)> _hits = new();

    public int Width { get; }
    public int Height { get; }

    public Canvas(int width, int height)
    {
        Width = width;
        Height = height;
        _px = new float[width * height * 4];
        _cov = new float[width];
    }

    // ------------------------------------------------------------------ filling

    public void Fill(Path path, Color c, float opacity = 1) => Fill(path, Brush.Solid(c), opacity);

    public void Fill(Path path, Brush brush, float opacity = 1, BlendMode mode = BlendMode.Normal)
    {
        var edges = path.Edges();
        if (edges.Count == 0) return;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var e in edges)
        {
            minY = MathF.Min(minY, MathF.Min(e.A.Y, e.B.Y));
            maxY = MathF.Max(maxY, MathF.Max(e.A.Y, e.B.Y));
        }
        int y0 = Math.Max(0, (int)MathF.Floor(minY)), y1 = Math.Min(Height - 1, (int)MathF.Ceiling(maxY));
        for (int y = y0; y <= y1; y++)
        {
            Array.Clear(_cov);
            int cx0 = Width, cx1 = -1;
            for (int s = 0; s < Sub; s++)
            {
                float sy = y + (s + 0.5f) / Sub;
                _hits.Clear();
                foreach (var e in edges)
                {
                    var a = e.A;
                    var b = e.B;
                    if (a.Y == b.Y) continue;
                    int dir = 1;
                    if (a.Y > b.Y)
                    {
                        (a, b) = (b, a);
                        dir = -1;
                    }
                    if (sy < a.Y || sy >= b.Y) continue;
                    float x = a.X + (sy - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                    _hits.Add((x, dir));
                }
                if (_hits.Count < 2) continue;
                _hits.Sort((p, q) => p.X.CompareTo(q.X));
                int wind = 0;
                for (int i = 0; i < _hits.Count - 1; i++)
                {
                    wind += _hits[i].Dir;
                    if (wind == 0) continue;
                    float xa = MathF.Max(0, _hits[i].X), xb = MathF.Min(Width, _hits[i + 1].X);
                    if (xb <= xa) continue;
                    int ia = (int)xa, ib = Math.Min(Width - 1, (int)xb);
                    cx0 = Math.Min(cx0, ia);
                    cx1 = Math.Max(cx1, ib);
                    if (ia == ib)
                    {
                        _cov[ia] += (xb - xa) / Sub;
                        continue;
                    }
                    _cov[ia] += (ia + 1 - xa) / Sub;
                    for (int x = ia + 1; x < ib; x++) _cov[x] += 1f / Sub;
                    if (ib < Width) _cov[ib] += (xb - ib) / Sub;
                }
            }
            for (int x = cx0; x <= cx1; x++)
            {
                float k = MathF.Min(1, _cov[x]) * opacity;
                if (k <= 0) continue;
                var c = brush.At(x + 0.5f, y + 0.5f);
                Blend(x, y, c, k, mode);
            }
        }
    }

    private void Blend(int x, int y, Vector4 c, float k, BlendMode mode)
    {
        // c is straight RGBA in 0..1
        int o = (y * Width + x) * 4;
        float a = c.W * k;
        if (a <= 0) return;
        switch (mode)
        {
            case BlendMode.Normal:
                _px[o] = c.X * a + _px[o] * (1 - a);
                _px[o + 1] = c.Y * a + _px[o + 1] * (1 - a);
                _px[o + 2] = c.Z * a + _px[o + 2] * (1 - a);
                _px[o + 3] = a + _px[o + 3] * (1 - a);
                break;
            case BlendMode.Add:
                _px[o] = MathF.Min(1, _px[o] + c.X * a);
                _px[o + 1] = MathF.Min(1, _px[o + 1] + c.Y * a);
                _px[o + 2] = MathF.Min(1, _px[o + 2] + c.Z * a);
                _px[o + 3] = MathF.Min(1, _px[o + 3] + a * 0.5f);
                break;
            case BlendMode.Multiply:
                // darkens what is already there, keeping its alpha
                _px[o] *= 1 - a + c.X * a;
                _px[o + 1] *= 1 - a + c.Y * a;
                _px[o + 2] *= 1 - a + c.Z * a;
                break;
            case BlendMode.Atop:
                // paints only where something is already drawn (shading inside a shape)
                float da = _px[o + 3];
                if (da <= 0) return;
                float aa = a * da;
                _px[o] = c.X * aa + _px[o] * (1 - a);
                _px[o + 1] = c.Y * aa + _px[o + 1] * (1 - a);
                _px[o + 2] = c.Z * aa + _px[o + 2] * (1 - a);
                break;
        }
    }

    /// <summary>A stroked outline of a path: segments as quads with round joins and caps.</summary>
    public void Stroke(Path path, float width, Color c, float opacity = 1) => Stroke(path, width, Brush.Solid(c), opacity);

    public void Stroke(Path path, float width, Brush brush, float opacity = 1, BlendMode mode = BlendMode.Normal)
    {
        var outline = new Path();
        float r = width / 2;
        foreach (var contour in path.Contours)
        {
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var a = contour.Points[i];
                bool last = i == contour.Points.Count - 1;
                if (last && !contour.Closed)
                {
                    outline.AddEllipse(a, r, r, 12);
                    break;
                }
                var b = contour.Points[last ? 0 : i + 1];
                var d = b - a;
                if (d.LengthSquared() < 1e-6f) continue;
                d.Normalize();
                var n = new Vector2(-d.Y, d.X) * r;
                outline.MoveTo(a - n).LineTo(b - n).LineTo(b + n).LineTo(a + n).Close();   // same winding as the round joins
                outline.AddEllipse(a, r, r, 12);
            }
        }
        Fill(outline, brush, opacity, mode);
    }

    // ------------------------------------------------------------------ whole-canvas effects

    /// <summary>Paints everything drawn so far with a brush, keeping its shape (e.g. a lighting pass).</summary>
    public void Tint(Brush brush, float opacity = 1, BlendMode mode = BlendMode.Atop)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                Blend(x, y, brush.At(x + 0.5f, y + 0.5f), opacity, mode);
    }

    /// <summary>A soft box blur (three passes approximate a Gaussian).</summary>
    public void Blur(int radius)
    {
        if (radius <= 0) return;
        var tmp = new float[_px.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            BlurLine(_px, tmp, Width, Height, radius, true);
            BlurLine(tmp, _px, Width, Height, radius, false);
        }
    }

    private static void BlurLine(float[] src, float[] dst, int w, int h, int r, bool horizontal)
    {
        int lines = horizontal ? h : w, len = horizontal ? w : h;
        for (int l = 0; l < lines; l++)
        {
            for (int ch = 0; ch < 4; ch++)
            {
                float sum = 0;
                int Idx(int i) => (horizontal ? l * w + Math.Clamp(i, 0, len - 1) : Math.Clamp(i, 0, len - 1) * w + l) * 4 + ch;
                for (int i = -r; i <= r; i++) sum += src[Idx(i)];
                for (int i = 0; i < len; i++)
                {
                    dst[Idx(i)] = sum / (2 * r + 1);
                    sum += src[Idx(i + r + 1)] - src[Idx(i - r)];
                }
            }
        }
    }

    /// <summary>Draws another canvas onto this one (e.g. a blurred shadow layer under the art).</summary>
    public void Draw(Canvas other, float dx, float dy, float opacity = 1, Color? tint = null)
    {
        var t = tint?.ToVector4();
        for (int y = 0; y < Height; y++)
        {
            int sy = (int)MathF.Round(y - dy);
            if (sy < 0 || sy >= other.Height) continue;
            for (int x = 0; x < Width; x++)
            {
                int sx = (int)MathF.Round(x - dx);
                if (sx < 0 || sx >= other.Width) continue;
                int so = (sy * other.Width + sx) * 4;
                float a = other._px[so + 3] * opacity;
                if (a <= 0) continue;
                Vector4 c = t is Vector4 tv
                    ? new Vector4(tv.X, tv.Y, tv.Z, 1)
                    : new Vector4(other._px[so] / other._px[so + 3], other._px[so + 1] / other._px[so + 3], other._px[so + 2] / other._px[so + 3], 1);
                Blend(x, y, c, a, BlendMode.Normal);
            }
        }
    }

    /// <summary>A copy of the shape's silhouette, for shadows and glows.</summary>
    public Canvas Silhouette(Color c)
    {
        var s = new Canvas(Width, Height);
        var v = c.ToVector4();
        for (int i = 0; i < Width * Height; i++)
        {
            float a = _px[i * 4 + 3] * v.W;
            s._px[i * 4] = v.X * a;
            s._px[i * 4 + 1] = v.Y * a;
            s._px[i * 4 + 2] = v.Z * a;
            s._px[i * 4 + 3] = a;
        }
        return s;
    }

    /// <summary>Straight-alpha pixel colour.</summary>
    public Vector4 Get(int x, int y)
    {
        int o = (y * Width + x) * 4;
        float a = _px[o + 3];
        return a <= 0 ? Vector4.Zero : new Vector4(_px[o] / a, _px[o + 1] / a, _px[o + 2] / a, a);
    }

    /// <summary>Sets a pixel (straight alpha) - for procedural textures.</summary>
    public void Set(int x, int y, Vector4 c)
    {
        int o = (y * Width + x) * 4;
        _px[o] = c.X * c.W;
        _px[o + 1] = c.Y * c.W;
        _px[o + 2] = c.Z * c.W;
        _px[o + 3] = c.W;
    }

    public Texture2D ToTexture(GraphicsDevice device)
    {
        var data = new Color[Width * Height];
        for (int i = 0; i < data.Length; i++)
        {
            int o = i * 4;
            // MonoGame's default blend is premultiplied: keep premultiplied colours
            data[i] = new Color(Math.Clamp(_px[o], 0, 1), Math.Clamp(_px[o + 1], 0, 1), Math.Clamp(_px[o + 2], 0, 1), Math.Clamp(_px[o + 3], 0, 1));
        }
        var t = new Texture2D(device, Width, Height, false, SurfaceFormat.Color);
        t.SetData(data);
        return t;
    }

    /// <summary>Writes a PNG without a graphics device (dev tools: tools/Durell.ArtLab).</summary>
    public void SavePng(string file)
    {
        var raw = new byte[(Width * 4 + 1) * Height];
        for (int y = 0; y < Height; y++)
        {
            int r = y * (Width * 4 + 1);
            raw[r] = 0;
            for (int x = 0; x < Width; x++)
            {
                var c = Get(x, y);
                int o = r + 1 + x * 4;
                raw[o] = (byte)(Math.Clamp(c.X, 0, 1) * 255 + 0.5f);
                raw[o + 1] = (byte)(Math.Clamp(c.Y, 0, 1) * 255 + 0.5f);
                raw[o + 2] = (byte)(Math.Clamp(c.Z, 0, 1) * 255 + 0.5f);
                raw[o + 3] = (byte)(Math.Clamp(c.W, 0, 1) * 255 + 0.5f);
            }
        }
        using var fs = System.IO.File.Create(file);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr, Width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), Height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        Chunk(fs, "IHDR", ihdr);
        using (var ms = new System.IO.MemoryStream())
        {
            using (var z = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Fastest, true))
                z.Write(raw);
            Chunk(fs, "IDAT", ms.ToArray());
        }
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void Chunk(System.IO.Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        var td = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type, td);
        data.CopyTo(td, 4);
        s.Write(td);
        uint crc = 0xFFFFFFFF;
        foreach (byte b in td)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }
        var cb = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(cb, ~crc);
        s.Write(cb);
    }
}

internal enum BlendMode { Normal, Add, Multiply, Atop }
