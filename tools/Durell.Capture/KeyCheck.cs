using System;
using System.Collections.Generic;
using System.IO;
using Durell.Games.Lunar;
using Durell.Graphics;
using Durell.Machine;
using Durell.Programs;
using Durell.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Durell.Capture;

/// <summary>Shared state for the key checker (the capture tool's tilt comes from here when set).</summary>
internal static class KeyCheck
{
    public static Vector3? Gravity;
    public static readonly List<string> Results = new();

    /// <summary>Gravity for a device held <paramref name="back"/> degrees back from upright, rolled <paramref name="roll"/> degrees.</summary>
    public static Vector3 Held(float back, float roll)
    {
        float b = MathHelper.ToRadians(back), r = MathHelper.ToRadians(roll);
        float y = -MathF.Cos(b), z = -MathF.Sin(b);
        return new Vector3(-y * MathF.Sin(r), y * MathF.Cos(r), z);
    }
}

/// <summary>
/// The key checker (script "keycheck"): plays every game live in the real app, pressing each of its keys
/// through the same path a player's keypress takes (host keyboard keys through the game's key bindings,
/// or touches and tilt on a phone layout), and checks the game itself reacted (its memory changed the
/// way that key should change it). Results go to keycheck.txt.
/// </summary>
internal sealed partial class Director
{
    /// <summary>One key to try: how to press it, for how long, and what shows the game took it.</summary>
    private sealed record Phase(string Name, Action<DurellGame, PlayScreen> Press, float PressFor, float WaitFor, Func<byte[], byte[], GameProgram, bool> Took, Action<byte[]>? Before = null);

    private static Phase Key(string name, Keys k, Func<byte[], byte[], GameProgram, bool> took, float press = 0.5f, float wait = 1f) =>
        new(name, (g, _) => g.Input.ForcedHostKeys.Add(k), press, wait, took);

    private static Phase Tilt(string name, float back, float roll, Func<byte[], byte[], GameProgram, bool> took, float press = 0.7f, float wait = 1f) =>
        new(name, (_, _) => KeyCheck.Gravity = KeyCheck.Held(back, roll), press, wait, took);

    private static Phase TouchPicture(string name, Func<byte[], byte[], GameProgram, bool> took) =>
        new(name, (g, ps) => g.Input.ForcedTouches.Add(new Vector2(ps.Picture.Center.X + ps.Picture.Width * 0.15f, ps.Picture.Center.Y - ps.Picture.Height * 0.15f)), 0.5f, 1f, took);

    private static Phase Button(string label, Func<byte[], byte[], GameProgram, bool> took) =>
        new("button " + label, (g, ps) =>
        {
            foreach (var (l, a) in ps.TouchButtons)
                if (l == label)
                {
                    g.Input.ForcedTouches.Add(a.Center);
                    return;
                }
        }, 0.5f, 1f, took);

    private static bool AnyChar(byte[] m, int r0, int r1, Func<byte, bool> f)
    {
        for (int r = r0; r <= r1; r++)
            for (int c = 1; c < 40; c++)
                if (f(m[0xBB80 + r * 40 + c])) return true;
        return false;
    }

    private static bool HiresText(byte[] m, string s)
    {
        for (int c = 0; c + s.Length <= 40; c++)
        {
            int k = 0;
            while (k < s.Length && (m[0xBF68 + c + k] & 0x7F) == s[k]) k++;
            if (k == s.Length) return true;
        }
        return false;
    }

    private List<Step> KeyCheckScript(bool mobile)
    {
        var steps = new List<Step>();
        var harrierStart = new[] { K(450, 455, OricKey.D1), K(600, 605, OricKey.Return), K(900, 935, OricKey.Up), K(960, 990, OricKey.Right), K(1490, 1530, OricKey.Up) };
        foreach (string id in new[] { "harrier", "harrier3d" })
        {
            var phases = mobile
                ? new List<Phase>
                {
                    Tilt("tilt away = climb", 60, 0, (b, m, p) => m[0x2111] < b[0x2111]),
                    Tilt("tilt right = faster", 40, 20, (b, m, p) => m[0x2113] > b[0x2113]),
                    TouchPicture("touch = rockets", (b, m, p) => AnyChar(m, 1, 23, ch => ch == 0x2D)),
                    Button("BOMBS", (b, m, p) => m[0x2124] == 1),
                }
                : new List<Phase>
                {
                    Key("UP = climb", Keys.Up, (b, m, p) => m[0x2111] < b[0x2111]),
                    Key("DOWN = descend", Keys.Down, (b, m, p) => m[0x2111] > b[0x2111], 0.3f),
                    Key("RIGHT = faster", Keys.Right, (b, m, p) => m[0x2113] > b[0x2113]),
                    Key("LEFT = slower", Keys.Left, (b, m, p) => m[0x2113] < b[0x2113]),
                    Key("SPACE = rockets", Keys.Space, (b, m, p) => AnyChar(m, 1, 23, ch => ch == 0x2D)),
                    Key("Z = bomb", Keys.Z, (b, m, p) => m[0x2124] == 1),
                };
            foreach (bool enhanced in new[] { true, false })
                steps.Add(CheckStep(id, enhanced, 2160, harrierStart, phases));
        }
        // redefined keys: rockets moved to Left Ctrl - Ctrl must fire, SPACE must not
        if (!mobile)
            steps.Add(CheckStep("harrier", true, 2160, harrierStart, new List<Phase>
            {
                Key("redefined: SPACE no longer fires", Keys.Space, (b, m, p) => !AnyChar(m, 1, 23, ch => ch == 0x2D), 0.5f, 0.6f),
                Key("redefined: L CTRL fires", Keys.LeftControl, (b, m, p) => AnyChar(m, 1, 23, ch => ch == 0x2D)),
            }, g => g.Save.For("harrier").Keys["Space"] = "LeftControl", g => g.Save.For("harrier").Keys.Clear()));

        // the sea's creatures (20 slots via $16E2): switched off so a jellyfish by the boat can't end the test
        static void ClearSea(byte[] m)
        {
            for (int slot = 1; slot < 41; slot += 2)
            {
                int p = m[0x16E2 + slot] | m[0x16E3 + slot] << 8;
                if (p is >= 0x400 and < 0xBF00) m[p + 0x14] = 0;
            }
        }
        var scubaPhases = mobile
            ? new List<Phase>
            {
                Tilt("tilt right = launch", 40, 20, (b, m, p) => m[0x146C] == 1),
                Tilt("tilt towards you = dive", 20, 0, (b, m, p) => m[0x146C] == 0 && m[0x146D] == 0) with { Before = ClearSea },
                Tilt("tilt left = swim left", 40, -20, (b, m, p) => m[0x140F] < b[0x140F] || m[0x1419] == 3) with { Before = ClearSea },
                Tilt("tilt right = swim right", 40, 20, (b, m, p) => m[0x140F] > b[0x140F] || m[0x1419] == 2) with { Before = ClearSea },
            }
            : new List<Phase>
            {
                Key("RIGHT = launch the boat", Keys.Right, (b, m, p) => m[0x146C] == 1, 0.4f),
                Key("DOWN = dive", Keys.Down, (b, m, p) => m[0x146C] == 0 && m[0x146D] == 0, 0.8f) with { Before = ClearSea },
                Key("LEFT = swim left", Keys.Left, (b, m, p) => m[0x140F] < b[0x140F] || m[0x1419] == 3) with { Before = ClearSea },
                Key("RIGHT = swim right", Keys.Right, (b, m, p) => m[0x140F] > b[0x140F] || m[0x1419] == 2) with { Before = ClearSea },
                Key("DOWN = swim down", Keys.Down, (b, m, p) => m[0x140E] > b[0x140E] || m[0x1419] == 4),
                Key("UP = swim up", Keys.Up, (b, m, p) => m[0x140E] < b[0x140E] || m[0x1419] == 5),
            };
        foreach (bool enhanced in new[] { true, false })
            steps.Add(CheckStep("scuba", enhanced, 1090, new[] { K(800, 805, OricKey.D1) }, scubaPhases));

        void Enemies(byte[] m)
        {
            foreach (int a in new[] { 0x0442, 0x0444, 0x0446 }) { m[a] = 0x14; m[a + 1] = 0x0B; }
        }
        var sfPhases = mobile
            ? new List<Phase>
            {
                Tilt("tilt away = up", 60, 0, (b, m, p) => m[0x0417] != b[0x0417]),
                Tilt("tilt left = left", 40, -20, (b, m, p) => m[0x0416] != b[0x0416]),
                TouchPicture("touch = fire", (b, m, p) => m[0x78] != 0),
            }
            : new List<Phase>
            {
                Key("UP", Keys.Up, (b, m, p) => m[0x0417] != b[0x0417]),
                Key("DOWN", Keys.Down, (b, m, p) => m[0x0417] != b[0x0417]),
                Key("LEFT", Keys.Left, (b, m, p) => m[0x0416] != b[0x0416]),
                Key("RIGHT", Keys.Right, (b, m, p) => m[0x0416] != b[0x0416]),
                Key("SPACE = fire", Keys.Space, (b, m, p) => m[0x78] != 0),
                new("RETURN = combat", (g, _) => g.Input.ForcedHostKeys.Add(Keys.Enter), 1.2f, 1f, (b, m, p) => m[0xBBD4] != 0x17, Enemies),
            };
        foreach (bool enhanced in new[] { true, false })
            steps.Add(CheckStep("starfighter", enhanced, 1150, new[] { K(700, 705, OricKey.D3), K(850, 855, OricKey.Y) }, sfPhases));

        static int GalaxyX(byte[] m) => m[0xE8] * 2 + m[0xE7];
        var galaxyPhases = mobile
            ? new List<Phase>
            {
                Tilt("tilt left = left", 40, -20, (b, m, p) => GalaxyX(m) < GalaxyX(b)),
                Tilt("tilt right = right", 40, 20, (b, m, p) => GalaxyX(m) > GalaxyX(b)),
                TouchPicture("touch = fire", (b, m, p) => m[0x18] == 0xFF),
                Button("SHIELD", (b, m, p) => m[0x19] > 0),
            }
            : new List<Phase>
            {
                Key("LEFT", Keys.Left, (b, m, p) => GalaxyX(m) < GalaxyX(b)),
                Key("RIGHT", Keys.Right, (b, m, p) => GalaxyX(m) > GalaxyX(b)),
                Key("UP = fire", Keys.Up, (b, m, p) => m[0x18] == 0xFF),
                Key("SPACE = shield", Keys.Space, (b, m, p) => m[0x19] > 0),
            };
        foreach (bool enhanced in new[] { true, false })
            steps.Add(CheckStep("galaxy", enhanced, 980, new[] { K(300, 305, OricKey.D2), K(400, 405, OricKey.Space) }, galaxyPhases));

        static int Motors(GameProgram p) => ((LunarProgram)p).Motors;
        var lunarPhases = mobile
            ? new List<Phase>
            {
                Tilt("tilt towards you = more power", 10, 0, (b, m, p) => Motors(p) >= 6, 0.9f, 1.5f),
                Tilt("held level = motors off", 40, 0, (b, m, p) => Motors(p) == 0, 0.9f, 1.5f),
                Button("9", (b, m, p) => Motors(p) == 9),
            }
            : new List<Phase>
            {
                Key("7 = motors 7", Keys.D7, (b, m, p) => Motors(p) == 7, 0.2f, 1.5f),
                Key("0 = motors off", Keys.D0, (b, m, p) => Motors(p) == 0, 0.2f, 1.5f),
                Key("9 = full power", Keys.D9, (b, m, p) => Motors(p) == 9, 0.2f, 1.5f),
                Key("3 = motors 3", Keys.D3, (b, m, p) => Motors(p) == 3, 0.2f, 1.5f),
            };
        foreach (bool enhanced in new[] { true, false })
            steps.Add(CheckStep("lunar", enhanced, 600, new[] { K(200, 215, OricKey.D1), K(300, 310, OricKey.Space) }, lunarPhases));

        var turboStart = new[] { K(200, 205, OricKey.D8), K(400, 405, OricKey.D1), K(420, 425, OricKey.Return), K(600, 610, OricKey.D1), K(700, 980, OricKey.S) };
        var turboPhases = mobile
            ? new List<Phase>
            {
                Tilt("tilt towards you = slower", 20, 0, (b, m, p) => (sbyte)m[0xFE02] < (sbyte)b[0xFE02], 1.5f, 1.5f),
                Tilt("tilt away = faster", 60, 0, (b, m, p) => (sbyte)m[0xFE02] > (sbyte)b[0xFE02], 1.5f, 1.5f),
                Tilt("tilt right = steer", 40, 20, (b, m, p) => m[0x7792] != b[0x7792] || m[0x7799] != b[0x7799], 1.5f, 1.5f),
                TouchPicture("touch = fire", (b, m, p) => (m[0xFE05] & 4) != 0),
                Button("MAP", (b, m, p) => (m[0x7610] & 1) != 0),
            }
            : new List<Phase>
            {
                Key("A = slower", Keys.A, (b, m, p) => (sbyte)m[0xFE02] < (sbyte)b[0xFE02], 1.5f, 1.5f),
                Key("S = faster", Keys.S, (b, m, p) => (sbyte)m[0xFE02] > (sbyte)b[0xFE02], 1.5f, 1.5f),
                Key("L = steer right", Keys.L, (b, m, p) => m[0x7792] != b[0x7792] || m[0x7799] != b[0x7799], 1.5f, 1.5f),
                Key("J = steer left", Keys.J, (b, m, p) => m[0x7792] != b[0x7792] || m[0x7799] != b[0x7799], 1.5f, 1.5f),
                Key("K = fire", Keys.K, (b, m, p) => (m[0xFE05] & 4) != 0),
                Key("M = map", Keys.M, (b, m, p) => (m[0x7610] & 1) != 0, 0.5f, 2f),
                Key("M = back to the road", Keys.M, (b, m, p) => (m[0x7610] & 1) == 0, 0.5f, 2f),
                Key("T = give up this life", Keys.T, (b, m, p) => m[0x73FF] < b[0x73FF], 0.5f, 3f),
            };
        foreach (bool enhanced in new[] { true, false })
            steps.Add(CheckStep("turbo", enhanced, 980, turboStart, turboPhases));

        steps.Add(new Step("report", 0.1f, _ =>
        {
            File.WriteAllLines(Path.Combine(_outDir, "keycheck.txt"), KeyCheck.Results);
            int failed = KeyCheck.Results.FindAll(r => r.StartsWith("FAIL")).Count;
            Console.WriteLine($"keycheck: {KeyCheck.Results.Count - failed} passed, {failed} failed");
        }));
        return steps;
    }

    /// <summary>A game fast-forwarded to its play, then each phase pressed live in turn.</summary>
    private Step CheckStep(string id, bool enhanced, int frames, (int, int, OricKey[])[] start, List<Phase> phases,
        Action<DurellGame>? before = null, Action<DurellGame>? after = null)
    {
        float total = 0.5f;
        foreach (var ph in phases) total += ph.PressFor + ph.WaitFor;
        int current = -1;
        float phaseStart = 0;
        byte[] snap = Array.Empty<byte>();
        bool passed = false;
        string look = enhanced ? "enhanced" : "original";
        return new Step($"keys-{id}-{look}", total + 0.2f, g =>
        {
            KeyCheck.Gravity = KeyCheck.Held(40, 0);
            before?.Invoke(g);
            Play(id, enhanced, frames, start)(g);
            current = -1;
        }, null, (g, t) =>
        {
            g.Input.ForcedHostKeys.Clear();
            g.Input.ForcedTouches.Clear();
            KeyCheck.Gravity = KeyCheck.Held(40, 0);
            if (g.CurrentScreen is not PlayScreen ps) return;
            var p = ps.Program;
            var m = p.Memory;
            if (t < 0.5f || current >= phases.Count) return;
            // move to the next phase when this one's time is up
            if (current < 0 || t - phaseStart >= phases[current].PressFor + phases[current].WaitFor)
            {
                if (current >= 0 && !passed)
                {
                    string detail = id switch
                    {
                        "scuba" => $"row {m[0x140E]:X2} col {m[0x140F]:X2} state {m[0x1416]:X2} wait {m[0x146C]:X2}/{m[0x146D]:X2}",
                        "lunar" => $"motors {((LunarProgram)p).Motors} flying {((LunarProgram)p).Flying}",
                        _ => "",
                    };
                    KeyCheck.Results.Add($"FAIL {id} {look} {(g.IsMobile ? "touch" : "keys")}: {phases[current].Name}  [{detail}; tilt pitch {g.Input.Tilt.Pitch:F0} roll {g.Input.Tilt.Roll:F0} avail {g.Input.Tilt.Available} on {g.Save.Tilt}]");
                }
                current++;
                if (current >= phases.Count)
                {
                    after?.Invoke(g);
                    current = phases.Count;
                    return;
                }
                phaseStart = t;
                passed = false;
                phases[current].Before?.Invoke(m);
                snap = (byte[])m.Clone();
            }
            if (current >= phases.Count) return;
            var ph = phases[current];
            if (t - phaseStart < ph.PressFor) ph.Press(g, ps);
            if (Environment.GetEnvironmentVariable("KEYCHECK_TRACE") is string tr && ph.Name.Contains(tr))
                Console.WriteLine($"    t={t - phaseStart:F2} L{(g.Input.Tilt.Left ? 1 : 0)} R{(g.Input.Tilt.Right ? 1 : 0)} U{(g.Input.Tilt.Up ? 1 : 0)} D{(g.Input.Tilt.Down ? 1 : 0)} roll {g.Input.Tilt.Roll:F0} col {m[0x140F]:X2} row {m[0x140E]:X2} key {m[0x1420]:X2} req {m[0x1419]:X2} st {m[0x1416]:X2}");
            if (!passed && ph.Took(snap, m, p))
            {
                passed = true;
                KeyCheck.Results.Add($"pass {id} {look} {(g.IsMobile ? "touch" : "keys")}: {ph.Name}");
            }
        });
    }
}
