using System;
using System.Collections.Generic;
using Durell.Graphics.Art;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Games;

/// <summary>
/// Scuba Dive's whole world as one map of text cells, in world coordinates: the sea screen (its sea
/// bed from $2FD0) above maze columns 57-96, extended leftwards so the sea runs the width of the
/// caves, and the cavern maze below it (8 cells x H, each 12 x 7 characters, drawn with the game's own
/// cell tables $1F49/$1F65 from the maze bytes at $1FA0, which the game makes at random each game).
/// The rock is painted in high-resolution tiles - one per maze cell, plus the sea bed - from the
/// game's character shapes, smoothed and textured; octopuses, oysters and treasure are left out of the
/// rock and drawn as artwork by the look.
/// </summary>
internal sealed class ScubaWorld
{
    public const int SeaRows = 22, CellW = 12, CellH = 7, MazeW = 8, Cols = MazeW * CellW;
    private const int R = 6;                                  // high-resolution pixels per Oric pixel

    public int Height = 6;                                    // maze rows (6-14)
    public int Rows => SeaRows + Height * CellH;
    private byte[] _grid = new byte[Cols * (SeaRows + 14 * CellH)];
    private readonly byte[] _seaGlyphs = new byte[0x400];     // the sea bed's charset (alternate), kept from the sea
    private readonly byte[] _caveGlyphs = new byte[0x400];
    private bool _haveSea, _haveCave;
    private readonly byte[] _maze = new byte[8 * 14];
    private readonly Dictionary<int, Texture2D?> _tiles = new();
    private readonly HashSet<int> _dirty = new();

    /// <summary>Octopuses in the cave rock and on the sea bed (world char cell of their top-left).</summary>
    public readonly List<Point> Octopuses = new();
    /// <summary>Treasure: world cell and kind 0-3.</summary>
    public readonly List<(Point Cell, int Kind)> Items = new();

    public byte At(int col, int row) => col < 0 || col >= Cols || row < 0 || row >= Rows ? (byte)0x2C : _grid[row * Cols + col];

    /// <summary>Reads the sea bed and the maze; repaints only tiles whose cells changed.</summary>
    public void Update(byte[] m, bool inCaves)
    {
        // the sea bed's alternate charset stays at $B800 in the caves too
        Buffer.BlockCopy(m, 0xB800, _seaGlyphs, 0, _seaGlyphs.Length);
        _haveSea = true;
        // the cave rock chars $21-$38 live at $1D00 whatever is on screen
        Array.Clear(_caveGlyphs);
        Buffer.BlockCopy(m, 0x1D00, _caveGlyphs, 0x21 * 8, 0x18 * 8);
        _haveCave = true;
        int h = Math.Clamp((int)m[0x1445], 1, 14);
        bool mazeChanged = h != Height;
        Height = h;
        for (int i = 0; i < 8 * h; i++)
            if (_maze[i] != m[0x1FA0 + i])
            {
                _maze[i] = m[0x1FA0 + i];
                mazeChanged = true;
                _dirty.Add(i);
            }
        if (!mazeChanged && _tiles.Count > 0) return;
        Build(m);
    }

    private static int W16(byte[] m, int a) => m[a] | m[a + 1] << 8;

    private void Build(byte[] m)
    {
        Array.Fill(_grid, (byte)0x20);
        Octopuses.Clear();
        Items.Clear();
        // the sea: water line, open water, the sea bed in rows 18-21 (screen rows 22-25)
        for (int r = 18; r < 22; r++)
            for (int c = 0; c < 40; c++)
            {
                byte b = m[0x2FD0 + 40 * (r - 18) + c];
                int wc = 57 + c;
                if (wc < Cols) _grid[r * Cols + wc] = b;
                // the same sea bed's rock continues to the left (without its oysters and octopus)
                for (int k = wc - 40; k >= 0; k -= 40)
                    _grid[r * Cols + k] = b is >= 0x2B and <= 0x2F ? b : (byte)0x20;
            }
        // under the sea bed and above the maze, solid rock where the sea bed has rock
        for (int r = 18; r < 22; r++)
            for (int c = 0; c < Cols; c++)
                if (_grid[r * Cols + c] == 0x20 && r == 21 && c < 57) _grid[r * Cols + c] = 0x2F;
        // the octopus on the sea bed
        for (int r = 18; r < 22; r++)
            for (int c = 57; c < Cols; c++)
                if (_grid[r * Cols + c] == 0x21) Octopuses.Add(new Point(c, r));
        // the maze
        for (int cy = 0; cy < Height; cy++)
            for (int cx = 0; cx < MazeW; cx++)
            {
                byte v = _maze[cy * 8 + cx];
                int rec = W16(m, 0x1F49 + 2 * (v & 15));
                for (int k = 0; k < CellH; k++)
                {
                    int piece = W16(m, 0x1F65 + 2 * m[rec + (CellH - k)]);
                    for (int j = 0; j < CellW; j++)
                    {
                        byte b = m[piece + 1 + j];
                        int wr = SeaRows + cy * CellH + k, wc = cx * CellW + j;
                        _grid[wr * Cols + wc] = b;
                        if (b == 0x21) Octopuses.Add(new Point(wc, wr));
                    }
                }
                AddItems(m, cx, cy, v);
            }
        BuildField();
        foreach (var key in new List<int>(_tiles.Keys)) _dirty.Add(key);
        for (int i = 0; i < 8 * Height; i++) _dirty.Add(i);
        for (int i = 1; i <= MazeW; i++) _dirty.Add(-i);
    }

    // the rock's shape at Oric-pixel resolution (the sea bed's dithering blurred into solid form),
    // so painting a tile is only lookups
    private float[] _field = Array.Empty<float>();
    private int _fw, _fh;

    private void BuildField()
    {
        _fw = Cols * 6;
        _fh = Rows * 8;
        var bits = new float[_fw * _fh];
        for (int j = 0; j < _fh; j++)
            for (int i = 0; i < _fw; i++)
                bits[j * _fw + i] = RawBit(i, j);
        _field = (float[])bits.Clone();
        int seaH = SeaRows * 8;
        for (int j = 0; j < Math.Min(seaH, _fh); j++)
            for (int i = 0; i < _fw; i++)
            {
                float sum = 0;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int x = Math.Clamp(i + dx, 0, _fw - 1), y = Math.Clamp(j + dy, 0, _fh - 1);
                        sum += bits[y * _fw + x];
                    }
                _field[j * _fw + i] = MathF.Min(1, sum / 25 * 2.2f);
            }
    }

    private void AddItems(byte[] m, int cx, int cy, byte v)
    {
        int hi = v & 0xF0, t = v & 15;
        if (hi == 0) return;
        int idx = cy * 8 + cx;
        int kind = t is 0x0B or 0x0C ? 0 : idx < 0x11 ? 1 : t is >= 5 and < 8 ? 2 : 3;
        int b = 280 - m[0x235D + t];
        for (int y = 2; y <= 5; y++)
        {
            if ((hi << (y - 2) & 0x80) == 0) continue;
            int o = b + y;
            Items.Add((new Point(cx * CellW + o % 40 - 1, SeaRows + cy * CellH + o / 40), kind));
        }
    }

    // ------------------------------------------------------------------ painting

    /// <summary>Tiles -1..-8 are the sea bed (rows 17-21) in 12-column pieces; 0.. are maze cells.</summary>
    public Texture2D? Tile(int key) => _tiles.TryGetValue(key, out var t) ? t : null;

    public static Rectangle TileCells(int key) =>
        key < 0 ? new Rectangle((-key - 1) * CellW, 17, CellW, 5) : new Rectangle(key % 8 * CellW, SeaRows + key / 8 * CellH, CellW, CellH);

    /// <summary>Paints up to <paramref name="budget"/> changed tiles, nearest the camera (world pixels) first.</summary>
    public void Paint(GraphicsDevice device, int budget, Vector2 camera)
    {
        if (!_haveSea || !_haveCave || _field.Length == 0) return;
        var order = new List<int>(_dirty);
        var centre = camera + new Vector2(120, 92);
        order.Sort((a, b) => Distance(a, centre).CompareTo(Distance(b, centre)));
        var done = new List<int>();
        foreach (var key in order)
        {
            if (budget-- <= 0) break;
            if (_tiles.TryGetValue(key, out var old)) old?.Dispose();
            _tiles[key] = PaintTile(device, key);
            done.Add(key);
        }
        foreach (var k in done) _dirty.Remove(k);
    }

    private static float Distance(int key, Vector2 p)
    {
        var c = TileCells(key);
        return Vector2.Distance(p, new Vector2((c.X + c.Width / 2f) * 6, (c.Y + c.Height / 2f) * 8));
    }

    private bool IsRock(byte ch, int row) =>
        row >= SeaRows ? ch is >= 0x2C and <= 0x34 : ch is >= 0x2B and <= 0x2F;

    private float Bit(int i, int j)
    {
        if (i < 0 || i >= _fw) return 1;
        if (j < 0) return 0;
        if (j >= _fh) return 1;
        return _field[j * _fw + i];
    }

    private int RawBit(int i, int j)
    {
        int col = (int)MathF.Floor(i / 6f), row = (int)MathF.Floor(j / 8f);
        if (col < 0 || col >= Cols) return 1;                    // rock beyond the world's sides
        if (row >= Rows) return 1;
        if (row < 0) return 0;
        byte ch = At(col, row);
        if (!IsRock(ch, row)) return 0;
        var glyphs = row >= SeaRows ? _caveGlyphs : _seaGlyphs;
        int bits = glyphs[ch * 8 + (j - row * 8)] & 0x3F;
        return bits >> (5 - (i - col * 6)) & 1;
    }

    private float V(float ox, float oy)
    {
        float x = ox - 0.5f, y = oy - 0.5f;
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        return MathHelper.Lerp(MathHelper.Lerp(Bit(x0, y0), Bit(x0 + 1, y0), fx), MathHelper.Lerp(Bit(x0, y0 + 1), Bit(x0 + 1, y0 + 1), fx), fy);
    }

    private float Field(float ox, float oy) => V(ox, oy);

    private Texture2D? PaintTile(GraphicsDevice device, int key)
    {
        var cells = TileCells(key);
        int w = cells.Width * 6 * R, h = cells.Height * 8 * R;
        var canvas = new Canvas(w, h);
        bool any = false;
        for (int y = 0; y < h; y++)
        {
            float oy = cells.Y * 8 + (y + 0.5f) / R;
            for (int x = 0; x < w; x++)
            {
                float ox = cells.X * 6 + (x + 0.5f) / R;
                float v = Field(ox, oy);
                if (v <= 0.3f) continue;
                float a = Math.Clamp((v - 0.38f) / 0.24f, 0, 1);
                a = a * a * (3 - 2 * a);
                // light comes from the surface above: top edges lit, undersides dark, algae on the tops
                float gy = Field(ox, oy + 0.8f) - Field(ox, oy - 0.8f), gx = Field(ox + 0.8f, oy) - Field(ox - 0.8f, oy);
                float lit = Math.Clamp(gy * 0.9f - gx * 0.25f, -1, 1);
                float n = Noise.Fbm(ox * 0.09f, oy * 0.09f, 5, 4);
                float cracks = Noise.Ridged(ox * 0.06f, oy * 0.11f, 6, 3);
                var rock = Vector3.Lerp(new Vector3(0.28f, 0.25f, 0.22f), new Vector3(0.48f, 0.42f, 0.34f), n);
                rock = Vector3.Lerp(rock, new Vector3(0.18f, 0.16f, 0.15f), MathF.Max(0, cracks - 0.7f) * 2);
                bool top = Field(ox, oy - 1.8f) < 0.4f;
                if (top)
                {
                    float weed = Noise.Fbm(ox * 0.4f, oy * 0.5f, 8, 3);
                    rock = Vector3.Lerp(rock, weed > 0.55f ? new Vector3(0.25f, 0.55f, 0.32f) : new Vector3(0.62f, 0.35f, 0.4f), 0.75f);
                }
                float depth = Math.Clamp(oy / (Rows * 8f), 0, 1);
                rock *= (1 + lit * 0.45f) * (1 - depth * 0.35f);
                // a blue-green cast: everything is seen through water
                rock = Vector3.Lerp(rock, new Vector3(0.1f, 0.3f, 0.38f), 0.18f + depth * 0.2f);
                canvas.Set(x, y, new Vector4(Vector3.Clamp(rock, Vector3.Zero, Vector3.One), a));
                any = true;
            }
        }
        return any ? canvas.ToTexture(device) : null;
    }
}
