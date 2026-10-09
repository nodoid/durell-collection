using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Graphics;

/// <summary>Small textures drawn in code at start-up.</summary>
public static class Textures
{
    /// <summary>A white rounded square (radius 32 of 128) for nine-slice panels.</summary>
    public static Texture2D RoundedRect(GraphicsDevice device)
    {
        const int n = 128, r = 32;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Math.Clamp(x + 0.5f, r, n - r), cy = Math.Clamp(y + 0.5f, r, n - r);
                float d = MathF.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = Math.Clamp(r - d + 0.5f, 0, 1);
                px[y * n + x] = Color.White * a;
            }
        var t = new Texture2D(device, n, n);
        t.SetData(px);
        return t;
    }

    /// <summary>A soft round glow (white, alpha falling off smoothly).</summary>
    public static Texture2D Glow(GraphicsDevice device)
    {
        const int n = 128;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                float a = MathF.Max(0, 1 - d);
                a = a * a * (3 - 2 * a);
                px[y * n + x] = Color.White * (a * a);
            }
        var t = new Texture2D(device, n, n);
        t.SetData(px);
        return t;
    }
}
