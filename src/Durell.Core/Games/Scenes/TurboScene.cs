using System;
using System.Collections.Generic;
using Durell.Graphics;
using Durell.Graphics.ThreeD;
using Durell.Machine;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// Turbo Esprit's road view redrawn as a real 3D city. The game rebuilds its view only 2-3 times a
/// second; a probe at $C11B (the start of its sprite scan) takes a consistent snapshot each time -
/// the player's position and heading, the traffic and pedestrians - in world coordinates
/// (<see cref="TurboCity"/>), and the display extrapolates smoothly between snapshots. The streets,
/// junctions, buildings (at the game's own seeded heights) and lamps are built from the city map the
/// game keeps in RAM, so the whole city is there in every direction. The dashboard is the original's.
/// </summary>
internal sealed partial class TurboLook
{
    private const int SpriteScanProbe = 0xC11B;

    private sealed class Thing
    {
        public int Id;
        public Vector2 Pos;
        public float Yaw;
        public Color Colour;
        public int Kind;          // 0 car, 1 pedestrian, 2 traffic light, 3 barrier
        public int Frame;
        public bool Wrecked;
    }

    private sealed class Snap
    {
        public long Cycle;
        public Vector2 Player;
        public float Yaw;
        public int NodeL, NodeH;
        public int Light;
        public bool Wrecked;
        public readonly List<Thing> Things = new();
    }

    private readonly TurboCity _city = new();
    private OricProgram? _program;
    private Snap? _s0, _s1;
    private long _frameCycle;
    private bool _mapView, _playing;
    private BasicEffect? _fx;
    private readonly MeshBuilder _static = new(), _dynamic = new();
    private int _staticL = -99, _staticH = -99;
    private Vector2 _shownPos;
    private float _shownYaw;
    private bool _shownSet;
    private float _lastT;
    private readonly Dictionary<uint, MeshBuilder> _saloons = new();
    private readonly MeshBuilder[] _walkers = { TurboModels.Walker(new Color(60, 90, 160), 0.35f), TurboModels.Walker(new Color(60, 90, 160), -0.35f), TurboModels.Walker(new Color(150, 60, 60), 0.35f), TurboModels.Walker(new Color(150, 60, 60), -0.35f) };
    private readonly MeshBuilder[] _lights = { TurboModels.Light(0), TurboModels.Light(1), TurboModels.Light(2), TurboModels.Light(3) };

    private static readonly Color[] Spectrum =
    {
        new(30, 30, 34), new(40, 60, 190), new(190, 40, 40), new(170, 50, 170),
        new(40, 160, 60), new(40, 170, 190), new(210, 190, 50), new(220, 222, 226),
    };

    private static readonly Color[] Paint =
    {
        new(200, 200, 205), new(30, 40, 90), new(110, 20, 30), new(40, 90, 60), new(230, 200, 60), new(70, 70, 76),
        new(150, 160, 175), new(20, 20, 24), new(180, 90, 40), new(60, 120, 170), new(240, 240, 236),
    };

    public override Rectangle? SceneArea => new Rectangle(6, 4, 224, 96);

    public override void Attach(GameProgram program)
    {
        if (program is not OricProgram op) return;
        _program = op;
        var prev = op.Probe;
        op.Probe = (p, a) =>
        {
            prev?.Invoke(p, a);
            if (a == SpriteScanProbe) TakeSnapshot();
        };
    }

    private void TakeSnapshot()
    {
        var m = _program!.Memory;
        _city.Read(m);
        var s = new Snap { Cycle = _program.Machine.Cpu.Cy };
        int hd = m[0x75F0] & 3;
        int turn = m[0x7797];
        float phase = (turn & 6) / 6f * ((turn & 1) != 0 ? -1 : 1);
        s.Yaw = (hd + phase) * MathHelper.PiOver2;
        s.Player = _city.Position(m, 0x7792);
        s.NodeL = m[0x7504];
        s.NodeH = m[0x7505];
        s.Light = m[0x75FA] & 3;
        s.Wrecked = (m[0x760E] & 8) != 0;
        for (int id = 1; id <= 27; id++)
        {
            if (id is >= 4 and <= 12) continue;
            int rec = 0x7702 + 12 * id;
            if ((m[rec + 1] & 0x80) == 0) continue;
            // the Oric draws every car black; give each its own paint (the drug cars and hit-car keep theirs)
            var colour = id is >= 13 and <= 16 ? Spectrum[2 + (id & 1)] : Paint[(id * 7) % Paint.Length];
            var t = new Thing { Id = id, Pos = _city.Position(m, rec), Colour = colour, Wrecked = (m[rec + 1] & 2) != 0 };
            if (id <= 3)
            {
                t.Kind = 1;
                t.Frame = m[rec + 5] & 7;
                t.Yaw = s.Yaw;
            }
            else
            {
                int dir = m[rec + 5];
                float rel = dir switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, _ => 0.5f };
                t.Yaw = (hd + rel) * MathHelper.PiOver2;
            }
            s.Things.Add(t);
        }
        // traffic lights and road works are only cells of the lane grid
        for (int page = 0x5C; page <= 0x63; page++)
            for (int row = 0; row < 256; row++)
            {
                byte v = m[page * 256 + row];
                if (v != 7 && v is not (8 or 0x0A or 0x0B)) continue;
                s.Things.Add(new Thing { Id = 1000 + page * 256 + row, Kind = v == 7 ? 2 : 3, Pos = _city.Position(m, -1, row, page), Yaw = s.Yaw });
            }
        _s0 = _s1;
        _s1 = s;
    }

    protected override bool ReadScene(GameProgram program, Scene scene)
    {
        var m = program.Memory;
        _frameCycle = program.Frame * OricMachine.CyclesPerFrame;
        _mapView = (m[0x7610] & 1) != 0;
        _playing = HiresTextHas(m, 0, "PENALTY");
        return _playing && !_mapView && _s1 != null;
    }

    private static bool HiresTextHas(byte[] m, int line, string text)
    {
        int b = 0xBF68 + line * 40;
        for (int c = 0; c + text.Length <= 40; c++)
        {
            int k = 0;
            while (k < text.Length && (m[b + c + k] & 0x7F) == text[k]) k++;
            if (k == text.Length) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ drawing

    private (Vector2 Pos, float Yaw) Predict(Vector2 p1, float y1, Vector2? p0, float? y0, long c1, long c0, long rc)
    {
        if (p0 is not Vector2 a || y0 is not float ya || c1 <= c0) return (p1, y1);
        long period = c1 - c0;
        float u = Math.Clamp((rc - c1) / (float)period, 0, 1.3f);
        if (Vector2.Distance(a, p1) > 60) return (p1, y1);
        float dy = MathHelper.WrapAngle(y1 - ya);
        return (p1 + (p1 - a) * u, y1 + dy * MathF.Min(u, 1));
    }

    public override void DrawScene(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        if (_s1 == null) return;
        var dev = g.Device;
        float dt = Math.Clamp(t - _lastT, 0, 0.1f);
        _lastT = t;
        long rc = _frameCycle - OricMachine.CyclesPerFrame + (long)(Track.Blend * OricMachine.CyclesPerFrame);
        var (pos, yaw) = Predict(_s1.Player, _s1.Yaw, _s0?.Player, _s0?.Yaw, _s1.Cycle, _s0?.Cycle ?? 0, rc);
        if (!_shownSet || Vector2.Distance(pos, _shownPos) > 60)
        {
            _shownPos = pos;
            _shownYaw = yaw;
            _shownSet = true;
        }
        float k = 1 - MathF.Exp(-dt * 9);
        _shownPos = Vector2.Lerp(_shownPos, pos, k);
        _shownYaw += MathHelper.WrapAngle(yaw - _shownYaw) * k;

        float kx = r.Width / 240f, ky = r.Height / 224f;
        var view = new RectangleF(r.X + 6 * kx, r.Y + 4 * ky, 224 * kx, 96 * ky);
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        g.Gradient(view, new Color(46, 98, 186) * alpha, new Color(176, 206, 236) * alpha, 64);
        g.Additive();
        g.GlowAt(new Vector2(view.X + view.Width * 0.75f, view.Y + view.Height * 0.15f), view.Width * 0.3f, new Color(255, 236, 200) * (0.3f * alpha));
        g.Alpha();

        if (_s1.NodeL != _staticL || _s1.NodeH != _staticH) BuildStatic(_s1.NodeL, _s1.NodeH);
        BuildDynamic(rc, t);

        g.End();
        var saved = dev.Viewport;
        var rect = Rectangle.Intersect(g.DeviceRect(view), saved.Bounds);
        if (rect.Width < 8 || rect.Height < 8) return;
        var vp = new Viewport(rect.X, rect.Y, rect.Width, rect.Height);
        dev.Viewport = vp;
        dev.Clear(ClearOptions.DepthBuffer, Color.Black, 1, 0);
        dev.DepthStencilState = DepthStencilState.Default;
        dev.RasterizerState = RasterizerState.CullNone;
        dev.BlendState = BlendState.Opaque;
        _fx ??= new BasicEffect(dev) { VertexColorEnabled = true, LightingEnabled = true };
        var fwd = new Vector3(MathF.Sin(_shownYaw), 0, -MathF.Cos(_shownYaw));
        var car = new Vector3(_shownPos.X, 0, -_shownPos.Y);
        // a chase camera just behind and above the Lotus
        var eye = car - fwd * 13f + Vector3.Up * 4.2f;
        float hfov = MathHelper.ToRadians(70);
        _fx.View = Matrix.CreateLookAt(eye, car + fwd * 22 + Vector3.Up * 1.2f, Vector3.Up);
        _fx.Projection = Matrix.CreatePerspectiveFieldOfView(2 * MathF.Atan(MathF.Tan(hfov / 2) / vp.AspectRatio), vp.AspectRatio, 0.5f, 600);
        _fx.World = Matrix.Identity;
        _fx.AmbientLightColor = new Vector3(0.45f, 0.46f, 0.5f);
        _fx.DirectionalLight0.Enabled = true;
        _fx.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.4f, -1f, -0.3f));
        _fx.DirectionalLight0.DiffuseColor = new Vector3(0.75f, 0.72f, 0.66f);
        _fx.DirectionalLight0.SpecularColor = Vector3.Zero;
        _fx.DirectionalLight1.Enabled = false;
        _fx.DirectionalLight2.Enabled = false;
        _fx.FogEnabled = true;
        _fx.FogColor = new Vector3(0.69f, 0.8f, 0.92f);
        _fx.FogStart = 160;
        _fx.FogEnd = 420;
        _fx.Alpha = alpha;
        _static.Draw(dev, _fx);
        _dynamic.Draw(dev, _fx);
        dev.Viewport = saved;
        g.Begin();
    }

    private void BuildDynamic(long rc, float t)
    {
        _dynamic.Clear();
        var s1 = _s1!;
        // the Lotus
        var lotusAt = new Vector3(_shownPos.X, 0, -_shownPos.Y);
        float roll = MathHelper.WrapAngle(s1.Yaw - _shownYaw) * 0.15f;
        _dynamic.Add(TurboModels.Lotus, Matrix.CreateRotationZ(roll) * Matrix.CreateRotationY(-_shownYaw) * Matrix.CreateTranslation(lotusAt));
        foreach (var th in s1.Things)
        {
            Thing? prev = null;
            if (_s0 != null)
                foreach (var o in _s0.Things)
                    if (o.Id == th.Id) { prev = o; break; }
            var (p, yaw) = Predict(th.Pos, th.Yaw, prev?.Pos, prev?.Yaw, s1.Cycle, _s0?.Cycle ?? 0, rc);
            var at = new Vector3(p.X, 0, -p.Y);
            var orient = Matrix.CreateRotationY(-yaw) * Matrix.CreateTranslation(at);
            switch (th.Kind)
            {
                case 0:
                {
                    uint key = th.Colour.PackedValue;
                    if (!_saloons.TryGetValue(key, out var model)) _saloons[key] = model = TurboModels.Saloon(th.Colour);
                    _dynamic.Add(model, (th.Wrecked ? Matrix.CreateRotationZ(0.3f) : Matrix.Identity) * orient);
                    break;
                }
                case 1:
                    _dynamic.Add(_walkers[(th.Id % 2) * 2 + ((th.Frame >> 1) & 1)], orient);
                    break;
                case 2:
                    _dynamic.Add(_lights[s1.Light], Matrix.CreateRotationY(-yaw + MathF.PI) * Matrix.CreateTranslation(at));
                    break;
                default:
                    _dynamic.Box(at + new Vector3(0, 0.6f, 0), new Vector3(2.6f, 1.2f, 0.4f), ((int)(t * 2) & 1) == 0 ? new Color(230, 120, 30) : new Color(240, 240, 240));
                    break;
            }
        }
    }

    // ------------------------------------------------------------------ the static city

    private static Vector3 W(float x, float y, float h) => new(x, h, -y);

    private void Flat(MeshBuilder m, float x0, float y0, float x1, float y1, float h, Color c) =>
        m.Quad(W(x0, y0, h), W(x1, y0, h), W(x1, y1, h), W(x0, y1, h), c);

    private void BuildStatic(int nl, int nh)
    {
        _staticL = nl;
        _staticH = nh;
        var m = _static;
        m.Clear();
        int span = 3;
        int lo = Math.Max(-1, nl - span), hi = Math.Min(32, nl + span);
        int lo2 = Math.Max(-1, nh - span), hi2 = Math.Min(32, nh + span);
        // the ground under everything
        float gx0 = _city.JunctionX(lo) - 150, gx1 = _city.JunctionX(hi) + 150, gy0 = _city.JunctionY(lo2) - 150, gy1 = _city.JunctionY(hi2) + 150;
        Flat(m, gx0, gy0, gx1, gy1, -0.05f, new Color(96, 98, 92));
        var asphalt = new Color(64, 66, 70);
        var junction = new Color(74, 76, 80);
        for (int h = lo2; h <= hi2; h++)
            for (int l = lo; l <= hi; l++)
            {
                float jx = _city.JunctionX(l), jy = _city.JunctionY(h);
                int wl = _city.StreetWidthX(l), wh = _city.StreetWidthY(h);
                bool n = l is >= 0 and < 32 && _city.NorthOpen(l, h), e = h is >= 0 and < 32 && _city.EastOpen(l, h);
                bool any = n || e || _city.EastOpen(l - 1, h) || _city.NorthOpen(l, h - 1);
                if (any) Flat(m, jx, jy, jx + wl, jy + wh, 0, junction);
                if (n)
                {
                    float ya = jy + wh, yb = ya + 100;
                    Flat(m, jx, ya, jx + wl, yb, 0, asphalt);
                    Markings(m, jx, ya, jx + wl, yb, true, _city.Block[256 + Math.Clamp(l, 0, 31)]);
                    Pavement(m, jx - 5, ya, jx, yb);
                    Pavement(m, jx + wl, ya, jx + wl + 5, yb);
                    if (h is >= 0 and < 32)
                    {
                        var (a, b) = TurboCity.Buildings(l, h);
                        Frontage(m, a, ya, jx - 5, -1, true, l * 64 + h);
                        Frontage(m, b, ya, jx + wl + 5, 1, true, l * 64 + h + 9000);
                    }
                    for (float y = ya + 12; y < yb; y += 24)
                    {
                        m.Add(TurboModels.Lamp, Matrix.CreateRotationY(MathF.PI) * Matrix.CreateTranslation(W(jx - 1.5f, y, 0.25f)));
                        m.Add(TurboModels.Lamp, Matrix.CreateTranslation(W(jx + wl + 1.5f, y, 0.25f)));
                    }
                }
                if (e)
                {
                    float xa = jx + wl, xb = xa + 100;
                    Flat(m, xa, jy, xb, jy + wh, 0, asphalt);
                    Markings(m, xa, jy, xb, jy + wh, false, _city.Block[288 + Math.Clamp(h, 0, 31)]);
                    Pavement(m, xa, jy + wh, xb, jy + wh + 5);
                    Pavement(m, xa, jy - 5, xb, jy);
                    if (l is >= 0 and < 32)
                    {
                        var (a, b) = TurboCity.Buildings(l, h);
                        Frontage(m, a, xa, jy + wh + 5, 1, false, l * 64 + h + 3000);
                        Frontage(m, b, xa, jy - 5, -1, false, l * 64 + h + 6000);
                    }
                }
            }
    }

    private void Pavement(MeshBuilder m, float x0, float y0, float x1, float y1)
    {
        m.Box(new Vector3((x0 + x1) / 2, 0.15f, -(y0 + y1) / 2), new Vector3(x1 - x0, 0.3f, y1 - y0), new Color(150, 148, 140), new Color(170, 168, 160));
    }

    private void Markings(MeshBuilder m, float x0, float y0, float x1, float y1, bool alongY, byte type)
    {
        int lanes = TurboCity.Lanes[type & 3];
        bool oneWay = (type & 0x80) != 0 || (type & 3) == 3;
        float w = alongY ? x1 - x0 : y1 - y0, len = alongY ? y1 - y0 : x1 - x0;
        int total = oneWay ? lanes : lanes * 2;
        for (int i = 1; i < total; i++)
        {
            float off = w * i / total;
            bool centre = !oneWay && i == lanes;
            for (float d = 2; d < len - 2; d += centre ? 4 : 8)
            {
                float dl = centre ? 4 : 3.5f;
                if (alongY) Flat(m, x0 + off - 0.25f, y0 + d, x0 + off + 0.25f, y0 + MathF.Min(len, d + dl), 0.02f, centre ? new Color(230, 200, 60) : new Color(230, 230, 230));
                else Flat(m, x0 + d, y0 + off - 0.25f, x0 + MathF.Min(len, d + dl), y0 + off + 0.25f, 0.02f, centre ? new Color(230, 200, 60) : new Color(230, 230, 230));
            }
        }
    }

    private static readonly float[] Heights = { 0, 22, 30, 44, 60 };

    /// <summary>One side of a street: 12 rows of type 2, six 12-row buildings, then 16 rows of type 2.</summary>
    private void Frontage(MeshBuilder m, int[] types, float start, float face, int side, bool alongY, int seed)
    {
        const float depth = 26;
        float pos = start;
        for (int i = 0; i < 8; i++)
        {
            int type = i == 0 || i == 7 ? 2 : types[i - 1];
            float len = i == 7 ? 16 : 12;
            float hgt = Heights[type] + Hash(seed * 13 + i) * 6;
            var wall = BuildingColour(type, seed * 7 + i);
            float a = pos + 0.6f, b = pos + len - 0.6f;
            float f0 = face, f1 = face + side * depth;
            Vector3 centre, size;
            if (alongY)
            {
                centre = new Vector3((f0 + f1) / 2, hgt / 2, -(a + b) / 2);
                size = new Vector3(depth, hgt, b - a);
            }
            else
            {
                centre = new Vector3((a + b) / 2, hgt / 2, -(f0 + f1) / 2);
                size = new Vector3(b - a, hgt, depth);
            }
            m.Box(centre, size, wall, ArtLighter(wall));
            // rows of windows on the street face
            var glass = new Color(46, 60, 78);
            for (float y = 3; y < hgt - 2; y += 4)
            {
                bool lit = Hash(seed * 31 + i * 7 + (int)y) > 0.82f;
                var c = lit ? new Color(240, 210, 130) : glass;
                float fx = face - side * 0.06f;
                if (alongY) m.Quad(W(fx, a + 0.8f, y), W(fx, b - 0.8f, y), W(fx, b - 0.8f, y + 1.8f), W(fx, a + 0.8f, y + 1.8f), c);
                else m.Quad(W(a + 0.8f, fx, y), W(b - 0.8f, fx, y), W(b - 0.8f, fx, y + 1.8f), W(a + 0.8f, fx, y + 1.8f), c);
            }
            pos += len;
        }
    }

    private static Color ArtLighter(Color c) => Color.Lerp(c, Color.White, 0.15f);

    private static Color BuildingColour(int type, int seed)
    {
        float n = Hash(seed);
        return type switch
        {
            1 => Color.Lerp(new Color(150, 80, 60), new Color(180, 110, 80), n),     // brick
            2 => Color.Lerp(new Color(196, 184, 160), new Color(220, 210, 186), n),  // stone
            3 => Color.Lerp(new Color(140, 144, 150), new Color(170, 172, 176), n),  // concrete
            _ => Color.Lerp(new Color(90, 110, 130), new Color(120, 140, 160), n),   // glass tower
        };
    }
}
