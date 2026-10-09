using System;
using System.Collections.Generic;
using Durell.Graphics;
using Durell.Graphics.Art;
using Durell.Machine;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// Harrier Attack redrawn with a continuously moving camera.
/// <para>The game is a text-mode game that scrolls its world left one character per "step" (5-19
/// frames) and makes the landscape up one column at a time at the right edge - there is no stored map.
/// A probe at $22F3 fires the instant each scroll ends: the screen then holds only the world (the plane
/// and enemies are erased around the scroll), so it is copied as that step's world snapshot and timed
/// in CPU cycles. Each world column is painted once, at high resolution, from the game's own character
/// shapes (smoothed, then given earth, grass, concrete, roof-tile and glass materials); the carriers
/// and the enemy ship get whole illustrations. The camera runs one step behind the game and slides
/// between steps, so new land glides in from the right edge instead of popping.</para>
/// </summary>
internal partial class HarrierLook
{
    private const int Rows = 25;                 // text rows 0-24 (1-23 playfield, 24 sea band)
    private const int ScrollProbe = 0x22F3;

    /// <summary>One game step: the world as it was when the scroll ended, and the object slots then.</summary>
    private sealed class Step
    {
        public readonly byte[] Chars = new byte[40 * Rows];
        public long Count;
        public long Cycle;
        public long Period = 17 * OricMachine.CyclesPerFrame;
        public readonly byte[] Slots = new byte[SlotAddrs.Length];
    }

    private static readonly int[] SlotAddrs =
    {
        0x2111, 0x2112,                 // plane row*2, nose column
        0x2171, 0x2175, 0x2176,         // enemy jet
        0x2173, 0x2177, 0x2178,         // jet missile
        0x2182, 0x2183, 0x2184,         // ship SAM
        0x2124, 0x2125, 0x2126,         // bomb
    };

    private OricProgram? _program;
    private readonly Step[] _steps = { new(), new(), new() };
    private int _stepHead = -1;
    private long _stepCount;
    private readonly HarrierWorld _world = new();
    private readonly byte[] _slotsNow = new byte[SlotAddrs.Length];
    private readonly byte[] _live = new byte[40 * 28];
    private int _phase;
    private long _frameCycle;
    /// <summary>Explosions; world ones (P.X in world pixels) scroll with the land.</summary>
    private readonly List<(Vector2 P, float Born, float Size, bool World)> _blasts = new();
    private readonly HashSet<long> _debrisSeen = new();
    private bool _crashShown;

    public override (int Y0, int Y1)[] HudRows => new[] { (0, 8), (200, 224) };
    internal HarrierWorld World => _world;

    public override void Attach(GameProgram program)
    {
        if (program is not OricProgram op) return;
        _program = op;
        var prev = op.Probe;
        op.Probe = (p, a) =>
        {
            prev?.Invoke(p, a);
            if (a == ScrollProbe) OnScroll();
        };
    }

    private void OnScroll()
    {
        var m = _program!.Memory;
        var prev = _stepHead >= 0 ? _steps[_stepHead] : null;
        _stepHead = (_stepHead + 1) % _steps.Length;
        var s = _steps[_stepHead];
        Buffer.BlockCopy(m, 0xBB80, s.Chars, 0, s.Chars.Length);
        s.Count = ++_stepCount;
        s.Cycle = _program.Machine.Cpu.Cy;
        if (prev != null) s.Period = Math.Clamp(s.Cycle - prev.Cycle, 3 * OricMachine.CyclesPerFrame, 24 * OricMachine.CyclesPerFrame);
        for (int i = 0; i < SlotAddrs.Length; i++) s.Slots[i] = m[SlotAddrs[i]];
        _world.Take(s.Chars, s.Count, m, m[0x216F]);
    }

    /// <summary>Messages the game writes over the playfield ("HARRIER DESTROYED", the landing): row, text.</summary>
    protected readonly List<(int Row, string Text)> Messages = new();
    /// <summary>The plane was lost: from the crash until the game leaves the sortie (its menu or a new take-off).</summary>
    protected bool Crashed { get; private set; }
    /// <summary>Where it went down (screen pixels) and when (game seconds).</summary>
    protected Vector2 CrashAt { get; private set; }
    protected float CrashTime { get; private set; }

    private void ReadMessages(byte[] live, byte[] m, long frame)
    {
        Messages.Clear();
        for (int row = 1; row <= 23; row++)
        {
            var sb = new System.Text.StringBuilder();
            int letters = 0;
            for (int c = 2; c < 40; c++)
            {
                byte ch = (byte)(live[row * 40 + c] & 0x7F);
                bool letter = ch is >= 0x41 and <= 0x5A;
                if (letter) letters++;
                sb.Append(letter ? (char)ch : ch == 0x20 && sb.Length > 0 && sb[^1] != ' ' ? ' ' : (sb.Length > 0 && sb[^1] != ' ' ? ' ' : '\0'));
            }
            string s = sb.ToString().Replace("\0", "").Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            // double-height text fills two rows with the same message
            bool repeat = Messages.Count > 0 && Messages[^1].Row == row - 1 && Messages[^1].Text == s;
            if (letters >= 5 && !repeat) Messages.Add((row, s));
        }
        // the crash: the wreck's characters at the plane, or the game's own message
        int pr = m[0x2111] / 2, pc = m[0x2112];
        byte at = pr is > 0 and < 28 && pc is > 1 and < 40 ? live[pr * 40 + pc] : (byte)0;
        bool wreck = at is 0x5E or 0x60 or 0x61 or 0x63;
        bool destroyed = Messages.Exists(x => x.Text.Contains("DESTROYED"));
        if (!Crashed && (wreck || destroyed))
        {
            Crashed = true;
            CrashAt = new Vector2(6 * pc, 8 * pr + 4);
            CrashTime = frame / 50.08f;
        }
        if (_phase is 0 or 7 && !destroyed) Crashed = false;
    }

    protected override bool ReadScene(GameProgram program, Scene scene)
    {
        var m = program.Memory;
        _phase = m[0x216F];
        _frameCycle = program.Frame * OricMachine.CyclesPerFrame;
        Buffer.BlockCopy(m, 0xBB80, _live, 0, _live.Length);
        for (int i = 0; i < SlotAddrs.Length; i++) _slotsNow[i] = m[SlotAddrs[i]];
        ReadMessages(_live, m, program.Frame);
        // the game's screens (title, menu, scores) are shown as they are; play is redrawn
        if (_phase == 7 || !TextRowHas(m, 0, "SCORE")) return false;
        if (_phase == 0)
        {
            // the intro (the carrier pushed in from the left) has no scroll probe: follow the live screen
            _world.Take(_live, _stepCount, m, _phase);
        }
        return _stepHead >= 0 || _phase == 0;
    }

    // ------------------------------------------------------------------ drawing

    public override void DrawScene(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        var dev = g.Device;
        _world.Upload(dev);
        float kx = r.Width / 240f, ky = r.Height / 224f;

        var (camera, f, slotsFrom, slotsTo) = Timing();
        Vector2 Px(float ox, float oy) => new(r.X + ox * kx, r.Y + oy * ky);

        DrawSky(g, r, t, camera, alpha);

        // the world, column by column
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        foreach (var col in _world.Columns)
        {
            float x = 6 * (col.Abs - camera);
            if (x < -12 || x > 246) continue;
            if (col.Texture == null) continue;
            var dest = new RectangleF(r.X + x * kx, r.Y + col.Top * 8 * ky, 6 * kx, (col.Bottom - col.Top) * 8 * ky);
            g.Texture(col.Texture, dest, Color.White * alpha);
        }
        DrawSetPieces(g, r, camera, kx, ky, alpha);
        DrawSmoke(g, r, t, camera, kx, ky, alpha);

        // moving things, tweened from the last step's slots to this one's
        float L(int i) => slotsFrom[i] + (slotsTo[i] - slotsFrom[i]) * f;
        bool Live(int i) => !Crashed && (slotsTo[i] == 1 || slotsFrom[i] == 1 && f < 1);
        if (Live(11)) Sprite(g, "harrier-bomb", Px(6 * (L(13) + 1) + 3, 4 * L(12) + 4), 7 * kx, 1.2f, false, alpha);
        if (Live(2)) Sprite(g, "harrier-mig", Px(6 * (L(4) + 2), 4 * L(3) + 4), 21 * kx, 0, false, alpha);
        if (Live(5)) Sprite(g, "harrier-rocket", Px(6 * (L(7) + 1) + 3, 4 * L(6) + 4), 12 * kx, 0, true, alpha);
        if (Live(8)) Sprite(g, "harrier-rocket", Px(6 * (L(10) + 1) + 3, 4 * L(9) + 4), 12 * kx, 0, true, alpha);

        // the Harrier (or its wreck)
        float row = L(0) / 2, nose = L(1);
        var planeAt = Px(6 * nose - 1, 8 * row + 4);
        int liveRow = slotsTo[0] / 2, liveCol = slotsTo[1];
        byte at = liveRow is > 0 and < 28 && liveCol is > 1 and < 40 ? _live[liveRow * 40 + liveCol] : (byte)0;
        bool crashed = Crashed || at is 0x60 or 0x63 || at == 0x61 || at == 0x5E;
        if (crashed)
        {
            if (!_crashShown) _blasts.Add((new Vector2(6 * nose, 8 * row), t, 30, false));
            _crashShown = true;
            // the wreck keeps burning while the game shows its message
            var w = Px(CrashAt.X, MathF.Min(184, CrashAt.Y + 30));
            g.Additive();
            g.GlowAt(w, (5 + MathF.Sin(t * 9) * 1.5f) * kx, new Color(255, 140, 40) * (0.7f * alpha));
            g.Alpha();
            for (int i = 0; i < 6; i++)
            {
                float age = (t * 0.5f + i / 6f) % 1;
                g.GlowAt(w + new Vector2(age * 8 * kx, -age * 50 * ky), (4 + age * 14) * kx, new Color(50, 50, 54) * (0.6f * (1 - age) * alpha));
            }
        }
        else
        {
            _crashShown = false;
            float bank = (slotsTo[0] - slotsFrom[0]) * -0.04f * (1 - f);
            Sprite(g, "harrier-jet", planeAt, 24 * kx, bank, false, alpha);
            // rockets: the game draws an instant streak of '-' to the right of the nose
            int streak = 0;
            for (int c = liveCol + 1; c < 40 && c <= liveCol + 8; c++)
                if (_live[liveRow * 40 + c] == 0x2D) streak = c;
            if (streak > 0)
            {
                var a = Px(6 * (liveCol + 1), 8 * liveRow + 4);
                var b = Px(6 * (streak + 1), 8 * liveRow + 4);
                g.Line(a, b, 1.2f * ky, new Color(255, 250, 230) * (0.5f * alpha));
                Sprite(g, "harrier-rocket", b, 12 * kx, 0, false, alpha);
            }
        }

        // explosions
        g.Additive();
        for (int i = _blasts.Count - 1; i >= 0; i--)
        {
            var (p, born, size, world) = _blasts[i];
            if (world) p.X -= 6 * camera;
            float age = t - born;
            if (age < 0 || age > 0.9f)
            {
                _blasts.RemoveAt(i);
                continue;
            }
            int frame = Math.Min(3, (int)(age / 0.22f));
            g.Alpha();
            g.Sprite(ArtCache.Get(dev, $"harrier-blast{frame}"), Px(p.X, p.Y), size * kx, 0, Color.White * alpha);
            g.Additive();
        }
        g.Alpha();
        DrawMessages(g, r, t, alpha);
    }

    /// <summary>
    /// Where the display is between game steps: <c>camera</c> is the world column at the playfield's left
    /// edge (absolute column a is drawn at screen column a - camera), <c>f</c> the fraction of the step,
    /// and the object slots to blend from and to. The display runs one step behind the game.
    /// </summary>
    protected (float Camera, float F, byte[] From, byte[] To) Timing()
    {
        // which step, and how far towards the next (the display runs one step behind the game)
        Step? cur = _stepHead >= 0 ? _steps[_stepHead] : null;
        Step? old = _stepHead >= 0 ? _steps[(_stepHead + _steps.Length - 1) % _steps.Length] : null;
        long renderCycle = _frameCycle - OricMachine.CyclesPerFrame + (long)(Track.Blend * OricMachine.CyclesPerFrame);
        float f = 1;
        long count = _stepCount;
        byte[] slotsFrom = _slotsNow, slotsTo = _slotsNow;
        if (cur != null && _phase != 0)
        {
            if (renderCycle < cur.Cycle && old != null && old.Count == cur.Count - 1)
            {
                // still showing the step before the newest scroll
                f = Math.Clamp((renderCycle - old.Cycle) / (float)cur.Period, 0, 1);
                count = old.Count;
                slotsFrom = old.Slots;
                slotsTo = cur.Slots;
            }
            else
            {
                f = Math.Clamp((renderCycle - cur.Cycle) / (float)cur.Period, 0, 1);
                count = cur.Count;
                slotsFrom = cur.Slots;
                slotsTo = _slotsNow;
            }
        }
        return (count - 1 + f, f, slotsFrom, slotsTo);
    }

    /// <summary>The object slots: 0/1 plane row*2 and nose column; 2-4 enemy jet (active, row*2, column); 5-7 its missile; 8-10 the ship's SAM; 11-13 the bomb.</summary>
    protected static float Blend(byte[] from, byte[] to, int i, float f) => from[i] + (to[i] - from[i]) * f;

    /// <summary>The live text screen (40 x 28) as of the last frame.</summary>
    protected byte[] LiveScreen => _live;
    protected int Phase => _phase;

    /// <summary>The game's messages, as banners at their rows.</summary>
    protected void DrawMessages(Gfx g, RectangleF r, float t, float alpha)
    {
        float ky = r.Height / 224f, kx = r.Width / 240f;
        foreach (var (row, text) in Messages)
        {
            float y = r.Y + (row * 8 - 4) * ky, scale = MathF.Max(1.2f, r.Width / 330f);
            float w = Gfx.TextWidth(text, scale) + 24;
            var band = new RectangleF(r.Center.X - w / 2, y, w, 8 * scale + 10);
            g.RoundRect(band, 8, Color.Black * (0.55f * alpha));
            float pulse = 0.85f + 0.15f * MathF.Sin(t * 4);
            g.GlowText(text, r.Center.X, y + 5, new Color(255, 220, 120) * (pulse * alpha), scale, 0.5f);
        }
    }

    private static void Sprite(Gfx g, string name, Vector2 at, float width, float rot, bool flip, float alpha) =>
        g.Sprite(ArtCache.Get(g.Device, name), at, width, rot, Color.White * alpha, flip);

    private void DrawSky(Gfx g, RectangleF r, float t, float camera, float alpha)
    {
        var dev = g.Device;
        float ky = r.Height / 224f, kx = r.Width / 240f;
        var sky = new RectangleF(r.X, r.Y, r.Width, 192 * ky);
        g.Gradient(sky, new Color(34, 84, 160) * alpha, new Color(176, 210, 236) * alpha, 160);
        g.Additive();
        g.GlowAt(new Vector2(r.X + r.Width * 0.82f, r.Y + 30 * ky), r.Width * 0.25f, new Color(255, 236, 190) * (0.35f * alpha));
        g.Alpha();
        // two cloud layers, drifting with the camera at different depths (parallax)
        var clouds = ArtCache.Get(dev, "harrier-clouds");
        for (int layer = 0; layer < 2; layer++)
        {
            float depth = layer == 0 ? 0.15f : 0.4f;
            float w = r.Width * (layer == 0 ? 1.6f : 2.2f), h = w / 4;
            float off = (camera * 6 * kx * depth + t * (2 + layer * 3) * kx) % w;
            float y = r.Y + (layer == 0 ? 6 : 34) * ky;
            for (float x = -off; x < r.Width; x += w)
                g.Texture(clouds, new RectangleF(r.X + x, y, w + 1, h), Color.White * (alpha * (layer == 0 ? 0.55f : 0.8f)));
        }
        // the sea (row 24 and wherever there is no land), with waves moving with the world
        var sea = ArtCache.Get(dev, "harrier-sea");
        float top = 186 * ky, band = 14 * ky;
        float tw = band * sea.Width / sea.Height;
        float so = (camera * 6 * kx) % tw;
        for (float x = -so; x < r.Width; x += tw)
            g.Texture(sea, new RectangleF(r.X + x, r.Y + top, tw + 1, band), Color.White * alpha);
        g.Gradient(new RectangleF(r.X, r.Y + top - 1.5f * ky, r.Width, 3 * ky), new Color(220, 236, 250) * (0.0f), new Color(220, 236, 250) * (0.5f * alpha), 4);
    }

    private void DrawSetPieces(Gfx g, RectangleF r, float camera, float kx, float ky, float alpha)
    {
        foreach (var (kind, first, last, row) in _world.Pieces)
        {
            float x0 = 6 * (first - camera), x1 = 6 * (last + 1 - camera);
            if (x1 < -40 || x0 > 280) continue;
            switch (kind)
            {
                case "carrier":
                {
                    // the art's deck runs from 8 to 560 of 640 px, at y 94 of 160
                    var tex = ArtCache.Get(g.Device, "harrier-carrier");
                    float deckW = x1 - x0, scale = deckW / 552f;
                    float w = 640 * scale, h = 160 * scale;
                    float left = x0 - 8 * scale, top = 8 * 23 - 94 * scale;
                    g.Texture(tex, new RectangleF(r.X + left * kx, r.Y + top * ky, w * kx, h * ky), Color.White * alpha);
                    // two Harriers parked on deck
                    foreach (float u in new[] { 0.12f, 0.3f })
                        g.Sprite(ArtCache.Get(g.Device, "harrier-jet"), new Vector2(r.X + (x0 + deckW * u) * kx, r.Y + (8 * 23 - 4) * ky), 18 * kx, 0, Color.White * alpha);
                    break;
                }
                case "frigate":
                {
                    var tex = ArtCache.Get(g.Device, "harrier-frigate");
                    float w = 30;
                    g.Texture(tex, new RectangleF(r.X + (x0 - 2) * kx, r.Y + (8 * row + 8 - w * 94 / 320f) * ky, w * kx, w * 112 / 320f * ky), Color.White * alpha);
                    break;
                }
            }
        }
    }

    private void DrawSmoke(Gfx g, RectangleF r, float t, float camera, float kx, float ky, float alpha)
    {
        // burning wrecks: smoke rising from debris cells, and a fireball when one first appears
        foreach (var (abs, row) in _world.Debris)
        {
            float x = 6 * (abs - camera) + 3, y = 8 * row + 4;
            if (x < -10 || x > 250) continue;
            long key = abs * 64 + row;
            if (_debrisSeen.Add(key)) _blasts.Add((new Vector2(abs * 6 + 3, y), t, 22, true));
            for (int i = 0; i < 4; i++)
            {
                float age = (t * 0.6f + i * 0.25f + (abs % 7) * 0.13f) % 1f;
                var p = new Vector2(r.X + (x + MathF.Sin(t + i) * 2 + age * 4) * kx, r.Y + (y - age * 22) * ky);
                g.GlowAt(p, (3 + age * 9) * kx, new Color(60, 60, 64) * (0.55f * (1 - age) * alpha));
            }
        }
    }
}
