using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Durell.Graphics.Art;

/// <summary>Galaxy's artwork: the alien fleet, the player's fighter, shots and explosions.</summary>
internal static class GalaxyArt
{
    public static readonly Color[] RowColours = { new(230, 60, 52), new(60, 120, 240), new(70, 210, 90) };
    private static readonly string[] RowNames = { "red", "blue", "green" };

    public static void Register(List<(string, Func<Canvas>)> all)
    {
        for (int r = 0; r < 3; r++)
        {
            int row = r;
            all.Add(($"galaxy-alien-{RowNames[row]}0", () => Alien(RowColours[row], 0)));
            all.Add(($"galaxy-alien-{RowNames[row]}1", () => Alien(RowColours[row], 1)));
            all.Add(($"galaxy-alien-{RowNames[row]}2", () => Alien(RowColours[row], 2)));
        }
        all.Add(("galaxy-fighter", Fighter));
        all.Add(("galaxy-bolt", Bolt));
        all.Add(("galaxy-bomb", Bomb));
        all.Add(("galaxy-shield", Shield));
        for (int i = 0; i < 5; i++)
        {
            int f = i;
            all.Add(($"galaxy-burst{f}", () => Burst(f)));
        }
    }

    /// <summary>
    /// An insect-like alien seen from above, 128 x 96: pose 0 wings up, 1 wings spread, 2 diving
    /// (wings swept back, legs down).
    /// </summary>
    public static Canvas Alien(Color col, int pose)
    {
        var c = new Canvas(128, 96);
        var dark = ArtKit.Darker(col, 0.55f);
        // wings: membranes with veins, mirrored left and right
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 P(float x, float y) => new(64 + side * x, y);
            Path wing = pose switch
            {
                0 => new Path().Smooth(P(10, 46), P(30, 22), P(52, 8), P(58, 18), P(44, 40), P(22, 54)),
                1 => new Path().Smooth(P(10, 46), P(36, 34), P(60, 32), P(62, 44), P(40, 56), P(18, 58)),
                _ => new Path().Smooth(P(10, 42), P(26, 46), P(40, 62), P(34, 72), P(20, 64), P(10, 54)),
            };
            c.Fill(wing, Brush.Linear(P(10, 46), P(56, 20), (0f, ArtKit.Alpha(ArtKit.Lighter(col, 0.3f), 0.95f)), (1f, ArtKit.Alpha(col, 0.55f))));
            c.Stroke(wing, 1.4f, dark, 0.9f);
            var tip = pose switch { 0 => P(52, 10), 1 => P(58, 36), _ => P(36, 66) };
            c.Stroke(new Path().MoveTo(P(12, 46)).LineTo(tip), 1.2f, dark, 0.7f);
            c.Stroke(new Path().MoveTo(P(12, 48)).LineTo(Vector2.Lerp(P(12, 48), tip, 0.7f) + new Vector2(0, 10)), 1, dark, 0.5f);
            // legs / mandibles
            c.Stroke(new Path().MoveTo(P(8, 62)).QuadTo(64 + side * 18, 70, 64 + side * 16, pose == 2 ? 86 : 80), 2.4f, dark);
        }
        // abdomen and thorax
        var body = new Path().Smooth(new Vector2(64, 20), new Vector2(76, 34), new Vector2(78, 52), new Vector2(70, 72),
            new Vector2(64, 78), new Vector2(58, 72), new Vector2(50, 52), new Vector2(52, 34));
        c.Fill(body, Brush.Radial(new Vector2(58, 38), 28, 36, (0f, ArtKit.Lighter(col, 0.55f)), (0.45f, col), (1f, ArtKit.Darker(col, 0.6f))));
        for (int i = 0; i < 3; i++) c.Stroke(new Path().MoveTo(54, 56 + i * 6).QuadTo(64, 59 + i * 6, 74, 56 + i * 6), 1.2f, dark, 0.7f);
        c.Stroke(body, 1.3f, ArtKit.Darker(col, 0.7f), 0.9f);
        // eyes: glowing compound eyes
        foreach (int side in new[] { -1, 1 })
        {
            var e = new Vector2(64 + side * 7, 30);
            c.Fill(Path.Ellipse(e.X, e.Y, 5.5f, 4.5f), Brush.Radial(new Vector2(e.X - 1, e.Y - 1), 6, 5, (0f, new Color(255, 255, 210)), (0.5f, new Color(255, 220, 60)), (1f, new Color(160, 80, 10))));
            c.Fill(Path.Circle(e.X - 1.5f, e.Y - 1.5f, 1.2f), Color.White);
        }
        // antennae
        c.Stroke(new Path().MoveTo(60, 22).QuadTo(54, 10, 46, 8), 1.4f, dark);
        c.Stroke(new Path().MoveTo(68, 22).QuadTo(74, 10, 82, 8), 1.4f, dark);
        return ArtKit.WithShadow(c, 0, 0, 0, 0);
    }

    /// <summary>The player's fighter, nose up: 96 x 112.</summary>
    public static Canvas Fighter()
    {
        var c = new Canvas(96, 112);
        // engine flames
        foreach (float x in new[] { 38f, 58f })
            c.Fill(new Path().MoveTo(x - 5, 88).QuadTo(x, 112, x + 5, 88).Close(),
                Brush.Linear(new Vector2(0, 88), new Vector2(0, 110), (0f, new Color(220, 240, 255)), (0.4f, new Color(90, 160, 255, 220)), (1f, new Color(40, 60, 255, 0))));
        var hullCol = new Color(200, 206, 214);
        var wings = new Path().MoveTo(48, 34).LineTo(90, 76).LineTo(88, 88).LineTo(60, 82).LineTo(36, 82).LineTo(8, 88).LineTo(6, 76).Close();
        ArtKit.Body(c, wings, ArtKit.Darker(hullCol, 0.15f), 34, 88);
        c.Fill(Path.Poly(80, 70, 90, 76, 88, 88, 80, 86), new Color(210, 40, 40));
        c.Fill(Path.Poly(16, 70, 6, 76, 8, 88, 16, 86), new Color(210, 40, 40));
        var fus = new Path().Smooth(new Vector2(48, 4), new Vector2(56, 30), new Vector2(60, 70), new Vector2(56, 90),
            new Vector2(40, 90), new Vector2(36, 70), new Vector2(40, 30));
        c.Fill(fus, Brush.Linear(new Vector2(36, 0), new Vector2(60, 0), (0f, ArtKit.Lighter(hullCol, 0.4f)), (0.4f, hullCol), (1f, ArtKit.Darker(hullCol, 0.45f))));
        c.Stroke(fus, 1.2f, new Color(40, 44, 54), 0.9f);
        ArtKit.Glass(c, new Path().Smooth(new Vector2(48, 24), new Vector2(53, 36), new Vector2(51, 48), new Vector2(45, 48), new Vector2(43, 36)), 43, 24, 53, 48, new Color(70, 150, 230));
        c.Fill(Path.Rect(44, 86, 8, 4), new Color(60, 64, 72));
        // wing cannons
        foreach (float x in new[] { 20f, 76f }) c.Fill(Path.RoundRect(x - 2, 58, 4, 18, 2), new Color(90, 94, 104));
        return c;
    }

    /// <summary>The fighter's laser bolt: 16 x 64.</summary>
    public static Canvas Bolt()
    {
        var c = new Canvas(16, 64);
        c.Fill(Path.RoundRect(2, 2, 12, 60, 6), Brush.Radial(new Vector2(8, 32), 8, 32, (0f, new Color(255, 255, 220, 255)), (0.4f, new Color(255, 230, 80, 220)), (1f, new Color(255, 120, 20, 0))));
        c.Fill(Path.RoundRect(6, 6, 4, 52, 2), new Color(255, 255, 240));
        return c;
    }

    /// <summary>An alien's plasma bomb: 32 x 32.</summary>
    public static Canvas Bomb()
    {
        var c = new Canvas(32, 32);
        c.Fill(Path.Circle(16, 16, 15), Brush.Radial(16, 16, 15, new Color(255, 120, 255, 200), new Color(160, 40, 255, 0)));
        c.Fill(Path.Circle(16, 16, 6), Brush.Radial(new Vector2(14, 14), 7, 7, (0f, Color.White), (0.6f, new Color(255, 180, 255)), (1f, new Color(220, 80, 255))));
        return c;
    }

    /// <summary>The shield: a shimmering energy bubble: 160 x 160.</summary>
    public static Canvas Shield()
    {
        var c = new Canvas(160, 160);
        c.Fill(Path.Circle(80, 80, 76), Brush.Func((x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(80, 80)) / 76;
            float hex = MathF.Abs(MathF.Sin(x * 0.35f) * MathF.Sin(y * 0.35f + x * 0.2f));
            float a = MathF.Pow(d, 4) * 0.85f + hex * 0.12f * d;
            return new Vector4(0.45f, 0.85f, 1f, Math.Clamp(a, 0, 1));
        }));
        c.Stroke(Path.Circle(80, 80, 75), 2, new Color(200, 245, 255), 0.8f);
        c.Fill(Path.Ellipse(56, 44, 22, 10), new Color(255, 255, 255), 0.25f);
        return c;
    }

    /// <summary>An alien bursting: a flash, then sparks and glowing debris (five frames): 128 x 128.</summary>
    public static Canvas Burst(int frame)
    {
        var c = new Canvas(128, 128);
        float k = frame / 4f;
        float flash = MathF.Max(0, 1 - k * 1.6f);
        if (flash > 0)
            c.Fill(Path.Circle(64, 64, 20 + 30 * k), Brush.Radial(64, 64, 20 + 30 * k, new Color(255, 240, 200), new Color(255, 100, 40, 0)), flash);
        for (int i = 0; i < 18; i++)
        {
            float a = i * 2.4f + 0.7f, sp = 0.6f + Noise.Value(i * 3.1f, 0, 900) * 0.6f;
            float r0 = 6 + 50 * k * sp, r1 = r0 + 6 + 8 * (1 - k);
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var col = i % 3 == 0 ? new Color(255, 255, 220) : i % 3 == 1 ? new Color(255, 190, 60) : new Color(255, 90, 40);
            c.Stroke(new Path().MoveTo(new Vector2(64, 64) + dir * r0).LineTo(new Vector2(64, 64) + dir * r1), 2.2f - k, col, 1 - k * 0.8f);
        }
        return c;
    }
}
