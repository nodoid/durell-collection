using System;
using Durell.Graphics;
using Microsoft.Xna.Framework;

namespace Durell.Screens;

/// <summary>Shared look of the menus: buttons, backgrounds, labels.</summary>
internal static class Ui
{
    public static readonly Color Gold = new(255, 206, 84);
    public static readonly Color Ink = new(232, 236, 248);
    public static readonly Color Dim = new(150, 160, 190);
    public static readonly Color Panel = new(14, 16, 32);

    /// <summary>A rounded button; returns true when it was tapped/clicked this frame.</summary>
    public static bool Button(Gfx g, DurellGame game, RectangleF r, string label, bool focused, Color accent, float scale = 1.4f, bool enabled = true)
    {
        bool hover = game.Input.Hover(r);
        bool held = game.Input.Held(r);
        float k = focused || hover ? 1f : 0.55f;
        var rim = Color.Lerp(new Color(60, 70, 100), accent, k);
        g.RoundRect(r.Inflate(1.5f), 9, rim * (enabled ? 1 : 0.4f));
        g.RoundRect(r, 8, Color.Lerp(Panel, accent, held ? 0.45f : focused ? 0.22f : 0.08f));
        g.RoundRect(new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, r.Height * 0.42f), 6, Color.White * 0.06f);
        float ty = r.Y + (r.Height - 8 * scale) / 2;
        g.TextCentred(label, r.Center.X, ty, (enabled ? (focused ? Color.White : Ink) : Dim), scale);
        if (focused)
        {
            g.Additive();
            g.GlowAt(r.Center, r.Width * 0.55f, accent * 0.12f);
            g.Alpha();
        }
        return enabled && game.Input.Tapped(r);
    }

    /// <summary>The menu backdrop: deep blue gradient, drifting stars, a coloured glow.</summary>
    public static void Backdrop(Gfx g, float t, Color tint)
    {
        var r = new RectangleF(0, 0, g.Width, Gfx.Height);
        g.Gradient(r, new Color(16, 18, 40), new Color(3, 3, 10));
        g.Additive();
        g.GlowAt(new Vector2(g.Width * 0.5f, Gfx.Height * 0.38f), g.Width * 0.6f, tint * 0.10f);
        for (int i = 0; i < 70; i++)
        {
            float fx = H(i * 3), fy = H(i * 3 + 1), tw = H(i * 3 + 2);
            float x = (fx * g.Width + t * (2 + tw * 6)) % g.Width;
            float y = fy * Gfx.Height;
            float a = 0.25f + 0.35f * (0.5f + 0.5f * MathF.Sin(t * (1 + tw * 2) + i));
            g.Rect(x, y, 0.9f + tw, 0.9f + tw, Color.White * a);
        }
        g.Alpha();
    }

    private static float H(int n)
    {
        uint h = (uint)n * 2654435761u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }
}
