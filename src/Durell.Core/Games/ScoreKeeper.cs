using System;
using Durell.Persistence;
using Durell.Programs;

namespace Durell.Games;

/// <summary>
/// Keeps a game's high scores between launches. The games keep their tables in memory and lose
/// them when the machine is switched off; the keeper writes the saved bytes back once the game has
/// set its table up, then watches them and saves whenever the game changes them.
/// </summary>
internal class ScoreKeeper
{
    /// <summary>The bytes of the game's high-score table(s).</summary>
    protected (int Address, int Length)[] Regions = Array.Empty<(int, int)>();
    /// <summary>The frame after which the game has initialised its table (then it is restored).</summary>
    protected int RestoreFrame = 25;
    /// <summary>Instead of a frame: the program address reached just after the table is set up.</summary>
    protected int RestoreProbe = -1;
    private bool _restored;
    private string _last = "";

    /// <summary>Set when the save needs writing.</summary>
    public bool Dirty { get; set; }

    public virtual void Attach(GameProgram p, GameSave save)
    {
        _restored = false;
        _last = "";
        if (RestoreProbe >= 0)
        {
            var prev = p.Probe;
            p.Probe = (prog, a) =>
            {
                prev?.Invoke(prog, a);
                if (a == RestoreProbe) Restore(prog, save);
            };
        }
    }

    /// <summary>Called after every frame the game runs.</summary>
    public virtual void AfterFrame(GameProgram p, GameSave save)
    {
        if (Regions.Length == 0) return;
        if (!_restored)
        {
            if (RestoreProbe < 0 && p.Frame >= RestoreFrame) Restore(p, save);
            return;
        }
        string now = Capture(p.Memory);
        if (now == _last) return;
        _last = now;
        save.Memory = now;
        int best = Best(p.Memory);
        if (best > save.Best)
        {
            save.Best = best;
            save.BestName = BestName(p.Memory);
            save.BestDate = DateTime.Now.ToString("yyyy-MM-dd");
        }
        Dirty = true;
    }

    protected void Restore(GameProgram p, GameSave save)
    {
        if (_restored) return;
        _restored = true;
        if (save.Memory.Length == Total * 2 && Valid(save.Memory))
        {
            var bytes = Convert.FromHexString(save.Memory);
            int o = 0;
            foreach (var (a, n) in Regions)
            {
                Array.Copy(bytes, o, p.Memory, a, n);
                o += n;
            }
            AfterRestore(p);
        }
        _last = Capture(p.Memory);
    }

    private int Total
    {
        get
        {
            int t = 0;
            foreach (var (_, n) in Regions) t += n;
            return t;
        }
    }

    private static bool Valid(string hex)
    {
        foreach (char c in hex)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    private string Capture(byte[] m)
    {
        var bytes = new byte[Total];
        int o = 0;
        foreach (var (a, n) in Regions)
        {
            Array.Copy(m, a, bytes, o, n);
            o += n;
        }
        return Convert.ToHexString(bytes);
    }

    /// <summary>A chance to redraw what the game showed of the table before it was restored.</summary>
    protected virtual void AfterRestore(GameProgram p) { }

    /// <summary>The best score in the game's table (for the menu).</summary>
    public virtual int Best(byte[] m) => 0;

    public virtual string BestName(byte[] m) => "";

    /// <summary>How the menu shows a score.</summary>
    public virtual string Format(int score) => score.ToString();

    /// <summary>Reads packed BCD digits (two per byte, most significant first).</summary>
    protected static int Bcd(byte[] m, int address, int bytes)
    {
        int v = 0;
        for (int i = 0; i < bytes; i++)
        {
            int b = m[address + i];
            v = v * 100 + (b >> 4) * 10 + (b & 15);
        }
        return v;
    }

    /// <summary>Reads ASCII digits (spaces count as 0).</summary>
    protected static int Ascii(byte[] m, int address, int length)
    {
        int v = 0;
        for (int i = 0; i < length; i++)
        {
            int c = m[address + i] & 0x7F;
            v = v * 10 + (c >= '0' && c <= '9' ? c - '0' : 0);
        }
        return v;
    }
}
