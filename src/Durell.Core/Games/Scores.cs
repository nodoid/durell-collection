using Durell.Games.Lunar;
using Durell.Persistence;
using Durell.Programs;

namespace Durell.Games;

// Where each game keeps its high scores (found by reading the translated code).

/// <summary>
/// Harrier Attack: the high score is packed BCD at $20D4 (4 digits, shown with a trailing 0); the
/// menu shows an ASCII copy at $20EC that the game refreshes at the start and end of a mission.
/// </summary>
internal sealed class HarrierScores : ScoreKeeper
{
    public HarrierScores()
    {
        Regions = new[] { (0x20D4, 2), (0x20EC, 4) };
        RestoreFrame = 30;
    }

    public override int Best(byte[] m) => Bcd(m, 0x20D4, 2) * 10;
}

/// <summary>Scuba Dive: 16-bit binary numbers; the high score is at $14BB.</summary>
internal sealed class ScubaScores : ScoreKeeper
{
    public ScubaScores()
    {
        Regions = new[] { (0x14BB, 2) };
        RestoreFrame = 30;
    }

    public override int Best(byte[] m) => m[0x14BB] | m[0x14BC] << 8;
}

/// <summary>
/// Star Fighter: packed BCD in zero page; the high score is at $81 (low) / $82 (high). The game keeps
/// it only while the same skill level is chosen ($89 = the last game's skill), so that is kept too.
/// </summary>
internal sealed class StarFighterScores : ScoreKeeper
{
    public StarFighterScores()
    {
        Regions = new[] { (0x81, 2), (0x89, 1) };
        RestoreFrame = 30;
    }

    public override int Best(byte[] m) => Bcd(m, 0x82, 1) * 100 + Bcd(m, 0x81, 1);
}

/// <summary>Galaxy: the high score is binary at $24BE (the score / 10); the top line is drawn from it.</summary>
internal sealed class GalaxyScores : ScoreKeeper
{
    public GalaxyScores()
    {
        Regions = new[] { (0x24BE, 2) };
        RestoreFrame = 30;
    }

    public override int Best(byte[] m) => (m[0x24BE] | m[0x24BF] << 8) * 10;
}

/// <summary>
/// Turbo Esprit: the SCORE and PENALTY tables at $9F5F (8 entries of name(7), skill, value; entry 0
/// is LAST), set up by the program entry (FD11); restored when the main menu (FA1B, $5EBA) is first reached.
/// </summary>
internal sealed class TurboScores : ScoreKeeper
{
    private const int Tables = 0x9F5F;
    public const int MenuProbe = 0x5EBA;

    public TurboScores()
    {
        Regions = new[] { (Tables, 160) };
        RestoreProbe = MenuProbe;
    }

    /// <summary>Entry 1 of the SCORE table (the best); shown x10.</summary>
    public override int Best(byte[] m) => (m[Tables + 18] | m[Tables + 19] << 8) * 10;

    public override string BestName(byte[] m) =>
        System.Text.Encoding.ASCII.GetString(m, Tables + 10, 7).Trim();
}

/// <summary>Lunar Lander: the native port keeps the top score itself.</summary>
internal sealed class LunarScores : ScoreKeeper
{
    public override void Attach(GameProgram p, GameSave save)
    {
        if (p is LunarProgram lp)
        {
            lp.TopScore = save.Best;
            lp.Scored += _ =>
            {
                if (lp.TopScore > save.Best)
                {
                    save.Best = lp.TopScore;
                    save.BestDate = System.DateTime.Now.ToString("yyyy-MM-dd");
                    Dirty = true;
                }
            };
        }
    }
}
