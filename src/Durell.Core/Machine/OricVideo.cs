namespace Durell.Machine;

/// <summary>
/// Turns the Oric's screen memory into pixels, the way its video chip shows
/// it: 240 x 224, serial attributes (ink, paper, character set, double height,
/// blink, video mode), inverse video, TEXT (28 rows at $BB80) and HIRES
/// (200 lines at $A000 + the 3 text rows at $BF68).
/// </summary>
internal sealed class OricVideo
{
    public const int Width = 240, Height = 224;

    /// <summary>Colour index (0-7) per pixel, plus a flag (8) for pixels drawn as ink.</summary>
    public readonly byte[] Index = new byte[Width * Height];

    private bool _hires;
    private int _frame;

    public static readonly uint[] Palette =
    {
        0xFF000000, 0xFF0000FF, 0xFF00FF00, 0xFF00FFFF,
        0xFFFF0000, 0xFFFF00FF, 0xFFFFFF00, 0xFFFFFFFF,
    };  // ABGR (as MonoGame's Color packed value)

    public bool Hires => _hires;

    public void Reset(byte[] m)
    {
        // the mode latched at power-up is TEXT; a program sets it with the last
        // byte of the screen ($BFDF) or an attribute anywhere
        _hires = false;
        _frame = 0;
    }

    public void Render(byte[] m)
    {
        _frame++;
        bool blinkOn = (_frame & 0x10) != 0;
        for (int y = 0; y < Height; y++)
        {
            int ink = 7, paper = 0;
            bool alt = false, dbl = false, blink = false;
            bool hiresLine = _hires && y < 200;
            int baseAddr = hiresLine ? 0xA000 + y * 40 : 0xBB80 + (y >> 3) * 40;
            int o = y * Width;
            for (int col = 0; col < 40; col++)
            {
                int b = m[baseAddr + col];
                int inv = (b & 0x80) != 0 ? 7 : 0;
                int bits;
                bool attr = (b & 0x60) == 0;
                if (attr)
                {
                    int v = b & 0x1F;
                    switch (v >> 3)
                    {
                        case 0: ink = v & 7; break;
                        case 1:
                            alt = (v & 1) != 0;
                            dbl = (v & 2) != 0;
                            blink = (v & 4) != 0;
                            break;
                        case 2: paper = v & 7; break;
                        case 3: _hires = (v & 4) != 0; break;
                    }
                    bits = 0;
                }
                else if (hiresLine)
                {
                    bits = b & 0x3F;
                }
                else
                {
                    int row = y >> 3, line = y & 7;
                    if (dbl) line = (line >> 1) | ((row & 1) << 2);
                    int cs = _hires ? (alt ? 0x9C00 : 0x9800) : (alt ? 0xB800 : 0xB400);
                    bits = m[cs + ((b & 0x7F) << 3) + line] & 0x3F;
                }
                if (blink && !blinkOn) bits = 0;
                byte fg = (byte)((ink ^ inv) | 8), bg = (byte)(paper ^ inv);
                for (int i = 0; i < 6; i++)
                    Index[o + col * 6 + i] = (bits & (0x20 >> i)) != 0 ? fg : bg;
            }
        }
    }

    public void ToRgba(uint[] dst)
    {
        for (int i = 0; i < Index.Length; i++) dst[i] = Palette[Index[i] & 7];
    }
}
