using System;
using System.Collections.Generic;
using Durell.Machine;
using Durell.Programs;

namespace Durell.Games;

/// <summary>An on-screen button for phones and tablets.</summary>
internal sealed record TouchKey(string Label, OricKey[] Keys, TouchSlot Slot);

internal enum TouchSlot
{
    /// <summary>The four-way pad on the left.</summary>
    Up, Down, Left, Right,
    /// <summary>Big action buttons on the right, from the bottom.</summary>
    Action,
    /// <summary>Small keys along the top (menus, options).</summary>
    Top,
}

/// <summary>How a game is played away from a real Oric keyboard.</summary>
internal sealed class Controls
{
    /// <summary>Gamepad: the stick/D-pad directions.</summary>
    public OricKey[] Up = Array.Empty<OricKey>(), Down = Array.Empty<OricKey>(), Left = Array.Empty<OricKey>(), Right = Array.Empty<OricKey>();
    /// <summary>Gamepad buttons A, B, X, Y and the shoulder buttons.</summary>
    public OricKey[] A = Array.Empty<OricKey>(), B = Array.Empty<OricKey>(), X = Array.Empty<OricKey>(), Y = Array.Empty<OricKey>();
    public OricKey[] LeftShoulder = Array.Empty<OricKey>(), RightShoulder = Array.Empty<OricKey>();
    /// <summary>Phones and tablets: tilt left/right and forward/back stand in for the arrows above.</summary>
    public bool TiltX = true, TiltY = true;
    /// <summary>The game takes one direction at a time (Scuba Dive): tilt gives only the stronger axis.</summary>
    public bool TiltOneAxis;
    /// <summary>Tilt sets a 0-9 power level instead (Lunar Lander's motors).</summary>
    public bool TiltPower;
    /// <summary>How tilt plays this game (the help screens).</summary>
    public string TiltHelp = "";
    /// <summary>Touch buttons.</summary>
    public List<TouchKey> Touch = new();
    /// <summary>Phones and tablets: the keys a touch on the game picture holds (fire); empty for games with no fire.</summary>
    public OricKey[] TapFire = Array.Empty<OricKey>();
    /// <summary>When set, the keys a touch on the picture holds depend on the game's state (Scuba Dive: launch, then dive).</summary>
    public Func<byte[], OricKey[]>? TapKeys;
    /// <summary>What each of the game's keys does in play (the keys the player can redefine).</summary>
    public (string Label, OricKey Key)[] Actions = Array.Empty<(string, OricKey)>();
    /// <summary>The keys, as the original's instructions gave them (shown on the menu and the pause screen).</summary>
    public string[] Help = Array.Empty<string>();
}

/// <summary>One game of the collection.</summary>
internal sealed class GameInfo
{
    public string Id = "";
    public string Title = "";
    public string Year = "";
    public string Author = "";
    public string Blurb = "";
    public Func<GameProgram> Create = null!;
    public Func<Look> Look = () => new Look();
    public Controls Controls = new();
    /// <summary>Accent colour for the menu card.</summary>
    public Microsoft.Xna.Framework.Color Accent;
    /// <summary>Frames to run so the menu preview shows the game.</summary>
    public int PreviewFrames = 300;
    /// <summary>Keys pressed on the way (from, to frame): starts a game for the preview.</summary>
    public (int From, int To, OricKey[] Keys)[] PreviewKeys = Array.Empty<(int, int, OricKey[])>();

    /// <summary>
    /// Runs a fresh program up to the preview moment. A <paramref name="look"/> given here follows the
    /// whole run (its probes and scenes), so the preview can be shown in the ENHANCED redraw at once.
    /// </summary>
    public GameProgram Demo(Look? look = null)
    {
        var p = Create();
        look?.Attach(p);
        for (int f = 0; f < PreviewFrames; f++)
        {
            p.ClearKeys();
            foreach (var (a, b, ks) in PreviewKeys)
                if (f >= a && f <= b) foreach (var k in ks) p.SetKey(k, true);
            p.RunFrame();
            p.SkipAudio();
            look?.AfterFrame(p);
        }
        p.ClearKeys();
        return p;
    }
    public Func<ScoreKeeper> Scores = () => new ScoreKeeper();
}
