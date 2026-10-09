using System;
using System.Collections.Generic;
using Durell.Graphics;
using Durell.Machine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Durell.Input;

/// <summary>A press on the screen in virtual coordinates.</summary>
public readonly record struct Tap(Vector2 Position);

/// <summary>
/// Per-frame snapshot of keyboard, gamepad, mouse and touch. Menus use the actions (Up, Down,
/// Confirm, Back...); games read the host keyboard mapped onto the Oric keyboard, plus the
/// gamepad and touch buttons mapped by each game's <see cref="Games.Controls"/>.
/// </summary>
public sealed class InputState
{
    private KeyboardState _kb, _prevKb;
    private GamePadState _gp, _prevGp;
    private MouseState _mouse, _prevMouse;
    private bool _touchSeen;
    private readonly HashSet<int> _knownTouches = new(), _liveTouches = new();

    public List<Tap> Taps { get; } = new();
    /// <summary>Fingers (or the held mouse) on the screen this frame, virtual coordinates.</summary>
    public List<Vector2> Touches { get; } = new();
    public Vector2 MousePosition { get; private set; }
    public bool MouseMoved { get; private set; }
    public bool IsMobileLayout { get; set; }
    public bool IsTouchDevice => _touchSeen || IsMobileLayout;
    public int ScrollDelta { get; private set; }
    public GamePadState Pad => _gp;
    /// <summary>Phones and tablets: tilting the device.</summary>
    public Tilt Tilt { get; } = new();
    public KeyboardState Keyboard => _kb;

    /// <summary>The capture tool can inject taps and Oric keys.</summary>
    internal List<Tap> ForcedTaps { get; } = new();
    internal HashSet<OricKey> ForcedKeys { get; } = new();
    /// <summary>The key checker: host keys held as if on the keyboard (they go through the same mapping as real keys).</summary>
    internal HashSet<Keys> ForcedHostKeys { get; } = new();
    /// <summary>The key checker: fingers held on the screen (virtual units).</summary>
    internal List<Vector2> ForcedTouches { get; } = new();
    internal bool ForcedBack, ForcedConfirm;

    // ---- menu actions ----
    public bool Up => Key(Keys.Up) || Button(Buttons.DPadUp) || Stick(0, 1);
    public bool Down => Key(Keys.Down) || Button(Buttons.DPadDown) || Stick(0, -1);
    public bool Left => Key(Keys.Left) || Button(Buttons.DPadLeft) || Stick(-1, 0);
    public bool Right => Key(Keys.Right) || Button(Buttons.DPadRight) || Stick(1, 0);
    public bool Confirm => ForcedConfirm || Key(Keys.Enter) || Key(Keys.Space) || Button(Buttons.A) || Button(Buttons.Start);
    public bool Back => ForcedBack || Key(Keys.Escape) || Key(Keys.Back) || Button(Buttons.B) || Button(Buttons.Back);
    /// <summary>Pause during play: ESC, the gamepad's Start/Back (Android's back key arrives as Back).</summary>
    public bool Pause => ForcedBack || Key(Keys.Escape) || Key(Keys.F1) || Button(Buttons.Start) || Button(Buttons.Back);
    public bool ToggleFullScreen =>
        Key(Keys.F11) || (Key(Keys.Enter) && (_kb.IsKeyDown(Keys.LeftAlt) || _kb.IsKeyDown(Keys.RightAlt)));
    public bool ToggleLook => Key(Keys.F2);

    private bool Stick(int dx, int dy)
    {
        var s = _gp.ThumbSticks.Left;
        var p = _prevGp.ThumbSticks.Left;
        bool now = dx != 0 ? s.X * dx > 0.6f : s.Y * dy > 0.6f;
        bool was = dx != 0 ? p.X * dx > 0.6f : p.Y * dy > 0.6f;
        return now && !was;
    }

    public void Update(Func<Vector2, Vector2> screenToVirtual)
    {
        _prevKb = _kb;
        _prevGp = _gp;
        _prevMouse = _mouse;
        _kb = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        _gp = GamePad.GetState(PlayerIndex.One);
        _mouse = Mouse.GetState();
        Taps.Clear();
        Touches.Clear();
        Taps.AddRange(ForcedTaps);
        ForcedTaps.Clear();
        Touches.AddRange(ForcedTouches);
        Tilt.Update();

        _liveTouches.Clear();
        foreach (var touch in TouchPanel.GetState())
        {
            _touchSeen = true;
            var p = screenToVirtual(touch.Position);
            _liveTouches.Add(touch.Id);
            bool fresh = _knownTouches.Add(touch.Id);
            if (touch.State is TouchLocationState.Pressed or TouchLocationState.Moved) Touches.Add(p);
            if (touch.State == TouchLocationState.Pressed && fresh) Taps.Add(new Tap(p));
            else if (touch.State == TouchLocationState.Released && fresh)
            {
                // pressed and lifted between two frames (a slow frame): still a tap, and held for this frame
                Taps.Add(new Tap(p));
                Touches.Add(p);
            }
        }
        _knownTouches.IntersectWith(_liveTouches);

        var mp = screenToVirtual(new Vector2(_mouse.X, _mouse.Y));
        MouseMoved = _mouse.X != _prevMouse.X || _mouse.Y != _prevMouse.Y;
        MousePosition = mp;
        ScrollDelta = _mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;
        if (!_touchSeen)
        {
            if (_mouse.LeftButton == ButtonState.Pressed) Touches.Add(mp);
            if (_mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
                Taps.Add(new Tap(mp));
        }
    }

    public bool Key(Keys k) => _kb.IsKeyDown(k) && !_prevKb.IsKeyDown(k);
    public bool KeyDown(Keys k) => _kb.IsKeyDown(k);
    public bool Button(Buttons b) => _gp.IsButtonDown(b) && !_prevGp.IsButtonDown(b);
    public bool ButtonDown(Buttons b) => _gp.IsButtonDown(b);

    public bool Tapped(RectangleF area)
    {
        foreach (var t in Taps)
            if (area.Contains(t.Position)) return true;
        return false;
    }

    public bool Held(RectangleF area)
    {
        foreach (var t in Touches)
            if (area.Contains(t)) return true;
        return false;
    }

    public bool Hover(RectangleF area) => !IsTouchDevice && area.Contains(MousePosition);

    /// <summary>The host keyboard as Oric keys (every key of the original keyboard has a home), through a game's redefined keys.</summary>
    public void AddOricKeys(HashSet<OricKey> into, KeyBindings? bindings = null)
    {
        foreach (var k in _kb.GetPressedKeys()) AddHost(into, k, bindings);
        foreach (var k in ForcedHostKeys) AddHost(into, k, bindings);
        foreach (var k in ForcedKeys) into.Add(k);
    }

    private static void AddHost(HashSet<OricKey> into, Keys k, KeyBindings? bindings)
    {
        var ok = bindings != null ? bindings.Map(k) : OricKeyboard.Map(k);
        if (ok is OricKey o) into.Add(o);
    }

    /// <summary>A key pressed this frame (for redefining keys), or null.</summary>
    public Keys? NewKey()
    {
        foreach (var k in _kb.GetPressedKeys())
            if (!_prevKb.IsKeyDown(k)) return k;
        return null;
    }
}

/// <summary>Host keys to Oric keyboard positions.</summary>
public static class OricKeyboard
{
    internal static OricKey? Map(Keys k) => k switch
    {
        >= Keys.A and <= Keys.Z => Enum.Parse<OricKey>(k.ToString()),
        >= Keys.D0 and <= Keys.D9 => Enum.Parse<OricKey>(k.ToString()),
        >= Keys.NumPad0 and <= Keys.NumPad9 => Enum.Parse<OricKey>("D" + (k - Keys.NumPad0)),
        Keys.Space => OricKey.Space,
        Keys.Enter => OricKey.Return,
        Keys.Up => OricKey.Up,
        Keys.Down => OricKey.Down,
        Keys.Left => OricKey.Left,
        Keys.Right => OricKey.Right,
        Keys.LeftControl or Keys.RightControl => OricKey.Ctrl,
        Keys.LeftAlt or Keys.RightAlt => OricKey.Funct,
        Keys.Back or Keys.Delete => OricKey.Del,
        Keys.OemComma => OricKey.Comma,
        Keys.OemPeriod => OricKey.Period,
        Keys.OemQuestion => OricKey.Slash,
        Keys.OemSemicolon => OricKey.Semicolon,
        Keys.OemQuotes => OricKey.Quote,
        Keys.OemMinus or Keys.Subtract => OricKey.Minus,
        Keys.OemPlus or Keys.Add => OricKey.Equals,
        Keys.OemOpenBrackets => OricKey.LBracket,
        Keys.OemCloseBrackets => OricKey.RBracket,
        Keys.OemPipe or Keys.OemBackslash => OricKey.Backslash,
        Keys.OemTilde or Keys.Tab => OricKey.Esc,
        Keys.LeftShift => OricKey.LShift,
        Keys.RightShift => OricKey.RShift,
        _ => null,
    };

    /// <summary>The host key that is an Oric key's usual home.</summary>
    internal static Keys? HostKey(OricKey o)
    {
        string n = o.ToString();
        // letters and digits share their names with the host keys (the Oric enum is in matrix order)
        if (n.Length == 1 || n.Length == 2 && n[0] == 'D' && char.IsDigit(n[1]))
            return Enum.TryParse<Keys>(n, out var k) ? k : null;
        return o switch
        {
        OricKey.Space => Keys.Space,
        OricKey.Return => Keys.Enter,
        OricKey.Up => Keys.Up,
        OricKey.Down => Keys.Down,
        OricKey.Left => Keys.Left,
        OricKey.Right => Keys.Right,
        OricKey.Ctrl => Keys.LeftControl,
        OricKey.Funct => Keys.LeftAlt,
        OricKey.Del => Keys.Back,
        OricKey.Comma => Keys.OemComma,
        OricKey.Period => Keys.OemPeriod,
        OricKey.Slash => Keys.OemQuestion,
        OricKey.Semicolon => Keys.OemSemicolon,
        OricKey.Quote => Keys.OemQuotes,
        OricKey.Minus => Keys.OemMinus,
        OricKey.Equals => Keys.OemPlus,
        OricKey.LBracket => Keys.OemOpenBrackets,
        OricKey.RBracket => Keys.OemCloseBrackets,
        OricKey.Backslash => Keys.OemPipe,
        OricKey.Esc => Keys.Tab,
        OricKey.LShift => Keys.LeftShift,
        OricKey.RShift => Keys.RightShift,
        _ => null,
        };
    }
}
