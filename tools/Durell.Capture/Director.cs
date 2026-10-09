using System;
using System.Collections.Generic;
using System.IO;
using Durell.Games;
using Durell.Machine;
using Durell.Screens;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Capture;

/// <summary>
/// Scripts the real app for store assets: a list of steps (a screen, a game fast-forwarded to an
/// interesting moment in either look), held for a while, grabbing stills at set moments.
/// </summary>
internal sealed partial class Director : ICaptureDirector
{
    public const float Fps = 30f;

    private sealed record Shot(float At, string Name);

    private sealed record Step(string Name, float Seconds, Action<DurellGame> Setup, Shot[]? Shots = null, Action<DurellGame, float>? During = null);

    private readonly string _outDir;
    private readonly Look _look;
    private readonly List<Step> _steps;
    private int _stepIndex = -1;
    private float _stepTime;
    private readonly HashSet<string> _taken = new();

    public Director(string outDir, Look look, string script)
    {
        _outDir = outDir;
        _look = look;
        Directory.CreateDirectory(Path.Combine(outDir, "stills"));
        _steps = script switch
        {
            "check" => CheckScript(),
            "keycheck" => KeyCheckScript(look.Mobile),
            "icon" => new List<Step> { new("icon", 0.3f, g => g.ChangeScreen(new IconScreen(g)), new[] { new Shot(0.2f, "icon") }) },
            _ => StillsScript(),
        };
    }

    public float FixedDelta => 1f / Fps;

    public void Attach(DurellGame game)
    {
        game.Audio.Muted = true;
    }

    // ------------------------------------------------------------------ building blocks

    private static Action<DurellGame> Menu(string id, bool enhanced) => g =>
    {
        g.SetEnhanced(enhanced);
        g.Save.LastGame = id;
        while (!g.Previews.Prepare()) { }
        g.ChangeScreen(new MenuScreen(g));
    };

    /// <summary>A game fast-forwarded with a key script: (from, to, keys) in Oric frames.</summary>
    private static Action<DurellGame> Play(string id, bool enhanced, int frames, params (int From, int To, OricKey[] Keys)[] script) =>
        Play(id, enhanced, frames, null, script);

    /// <summary>As <see cref="Play(string, bool, int, (int, int, OricKey[])[])"/>, with memory pokes on the way (frame, memory).</summary>
    private static Action<DurellGame> Play(string id, bool enhanced, int frames, Action<int, byte[]>? poke, params (int From, int To, OricKey[] Keys)[] script) => g =>
    {
        g.SetEnhanced(enhanced);
        var ps = new PlayScreen(g, Catalog.Get(id)) { ShowHints = false };
        g.ChangeScreen(ps);
        var p = ps.Program;
        for (int f = 0; f < frames; f++)
        {
            p.ClearKeys();
            foreach (var (a, b, ks) in script)
                if (f >= a && f <= b) foreach (var k in ks) p.SetKey(k, true);
            poke?.Invoke(f, p.Memory);
            p.RunFrame();
            p.SkipAudio();
        }
        p.ClearKeys();
    };

    private static (int, int, OricKey[]) K(int a, int b, params OricKey[] k) => (a, b, k);

    // ------------------------------------------------------------------ scripts

    /// <summary>Every game and the menu in both looks (development check).</summary>
    private List<Step> CheckScript()
    {
        var s = new List<Step>
        {
            new("menu", 1.2f, Menu("harrier", true), new[] { new Shot(1.0f, "menu-enhanced") }),
        new("scuba-live", 6f, Play("scuba", true, 1090, K(800, 805, OricKey.D1)),
            new[] { new Shot(1.5f, "scuba-live-1"), new Shot(2.9f, "scuba-live-2"), new Shot(3.3f, "scuba-live-3"), new Shot(3.8f, "scuba-live-4") },
            (g, t) =>
            {
                // keys pressed during live play, as a player would
                g.Input.ForcedKeys.Clear();
                if (t is > 0.2f and < 0.6f) g.Input.ForcedKeys.Add(OricKey.Right);
                if (t is > 2.5f and < 5f) g.Input.ForcedKeys.Add(OricKey.Down);
                if (MathF.Abs(t * 4 - MathF.Round(t * 4)) < 0.01f)
                {
                    var m = ((PlayScreen)g.CurrentScreen).Program.Memory;
                    Console.WriteLine($"  scuba-live t={t:F2}: boat {m[0x1467]:X2}/{m[0x1468]:X2} launch {m[0x146C]:X2} diver row {m[0x140E]:X2} state {m[0x1416]:X2}");
                }
            }),
        new("h3d-crash", 14f, Play("harrier3d", true, 1500, K(450, 455, OricKey.D1), K(600, 605, OricKey.Return), K(900, 935, OricKey.Up), K(960, 990, OricKey.Right)),
            new[] { new Shot(1.5f, "h3d-crash-1"), new Shot(4f, "h3d-crash-2"), new Shot(8f, "h3d-crash-3"), new Shot(13f, "h3d-crash-4") },
            (g, t) =>
            {
                if (MathF.Abs(t - MathF.Round(t)) < 0.02f && g.CurrentScreen is PlayScreen ps)
                {
                    var m = ps.Program.Memory;
                    Console.WriteLine($"  h3d t={t:F0} phase {m[0x216F]} landed {m[0x217A]} row {m[0x2111] / 2} frame {ps.Program.Frame}");
                }
            }),
        new("keys", 0.8f, g =>
        {
            Menu("harrier", true)(g);
            g.Save.For("harrier").Keys["Space"] = "LeftControl";
            ((MenuScreen)g.CurrentScreen).OpenKeys();
        }, new[] { new Shot(0.6f, "keys-editor") }),
            new("menu-o", 0.8f, Menu("turbo", false), new[] { new Shot(0.6f, "menu-original") }),
        };
        foreach (var info in Catalog.All)
        {
            s.Add(new(info.Id + "-e", 0.5f, Play(info.Id, true, info.PreviewFrames, info.PreviewKeys), new[] { new Shot(0.4f, info.Id + "-enhanced") }));
            s.Add(new(info.Id + "-o", 0.3f, Play(info.Id, false, info.PreviewFrames, info.PreviewKeys), new[] { new Shot(0.25f, info.Id + "-original") }));
        }
        return s;
    }

    private static List<(string, int, (int, int, OricKey[])[])> GameScripts() => new()
    {
        ("harrier", 1500, new[] { K(450, 455, OricKey.D1), K(600, 605, OricKey.Return), K(900, 960, OricKey.Up), K(1000, 1500, OricKey.Right) }),
        ("scuba", 1500, new[] { K(800, 805, OricKey.D1), K(1100, 1110, OricKey.Right), K(1200, 1210, OricKey.Down), K(1300, 1420, OricKey.Down) }),
        ("starfighter", 1300, new[] { K(700, 705, OricKey.D3), K(900, 905, OricKey.Return) }),
        ("galaxy", 1300, new[] { K(300, 305, OricKey.D2), K(400, 405, OricKey.Space), K(900, 960, OricKey.Left) }),
        ("lunar", 1000, new[] { K(200, 215, OricKey.D2), K(300, 310, OricKey.Space), K(520, 1000, OricKey.D5) }),
        ("turbo", 1400, new[] { K(200, 205, OricKey.D8), K(400, 405, OricKey.D1), K(420, 425, OricKey.Return), K(600, 610, OricKey.D1), K(700, 1400, OricKey.S) }),
    };

    /// <summary>The store screenshots, in store order.</summary>
    private List<Step> StillsScript() => new()
    {
        new("menu", 1.5f, Menu("harrier", true), new[] { new Shot(1.4f, "01-collection") }),
        new("harrier", 0.6f, Play("harrier", true, 2160, K(450, 455, OricKey.D1), K(600, 605, OricKey.Return), K(900, 935, OricKey.Up), K(960, 990, OricKey.Right), K(1490, 1530, OricKey.Up), K(2140, 2150, OricKey.Space)), new[] { new Shot(0.5f, "02-harrier") }),
        new("harrier3d", 0.8f, Play("harrier3d", true, 2160, K(450, 455, OricKey.D1), K(600, 605, OricKey.Return), K(900, 935, OricKey.Up), K(960, 990, OricKey.Right), K(1490, 1530, OricKey.Up), K(2140, 2150, OricKey.Space)), new[] { new Shot(0.7f, "09-harrier3d") }),
        new("scuba", 0.6f, Play("scuba", true, 1200, K(800, 805, OricKey.D1), K(1100, 1110, OricKey.Right), K(1200, 1210, OricKey.Down), K(1230, 1380, OricKey.Down), K(1380, 1440, OricKey.Left)), new[] { new Shot(0.5f, "03-scuba") }),
        new("scuba-caves", 0.8f, Play("scuba", true, 1700, K(800, 805, OricKey.D1), K(1100, 1110, OricKey.Right), K(1250, 1440, OricKey.Down), K(1450, 1528, OricKey.Right), K(1530, 1700, OricKey.Down)), new[] { new Shot(0.7f, "10-scuba-caves") }),
        new("starfighter", 0.6f, Play("starfighter", true, 1250, K(700, 705, OricKey.D3), K(850, 855, OricKey.Y), K(1000, 1060, OricKey.Left)), new[] { new Shot(0.5f, "04-starfighter") }),
        new("sf-combat", 0.8f, Play("starfighter", true, 1130, (f, m) =>
        {
            // three enemies right beside the player, so RETURN takes it into combat
            if (f == 950) foreach (int a in new[] { 0x0442, 0x0444, 0x0446 }) { m[a] = 0x14; m[a + 1] = 0x0B; }
        }, K(700, 705, OricKey.D3), K(850, 855, OricKey.Y), K(960, 1000, OricKey.Return)), new[] { new Shot(0.7f, "11-starfighter-combat") }),
        new("galaxy", 0.6f, Play("galaxy", true, 980, K(300, 305, OricKey.D2), K(400, 405, OricKey.Space), K(700, 760, OricKey.Left), K(800, 980, OricKey.Up)), new[] { new Shot(0.5f, "05-galaxy") }),
        new("lunar", 0.6f, Play("lunar", true, 950, K(200, 215, OricKey.D2), K(300, 310, OricKey.Space), K(520, 525, OricKey.D2)), new[] { new Shot(0.5f, "06-lunar") }),
        new("turbo", 0.6f, Play("turbo", true, 1150, K(200, 205, OricKey.D8), K(400, 405, OricKey.D1), K(420, 425, OricKey.Return), K(600, 610, OricKey.D1), K(700, 1150, OricKey.S), K(1000, 1040, OricKey.L)), new[] { new Shot(0.5f, "07-turbo") }),
        new("original", 0.6f, Play("scuba", false, 1460, K(800, 805, OricKey.D1), K(1100, 1110, OricKey.Right), K(1200, 1210, OricKey.Down), K(1230, 1380, OricKey.Down), K(1380, 1440, OricKey.Left)), new[] { new Shot(0.5f, "08-original") }),
        new("done", 0.1f, Menu("harrier", true)),
    };

    // ------------------------------------------------------------------ driving

    public void BeforeUpdate(DurellGame game)
    {
        if (_stepIndex < 0 || _stepTime >= _steps[_stepIndex].Seconds)
        {
            _stepIndex++;
            if (_stepIndex >= _steps.Count)
            {
                game.Exit();
                return;
            }
            _stepTime = 0;
            _steps[_stepIndex].Setup(game);
        }
        _steps[_stepIndex].During?.Invoke(game, _stepTime);
        _stepTime += FixedDelta;
    }

    public void AfterDraw(DurellGame game)
    {
        if (_stepIndex < 0 || _stepIndex >= _steps.Count) return;
        var step = _steps[_stepIndex];
        if (step.Shots == null) return;
        foreach (var shot in step.Shots)
        {
            if (_stepTime < shot.At || !_taken.Add(step.Name + shot.Name)) continue;
            var rt = game.CaptureTarget!;
            string path = Path.Combine(_outDir, "stills", shot.Name + ".png");
            using var fs = File.Create(path);
            rt.SaveAsPng(fs, rt.Width, rt.Height);
            Console.WriteLine("  " + path);
        }
    }
}
