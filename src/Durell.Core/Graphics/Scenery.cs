using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Graphics;

/// <summary>Textures for the enhanced scenery, drawn in code once at start-up.</summary>
internal sealed class Scenery
{
    public Texture2D Cloud { get; }
    public Texture2D Nebula { get; }
    public Texture2D Ray { get; }
    public Texture2D Bubble { get; }
    public Texture2D Planet { get; }
    public Texture2D Moon { get; }
    public Texture2D Haze { get; }

    public Scenery(GraphicsDevice d)
    {
        Cloud = Make(d, 256, 128, CloudPixel);
        Nebula = Make(d, 256, 256, NebulaPixel);
        Ray = Make(d, 32, 256, (x, y) =>
        {
            float u = (x + 0.5f) / 32 * 2 - 1, v = (y + 0.5f) / 256;
            float a = MathF.Max(0, 1 - u * u) * MathF.Pow(1 - v, 1.6f);
            return Color.White * a;
        });
        Bubble = Make(d, 64, 64, (x, y) =>
        {
            float u = (x + 0.5f) / 32 - 1, v = (y + 0.5f) / 32 - 1;
            float r = MathF.Sqrt(u * u + v * v);
            float rim = MathF.Max(0, 1 - MathF.Abs(r - 0.82f) * 7);
            float fill = r < 0.85f ? 0.12f : 0;
            float hi = MathF.Max(0, 1 - MathF.Sqrt((u + 0.35f) * (u + 0.35f) + (v + 0.35f) * (v + 0.35f)) * 5);
            return Color.White * MathF.Min(1, rim * 0.8f + fill + hi);
        });
        Planet = Make(d, 256, 256, PlanetPixel);
        Moon = Make(d, 256, 256, MoonPixel);
        Haze = Make(d, 256, 64, (x, y) =>
        {
            float v = (y + 0.5f) / 64;
            float a = MathF.Sin(v * MathF.PI);
            a *= 0.6f + 0.4f * Noise(x * 0.04f, y * 0.1f, 9);
            return Color.White * (a * a);
        });
    }

    private static Texture2D Make(GraphicsDevice d, int w, int h, Func<int, int, Color> f)
    {
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f(x, y);
        var t = new Texture2D(d, w, h);
        t.SetData(px);
        return t;
    }

    // ---------------------------------------------------------------- noise

    private static float Lattice(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    private static float Smooth(float x, float y, int seed)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        float a = Lattice(x0, y0, seed), b = Lattice(x0 + 1, y0, seed);
        float c = Lattice(x0, y0 + 1, seed), e = Lattice(x0 + 1, y0 + 1, seed);
        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, e, fx), fy);
    }

    public static float Noise(float x, float y, int seed)
    {
        float v = 0, amp = 0.5f, f = 1;
        for (int o = 0; o < 5; o++)
        {
            v += Smooth(x * f, y * f, seed + o * 17) * amp;
            amp *= 0.5f;
            f *= 2;
        }
        return v;
    }

    private static Color CloudPixel(int x, int y)
    {
        float u = (x + 0.5f) / 256 * 2 - 1, v = (y + 0.5f) / 128 * 2 - 1;
        // a flat-bottomed puff: an ellipse, cut softly at the bottom, roughened by noise
        float e = 1 - (u * u + v * v * 1.6f);
        float n = Noise(x * 0.035f, y * 0.06f, 3);
        float a = Math.Clamp(e * 1.6f + (n - 0.5f) * 1.4f, 0, 1);
        a *= Math.Clamp((0.55f - v) * 3, 0, 1);
        float shade = 0.82f + 0.18f * Math.Clamp(-v + 0.2f, 0, 1);
        var c = new Color(shade, shade, MathF.Min(1, shade + 0.03f));
        return c * (a * a);
    }

    private static Color NebulaPixel(int x, int y)
    {
        float n1 = Noise(x * 0.02f, y * 0.02f, 41), n2 = Noise(x * 0.03f + 7, y * 0.03f, 77);
        float u = (x + 0.5f) / 128 - 1, v = (y + 0.5f) / 128 - 1;
        float fall = MathF.Max(0, 1 - MathF.Sqrt(u * u + v * v));
        float a = MathF.Pow(Math.Clamp(n1 * 1.6f - 0.45f, 0, 1), 1.5f) * fall;
        var c = Color.Lerp(new Color(120, 60, 220), new Color(40, 150, 230), n2);
        return c * a;
    }

    private static Color PlanetPixel(int x, int y)
    {
        float u = (x + 0.5f) / 128 - 1, v = (y + 0.5f) / 128 - 1;
        float r2 = u * u + v * v;
        if (r2 > 1) return Color.Transparent;
        float z = MathF.Sqrt(1 - r2);
        float land = Noise(u * 3 + 10, v * 3 / MathF.Max(0.3f, z), 5);
        var sea = new Color(30, 90, 190);
        var ground = new Color(70, 140, 70);
        var c = land > 0.55f ? Color.Lerp(ground, new Color(150, 130, 90), (land - 0.55f) * 3) : sea;
        float clouds = Noise(u * 5 + 3, v * 5, 13);
        if (clouds > 0.58f) c = Color.Lerp(c, Color.White, (clouds - 0.58f) * 3);
        float light = Math.Clamp(-u * 0.6f - v * 0.5f + z * 0.7f, 0.05f, 1);
        c = new Color((byte)(c.R * light), (byte)(c.G * light), (byte)(c.B * light));
        float edge = Math.Clamp((1 - MathF.Sqrt(r2)) * 60, 0, 1);
        return c * edge;
    }

    private static Color MoonPixel(int x, int y)
    {
        float u = (x + 0.5f) / 128 - 1, v = (y + 0.5f) / 128 - 1;
        float r2 = u * u + v * v;
        if (r2 > 1) return Color.Transparent;
        float z = MathF.Sqrt(1 - r2);
        float n = Noise(u * 4 + 20, v * 4, 21);
        float grey = 0.55f + 0.35f * n;
        float light = Math.Clamp(u * 0.5f - v * 0.4f + z * 0.8f, 0.04f, 1);
        float g = grey * light;
        float edge = Math.Clamp((1 - MathF.Sqrt(r2)) * 60, 0, 1);
        return new Color(g, g * 0.98f, g * 0.94f) * edge;
    }
}
