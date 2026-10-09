using System;
using System.Collections.Generic;
using Durell.Machine;
using Microsoft.Xna.Framework.Input;

namespace Durell.Input;

/// <summary>
/// A game's redefined keys: host keyboard keys that press a different Oric key than their own. Each
/// rebound Oric key stops coming from its usual host key, so moving an action moves it rather than
/// adding a second key. Saved per game as Oric key name to host key name.
/// </summary>
public sealed class KeyBindings
{
    private readonly Dictionary<Keys, OricKey> _host = new();
    private readonly HashSet<OricKey> _moved = new();

    public bool IsEmpty => _host.Count == 0;

    public static KeyBindings From(Dictionary<string, string>? saved)
    {
        var b = new KeyBindings();
        if (saved == null) return b;
        foreach (var (oric, host) in saved)
            if (Enum.TryParse<OricKey>(oric, out var ok) && Enum.TryParse<Keys>(host, out var hk))
            {
                b._host[hk] = ok;
                b._moved.Add(ok);
            }
        return b;
    }

    /// <summary>The Oric key a host key presses, or null when it presses none.</summary>
    public OricKey? Map(Keys k)
    {
        if (_host.TryGetValue(k, out var ok)) return ok;
        var own = OricKeyboard.Map(k);
        return own is OricKey o && _moved.Contains(o) ? null : own;
    }

    /// <summary>The host key that presses <paramref name="oric"/> now (null if none does).</summary>
    public Keys? HostFor(OricKey oric)
    {
        foreach (var (h, o) in _host)
            if (o == oric) return h;
        if (_moved.Contains(oric)) return null;
        var own = OricKeyboard.HostKey(oric);
        return own is Keys k && _host.ContainsKey(k) ? null : own;
    }

    /// <summary>A short name for a host key, as the help and the editor show it.</summary>
    public static string Name(Keys k) => k switch
    {
        >= Keys.D0 and <= Keys.D9 => ((int)(k - Keys.D0)).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => "PAD " + (int)(k - Keys.NumPad0),
        Keys.Enter => "RETURN",
        Keys.Space => "SPACE",
        Keys.Back => "BACKSPACE",
        Keys.OemComma => ",",
        Keys.OemPeriod => ".",
        Keys.OemQuestion => "/",
        Keys.OemSemicolon => ";",
        Keys.OemQuotes => "'",
        Keys.OemMinus => "-",
        Keys.OemPlus => "=",
        Keys.OemOpenBrackets => "[",
        Keys.OemCloseBrackets => "]",
        Keys.OemPipe or Keys.OemBackslash => "\\",
        Keys.OemTilde => "`",
        Keys.LeftShift => "L SHIFT",
        Keys.RightShift => "R SHIFT",
        Keys.LeftControl => "L CTRL",
        Keys.RightControl => "R CTRL",
        Keys.LeftAlt => "L ALT",
        Keys.RightAlt => "R ALT",
        _ => k.ToString().ToUpperInvariant(),
    };

    /// <summary>Keys the collection keeps for itself (pause, look, full screen).</summary>
    public static bool Reserved(Keys k) => k is Keys.Escape or Keys.F1 or Keys.F2 or Keys.F11 or (>= Keys.F1 and <= Keys.F24);
}
