using System;
using Microsoft.Xna.Framework;

namespace Durell.Graphics.Art;

/// <summary>Painting helpers shared by the games' artwork: lit surfaces, shadows, colour maths.</summary>
internal static class ArtKit
{
    /// <summary>Light comes from the top left, as in most of the art.</summary>
    public static readonly Vector2 Light = Vector2.Normalize(new Vector2(-0.45f, -1f));

    public static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Math.Clamp(t, 0, 1));
    public static Color Lighter(Color c, float t) => Mix(c, Color.White, t);
    public static Color Darker(Color c, float t) => Mix(c, Color.Black, t);
    public static Color Alpha(Color c, float a) => new(c.R, c.G, c.B, (byte)(Math.Clamp(a, 0, 1) * 255));

    /// <summary>
    /// A rounded, lit body: a gradient across the shape from lit to shaded, a soft specular band near
    /// the lit edge and a darker core shadow, then a thin dark outline.
    /// </summary>
    public static void Body(Canvas c, Path p, Color baseColour, float top, float bottom, float outline = 1.2f, float shine = 0.55f)
    {
        float h = bottom - top;
        c.Fill(p, Brush.Linear(new Vector2(0, top), new Vector2(0, bottom),
            (0f, Lighter(baseColour, 0.35f)),
            (0.18f, Lighter(baseColour, 0.12f + shine * 0.25f)),
            (0.32f, baseColour),
            (0.7f, Darker(baseColour, 0.28f)),
            (1f, Darker(baseColour, 0.55f))));
        if (outline > 0) c.Stroke(p, outline, Darker(baseColour, 0.75f), 0.8f);
    }

    /// <summary>A soft drop shadow of everything drawn so far (draw first on a separate canvas, then compose).</summary>
    public static Canvas WithShadow(Canvas art, float dx, float dy, int blur, float opacity)
    {
        var shadow = art.Silhouette(Color.Black);
        shadow.Blur(blur);
        var outC = new Canvas(art.Width, art.Height);
        outC.Draw(shadow, dx, dy, opacity);
        outC.Draw(art, 0, 0);
        return outC;
    }

    /// <summary>Glass: a dark tinted dome with a bright reflection streak.</summary>
    public static void Glass(Canvas c, Path p, float x0, float y0, float x1, float y1, Color tint)
    {
        c.Fill(p, Brush.Linear(new Vector2(x0, y0), new Vector2(x1, y1),
            (0f, Lighter(tint, 0.65f)), (0.35f, tint), (1f, Darker(tint, 0.6f))));
        c.Stroke(p, 1f, Darker(tint, 0.7f), 0.9f);
    }

    /// <summary>Fine surface grain (paint, metal, rock) over what is drawn.</summary>
    public static void Grain(Canvas c, float amount, float scale, int seed)
    {
        c.Tint(Brush.Func((x, y) =>
        {
            float n = Noise.Fbm(x / scale, y / scale, seed, 3);
            return n > 0.5f ? new Vector4(1, 1, 1, (n - 0.5f) * 2 * amount) : new Vector4(0, 0, 0, (0.5f - n) * 2 * amount);
        }));
    }
}
