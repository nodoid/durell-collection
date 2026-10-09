using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Durell.Machine;
using Durell.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Durell.Capture;

/// <summary>
/// Video scripts ("video-preview", "video-GAME"): the real app playing live - keys pressed through the
/// same input path a player's take - recorded frame by frame at 30 fps (piped to ffmpeg as
/// footage.mkv) with the game sound taken from the app's own audio output (soundtrack.wav).
/// </summary>
internal sealed partial class Director
{
    private bool _video;
    private Process? _ffmpeg;
    private Stream? _ffmpegIn;
    private Color[]? _frame;
    private readonly List<float> _sound = new();
    private long _frames, _sinkCalls, _nonZero;

    private void StartRecording(DurellGame game)
    {
        var size = game.CaptureSize!.Value;
        _frame = new Color[size.X * size.Y];
        var psi = new ProcessStartInfo("ffmpeg",
            $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {size.X}x{size.Y} -r {Fps} -i - " +
            $"-c:v libx264 -preset fast -crf 12 -pix_fmt yuv420p \"{Path.Combine(_outDir, "footage.mkv")}\"")
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        _ffmpeg = Process.Start(psi);
        _ffmpegIn = _ffmpeg!.StandardInput.BaseStream;
        game.Audio.OfflineSink = s =>
        {
            _sinkCalls++;
            foreach (var v in s)
            {
                if (v != 0) _nonZero++;
                _sound.Add(v);
            }
        };
    }

    private void RecordFrame(DurellGame game)
    {
        if (_ffmpegIn == null || _frame == null || game.CaptureTarget == null) return;
        if (_stepIndex < 0 || _stepIndex >= _steps.Count || _steps[_stepIndex].Name == "end") return;
        game.CaptureTarget.GetData(_frame);
        _ffmpegIn.Write(MemoryMarshal.AsBytes(_frame.AsSpan()));
        _frames++;
        // keep the sound in step with the picture (the menu makes none)
        long want = _frames * 44100 / (long)Fps * 2;
        while (_sound.Count < want) _sound.Add(0);
    }

    private void FinishRecording()
    {
        if (_ffmpegIn == null) return;
        _ffmpegIn.Close();
        _ffmpeg!.WaitForExit();
        _ffmpegIn = null;
        // soundtrack.wav: 16-bit stereo 44.1 kHz
        int n = (int)Math.Min(_sound.Count, _frames * 44100 / (long)Fps * 2);
        using var w = new BinaryWriter(File.Create(Path.Combine(_outDir, "soundtrack.wav")));
        w.Write("RIFF"u8); w.Write(36 + n * 2); w.Write("WAVEfmt "u8); w.Write(16); w.Write((short)1); w.Write((short)2);
        w.Write(44100); w.Write(44100 * 4); w.Write((short)4); w.Write((short)16); w.Write("data"u8); w.Write(n * 2);
        for (int i = 0; i < n; i++) w.Write((short)(Math.Clamp(_sound[i], -1f, 1f) * 30000));
        Console.WriteLine($"  sound: {_sinkCalls} buffers, {_nonZero} non-zero samples, {_sound.Count} total");
        Console.WriteLine($"  video: {_frames} frames ({_frames / Fps:F1} s) -> {_outDir}");
    }

    private Step End() => new("end", 0.1f, _ => FinishRecording());

    /// <summary>A live stretch of a game: fast-forwarded to an interesting moment, then played live with held keys.</summary>
    private Step Live(string name, string id, float seconds, int frames, (int, int, OricKey[])[] start, Func<float, Keys[]> keys, Action<int, byte[]>? poke = null) =>
        new(name, seconds, Play(id, true, frames, poke, start), null, (g, t) =>
        {
            g.Input.ForcedHostKeys.Clear();
            foreach (var k in keys(t)) g.Input.ForcedHostKeys.Add(k);
        });

    private static bool Pulse(float t, float period, float on) => t % period < on;

    private static readonly (int, int, OricKey[])[] HarrierStart =
        { K(450, 455, OricKey.D1), K(600, 605, OricKey.Return), K(900, 935, OricKey.Up), K(960, 990, OricKey.Right), K(1490, 1530, OricKey.Up) };
    private static readonly (int, int, OricKey[])[] TurboStart =
        { K(200, 205, OricKey.D8), K(400, 405, OricKey.D1), K(420, 425, OricKey.Return), K(600, 610, OricKey.D1), K(700, 980, OricKey.S) };

    private static void SfEnemies(int f, byte[] m)
    {
        if (f == 950) foreach (int a in new[] { 0x0442, 0x0444, 0x0446 }) { m[a] = 0x14; m[a + 1] = 0x0B; }
    }

    // ---- what each game does while it is filmed
    private static Keys[] HarrierKeys(float t) =>
        t switch
        {
            < 1.2f => new[] { Keys.Right },
            _ when Pulse(t, 1.6f, 0.5f) => new[] { Keys.Space },
            _ when Pulse(t + 0.8f, 3.2f, 0.3f) => new[] { Keys.Z },
            _ when Pulse(t, 4f, 0.25f) => new[] { Keys.Up },
            _ => Array.Empty<Keys>(),
        };

    private static Keys[] ScubaKeys(float t) => (t % 6) switch { < 2 => new[] { Keys.Right }, < 3 => new[] { Keys.Down }, < 5 => new[] { Keys.Left }, _ => new[] { Keys.Up } };
    private static Keys[] TurboKeys(float t) => Pulse(t, 3.5f, 0.4f) ? new[] { Keys.S, Keys.L } : Pulse(t + 1.7f, 3.5f, 0.4f) ? new[] { Keys.S, Keys.J } : Pulse(t, 2.3f, 0.2f) ? new[] { Keys.S, Keys.K } : new[] { Keys.S };
    private static Keys[] GalaxyKeys(float t) => (Pulse(t, 0.6f, 0.15f) ? new[] { Keys.Up } : Array.Empty<Keys>()) is var f && (t % 4) < 2 ? Append(f, Keys.Right) : Append(f, Keys.Left);
    private static Keys[] SfMapKeys(float t) => (t % 5) switch { < 1.5f => new[] { Keys.Left }, < 2 => new[] { Keys.Space }, < 3.5f => new[] { Keys.Up }, _ => new[] { Keys.Space } };
    private static Keys[] SfCombatKeys(float t) => Pulse(t, 1.2f, 0.3f) ? new[] { Keys.Space } : (t % 3) < 1.5f ? new[] { Keys.Left } : new[] { Keys.Right };
    private static Keys[] LunarKeys(float t) => t switch { < 2 => new[] { Keys.D2 }, < 4 => new[] { Keys.D6 }, < 6 => new[] { Keys.D4 }, < 9 => new[] { Keys.D5 }, _ => new[] { Keys.D6 } };

    private static Keys[] Append(Keys[] a, Keys k)
    {
        var r = new Keys[a.Length + 1];
        a.CopyTo(r, 0);
        r[^1] = k;
        return r;
    }

    /// <summary>The 30-second app preview: a tour of the collection.</summary>
    private List<Step> VideoPreviewScript()
    {
        _video = true;
        return new List<Step>
        {
            new("menu", 3.0f, Menu("harrier3d", true)),
            Live("h3d", "harrier3d", 6.0f, 2140, HarrierStart, HarrierKeys),
            Live("scuba", "scuba", 4.0f, 1700, new[] { K(800, 805, OricKey.D1), K(1100, 1110, OricKey.Right), K(1250, 1440, OricKey.Down), K(1450, 1528, OricKey.Right), K(1530, 1700, OricKey.Down) }, ScubaKeys),
            Live("turbo", "turbo", 4.5f, 980, TurboStart, TurboKeys),
            Live("galaxy", "galaxy", 3.5f, 980, new[] { K(300, 305, OricKey.D2), K(400, 405, OricKey.Space) }, GalaxyKeys),
            Live("sf", "starfighter", 3.0f, 1130, new[] { K(700, 705, OricKey.D3), K(850, 855, OricKey.Y), K(960, 1000, OricKey.Return) }, SfCombatKeys, SfEnemies),
            Live("lunar", "lunar", 3.0f, 640, new[] { K(200, 215, OricKey.D2), K(300, 310, OricKey.Space) }, LunarKeys),
            Live("harrier", "harrier", 2.8f, 2140, HarrierStart, HarrierKeys),
            End(),
        };
    }

    /// <summary>A longer look at one game.</summary>
    private List<Step> VideoGameScript(string id)
    {
        _video = true;
        var steps = new List<Step> { new("menu", 2.5f, Menu(id, true)) };
        steps.Add(id switch
        {
            "harrier" or "harrier3d" => Live(id, id, 40, 2000, HarrierStart, HarrierKeys),
            "scuba" => Live(id, id, 40, 1600, new[] { K(800, 805, OricKey.D1), K(1100, 1110, OricKey.Right), K(1250, 1440, OricKey.Down), K(1450, 1528, OricKey.Right), K(1530, 1600, OricKey.Down) }, ScubaKeys),
            "starfighter" => Live(id, id, 40, 1000, new[] { K(700, 705, OricKey.D3), K(850, 855, OricKey.Y) }, t => t < 20 ? SfMapKeys(t) : SfCombatKeys(t)),
            "galaxy" => Live(id, id, 40, 900, new[] { K(300, 305, OricKey.D2), K(400, 405, OricKey.Space) }, GalaxyKeys),
            "lunar" => Live(id, id, 40, 560, new[] { K(200, 215, OricKey.D2), K(300, 310, OricKey.Space) }, LunarKeys),
            _ => Live(id, id, 40, 980, TurboStart, TurboKeys),
        });
        steps.Add(End());
        return steps;
    }
}
