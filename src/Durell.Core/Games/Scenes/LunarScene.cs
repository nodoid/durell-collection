using System;
using Durell.Games.Lunar;
using Durell.Graphics;
using Durell.Graphics.Art;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// Lunar Lander's window redrawn: the module comes down smoothly from its true height (the BASIC
/// program moves the character ship a whole row at a time, but keeps the height H to the metre), its
/// flame follows the motor setting, and it lands on a lit pad on a cratered surface under the Earth.
/// The gauge panel to the right is the original's.
/// </summary>
internal sealed partial class LunarLook
{
    private readonly Tweener _tween = new() { MaxStep = 400, MaxDuration = 1.2f };
    private float _time;
    private bool _flying, _crashed, _landed;
    private int _motors;
    private float _crashTime = -10;
    private readonly string[] _text = new string[28];

    public override Rectangle? SceneArea => new Rectangle(12, 8, 162, 192);

    protected override bool ReadScene(GameProgram program, Scene scene)
    {
        if (program is not LunarProgram p || !p.InWindow) return false;
        var m = p.Memory;
        _time = program.Frame / 50.08f;
        _flying = p.Flying;
        _motors = p.Motors;
        // where the ship's character is (the take-off and the touchdown), else the true height
        float bottom = -1;
        for (int row = 0; row < 25; row++)
            if (m[0xBB80 + (row + 1) * 40 + 15] == 91) bottom = (row + 1) * 8 + 8;
        if (_flying) bottom = 192 - 0.08f * (float)Math.Max(0, p.Height);
        if (bottom >= 0) _tween.Set(1, new Vector2(93, bottom), _time);
        if (p.Crashed && !_crashed) _crashTime = _time;
        _crashed = p.Crashed;
        _landed = !_flying && !p.Crashed && p.Height < 10 && p.Fuel != 35;
        // the score and "repeat" messages written over the window after a landing
        for (int row = 3; row <= 16; row++)
        {
            var chars = new char[27];
            bool any = false;
            for (int c = 2; c < 29; c++)
            {
                byte b = (byte)(m[0xBB80 + (row + 1) * 40 + c] & 0x7F);
                chars[c - 2] = b is >= 0x30 and < 0x5B || b is >= 0x61 and < 0x7B ? (char)b : ' ';
                any |= chars[c - 2] != ' ';
            }
            _text[row] = any && !_flying ? new string(chars).TrimEnd() : "";
        }
        return true;
    }

    public override void DrawScene(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        var dev = g.Device;
        float k = r.Width / 240f, ky = r.Height / 224f;
        Vector2 S(float x, float y) => new(r.X + x * k, r.Y + y * ky);
        var win = new RectangleF(r.X + 12 * k, r.Y + 8 * ky, 162 * k, 192 * ky);
        float now = _time + (Track.Blend - 1) / 50.08f;

        g.Gradient(win, new Color(2, 3, 12) * alpha, new Color(10, 10, 24) * alpha, 32);
        g.Additive();
        Stars(g, win, t, 60, 4321, 0, 0, r.Width / 420f, 0.5f * alpha);
        g.GlowAt(S(130, 40), 30 * k, new Color(80, 140, 255) * (0.3f * alpha));
        g.Alpha();
        g.Texture(Kit.Scenery.Planet, new RectangleF(S(118, 28).X, S(118, 28).Y, 24 * k, 24 * k), Color.White * alpha);

        // the surface, and the pad with its lights
        var surf = ArtCache.Get(dev, "lunar-surface");
        float sh = 40 * ky, sw = sh * surf.Width / surf.Height;
        for (float x = win.X - sw * 0.3f; x < win.Right; x += sw)
            g.Texture(surf, new RectangleF(x, win.Bottom - sh, sw, sh), Color.White * alpha);
        var pad = new RectangleF(S(80, 189).X, S(80, 189).Y, 26 * k, 3 * ky);
        g.Rect(pad, new Color(120, 124, 130) * alpha);
        g.Rect(new RectangleF(pad.X, pad.Y, pad.Width, 0.8f * ky), new Color(200, 204, 210) * alpha);
        g.Additive();
        foreach (float lx in new[] { 81f, 105f })
            g.GlowAt(S(lx, 189), (2 + MathF.Sin(t * 4) * 0.8f) * k, new Color(255, 80, 60) * alpha);
        g.Alpha();

        // the module
        var p = _tween.Get(1, now);
        float size = 22;
        if (_crashed)
        {
            float age = now - _crashTime;
            g.Sprite(ArtCache.Get(dev, "lunar-wreck"), S(93, 186), 22 * k, 0, Color.White * alpha);
            if (age < 1.2f)
                g.Sprite(ArtCache.Get(dev, $"harrier-blast{Math.Min(3, (int)(age / 0.3f))}"), S(93, 180 - age * 6), (28 + age * 10) * k, age, Color.White * alpha);
        }
        else
        {
            var at = S(p.X, p.Y - size * 124 / 128 / 2 * 1.0f);
            if (_flying && _motors > 0)
            {
                float len = (10 + _motors * 3.2f) * (0.9f + 0.1f * MathF.Sin(t * 40));
                var nozzle = S(p.X, p.Y - 3);
                g.Additive();
                g.Sprite(ArtCache.Get(dev, $"lunar-flame{(int)(t * 20) % 3}"), nozzle + new Vector2(0, len * k / 2 * 0.95f), len * k * 0.5f, 0, Color.White * alpha);
                g.GlowAt(nozzle + new Vector2(0, len * k * 0.4f), len * 0.5f * k, new Color(255, 170, 80) * (0.25f * alpha));
                // dust thrown up when close to the ground
                float alt = 189 - p.Y;
                if (alt < 30)
                    for (int i = 0; i < 10; i++)
                    {
                        float a = (t * 2 + i * 0.1f) % 1;
                        float dx = (i % 2 == 0 ? 1 : -1) * (6 + a * 30);
                        g.GlowAt(S(93 + dx, 188 - a * 4), (2 + a * 4) * k, new Color(200, 196, 190) * ((1 - a) * (1 - alt / 30) * 0.3f * alpha));
                    }
                g.Alpha();
            }
            g.Sprite(ArtCache.Get(dev, "lunar-module"), at, size * k, 0, Color.White * alpha);
        }

        // the messages after a landing
        for (int row = 3; row <= 16; row++)
        {
            string s = _text[row];
            if (string.IsNullOrEmpty(s) || row > 3 && _text[row - 1] == s) continue;
            var pos = S(12, (row + 1) * 8);
            g.Text(s, pos.X, pos.Y, (row >= 15 ? Color.White : new Color(255, 220, 120)) * alpha, k * 1.0f, true);
        }
        _ = _landed;
    }
}
