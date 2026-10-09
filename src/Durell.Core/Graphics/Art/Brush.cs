using System;
using Microsoft.Xna.Framework;

namespace Durell.Graphics.Art;

/// <summary>What a <see cref="Canvas"/> fill paints with: a colour, a gradient, or a procedural texture.</summary>
internal abstract class Brush
{
    /// <summary>Straight RGBA (0..1) at a canvas position.</summary>
    public abstract Vector4 At(float x, float y);

    public static Brush Solid(Color c) => new SolidBrush(c.ToVector4());

    /// <summary>A gradient from p0 to p1 through colour stops (position 0..1, colour).</summary>
    public static Brush Linear(Vector2 p0, Vector2 p1, params (float At, Color C)[] stops) => new LinearBrush(p0, p1, stops);

    public static Brush Linear(float x0, float y0, float x1, float y1, Color a, Color b) =>
        new LinearBrush(new Vector2(x0, y0), new Vector2(x1, y1), new[] { (0f, a), (1f, b) });

    /// <summary>A radial gradient (optionally squashed: ry/rx), for rounded, lit surfaces.</summary>
    public static Brush Radial(Vector2 c, float rx, float ry, params (float At, Color C)[] stops) => new RadialBrush(c, rx, ry, stops);

    public static Brush Radial(float cx, float cy, float r, Color inner, Color outer) =>
        new RadialBrush(new Vector2(cx, cy), r, r, new[] { (0f, inner), (1f, outer) });

    public static Brush Func(Func<float, float, Vector4> f) => new FuncBrush(f);

    public static Brush Func(Func<float, float, Color> f) => new FuncBrush((x, y) => f(x, y).ToVector4());

    private sealed class SolidBrush : Brush
    {
        private readonly Vector4 _c;
        public SolidBrush(Vector4 c) => _c = c;
        public override Vector4 At(float x, float y) => _c;
    }

    private sealed class FuncBrush : Brush
    {
        private readonly Func<float, float, Vector4> _f;
        public FuncBrush(Func<float, float, Vector4> f) => _f = f;
        public override Vector4 At(float x, float y) => _f(x, y);
    }

    private abstract class GradientBrush : Brush
    {
        private readonly float[] _at;
        private readonly Vector4[] _c;

        protected GradientBrush((float At, Color C)[] stops)
        {
            _at = new float[stops.Length];
            _c = new Vector4[stops.Length];
            for (int i = 0; i < stops.Length; i++)
            {
                _at[i] = stops[i].At;
                _c[i] = stops[i].C.ToVector4();
            }
        }

        protected Vector4 Sample(float t)
        {
            if (t <= _at[0]) return _c[0];
            for (int i = 1; i < _at.Length; i++)
                if (t <= _at[i])
                {
                    float k = (t - _at[i - 1]) / MathF.Max(1e-5f, _at[i] - _at[i - 1]);
                    k = k * k * (3 - 2 * k);
                    return Vector4.Lerp(_c[i - 1], _c[i], k);
                }
            return _c[^1];
        }
    }

    private sealed class LinearBrush : GradientBrush
    {
        private readonly Vector2 _p0, _d;
        private readonly float _len2;

        public LinearBrush(Vector2 p0, Vector2 p1, (float, Color)[] stops) : base(stops)
        {
            _p0 = p0;
            _d = p1 - p0;
            _len2 = MathF.Max(1e-5f, _d.LengthSquared());
        }

        public override Vector4 At(float x, float y) => Sample(Vector2.Dot(new Vector2(x, y) - _p0, _d) / _len2);
    }

    private sealed class RadialBrush : GradientBrush
    {
        private readonly Vector2 _c;
        private readonly float _rx, _ry;

        public RadialBrush(Vector2 c, float rx, float ry, (float, Color)[] stops) : base(stops)
        {
            _c = c;
            _rx = rx;
            _ry = ry;
        }

        public override Vector4 At(float x, float y)
        {
            float u = (x - _c.X) / _rx, v = (y - _c.Y) / _ry;
            return Sample(MathF.Sqrt(u * u + v * v));
        }
    }
}

/// <summary>Value noise for procedural surfaces (water, rock, metal, clouds).</summary>
internal static class Noise
{
    private static float Lattice(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    /// <summary>Smooth noise, 0..1, one octave; <paramref name="period"/> &gt; 0 makes it tile.</summary>
    public static float Value(float x, float y, int seed, int period = 0)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int x1 = x0 + 1, y1 = y0 + 1;
        if (period > 0)
        {
            x0 = ((x0 % period) + period) % period;
            x1 = ((x1 % period) + period) % period;
        }
        float a = Lattice(x0, y0, seed), b = Lattice(x1, y0, seed);
        float c = Lattice(x0, y1, seed), d = Lattice(x1, y1, seed);
        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
    }

    /// <summary>Fractal noise, 0..1. <paramref name="period"/> (in base-octave cells) makes it tile horizontally.</summary>
    public static float Fbm(float x, float y, int seed, int octaves = 5, int period = 0)
    {
        float v = 0, amp = 0.5f, f = 1, total = 0;
        for (int o = 0; o < octaves; o++)
        {
            v += Value(x * f, y * f, seed + o * 17, period > 0 ? period * (int)f : 0) * amp;
            total += amp;
            amp *= 0.5f;
            f *= 2;
        }
        return v / total;
    }

    /// <summary>Ridged noise (sharp crests), 0..1: rock strata, wave crests.</summary>
    public static float Ridged(float x, float y, int seed, int octaves = 4, int period = 0)
    {
        float v = 0, amp = 0.5f, f = 1, total = 0;
        for (int o = 0; o < octaves; o++)
        {
            float n = 1 - MathF.Abs(Value(x * f, y * f, seed + o * 31, period > 0 ? period * (int)f : 0) * 2 - 1);
            v += n * n * amp;
            total += amp;
            amp *= 0.5f;
            f *= 2;
        }
        return v / total;
    }
}
