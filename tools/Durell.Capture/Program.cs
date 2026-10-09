using System;
using System.IO;
using Durell;
using Durell.Capture;
using Durell.Persistence;
using Microsoft.Xna.Framework;

// usage: Durell.Capture <look> [out-dir] [--script name]
//   looks: mac, iphone, ipad, android-phone, android-tablet, windows, preview, icon
string lookName = args.Length > 0 ? args[0] : "preview";
string outDir = Path.GetFullPath(args.Length > 1 && !args[1].StartsWith("--") ? args[1] : $"artifacts/capture/{lookName}");
int si = Array.IndexOf(args, "--script");
string script = si >= 0 && si + 1 < args.Length ? args[si + 1] : "stills";
var look = Looks.Get(lookName);
Directory.CreateDirectory(outDir);

string saveDir = Path.Combine(outDir, "save");
if (Directory.Exists(saveDir)) Directory.Delete(saveDir, true);
var save = new SaveData { Look = "Enhanced", Sound = true, Volume = 0.8f, LastGame = "harrier" };
// a lived-in collection: some best scores on the menu
save.For("harrier").Best = 14250; save.For("harrier").Plays = 12; save.For("harrier").BestDate = "2026-10-02";
save.For("scuba").Best = 3450; save.For("scuba").Plays = 7; save.For("scuba").BestDate = "2026-10-03";
save.For("starfighter").Best = 1820; save.For("starfighter").Plays = 5; save.For("starfighter").BestDate = "2026-10-04";
save.For("galaxy").Best = 4130; save.For("galaxy").Plays = 9; save.For("galaxy").BestDate = "2026-10-05";
save.For("lunar").Best = 16400; save.For("lunar").Plays = 6; save.For("lunar").BestDate = "2026-10-06";
save.For("turbo").Best = 2560; save.For("turbo").Plays = 4; save.For("turbo").BestName = "PAUL"; save.For("turbo").BestDate = "2026-10-07";
new SaveStore(saveDir).Save(save);

var director = new Director(outDir, look, script);
DurellGame? running = null;
using var game = new DurellGame(saveDir, director, look.Mobile)
{
    CaptureSize = new Point(look.Width, look.Height),
    SafeInsetsProvider = () => look.Insets,
    // phones and tablets: a hand gently rocking the device (held 40 degrees back from upright)
    TiltProvider = look.Mobile ? () => KeyCheck.Gravity ?? Rocking(running?.Clock ?? 0) : null,
};
running = game;
director.Attach(game);
game.Run();

static Vector3? Rocking(float t)
{
    float back = MathHelper.ToRadians(40 + 6 * MathF.Sin(t * 0.7f)), roll = MathHelper.ToRadians(16 * MathF.Sin(t * 0.45f + 1));
    float y = -MathF.Cos(back), z = -MathF.Sin(back);
    return new Vector3(-y * MathF.Sin(roll), y * MathF.Cos(roll), z);
}

internal sealed record Look(string Name, int Width, int Height, bool Mobile, (int, int, int, int) Insets);

internal static class Looks
{
    public static Look Get(string name) => name switch
    {
        "mac" => new("mac", 2880, 1800, false, (0, 0, 0, 0)),
        "windows" => new("windows", 1920, 1080, false, (0, 0, 0, 0)),
        "iphone" => new("iphone", 2868, 1320, true, (186, 0, 186, 63)),       // 6.9" display, landscape
        "ipad" => new("ipad", 2752, 2064, true, (0, 0, 0, 40)),               // 13" display
        "android-phone" => new("android-phone", 1920, 1080, true, (0, 0, 0, 0)),
        "android-tablet" => new("android-tablet", 2560, 1600, true, (0, 0, 0, 0)),
        "preview" => new("preview", 1280, 800, false, (0, 0, 0, 0)),
        "preview-phone" => new("preview-phone", 1434, 660, true, (93, 0, 93, 31)),
        // the stores' video sizes, rendered natively
        "video-iphone" => new("video-iphone", 1920, 886, true, (124, 0, 124, 42)),
        "video-ipad" => new("video-ipad", 1600, 1200, true, (0, 0, 0, 24)),
        "video-mac" => new("video-mac", 1920, 1080, false, (0, 0, 0, 0)),
        "icon" => new("icon", 1024, 1024, false, (0, 0, 0, 0)),
        _ => throw new ArgumentException("unknown look " + name),
    };
}
