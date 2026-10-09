using System;
using System.Collections.Generic;
using Durell.Graphics;
using Durell.Graphics.Art;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// Star Fighter redrawn. The game is text mode: a top-down sector map whose objects all step one
/// character cell when you steer (the whole 256 x 256-cell sector is in RAM, so the view can show
/// space beyond the original's clip box), and a cockpit view for combat. Every object glides between
/// its steps, and the starfield behind follows the accumulated steering with parallax.
/// </summary>
internal sealed partial class StarFighterLook
{
    private readonly Tweener _tween = new() { MaxStep = 20 };
    private float _time;
    private bool _combat;
    private readonly List<(int Id, string Art, float Size, float Spin)> _things = new();
    private int _facing = 0x21;
    private float _heading, _headingShown;
    private Vector2 _steer;                 // accumulated steering, in cells
    private int _lastAstCol = -1, _lastAstRow = -1;
    private bool _shot;
    private float _shotAngle;
    private readonly List<Point> _shots = new();
    private bool _phasor, _boom, _hit;
    private float _boomStart = -10;

    public override (int Y0, int Y1)[] HudRows => new[] { (0, 8), (176, 224) };

    private static int Rel(int v, int centre) => ((v - centre + 128) & 255) - 128;

    protected override bool ReadScene(GameProgram program, Scene scene)
    {
        var m = program.Memory;
        if (!TextRowHas(m, 0, "Score") || !TextRowHas(m, 23, "LRS")) return false;
        _time = program.Frame / 50.08f;
        _combat = m[0xBBD4] != 0x17;
        _things.Clear();
        _shots.Clear();
        if (!_combat)
        {
            // the steering: every world object steps the opposite way; the first asteroid shows it
            int ac = m[0x0416], ar = m[0x0417];
            if (_lastAstCol >= 0)
            {
                int dc = Rel(ac, _lastAstCol), dr = Rel(ar, _lastAstRow);
                if (Math.Abs(dc) <= 1 && Math.Abs(dr) <= 1) _steer -= new Vector2(dc, dr);
            }
            _lastAstCol = ac;
            _lastAstRow = ar;
            _tween.Set(9999, _steer * new Vector2(6, 8), _time);

            void Add(int id, int col, int row, string art, float size, float spin = 0)
            {
                int rc = Rel(col, 20), rr = Rel(row, 11);
                if (Math.Abs(rc) > 24 || Math.Abs(rr) > 14) return;
                _tween.Set(id, new Vector2(120 + rc * 6, 92 + rr * 8), _time);
                _things.Add((id, art, size, spin));
            }
            for (int i = 0; i < 15; i++) Add(100 + i, m[0x0416 + 2 * i], m[0x0417 + 2 * i], $"sf-asteroid{i % 3}", 9 + i % 3 * 2, 0.3f + i % 4 * 0.2f);
            for (int i = 0; i < 5; i++) Add(200 + i, m[0x0436 + 2 * i], m[0x0437 + 2 * i], "sf-gate", 14, 0.5f);
            if (m[0x8678] == 0xEA) Add(300, m[0x0448], m[0x0449], "sf-base", 16, 0.15f);
            for (int i = 0; i < 30; i++)
                if (m[0x044B + 2 * i] != 0) Add(400 + i, m[0x044A + 2 * i], m[0x044B + 2 * i], "sf-mine", 6, 1.5f);
            int n = (sbyte)m[0x75];
            for (int x = n; x >= 1; x -= 2) Add(500 + x, m[0x0442 + x - 1], m[0x0442 + x], "sf-enemy", 9);
            _shot = m[0x78] != 0;
            if (_shot)
            {
                int p = m[0x7B] | m[0x7C] << 8;
                _shotAngle = p switch { 0x8442 => 0, 0x8477 => MathF.PI, 0x84AC => MathF.PI / 2, _ => -MathF.PI / 2 };
                _tween.Set(600, new Vector2(m[0x86] * 6 + 3, m[0x87] * 8 + 4), _time);
            }
            if (m[0x73] is 0x21 or 0x22 or 0x27 or 0x28) _facing = m[0x73];
            _heading = _facing switch { 0x21 => 0, 0x27 => MathF.PI, 0x28 => MathF.PI / 2, _ => -MathF.PI / 2 };
        }
        else
        {
            for (int i = 0; i < 20; i++)
                _tween.Set(700 + i, new Vector2(m[0x04B2 + 2 * i] * 6 + 3, m[0x04B3 + 2 * i] * 8 + 4), _time);
            _tween.Set(800, new Vector2(m[0x04DA] * 6 + 6, m[0x04DB] * 8 + 4), _time);
            // shots, phasors, explosions and hits exist only as characters on the screen
            _phasor = false;
            bool boom = false;
            _hit = false;
            for (int row = 2; row <= 21; row++)
                for (int col = 2; col <= 38; col++)
                {
                    byte ch = m[0xBB80 + row * 40 + col];
                    if (ch is >= 0x23 and <= 0x26 or >= 0x2A and <= 0x2D) _phasor = true;
                    else if (ch is 0x5D or 0x5E or 0x5F) _shots.Add(new Point(col, row));
                    else if (ch is 0x3A or 0x3E or 0x78) boom = true;
                    else if (ch == 0x0A) _hit = true;
                }
            if (boom && !_boom) _boomStart = _time;
            _boom = boom;
        }
        _tween.Prune(_time - 1);
        return true;
    }

    public override void DrawScene(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        var dev = g.Device;
        float now = _time + (Track.Blend - 1) / 50.08f;
        var view = new RectangleF(r.X, r.Y + 8 * r.Height / 224f, r.Width, 168 * r.Height / 224f);
        float k = r.Width / 240f, ky = r.Height / 224f;
        Vector2 S(Vector2 p) => new(r.X + p.X * k, r.Y + p.Y * ky);

        g.PushClip(view);
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        g.Gradient(view, new Color(4, 6, 24) * alpha, new Color(1, 1, 8) * alpha, 32);
        var cam = _combat ? Vector2.Zero : _tween.Get(9999, now);
        // nebula and three star layers, drifting with the steering at different depths
        g.Additive();
        var neb = Kit.Scenery.Nebula;
        float nw = view.Width * 1.2f;
        var no = new Vector2(-cam.X * 0.15f * k % nw, -cam.Y * 0.15f * ky % nw);
        for (float x = no.X - nw; x < view.Width; x += nw)
            for (float y = no.Y - nw; y < view.Height; y += nw)
                g.Texture(neb, new RectangleF(view.X + x, view.Y + y, nw, nw), new Color(150, 110, 255) * (0.35f * alpha));
        for (int layer = 0; layer < 3; layer++)
        {
            float depth = 0.25f + layer * 0.35f;
            for (int i = 0; i < 50; i++)
            {
                float sx = (Hash(i * 7 + layer * 101) * 300 - cam.X * depth) % 300;
                float sy = (Hash(i * 7 + layer * 101 + 1) * 220 - cam.Y * depth) % 220;
                if (sx < 0) sx += 300;
                if (sy < 0) sy += 220;
                float tw = 0.6f + 0.4f * MathF.Sin(t * (1 + Hash(i)) * 2 + i);
                g.Rect(view.X + sx * k, view.Y + sy * ky, (0.5f + layer * 0.35f) * k, (0.5f + layer * 0.35f) * k, new Color(200, 220, 255) * (tw * (0.4f + layer * 0.25f) * alpha));
            }
        }
        g.Alpha();

        if (!_combat) DrawMap(g, dev, S, k, now, t, alpha);
        else DrawCockpit(g, dev, view, S, k, now, t, alpha);
        g.PopClip();
    }

    private void DrawMap(Gfx g, GraphicsDevice dev, Func<Vector2, Vector2> S, float k, float now, float t, float alpha)
    {
        foreach (var (id, art, size, spin) in _things)
        {
            var p = _tween.Get(id, now);
            float rot = art == "sf-enemy" ? 0 : t * spin + id;
            if (art == "sf-gate" || art == "sf-base")
            {
                g.Additive();
                g.GlowAt(S(p), size * 0.9f * k, new Color(120, 160, 255) * (0.25f * alpha));
                g.Alpha();
            }
            g.Sprite(ArtCache.Get(dev, art), S(p), size * k, rot, Color.White * alpha);
        }
        if (_shot)
        {
            g.Additive();
            g.Sprite(ArtCache.Get(dev, "galaxy-bolt"), S(_tween.Get(600, now)), 2.6f * k, _shotAngle, Color.White * alpha);
            g.Alpha();
        }
        // the player's ship at the centre, turning smoothly to its new heading
        float d = MathHelper.WrapAngle(_heading - _headingShown);
        _headingShown += d * 0.25f;
        var c = S(new Vector2(123, 92));
        g.Additive();
        g.GlowAt(c + new Vector2(-MathF.Sin(_headingShown), MathF.Cos(_headingShown)) * 6 * k, 5 * k, new Color(90, 150, 255) * (0.5f * alpha));
        g.Alpha();
        g.Sprite(ArtCache.Get(dev, "sf-player"), c, 11 * k, _headingShown, Color.White * alpha);
    }

    private void DrawCockpit(Gfx g, GraphicsDevice dev, RectangleF view, Func<Vector2, Vector2> S, float k, float now, float t, float alpha)
    {
        // stars streak in the direction they move
        for (int i = 0; i < 20; i++)
        {
            var p = _tween.Get(700 + i, now);
            var p0 = _tween.Get(700 + i, now - 0.08f);
            g.Line(S(p0), S(p), 0.8f * k, new Color(220, 230, 255) * (0.8f * alpha));
        }
        var e = _tween.Get(800, now);
        float bob = MathF.Sin(t * 2.5f) * 1.2f;
        bool exploding = _boom && _time - _boomStart < 1.2f;
        if (!exploding) g.Sprite(ArtCache.Get(dev, "sf-cockpit-enemy"), S(e + new Vector2(0, bob)), 26 * k, MathF.Sin(t * 1.3f) * 0.08f, Color.White * alpha);
        foreach (var s in _shots)
        {
            g.Additive();
            g.Sprite(ArtCache.Get(dev, "sf-plasma"), S(new Vector2(s.X * 6 + 3, s.Y * 8 + 4)), 7 * k, t * 5, Color.White * alpha);
            g.Alpha();
        }
        if (_phasor)
        {
            // phasor beams converge from the bottom corners on the sights
            var aim = S(new Vector2(120, 92));
            g.Additive();
            foreach (var from in new[] { S(new Vector2(12, 172)), S(new Vector2(228, 172)) })
            {
                g.Line(from, aim, 3 * k, new Color(255, 80, 60) * (0.6f * alpha));
                g.Line(from, aim, 1 * k, new Color(255, 230, 200) * alpha);
            }
            g.GlowAt(aim, 8 * k, new Color(255, 120, 80) * (0.6f * alpha));
            g.Alpha();
        }
        if (exploding)
        {
            float age = _time - _boomStart;
            int f = Math.Min(4, (int)(age / 0.18f));
            g.Additive();
            g.Sprite(ArtCache.Get(dev, $"galaxy-burst{f}"), S(e), 50 * k, 0, Color.White * alpha);
            g.Alpha();
            g.Sprite(ArtCache.Get(dev, $"harrier-blast{Math.Min(3, (int)(age / 0.3f))}"), S(e), 40 * k, age, Color.White * alpha);
        }
        // the sights
        var sc = S(new Vector2(120, 92));
        var box = new RectangleF(sc.X - 21 * k, sc.Y - 28 * k, 42 * k, 56 * k);
        var hud = new Color(120, 255, 180) * (0.5f * alpha);
        float l = 8 * k;
        g.Line(new Vector2(box.X, box.Y), new Vector2(box.X + l, box.Y), 0.8f * k, hud);
        g.Line(new Vector2(box.X, box.Y), new Vector2(box.X, box.Y + l), 0.8f * k, hud);
        g.Line(new Vector2(box.Right, box.Y), new Vector2(box.Right - l, box.Y), 0.8f * k, hud);
        g.Line(new Vector2(box.Right, box.Y), new Vector2(box.Right, box.Y + l), 0.8f * k, hud);
        g.Line(new Vector2(box.X, box.Bottom), new Vector2(box.X + l, box.Bottom), 0.8f * k, hud);
        g.Line(new Vector2(box.X, box.Bottom), new Vector2(box.X, box.Bottom - l), 0.8f * k, hud);
        g.Line(new Vector2(box.Right, box.Bottom), new Vector2(box.Right - l, box.Bottom), 0.8f * k, hud);
        g.Line(new Vector2(box.Right, box.Bottom), new Vector2(box.Right, box.Bottom - l), 0.8f * k, hud);
        g.Line(sc - new Vector2(4 * k, 0), sc + new Vector2(4 * k, 0), 0.6f * k, hud);
        g.Line(sc - new Vector2(0, 4 * k), sc + new Vector2(0, 4 * k), 0.6f * k, hud);
        if (_hit) g.Rect(view, new Color(255, 30, 20) * ((0.25f + 0.15f * MathF.Sin(t * 30)) * alpha));
    }
}
