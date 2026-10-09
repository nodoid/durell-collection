using System;
using System.Collections.Generic;
using Durell.Graphics;
using Durell.Graphics.Art;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// Scuba Dive redrawn as one continuous underwater world. The original shows the sea as one screen
/// and the caverns as a window that flips a whole maze cell at a time; here the sea, the shaft and the
/// whole cavern maze are one map (<see cref="ScubaWorld"/>) and the camera follows the diver smoothly
/// in every direction. Everything moves in whole character steps in the game, at its own rate, so
/// each thing glides between its steps (<see cref="Tweener"/>).
/// </summary>
internal sealed partial class ScubaLook
{
    private readonly ScubaWorld _world = new();
    private readonly Tweener _tween = new() { MaxStep = 40 };
    private float _time;
    private bool _caves;
    private Vector2 _cam;
    private bool _camSet;
    private float _lastDraw;
    private readonly List<(int Id, string Kind, int Frame, bool Left, float W)> _creatures = new();
    private readonly (Vector2 Cell, int State)[] _oysters = new (Vector2, int)[9];
    private int _diverFacing = 2;
    private bool _diverAlive = true;
    /// <summary>Aboard: the game is waiting for RIGHT to launch ($146D) or DOWN to dive ($146C); his position is stale then.</summary>
    private bool _onBoat;
    private bool _urchinOn;
    private int _tentacle;

    public override (int Y0, int Y1)[] HudRows => new[] { (0, 24), (208, 224) };

    /// <summary>For tests: where the diver is drawn (world pixels) at game time <paramref name="t"/>, and whether he is aboard.</summary>
    internal (Vector2 Pos, bool OnBoat) DiverAt(float t) => (_onBoat ? _tween.Get(2, t) : _tween.Get(1, t), _onBoat);

    // world pixel position of a screen cell, in the sea or in the cave window
    private Vector2 SeaCell(float col, float row) => new((57 + col) * 6, (row - 4) * 8);

    private Vector2 CaveCell(byte[] m, float col, float row) =>
        new((ScubaWorld.CellW * m[0x1448] + col - 3) * 6, (ScubaWorld.SeaRows + ScubaWorld.CellH * m[0x1449] + row - 5) * 8);

    protected override bool ReadScene(GameProgram program, Scene scene)
    {
        var m = program.Memory;
        // in play: the AIR gauge (chars $C6/$C7, drawn with the HUD's own charset) fills text row 2
        int gauge = 0;
        for (int c = 10; c < 34; c++) if ((m[0xBBD0 + c] & 0xFE) == 0xC6) gauge++;
        if (gauge < 12) return false;
        _time = program.Frame / 50.08f;
        _caves = m[0x142F] == 1;
        _world.Update(m, _caves);

        // the diver (only while he is in the water: aboard, his position is left over from the last dive)
        _onBoat = !_caves && (m[0x146D] == 1 || m[0x146C] == 1);
        int row = m[0x140E], col = m[0x140F];
        var diver = _caves ? CaveCell(m, col, row) : SeaCell(col, row);
        if (!_onBoat) _tween.Set(1, diver + new Vector2(3, 4), _time);
        int facing = m[0x1418];
        if (facing is >= 2 and <= 5) _diverFacing = facing;
        _diverAlive = m[0x1416] != 2;

        // the boat (moves by single pixels; wraps around)
        float boatX = 6 * m[0x1467] + 5 - m[0x1468];
        if (m[0x1467] >= 0xF0) boatX -= 256 * 6;
        _tween.Set(2, new Vector2(57 * 6 + boatX + 21, 8), _time);

        // sea creatures (only in the sea)
        _creatures.Clear();
        if (!_caves)
        {
            int slots = Math.Min(41, (int)m[0x148A]);
            for (int slot = 1; slot < slots; slot += 2)
            {
                int p = m[0x16E2 + slot] | m[0x16E3 + slot] << 8;
                if (p < 0x400 || p > 0xBF00 || m[p + 0x14] != 1) continue;
                int frameA = m[p + 1] | m[p + 2] << 8;
                string kind; float w;
                if (frameA < 0x2C90) { kind = "jelly"; w = 2; }
                else if (frameA < 0x2D50) { kind = "shark"; w = 4; }
                else if (frameA < 0x2E10) { kind = "serpent"; w = 4; }
                else if (frameA < 0x2EA0) { kind = "squid"; w = 3; }
                else { kind = "eel"; w = 4; }
                bool left = m[p + 9] == 0;
                float x = m[p + 0x0C] + 1 + w / 2, y = m[p + 0x0B] / 2f;
                _tween.Set(100 + slot, SeaCell(x, y) + new Vector2(0, 4), _time);
                _creatures.Add((100 + slot, kind, m[p + 0x0A], left, w));
            }
            for (int i = 0; i < 9; i++)
            {
                int a = m[0x1490 + 2 * i] | m[0x1491 + 2 * i] << 8;
                if (a < 0xBB80 || a >= 0xBFE0) continue;
                int cell = a - 0xBB80;
                _oysters[i] = (SeaCell(cell % 40 + 0.5f, cell / 40) + new Vector2(0, 8), m[a] - 0x30);
            }
            _tentacle = m[0x1421];
        }
        // the urchin hunting in the caves
        _urchinOn = _caves && m[0x145C] != 0;
        if (_urchinOn) _tween.Set(3, CaveCell(m, m[0x145C] + 1.5f, m[0x145B] / 2f + 1), _time);
        _tween.Prune(_time - 1);
        return true;
    }

    public override void DrawScene(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        var dev = g.Device;
        _world.Paint(dev, _camSet ? 2 : 12, _cam);
        float now = _time + (Track.Blend - 1) / 50.08f;
        float dt = Math.Clamp(t - _lastDraw, 0, 0.1f);
        _lastDraw = t;

        // the view: the original playfield (rows 3-25, 40 columns) as a window onto the world
        var view = new RectangleF(r.X, r.Y + 24 * r.Height / 224f, r.Width, 184 * r.Height / 224f);
        float k = view.Width / 240f;                         // screen units per world pixel (x)
        float ky = view.Height / 184f;
        var boatNow = _tween.Get(2, now);
        var diver = _onBoat ? boatNow + new Vector2(-12, -10) : _tween.Get(1, now);
        // the camera: the sea keeps the original framing; in the caves it follows the diver everywhere
        Vector2 want = _caves
            ? new Vector2(diver.X - 120, diver.Y - 92)
            : new Vector2(57 * 6, -8);
        float maxX = ScubaWorld.Cols * 6 - 240, maxY = _world.Rows * 8 - 184;
        want.X = Math.Clamp(want.X, 0, maxX);
        want.Y = Math.Clamp(want.Y, -8, maxY);
        if (!_camSet)
        {
            _cam = want;
            _camSet = true;
        }
        _cam = Vector2.Lerp(_cam, want, 1 - MathF.Exp(-dt * 4));
        Vector2 S(Vector2 w) => new(view.X + (w.X - _cam.X) * k, view.Y + (w.Y - _cam.Y) * ky);

        g.PushClip(view);
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        // water: bright under the surface, darkening with depth; sky above the surface
        float depthTop = _cam.Y, depthBottom = _cam.Y + 184;
        Color Water(float wy) => Color.Lerp(new Color(30, 140, 170), new Color(2, 12, 30), Math.Clamp(wy / (_world.Rows * 8f) * 1.6f, 0, 1));
        g.Gradient(view, Water(depthTop) * alpha, Water(depthBottom) * alpha, 64);
        float surfaceY = S(new Vector2(0, 12)).Y;
        if (surfaceY > view.Y)
        {
            var sky = new RectangleF(view.X, view.Y, view.Width, surfaceY - view.Y);
            g.Gradient(sky, new Color(90, 150, 220) * alpha, new Color(200, 226, 245) * alpha, 32);
        }
        // light rays from the surface
        g.Additive();
        var ray = Kit.Scenery.Ray;
        for (int i = 0; i < 7; i++)
        {
            float wx = i * 90 + MathF.Sin(t * 0.25f + i * 1.7f) * 20;
            var top = S(new Vector2(wx + _cam.X * 0.5f % 90, 12));
            float a = (0.12f + 0.06f * MathF.Sin(t * 0.6f + i * 2.3f)) * Math.Clamp(1 - _cam.Y / 300f, 0, 1);
            g.Batch.Draw(ray, top, null, new Color(170, 230, 255) * (a * alpha), 0.2f + 0.05f * MathF.Sin(t * 0.2f + i),
                new Vector2(16, 0), new Vector2(view.Width * 0.08f / 32, view.Height * 0.9f / 256), SpriteEffects.None, 0);
        }
        // drifting specks in the water, at two depths
        for (int i = 0; i < 60; i++)
        {
            float px = (Hash(i) * 600 - _cam.X * (0.6f + Hash(i + 9) * 0.4f) + t * 2) % 260;
            if (px < -10) px += 260;
            float py = (Hash(i + 3) * 400 + t * (1 + Hash(i + 5) * 2) - _cam.Y * (0.6f + Hash(i + 9) * 0.4f)) % 200;
            if (py < 0) py += 200;
            g.Rect(view.X + px * k, view.Y + py * ky, 0.6f * k, 0.6f * k, new Color(190, 230, 240) * (0.25f * alpha));
        }
        g.Alpha();
        // the surface line
        if (surfaceY > view.Y && surfaceY < view.Bottom)
            g.Gradient(new RectangleF(view.X, surfaceY - 1.5f * ky, view.Width, 3 * ky), new Color(230, 245, 255) * (0.8f * alpha), new Color(120, 200, 230) * (0.2f * alpha), 6);

        // rock
        for (int i = 1; i <= ScubaWorld.MazeW; i++) DrawTile(g, -i, S, k, ky, alpha);
        for (int i = 0; i < 8 * _world.Height; i++) DrawTile(g, i, S, k, ky, alpha);

        // the boat
        var boat = _tween.Get(2, now);
        g.Sprite(ArtCache.Get(dev, "scuba-boat"), S(boat + new Vector2(0, -3)), 46 * k, MathF.Sin(t * 1.3f) * 0.02f, Color.White * alpha);

        // oysters, octopuses, treasure
        foreach (var (cell, state) in _oysters)
        {
            if (cell == Vector2.Zero) continue;
            g.Sprite(ArtCache.Get(dev, state >= 2 ? "scuba-oyster-open" : "scuba-oyster-shut"), S(cell + new Vector2(0, -3)), 10 * k, 0, Color.White * alpha);
        }
        int frame = (int)(t * 4) & 3;
        foreach (var o in _world.Octopuses)
        {
            var p = new Vector2(o.X * 6 + 9, o.Y * 8 + 8);
            bool seaBed = o.Y < ScubaWorld.SeaRows;
            float size = seaBed ? 30 + _tentacle * 1.5f : 22;
            g.Sprite(ArtCache.Get(dev, $"scuba-octopus{(frame + o.X) & 3}"), S(p), size * k, 0, Color.White * alpha);
        }
        foreach (var (cell, kind) in _world.Items)
        {
            var p = new Vector2(cell.X * 6 + 3, cell.Y * 8 + 3 + MathF.Sin(t * 2 + cell.X) * 0.6f);
            g.Additive();
            g.GlowAt(S(p), 6 * k, (kind == 1 ? new Color(80, 160, 255) : kind == 2 ? new Color(255, 210, 80) : new Color(200, 255, 220)) * (0.25f * alpha));
            g.Alpha();
            g.Sprite(ArtCache.Get(dev, $"scuba-item{kind}"), S(p), 8 * k, 0, Color.White * alpha);
        }

        // sea creatures
        foreach (var (id, kind, f, left, w) in _creatures)
        {
            var p = _tween.Get(id, now);
            float width = kind switch { "jelly" => 12, "squid" => 20, "shark" => 30, _ => 26 };
            int anim = ((int)(t * 6) + id) & 3;
            float bob = kind == "jelly" ? MathF.Sin(t * 2 + id) * 1.5f : 0;
            g.Sprite(ArtCache.Get(dev, $"scuba-{kind}{anim}"), S(p + new Vector2(0, bob)), width * k, 0, Color.White * alpha, left);
        }
        if (_urchinOn)
            g.Sprite(ArtCache.Get(dev, $"scuba-urchin{frame}"), S(_tween.Get(3, now)), 16 * k, t * 0.3f, Color.White * alpha);

        // the diver: standing on the deck while aboard, else swimming
        if (_onBoat)
            g.Sprite(ArtCache.Get(dev, "scuba-diver0"), S(diver), 16 * k, -MathHelper.PiOver2, Color.White * alpha);
        else
        {
            int f2 = ((int)(t * 7)) & 3;
            float rot = _diverFacing switch { 4 => MathHelper.PiOver2, 5 => -MathHelper.PiOver2, _ => 0 };
            bool flip = _diverFacing == 3;
            g.Sprite(ArtCache.Get(dev, $"scuba-diver{f2}"), S(diver), 22 * k, rot, Color.White * alpha, flip);
        }

        // rising bubbles from the diver
        if (_onBoat) diver = new Vector2(-1000, -1000);
        g.Additive();
        for (int i = 0; i < 6; i++)
        {
            float age = (t * 0.7f + i / 6f) % 1;
            var p = diver + new Vector2(6 + MathF.Sin(t * 3 + i) * 1.5f, -4 - age * 30);
            g.Texture(Kit.Scenery.Bubble, new RectangleF(S(p).X, S(p).Y, (1 + age) * k, (1 + age) * k), new Color(200, 240, 255) * (0.5f * (1 - age) * alpha));
        }
        g.Alpha();
        g.PopClip();
    }

    private void DrawTile(Gfx g, int key, Func<Vector2, Vector2> S, float k, float ky, float alpha)
    {
        var tex = _world.Tile(key);
        if (tex == null) return;
        var cells = ScubaWorld.TileCells(key);
        var a = S(new Vector2(cells.X * 6, cells.Y * 8));
        var dest = new RectangleF(a.X, a.Y, cells.Width * 6 * k, cells.Height * 8 * ky);
        if (dest.Right < 0 || dest.Bottom < 0 || dest.X > 10000) return;
        g.Texture(tex, dest, Color.White * alpha);
    }
}
