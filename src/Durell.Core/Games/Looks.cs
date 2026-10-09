using System;
using Durell.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>The scenery textures, set when the graphics device is ready.</summary>
internal static class Kit
{
    public static Scenery Scenery = null!;
}

/// <summary>Harrier Attack: a real sky with drifting clouds over a sparkling sea.</summary>
internal partial class HarrierLook : Look
{
    private int _seaTop = 186;

    public HarrierLook()
    {
        InkPalette[0] = Abgr(30, 34, 44);      // the carrier, buildings and ships: dark steel
        InkPalette[4] = Abgr(50, 80, 200);
    }

    public override bool IsBackdrop(byte v, int x, int y, byte[] index)
    {
        if ((v & 8) != 0 || y < 8 || y >= 200) return false;
        int c = v & 7;
        return c == 6 || c == 4;
    }

    public override void DrawBackdrop(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        // where does the sea start? (the lowest run of blue-paper rows)
        int sea = 200;
        for (int y = 199; y > 120; y--)
        {
            byte v = index[y * 240 + 4];
            if ((v & 8) == 0 && (v & 7) == 4) sea = y;
            else if (sea < 200) break;
        }
        _seaTop = sea;
        var sky = Area(r, 0, 8, 240, sea - 8);
        g.Gradient(sky, new Color(26, 72, 150) * alpha, new Color(172, 214, 240) * alpha);
        // sun glow
        g.Begin(Gfx.AdditiveBlend, SamplerState.LinearClamp);
        g.GlowAt(At(r, 196, 40), r.Width * 0.22f, new Color(255, 230, 170) * (0.35f * alpha));
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        // clouds: three layers drifting at different speeds
        var cloud = Kit.Scenery.Cloud;
        for (int layer = 0; layer < 3; layer++)
        {
            float speed = 4 + layer * 5, scale = 0.6f + layer * 0.25f;
            for (int i = 0; i < 4; i++)
            {
                float fx = Hash(i * 7 + layer * 31);
                float x = (fx * 300 - t * speed) % 320;
                if (x < -60) x += 320;
                float y = 18 + Hash(i * 13 + layer) * (sea - 80) * 0.6f + layer * 14;
                var area = Area(r, x - 40, y, 90 * scale, 40 * scale);
                if (area.X > r.Right || area.Right < r.X) continue;
                g.Texture(cloud, area, Color.White * (alpha * (0.55f + layer * 0.15f)));
            }
        }
        if (sea < 200)
        {
            var water = Area(r, 0, sea, 240, 200 - sea);
            g.Gradient(water, new Color(40, 110, 190) * alpha, new Color(10, 40, 90) * alpha, 16);
            g.Begin(Gfx.AdditiveBlend, SamplerState.LinearClamp);
            for (int i = 0; i < 40; i++)
            {
                float x = (Hash(i) * 240 + t * (6 + Hash(i + 5) * 6)) % 240;
                float y = sea + 1 + Hash(i + 9) * (200 - sea - 2);
                float a = 0.5f + 0.5f * MathF.Sin(t * 3 + i);
                g.Rect(Area(r, x, y, 3 + Hash(i + 3) * 5, 0.6f), new Color(200, 230, 255) * (a * 0.35f * alpha));
            }
            g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        }
    }
}

/// <summary>Scuba Dive: deep water with light rays from the surface and rising bubbles.</summary>
internal sealed partial class ScubaLook : Look
{
    public ScubaLook()
    {
        PaperPalette[0] = Abgr(0, 0, 0);
        InkPalette[2] = Abgr(60, 190, 110);    // weed and seabed
        InkPalette[4] = Abgr(150, 196, 255);   // the diver and the blue creatures: light, to show against the water
    }

    public override bool IsBackdrop(byte v, int x, int y, byte[] index) =>
        (v & 8) == 0 && (v & 7) == 0 && y >= 46 && y < 212;

    public override void DrawBackdrop(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        g.Rect(r, Color.Black * alpha);
        var water = Area(r, 0, 46, 240, 166);
        g.Gradient(water, new Color(18, 120, 160) * alpha, new Color(4, 18, 46) * alpha);
        g.Begin(Gfx.AdditiveBlend, SamplerState.LinearClamp);
        var ray = Kit.Scenery.Ray;
        for (int i = 0; i < 6; i++)
        {
            float x = 20 + i * 40 + MathF.Sin(t * 0.3f + i * 1.7f) * 12;
            float a = 0.13f + 0.07f * MathF.Sin(t * 0.7f + i * 2.3f);
            var top = At(r, x, 46);
            float w = r.Width * (0.06f + Hash(i) * 0.05f), h = r.Height * 0.62f;
            g.Batch.Draw(ray, top, null, new Color(170, 230, 255) * (a * alpha), 0.18f + 0.05f * MathF.Sin(t * 0.2f + i),
                new Vector2(16, 0), new Vector2(w / 32, h / 256), SpriteEffects.None, 0);
        }
        // shimmer on the surface
        for (int i = 0; i < 18; i++)
        {
            float x = (Hash(i + 40) * 240 + t * 8) % 240;
            g.Rect(Area(r, x, 46.5f + Hash(i) * 3, 6 + Hash(i + 2) * 8, 0.7f), new Color(180, 240, 255) * (0.25f * alpha));
        }
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        var bubble = Kit.Scenery.Bubble;
        for (int i = 0; i < 26; i++)
        {
            float speed = 10 + Hash(i * 5) * 16;
            float y = 210 - ((t * speed + Hash(i) * 170) % 165);
            float x = Hash(i * 3 + 1) * 240 + MathF.Sin(t * 2 + i) * 2;
            float s = 1.5f + Hash(i * 7) * 2.5f;
            float sh = s * (r.Width / 240f) / (r.Height / 224f);      // round on screen
            g.Texture(bubble, Area(r, x, y, s, sh), new Color(200, 240, 255) * (0.45f * alpha));
        }
    }
}

/// <summary>Star Fighter: the view screen opens onto deep space.</summary>
internal sealed partial class StarFighterLook : Look
{
    public StarFighterLook()
    {
        InkPalette[4] = Abgr(220, 236, 255);    // the original's blue dots are stars
    }

    private static bool InView(int x, int y) => x >= 24 && x < 216 && y >= 14 && y < 168;

    public override bool IsBackdrop(byte v, int x, int y, byte[] index) =>
        (v & 8) == 0 && (v & 7) == 7 && InView(x, y);

    public override uint Colour(byte v, int x, int y)
    {
        if (InView(x, y) && (v & 8) != 0 && (v & 7) == 4) return Abgr(225, 238, 255);
        return base.Colour(v, x, y);
    }

    public override float Emission(byte v, int x, int y) =>
        InView(x, y) && (v & 8) != 0 ? 1.2f : base.Emission(v, x, y) * 0.6f;

    public override void DrawBackdrop(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        g.Rect(r, Color.Black * alpha);
        var view = Area(r, 24, 14, 192, 154);
        g.Gradient(view, new Color(6, 8, 30) * alpha, new Color(2, 2, 10) * alpha, 24);
        g.Begin(Gfx.AdditiveBlend, SamplerState.LinearClamp);
        g.Texture(Kit.Scenery.Nebula, new RectangleF(view.X + view.Width * 0.15f, view.Y + view.Height * 0.05f, view.Width * 0.8f, view.Height * 0.95f),
            Color.White * (0.55f * alpha));
        Stars(g, view, t, 45, 900, 0.004f, 0, r.Width / 400f, 0.5f * alpha);
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
    }
}

/// <summary>Galaxy: the invaders swoop across a starfield and a nebula.</summary>
internal partial class GalaxyLook : Look
{
    public override bool IsBackdrop(byte v, int x, int y, byte[] index) =>
        (v & 8) == 0 && (v & 7) == 0 && y >= 8 && y < 216;

    public override void DrawBackdrop(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        g.Rect(r, Color.Black * alpha);
        var field = Area(r, 0, 8, 240, 208);
        g.Gradient(field, new Color(10, 6, 34) * alpha, new Color(2, 4, 14) * alpha, 24);
        g.Begin(Gfx.AdditiveBlend, SamplerState.LinearClamp);
        float drift = (t * 0.01f) % 1f;
        g.Texture(Kit.Scenery.Nebula, Area(r, -30 + drift * 20, 20, 200, 170), new Color(255, 140, 220) * (0.9f * alpha));
        g.Texture(Kit.Scenery.Nebula, Area(r, 110 - drift * 20, 60, 160, 150), new Color(120, 200, 255) * (0.75f * alpha));
        Stars(g, field, t, 60, 300, 0, 0.012f, r.Width / 380f, 0.8f * alpha);
        Stars(g, field, t, 30, 700, 0, 0.03f, r.Width / 280f, 1f * alpha);
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
    }
}

/// <summary>Lunar Lander: the window looks out into black space with the Earth in the sky.</summary>
internal sealed partial class LunarLook : Look
{
    public LunarLook()
    {
        InkPalette[7] = Abgr(236, 236, 230);
    }

    public override bool IsBackdrop(byte v, int x, int y, byte[] index)
    {
        if ((v & 8) != 0 || y < 8 || y >= 200 || x >= 174) return false;
        int c = v & 7;
        return c == 4 || (c == 0 && x < 102);
    }

    public override float Emission(byte v, int x, int y) =>
        (v & 8) != 0 && (v & 7) == 7 && y < 180 && x < 102 ? 1.0f : base.Emission(v, x, y);

    public override void DrawBackdrop(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        g.Rect(r, Color.Black * alpha);
        var space = Area(r, 0, 8, 174, 192);
        g.Gradient(space, new Color(4, 6, 22) * alpha, new Color(16, 14, 30) * alpha, 24);
        g.Begin(Gfx.AdditiveBlend, SamplerState.LinearClamp);
        Stars(g, space, t, 40, 1234, 0, 0, r.Width / 420f, 0.35f * alpha);
        g.GlowAt(At(r, 70, 40), r.Width * 0.08f, new Color(80, 140, 255) * (0.35f * alpha));
        g.Begin(BlendState.AlphaBlend, SamplerState.LinearClamp);
        g.Texture(Kit.Scenery.Planet, Area(r, 58, 30, 24, 24 * 224f / 240f * 1.07f), Color.White * alpha);
    }
}

/// <summary>Turbo Esprit: a sky with clouds above the city.</summary>
internal sealed partial class TurboLook : Look
{
    public TurboLook()
    {
        PaperPalette[7] = Abgr(232, 230, 222);  // buildings
        PaperPalette[6] = Abgr(84, 196, 214);
        Bevel = 0.35f;
        GlowStrength = 0.3f;
    }

    public override bool IsBackdrop(byte v, int x, int y, byte[] index) =>
        (v & 8) == 0 && (v & 7) == 4 && y >= 4 && y < 100 && x >= 6;

    public override void DrawBackdrop(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        g.Rect(r, Color.Black * alpha);
        var sky = Area(r, 6, 4, 234, 96);
        g.Gradient(sky, new Color(40, 90, 190) * alpha, new Color(150, 196, 236) * alpha, 24);
        var cloud = Kit.Scenery.Cloud;
        for (int i = 0; i < 5; i++)
        {
            float x = (Hash(i * 11) * 280 - t * 3) % 280;
            if (x < -60) x += 280;
            float y = 8 + Hash(i * 5 + 1) * 30;
            g.Texture(cloud, Area(r, x - 20, y, 60 + Hash(i) * 40, 22), Color.White * (0.7f * alpha));
        }
    }
}
