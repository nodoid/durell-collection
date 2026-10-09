using System;
using Durell.Games;
using Durell.Graphics;
using Durell.Screens;
using Microsoft.Xna.Framework;

namespace Durell.Capture;

/// <summary>
/// The app icon, drawn by the app's own renderer: Galaxy's invaders in formation (the game's own
/// pixels, in the enhanced look) above the DURELL name in gold, against deep space.
/// </summary>
internal sealed class IconScreen : Screen
{
    private readonly byte[] _galaxy;
    private readonly Durell.Games.Look _look = new Sky();

    /// <summary>Galaxy's look with no scenery: the invaders float on the icon's own sky.</summary>
    private sealed class Sky : GalaxyLook
    {
        public override void DrawBackdrop(Gfx g, RectangleF r, float t, byte[] index, float alpha) { }
    }

    public IconScreen(DurellGame game) : base(game)
    {
        _galaxy = (byte[])Catalog.Get("galaxy").Demo().Index.Clone();
    }

    public override void Update(float dt) { }

    public override void Draw(Gfx g)
    {
        // the square canvas in virtual units (the capture is square; Gfx keeps 360 units of height
        // in the middle, so the canvas reaches above and below that)
        float size = g.Width;
        float top = (Gfx.Height - size) / 2;
        var canvas = new RectangleF(0, top, size, size);
        g.Begin();
        g.Gradient(canvas, new Color(10, 14, 52), new Color(44, 12, 74), 64);
        g.Additive();
        g.GlowAt(new Vector2(size * 0.5f, top + size * 0.62f), size * 0.55f, new Color(255, 170, 60) * 0.18f);
        g.GlowAt(new Vector2(size * 0.5f, top + size * 0.22f), size * 0.5f, new Color(90, 120, 255) * 0.22f);
        var rng = new Random(11);
        for (int n = 0; n < 70; n++)
        {
            var p = new Vector2((float)rng.NextDouble() * size, top + (float)rng.NextDouble() * size);
            float s = 0.6f + (float)rng.NextDouble() * 1.2f;
            g.GlowAt(p, s * 3, Color.White * 0.25f);
            g.Rect(p.X - s / 2, p.Y - s / 2, s, s, Color.White * (0.5f + (float)rng.NextDouble() * 0.5f));
        }
        g.Alpha();

        // the invaders: Galaxy's formation (Oric x 56..196, y 14..44), large
        // the formation's bounding box in the Oric picture (ink pixels below the score line)
        int x0 = 240, x1 = 0, y0 = 224, y1 = 0;
        for (int y = 10; y < 80; y++)
            for (int x = 0; x < 240; x++)
                if ((_galaxy[y * 240 + x] & 8) != 0)
                {
                    x0 = Math.Min(x0, x); x1 = Math.Max(x1, x);
                    y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                }
        x0 -= 4; x1 += 4; y0 -= 3; y1 += 3;
        float k = size * 0.86f / (x1 - x0);          // Oric pixels -> canvas units
        var band = new RectangleF(size * 0.07f, top + size * 0.08f, (x1 - x0) * k, (y1 - y0) * k * 1.07f);
        var pic = new RectangleF(band.X - x0 * k, band.Y - y0 * k * 1.07f, 240 * k, 224 * k * 1.07f);
        g.PushClip(band);
        Game.Renderer.Draw(g, _galaxy, pic, _look, true, false);
        g.PopClip();
        g.Begin();

        // the name
        float ts = size / 46f;
        g.Additive();
        g.GlowAt(new Vector2(size / 2, top + size * 0.50f + 4 * ts), size * 0.42f, new Color(255, 190, 60) * 0.35f);
        g.Alpha();
        g.OutlinedText("DURELL", size / 2, top + size * 0.50f, new Color(255, 208, 80), new Color(60, 24, 0), ts, 0.7f);
        g.OutlinedText("COLLECTION", size / 2, top + size * 0.50f + 8 * ts + size * 0.04f, Color.White, new Color(10, 10, 40), ts * 0.52f, 0.9f);
        g.End();
    }
}
