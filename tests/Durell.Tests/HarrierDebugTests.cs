using Durell.Games;
using Durell.Machine;
using Xunit;
using Xunit.Abstractions;

namespace Durell.Tests;

public class Harrier3DSoundTests
{
    private readonly ITestOutputHelper _out;
    public Harrier3DSoundTests(ITestOutputHelper o) => _out = o;

    /// <summary>Flies a stretch of Harrier Attack 3D (take-off, rockets, a bomb) and checks the new sound is sane.</summary>
    [Fact]
    public void SoundIsAudibleAndClean()
    {
        var info = Catalog.Get("harrier3d");
        var p = info.Create();
        var look = info.Look();
        look.Attach(p);
        (int, int, OricKey)[] keys =
        {
            (450, 455, OricKey.D1), (600, 605, OricKey.Return), (900, 935, OricKey.Up), (960, 990, OricKey.Right),
            (1490, 1530, OricKey.Up), (1700, 1705, OricKey.Space), (1800, 1806, OricKey.Z), (2100, 2106, OricKey.Space),
        };
        var buf = new float[OricMachine.SamplesPerFrame * 2];
        var all = new System.Collections.Generic.List<float>();
        int replaced = 0;
        for (int f = 0; f < 2400; f++)
        {
            p.ClearKeys();
            foreach (var (a, b, k) in keys) if (f >= a && f <= b) p.SetKey(k, true);
            p.RunFrame();
            look.AfterFrame(p);
            p.RenderAudio(buf, true);
            if (look.MixAudio(p, buf, true)) replaced++;
            if (f >= 900) all.AddRange(buf);
        }
        Assert.True(replaced > 1000, $"the 3D sound only played on {replaced} frames");
        float peak = 0;
        double sum = 0;
        foreach (var v in all)
        {
            Assert.False(float.IsNaN(v) || float.IsInfinity(v));
            peak = System.Math.Max(peak, System.Math.Abs(v));
            sum += v * v;
        }
        double rms = System.Math.Sqrt(sum / all.Count);
        _out.WriteLine($"peak {peak:F3} rms {rms:F3}");
        Assert.InRange(rms, 0.01, 0.5);
        var wav = System.Environment.GetEnvironmentVariable("DURELL_WAV");
        if (wav != null) WriteWav(wav, all);
    }

    private static void WriteWav(string path, System.Collections.Generic.List<float> s)
    {
        using var w = new System.IO.BinaryWriter(System.IO.File.Create(path));
        int n = s.Count;
        w.Write("RIFF"u8); w.Write(36 + n * 2); w.Write("WAVEfmt "u8); w.Write(16); w.Write((short)1); w.Write((short)2);
        w.Write(44100); w.Write(44100 * 4); w.Write((short)4); w.Write((short)16); w.Write("data"u8); w.Write(n * 2);
        foreach (var v in s) w.Write((short)(System.Math.Clamp(v, -1, 1) * 32000));
    }
}
