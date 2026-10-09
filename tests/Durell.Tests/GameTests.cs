using Durell.Games;
using Durell.Machine;
using Durell.Persistence;
using Durell.Programs;
using Xunit;

namespace Durell.Tests;

public class GameTests
{
    public static IEnumerable<object[]> Games() => Catalog.All.Select(g => new object[] { g.Id });

    /// <summary>Runs a game with random keys for ten minutes of play: it must never leave its translated code.</summary>
    [Theory]
    [MemberData(nameof(Games))]
    public void RunsWithoutFault(string id)
    {
        var info = Catalog.Get(id);
        var p = info.Demo();
        var rng = new Random(7);
        var keys = Enum.GetValues<OricKey>();
        var held = Array.Empty<OricKey>();
        var audio = new float[OricMachine.SamplesPerFrame * 2];
        for (int f = 0; f < 30000; f++)
        {
            if (rng.Next(10) == 0) held = Enumerable.Range(0, rng.Next(3)).Select(_ => keys[rng.Next(keys.Length)]).ToArray();
            p.ClearKeys();
            foreach (var k in held) p.SetKey(k, true);
            p.RunFrame();
            if (f % 7 == 0) p.RenderAudio(audio, f % 2 == 0);
            else p.SkipAudio();
            if (p.Faulted)
            {
                var miss = p is OricProgram op ? string.Join(" ", op.Machine.Cpu.Misses.Select(a => a.ToString("X4"))) : "";
                Assert.Fail($"{id} faulted at frame {f}: no code at {miss}");
            }
        }
    }

    /// <summary>
    /// Star Fighter: destroying an enemy that isn't the last in its list runs the list-compaction loop
    /// at $97E6, which the Atmos tape image had overwritten with text (patched in games.json).
    /// </summary>
    [Fact]
    public void StarFighterCombatKillDoesNotFault()
    {
        var p = Catalog.Get("starfighter").Create();
        void Poke(int addr, params byte[] b) => b.CopyTo(p.Memory, addr);
        for (int f = 0; f < 1600; f++)
        {
            p.ClearKeys();
            if (f is >= 700 and <= 705) p.SetKey(OricKey.D3, true);
            if (f is >= 850 and <= 855) p.SetKey(OricKey.Y, true);
            if (f == 950)
            {
                // three enemies right beside the player, then into combat with one in the sights
                Poke(0x0442, 0x14, 0x0B);
                Poke(0x0444, 0x14, 0x0B);
                Poke(0x0446, 0x14, 0x0B);
            }
            if (f is >= 960 and <= 1000) p.SetKey(OricKey.Return, true);
            if (f == 1112) Poke(0x04B0, 0x13, 0x0A);
            if (f is >= 1112 and <= 1135) p.SetKey(OricKey.Space, true);
            p.RunFrame();
            p.SkipAudio();
            Assert.False(p.Faulted, $"faulted at frame {f}");
        }
        Assert.Equal(0x49, p.Memory[0x7F]);    // the kill scored (BCD 0049, low byte first)
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void PicturesAreNotBlank(string id)
    {
        var p = Catalog.Get(id).Demo();
        int colours = p.Index.Select(v => v & 7).Distinct().Count();
        Assert.True(colours >= 2, $"{id} shows a blank screen");
    }

    private static string ScreenText(byte[] m, int row) =>
        new(Enumerable.Range(0, 40).Select(c => (char)(m[0xBB80 + row * 40 + c] & 0x7F)).Select(ch => ch < 32 ? ' ' : ch).ToArray());

    private static GameProgram Relaunch(string id, GameSave save, int frames, params (int, int, OricKey)[] keys)
    {
        var info = Catalog.Get(id);
        var p = info.Create();
        var keeper = info.Scores();
        keeper.Attach(p, save);
        for (int f = 0; f < frames; f++)
        {
            p.ClearKeys();
            foreach (var (a, b, k) in keys)
                if (f >= a && f <= b) p.SetKey(k, true);
            p.RunFrame();
            p.SkipAudio();
            keeper.AfterFrame(p, save);
        }
        return p;
    }

    [Fact]
    public void HarrierHighScoreSurvivesARelaunch()
    {
        var save = new GameSave { Memory = "1234" + "31323334", Best = 12340 };
        var p = Relaunch("harrier", save, 700, (450, 455, OricKey.D1), (600, 605, OricKey.Return));
        Assert.Contains("HIGH SCORE 12340", ScreenText(p.Memory, 0));
        Assert.Equal(12340, Catalog.Get("harrier").Scores().Best(p.Memory));
    }

    [Fact]
    public void GalaxyHighScoreSurvivesARelaunch()
    {
        var save = new GameSave { Memory = "7B00", Best = 1230 };
        var p = Relaunch("galaxy", save, 700, (300, 305, OricKey.D2), (400, 405, OricKey.Space));
        Assert.Contains("HIGH SCORE 001230", ScreenText(p.Memory, 0));
    }

    [Fact]
    public void StarFighterHighScoreSurvivesARelaunch()
    {
        // the skill level the game remembers: as a first game on skill 3 leaves it
        var once = Relaunch("starfighter", new GameSave(), 1100, (700, 705, OricKey.D3), (850, 855, OricKey.Y));
        var save = new GameSave { Memory = "3412" + once.Memory[0x89].ToString("X2"), Best = 1234 };
        var p = Relaunch("starfighter", save, 1100, (700, 705, OricKey.D3), (850, 855, OricKey.Y), (1000, 1002, OricKey.Space));
        Assert.Contains("1234", ScreenText(p.Memory, 0));
        Assert.Equal(1234, Catalog.Get("starfighter").Scores().Best(p.Memory));
    }

    [Fact]
    public void ScubaHighScoreSurvivesARelaunch()
    {
        var save = new GameSave { Memory = "5704", Best = 1111 };
        var p = Relaunch("scuba", save, 1000, (800, 805, OricKey.D1));
        Assert.Equal(1111, Catalog.Get("scuba").Scores().Best(p.Memory));
    }

    [Fact]
    public void TurboScoreTablesSurviveARelaunch()
    {
        // first launch: reach the menu, then put a score in the table as a game would
        var info = Catalog.Get("turbo");
        var save = new GameSave();
        var p = info.Create();
        var keeper = info.Scores();
        keeper.Attach(p, save);
        for (int f = 0; f < 300; f++)
        {
            p.RunFrame();
            p.SkipAudio();
            keeper.AfterFrame(p, save);
        }
        "PAUL   "u8.ToArray().CopyTo(p.Memory, 0x9F5F + 10);
        p.Memory[0x9F5F + 18] = 0x00;
        p.Memory[0x9F5F + 19] = 0x01;          // 256 -> shown as 2560
        p.RunFrame();
        keeper.AfterFrame(p, save);
        Assert.True(keeper.Dirty);
        Assert.Equal(2560, save.Best);
        Assert.Equal("PAUL", save.BestName);
        // relaunch: the table comes back when the menu is reached
        var again = Relaunch("turbo", save, 400);
        Assert.Equal(2560, info.Scores().Best(again.Memory));
        Assert.Equal("PAUL", info.Scores().BestName(again.Memory));
    }

    [Fact]
    public void LunarTopScoreIsKept()
    {
        var save = new GameSave { Best = 4321 };
        var info = Catalog.Get("lunar");
        var p = (Durell.Games.Lunar.LunarProgram)info.Create();
        info.Scores().Attach(p, save);
        Assert.Equal(4321, p.TopScore);
    }

    [Fact]
    public void SaveRoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "durell-test-" + Guid.NewGuid().ToString("N"));
        var store = new SaveStore(dir);
        var s = new SaveData { Look = "Original" };
        s.For("harrier").Best = 990;
        s.For("harrier").Memory = "0099";
        Assert.True(store.Save(s));
        var back = store.Load();
        Assert.Equal("Original", back.Look);
        Assert.Equal(990, back.For("harrier").Best);
        Assert.Equal("0099", back.For("harrier").Memory);
        Directory.Delete(dir, true);
    }
}

public class MenuPreviewTests
{
    public static IEnumerable<object[]> Games() => Catalog.All.Select(g => new object[] { g.Id });

    /// <summary>The menu's live preview and card pictures show each game in the ENHANCED redraw, not the old picture.</summary>
    [Theory]
    [MemberData(nameof(Games))]
    public void PreviewIsRedrawn(string id)
    {
        var info = Catalog.Get(id);
        var look = info.Look();
        info.Demo(look);
        Assert.True(look.SceneActive, $"{id}: the preview moment is not redrawn in the enhanced look");
    }
}

public class ScubaSceneTests
{
    /// <summary>The enhanced redraw keeps the diver on the boat until he dives, then follows him down.</summary>
    [Fact]
    public void DiverFollowsTheGame()
    {
        var info = Catalog.Get("scuba");
        var p = info.Create();
        var look = (ScubaLook)info.Look();
        look.Attach(p);
        for (int f = 0; f <= 1440; f++)
        {
            p.ClearKeys();
            if (f is >= 800 and <= 805) p.SetKey(OricKey.D1, true);
            if (f is >= 1100 and <= 1110) p.SetKey(OricKey.Right, true);
            if (f is >= 1250 and <= 1440) p.SetKey(OricKey.Down, true);
            p.RunFrame();
            p.SkipAudio();
            look.AfterFrame(p);
            float t = (f + 1) / 50.08f;
            if (f == 1150)
            {
                var (_, onBoat) = look.DiverAt(t);
                Assert.True(onBoat, "before the dive the diver should be on the boat");
            }
            if (f is 1300 or 1350 or 1440)
            {
                var (pos, onBoat) = look.DiverAt(t + 1);      // after the glide has caught up
                Assert.False(onBoat);
                int row = p.Memory[0x140E];
                Assert.InRange(pos.Y, (row - 4) * 8 - 10, (row - 4) * 8 + 14);
            }
        }
    }
}

public class LunarKeyTests
{
    /// <summary>As on the Oric, only the last key pressed counts: a quick run of motor keys (tilting) lands on the last one.</summary>
    [Fact]
    public void LastMotorKeyWins()
    {
        var p = (Durell.Games.Lunar.LunarProgram)Catalog.Get("lunar").Create();
        OricKey[] digits = { OricKey.D1, OricKey.D2, OricKey.D3, OricKey.D4, OricKey.D5, OricKey.D6, OricKey.D7, OricKey.D8, OricKey.D9 };
        int f = 0;
        void Run(int frames, OricKey? key)
        {
            for (int i = 0; i < frames; i++, f++)
            {
                p.ClearKeys();
                if (key is OricKey k) p.SetKey(k, true);
                p.RunFrame();
                p.SkipAudio();
            }
        }
        Run(200, null);
        Run(16, OricKey.D1);       // level 1
        Run(84, null);
        Run(11, OricKey.Space);    // volume
        while (!p.Flying && f < 2000) Run(1, null);
        Assert.True(p.Flying);
        foreach (var d in digits) Run(2, d);   // 1..9 within a few frames
        Run(150, OricKey.D9);
        Assert.Equal(9, p.Motors);
    }
}
