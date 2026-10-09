using System;
using System.Collections.Generic;
using Durell.Audio;
using Durell.Graphics;
using Durell.Programs;
using Durell.Graphics.Art;
using Durell.Graphics.ThreeD;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// Harrier Attack 3D: the original Harrier Attack program, unchanged, seen from a chase camera behind
/// the jet. The game's side-on world (read exactly as the 2D look reads it) becomes a lit landscape:
/// its land columns are the floor of a valley whose sides rise on either hand, its guns, town, carrier
/// and frigate become models, and the moving things are placed and tweened from the game's object
/// slots. The game only makes the land a column at a time ahead of the plane, so it emerges from the
/// haze in the distance.
/// </summary>
internal sealed class Harrier3DLook : HarrierLook
{
    private BasicEffect? _fx;
    private readonly MeshBuilder _mesh = new();
    private readonly MeshBuilder[] _guns = { HarrierModels.Gun(0), HarrierModels.Gun(1), HarrierModels.Gun(2) };
    private Vector3 _eye, _look;
    private bool _cameraSet;
    private float _lastT;
    private Matrix _view, _proj;
    private Viewport _vp;
    private readonly List<(Vector3 P, float Size, Texture2D Tex, Color C, float Rot)> _billboards = new();
    private readonly List<(Vector3 P, float Born, float Size, bool World)> _blasts = new();
    private readonly HashSet<long> _debrisSeen = new();
    private bool _crashShown;
    private float _roll;
    // turning back (the landing approach lets the plane fly either way): a half loop and a half roll
    private int _facing = 1;
    private float _turnStart = -10;
    private float _cameraYaw;
    private const float TurnTime = 1.3f;

    private static readonly float[] Zs = { -110, -80, -56, -38, -24, -14, -6, 0, 6, 14, 24, 38, 56, 80, 110 };
    private static readonly Color SkyTop = new(40, 92, 168), SkyLow = new(186, 214, 236);

    /// <summary>Scene values for the sound (see <see cref="Durell.Audio.HarrierSound"/>).</summary>
    public float PlaneAltitude { get; private set; }
    public float GroundBelow { get; private set; }

    private readonly HarrierSound _sound = new();
    private HarrierSound.State _state;
    private int _lastRow = -1;

    protected override bool ReadScene(GameProgram program, Scene scene)
    {
        bool active = base.ReadScene(program, scene);
        var m = program.Memory;
        var live = LiveScreen;
        int row = m[0x2111] / 2, col = m[0x2112];
        var s = new HarrierSound.State
        {
            Playing = active,
            Phase = m[0x216F],
            Speed = m[0x2113],
            Fuel = m[0x216C] | m[0x216D] << 8,
            PlaneX = col,
            PlaneRow = row,
            Climbing = _lastRow >= 0 && row < _lastRow,
            Bomb = m[0x2124] == 1,
            BombRow = m[0x2125] / 2f,
            BombX = m[0x2126],
            Jet = m[0x2171] == 1,
            JetX = m[0x2176],
            Missile = m[0x2173] == 1,
            MissileX = m[0x2178],
            Sam = m[0x2182] == 1,
            SamX = m[0x2184],
            Landing = m[0x217A],
            Score = Bcd(m[0x20D0]) * 100 + Bcd(m[0x20D1]),
            Clearance = 999,
        };
        _lastRow = row;
        if (row is > 0 and < 28 && col is > 1 and < 40)
        {
            byte at = live[row * 40 + col];
            s.Crashed = at is 0x5E or 0x60 or 0x61 or 0x63;
            for (int c = col + 1; c < 40 && c <= col + 8; c++) if (live[row * 40 + c] == 0x2D) s.Rockets = true;
            for (int r = row + 1; r <= 23; r++)
            {
                byte ch = live[r * 40 + col];
                if (ch is 0x7F or 0x2F or 0x3B or >= 0x64 and <= 0x6C)
                {
                    s.Clearance = (r - row - 1) * 8;
                    break;
                }
            }
        }
        for (int r = 1; r <= 23; r++)
            for (int c = 2; c < 40; c++)
            {
                byte ch = live[r * 40 + c];
                if (ch is 0x24 or 0x25) s.Debris++;
                else if (ch == 0x3C) s.Flak++;
            }
        _state = s;
        _sound.Update(_state, 0.02f);
        return active;
    }

    private static int Bcd(byte b) => (b >> 4) * 10 + (b & 15);

    public override bool MixAudio(GameProgram program, float[] stereo, bool enhanced)
    {
        if (!enhanced || !_state.Playing) return false;
        _sound.Render(stereo);
        return true;
    }

    public override void DrawScene(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        var dev = g.Device;
        float dt = Math.Clamp(t - _lastT, 0, 0.1f);
        _lastT = t;
        var (camera, f, from, to) = Timing();
        var world = World;
        var live = LiveScreen;

        // sky (2D, behind everything)
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        g.Gradient(r, SkyTop * alpha, SkyLow * alpha, 160);
        g.Additive();
        g.GlowAt(new Vector2(r.X + r.Width * 0.78f, r.Y + r.Height * 0.16f), r.Width * 0.28f, new Color(255, 236, 190) * (0.4f * alpha));
        g.Alpha();
        var clouds = ArtCache.Get(dev, "harrier-clouds");
        float cw = r.Width * 1.8f, off = (camera * 2.5f + t * 3) * r.Width / 240f % cw;
        for (float x = -off; x < r.Width; x += cw)
            g.Texture(clouds, new RectangleF(r.X + x, r.Y + r.Height * 0.08f, cw + 1, cw / 4), Color.White * (0.7f * alpha));

        // the plane, from its slots
        float row = Blend(from, to, 0, f) / 2, nose = Blend(from, to, 1, f);
        var plane = new Vector3(6 * nose - 4, 192 - (8 * row + 4), 0);
        PlaneAltitude = plane.Y;
        float climb = (to[0] - from[0]) * (f < 1 ? 1 : 0);
        _roll += ((to[1] - from[1]) * 0.15f - _roll) * MathF.Min(1, dt * 4);

        // which way the plane flies: a change of direction starts the loop-and-roll
        int dir = Math.Sign(to[1] - from[1]);
        if (dir != 0 && dir != _facing && t - _turnStart > TurnTime)
        {
            _facing = dir;
            _turnStart = t;
        }
        float turn = Math.Clamp((t - _turnStart) / TurnTime, 0, 1);
        bool turning = turn < 1;
        float loopLift = turning ? MathF.Sin(MathF.Min(1, turn * 2) * MathF.PI) * 14 : 0;

        // camera: behind, a little above and to one side, easing after the plane (and swinging round
        // to stay behind it after a turn)
        float yawWant = _facing > 0 ? 0 : MathF.PI;
        _cameraYaw += (yawWant - _cameraYaw) * (1 - MathF.Exp(-dt * 2.2f));
        var yaw = Matrix.CreateRotationY(_cameraYaw);
        var eyeWant = plane + Vector3.Transform(new Vector3(-62, 15, -30), yaw);
        var lookWant = plane + Vector3.Transform(new Vector3(70, -4, 6), yaw);
        if (Crashed)
        {
            // circle slowly round the wreck while the game shows "HARRIER DESTROYED"
            var w = new Vector3(CrashAt.X, 4, 0);
            float a = (t - CrashTime) * 0.25f - MathF.PI / 2;
            eyeWant = w + new Vector3(MathF.Cos(a) * 95, 40, MathF.Sin(a) * 95);
            lookWant = w;
        }
        if (!_cameraSet)
        {
            _eye = eyeWant;
            _look = lookWant;
            _cameraSet = true;
        }
        float k = 1 - MathF.Exp(-dt * 3.5f);
        _eye = Vector3.Lerp(_eye, eyeWant, k);
        _look = Vector3.Lerp(_look, lookWant, k);
        if (MathF.Abs(_cameraYaw) < 0.01f && !Crashed)
        {
            _eye.X = eyeWant.X;                  // no lag along the flight: the world scrolls smoothly already
            _look.X = lookWant.X;
        }

        // ---------------------------------------------------------- build the scene
        _mesh.Clear();
        _billboards.Clear();
        BuildTerrain(world, camera, t);
        BuildSea(camera, t);
        BuildObjects(g, world, camera, t);

        // moving things
        bool Live(int i) => !Crashed && (to[i] == 1 || from[i] == 1 && f < 1);
        if (Live(11))
            _mesh.Add(HarrierModels.Bomb, Matrix.CreateRotationZ(-1.2f) * Matrix.CreateTranslation(6 * (Blend(from, to, 13, f) + 1) + 3, 192 - (4 * Blend(from, to, 12, f) + 4), 0));
        if (Live(2))
            _mesh.Add(HarrierModels.Mig, Matrix.CreateRotationY(MathHelper.Pi) * Matrix.CreateTranslation(6 * (Blend(from, to, 4, f) + 2), 192 - (4 * Blend(from, to, 3, f) + 4), 0));
        foreach (int s in new[] { 5, 8 })
            if (Live(s))
            {
                var p = new Vector3(6 * (Blend(from, to, s + 2, f) + 1) + 3, 192 - (4 * Blend(from, to, s + 1, f) + 4), 0);
                _mesh.Add(HarrierModels.Missile, Matrix.CreateRotationY(MathHelper.Pi) * Matrix.CreateTranslation(p));
                _billboards.Add((p + new Vector3(5, 0, 0), 5, g.Glow, new Color(255, 170, 80), 0));
                for (int i = 1; i <= 4; i++)
                    _billboards.Add((p + new Vector3(6 + i * 5, 0, 0), 3 + i * 1.5f, g.Glow, new Color(200, 200, 205) * (0.4f / i), 0));
            }

        int liveRow = to[0] / 2, liveCol = to[1];
        byte at = liveRow is > 0 and < 28 && liveCol is > 1 and < 40 ? live[liveRow * 40 + liveCol] : (byte)0;
        bool crashed = Crashed || at is 0x5E or 0x60 or 0x61 or 0x63;
        if (crashed)
        {
            if (!_crashShown) _blasts.Add((plane, t, 40, false));
            _crashShown = true;
            // the wreck burns on the surface below where it went down
            var wreck = new Vector3(CrashAt.X, 1, 0);
            _billboards.Add((wreck, 6 + MathF.Sin(t * 9) * 1.5f, g.Glow, new Color(255, 140, 40), 0));
            for (int i = 0; i < 8; i++)
            {
                float age = (t * 0.4f + i / 8f) % 1;
                _billboards.Add((wreck + new Vector3(age * 10, age * 45, MathF.Sin(t + i) * 3), 5 + age * 16, g.Glow, new Color(45, 45, 50) * (0.75f * (1 - age)), 0));
            }
        }
        else
        {
            _crashShown = false;
            float pitch = -climb * 0.12f * (1 - f) * 2;
            Matrix orient;
            if (turning)
            {
                // first half: pull up and over (upside down, facing back); second half: roll upright
                float loop = MathF.Min(1, turn * 2) * MathF.PI, roll = MathF.Max(0, turn * 2 - 1) * MathF.PI;
                var start = _facing > 0 ? Matrix.CreateRotationY(MathF.PI) : Matrix.Identity;
                orient = start * Matrix.CreateRotationZ(_facing > 0 ? -loop : loop) * Matrix.CreateRotationX(roll);
            }
            else orient = (_facing > 0 ? Matrix.Identity : Matrix.CreateRotationY(MathF.PI)) * Matrix.CreateRotationX(_roll) * Matrix.CreateRotationZ(pitch * _facing);
            _mesh.Add(HarrierModels.Jet, orient * Matrix.CreateTranslation(plane + new Vector3(0, loopLift, 0)));
            // engine glow and shadow on the ground below
            _billboards.Add((plane + new Vector3(-12, 0.3f, 0), 4, g.Glow, new Color(255, 200, 140) * 0.6f, 0));
            int streak = 0;
            for (int c = liveCol + 1; c < 40 && c <= liveCol + 8; c++)
                if (live[liveRow * 40 + c] == 0x2D) streak = c;
            if (streak > 0)
            {
                float flight = (t * 6) % 1;
                var a = new Vector3(6 * (liveCol + 1), plane.Y, 0);
                var b = new Vector3(6 * (streak + 1), plane.Y, 0);
                foreach (float z in new[] { -2.5f, 2.5f })
                {
                    var p = Vector3.Lerp(a, b, flight) + new Vector3(0, -1, z);
                    _mesh.Add(HarrierModels.Missile, Matrix.CreateScale(0.7f) * Matrix.CreateTranslation(p));
                    _billboards.Add((p - new Vector3(4, 0, 0), 3.5f, g.Glow, new Color(255, 220, 150), 0));
                    for (int i = 1; i <= 5; i++)
                        _billboards.Add((Vector3.Lerp(a, p, 1 - i / 6f) + new Vector3(0, -1, z), 2 + i * 0.4f, g.Glow, new Color(230, 230, 235) * 0.25f, 0));
                }
            }
        }

        // ---------------------------------------------------------- render
        g.End();
        var saved = dev.Viewport;
        var rect = g.DeviceRect(r);
        rect = Rectangle.Intersect(rect, saved.Bounds);
        if (rect.Width < 8 || rect.Height < 8) return;
        _vp = new Viewport(rect.X, rect.Y, rect.Width, rect.Height);
        dev.Viewport = _vp;
        dev.Clear(ClearOptions.DepthBuffer, Color.Black, 1, 0);
        dev.DepthStencilState = DepthStencilState.Default;
        dev.RasterizerState = RasterizerState.CullNone;
        dev.BlendState = BlendState.Opaque;
        _fx ??= new BasicEffect(dev) { VertexColorEnabled = true, LightingEnabled = true, PreferPerPixelLighting = false };
        _view = Matrix.CreateLookAt(_eye, _look, Vector3.Up);
        _proj = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(52), _vp.AspectRatio, 1f, 700f);
        _fx.World = Matrix.Identity;
        _fx.View = _view;
        _fx.Projection = _proj;
        _fx.AmbientLightColor = new Vector3(0.42f, 0.44f, 0.5f);
        _fx.DirectionalLight0.Enabled = true;
        _fx.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.35f, -1f, 0.45f));
        _fx.DirectionalLight0.DiffuseColor = new Vector3(0.82f, 0.78f, 0.7f);
        _fx.DirectionalLight0.SpecularColor = new Vector3(0.15f);
        _fx.DirectionalLight1.Enabled = false;
        _fx.DirectionalLight2.Enabled = false;
        _fx.FogEnabled = true;
        _fx.FogColor = SkyLow.ToVector3();
        _fx.FogStart = 150;
        _fx.FogEnd = 300;
        _fx.Alpha = alpha;
        _mesh.Draw(dev, _fx);
        dev.Viewport = saved;

        // ---------------------------------------------------------- billboards and explosions (2D, projected)
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        for (int i = _blasts.Count - 1; i >= 0; i--)
        {
            var (p, born, size, worldPos) = _blasts[i];
            float age = t - born;
            if (age < 0 || age > 1.1f)
            {
                _blasts.RemoveAt(i);
                continue;
            }
            if (worldPos) p.X -= 6 * camera;
            int frame = Math.Min(3, (int)(age / 0.27f));
            _billboards.Add((p + new Vector3(0, age * 6, 0), size * (0.7f + age * 0.5f), ArtCache.Get(dev, $"harrier-blast{frame}"), Color.White, age));
        }
        // far first
        _billboards.Sort((a, b) => Vector3.DistanceSquared(b.P, _eye).CompareTo(Vector3.DistanceSquared(a.P, _eye)));
        foreach (var (p, size, tex, c, rot) in _billboards)
        {
            var s = _vp.Project(p, _proj, _view, Matrix.Identity);
            if (s.Z <= 0 || s.Z >= 1) continue;
            var s2 = _vp.Project(p + Vector3.Up * size, _proj, _view, Matrix.Identity);
            float px = MathF.Abs(s2.Y - s.Y) * 2;
            var at2 = g.FromDevice(new Vector2(s.X, s.Y));
            float w = px / g.Scale;
            if (w < 0.3f || w > r.Width * 3) continue;
            float fog = Math.Clamp((Vector3.Distance(p, _eye) - 150) / 150, 0, 1);
            var col = Color.Lerp(c, SkyLow * (c.A / 255f), fog * 0.8f) * alpha;
            g.Sprite(tex, at2, w, rot, col);
        }
        DrawMessages(g, r, t, alpha);
    }

    private void BuildTerrain(HarrierWorld world, float camera, float t)
    {
        var cols = new List<HarrierWorld.Column>(world.Columns);
        for (int i = 0; i < cols.Count; i++)
        {
            var c = cols[i];
            bool land = world.Heights(c, out float hl, out float hr);
            float x0 = 6 * (c.Abs - camera), x1 = x0 + 6;
            if (x1 < -200 || x0 > 330) continue;
            if (!land)
            {
                // a beach shelf where land meets sea, so the land doesn't end in a wall
                bool prevLand = i > 0 && world.Heights(cols[i - 1], out _, out float ph) && ph > 0;
                bool nextLand = i + 1 < cols.Count && world.Heights(cols[i + 1], out float nh, out _) && nh > 0;
                if (!prevLand && !nextLand) continue;
                hl = prevLand ? 2 : -4;
                hr = nextLand ? 2 : -4;
            }
            for (int zi = 0; zi < Zs.Length - 1; zi++)
            {
                float za = Zs[zi], zb = Zs[zi + 1];
                Vector3 P(float x, float h, float z) => new(x, Height(x + 6 * camera, h, z), z);
                var a = P(x0, hl, za); var b = P(x1, hr, za); var cc = P(x1, hr, zb); var d = P(x0, hl, zb);
                Color C(Vector3 v) => GroundColour(v, v.X + 6 * camera);
                var n = Vector3.Normalize(Vector3.Cross(d - a, b - a));
                if (n.Y < 0) n = -n;
                _mesh.QuadSmooth(a, b, cc, d, n, n, n, n, C(a), C(b), C(cc), C(d));
            }
        }
    }

    /// <summary>The ground height across the valley: the game's land along the flight lane, rising into hills either side.</summary>
    private static float Height(float worldX, float h, float z)
    {
        float az = MathF.Abs(z);
        if (az <= 14 || h <= 0) return h;
        float side = (az - 14);
        float hills = Noise.Fbm(worldX * 0.012f, z * 0.02f, 7, 4);
        return h + side * 0.16f + (hills - 0.42f) * side * 0.35f;
    }

    private static Color GroundColour(Vector3 v, float worldX)
    {
        float n = Noise.Fbm(worldX * 0.05f, v.Z * 0.05f, 3, 3);
        if (v.Y < 3) return Color.Lerp(new Color(194, 178, 128), new Color(170, 150, 100), n);
        var grass = Color.Lerp(new Color(62, 110, 44), new Color(104, 140, 60), n);
        float rock = Math.Clamp((v.Y - 70) / 50, 0, 1);
        return Color.Lerp(grass, new Color(120, 112, 100), rock);
    }

    private void BuildSea(float camera, float t)
    {
        const float step = 40;
        for (float x = -240; x < 440; x += step)
            for (float z = -400; z < 400; z += step)
            {
                float wx = x + 6 * camera;
                Color C(float px, float pz)
                {
                    float n = Noise.Value((px + 6 * camera) * 0.02f + t * 0.3f, pz * 0.02f, 9);
                    return Color.Lerp(new Color(24, 82, 140), new Color(48, 120, 180), n);
                }
                var a = new Vector3(x, 0, z); var b = new Vector3(x + step, 0, z); var c = new Vector3(x + step, 0, z + step); var d = new Vector3(x, 0, z + step);
                _mesh.QuadSmooth(a, b, c, d, Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up, C(a.X, a.Z), C(b.X, b.Z), C(c.X, c.Z), C(d.X, d.Z));
                _ = wx;
            }
    }

    private void BuildObjects(Gfx g, HarrierWorld world, float camera, float t)
    {
        var dev = g.Device;
        foreach (var c in world.Columns)
        {
            float x0 = 6 * (c.Abs - camera);
            if (x0 < -200 || x0 > 330) continue;
            for (int row = 1; row <= 23; row++)
            {
                byte ch = c.Chars[row];
                if (ch <= 0x20 || ch >= 0x80 || ch is 0x7F or 0x2F or 0x3B) continue;
                if (world.IsSetPiece(c, row)) continue;
                float y0 = 192 - 8 * (row + 1), cx = x0 + 3;
                byte ink = c.Inks[row];
                switch (ch)
                {
                    case 0x64: case 0x65: case 0x66:
                        _mesh.Add(_guns[ch - 0x64], Matrix.CreateRotationY(MathHelper.Pi) * Matrix.CreateTranslation(cx, y0, 0));
                        break;
                    case 0x2E: case 0x3A: case 0x3E:
                        // the game's clouds: soft puffs
                        for (int i = 0; i < 2; i++)
                            _billboards.Add((new Vector3(cx + i * 3, y0 + 4, -4 + i * 8), 9, Kit.Scenery.Cloud, Color.White * 0.85f, 0));
                        break;
                    case 0x3C:
                        _billboards.Add((new Vector3(cx, y0 + 4, -3), 7, g.Glow, new Color(40, 40, 44) * 0.7f, 0));
                        break;
                    case 0x24: case 0x25:
                    {
                        long key = c.Abs * 64 + row;
                        if (_debrisSeen.Add(key)) _blasts.Add((new Vector3(c.Abs * 6 + 3, y0 + 4, 0), t, 26, true));
                        _mesh.Box(new Vector3(cx, y0 + 1.5f, 0), new Vector3(6, 3, 8), new Color(40, 36, 32));
                        for (int i = 0; i < 4; i++)
                        {
                            float age = (t * 0.5f + i * 0.25f + (c.Abs % 5) * 0.11f) % 1f;
                            _billboards.Add((new Vector3(cx + age * 6, y0 + 4 + age * 24, MathF.Sin(t + i) * 2), 4 + age * 10, g.Glow, new Color(50, 50, 54) * (0.6f * (1 - age)), 0));
                        }
                        if (ink == 1) _billboards.Add((new Vector3(cx, y0 + 3, 0), 5, g.Glow, new Color(255, 140, 40) * 0.8f, 0));
                        break;
                    }
                    case 0x6B:      // roof slope up
                        _mesh.Plate(Matrix.CreateScale(6, 8, 18) * Matrix.CreateTranslation(x0, y0, 0), new Color(170, 64, 44), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1));
                        break;
                    case 0x6C:      // roof slope down
                        _mesh.Plate(Matrix.CreateScale(6, 8, 18) * Matrix.CreateTranslation(x0, y0, 0), new Color(170, 64, 44), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1));
                        break;
                    case 0x6A:      // wall with windows
                        _mesh.Box(new Vector3(cx, y0 + 4, 0), new Vector3(6, 8, 18), new Color(150, 140, 126));
                        _mesh.Box(new Vector3(cx, y0 + 5, -9.05f), new Vector3(3.6f, 1.6f, 0.2f), new Color(255, 220, 140));
                        break;
                    default:
                    {
                        var col = ink == 1 ? new Color(170, 64, 44) : new Color(96, 98, 100);
                        float depth = ch == 0x69 && ink != 1 ? 8 : 18;
                        _mesh.Box(new Vector3(cx, y0 + 4, 0), new Vector3(6, 8, depth), col);
                        break;
                    }
                }
            }
        }
        foreach (var (kind, first, last, row) in world.Pieces)
        {
            float x0 = 6 * (first - camera), len = (last - first + 1) * 6;
            if (x0 + len < -200 || x0 > 330) continue;
            if (kind == "carrier")
            {
                _mesh.Add(HarrierModels.Carrier, Matrix.CreateScale(len, 1, 1) * Matrix.CreateTranslation(x0 + len / 2, 8, 0));
                foreach (float u in new[] { 0.15f, 0.32f })
                    _mesh.Add(HarrierModels.Jet, Matrix.CreateScale(0.8f) * Matrix.CreateTranslation(x0 + len * u, 11, 4));
            }
            else
                _mesh.Add(HarrierModels.Frigate, Matrix.CreateTranslation(x0 + 12, 0, 0));
        }
    }
}
