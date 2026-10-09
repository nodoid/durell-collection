using System;
using Microsoft.Xna.Framework;

namespace Durell.Games;

/// <summary>
/// Turbo Esprit's city in world coordinates (units: lane-grid rows; x east, y north), from the
/// 339-byte city block the game keeps at $019D (research notes, verified against the in-game map):
/// a 32 x 32 lattice of junctions, each a W(L) x W(H) square (W = 28/20/12 by street class), joined
/// by street segments exactly 100 rows long; building frontages seeded from each segment's node.
/// </summary>
internal sealed class TurboCity
{
    public static readonly int[] Width = { 28, 20, 12, 12 };
    public static readonly int[] Lanes = { 3, 2, 1, 1 };

    public readonly byte[] Block = new byte[0x153];
    public readonly int[] Wx = new int[32], Wy = new int[32];
    public readonly int[] X0 = new int[33], Y0 = new int[33];

    public void Read(byte[] m)
    {
        Buffer.BlockCopy(m, 0x019D, Block, 0, Block.Length);
        X0[0] = Y0[0] = 0;
        for (int l = 0; l < 32; l++)
        {
            Wx[l] = Width[Block[256 + l] & 3];
            Wy[l] = Width[Block[288 + l] & 3];
            X0[l + 1] = X0[l] + Wx[l] + 100;
            Y0[l + 1] = Y0[l] + Wy[l] + 100;
        }
    }

    public int EntryL => Block[320];
    public int EntryH => Block[321];

    private int Look(int l, int h)
    {
        if (l is >= 0 and < 32 && h is >= 0 and < 32) return Block[(h >> 2) * 32 + l] >> (h & 3);
        if (l is < 0 or >= 32) return h == EntryH ? 0x01 : 0xFF;
        return l == EntryL ? 0x10 : 0xFF;
    }

    public bool NorthOpen(int l, int h) => (Look(l, h) & 1) == 0;

    public bool EastOpen(int l, int h) => l is < 0 or >= 32 ? h == EntryH : (Look(l, h) & 0x10) == 0;

    public int StreetWidthX(int l) => l is >= 0 and < 32 ? Wx[l] : 28;
    public int StreetWidthY(int h) => h is >= 0 and < 32 ? Wy[h] : 28;
    public int JunctionX(int l) => l is >= 0 and < 32 ? X0[l] : l < 0 ? -128 : X0[32];
    public int JunctionY(int h) => h is >= 0 and < 32 ? Y0[h] : h < 0 ? -128 : Y0[32];

    /// <summary>The building types (1-4) along both sides of the segment named after node (L, H) ($958B).</summary>
    public static (int[] A, int[] B) Buildings(int l, int h)
    {
        int sl = l & 255, sh = h & 255;
        var res = new int[2][];
        for (int k = 0; k < 2; k++)
        {
            int a = sl, cy = 0;
            for (int i = 0; i < (sl & 7) + 1; i++)
            {
                cy = a & 1;
                a = (a >> 1) | (cy << 7);
            }
            sl = (a + 0xAA + cy) & 255;
            a = sh;
            for (int i = 0; i < (sh & 7) + 1; i++)
            {
                cy = a >> 7;
                a = ((a << 1) & 255) | cy;
            }
            sh = (a + 0xA5 + cy) & 255;
            int de = sh * 256 + sl;
            res[k] = new int[6];
            for (int i = 0; i < 6; i++)
            {
                res[k][i] = (de & 3) + 1;
                de >>= 2;
            }
        }
        return (res[0], res[1]);
    }

    private static int LatUnits(int page, int sub) =>
        page >= 0x60 ? 4 + 8 * (page - 0x60) - sub : -(4 + 8 * (0x5F - page) - sub);

    /// <summary>
    /// The world position of an object record (or a lane-grid cell when <paramref name="rec"/> is &lt; 0),
    /// in the frame of the player's current block (node ahead $7504/$7505, heading $75F0).
    /// </summary>
    public Vector2 Position(byte[] m, int rec, int cellRow = 0, int cellPage = 0)
    {
        int l = m[0x7504], h = m[0x7505], hd = m[0x75F0] & 3;
        int r, u;
        if (rec >= 0 && (m[rec + 1] & 0x40) != 0)
        {
            // in a side-street arm
            r = 0x64 + m[rec + 9];
            int c = m[rec + 8];
            int kerb = (m[rec + 1] & 0x20) != 0 ? m[0x75D7] : m[0x75D6];
            u = LatUnits(kerb, 0);
            u += u > 0 ? 8 * (c + 1) : -8 * (c + 1);
        }
        else if (rec >= 0)
        {
            r = m[rec + 6];
            int sub = m[rec] < 8 ? m[rec] : 0;
            u = LatUnits(m[rec + 7], sub);
        }
        else
        {
            r = cellRow;
            u = LatUnits(cellPage, 0);
        }
        int s = r - 0x64;
        float jx = JunctionX(l), jy = JunctionY(h);
        int wl = StreetWidthX(l), wh = StreetWidthY(h);
        int road = hd is 0 or 2 ? wl : wh;
        int cls = Array.IndexOf(Width, road);
        if (cls < 0) cls = 0;
        float k = road / (16f * Lanes[cls]);
        return hd switch
        {
            0 => new Vector2(jx + wl / 2f + u * k, jy + wh - s),
            2 => new Vector2(jx + wl / 2f - u * k, jy + s),
            1 => new Vector2(jx + wl - s, jy + wh / 2f - u * k),
            _ => new Vector2(jx + s, jy + wh / 2f + u * k),
        };
    }
}
