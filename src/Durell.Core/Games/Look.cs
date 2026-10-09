using System;
using Durell.Graphics;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// A game's ENHANCED look: which of the original's flat background colours become painted
/// scenery, the palette for everything else, and the scenery itself.
/// </summary>
internal class Look
{
    /// <summary>Richer versions of the Oric's eight colours (ABGR).</summary>
    protected static readonly uint[] Rich =
    {
        Abgr(8, 10, 18), Abgr(236, 64, 58), Abgr(70, 206, 96), Abgr(250, 214, 72),
        Abgr(54, 102, 230), Abgr(212, 78, 206), Abgr(76, 214, 236), Abgr(244, 244, 238),
    };

    protected uint[] InkPalette = (uint[])Rich.Clone();
    protected uint[] PaperPalette = (uint[])Rich.Clone();

    public float Bevel { get; protected set; } = 0.6f;
    public float Shadow { get; protected set; } = 1f;
    public float GlowStrength { get; protected set; } = 0.55f;

    public static uint Abgr(int r, int g, int b, int a = 255) => (uint)(a << 24 | b << 16 | g << 8 | r);
    public static Color C(int r, int g, int b) => new(r, g, b);

    /// <summary>Is this pixel of the original background scenery (painted by <see cref="DrawBackdrop"/>)?</summary>
    public virtual bool IsBackdrop(byte v, int x, int y, byte[] index) => false;

    public virtual uint Colour(byte v, int x, int y) => (v & 8) != 0 ? InkPalette[v & 7] : PaperPalette[v & 7];

    /// <summary>How much this pixel glows (0 = not at all).</summary>
    public virtual float Emission(byte v, int x, int y)
    {
        if ((v & 8) == 0) return 0;
        return (v & 7) switch { 1 => 0.8f, 3 => 0.7f, 5 => 0.7f, 6 => 0.5f, 7 => 0.35f, 2 => 0.45f, _ => 0.15f };
    }

    public virtual void DrawBackdrop(Gfx g, RectangleF r, float t, byte[] index, float alpha)
    {
        g.Rect(r, Color.Black * alpha);
    }

    public virtual void DrawOverlay(Gfx g, RectangleF r, float t, byte[] index, float alpha) { }

    // ---------------------------------------------------------------- scene mode (realistic redraw)

    /// <summary>The game's last two scenes, read after every frame.</summary>
    public SceneTrack Track { get; } = new();

    /// <summary>The game is showing something this look redraws as artwork.</summary>
    public bool SceneActive => Track.Current.Active;

    /// <summary>
    /// The look's own sound for this frame, after the program has rendered the Oric's into
    /// <paramref name="stereo"/>; returns true if it replaced it (Harrier Attack 3D's new effects).
    /// </summary>
    public virtual bool MixAudio(GameProgram program, float[] stereo, bool enhanced) => false;

    /// <summary>Called once when the look is paired with a running program (e.g. to hook its probes).</summary>
    public virtual void Attach(GameProgram program) { }

    /// <summary>Called after every game frame: reads the scene from the game's memory.</summary>
    public void AfterFrame(GameProgram program)
    {
        var s = Track.Next();
        s.Active = ReadScene(program, s);
    }

    /// <summary>Reads this frame's scene; returns false when the look doesn't redraw this screen.</summary>
    protected virtual bool ReadScene(GameProgram program, Scene scene) => false;

    /// <summary>Does text row <paramref name="row"/> of the TEXT screen ($BB80) contain <paramref name="text"/>?</summary>
    protected static bool TextRowHas(byte[] m, int row, string text)
    {
        for (int c = 0; c + text.Length <= 40; c++)
        {
            int k = 0;
            while (k < text.Length && (m[0xBB80 + row * 40 + c + k] & 0x7F) == text[k]) k++;
            if (k == text.Length) return true;
        }
        return false;
    }

    /// <summary>Draws the playfield as artwork (the HUD rows are drawn from the original picture on top).</summary>
    public virtual void DrawScene(Gfx g, RectangleF r, float t, byte[] index, float alpha) { }

    /// <summary>
    /// When set, only this part of the 240 x 224 picture is redrawn by <see cref="DrawScene"/>; the rest
    /// is the original picture in the ENHANCED style (e.g. Lunar Lander's gauge panel, Turbo Esprit's dashboard).
    /// </summary>
    public virtual Rectangle? SceneArea => null;

    /// <summary>Rows of the 240 x 224 picture kept from the original when the scene is redrawn (score, gauges, text).</summary>
    public virtual (int Y0, int Y1)[] HudRows => Array.Empty<(int, int)>();

    // ---------------------------------------------------------------- helpers for scenery

    /// <summary>A rectangle of the Oric picture (pixels) inside the drawn picture.</summary>
    protected static RectangleF Area(RectangleF r, float x, float y, float w, float h)
    {
        float sx = r.Width / 240f, sy = r.Height / 224f;
        return new RectangleF(r.X + x * sx, r.Y + y * sy, w * sx, h * sy);
    }

    protected static Vector2 At(RectangleF r, float x, float y) => new(r.X + x * r.Width / 240f, r.Y + y * r.Height / 224f);

    /// <summary>Deterministic pseudo-random numbers for scenery layouts.</summary>
    protected static float Hash(int n)
    {
        uint h = (uint)n * 2654435761u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    /// <summary>A field of twinkling stars over an area.</summary>
    protected static void Stars(Gfx g, RectangleF area, float t, int count, int seed, float speedX, float speedY, float size, float alpha)
    {
        for (int i = 0; i < count; i++)
        {
            float fx = Hash(seed + i * 3), fy = Hash(seed + i * 3 + 1), tw = Hash(seed + i * 3 + 2);
            float x = (fx + t * speedX) % 1f, y = (fy + t * speedY) % 1f;
            if (x < 0) x += 1;
            if (y < 0) y += 1;
            float a = alpha * (0.45f + 0.55f * (0.5f + 0.5f * MathF.Sin(t * (1.5f + tw * 3) + tw * 20)));
            var p = new Vector2(area.X + x * area.Width, area.Y + y * area.Height);
            var col = Color.Lerp(new Color(170, 200, 255), new Color(255, 236, 210), tw);
            g.GlowAt(p, size * (1.5f + tw * 2.5f), col * (a * 0.35f));
            g.Rect(p.X - size * 0.35f, p.Y - size * 0.35f, size * 0.7f, size * 0.7f, col * a);
        }
    }
}
