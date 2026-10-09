using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Durell.Graphics.Art;

/// <summary>Lunar Lander's artwork: the lunar module, its flame, the Moon's surface and the pad.</summary>
internal static class LunarArt
{
    public static void Register(List<(string, Func<Canvas>)> all)
    {
        all.Add(("lunar-module", Module));
        for (int i = 0; i < 3; i++)
        {
            int f = i;
            all.Add(($"lunar-flame{f}", () => Flame(f)));
        }
        all.Add(("lunar-surface", Surface));
        all.Add(("lunar-wreck", Wreck));
    }

    /// <summary>The lunar module: gold-foil descent stage on four legs, grey ascent stage above: 128 x 128 (feet at y 124).</summary>
    public static Canvas Module()
    {
        var c = new Canvas(128, 128);
        var foil = new Color(214, 170, 60);
        // legs and footpads (behind the stage)
        foreach (int side in new[] { -1, 1 })
        {
            c.Stroke(new Path().MoveTo(64 + side * 22, 78).LineTo(64 + side * 50, 118), 3.2f, new Color(150, 150, 156));
            c.Stroke(new Path().MoveTo(64 + side * 26, 92).LineTo(64 + side * 46, 112), 2, new Color(120, 120, 126));
            c.Fill(Path.Ellipse(64 + side * 51, 121, 8, 3), new Color(170, 170, 176));
        }
        c.Stroke(new Path().MoveTo(64, 86).LineTo(64, 116), 3, new Color(140, 140, 146));
        c.Fill(Path.Ellipse(64, 119, 6, 2.5f), new Color(170, 170, 176));
        // descent stage: crinkled gold foil
        var stage = Path.Poly(30, 64, 98, 64, 104, 72, 104, 90, 98, 96, 30, 96, 24, 90, 24, 72);
        c.Fill(stage, Brush.Func((x, y) =>
        {
            float n = Noise.Fbm(x * 0.2f, y * 0.25f, 700, 3);
            float lit = 1.2f - (x - 24) / 100f * 0.6f;
            var col = Vector3.Lerp(new Vector3(0.6f, 0.45f, 0.12f), new Vector3(1f, 0.85f, 0.4f), n) * lit;
            return new Vector4(Vector3.Min(col, Vector3.One), 1);
        }));
        c.Stroke(stage, 1.2f, new Color(90, 64, 20), 0.9f);
        c.Fill(Path.Poly(56, 96, 72, 96, 76, 104, 52, 104), new Color(70, 70, 76));      // engine bell
        // ascent stage
        var cabin = new Path().MoveTo(38, 64).LineTo(40, 40).LineTo(52, 26).LineTo(78, 24).LineTo(92, 36).LineTo(92, 64).Close();
        ArtKit.Body(c, cabin, new Color(176, 178, 184), 24, 64);
        c.Fill(Path.Poly(48, 38, 58, 30, 58, 44, 48, 48), new Color(30, 40, 60));       // windows
        c.Fill(Path.Poly(62, 30, 72, 30, 72, 44, 62, 44), new Color(30, 40, 60));
        c.Fill(Path.Rect(76, 44, 12, 16), new Color(40, 40, 46));                          // hatch
        // antennae and the RCS thrusters
        c.Stroke(new Path().MoveTo(80, 24).LineTo(90, 8), 1.5f, new Color(200, 200, 205));
        c.Fill(Path.Circle(90, 8, 4), new Color(230, 230, 235));
        c.Stroke(new Path().MoveTo(46, 30).LineTo(38, 18), 1.2f, new Color(200, 200, 205));
        foreach (float x in new[] { 34f, 94f }) c.Fill(Path.Rect(x - 3, 42, 6, 8), new Color(110, 112, 118));
        return c;
    }

    /// <summary>The descent engine's exhaust, three flickers: 64 x 128 (nozzle at the top).</summary>
    public static Canvas Flame(int frame)
    {
        var c = new Canvas(64, 128);
        float len = 100 + frame * 10;
        var plume = new Path().MoveTo(22, 2).QuadTo(32 - frame * 2, len, 32, len).QuadTo(32 + frame * 2, len, 42, 2).Close();
        c.Fill(plume, Brush.Func((x, y) =>
        {
            float k = y / len;
            float n = Noise.Fbm(x * 0.15f, y * 0.08f + frame * 4, 710, 3);
            var col = Vector3.Lerp(new Vector3(1, 1, 0.95f), new Vector3(1f, 0.55f, 0.15f), MathF.Min(1, k * 1.6f));
            return new Vector4(col, Math.Clamp((1 - k) * (0.7f + n * 0.6f), 0, 1));
        }));
        return c;
    }

    /// <summary>The Moon's surface: grey regolith with craters and rocks, tiling sideways: 512 x 96.</summary>
    public static Canvas Surface()
    {
        const int w = 512, h = 96;
        var c = new Canvas(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float ridge = 18 + Noise.Fbm(x / 512f * 6, 0.5f, 720, 4, 6) * 30;
                if (y < ridge) continue;
                float n = Noise.Fbm(x * 0.03f, y * 0.05f, 721, 5, 15);
                float crater = 0;
                for (int i = 0; i < 9; i++)
                {
                    float cx = (i * 61 + 23) % w, cy = ridge + 14 + (i * 37 % 40), r = 6 + i % 4 * 5;
                    float dx = MathF.Min(MathF.Abs(x - cx), w - MathF.Abs(x - cx)), dy = (y - cy) * 2.2f;
                    float d = MathF.Sqrt(dx * dx + dy * dy) / r;
                    if (d < 1) crater += (d < 0.8f ? -0.25f * (1 - d) : 0.25f) * (dy < 0 ? -1 : 1);
                }
                float lit = 0.55f + n * 0.4f + crater - (y - ridge) / h * 0.25f;
                if (y < ridge + 2) lit += 0.2f;
                lit = Math.Clamp(lit, 0.08f, 1);
                c.Set(x, y, new Vector4(lit * 0.86f, lit * 0.85f, lit * 0.84f, 1));
            }
        return c;
    }

    /// <summary>The module after a crash: a crumpled, scorched heap: 128 x 64.</summary>
    public static Canvas Wreck()
    {
        var c = new Canvas(128, 64);
        var heap = new Path().Smooth(new Vector2(14, 60), new Vector2(30, 34), new Vector2(54, 26), new Vector2(80, 36), new Vector2(110, 44), new Vector2(118, 60));
        c.Fill(heap, Brush.Func((x, y) =>
        {
            float n = Noise.Fbm(x * 0.15f, y * 0.15f, 730, 4);
            return new Vector4(Vector3.Lerp(new Vector3(0.15f, 0.13f, 0.12f), new Vector3(0.6f, 0.45f, 0.15f), n * n), 1);
        }));
        c.Stroke(new Path().MoveTo(40, 40).LineTo(20, 20), 2.5f, new Color(150, 150, 156));
        c.Stroke(new Path().MoveTo(90, 42).LineTo(112, 30), 2.5f, new Color(150, 150, 156));
        return c;
    }
}
