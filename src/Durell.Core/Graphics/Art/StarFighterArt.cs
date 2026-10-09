using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Durell.Graphics.Art;

/// <summary>Star Fighter's artwork: the sector map's ships and objects (seen from above, facing up) and the cockpit's enemy.</summary>
internal static class StarFighterArt
{
    public static void Register(List<(string, Func<Canvas>)> all)
    {
        all.Add(("sf-player", Player));
        all.Add(("sf-enemy", Enemy));
        for (int i = 0; i < 3; i++)
        {
            int k = i;
            all.Add(($"sf-asteroid{k}", () => Asteroid(k)));
        }
        all.Add(("sf-gate", Gate));
        all.Add(("sf-base", Base));
        all.Add(("sf-mine", Mine));
        all.Add(("sf-cockpit-enemy", CockpitEnemy));
        all.Add(("sf-plasma", Plasma));
    }

    /// <summary>The player's star fighter from above, nose up: 96 x 96.</summary>
    public static Canvas Player()
    {
        var c = new Canvas(96, 96);
        var hull = new Color(210, 214, 222);
        foreach (float x in new[] { 36f, 60f })
            c.Fill(new Path().MoveTo(x - 5, 74).QuadTo(x, 96, x + 5, 74).Close(), Brush.Linear(0, 74, 0, 94, new Color(200, 230, 255), new Color(60, 120, 255, 0)));
        var wings = Path.Poly(48, 30, 88, 66, 86, 76, 58, 72, 38, 72, 10, 76, 8, 66);
        ArtKit.Body(c, wings, ArtKit.Darker(hull, 0.18f), 30, 76);
        c.Fill(Path.Poly(78, 60, 88, 66, 86, 76, 78, 74), new Color(40, 140, 230));
        c.Fill(Path.Poly(18, 60, 8, 66, 10, 76, 18, 74), new Color(40, 140, 230));
        var fus = new Path().Smooth(new Vector2(48, 4), new Vector2(56, 30), new Vector2(58, 64), new Vector2(54, 78), new Vector2(42, 78), new Vector2(38, 64), new Vector2(40, 30));
        c.Fill(fus, Brush.Linear(new Vector2(38, 0), new Vector2(58, 0), (0f, ArtKit.Lighter(hull, 0.4f)), (0.45f, hull), (1f, ArtKit.Darker(hull, 0.45f))));
        c.Stroke(fus, 1.1f, new Color(40, 44, 54), 0.9f);
        ArtKit.Glass(c, new Path().Smooth(new Vector2(48, 22), new Vector2(52, 32), new Vector2(51, 42), new Vector2(45, 42), new Vector2(44, 32)), 44, 22, 52, 42, new Color(255, 170, 60));
        return c;
    }

    /// <summary>An enemy fighter from above: twin hexagonal wing panels around a ball cockpit: 96 x 96.</summary>
    public static Canvas Enemy()
    {
        var c = new Canvas(96, 96);
        var panel = new Color(70, 74, 82);
        foreach (float x in new[] { 14f, 82f })
        {
            var p = Path.Poly(x - 8, 14, x + 8, 14, x + 10, 48, x + 8, 82, x - 8, 82, x - 10, 48);
            c.Fill(p, Brush.Linear(new Vector2(x - 10, 0), new Vector2(x + 10, 0), (0f, ArtKit.Lighter(panel, 0.25f)), (1f, ArtKit.Darker(panel, 0.4f))));
            for (int i = 0; i < 5; i++) c.Stroke(new Path().MoveTo(x - 8, 22 + i * 13).LineTo(x + 8, 22 + i * 13), 1, new Color(30, 32, 36), 0.8f);
            c.Stroke(p, 1.2f, new Color(200, 60, 50), 0.9f);
        }
        c.Fill(Path.Rect(22, 45, 52, 6), new Color(90, 94, 100));
        ArtKit.Body(c, Path.Circle(48, 48, 15), new Color(150, 154, 160), 33, 63);
        ArtKit.Glass(c, Path.Circle(48, 42, 7), 42, 36, 54, 48, new Color(220, 60, 50));
        return c;
    }

    /// <summary>A rocky asteroid, three shapes: 96 x 96.</summary>
    public static Canvas Asteroid(int kind)
    {
        var c = new Canvas(96, 96);
        var pts = new Vector2[11];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = i * MathF.Tau / pts.Length;
            float r = 30 + Noise.Value(i * 1.7f, kind * 5, 600) * 14;
            pts[i] = new Vector2(48 + MathF.Cos(a) * r, 48 + MathF.Sin(a) * r * 0.85f);
        }
        var rock = new Path().Smooth(pts);
        c.Fill(rock, Brush.Func((x, y) =>
        {
            float n = Noise.Fbm(x * 0.06f, y * 0.06f, 610 + kind, 5);
            float lit = Math.Clamp(1.2f - Vector2.Distance(new Vector2(x, y), new Vector2(34, 32)) / 50f, 0.2f, 1);
            var col = Vector3.Lerp(new Vector3(0.32f, 0.28f, 0.24f), new Vector3(0.6f, 0.54f, 0.46f), n) * lit;
            return new Vector4(col, 1);
        }));
        // craters
        for (int i = 0; i < 5; i++)
        {
            float x = 32 + Noise.Value(i * 3.3f, kind, 620) * 34, y = 32 + Noise.Value(i * 2.1f, kind, 630) * 32, r = 3 + Noise.Value(i, kind, 640) * 5;
            c.Fill(Path.Circle(x, y, r), Brush.Solid(new Color(0, 0, 0)), 0.25f, BlendMode.Atop);
            c.Stroke(new Path().MoveTo(x - r, y + r * 0.2f).QuadTo(x, y + r * 1.2f, x + r, y + r * 0.2f), 1, new Color(255, 255, 255), 0.25f);
        }
        c.Stroke(rock, 1, new Color(30, 26, 22), 0.6f);
        return c;
    }

    /// <summary>A warp gate: a glowing ring with a swirling core: 128 x 128.</summary>
    public static Canvas Gate()
    {
        var c = new Canvas(128, 128);
        c.Fill(Path.Circle(64, 64, 44), Brush.Func((x, y) =>
        {
            var d = new Vector2(x - 64, y - 64);
            float r = d.Length() / 44, a = MathF.Atan2(d.Y, d.X);
            float swirl = 0.5f + 0.5f * MathF.Sin(a * 3 + r * 9);
            return new Vector4(0.4f + swirl * 0.4f, 0.3f, 1f, (1 - r) * 0.8f * (0.5f + swirl * 0.5f));
        }));
        c.Stroke(Path.Circle(64, 64, 48), 8, Brush.Linear(0, 16, 0, 112, new Color(220, 230, 255), new Color(90, 100, 140)));
        c.Stroke(Path.Circle(64, 64, 48), 2, new Color(140, 220, 255), 0.9f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8;
            c.Fill(Path.Circle(64 + MathF.Cos(a) * 48, 64 + MathF.Sin(a) * 48, 3.5f), new Color(120, 255, 220));
        }
        return c;
    }

    /// <summary>The starbase: a wheel station with a docking hub: 128 x 128.</summary>
    public static Canvas Base()
    {
        var c = new Canvas(128, 128);
        var steel = new Color(170, 176, 186);
        c.Stroke(Path.Circle(64, 64, 46), 14, Brush.Linear(0, 18, 0, 110, ArtKit.Lighter(steel, 0.3f), ArtKit.Darker(steel, 0.4f)));
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI / 2 + 0.4f;
            c.Stroke(new Path().MoveTo(64, 64).LineTo(64 + MathF.Cos(a) * 46, 64 + MathF.Sin(a) * 46), 5, ArtKit.Darker(steel, 0.15f));
        }
        for (int i = 0; i < 16; i++)
        {
            float a = i * MathF.Tau / 16;
            c.Fill(Path.Rect(64 + MathF.Cos(a) * 46 - 1.5f, 64 + MathF.Sin(a) * 46 - 1.5f, 3, 3), new Color(255, 230, 150));
        }
        ArtKit.Body(c, Path.Circle(64, 64, 15), steel, 49, 79);
        c.Fill(Path.Circle(64, 64, 6), new Color(80, 200, 255));
        return c;
    }

    /// <summary>A space mine: a spiked orb with a red light: 64 x 64.</summary>
    public static Canvas Mine()
    {
        var c = new Canvas(64, 64);
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8;
            c.Stroke(new Path().MoveTo(32, 32).LineTo(32 + MathF.Cos(a) * 28, 32 + MathF.Sin(a) * 28), 3, new Color(120, 124, 130));
        }
        ArtKit.Body(c, Path.Circle(32, 32, 14), new Color(90, 94, 100), 18, 46);
        c.Fill(Path.Circle(32, 32, 5), Brush.Radial(32, 32, 5, new Color(255, 200, 180), new Color(220, 20, 20)));
        return c;
    }

    /// <summary>The enemy ship seen through the cockpit window: 192 x 128.</summary>
    public static Canvas CockpitEnemy()
    {
        var c = new Canvas(192, 128);
        var panel = new Color(78, 82, 92);
        foreach (int side in new[] { -1, 1 })
        {
            float x = 96 + side * 70;
            var p = Path.Poly(x - 14, 8, x + 14, 8, x + 18, 64, x + 14, 120, x - 14, 120, x - 18, 64);
            c.Fill(p, Brush.Linear(new Vector2(0, 8), new Vector2(0, 120), (0f, ArtKit.Lighter(panel, 0.3f)), (0.5f, panel), (1f, ArtKit.Darker(panel, 0.5f))));
            for (int i = 0; i < 6; i++) c.Stroke(new Path().MoveTo(x - 15, 20 + i * 17).LineTo(x + 15, 20 + i * 17), 1.2f, new Color(30, 32, 38), 0.8f);
            c.Stroke(p, 1.5f, new Color(210, 70, 60), 0.9f);
            c.Fill(Path.Rect(96 + side * 26 - (side > 0 ? 0 : 30), 58, 30, 12), ArtKit.Darker(panel, 0.1f));
        }
        ArtKit.Body(c, Path.Circle(96, 64, 30), new Color(150, 156, 166), 34, 94);
        c.Fill(Path.Circle(96, 64, 16), Brush.Radial(new Vector2(92, 60), 18, 18, (0f, new Color(255, 160, 140)), (0.5f, new Color(200, 30, 30)), (1f, new Color(60, 0, 0))));
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8;
            c.Stroke(new Path().MoveTo(96 + MathF.Cos(a) * 9, 64 + MathF.Sin(a) * 9).LineTo(96 + MathF.Cos(a) * 16, 64 + MathF.Sin(a) * 16), 1.2f, new Color(40, 0, 0), 0.8f);
        }
        return c;
    }

    /// <summary>An enemy plasma shot: 48 x 48.</summary>
    public static Canvas Plasma()
    {
        var c = new Canvas(48, 48);
        c.Fill(Path.Circle(24, 24, 22), Brush.Radial(24, 24, 22, new Color(120, 255, 140, 230), new Color(0, 160, 60, 0)));
        c.Fill(Path.Circle(24, 24, 7), Color.White);
        return c;
    }
}
