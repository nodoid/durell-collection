using System.Collections.Generic;
using Durell.Games.Lunar;
using Durell.Machine;
using Durell.Programs;
using Microsoft.Xna.Framework;
using K = Durell.Machine.OricKey;

namespace Durell.Games;

/// <summary>The six games of the collection.</summary>
internal static class Catalog
{
    private static K[] Keys(params K[] k) => k;

    private static TouchKey Pad(TouchSlot s, params K[] k) => new(s.ToString(), k, s);
    private static TouchKey Act(string label, params K[] k) => new(label, k, TouchSlot.Action);
    private static TouchKey Top(string label, params K[] k) => new(label, k, TouchSlot.Top);
    private static (int, int, K[]) P(int a, int b, params K[] k) => (a, b, k);

    private static List<TouchKey> Arrows() => new()
    {
        Pad(TouchSlot.Up, K.Up), Pad(TouchSlot.Down, K.Down), Pad(TouchSlot.Left, K.Left), Pad(TouchSlot.Right, K.Right),
    };

    public static readonly List<GameInfo> All = new()
    {
        new GameInfo
        {
            Id = "harrier",
            Title = "HARRIER ATTACK",
            Year = "1983",
            Author = "Ronald Jeffs",
            Blurb = "Take off from the carrier, cross the sea and strike at the enemy's ships, guns, " +
                    "buildings and jets with rockets and bombs. Watch the fuel: you must land back on the " +
                    "carrier to complete the mission.",
            Create = () => new OricProgram("harrier"),
            Look = () => new HarrierLook(),
            Accent = new Color(80, 200, 255),
            PreviewFrames = 1010,
            PreviewKeys = new[] { P(450, 455, K.D1), P(600, 605, K.Return), P(900, 935, K.Up), P(960, 990, K.Right) },
            Scores = () => new HarrierScores(),
            Controls = new Controls
            {
                Up = Keys(K.Up), Down = Keys(K.Down), Left = Keys(K.Left), Right = Keys(K.Right),
                A = Keys(K.Space), B = Keys(K.Z), X = Keys(K.Return), Y = Keys(K.D1),
                TiltHelp = "TILT    left / right: slower / faster;  away / towards you: climb / descend",
                Touch = new List<TouchKey>(Arrows())
                {
                    Act("BOMBS", K.Z),
                    Top("1", K.D1), Top("2", K.D2), Top("3", K.D3), Top("4", K.D4), Top("5", K.D5),
                    Top("0", K.D0), Top("RETURN", K.Return),
                },
                TapFire = Keys(K.Space),
                Actions = new[] { ("Take off / climb", K.Up), ("Descend", K.Down), ("Slower", K.Left), ("Faster", K.Right), ("Fire rockets", K.Space), ("Drop bombs", K.Z) },
                Help = new[]
                {
                    "1 - 5   choose the skill level",
                    "0       sound level (then arrows, RETURN)",
                    "UP      take off, climb",
                    "DOWN    descend",
                    "RIGHT / LEFT  faster / slower",
                    "SPACE   fire rockets",
                    "Z to /  drop bombs",
                },
            },
        },
        new GameInfo
        {
            Id = "harrier3d",
            Title = "HARRIER ATTACK 3D",
            Year = "1983",
            Author = "Ronald Jeffs",
            Blurb = "Harrier Attack in 3D: the same mission, the same original game, flown from a chase " +
                    "camera behind your jet - over the sea and through the valley, past the guns and the town, " +
                    "and back down onto the carrier's deck. New sound effects throughout.",
            Create = () => new OricProgram("harrier"),
            Look = () => new Harrier3DLook(),
            Accent = new Color(120, 230, 160),
            PreviewFrames = 1010,
            PreviewKeys = new[] { P(450, 455, K.D1), P(600, 605, K.Return), P(900, 935, K.Up), P(960, 990, K.Right) },
            Scores = () => new HarrierScores(),
            Controls = new Controls
            {
                Up = Keys(K.Up), Down = Keys(K.Down), Left = Keys(K.Left), Right = Keys(K.Right),
                A = Keys(K.Space), B = Keys(K.Z), X = Keys(K.Return), Y = Keys(K.D1),
                TiltHelp = "TILT    left / right: slower / faster;  away / towards you: climb / descend",
                Touch = new List<TouchKey>(Arrows())
                {
                    Act("BOMBS", K.Z),
                    Top("1", K.D1), Top("2", K.D2), Top("3", K.D3), Top("4", K.D4), Top("5", K.D5),
                    Top("0", K.D0), Top("RETURN", K.Return),
                },
                TapFire = Keys(K.Space),
                Actions = new[] { ("Take off / climb", K.Up), ("Descend", K.Down), ("Slower", K.Left), ("Faster", K.Right), ("Fire rockets", K.Space), ("Drop bombs", K.Z) },
                Help = new[]
                {
                    "1 - 5   choose the skill level",
                    "0       sound level (then arrows, RETURN)",
                    "UP      take off, climb",
                    "DOWN    descend",
                    "RIGHT / LEFT  faster / slower",
                    "SPACE   fire rockets",
                    "Z to /  drop bombs",
                },
            },
        },
        new GameInfo
        {
            Id = "scuba",
            Title = "SCUBA DIVE",
            Year = "1983",
            Author = "Ronald Jeffs",
            Blurb = "Dive from your boat to the sea bed, gather pearls from the oysters and explore the " +
                    "caverns, but mind the sharks, jellyfish and squid - and the octopus guarding the caves. " +
                    "Get back to the boat before your air runs out.",
            Create = () => new OricProgram("scuba"),
            Look = () => new ScubaLook(),
            Accent = new Color(60, 220, 200),
            PreviewFrames = 1320,
            PreviewKeys = new[] { P(800, 805, K.D1), P(1100, 1110, K.Right), P(1200, 1210, K.Down), P(1230, 1300, K.Down) },
            Scores = () => new ScubaScores(),
            Controls = new Controls
            {
                Up = Keys(K.Up), Down = Keys(K.Down), Left = Keys(K.Left), Right = Keys(K.Right),
                A = Keys(K.Down), B = Keys(K.Up), X = Keys(K.Return), Y = Keys(K.D1),
                TiltHelp = "TILT    swim: tip the device the way you want to go",
                TiltOneAxis = true,
                Touch = new List<TouchKey>(Arrows())
                {
                    Top("1", K.D1), Top("2", K.D2), Top("3", K.D3), Top("4", K.D4), Top("5", K.D5),
                    Top("0", K.D0), Top("RETURN", K.Return),
                },
                Actions = new[] { ("Swim up", K.Up), ("Swim down", K.Down), ("Swim left", K.Left), ("Swim right", K.Right) },
                Help = new[]
                {
                    "1 - 5   choose the skill level",
                    "0       sound level (then arrows, RETURN)",
                    "RIGHT, then DOWN   dive from the boat",
                    "ARROWS  swim",
                },
            },
        },
        new GameInfo
        {
            Id = "starfighter",
            Title = "STAR FIGHTER",
            Year = "1983",
            Author = "Mike Highfield",
            Blurb = "From the cockpit of your star fighter, hunt down the enemy ships across the galaxy. " +
                    "Line them up in your sights, keep an eye on the long-range scanner, and watch your " +
                    "shields and energy.",
            Create = () => new OricProgram("starfighter"),
            Look = () => new StarFighterLook(),
            Accent = new Color(255, 120, 90),
            PreviewFrames = 1150,
            PreviewKeys = new[] { P(700, 705, K.D3), P(850, 855, K.Y) },
            Scores = () => new StarFighterScores(),
            Controls = new Controls
            {
                Up = Keys(K.Up), Down = Keys(K.Down), Left = Keys(K.Left), Right = Keys(K.Right),
                A = Keys(K.Space), X = Keys(K.Return), Y = Keys(K.D1),
                TiltHelp = "TILT    steer: tip the device left, right, away or towards you",
                Touch = new List<TouchKey>(Arrows())
                {
                                        Top("0", K.D0), Top("1", K.D1), Top("2", K.D2), Top("3", K.D3), Top("4", K.D4),
                    Top("5", K.D5), Top("6", K.D6), Top("7", K.D7), Top("8", K.D8), Top("9", K.D9),
                    Top("Y", K.Y), Top("N", K.N), Top("RETURN", K.Return),
                },
                TapFire = Keys(K.Space),
                Actions = new[] { ("Steer up", K.Up), ("Steer down", K.Down), ("Steer left", K.Left), ("Steer right", K.Right), ("Fire", K.Space), ("Combat (when asked)", K.Return) },
                Help = new[]
                {
                    "0 - 9   choose the skill level",
                    "Y / N   sound on or off",
                    "ARROWS  steer",
                    "SPACE   fire",
                    "RETURN  (between rounds)",
                },
            },
        },
        new GameInfo
        {
            Id = "galaxy",
            Title = "GALAXY",
            Year = "1983",
            Author = "Philip Dierks",
            Blurb = "The alien fleet swoops down in formation. Shoot them out of the sky before their " +
                    "dive-bombing attacks wear down your squadron. From Durell's Galaxy 5 tape.",
            Create = () => new OricProgram("galaxy"),
            Look = () => new GalaxyLook(),
            Accent = new Color(220, 110, 255),
            PreviewFrames = 640,
            PreviewKeys = new[] { P(300, 305, K.D2), P(400, 405, K.Space) },
            Scores = () => new GalaxyScores(),
            Controls = new Controls
            {
                Left = Keys(K.Left), Right = Keys(K.Right), Up = Keys(K.Up), Down = Keys(K.Space),
                A = Keys(K.Up), B = Keys(K.Space), X = Keys(K.Return), Y = Keys(K.D1),
                TiltY = false,
                TiltHelp = "TILT    left / right: move;  touch the picture to shoot",
                Touch = new List<TouchKey>
                {
                    Pad(TouchSlot.Left, K.Left), Pad(TouchSlot.Right, K.Right),
                    Act("SHIELD", K.Space),
                    Top("1", K.D1), Top("2", K.D2), Top("3", K.D3), Top("4", K.D4), Top("SPACE", K.Space),
                },
                TapFire = Keys(K.Up),
                Actions = new[] { ("Move left", K.Left), ("Move right", K.Right), ("Fire", K.Up), ("Shield", K.Space) },
                Help = new[]
                {
                    "1 - 4   choose the skill level",
                    "SPACE   set the volume (title screen)",
                    "LEFT / RIGHT  move",
                    "UP      fire",
                    "SPACE   shield (you have a few)",
                },
            },
        },
        new GameInfo
        {
            Id = "lunar",
            Title = "LUNAR LANDER",
            Year = "1983",
            Author = "Robert White",
            Blurb = "Bring your module down gently on the landing pad. Set the motors with the number " +
                    "keys - 0 is off, 9 is full power - and land with the least time and the most fuel " +
                    "for the best score. From Durell's Galaxy 5 tape.",
            Create = () => new LunarProgram(),
            Look = () => new LunarLook(),
            Accent = new Color(255, 210, 90),
            PreviewFrames = 760,
            PreviewKeys = new[] { P(200, 215, K.D2), P(300, 310, K.Space), P(520, 700, K.D4) },
            Scores = () => new LunarScores(),
            Controls = new Controls
            {
                Up = Keys(K.D9), Down = Keys(K.D0), Left = Keys(K.D3), Right = Keys(K.D6),
                A = Keys(K.D5), B = Keys(K.D0), X = Keys(K.Space), Y = Keys(K.Y),
                TiltX = false, TiltY = false, TiltPower = true,
                TiltHelp = "TILT    tip the top towards you for more motor power (0 to 9)",
                RightShoulder = Keys(K.D9), LeftShoulder = Keys(K.D0),
                Touch = new List<TouchKey>
                {
                    Act("0", K.D0), Act("3", K.D3), Act("5", K.D5), Act("7", K.D7), Act("9", K.D9),
                    Top("1", K.D1), Top("2", K.D2), Top("3", K.D3), Top("4", K.D4), Top("SPACE", K.Space),
                    Top("Y", K.Y), Top("N", K.N),
                },
                Actions = new[] { ("Motors off (0)", K.D0), ("Motors 1", K.D1), ("Motors 2", K.D2), ("Motors 3", K.D3), ("Motors 4", K.D4),
                    ("Motors 5", K.D5), ("Motors 6", K.D6), ("Motors 7", K.D7), ("Motors 8", K.D8), ("Motors full (9)", K.D9), ("Volume / start", K.Space) },
                Help = new[]
                {
                    "1 - 4   choose the level",
                    "SPACE   set the volume",
                    "0 - 9   motor power (0 off, 9 full)",
                    "Y / N   another landing?",
                },
            },
        },
        new GameInfo
        {
            Id = "turbo",
            Title = "TURBO ESPRIT",
            Year = "1986",
            Author = "Mike Richardson",
            Blurb = "Drive your Lotus Esprit Turbo through the city streets to stop the drug smugglers' " +
                    "delivery cars before they reach the dealers - without crashing, running red lights " +
                    "or knocking down pedestrians. Oric conversion of the 1986 Spectrum classic.",
            Create = () => new OricProgram("turbo"),
            Look = () => new TurboLook(),
            Accent = new Color(255, 80, 80),
            PreviewFrames = 980,
            PreviewKeys = new[] { P(200, 205, K.D8), P(400, 405, K.D1), P(420, 425, K.Return), P(600, 610, K.D1), P(700, 980, K.S) },
            Scores = () => new TurboScores(),
            Controls = new Controls
            {
                Left = Keys(K.J), Right = Keys(K.L), Up = Keys(K.S), Down = Keys(K.A),
                A = Keys(K.K), B = Keys(K.A), X = Keys(K.M), Y = Keys(K.T),
                TiltHelp = "TILT    left / right: steer;  away / towards you: faster / slower",
                RightShoulder = Keys(K.S), LeftShoulder = Keys(K.A),
                Touch = new List<TouchKey>
                {
                    Pad(TouchSlot.Left, K.J), Pad(TouchSlot.Right, K.L), Pad(TouchSlot.Up, K.S), Pad(TouchSlot.Down, K.A),
                    Act("MAP", K.M),
                    Top("1", K.D1), Top("2", K.D2), Top("3", K.D3), Top("4", K.D4),
                    Top("5", K.D5), Top("6", K.D6), Top("7", K.D7), Top("8", K.D8), Top("T", K.T),
                },
                TapFire = Keys(K.K),
                Actions = new[] { ("Steer left", K.J), ("Steer right", K.L), ("Faster", K.S), ("Slower", K.A), ("Fire", K.K), ("Map", K.M), ("Give up this life", K.T) },
                Help = new[]
                {
                    "1 - 8   menu (7 practice, 8 play)",
                    "J / L   steer left / right",
                    "S / A   faster / slower",
                    "K       fire",
                    "M       map",
                    "T       give up this life",
                },
            },
        },
    };

    public static GameInfo Get(string id) => All.Find(g => g.Id == id) ?? All[0];
}
