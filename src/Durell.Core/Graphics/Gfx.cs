using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Graphics;

/// <summary>
/// 2D drawing in virtual units: the screen is <see cref="Height"/> (360) units tall and 480 to 860
/// wide, drawn straight to the back buffer at the display's full resolution. Text is the Oric-style
/// pixel font, sharp (<see cref="PixelText"/>) or smoothed (<see cref="Text"/>).
/// </summary>
public sealed class Gfx
{
    public const float Height = 360;

    private readonly SpriteBatch _sb;
    private bool _begun;
    private BlendState _blend = BlendState.AlphaBlend;
    private SamplerState _sampler = SamplerState.LinearClamp;
    private Matrix _transform = Matrix.Identity;
    private Vector2 _offset;
    private RectangleF? _clip;
    private readonly System.Collections.Generic.Stack<RectangleF?> _clips = new();
    private static readonly RasterizerState ScissorOn = new() { CullMode = CullMode.None, ScissorTestEnable = true };

    public static readonly BlendState AdditiveBlend = new()
    {
        ColorSourceBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
    };

    public Gfx(GraphicsDevice device, BitmapFont font)
    {
        Device = device;
        Font = font;
        _sb = new SpriteBatch(device);
        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData(new[] { Color.White });
        Rounded = Textures.RoundedRect(device);
        Glow = Textures.Glow(device);
    }

    public GraphicsDevice Device { get; }
    public BitmapFont Font { get; }
    public SpriteBatch Batch => _sb;
    public Texture2D Pixel { get; }
    public Texture2D Rounded { get; }
    public Texture2D Glow { get; }

    /// <summary>Virtual width (depends on the display's shape).</summary>
    public float Width { get; private set; } = 640;
    /// <summary>Device pixels per virtual unit.</summary>
    public float Scale { get; private set; } = 1;
    public float Time { get; set; }
    /// <summary>Area kept clear of notches and the home indicator, in virtual units.</summary>
    public RectangleF Safe { get; set; }

    public void BeginFrame(float virtualWidth, float scale, Vector2 offset)
    {
        Width = virtualWidth;
        Scale = scale;
        _transform = Matrix.CreateScale(scale, scale, 1) * Matrix.CreateTranslation(offset.X, offset.Y, 0);
        _offset = offset;
        _clip = null;
        if (Safe.Width <= 0) Safe = new RectangleF(0, 0, Width, Height);
    }

    /// <summary>A rectangle in virtual units as device pixels (for 3D viewports).</summary>
    public Rectangle DeviceRect(RectangleF r) =>
        new((int)MathF.Floor(_offset.X + r.X * Scale), (int)MathF.Floor(_offset.Y + r.Y * Scale),
            (int)MathF.Ceiling(r.Width * Scale), (int)MathF.Ceiling(r.Height * Scale));

    /// <summary>A device-pixel position in virtual units.</summary>
    public Vector2 FromDevice(Vector2 p) => (p - _offset) / Scale;

    public void Begin(BlendState? blend = null, SamplerState? sampler = null)
    {
        if (_begun) _sb.End();
        _blend = blend ?? BlendState.AlphaBlend;
        _sampler = sampler ?? SamplerState.LinearClamp;
        _sb.Begin(SpriteSortMode.Deferred, _blend, _sampler, DepthStencilState.None, _clip != null ? ScissorOn : RasterizerState.CullNone, null, _transform);
        _begun = true;
    }

    public void End()
    {
        if (_begun) _sb.End();
        _begun = false;
    }

    private void Ensure()
    {
        if (!_begun) Begin();
    }

    /// <summary>Limits drawing to <paramref name="r"/> as well as any clip already in force.</summary>
    public void PushClip(RectangleF r)
    {
        _clips.Push(_clip);
        if (_clip is RectangleF o)
        {
            float x0 = MathF.Max(o.X, r.X), y0 = MathF.Max(o.Y, r.Y);
            float x1 = MathF.Min(o.Right, r.Right), y1 = MathF.Min(o.Bottom, r.Bottom);
            r = new RectangleF(x0, y0, MathF.Max(0, x1 - x0), MathF.Max(0, y1 - y0));
        }
        Clip(r);
    }

    public void PopClip() => Clip(_clips.Count > 0 ? _clips.Pop() : null);

    /// <summary>Text with a dark outline (for titles over busy art).</summary>
    public void OutlinedText(string s, float cx, float y, Color c, Color outline, float scale, float width = 0.8f)
    {
        float x = cx - TextWidth(s, scale) / 2;
        Ensure();
        for (int i = 0; i < 16; i++)
        {
            float a = i * MathF.PI / 8;
            Font.DrawSmooth(_sb, s, new Vector2(x + MathF.Cos(a) * width * scale, y + MathF.Sin(a) * width * scale), outline, scale);
        }
        Font.DrawSmooth(_sb, s, new Vector2(x, y), c, scale);
    }

    /// <summary>Limits drawing to <paramref name="r"/> (virtual units), or lifts the limit (null).</summary>
    public void Clip(RectangleF? r)
    {
        bool was = _begun;
        if (_begun) _sb.End();
        _begun = false;
        _clip = r;
        if (r is RectangleF c)
        {
            var vp = Device.Viewport;
            int x0 = (int)MathF.Floor(c.X * Scale + _offset.X), y0 = (int)MathF.Floor(c.Y * Scale + _offset.Y);
            int x1 = (int)MathF.Ceiling(c.Right * Scale + _offset.X), y1 = (int)MathF.Ceiling(c.Bottom * Scale + _offset.Y);
            x0 = Math.Clamp(x0, 0, vp.Width); x1 = Math.Clamp(x1, 0, vp.Width);
            y0 = Math.Clamp(y0, 0, vp.Height); y1 = Math.Clamp(y1, 0, vp.Height);
            Device.ScissorRectangle = new Rectangle(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }
        if (was) Begin(_blend, _sampler);
    }

    public void Additive() => Begin(AdditiveBlend, _sampler);
    public void Alpha() => Begin(BlendState.AlphaBlend, _sampler);
    public void Pixelated() => Begin(_blend, SamplerState.PointClamp);
    public void Smooth() => Begin(_blend, SamplerState.LinearClamp);

    // ------------------------------------------------------------------ shapes

    public void Rect(float x, float y, float w, float h, Color c)
    {
        Ensure();
        _sb.Draw(Pixel, new Vector2(x, y), null, c, 0, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0);
    }

    public void Rect(RectangleF r, Color c) => Rect(r.X, r.Y, r.Width, r.Height, c);

    public void Frame(RectangleF r, float t, Color c)
    {
        Rect(r.X, r.Y, r.Width, t, c);
        Rect(r.X, r.Bottom - t, r.Width, t, c);
        Rect(r.X, r.Y + t, t, r.Height - 2 * t, c);
        Rect(r.Right - t, r.Y + t, t, r.Height - 2 * t, c);
    }

    public void Line(Vector2 a, Vector2 b, float width, Color c)
    {
        Ensure();
        var d = b - a;
        float len = d.Length();
        if (len < 0.001f) return;
        _sb.Draw(Pixel, a, null, c, MathF.Atan2(d.Y, d.X), new Vector2(0, 0.5f), new Vector2(len, width), SpriteEffects.None, 0);
    }

    /// <summary>Vertical gradient (top to bottom).</summary>
    public void Gradient(RectangleF r, Color top, Color bottom, int steps = 48)
    {
        float sh = r.Height / steps;
        for (int i = 0; i < steps; i++)
            Rect(r.X, r.Y + i * sh, r.Width, sh + 0.05f, Color.Lerp(top, bottom, (i + 0.5f) / steps));
    }

    /// <summary>Rounded rectangle (nine-slice); radius in virtual units.</summary>
    public void RoundRect(RectangleF r, float radius, Color c)
    {
        Ensure();
        radius = MathF.Max(0.5f, MathF.Min(radius, MathF.Min(r.Width, r.Height) / 2));
        const int s = 32, n = 128;
        var srcs = new[] { 0, s, n - s, n };
        var xs = new[] { r.X, r.X + radius, r.Right - radius, r.Right };
        var ys = new[] { r.Y, r.Y + radius, r.Bottom - radius, r.Bottom };
        for (int iy = 0; iy < 3; iy++)
            for (int ix = 0; ix < 3; ix++)
            {
                var src = new Rectangle(srcs[ix], srcs[iy], srcs[ix + 1] - srcs[ix], srcs[iy + 1] - srcs[iy]);
                float dw = xs[ix + 1] - xs[ix], dh = ys[iy + 1] - ys[iy];
                if (dw <= 0 || dh <= 0) continue;
                _sb.Draw(Rounded, new Vector2(xs[ix], ys[iy]), src, c, 0, Vector2.Zero, new Vector2(dw / src.Width, dh / src.Height), SpriteEffects.None, 0);
            }
    }

    /// <summary>A glass panel: dark translucent fill, a lit rim and a soft top sheen.</summary>
    public void Panel(RectangleF r, Color rim, float alpha = 0.85f, float radius = 10)
    {
        RoundRect(r.Inflate(1.5f), radius + 1.5f, rim);
        RoundRect(r, radius, new Color(10, 12, 24) * alpha);
        RoundRect(new RectangleF(r.X + 3, r.Y + 3, r.Width - 6, MathF.Min(r.Height * 0.35f, 22)), radius - 3, Color.White * 0.05f);
    }

    public void GlowAt(Vector2 centre, float radius, Color c)
    {
        Ensure();
        _sb.Draw(Glow, centre, null, c, 0, new Vector2(Glow.Width / 2f), radius * 2 / Glow.Width, SpriteEffects.None, 0);
    }

    /// <summary>A texture centred on a point, <paramref name="width"/> units wide, rotated (radians) and optionally mirrored.</summary>
    public void Sprite(Texture2D tex, Vector2 centre, float width, float rotation, Color c, bool flipX = false, Vector2? origin = null)
    {
        Ensure();
        float k = width / tex.Width;
        var o = origin ?? new Vector2(tex.Width / 2f, tex.Height / 2f);
        if (flipX && origin != null) o.X = tex.Width - o.X;
        _sb.Draw(tex, centre, null, c, rotation, o, k, flipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
    }

    public void Texture(Texture2D tex, RectangleF dest, Color c, Rectangle? src = null)
    {
        Ensure();
        var s = src ?? new Rectangle(0, 0, tex.Width, tex.Height);
        _sb.Draw(tex, new Vector2(dest.X, dest.Y), s, c, 0, Vector2.Zero, new Vector2(dest.Width / s.Width, dest.Height / s.Height), SpriteEffects.None, 0);
    }

    // ------------------------------------------------------------------ text

    public static float TextWidth(string s, float scale = 1) => s.Length * BitmapFont.CharWidth * scale;

    /// <summary>Oric pixel-font text (draw inside <see cref="Pixelated"/> for hard edges).</summary>
    public void PixelText(string s, float x, float y, Color c, int scale = 1)
    {
        Ensure();
        Font.Draw(_sb, s, x, y, c, scale, (int)Width + 64);
    }

    /// <summary>Smoothed text with an optional drop shadow; each character is 6 x 8 units at scale 1.</summary>
    public void Text(string s, float x, float y, Color c, float scale = 1, bool shadow = true)
    {
        Ensure();
        if (shadow) Font.DrawSmooth(_sb, s, new Vector2(x + 0.6f * scale, y + 0.6f * scale), Color.Black * (c.A / 255f) * 0.75f, scale);
        Font.DrawSmooth(_sb, s, new Vector2(x, y), c, scale);
    }

    public void TextCentred(string s, float cx, float y, Color c, float scale = 1, bool shadow = true) =>
        Text(s, cx - TextWidth(s, scale) / 2, y, c, scale, shadow);

    public void TextRight(string s, float right, float y, Color c, float scale = 1, bool shadow = true) =>
        Text(s, right - TextWidth(s, scale), y, c, scale, shadow);

    /// <summary>Glowing smoothed text: a soft halo then the letters.</summary>
    public void GlowText(string s, float cx, float y, Color c, float scale = 1, float glow = 0.6f)
    {
        float x = cx - TextWidth(s, scale) / 2;
        Ensure();
        var halo = c * (glow * 0.2f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.PI / 4;
            Font.DrawSmooth(_sb, s, new Vector2(x + MathF.Cos(a) * scale, y + MathF.Sin(a) * scale), halo, scale);
        }
        Font.DrawSmooth(_sb, s, new Vector2(x, y), c, scale);
    }

    /// <summary>Wraps <paramref name="text"/> to lines of at most <paramref name="width"/> units.</summary>
    public static System.Collections.Generic.List<string> Wrap(string text, float width, float scale)
    {
        var lines = new System.Collections.Generic.List<string>();
        int max = Math.Max(1, (int)(width / (BitmapFont.CharWidth * scale)));
        foreach (var para in text.Split('\n'))
        {
            var line = "";
            foreach (var word in para.Split(' '))
            {
                if (line.Length == 0) line = word;
                else if (line.Length + 1 + word.Length <= max) line += " " + word;
                else
                {
                    lines.Add(line);
                    line = word;
                }
            }
            lines.Add(line);
        }
        return lines;
    }
}
