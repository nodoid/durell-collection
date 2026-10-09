using System;
using System.Collections.Generic;
using Durell.Graphics;
using Durell.Graphics.Art;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// Galaxy redrawn: the whole game state is in zero page (research notes: formation rows at $C0/$CD/$DA,
/// divers at $30/$7A, bombs at $86, bullet $18/$0E, player $E7/$E8, shield timer $19). Positions are in
/// Oric pixels of the 240 x 224 picture (centres).
/// </summary>
internal partial class GalaxyLook
{
    private static readonly float[] PhaseShift = { 0, 1.5f, 3, 4.5f };
    private readonly List<(Vector2 P, float Born, int Colour)> _bursts = new();
    private float _clock;

    public override (int Y0, int Y1)[] HudRows => new[] { (0, 8), (208, 224) };

    protected override bool ReadScene(GameProgram program, Scene s)
    {
        var m = program.Memory;
        // in a game: the formation rows point at their screen rows, and the top line is the score line
        if (m[0xC0] != 0xD2 || m[0xC1] != 0xBB || m[0xCD] != 0x22 || m[0xCE] != 0xBC) return false;
        if (!TextRowHas(m, 0, "SCORE")) return false;

        for (int r = 0; r < 3; r++)
            for (int i = 0; i < 5; i++)
            {
                int phase = m[0xC3 + 13 * r + 2 * i], col = m[0xC4 + 13 * r + 2 * i];
                if (phase > 3) continue;
                var o = s.Add(r * 5 + i, "alien", 6 * (2 + col) + PhaseShift[phase] + 7.5f, 16 + 16 * r + 4, phase & 1);
                o.Data = r;
            }
        for (int i = 0; i < 3; i++)
        {
            int state = m[0x31 + 4 * i];
            if (state == 0xFF || state == 0xFD) continue;
            SceneObject o;
            if (state < 0x80)
            {
                float y = 4 * (m[0x7B + 4 * i] + m[0x7A + 4 * i] / 256f);
                o = s.Add(100 + i, "alien", 3 * m[0x7D + 4 * i] + 8, y + 4, 2);
                o.FlipX = m[0x30 + 4 * i] == 0;
            }
            else
            {
                int addr = m[0x32 + 4 * i] | m[0x33 + 4 * i] << 8;
                int cell = addr - 0xBB80 + 1;
                int q = m[0x7C + 4 * i] & 3;
                o = s.Add(100 + i, "alien", 6 * (cell % 40) + PhaseShift[q] + 7.5f, 8 * (cell / 40) + 4, q & 1);
            }
            o.Data = RowOf(o.Y);
        }
        for (int i = 0; i < 3; i++)
        {
            int addr = m[0x86 + 2 * i] | m[0x87 + 2 * i] << 8;
            if (m[0x87 + 2 * i] == 0 || addr < 0xBB80) continue;
            int cell = addr - 0xBB80;
            s.Add(200 + i, "bomb", 6 * (cell % 40) + 3, 8 * (cell / 40) + 4);
        }
        if (m[0x18] == 0xFF)
        {
            int cell = (m[0x0E] | m[0x0F] << 8) + 40 - 0xBB80;
            float dx = m[0x10] == 0x50 ? 4.5f : 1.5f;
            s.Add(300, "bolt", 6 * (cell % 40) + dx, 8 * (cell / 40) + 4);
        }
        var p = s.Add(400, "fighter", 6 * m[0xE8] + 16 + 3 * m[0xE7], 183);
        p.Data = m[0x19] > 0 ? 1 : 0;
        s.Values["hit"] = m[0x93];
        return true;
    }

    private static int RowOf(float y) => y < 32 ? 0 : y < 48 ? 1 : 2;

    public override void DrawScene(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        var track = Track;
        var dev = g.Device;
        float k = r.Width / 240f;
        Vector2 At(Vector2 p) => new(r.X + p.X * k, r.Y + p.Y * k * (r.Height / r.Width * 240f / 224f));
        float dt = t - _clock;
        _clock = t;

        DrawBackdrop(g, r, t, index, alpha);

        // explosions where an alien vanished this frame
        if (track.Previous.Active)
            foreach (var o in track.Previous.Objects)
                if (o.Kind == "alien" && track.Current.Find(o.Id, o.Kind) == null && track.Current.Value("hit") >= 3)
                    _bursts.Add((new Vector2(o.X, o.Y), t, o.Data));

        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        string[] names = { "red", "blue", "green" };
        foreach (var o in track.Current.Objects)
        {
            var pos = At(track.Position(o));
            switch (o.Kind)
            {
                case "alien":
                {
                    // wings beat on their own clock as well as with the game's phase
                    int pose = o.Frame == 2 ? 2 : ((int)(t * 6 + o.Id) & 1) ^ o.Frame;
                    var tex = ArtCache.Get(dev, $"galaxy-alien-{names[Math.Clamp(o.Data, 0, 2)]}{pose}");
                    float wobble = o.Frame == 2 ? 0 : MathF.Sin(t * 2.2f + o.Id) * 0.05f;
                    g.Sprite(tex, pos, 19 * k, wobble + (o.Frame == 2 ? (o.FlipX ? -0.3f : 0.3f) : 0), Color.White * alpha);
                    break;
                }
                case "bomb":
                    g.Additive();
                    g.Sprite(ArtCache.Get(dev, "galaxy-bomb"), pos, 7 * k, t * 4, Color.White * alpha);
                    g.Alpha();
                    break;
                case "bolt":
                    g.Additive();
                    g.Sprite(ArtCache.Get(dev, "galaxy-bolt"), pos, 2.6f * k, 0, Color.White * alpha);
                    g.Alpha();
                    break;
                case "fighter":
                    g.Sprite(ArtCache.Get(dev, "galaxy-fighter"), pos, 14 * k, 0, Color.White * alpha);
                    if (o.Data == 1)
                    {
                        g.Additive();
                        float pulse = 0.75f + 0.25f * MathF.Sin(t * 12);
                        g.Sprite(ArtCache.Get(dev, "galaxy-shield"), pos, 24 * k, t, Color.White * (pulse * alpha));
                        g.Alpha();
                    }
                    break;
            }
        }
        g.Additive();
        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            var (p, born, _) = _bursts[i];
            float age = t - born;
            if (age > 0.5f || age < 0)
            {
                _bursts.RemoveAt(i);
                continue;
            }
            int f = Math.Min(4, (int)(age / 0.1f));
            g.Sprite(ArtCache.Get(dev, $"galaxy-burst{f}"), At(p), 26 * k, 0, Color.White * alpha);
        }
        g.Alpha();
    }
}
