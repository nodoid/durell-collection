using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Durell.Graphics.Art;

/// <summary>Harrier Attack's artwork.</summary>
internal static class HarrierArt
{
    public static void Register(List<(string, Func<Canvas>)> all)
    {
        all.Add(("harrier-jet", Jet));
        all.Add(("harrier-carrier", Carrier));
        all.Add(("harrier-frigate", Frigate));
        all.Add(("harrier-mig", Mig));
        all.Add(("harrier-rocket", Rocket));
        all.Add(("harrier-bomb", Bomb));
        for (int i = 0; i < 4; i++)
        {
            int f = i;
            all.Add(($"harrier-blast{f}", () => Blast(f)));
        }
        all.Add(("harrier-flak", Flak));
        all.Add(("harrier-clouds", Clouds));
        all.Add(("harrier-sea", Sea));
    }

    private static readonly Color SeaGrey = new(84, 96, 108);
    private static readonly Color DarkGreen = new(62, 76, 54);
    private static readonly Color Underside = new(168, 176, 184);

    /// <summary>The player's Harrier GR.3, side view, flying right: 256 x 96.</summary>
    public static Canvas Jet()
    {
        var c = new Canvas(256, 96);

        // tailplane (far side, drawn first) and the fin
        var tailplane = Path.Poly(18, 50, 30, 46, 62, 47, 58, 52, 26, 54);
        ArtKit.Body(c, tailplane, ArtKit.Darker(SeaGrey, 0.15f), 46, 54);
        var fin = new Path().MoveTo(22, 44).LineTo(12, 10).QuadTo(16, 7, 24, 9).LineTo(64, 42).Close();
        ArtKit.Body(c, fin, SeaGrey, 8, 44);

        // fuselage
        var body = new Path()
            .MoveTo(16, 42)
            .CubicTo(60, 36, 120, 34, 160, 32)
            .CubicTo(176, 30, 196, 32, 214, 38)
            .CubicTo(226, 42, 236, 45, 244, 48)
            .CubicTo(236, 52, 222, 55, 206, 57)
            .CubicTo(188, 61, 168, 63, 146, 62)
            .CubicTo(110, 61, 70, 58, 18, 52)
            .Close();
        Camouflaged(c, body, 30, 63, 3);

        // jet nozzles (front pair and rear pair, angled for forward flight)
        foreach (var (x, y) in new[] { (126f, 58f), (164f, 60f) })
        {
            var noz = Path.RoundRect(-7, -5, 14, 18, 4).Transform(Matrix.CreateRotationZ(0.55f) * Matrix.CreateTranslation(x, y, 0));
            ArtKit.Body(c, noz, new Color(58, 60, 64), y - 6, y + 14);
            c.Fill(Path.Ellipse(x - 5, y + 9, 4, 2.5f).Transform(Matrix.Identity), new Color(20, 20, 22));
        }

        // the wing, nearly edge-on, with its drooping tip and outrigger
        var wing = new Path().MoveTo(96, 40).LineTo(150, 37).LineTo(160, 41).LineTo(104, 46).Close();
        ArtKit.Body(c, wing, ArtKit.Darker(SeaGrey, 0.2f), 37, 46, 1f);
        c.Fill(Path.RoundRect(98, 44, 8, 10, 3), new Color(50, 52, 56));

        // air intake: a dark mouth with a lip
        var intake = new Path().MoveTo(172, 38).CubicTo(184, 36, 190, 44, 186, 54).CubicTo(180, 58, 170, 56, 168, 48).Close();
        c.Fill(intake, Brush.Radial(new Vector2(176, 46), 12, 10, (0f, new Color(10, 10, 12)), (0.8f, new Color(30, 32, 36)), (1f, new Color(90, 98, 106))));
        c.Stroke(intake, 1.4f, ArtKit.Lighter(SeaGrey, 0.25f), 0.9f);
        // roundel on the intake side
        c.Fill(Path.Circle(160, 47, 6.5f), new Color(30, 60, 140));
        c.Fill(Path.Circle(160, 47, 4.2f), new Color(220, 220, 220));
        c.Fill(Path.Circle(160, 47, 2.2f), new Color(190, 30, 36));

        // canopy
        var canopy = new Path().MoveTo(178, 33).CubicTo(186, 22, 204, 22, 216, 37).CubicTo(204, 36, 190, 35, 178, 33).Close();
        ArtKit.Glass(c, canopy, 190, 22, 200, 38, new Color(60, 90, 120));
        c.Stroke(new Path().MoveTo(186, 26).QuadTo(194, 23, 204, 27), 1.6f, new Color(255, 255, 255), 0.6f);

        // nose probe and panel lines
        c.Stroke(new Path().MoveTo(240, 48).LineTo(252, 48.5f), 1.2f, new Color(40, 40, 44));
        c.Stroke(new Path().MoveTo(150, 34).LineTo(152, 61), 0.6f, new Color(30, 34, 38), 0.5f);
        c.Stroke(new Path().MoveTo(90, 36).LineTo(92, 58), 0.6f, new Color(30, 34, 38), 0.5f);
        c.Stroke(new Path().MoveTo(40, 40).LineTo(42, 54), 0.6f, new Color(30, 34, 38), 0.5f);

        return ArtKit.WithShadow(c, 3, 5, 3, 0.35f);
    }

    /// <summary>Grey/green disruptive camouflage over a light underside, with rounded lighting.</summary>
    private static void Camouflaged(Canvas c, Path p, float top, float bottom, int seed)
    {
        float split = top + (bottom - top) * 0.62f;
        c.Fill(p, Brush.Func((x, y) =>
        {
            if (y > split + Noise.Value(x * 0.08f, 0, seed) * 3) return Underside.ToVector4();
            float n = Noise.Fbm(x * 0.035f, y * 0.06f, seed, 3);
            return (n > 0.52f ? DarkGreen : SeaGrey).ToVector4();
        }));
        // roundness: highlight along the spine, shadow along the belly
        c.Fill(p, Brush.Linear(new Vector2(0, top), new Vector2(0, bottom),
            (0f, new Color(255, 255, 255, 120)), (0.2f, new Color(255, 255, 255, 40)), (0.45f, new Color(0, 0, 0, 0)),
            (0.8f, new Color(0, 0, 0, 60)), (1f, new Color(0, 0, 0, 130))));
        c.Stroke(p, 1.2f, new Color(24, 28, 32), 0.85f);
    }

    private static readonly Color Hull = new(98, 106, 116);
    private static readonly Color Deck = new(70, 74, 80);

    /// <summary>The aircraft carrier, bow to the right with a ski-jump: 640 x 160.</summary>
    public static Canvas Carrier()
    {
        var c = new Canvas(640, 160);
        // island superstructure, mast and radar (behind the deck edge)
        var island = Path.Poly(380, 92, 386, 60, 420, 56, 436, 40, 470, 40, 478, 60, 500, 64, 504, 92);
        ArtKit.Body(c, island, ArtKit.Lighter(Hull, 0.1f), 40, 92);
        for (int i = 0; i < 6; i++) c.Fill(Path.Rect(392 + i * 18, 70, 10, 5), new Color(30, 40, 52));
        c.Fill(Path.Rect(440, 46, 30, 6), new Color(28, 36, 46));
        c.Stroke(new Path().MoveTo(452, 40).LineTo(452, 8), 3, new Color(70, 74, 80));
        c.Stroke(new Path().MoveTo(440, 18).LineTo(466, 18), 2, new Color(70, 74, 80));
        c.Fill(Path.RoundRect(436, 24, 32, 7, 3), new Color(150, 156, 164));
        c.Fill(Path.Rect(410, 34, 14, 22), new Color(60, 64, 70));                 // funnel
        c.Fill(Path.Rect(410, 32, 14, 4), new Color(24, 24, 26));

        // hull: the flight deck slopes up into the ski-jump at the bow
        var hull = new Path().MoveTo(8, 96).LineTo(560, 96).QuadTo(600, 94, 626, 78).LineTo(632, 82)
            .QuadTo(620, 118, 590, 134).LineTo(70, 136).QuadTo(30, 130, 14, 112).Close();
        c.Fill(hull, Brush.Linear(new Vector2(0, 90), new Vector2(0, 136),
            (0f, ArtKit.Lighter(Hull, 0.3f)), (0.25f, Hull), (1f, ArtKit.Darker(Hull, 0.45f))));
        // weathering streaks and the waterline
        c.Fill(hull, Brush.Func((x, y) => new Vector4(0.15f, 0.12f, 0.1f, MathF.Max(0, Noise.Fbm(x * 0.2f, y * 0.02f, 8, 3) - 0.55f) * 1.2f)));
        c.Fill(Path.Rect(40, 128, 560, 8), new Color(120, 36, 34), 0.9f);
        c.Stroke(hull, 1.4f, new Color(30, 34, 40), 0.9f);
        // portholes
        for (int x = 60; x < 560; x += 22) c.Fill(Path.Circle(x, 110, 1.8f), new Color(30, 34, 40));
        // flight deck surface with markings
        var deck = new Path().MoveTo(8, 92).LineTo(560, 92).QuadTo(600, 90, 626, 74).LineTo(626, 79).QuadTo(600, 96, 560, 97).LineTo(8, 97).Close();
        c.Fill(deck, Deck);
        c.Stroke(new Path().MoveTo(20, 94.5f).LineTo(556, 94.5f), 0.8f, new Color(230, 230, 220), 0.8f);
        for (int x = 30; x < 540; x += 34) c.Fill(Path.Rect(x, 93.5f, 14, 2), new Color(240, 210, 60), 0.85f);
        return ArtKit.WithShadow(c, 0, 0, 0, 0);
    }

    /// <summary>An enemy frigate, bow to the left: 320 x 112.</summary>
    public static Canvas Frigate()
    {
        var c = new Canvas(320, 112);
        var grey = new Color(118, 124, 120);
        var sup = Path.Poly(110, 70, 116, 48, 150, 46, 158, 30, 196, 30, 204, 48, 232, 50, 238, 70);
        ArtKit.Body(c, sup, grey, 30, 70);
        for (int i = 0; i < 5; i++) c.Fill(Path.Rect(162 + i * 7, 36, 4, 4), new Color(26, 30, 34));
        c.Stroke(new Path().MoveTo(176, 30).LineTo(176, 4), 2.5f, new Color(80, 84, 82));
        c.Stroke(new Path().MoveTo(166, 12).LineTo(186, 12), 2, new Color(80, 84, 82));
        c.Fill(Path.Ellipse(176, 22, 9, 4), new Color(150, 154, 150));
        // gun turret and barrel at the bow
        c.Stroke(new Path().MoveTo(70, 60).LineTo(40, 54), 3, new Color(60, 62, 60));
        ArtKit.Body(c, Path.RoundRect(64, 56, 26, 14, 6), grey, 56, 70);
        // missile launcher aft
        ArtKit.Body(c, Path.Poly(250, 70, 252, 58, 276, 56, 280, 70), ArtKit.Darker(grey, 0.1f), 56, 70);
        var hull = new Path().MoveTo(10, 66).QuadTo(40, 70, 300, 70).LineTo(306, 74).LineTo(300, 92).LineTo(46, 94).Close();
        c.Fill(hull, Brush.Linear(new Vector2(0, 66), new Vector2(0, 94),
            (0f, ArtKit.Lighter(grey, 0.25f)), (0.3f, grey), (1f, ArtKit.Darker(grey, 0.5f))));
        c.Fill(Path.Rect(40, 88, 264, 6), new Color(110, 34, 30), 0.85f);
        c.Stroke(hull, 1.2f, new Color(30, 34, 36), 0.9f);
        // hull number
        c.Fill(Path.Rect(30, 74, 4, 8), new Color(240, 240, 240), 0.8f);
        c.Fill(Path.Rect(37, 74, 4, 8), new Color(240, 240, 240), 0.8f);
        return c;
    }

    /// <summary>An enemy fighter, flying left: 224 x 80.</summary>
    public static Canvas Mig()
    {
        var c = new Canvas(224, 80);
        var skin = new Color(150, 156, 140);
        var fin = new Path().MoveTo(176, 38).LineTo(196, 6).QuadTo(204, 4, 206, 8).LineTo(204, 40).Close();
        ArtKit.Body(c, fin, ArtKit.Darker(skin, 0.1f), 6, 40);
        var body = new Path().MoveTo(8, 42).CubicTo(30, 34, 70, 30, 120, 32).CubicTo(160, 33, 196, 36, 214, 38)
            .LineTo(216, 48).CubicTo(180, 52, 120, 54, 70, 52).CubicTo(40, 50, 20, 47, 8, 42).Close();
        c.Fill(body, Brush.Func((x, y) =>
        {
            float n = Noise.Fbm(x * 0.04f, y * 0.07f, 21, 3);
            return (n > 0.55f ? new Color(110, 120, 90) : n < 0.4f ? new Color(170, 160, 130) : skin).ToVector4();
        }));
        c.Fill(body, Brush.Linear(new Vector2(0, 30), new Vector2(0, 54),
            (0f, new Color(255, 255, 255, 110)), (0.3f, new Color(255, 255, 255, 0)), (0.75f, new Color(0, 0, 0, 50)), (1f, new Color(0, 0, 0, 120))));
        c.Stroke(body, 1.2f, new Color(30, 32, 28), 0.85f);
        // swept wing and tailplane
        ArtKit.Body(c, Path.Poly(90, 44, 150, 42, 170, 52, 104, 50), ArtKit.Darker(skin, 0.25f), 42, 52, 1f);
        ArtKit.Body(c, Path.Poly(176, 44, 206, 44, 214, 52, 184, 50), ArtKit.Darker(skin, 0.25f), 44, 52, 1f);
        // intake, canopy, star, exhaust glow
        c.Fill(Path.Ellipse(72, 42, 8, 6), new Color(20, 20, 22));
        ArtKit.Glass(c, new Path().MoveTo(30, 36).CubicTo(38, 26, 56, 26, 64, 34).Close(), 40, 26, 50, 36, new Color(70, 100, 90));
        Star(c, 186, 26, 6, new Color(200, 30, 30));
        c.Fill(Path.Ellipse(216, 43, 4, 5), Brush.Radial(216, 43, 5, new Color(255, 230, 160), new Color(255, 120, 40, 0)));
        return ArtKit.WithShadow(c, 2, 4, 3, 0.3f);
    }

    private static void Star(Canvas c, float cx, float cy, float r, Color col)
    {
        var p = new Path();
        for (int i = 0; i < 10; i++)
        {
            float a = -MathF.PI / 2 + i * MathF.PI / 5, rr = i % 2 == 0 ? r : r * 0.42f;
            var v = new Vector2(cx + MathF.Cos(a) * rr, cy + MathF.Sin(a) * rr);
            if (i == 0) p.MoveTo(v);
            else p.LineTo(v);
        }
        p.Close();
        c.Fill(p, col);
        c.Stroke(p, 0.8f, ArtKit.Darker(col, 0.5f), 0.8f);
    }

    /// <summary>A rocket with its exhaust flame, flying right: 96 x 24.</summary>
    public static Canvas Rocket()
    {
        var c = new Canvas(96, 24);
        c.Fill(new Path().MoveTo(2, 12).QuadTo(20, 4, 44, 9).LineTo(44, 15).QuadTo(20, 20, 2, 12).Close(),
            Brush.Linear(new Vector2(44, 0), new Vector2(0, 0), (0f, new Color(255, 250, 210)), (0.3f, new Color(255, 190, 70, 230)), (1f, new Color(255, 80, 20, 0))));
        var body = new Path().MoveTo(40, 9).LineTo(80, 9).QuadTo(92, 12, 80, 15).LineTo(40, 15).Close();
        ArtKit.Body(c, body, new Color(220, 222, 214), 9, 15, 0.8f);
        c.Fill(Path.Poly(40, 9, 48, 9, 42, 4), new Color(90, 94, 100));
        c.Fill(Path.Poly(40, 15, 48, 15, 42, 20), new Color(90, 94, 100));
        return c;
    }

    /// <summary>A falling bomb: 48 x 20 (nose right; rotated as it falls).</summary>
    public static Canvas Bomb()
    {
        var c = new Canvas(48, 20);
        var body = new Path().MoveTo(8, 10).CubicTo(10, 3, 30, 3, 40, 6).QuadTo(47, 10, 40, 14).CubicTo(30, 17, 10, 17, 8, 10).Close();
        ArtKit.Body(c, body, new Color(70, 80, 60), 4, 16, 0.9f);
        c.Fill(Path.Poly(2, 3, 12, 8, 12, 12, 2, 17), new Color(60, 66, 54));
        c.Fill(Path.Rect(30, 5, 2, 10), new Color(220, 200, 60), 0.8f);
        return c;
    }

    /// <summary>A fireball, four frames from flash to smoke: 128 x 128.</summary>
    public static Canvas Blast(int frame)
    {
        var c = new Canvas(128, 128);
        float k = frame / 3f;
        float r = 26 + 30 * k;
        // smoke first, then fire on top (fire fades as the smoke grows)
        c.Fill(Path.Circle(64, 64 - k * 8, r), Brush.Func((x, y) =>
        {
            float dx = (x - 64) / r, dy = (y - 64 + k * 8) / r;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            float n = Noise.Fbm(x * 0.06f + frame * 3, y * 0.06f, 50, 4);
            float a = Math.Clamp((1 - d) * 2.2f + (n - 0.5f) * 1.6f, 0, 1) * (0.4f + 0.5f * k);
            float g = 0.16f + 0.18f * n;
            return new Vector4(g, g, g * 1.05f, a);
        }));
        float fire = 1 - k * 0.85f;
        c.Fill(Path.Circle(64, 64, r * 0.85f), Brush.Func((x, y) =>
        {
            float dx = (x - 64) / (r * 0.85f), dy = (y - 64) / (r * 0.85f);
            float d = MathF.Sqrt(dx * dx + dy * dy);
            float n = Noise.Fbm(x * 0.08f, y * 0.08f + frame * 5, 60, 4);
            float heat = Math.Clamp(1.25f - d * 1.3f + (n - 0.5f) * 1.2f, 0, 1);
            var col = Vector4.Lerp(new Vector4(0.8f, 0.15f, 0.02f, 0), new Vector4(1, 0.95f, 0.7f, 1), heat);
            col.W = Math.Clamp(heat * 1.6f, 0, 1) * fire;
            return col;
        }), 1, BlendMode.Normal);
        return c;
    }

    /// <summary>An anti-aircraft shell burst: 64 x 64.</summary>
    public static Canvas Flak()
    {
        var c = new Canvas(64, 64);
        c.Fill(Path.Circle(32, 32, 26), Brush.Func((x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32)) / 26;
            float n = Noise.Fbm(x * 0.12f, y * 0.12f, 70, 4);
            float a = Math.Clamp((1 - d) * 2 + (n - 0.5f) * 1.8f, 0, 1) * 0.9f;
            float g = 0.12f + 0.2f * n;
            return new Vector4(g, g, g, a);
        }));
        c.Fill(Path.Circle(32, 32, 7), Brush.Radial(32, 32, 7, new Color(255, 240, 180), new Color(255, 120, 30, 0)));
        return c;
    }

    /// <summary>A band of cumulus clouds, transparent around them, tiling left to right: 1024 x 256.</summary>
    public static Canvas Clouds()
    {
        const int w = 1024, h = 256;
        var c = new Canvas(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w * 8, v = y / (float)h * 2;
                float n = Noise.Fbm(u, v, 101, 6, 8);
                // flat cloud bases, puffy tops: density falls off towards the bottom and top of the band
                float band = MathF.Sin(MathF.PI * Math.Clamp(y / (float)h * 1.15f - 0.05f, 0, 1));
                float d = Math.Clamp((n - 0.47f + 0.14f * band) * 5f * band, 0, 1);
                if (d <= 0) continue;
                // lit from above: the broad shape (few octaves) decides light and shade, not the fine detail
                float broad = Noise.Fbm(u, v, 101, 3, 8);
                float broadAbove = Noise.Fbm(u, (y - 18) / (float)h * 2, 101, 3, 8);
                float light = Math.Clamp(0.8f + (broad - broadAbove) * 3.5f, 0.58f, 1);
                float shade = light * (0.88f + 0.12f * (1 - y / (float)h));
                c.Set(x, y, new Vector4(shade, shade, MathF.Min(1, shade * 1.03f + 0.02f), d * d * (3 - 2 * d)));
            }
        return c;
    }

    /// <summary>Sea surface: waves and glints, tiling in both directions: 512 x 128.</summary>
    public static Canvas Sea()
    {
        const int w = 512, h = 128;
        var c = new Canvas(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w * 16, v = y / (float)h * 4;
                float r = Noise.Ridged(u, v * 2.5f, 202, 4, 16);
                float n = Noise.Fbm(u * 0.5f, v, 203, 4, 8);
                var deep = new Vector3(0.05f, 0.22f, 0.38f);
                var lit = new Vector3(0.2f, 0.5f, 0.68f);
                var col = Vector3.Lerp(deep, lit, n * 0.8f + r * 0.35f);
                if (r > 0.72f) col = Vector3.Lerp(col, new Vector3(0.85f, 0.93f, 1f), (r - 0.72f) * 2.4f);
                c.Set(x, y, new Vector4(col, 1));
            }
        return c;
    }
}
