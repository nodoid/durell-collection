using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Durell.Graphics.Art;

/// <summary>Scuba Dive's artwork: the diver and the sea creatures (all facing right; mirrored to face left).</summary>
internal static class ScubaArt
{
    public static void Register(List<(string, Func<Canvas>)> all)
    {
        for (int i = 0; i < 4; i++)
        {
            int f = i;
            all.Add(($"scuba-diver{f}", () => Diver(f)));
            all.Add(($"scuba-shark{f}", () => Shark(f)));
            all.Add(($"scuba-jelly{f}", () => Jelly(f)));
            all.Add(($"scuba-squid{f}", () => Squid(f)));
            all.Add(($"scuba-octopus{f}", () => Octopus(f)));
            all.Add(($"scuba-eel{f}", () => Eel(f)));
            all.Add(($"scuba-serpent{f}", () => Serpent(f)));
            all.Add(($"scuba-urchin{f}", () => Urchin(f)));
        }
        all.Add(("scuba-oyster-open", () => Oyster(true)));
        all.Add(("scuba-oyster-shut", () => Oyster(false)));
        all.Add(("scuba-pearl", Pearl));
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            all.Add(($"scuba-item{k}", () => Item(k)));
        }
        all.Add(("scuba-boat", Boat));
    }

    private static float Swing(int frame, float amount) => MathF.Sin(frame * MathF.PI / 2) * amount;

    /// <summary>The diver swimming right, kicking (four frames): 192 x 96.</summary>
    public static Canvas Diver(int frame)
    {
        var c = new Canvas(192, 96);
        var suit = new Color(28, 30, 36);
        var trim = new Color(230, 200, 40);
        float kick = Swing(frame, 9);
        // far leg and fin
        Leg(c, 70, 50, 26 + kick, ArtKit.Darker(suit, 0.3f), new Color(160, 130, 20));
        // air tank on the back
        var tank = Path.RoundRect(64, 26, 62, 15, 7);
        ArtKit.Body(c, tank, new Color(200, 205, 210), 26, 41);
        c.Fill(Path.RoundRect(124, 29, 8, 9, 3), new Color(70, 72, 76));
        // body
        var body = new Path().MoveTo(66, 44).CubicTo(80, 36, 120, 36, 140, 40).CubicTo(150, 42, 152, 54, 140, 58)
            .CubicTo(120, 62, 84, 60, 66, 56).Close();
        ArtKit.Body(c, body, suit, 36, 60, 1.1f, 0.8f);
        c.Stroke(new Path().MoveTo(80, 42).LineTo(80, 58), 3, trim, 0.9f);
        // near arm reaching forward
        float reach = Swing(frame + 1, 4);
        var arm = new Path().MoveTo(130, 46).QuadTo(150, 50 + reach, 168, 50 + reach);
        c.Stroke(arm, 8, suit);
        c.Fill(Path.Circle(170, 50 + reach, 4.5f), new Color(40, 44, 50));
        // head, mask and regulator hose
        ArtKit.Body(c, Path.Circle(152, 40, 11), suit, 29, 51, 1, 0.9f);
        ArtKit.Glass(c, Path.RoundRect(154, 32, 12, 10, 4), 154, 32, 166, 42, new Color(90, 170, 210));
        c.Stroke(new Path().MoveTo(150, 50).QuadTo(138, 56, 128, 38), 2, new Color(20, 20, 24), 0.9f);
        // near leg and fin
        Leg(c, 72, 54, 30 - kick, suit, trim);
        // bubbles from the regulator
        for (int i = 0; i < 3; i++)
        {
            float bx = 160 - i * 3 + Swing(frame + i, 2), by = 26 - i * 9 - frame * 2;
            c.Stroke(Path.Circle(bx, by, 2 + i * 0.7f), 0.8f, new Color(220, 240, 255), 0.8f);
        }
        return c;
    }

    private static void Leg(Canvas c, float hipX, float hipY, float angleDeg, Color suit, Color fin)
    {
        float a = MathHelper.ToRadians(180 + angleDeg * 0.4f);
        var knee = new Vector2(hipX + MathF.Cos(a) * 28, hipY - MathF.Sin(a) * 28 * 0.3f + angleDeg * 0.15f);
        var foot = knee + new Vector2(-26, (angleDeg - 28) * 0.6f);
        c.Stroke(new Path().MoveTo(hipX, hipY).LineTo(knee).LineTo(foot), 10, suit);
        var dir = Vector2.Normalize(foot - knee);
        var side = new Vector2(-dir.Y, dir.X);
        var tip = foot + dir * 28;
        var finPath = Path.Polygon(foot + side * 4, tip + side * 7, tip - side * 7, foot - side * 4);
        c.Fill(finPath, Brush.Linear(foot, tip, (0f, fin), (1f, ArtKit.Darker(fin, 0.4f))));
        c.Stroke(finPath, 0.8f, ArtKit.Darker(fin, 0.7f), 0.8f);
    }

    /// <summary>A grey shark swimming right, tail sweeping: 256 x 96.</summary>
    public static Canvas Shark(int frame)
    {
        var c = new Canvas(256, 96);
        var top = new Color(96, 110, 124);
        var belly = new Color(222, 226, 228);
        float tail = Swing(frame, 10);
        // tail fin
        var tailFin = new Path().MoveTo(48, 48).LineTo(12, 18 + tail).QuadTo(22, 44 + tail * 0.5f, 14, 78 + tail).Close();
        ArtKit.Body(c, tailFin, top, 18, 78);
        // body: counter-shaded, darker above, white belly
        var body = new Path().MoveTo(40, 48).CubicTo(80, 30, 160, 26, 210, 38).CubicTo(236, 44, 248, 50, 250, 54)
            .CubicTo(236, 60, 200, 66, 160, 66).CubicTo(110, 66, 70, 60, 40, 52).Close();
        c.Fill(body, Brush.Linear(new Vector2(0, 30), new Vector2(0, 66),
            (0f, ArtKit.Lighter(top, 0.2f)), (0.45f, top), (0.62f, ArtKit.Mix(top, belly, 0.6f)), (0.75f, belly), (1f, ArtKit.Darker(belly, 0.2f))));
        c.Stroke(body, 1.1f, ArtKit.Darker(top, 0.6f), 0.8f);
        // dorsal and pectoral fins
        ArtKit.Body(c, new Path().MoveTo(130, 32).LineTo(148, 6).QuadTo(156, 20, 168, 30).Close(), top, 6, 32);
        ArtKit.Body(c, new Path().MoveTo(170, 60).LineTo(150, 86).QuadTo(168, 80, 190, 62).Close(), ArtKit.Darker(top, 0.1f), 60, 86);
        // gills, eye, mouth
        for (int i = 0; i < 4; i++) c.Stroke(new Path().MoveTo(196 - i * 5, 46).QuadTo(194 - i * 5, 52, 196 - i * 5, 58), 1, ArtKit.Darker(top, 0.5f), 0.7f);
        c.Fill(Path.Circle(226, 46, 2.6f), new Color(10, 10, 10));
        c.Fill(Path.Circle(225.3f, 45.3f, 0.8f), new Color(255, 255, 255));
        c.Stroke(new Path().MoveTo(218, 60).QuadTo(234, 64, 246, 57), 1.2f, new Color(70, 40, 44), 0.9f);
        return c;
    }

    /// <summary>A translucent jellyfish, pulsing: 96 x 128.</summary>
    public static Canvas Jelly(int frame)
    {
        var c = new Canvas(96, 128);
        float pulse = Swing(frame, 5);
        var bell = new Path().MoveTo(14 - pulse, 54).CubicTo(10 - pulse, 18, 86 + pulse, 18, 82 + pulse, 54)
            .QuadTo(70, 62, 48, 58).QuadTo(26, 62, 14 - pulse, 54).Close();
        // tentacles first (behind)
        for (int i = 0; i < 6; i++)
        {
            float x = 22 + i * 10.5f;
            var t = new Path().MoveTo(x, 56);
            for (int k = 1; k <= 6; k++) t.LineTo(x + MathF.Sin(k * 0.9f + i + frame * 1.2f) * 4, 56 + k * 11);
            c.Stroke(t, 1.6f, new Color(255, 180, 220), 0.55f);
        }
        c.Fill(bell, Brush.Radial(new Vector2(40, 34), 44, 34, (0f, new Color(255, 230, 245, 230)), (0.6f, new Color(240, 120, 200, 170)), (1f, new Color(200, 70, 170, 120))));
        c.Stroke(bell, 1.2f, new Color(255, 210, 240), 0.8f);
        c.Fill(Path.Ellipse(48, 46, 18, 7), new Color(255, 120, 190), 0.6f);
        c.Fill(Path.Ellipse(36, 30, 10, 5), new Color(255, 255, 255), 0.5f);
        return c;
    }

    /// <summary>A squid jetting right: 192 x 80.</summary>
    public static Canvas Squid(int frame)
    {
        var c = new Canvas(192, 80);
        var skin = new Color(230, 150, 140);
        float wave = frame * 1.3f;
        for (int i = 0; i < 7; i++)
        {
            float y = 30 + i * 3.5f;
            var t = new Path().MoveTo(62, y);
            for (int k = 1; k <= 6; k++) t.LineTo(62 - k * 9, y + (i - 3) * k * 0.9f + MathF.Sin(k * 0.8f + i + wave) * 3);
            c.Stroke(t, 3.2f - i * 0.1f, ArtKit.Darker(skin, 0.15f));
        }
        var mantle = new Path().MoveTo(60, 32).CubicTo(100, 22, 150, 24, 182, 40).CubicTo(150, 56, 100, 58, 60, 50).Close();
        c.Fill(mantle, Brush.Func((x, y) =>
        {
            float n = Noise.Fbm(x * 0.15f, y * 0.15f, 300, 3);
            var col = ArtKit.Mix(skin, new Color(170, 60, 70), MathF.Max(0, n - 0.5f) * 3);
            return col.ToVector4();
        }));
        c.Fill(mantle, Brush.Linear(new Vector2(0, 24), new Vector2(0, 56), (0f, new Color(255, 255, 255, 90)), (0.5f, new Color(0, 0, 0, 0)), (1f, new Color(0, 0, 0, 90))));
        c.Fill(new Path().MoveTo(150, 30).LineTo(186, 40).LineTo(150, 50).QuadTo(160, 40, 150, 30).Close(), ArtKit.Darker(skin, 0.1f));
        c.Stroke(mantle, 1, ArtKit.Darker(skin, 0.6f), 0.7f);
        c.Fill(Path.Circle(70, 38, 5), new Color(250, 240, 200));
        c.Fill(Path.Circle(70, 38, 2.6f), new Color(10, 10, 20));
        return c;
    }

    /// <summary>The red octopus guarding the caves, arms curling: 224 x 160.</summary>
    public static Canvas Octopus(int frame)
    {
        var c = new Canvas(224, 160);
        var skin = new Color(200, 54, 48);
        for (int i = 0; i < 8; i++)
        {
            float baseX = 70 + i * 12;
            float dir = (i - 3.5f) / 3.5f;
            var arm = new Path().MoveTo(baseX, 92);
            var pts = new List<Vector2> { new(baseX, 92) };
            for (int k = 1; k <= 7; k++)
            {
                float a = dir * (0.5f + k * 0.18f) + MathF.Sin(k * 0.9f + i * 1.7f + frame * 1.1f) * 0.35f;
                var last = pts[^1];
                pts.Add(last + new Vector2(MathF.Sin(a) * 9, MathF.Cos(a) * 8));
            }
            for (int k = 1; k < pts.Count; k++)
            {
                float w = 9 - k * 1.1f;
                c.Stroke(new Path().MoveTo(pts[k - 1]).LineTo(pts[k]), w, ArtKit.Darker(skin, 0.05f + k * 0.03f));
            }
            for (int k = 2; k < pts.Count; k += 2) c.Fill(Path.Circle(pts[k].X, pts[k].Y + 2, 1.4f), new Color(250, 200, 180), 0.8f);
        }
        var head = new Path().MoveTo(66, 94).CubicTo(56, 30, 168, 30, 158, 94).QuadTo(112, 104, 66, 94).Close();
        c.Fill(head, Brush.Func((x, y) =>
        {
            float n = Noise.Fbm(x * 0.09f, y * 0.09f, 400, 4);
            return ArtKit.Mix(skin, new Color(120, 20, 24), MathF.Max(0, n - 0.48f) * 2.5f).ToVector4();
        }));
        c.Fill(head, Brush.Radial(new Vector2(96, 50), 70, 50, (0f, new Color(255, 200, 180, 120)), (0.5f, new Color(255, 255, 255, 0)), (1f, new Color(0, 0, 0, 110))));
        c.Stroke(head, 1.2f, new Color(80, 14, 16), 0.85f);
        foreach (float ex in new[] { 92f, 132f })
        {
            c.Fill(Path.Ellipse(ex, 80, 10, 8), new Color(250, 230, 160));
            c.Fill(Path.Ellipse(ex + 2, 81, 6, 3), new Color(10, 10, 10));
        }
        return c;
    }

    /// <summary>A moray eel slithering right: 224 x 64.</summary>
    public static Canvas Eel(int frame)
    {
        var c = new Canvas(224, 64);
        var skin = new Color(90, 150, 70);
        var pts = new List<Vector2>();
        for (int i = 0; i <= 20; i++)
        {
            float x = 12 + i * 9.5f;
            pts.Add(new Vector2(x, 32 + MathF.Sin(i * 0.55f - frame * MathF.PI / 2) * (8 - i * 0.25f)));
        }
        for (int i = 1; i < pts.Count; i++)
        {
            float w = 4 + MathF.Min(i, 14) * 0.7f;
            c.Stroke(new Path().MoveTo(pts[i - 1]).LineTo(pts[i]), w, ArtKit.Darker(skin, 0.2f));
            c.Stroke(new Path().MoveTo(pts[i - 1] - new Vector2(0, w * 0.2f)).LineTo(pts[i] - new Vector2(0, w * 0.2f)), w * 0.45f, ArtKit.Lighter(skin, 0.15f));
        }
        var head = pts[^1];
        c.Fill(Path.Ellipse(head.X + 4, head.Y, 10, 7), ArtKit.Darker(skin, 0.1f));
        c.Fill(Path.Circle(head.X + 7, head.Y - 3, 1.8f), new Color(250, 240, 100));
        c.Stroke(new Path().MoveTo(head.X + 2, head.Y + 3).LineTo(head.X + 13, head.Y + 2), 1, new Color(30, 40, 20));
        return c;
    }

    /// <summary>An oyster on the sea bed, open (with its pearl showing) or shut: 96 x 64.</summary>
    public static Canvas Oyster(bool open)
    {
        var c = new Canvas(96, 64);
        var shell = new Color(150, 140, 120);
        var lower = new Path().MoveTo(10, 46).QuadTo(48, 64, 86, 46).QuadTo(48, 52, 10, 46).Close();
        c.Fill(lower, Brush.Linear(0, 44, 0, 60, ArtKit.Lighter(shell, 0.1f), ArtKit.Darker(shell, 0.5f)));
        if (open)
        {
            c.Fill(Path.Ellipse(48, 46, 30, 6), new Color(200, 170, 180));
            c.Fill(Path.Circle(48, 42, 7), Brush.Radial(new Vector2(45, 39), 9, 9, (0f, Color.White), (0.5f, new Color(240, 236, 228)), (1f, new Color(170, 165, 175))));
        }
        var upper = open
            ? new Path().MoveTo(10, 46).QuadTo(20, 8, 70, 14).QuadTo(52, 30, 10, 46).Close()
            : new Path().MoveTo(10, 46).QuadTo(48, 22, 86, 46).QuadTo(48, 50, 10, 46).Close();
        c.Fill(upper, Brush.Func((x, y) =>
        {
            float ring = MathF.Sin(MathF.Sqrt((x - 10) * (x - 10) + (y - 46) * (y - 46)) * 0.7f) * 0.5f + 0.5f;
            return ArtKit.Mix(ArtKit.Lighter(shell, 0.2f), ArtKit.Darker(shell, 0.3f), ring * 0.6f + Noise.Fbm(x * 0.2f, y * 0.2f, 500, 3) * 0.4f).ToVector4();
        }));
        c.Stroke(upper, 1, ArtKit.Darker(shell, 0.6f), 0.8f);
        c.Stroke(lower, 1, ArtKit.Darker(shell, 0.6f), 0.8f);
        return c;
    }

    /// <summary>A pearl: 32 x 32.</summary>
    public static Canvas Pearl()
    {
        var c = new Canvas(32, 32);
        c.Fill(Path.Circle(16, 16, 11), Brush.Radial(new Vector2(12, 12), 15, 15, (0f, Color.White), (0.45f, new Color(244, 238, 230)), (0.8f, new Color(200, 196, 210)), (1f, new Color(140, 136, 150))));
        c.Fill(Path.Ellipse(12, 11, 4, 2.5f), Color.White, 0.9f);
        return c;
    }

    /// <summary>The dive boat on the surface, bow to the right: 256 x 96.</summary>
    public static Canvas Boat()
    {
        var c = new Canvas(256, 96);
        var hull = new Path().MoveTo(10, 56).LineTo(246, 50).QuadTo(234, 76, 210, 84).LineTo(40, 84).QuadTo(18, 76, 10, 56).Close();
        c.Fill(hull, Brush.Linear(new Vector2(0, 50), new Vector2(0, 84), (0f, new Color(250, 250, 246)), (0.5f, new Color(220, 224, 226)), (1f, new Color(150, 156, 164))));
        c.Fill(Path.Rect(20, 66, 222, 5), new Color(30, 90, 170));
        c.Stroke(hull, 1.2f, new Color(60, 66, 74), 0.9f);
        // wheelhouse
        var house = Path.Poly(120, 54, 126, 26, 190, 26, 200, 52);
        ArtKit.Body(c, house, new Color(236, 238, 240), 26, 54);
        for (int i = 0; i < 3; i++) ArtKit.Glass(c, Path.RoundRect(132 + i * 20, 32, 14, 10, 2), 132, 32, 146, 42, new Color(60, 110, 150));
        // dive flag and two waiting divers' tanks
        c.Stroke(new Path().MoveTo(160, 26).LineTo(160, 4), 1.6f, new Color(80, 80, 80));
        c.Fill(Path.Rect(161, 5, 16, 11), new Color(220, 30, 30));
        c.Stroke(new Path().MoveTo(161, 16).LineTo(177, 5), 2.4f, Color.White);
        foreach (float x in new[] { 50f, 70f })
            ArtKit.Body(c, Path.RoundRect(x, 34, 10, 22, 5), new Color(240, 200, 40), 34, 56);
        return c;
    }

    /// <summary>A sea serpent undulating right, with a frilled head: 224 x 72.</summary>
    public static Canvas Serpent(int frame)
    {
        var c = new Canvas(224, 72);
        var skin = new Color(70, 110, 170);
        var pts = new List<Vector2>();
        for (int i = 0; i <= 22; i++)
            pts.Add(new Vector2(10 + i * 8.5f, 38 + MathF.Sin(i * 0.6f - frame * MathF.PI / 2) * (12 - i * 0.3f)));
        for (int i = 1; i < pts.Count; i++)
        {
            float w = 3 + MathF.Min(i, 16) * 0.75f;
            c.Stroke(new Path().MoveTo(pts[i - 1]).LineTo(pts[i]), w, ArtKit.Darker(skin, 0.25f));
            c.Stroke(new Path().MoveTo(pts[i - 1] - new Vector2(0, w * 0.25f)).LineTo(pts[i] - new Vector2(0, w * 0.25f)), w * 0.4f, ArtKit.Lighter(skin, 0.25f));
            if (i % 2 == 0) c.Fill(Path.Poly(pts[i].X - 3, pts[i].Y - w / 2, pts[i].X + 1, pts[i].Y - w / 2 - 5, pts[i].X + 4, pts[i].Y - w / 2), new Color(220, 90, 60));
        }
        var head = pts[^1];
        c.Fill(Path.Ellipse(head.X + 6, head.Y, 13, 8), ArtKit.Darker(skin, 0.15f));
        c.Fill(Path.Poly(head.X - 4, head.Y - 6, head.X + 2, head.Y - 18, head.X + 8, head.Y - 7), new Color(230, 110, 70));
        c.Fill(Path.Circle(head.X + 10, head.Y - 3, 2.2f), new Color(255, 230, 80));
        c.Stroke(new Path().MoveTo(head.X + 6, head.Y + 4).LineTo(head.X + 19, head.Y + 2), 1.2f, new Color(30, 20, 30));
        return c;
    }

    /// <summary>The spiny urchin that hunts the diver in the caves (four frames, spines twitching): 96 x 96.</summary>
    public static Canvas Urchin(int frame)
    {
        var c = new Canvas(96, 96);
        for (int i = 0; i < 28; i++)
        {
            float a = i * MathF.Tau / 28 + frame * 0.06f;
            float len = 34 + MathF.Sin(i * 2.7f + frame) * 6;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            c.Stroke(new Path().MoveTo(new Vector2(48, 48) + dir * 14).LineTo(new Vector2(48, 48) + dir * len), 2.2f, new Color(120, 40, 140));
            c.Stroke(new Path().MoveTo(new Vector2(48, 48) + dir * (len - 6)).LineTo(new Vector2(48, 48) + dir * len), 1.4f, new Color(230, 160, 240));
        }
        c.Fill(Path.Circle(48, 48, 17), Brush.Radial(new Vector2(42, 42), 20, 20, (0f, new Color(200, 110, 210)), (0.6f, new Color(110, 30, 120)), (1f, new Color(50, 10, 60))));
        for (int i = 0; i < 12; i++)
        {
            float a = i * 0.9f;
            c.Fill(Path.Circle(48 + MathF.Cos(a) * 9, 48 + MathF.Sin(a) * 9, 1.6f), new Color(240, 200, 250), 0.8f);
        }
        return c;
    }

    /// <summary>The four kinds of treasure on the cave floors: 0 a silver chalice, 1 sapphires, 2 gold coins, 3 an emerald idol: 64 x 64.</summary>
    public static Canvas Item(int kind)
    {
        var c = new Canvas(64, 64);
        c.Fill(Path.Ellipse(32, 56, 22, 5), new Color(0, 0, 0), 0.35f);
        switch (kind)
        {
            case 0:
            {
                var cup = new Path().MoveTo(14, 12).LineTo(50, 12).QuadTo(50, 34, 34, 38).LineTo(34, 48).LineTo(44, 54).LineTo(20, 54).LineTo(30, 48).LineTo(30, 38).QuadTo(14, 34, 14, 12).Close();
                c.Fill(cup, Brush.Linear(new Vector2(14, 0), new Vector2(50, 0), (0f, new Color(130, 136, 150)), (0.35f, new Color(250, 250, 255)), (0.6f, new Color(180, 186, 200)), (1f, new Color(100, 104, 116))));
                c.Stroke(cup, 1, new Color(60, 64, 76), 0.9f);
                c.Fill(Path.Circle(32, 24, 3.5f), new Color(220, 40, 60));
                break;
            }
            case 1:
                foreach (var (x, y, r) in new[] { (22f, 44f, 9f), (40f, 46f, 8f), (31f, 32f, 10f) })
                {
                    var gem = Path.Poly(x, y - r, x + r, y, x, y + r * 0.8f, x - r, y);
                    c.Fill(gem, Brush.Linear(new Vector2(x - r, y - r), new Vector2(x + r, y + r), (0f, new Color(180, 230, 255)), (0.5f, new Color(30, 110, 230)), (1f, new Color(10, 30, 110))));
                    c.Stroke(gem, 1, new Color(200, 240, 255), 0.7f);
                    c.Fill(Path.Poly(x - r * 0.3f, y - r * 0.6f, x, y - r * 0.8f, x + r * 0.1f, y - r * 0.3f), Color.White, 0.8f);
                }
                break;
            case 2:
                for (int i = 0; i < 7; i++)
                {
                    float x = 16 + (i % 4) * 10 + (i / 4) * 5, y = 50 - (i / 4) * 7 - (i % 2) * 2;
                    c.Fill(Path.Ellipse(x, y, 8, 4), Brush.Linear(new Vector2(x - 8, y - 4), new Vector2(x + 8, y + 4), (0f, new Color(255, 245, 170)), (0.5f, new Color(230, 180, 40)), (1f, new Color(140, 90, 10))));
                    c.Stroke(Path.Ellipse(x, y, 8, 4), 0.8f, new Color(120, 80, 10), 0.8f);
                }
                break;
            default:
            {
                var idol = new Path().Smooth(new Vector2(32, 8), new Vector2(42, 16), new Vector2(40, 30), new Vector2(46, 52), new Vector2(18, 52), new Vector2(24, 30), new Vector2(22, 16));
                c.Fill(idol, Brush.Radial(new Vector2(28, 22), 24, 34, (0f, new Color(170, 255, 190)), (0.5f, new Color(40, 170, 80)), (1f, new Color(10, 70, 30))));
                c.Stroke(idol, 1, new Color(10, 50, 20), 0.9f);
                c.Fill(Path.Circle(28, 18, 2), new Color(255, 240, 120));
                c.Fill(Path.Circle(36, 18, 2), new Color(255, 240, 120));
                break;
            }
        }
        return c;
    }
}
