using System;
using System.Collections.Generic;
using Durell.Graphics.Art;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// Harrier Attack's world as high-resolution columns. Every column the game scrolls past is kept by
/// its absolute position (step count + screen column) and painted once - again only if the game
/// changes it (a target destroyed, flak placed) - from the game's own character shapes: the 6 x 8
/// glyphs are sampled bilinearly across neighbouring cells and thresholded, which turns the stair-step
/// pixels into smooth outlines, then filled with a material chosen by the character and its ink.
/// </summary>
internal sealed class HarrierWorld
{
    private const int Rows = 25;
    private const int R = 8;                      // high-resolution pixels per Oric pixel

    internal sealed class Column
    {
        public long Abs;
        public readonly byte[] Chars = new byte[Rows];
        public readonly byte[] Inks = new byte[Rows];
        public bool Dirty = true, Provisional;
        public Texture2D? Texture;
        public int Top, Bottom;
    }

    private enum Mat : byte { None, Ground, Dark, Roof, Cloud, Glass, Smoke, Ember }

    private readonly Dictionary<long, Column> _cols = new();
    private readonly byte[] _charset = new byte[0x400];
    private readonly List<Column> _ordered = new();

    public IEnumerable<Column> Columns => _ordered;
    public readonly List<(string Kind, long First, long Last, int Row)> Pieces = new();
    public readonly List<(long Abs, int Row)> Debris = new();

    /// <summary>Takes the screen (40 x 25 text cells from $BB80) as the world at step <paramref name="count"/>.</summary>
    public void Take(byte[] screen, long count, byte[] memory, int phase)
    {
        Buffer.BlockCopy(memory, 0xB400, _charset, 0, _charset.Length);
        // the phase decides what $7F in the bottom rows is (carrier deck at sea, land otherwise)
        if (phase != _lastPhase)
        {
            foreach (var c in _cols.Values) c.Dirty = true;
            _lastPhase = phase;
        }
        var inks = new byte[40];
        for (int c = 2; c <= 38; c++)
        {
            long abs = count + c;
            if (!_cols.TryGetValue(abs, out var col))
            {
                col = new Column { Abs = abs };
                _cols[abs] = col;
            }
            bool changed = false;
            for (int row = 0; row < Rows; row++)
            {
                byte ch = screen[row * 40 + c];
                byte ink = InkAt(screen, row, c);
                if (col.Chars[row] != ch || col.Inks[row] != ink)
                {
                    col.Chars[row] = ch;
                    col.Inks[row] = ink;
                    changed = true;
                }
            }
            bool provisional = c == 38;
            if (col.Provisional && !provisional) changed = true;     // its right-hand neighbour is known now
            col.Provisional = provisional;
            if (changed)
            {
                col.Dirty = true;
                if (_cols.TryGetValue(abs - 1, out var l)) l.Dirty = true;
                if (_cols.TryGetValue(abs + 1, out var rr)) rr.Dirty = true;
            }
        }
        // forget columns long gone off the left (or, in the intro, the right)
        var drop = new List<long>();
        foreach (var k in _cols.Keys)
            if (k < count - 3 || k > count + 40) drop.Add(k);
        foreach (var k in drop)
        {
            _cols[k].Texture?.Dispose();
            _cols.Remove(k);
        }
        _ordered.Clear();
        _ordered.AddRange(_cols.Values);
        _ordered.Sort((a, b) => a.Abs.CompareTo(b.Abs));
        FindPieces(count, phase);
    }

    /// <summary>
    /// The land's surface height (world units above the sea, 8 per text row) at a column's left and right
    /// edges, from its terrain cells; false where there is no land (sea, or the carrier's deck).
    /// </summary>
    public bool Heights(Column c, out float left, out float right)
    {
        left = right = 0;
        bool carrier = _seaPhase && IsCarrierColumn(c);
        if (carrier) return false;
        int top = -1;
        for (int row = 23; row >= 1; row--)
        {
            byte ch = c.Chars[row];
            if (ch is 0x7F or 0x2F or 0x3B) top = row;
            else break;
            if (ch != 0x7F) break;
        }
        if (top < 0) return false;
        float full = (24 - top) * 8, lower = (23 - top) * 8;
        switch (c.Chars[top])
        {
            case 0x2F: left = lower; right = full; break;
            case 0x3B: left = full; right = lower; break;
            default: left = right = full; break;
        }
        return true;
    }

    /// <summary>Is this a cell of a set piece drawn as a whole model (carrier, enemy ship)?</summary>
    public bool IsSetPiece(Column c, int row) =>
        _seaPhase && (IsCarrierChar(c.Chars[row]) || row >= 20 && c.Chars[row] == 0x7F && IsCarrierColumn(c)) ||
        IsBoatChar(c.Chars[row]) && row >= 21;

    private static byte InkAt(byte[] screen, int row, int c)
    {
        byte ink = 7;
        for (int x = 0; x <= c; x++)
        {
            byte v = (byte)(screen[row * 40 + x] & 0x7F);
            if (v < 8) ink = v;
        }
        return ink;
    }

    private static bool IsCarrierChar(byte ch) => ch is 0x22 or 0x23 or 0x26 or 0x27 or 0x28 or 0x29 or 0x2C;
    private static bool IsBoatChar(byte ch) => ch is 0x5F or 0x67 or 0x68 or 0x62;

    private bool _seaPhase;
    private int _lastPhase = -1;

    private void FindPieces(long count, int phase)
    {
        Pieces.Clear();
        Debris.Clear();
        _seaPhase = phase is 0 or 2 or 5;
        long runStart = -1;
        bool touchLeft = false;
        for (int i = 0; i <= _ordered.Count; i++)
        {
            var col = i < _ordered.Count ? _ordered[i] : null;
            bool carrier = col != null && _seaPhase && IsCarrierColumn(col);
            if (carrier && runStart < 0)
            {
                runStart = col!.Abs;
                touchLeft = col.Abs <= count + 2;
            }
            if (!carrier && runStart >= 0)
            {
                long last = _ordered[i - 1].Abs;
                bool touchRight = last >= count + 38;
                int length = phase == 5 ? 18 : 21;
                long first = runStart;
                if (touchRight) last = first + length - 1;
                else if (touchLeft) first = last - length + 1;
                Pieces.Add(("carrier", first, last, 23));
                runStart = -1;
            }
            if (col == null) break;
            for (int row = 1; row < Rows; row++)
            {
                if (col.Chars[row] == 0x5F && row >= 21) Pieces.Add(("frigate", col.Abs, col.Abs + 3, row));
                if (col.Chars[row] is 0x24 or 0x25) Debris.Add((col.Abs, row));
            }
        }
    }

    private bool IsCarrierColumn(Column col)
    {
        for (int row = 20; row <= 23; row++)
        {
            byte ch = col.Chars[row];
            if (IsCarrierChar(ch) || row == 23 && ch == 0x7F) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ painting

    /// <summary>Paints columns that changed (a few per frame, so a busy step never stalls a frame).</summary>
    public void Upload(GraphicsDevice device)
    {
        int budget = 0;
        foreach (var col in _ordered)
        {
            if (!col.Dirty) continue;
            if (col.Texture != null && budget >= 6) break;
            Paint(device, col);
            budget++;
        }
    }

    private Column? Neighbour(Column c, int d) => _cols.TryGetValue(c.Abs + d, out var n) ? n : null;

    private byte CharAt(Column?[] win, int ci, int row)
    {
        var col = win[ci];
        if (col == null || row < 0 || row >= Rows) return 0x20;
        byte ch = col.Chars[row];
        if (ch < 0x20 || ch >= 0x80) return 0x20;
        if (_seaPhase && (IsCarrierChar(ch) || row >= 20 && ch == 0x7F && IsCarrierColumn(col))) return 0x20;
        if (IsBoatChar(ch) && row >= 21) return 0x20;
        return ch;
    }

    private void Paint(GraphicsDevice device, Column col)
    {
        col.Dirty = false;
        var left = Neighbour(col, -1);
        var right = Neighbour(col, 1) ?? col;          // provisional: continue the column itself
        var win = new Column?[] { left, col, right };
        int top = Rows, bottom = 0;
        for (int row = 1; row < Rows; row++)
            if (CharAt(win, 1, row) != 0x20)
            {
                top = Math.Min(top, row);
                bottom = Math.Max(bottom, row + 1);
            }
        col.Texture?.Dispose();
        col.Texture = null;
        if (top >= bottom) return;
        top = Math.Max(0, top - 1);
        bottom = Math.Min(Rows, bottom + 1);
        col.Top = top;
        col.Bottom = bottom;

        int w = 6 * R, h = (bottom - top) * 8 * R;
        var canvas = new Canvas(w, h);
        // the Oric pixel (i, j) in the window: i 0..17 across three columns, j 0..199 down
        int Bit(int i, int j, out Mat mat)
        {
            mat = Mat.None;
            if (i < 0 || i >= 18 || j < 0 || j >= Rows * 8) return 0;
            int ci = i / 6, row = j / 8;
            byte ch = CharAt(win, ci, row);
            if (ch == 0x20) return 0;
            byte glyph = (byte)(_charset[ch * 8 + (j & 7)] & 0x3F);
            bool set = (glyph >> (5 - i % 6) & 1) != 0;
            byte ink = win[ci]!.Inks[row];
            mat = Material(ch, ink);
            if (!set && ch == 0x6A && (j & 7) == 2) mat = Mat.Glass;    // the windows of the town's walls
            else if (!set) return 0;
            return 1;
        }
        Mat CellMat(int i, int j)
        {
            if (i < 0 || i >= 18 || j < 0 || j >= Rows * 8) return Mat.None;
            byte ch = CharAt(win, i / 6, j / 8);
            return ch == 0x20 ? Mat.None : Material(ch, win[i / 6]!.Inks[j / 8]);
        }
        bool NearCloud(float ox, float oy)
        {
            for (int dj = -3; dj <= 3; dj += 3)
                for (int di = -3; di <= 3; di += 3)
                    if (CellMat((int)ox + di, (int)oy + dj) == Mat.Cloud) return true;
            return false;
        }
        float V(float ox, float oy)
        {
            float x = ox - 0.5f, y = oy - 0.5f;
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            float a = Bit(x0, y0, out _), b = Bit(x0 + 1, y0, out _), c = Bit(x0, y0 + 1, out _), d = Bit(x0 + 1, y0 + 1, out _);
            return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
        }
        Mat MatAt(float ox, float oy)
        {
            int i = (int)MathF.Floor(ox), j = (int)MathF.Floor(oy);
            Mat best = Mat.None;
            for (int dj = 0; dj <= 1 && best == Mat.None; dj++)
                for (int di = 0; di <= 1 && best == Mat.None; di++)
                    if (Bit(i - di + (ox - i > 0.5f ? 1 : 0), j - dj + (oy - j > 0.5f ? 1 : 0), out var m) != 0) best = m;
            if (best == Mat.None) Bit(i, j, out best);
            return best;
        }

        for (int y = 0; y < h; y++)
        {
            float oy = top * 8 + (y + 0.5f) / R;
            int cellRow = (int)(oy / 8);
            for (int x = 0; x < w; x++)
            {
                float ox = 6 + (x + 0.5f) / R;
                // quick skip: nothing in this cell or around it
                float v = V(ox, oy);
                bool cloud = NearCloud(ox, oy);
                if (v <= 0.02f && !cloud) continue;
                var mat = cloud ? Mat.Cloud : MatAt(ox, oy);
                if (mat == Mat.None) continue;
                float wx = col.Abs * 6 + (x + 0.5f) / R;      // world position, for textures that line up across columns
                Vector4 colour;
                float a;
                switch (mat)
                {
                    case Mat.Ground:
                    {
                        a = Smooth(0.38f, 0.62f, v);
                        bool grass = V(ox, oy - 1.4f) < 0.45f;
                        float n = Noise.Fbm(wx * 0.18f, oy * 0.25f, 11, 4);
                        float strata = Noise.Ridged(wx * 0.05f, oy * 0.45f, 12, 3);
                        var earth = Vector3.Lerp(new Vector3(0.42f, 0.33f, 0.22f), new Vector3(0.58f, 0.47f, 0.32f), n);
                        earth = Vector3.Lerp(earth, new Vector3(0.35f, 0.3f, 0.26f), MathF.Max(0, strata - 0.6f) * 1.5f);
                        if (grass)
                        {
                            float gn = Noise.Fbm(wx * 0.6f, oy * 0.6f, 13, 3);
                            earth = Vector3.Lerp(new Vector3(0.22f, 0.45f, 0.14f), new Vector3(0.42f, 0.62f, 0.2f), gn);
                        }
                        else if (V(ox, oy - 2.6f) < 0.45f) earth *= 0.82f;     // shade under the turf
                        colour = new Vector4(earth, 1);
                        break;
                    }
                    case Mat.Cloud:
                    {
                        // the clouds are dithered patterns: blur them into soft puffs
                        float soft = 0;
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++) soft += V(ox + dx * 0.9f, oy + dy * 0.9f);
                        soft /= 25;
                        float n = Noise.Fbm(wx * 0.25f, oy * 0.3f, 21, 4);
                        a = Smooth(0.12f, 0.55f, soft * (0.75f + n * 0.5f)) * 0.95f;
                        float lit = 0.86f + 0.14f * Math.Clamp((V(ox, oy + 1.5f) - V(ox, oy - 1.5f)) + 0.5f, 0, 1);
                        colour = new Vector4(lit, lit, MathF.Min(1, lit + 0.03f), 1);
                        break;
                    }
                    case Mat.Smoke:
                    {
                        float n = Noise.Fbm(wx * 0.4f, oy * 0.4f, 31, 3);
                        a = Smooth(0.1f, 0.8f, v * (0.6f + n * 0.8f)) * 0.85f;
                        float k = 0.18f + 0.15f * n;
                        colour = new Vector4(k, k, k * 1.05f, 1);
                        break;
                    }
                    case Mat.Ember:
                    {
                        a = Smooth(0.35f, 0.65f, v);
                        float n = Noise.Fbm(wx * 0.7f, oy * 0.7f, 41, 3);
                        colour = new Vector4(1f, 0.35f + 0.5f * n, 0.08f, 1);
                        break;
                    }
                    case Mat.Glass:
                    {
                        a = 1;
                        float n = Noise.Value(wx * 0.5f, cellRow, 51);
                        colour = n > 0.45f ? new Vector4(1f, 0.86f, 0.5f, 1) : new Vector4(0.25f, 0.35f, 0.45f, 1);
                        break;
                    }
                    default:
                    {
                        a = Smooth(0.38f, 0.62f, v);
                        // lit from the top left: the outline's slope says which way each edge faces
                        float gx = V(ox + 0.6f, oy) - V(ox - 0.6f, oy), gy = V(ox, oy + 0.6f) - V(ox, oy - 0.6f);
                        float light = Math.Clamp(-(gx * -0.45f + gy * -0.9f), -1, 1);
                        float n = Noise.Fbm(wx * 0.35f, oy * 0.35f, mat == Mat.Roof ? 61 : 71, 3);
                        Vector3 baseC;
                        if (mat == Mat.Roof)
                        {
                            float tile = (MathF.Floor(oy * 1.2f) % 2 == 0 ? 0.92f : 1.05f) * (MathF.Floor(wx * 0.8f + MathF.Floor(oy * 1.2f) * 0.5f) % 2 == 0 ? 1 : 0.94f);
                            baseC = new Vector3(0.66f, 0.24f, 0.16f) * tile;
                        }
                        else baseC = Vector3.Lerp(new Vector3(0.24f, 0.25f, 0.24f), new Vector3(0.36f, 0.37f, 0.35f), n);
                        baseC *= 1 + light * 0.55f;
                        colour = new Vector4(Vector3.Clamp(baseC, Vector3.Zero, Vector3.One), 1);
                        break;
                    }
                }
                if (a <= 0) continue;
                colour.W = a;
                canvas.Set(x, y, colour);
            }
        }
        col.Texture = canvas.ToTexture(device);
    }

    private static Mat Material(byte ch, byte ink)
    {
        if (ch is 0x7F or 0x2F or 0x3B && ink == 2) return Mat.Ground;
        if (ch is 0x2E or 0x3A or 0x3E && ink == 7) return Mat.Cloud;
        if (ch == 0x3C) return Mat.Smoke;
        if (ch is 0x24 or 0x25) return ink == 1 ? Mat.Ember : Mat.Dark;
        if (ink == 1) return Mat.Roof;
        if (ch is 0x7F or 0x2F or 0x3B) return Mat.Ground;
        return Mat.Dark;
    }

    private static float Smooth(float e0, float e1, float v)
    {
        float t = Math.Clamp((v - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
