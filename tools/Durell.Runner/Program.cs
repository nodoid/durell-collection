using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Durell.Machine;

// usage: Durell.Runner GAME FRAMES [--keys F1-F2:KEY+KEY]... [--shot F]... [--every N] [--random SEED] [--out DIR] [--wav FILE]
var game = args[0];
int frames = int.Parse(args[1]);
var keys = new List<(int a, int b, OricKey[] k)>();
var shots = new HashSet<int>();
string outDir = Path.Combine("artifacts", "runner", game);
int every = 0;
int? seed = null;
OricKey[]? pool = null;
int randomFrom = 0;
string? wav = null;
int probe = -1;
int probeLen = 50;
string? probeMem = null;
int histAt = -1;
var peeks = new List<string>();
var dumps = new List<string>();
var pokes = new List<string>();
for (int i = 2; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--keys":
            var p = args[++i].Split(':');
            var r = p[0].Split('-');
            keys.Add((int.Parse(r[0]), int.Parse(r[1]), p[1].Split('+').Select(Enum.Parse<OricKey>).ToArray()));
            break;
        case "--shot": shots.Add(int.Parse(args[++i])); break;
        case "--every": every = int.Parse(args[++i]); break;
        case "--random": seed = int.Parse(args[++i]); break;
        case "--pool": pool = args[++i].Split('+').Select(Enum.Parse<OricKey>).ToArray(); break;
        case "--from": randomFrom = int.Parse(args[++i]); break;
        case "--out": outDir = args[++i]; break;
        case "--wav": wav = args[++i]; break;
        case "--probe": probe = int.Parse(args[++i]); break;
        case "--probelen": probeLen = int.Parse(args[++i]); break;
        case "--probemem": probeMem = args[++i]; break;
        case "--hist": histAt = int.Parse(args[++i]); break;
        case "--peek": peeks.Add(args[++i]); break;
        case "--dump": dumps.Add(args[++i]); break;
        case "--poke": pokes.Add(args[++i]); break;
    }
}
Directory.CreateDirectory(outDir);
Directory.CreateDirectory(Path.Combine("originals", "cov"));
if (game == "lunar")
{
    // the native port: screenshots only
    var lp = new Durell.Games.Lunar.LunarProgram();
    var px = new uint[OricVideo.Width * OricVideo.Height];
    var ab = new float[OricMachine.SamplesPerFrame * 2];
    var snd = new List<float>();
    for (int lf = 0; lf < frames; lf++)
    {
        lp.ClearKeys();
        foreach (var (a, b, k) in keys)
            if (lf >= a && lf <= b) foreach (var key in k) lp.SetKey(key, true);
        lp.RunFrame();
        lp.RenderAudio(ab, false);
        if (wav != null) snd.AddRange(ab);
        if (shots.Contains(lf) || (every > 0 && lf % every == 0))
        {
            for (int i = 0; i < px.Length; i++) px[i] = OricVideo.Palette[lp.Index[i] & 7];
            Png.Write(Path.Combine(outDir, $"{lf:D5}.png"), px, OricVideo.Width, OricVideo.Height);
        }
    }
    if (wav != null) Wav.Write(wav, snd);
    Console.WriteLine($"lunar: top score {lp.TopScore}");
    return;
}
var m = ProgramCatalog.Create(game);
m.Cpu.TraceCodeWrites = true;
var rng = seed is int s0 ? new Random(s0) : null;
var randomKeys = Enum.GetValues<OricKey>();
if (pool != null) randomKeys = pool;
OricKey[] held = Array.Empty<OricKey>();
var audio = new List<float>();
var buf = new float[OricMachine.SamplesPerFrame * 2];
var rgba = new uint[OricVideo.Width * OricVideo.Height];
var sw = System.Diagnostics.Stopwatch.StartNew();
int f = 0;
try
{
    for (; f < frames; f++)
    {
        m.Bus.ClearKeys();
        foreach (var (a, b, k) in keys)
            if (f >= a && f <= b) foreach (var key in k) m.Bus.SetKey(key, true);
        if (rng != null && f >= randomFrom)
        {
            if (rng.Next(12) == 0)
                held = Enumerable.Range(0, rng.Next(3)).Select(_ => randomKeys[rng.Next(randomKeys.Length)]).ToArray();
            foreach (var key in held) m.Bus.SetKey(key, true);
        }
        if (f == probe)
        {
            Probe(m, probeLen, probeMem);
            break;
        }
        foreach (var pk in pokes)
        {
            var a = pk.Split('@');           // ADDR:HEX@FRAME
            if (int.Parse(a[1]) != f) continue;
            var r = a[0].Split(':');
            var bytes = Convert.FromHexString(r[1]);
            bytes.CopyTo(m.M, Convert.ToInt32(r[0], 16));
        }
        m.RunFrame();
        m.RenderAudio(buf, false);
        if (wav != null) audio.AddRange(buf);
        foreach (var dp in dumps)
        {
            var a = dp.Split('@');           // PATH@FRAME
            if (int.Parse(a[1]) == f) File.WriteAllBytes(a[0], m.M);
        }
        if (f == histAt)
        {
            var seen = new Dictionary<int, int>();
            for (int k = 0; k < 64; k++) { int a = m.Cpu.History[k]; seen[a] = seen.GetValueOrDefault(a) + 1; }
            Console.WriteLine($"frame {f} loop: " + string.Join(" ", seen.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key:X4}x{kv.Value}")));
        }
        foreach (var pk in peeks)
        {
            // ADDR[:LEN]@FRAME
            var a = pk.Split('@');
            if (int.Parse(a[1]) != f) continue;
            var r = a[0].Split(':');
            int addr = Convert.ToInt32(r[0], 16), len = r.Length > 1 ? int.Parse(r[1]) : 16;
            Console.WriteLine($"frame {f} ${addr:X4}: " + string.Join(" ", Enumerable.Range(addr, len).Select(x => m.M[x].ToString("X2"))));
        }
        if (shots.Contains(f) || (every > 0 && f % every == 0))
        {
            m.Video.ToRgba(rgba);
            Png.Write(Path.Combine(outDir, $"{f:D5}.png"), rgba, OricVideo.Width, OricVideo.Height);
        }
    }
}
catch (Cpu6502.UntranslatedCodeException e)
{
    Console.WriteLine($"frame {f}: {e.Message}  (S={m.Cpu.S:X2} A={m.Cpu.A:X2} X={m.Cpu.X:X2} Y={m.Cpu.Y:X2})");
    Console.WriteLine("  came from: " + string.Join(" ", Enumerable.Range(0, 24).Select(k => m.Cpu.History[(m.Cpu.HistoryPos - 1 - k) & 63].ToString("X4"))));
    Console.WriteLine("  stack: " + string.Join(" ", Enumerable.Range(m.Cpu.S + 1, 12).Where(a => a < 256).Select(a => m.M[0x100 + a].ToString("X2"))));
    var cov = Path.Combine("originals", "cov", game + ".entries");
    Directory.CreateDirectory(Path.GetDirectoryName(cov)!);
    File.AppendAllText(cov, $"{e.Address:X4}\n");
    m.Video.ToRgba(rgba);
    Png.Write(Path.Combine(outDir, "miss.png"), rgba, OricVideo.Width, OricVideo.Height);
    Environment.ExitCode = 2;
}
Console.WriteLine($"{game}: {f} frames in {sw.ElapsedMilliseconds} ms");
if (m.Cpu.CodeWrites.Count > 0)
{
    var list = string.Join(" ", m.Cpu.CodeWrites.OrderBy(x => x).Select(x => x.ToString("X4")));
    Console.WriteLine("writes to code: " + list);
    File.AppendAllText(Path.Combine("originals", "cov", game + ".codewrites"), list + "\n");
    File.AppendAllText(Path.Combine("originals", "cov", game + ".codevalues"),
        string.Join(" ", m.Cpu.CodeWriteValues.OrderBy(x => x).Select(x => $"{x >> 8:X4}:{x & 0xFF:X2}")) + "\n");
}
if (wav != null) Wav.Write(wav, audio);

static void Probe(OricMachine m, int len, string? mem)
{
    // which keys change the game, compared with pressing nothing?
    var st = m.Save();
    byte[] RunWith(OricKey? k, int frames)
    {
        m.Load(st);
        for (int i = 0; i < frames; i++)
        {
            m.Bus.ClearKeys();
            if (k is OricKey kk) m.Bus.SetKey(kk, true);
            m.RunFrame();
            m.SkipAudio();
        }
        return (byte[])m.Video.Index.Clone();
    }
    var basePic = RunWith(null, len);
    var memBase = (byte[])m.M.Clone();
    int ma = 0, ml = 0;
    if (mem != null)
    {
        var mp = mem.Split(':');
        ma = Convert.ToInt32(mp[0], 16);
        ml = int.Parse(mp[1]);
        Console.WriteLine($"  {"(none)",-10} " + string.Join(" ", Enumerable.Range(ma, ml).Select(x => memBase[x].ToString("X2"))));
    }
    foreach (var k in Enum.GetValues<OricKey>())
    {
        try
        {
            var pic = RunWith(k, len);
            int d = 0;
            for (int i = 0; i < pic.Length; i++) if (pic[i] != basePic[i]) d++;
            if (mem != null)
            {
                bool diff = Enumerable.Range(ma, ml).Any(x => m.M[x] != memBase[x]);
                if (diff) Console.WriteLine($"  {k,-10} " + string.Join(" ", Enumerable.Range(ma, ml).Select(x => m.M[x].ToString("X2"))));
            }
            else if (d > 0) Console.WriteLine($"  {k,-10} {d,6} pixels differ");
        }
        catch (Cpu6502.UntranslatedCodeException e)
        {
            Console.WriteLine($"  {k,-10} MISS {e.Address:X4}");
        }
    }
}

internal static class Png
{
    public static void Write(string path, uint[] abgr, int w, int h)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        Be(ihdr, 0, w);
        Be(ihdr, 4, h);
        ihdr[8] = 8;
        ihdr[9] = 6;
        Chunk(fs, "IHDR", ihdr);
        var raw = new MemoryStream();
        for (int y = 0; y < h; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < w; x++)
            {
                uint c = abgr[y * w + x];
                raw.WriteByte((byte)c);
                raw.WriteByte((byte)(c >> 8));
                raw.WriteByte((byte)(c >> 16));
                raw.WriteByte((byte)(c >> 24));
            }
        }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) raw.WriteTo(zs);
        Chunk(fs, "IDAT", z.ToArray());
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    private static void Be(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        Be(len, 0, data.Length);
        s.Write(len);
        var td = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
        data.CopyTo(td, 4);
        s.Write(td);
        var crc = new byte[4];
        Be(crc, 0, (int)Crc(td));
        s.Write(crc);
    }

    private static uint Crc(byte[] d)
    {
        uint c = 0xFFFFFFFF;
        foreach (var b in d)
        {
            c ^= b;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        }
        return c ^ 0xFFFFFFFF;
    }
}

internal static class Wav
{
    public static void Write(string path, List<float> stereo)
    {
        using var bw = new BinaryWriter(File.Create(path));
        int n = stereo.Count;
        bw.Write("RIFF"u8.ToArray());
        bw.Write(36 + n * 2);
        bw.Write("WAVEfmt "u8.ToArray());
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)2);
        bw.Write(AyChip.Rate);
        bw.Write(AyChip.Rate * 4);
        bw.Write((short)4);
        bw.Write((short)16);
        bw.Write("data"u8.ToArray());
        bw.Write(n * 2);
        foreach (var v in stereo) bw.Write((short)Math.Clamp(v * 32767, -32768, 32767));
    }
}
