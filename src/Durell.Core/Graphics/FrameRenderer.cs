using System;
using Durell.Games;
using Durell.Machine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Graphics;

/// <summary>
/// Shows a game's 240 x 224 Oric picture in one of two looks.
/// <para>ORIGINAL: the Oric's eight colours and fat pixels, exactly as the video chip made them
/// (scaled up crisply, optionally with scanlines).</para>
/// <para>ENHANCED: the game's <see cref="Look"/> paints new scenery behind the action (skies, seas,
/// space) wherever the original showed a flat background colour; everything else is redrawn at three
/// times the resolution with edge smoothing (Scale3x), the look's palette, bevel lighting, a soft
/// drop shadow and a glow around bright colours.</para>
/// </summary>
internal sealed class FrameRenderer
{
    private const int W = OricVideo.Width, H = OricVideo.Height;
    private const int S = 3, BW = W * S, BH = H * S;
    private const int GW = W / 4, GH = H / 4;

    private readonly GraphicsDevice _device;
    private readonly Texture2D _texOriginal, _texEnhanced, _texGlow, _texScan;
    private readonly uint[] _src = new uint[W * H];
    private readonly uint[] _big = new uint[BW * BH];
    private readonly uint[] _flat = new uint[BW * BH];
    private readonly uint[] _glow = new uint[GW * GH];
    private readonly bool[] _backdrop = new bool[W * H];

    public FrameRenderer(GraphicsDevice device)
    {
        _device = device;
        _texOriginal = new Texture2D(device, BW, BH);
        _texEnhanced = new Texture2D(device, BW, BH);
        _texGlow = new Texture2D(device, GW, GH);
        _texScan = new Texture2D(device, 1, 4);
        _texScan.SetData(new[] { new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), new Color(0, 0, 0, 70), new Color(0, 0, 0, 110) });
    }

    /// <summary>Where a 240 x 224 picture goes in <paramref name="area"/>: as large as fits, at a 4:3 shape.</summary>
    public static RectangleF Fit(RectangleF area, float aspect = 4f / 3f)
    {
        float w = area.Width, h = area.Height;
        if (w / h > aspect) w = h * aspect;
        else h = w / aspect;
        return new RectangleF(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
    }

    public void Draw(Gfx g, byte[] index, RectangleF dest, Look look, bool enhanced, bool scanlines, float alpha = 1f)
    {
        if (enhanced) DrawEnhanced(g, index, dest, look, alpha);
        else DrawOriginal(g, index, dest, scanlines, alpha);
    }

    // ------------------------------------------------------------------ ORIGINAL

    private void DrawOriginal(Gfx g, byte[] index, RectangleF dest, bool scanlines, float alpha)
    {
        // 3x nearest-neighbour, then drawn with linear filtering: hard pixels of even size at any scale
        var pal = OricVideo.Palette;
        for (int y = 0; y < H; y++)
        {
            int si = y * W;
            int row = y * S * BW;
            for (int x = 0; x < W; x++)
            {
                uint c = pal[index[si + x] & 7];
                int o = row + x * S;
                _big[o] = c; _big[o + 1] = c; _big[o + 2] = c;
            }
            Array.Copy(_big, row, _big, row + BW, BW);
            Array.Copy(_big, row, _big, row + 2 * BW, BW);
        }
        g.End();
        _device.Textures[0] = null;
        _texOriginal.SetData(_big);
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        g.Texture(_texOriginal, dest, Color.White * alpha);
        if (scanlines)
        {
            g.Begin(BlendState.AlphaBlend, SamplerState.PointWrap);
            g.Batch.Draw(_texScan, new Vector2(dest.X, dest.Y), new Rectangle(0, 0, 1, H * 4), Color.White * alpha, 0,
                Vector2.Zero, new Vector2(dest.Width, dest.Height / (H * 4)), SpriteEffects.None, 0);
        }
        g.Begin();
    }

    // ------------------------------------------------------------------ ENHANCED

    private void DrawEnhanced(Gfx g, byte[] index, RectangleF dest, Look look, float alpha)
    {
        // 1. classify and colour each Oric pixel
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                byte v = index[i];
                bool bd = look.IsBackdrop(v, x, y, index);
                _backdrop[i] = bd;
                _src[i] = bd ? 0u : look.Colour(v, x, y);
            }
        // 2. Scale3x (AdvMAME3x) on the colours
        Scale3x();
        // 3. bevel: lighter top edges, darker bottom edges
        Bevel(look.Bevel);
        // 4. glow map (a quarter of the resolution, filtered up when drawn = a soft halo)
        for (int gy = 0; gy < GH; gy++)
            for (int gx = 0; gx < GW; gx++)
            {
                float r = 0, gg = 0, b = 0;
                for (int dy = 0; dy < 4; dy++)
                    for (int dx = 0; dx < 4; dx++)
                    {
                        int i = (gy * 4 + dy) * W + gx * 4 + dx;
                        if (_backdrop[i]) continue;
                        float e = look.Emission(index[i], gx * 4 + dx, gy * 4 + dy);
                        if (e <= 0) continue;
                        uint c = _src[i];
                        r += (c & 0xFF) * e;
                        gg += ((c >> 8) & 0xFF) * e;
                        b += ((c >> 16) & 0xFF) * e;
                    }
                r = MathF.Min(255, r / 16 * 2.2f);
                gg = MathF.Min(255, gg / 16 * 2.2f);
                b = MathF.Min(255, b / 16 * 2.2f);
                _glow[gy * GW + gx] = 0xFF000000u | ((uint)b << 16) | ((uint)gg << 8) | (uint)r;
            }

        g.End();
        _device.Textures[0] = null;
        _texEnhanced.SetData(_big);
        _texGlow.SetData(_glow);

        g.PushClip(dest);
        if (look.SceneActive && look.SceneArea is Rectangle area)
        {
            // the original (enhanced) everywhere, then the redrawn part over its area
            DrawComposite(g, dest, look, index, alpha);
            float kx = dest.Width / W, ky = dest.Height / H;
            var part = new RectangleF(dest.X + area.X * kx, dest.Y + area.Y * ky, area.Width * kx, area.Height * ky);
            g.PushClip(part);
            g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
            look.DrawScene(g, dest, g.Time, index, alpha);
            g.PopClip();
            g.PopClip();
            g.Begin();
            return;
        }
        if (look.SceneActive)
        {
            // realistic redraw of the playfield, with the original's HUD rows over it
            g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
            look.DrawScene(g, dest, g.Time, index, alpha);
            g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
            float sy = dest.Height / H;
            foreach (var (y0, y1) in look.HudRows)
            {
                var band = new RectangleF(dest.X, dest.Y + y0 * sy, dest.Width, (y1 - y0) * sy);
                g.Rect(band, new Color(6, 8, 14) * (0.92f * alpha));
                g.Texture(_texEnhanced, band, Color.White * alpha, new Rectangle(0, y0 * S, BW, (y1 - y0) * S));
                g.Begin(Gfx.AdditiveBlend, SamplerState.LinearClamp);
                g.Texture(_texGlow, band, Color.White * (look.GlowStrength * alpha), new Rectangle(0, y0 / 4, GW, Math.Max(1, (y1 - y0) / 4)));
                g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
            }
            g.PopClip();
            g.Begin();
            return;
        }
        DrawComposite(g, dest, look, index, alpha);
        g.PopClip();
        g.Begin();
    }

    /// <summary>The enhanced picture: backdrop, shadow, picture, glow, effects (inside the picture).</summary>
    private void DrawComposite(Gfx g, RectangleF dest, Look look, byte[] index, float alpha)
    {
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        look.DrawBackdrop(g, dest, g.Time, index, alpha);
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        float px = dest.Width / W;
        g.Texture(_texEnhanced, dest.Offset(px * 0.9f, px * 1.1f), Color.Black * (0.45f * alpha * look.Shadow));
        g.Texture(_texEnhanced, dest, Color.White * alpha);
        g.Begin(Gfx.AdditiveBlend, SamplerState.LinearClamp);
        g.Texture(_texGlow, dest.Inflate(px * 2), Color.White * (look.GlowStrength * alpha));
        look.DrawOverlay(g, dest, g.Time, index, alpha);
    }

    private void Scale3x()
    {
        var s = _src;
        var d = _big;
        for (int y = 0; y < H; y++)
        {
            int ym = y > 0 ? y - 1 : 0, yp = y < H - 1 ? y + 1 : H - 1;
            for (int x = 0; x < W; x++)
            {
                int xm = x > 0 ? x - 1 : 0, xp = x < W - 1 ? x + 1 : W - 1;
                uint a = s[ym * W + xm], b = s[ym * W + x], c = s[ym * W + xp];
                uint dd = s[y * W + xm], e = s[y * W + x], f = s[y * W + xp];
                uint gg = s[yp * W + xm], h = s[yp * W + x], i = s[yp * W + xp];
                uint e0 = e, e1 = e, e2 = e, e3 = e, e5 = e, e6 = e, e7 = e, e8 = e;
                if (b != h && dd != f)
                {
                    e0 = dd == b ? dd : e;
                    e1 = (dd == b && e != c) || (b == f && e != a) ? b : e;
                    e2 = b == f ? f : e;
                    e3 = (dd == b && e != gg) || (dd == h && e != a) ? dd : e;
                    e5 = (b == f && e != i) || (h == f && e != c) ? f : e;
                    e6 = dd == h ? dd : e;
                    e7 = (dd == h && e != i) || (h == f && e != gg) ? h : e;
                    e8 = h == f ? f : e;
                }
                int o = y * S * BW + x * S;
                d[o] = e0; d[o + 1] = e1; d[o + 2] = e2;
                d[o + BW] = e3; d[o + BW + 1] = e; d[o + BW + 2] = e5;
                d[o + 2 * BW] = e6; d[o + 2 * BW + 1] = e7; d[o + 2 * BW + 2] = e8;
            }
        }
    }

    private void Bevel(float amount)
    {
        if (amount <= 0) return;
        Array.Copy(_big, _flat, _big.Length);
        var d = _big;
        var f = _flat;
        int up = (int)(amount * 60), down = (int)(amount * 70);
        for (int y = 1; y < BH - 1; y++)
        {
            int row = y * BW;
            for (int x = 0; x < BW; x++)
            {
                uint c = f[row + x];
                if (c == 0) continue;
                if (f[row - BW + x] != c) d[row + x] = Shade(c, up);
                else if (f[row + BW + x] != c) d[row + x] = Shade(c, -down);
            }
        }
    }

    private static uint Shade(uint c, int k)
    {
        int r = (int)(c & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)((c >> 16) & 0xFF);
        if (k > 0)
        {
            r += (255 - r) * k / 160; g += (255 - g) * k / 160; b += (255 - b) * k / 160;
        }
        else
        {
            r = r * (160 + k) / 160; g = g * (160 + k) / 160; b = b * (160 + k) / 160;
        }
        return (c & 0xFF000000u) | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
    }
}
