using System;
using System.Collections.Generic;
using Durell.Games;
using Durell.Games.Lunar;
using Durell.Graphics;
using Durell.Input;
using Durell.Machine;
using Durell.Persistence;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Durell.Screens;

/// <summary>
/// Plays one game: runs its program at the Oric's 50 frames a second, feeds it the keyboard
/// (as the Oric's), the gamepad and the touch buttons, shows it in the chosen look with its sound,
/// keeps its high scores, and offers a pause menu.
/// </summary>
internal sealed class PlayScreen : Screen
{
    private const float OricHz = 50.08f;
    private readonly GameInfo _info;
    private readonly GameProgram _program;
    private readonly Look _look;
    private readonly ScoreKeeper _scores;
    private readonly GameSave _save;
    private readonly float[] _audio = new float[OricMachine.SamplesPerFrame * 2];
    private readonly HashSet<OricKey> _keys = new();
    private readonly HashSet<OricKey> _touchKeys = new();
    /// <summary>Where this frame's on-screen buttons are: a touch on the picture anywhere else fires.</summary>
    private readonly List<RectangleF> _buttonAreas = new();
    /// <summary>The key checker: the on-screen buttons drawn last frame (label, area) and the picture.</summary>
    internal readonly List<(string Label, RectangleF Area)> TouchButtons = new();
    internal RectangleF Picture { get; private set; }
    private KeyBindings _bindings;
    private KeysEditor? _keysEditor;
    private int _tiltDir, _tiltGap;
    /// <summary>The QUIT button (top left): returns to the menu.</summary>
    private RectangleF _quitRect;
    internal RectangleF QuitButton => _quitRect;
    private float _acc;
    private bool _paused;
    private int _pauseSel;
    private bool _keyboard;
    private bool _showHelp;
    private float _toast;
    private string _toastText = "";
    private float _hint = 6f;

    /// <summary>The capture tool can keep the game running without the pause hint.</summary>
    internal bool ShowHints { get; set; } = true;
    internal GameProgram Program => _program;

    public PlayScreen(DurellGame game, GameInfo info) : base(game)
    {
        _info = info;
        _program = info.Create();
        _look = info.Look();
        _look.Attach(_program);
        _scores = info.Scores();
        _save = game.Save.For(info.Id);
        _bindings = KeyBindings.From(_save.Keys);
        _scores.Attach(_program, _save);
        _save.Plays++;
        game.SaveSoon();
        game.Input.Tilt.Centre();
    }

    private bool TiltOn => Game.TiltPlays(_info);

    public override void Leave()
    {
        Game.PersistSave();
    }

    public override void OnDeactivated()
    {
        if (!Game.IsMobile && Game.Director == null) return;
        _paused = true;
    }

    // ------------------------------------------------------------------ update

    public override void Update(float dt)
    {
        var inp = Game.Input;
        _hint -= dt;
        if (_toast > 0) _toast -= dt;
        if (inp.ToggleLook) Game.SetEnhanced(!Game.Enhanced);

        if (_paused)
        {
            UpdatePause();
            return;
        }
        if (inp.Pause)
        {
            _paused = true;
            _pauseSel = 0;
            _showHelp = false;
            return;
        }

        // keys for this frame
        _keys.Clear();
        inp.AddOricKeys(_keys, _bindings);
        var gp = inp.Pad;
        var c = _info.Controls;
        var stick = gp.ThumbSticks.Left;
        if (gp.IsButtonDown(Buttons.DPadUp) || stick.Y > 0.5f) Add(c.Up);
        if (gp.IsButtonDown(Buttons.DPadDown) || stick.Y < -0.5f) Add(c.Down);
        if (gp.IsButtonDown(Buttons.DPadLeft) || stick.X < -0.5f) Add(c.Left);
        if (gp.IsButtonDown(Buttons.DPadRight) || stick.X > 0.5f) Add(c.Right);
        if (gp.IsButtonDown(Buttons.A) || gp.IsButtonDown(Buttons.RightTrigger)) Add(c.A);
        if (gp.IsButtonDown(Buttons.B) || gp.IsButtonDown(Buttons.LeftTrigger)) Add(c.B);
        if (gp.IsButtonDown(Buttons.X)) Add(c.X);
        if (gp.IsButtonDown(Buttons.Y)) Add(c.Y);
        if (gp.IsButtonDown(Buttons.LeftShoulder)) Add(c.LeftShoulder);
        if (gp.IsButtonDown(Buttons.RightShoulder)) Add(c.RightShoulder);
        foreach (var k in _touchKeys) _keys.Add(k);
        if (TiltOn)
        {
            var tilt = inp.Tilt;
            bool left = c.TiltX && tilt.Left, right = c.TiltX && tilt.Right, up = c.TiltY && tilt.Up, down = c.TiltY && tilt.Down;
            if (c.TiltOneAxis && (left || right) && (up || down))
            {
                // one direction at a time: the stronger tilt wins
                if (MathF.Abs(tilt.Roll) >= MathF.Abs(tilt.Pitch)) up = down = false;
                else left = right = false;
            }
            // a clean hand-over when the direction changes: a moment with no direction, so a game that
            // waits for a new key press (as the Oric's keyboard routine does) sees the new one
            int dir = (left ? 1 : 0) | (right ? 2 : 0) | (up ? 4 : 0) | (down ? 8 : 0);
            if (dir != _tiltDir)
            {
                if (_tiltDir != 0 && dir != 0) _tiltGap = 3;
                _tiltDir = dir;
            }
            if (_tiltGap > 0)
            {
                _tiltGap--;
                left = right = up = down = false;
            }
            if (left) Add(c.Left);
            if (right) Add(c.Right);
            if (up) Add(c.Up);
            if (down) Add(c.Down);
            // Lunar Lander reads a new motor setting whenever another number key goes down
            if (c.TiltPower && _program is LunarProgram { Flying: true }) _keys.Add(Digits[tilt.Power()]);
        }

        _acc += dt * OricHz;
        int n = 0;
        while (_acc >= 1 && n < 4)
        {
            _acc -= 1;
            n++;
            _program.ClearKeys();
            foreach (var k in _keys) _program.SetKey(k, true);
            _program.RunFrame();
            _look.AfterFrame(_program);
            if (Game.Audio.Saturated) _program.SkipAudio();
            else
            {
                _program.RenderAudio(_audio, Game.Enhanced);
                _look.MixAudio(_program, _audio, Game.Enhanced);
                Game.Audio.Submit(_audio);
            }
            _scores.AfterFrame(_program, _save);
            if (_scores.Dirty)
            {
                _scores.Dirty = false;
                Game.SaveSoon();
            }
            if (_program.Faulted)
            {
                _program.Reset();
                _scores.Attach(_program, _save);
                Toast("THE GAME WAS RESTARTED");
            }
        }
        if (_acc > 4) _acc = 0;
    }

    /// <summary>The digit keys 0-9 (the Oric's key codes are in keyboard-matrix order, not numeric).</summary>
    private static readonly OricKey[] Digits =
    {
        OricKey.D0, OricKey.D1, OricKey.D2, OricKey.D3, OricKey.D4, OricKey.D5, OricKey.D6, OricKey.D7, OricKey.D8, OricKey.D9,
    };

    private void Add(OricKey[] ks)
    {
        foreach (var k in ks) _keys.Add(k);
    }

    private void Toast(string s)
    {
        _toastText = s;
        _toast = 3f;
    }

    private string[] PauseItems => Game.IsMobile && Game.Input.Tilt.Available
        ? new[] { "RESUME", "RESTART", "LOOK", "SOUND", "TILT", "HOW TO PLAY", "MENU" }
        : TouchUi
            ? new[] { "RESUME", "RESTART", "LOOK", "SOUND", "HOW TO PLAY", "MENU" }
            : new[] { "RESUME", "RESTART", "LOOK", "SOUND", "KEYS", "HOW TO PLAY", "MENU" };

    private void Resume()
    {
        _paused = false;
        Game.Input.Tilt.Centre();
    }

    private void UpdatePause()
    {
        var inp = Game.Input;
        if (_keysEditor != null)
        {
            if (!_keysEditor.Update() || _keysEditor.CloseRequested)
            {
                _bindings = _keysEditor.Bindings;
                _keysEditor = null;
            }
            return;
        }
        if (_showHelp)
        {
            if (inp.Back || inp.Confirm || inp.Taps.Count > 0) _showHelp = false;
            return;
        }
        int n = PauseItems.Length;
        if (inp.Up) _pauseSel = (_pauseSel + n - 1) % n;
        if (inp.Down) _pauseSel = (_pauseSel + 1) % n;
        if (inp.Pause || inp.Back) Resume();
        else if (inp.Confirm) DoPause(_pauseSel);
    }

    private void DoPause(int item)
    {
        switch (PauseItems[item])
        {
            case "RESUME": Resume(); break;
            case "RESTART":
                _program.Reset();
                _scores.Attach(_program, _save);
                Resume();
                break;
            case "LOOK": Game.SetEnhanced(!Game.Enhanced); break;
            case "SOUND": Game.SetSound(!Game.SoundOn); break;
            case "TILT": Game.SetTilt(!Game.Save.Tilt); break;
            case "KEYS": _keysEditor = new KeysEditor(Game, _info); break;
            case "HOW TO PLAY": _showHelp = true; break;
            case "MENU": Game.ChangeScreen(new MenuScreen(Game)); break;
        }
    }

    // ------------------------------------------------------------------ layout

    private bool TouchUi => Game.Input.IsTouchDevice;

    private RectangleF PictureArea(Gfx g)
    {
        var s = g.Safe;
        return FrameRenderer.Fit(new RectangleF(s.X, 0, s.Width, Gfx.Height));
    }

    // ------------------------------------------------------------------ drawing

    public override void Draw(Gfx g)
    {
        var pic = PictureArea(g);
        // the margins: a dim, blurred echo of the game's colours
        g.Rect(new RectangleF(0, 0, g.Width, Gfx.Height), new Color(4, 4, 8));
        if (pic.X > 4)
        {
            g.Gradient(new RectangleF(0, 0, pic.X, Gfx.Height), new Color(14, 16, 30), new Color(4, 4, 10));
            g.Gradient(new RectangleF(pic.Right, 0, g.Width - pic.Right, Gfx.Height), new Color(14, 16, 30), new Color(4, 4, 10));
        }
        _look.Track.Blend = _paused ? 1 : Math.Clamp(_acc, 0, 1);
        Game.Renderer.Draw(g, _program.Index, pic, _look, Game.Enhanced, Game.Save.Scanlines);
        if (Game.Enhanced)
        {
            // a soft frame around the picture
            g.Frame(pic.Inflate(1), 1, _info.Accent * 0.25f);
        }

        _touchKeys.Clear();
        if (!_paused && DrawQuit(g)) return;
        if (TouchUi && !_paused) DrawTouch(g, pic);
        string? hint = !TouchUi ? "ESC PAUSES  -  F2 SWITCHES THE LOOK" : TiltOn ? "TILT TO PLAY  -  TAP THE BUBBLE TO CENTRE" : null;
        if (hint != null && ShowHints && _hint > 0 && !_paused)
        {
            float a = MathF.Min(1, _hint / 1.5f), hw = Gfx.TextWidth(hint) + 20;
            g.RoundRect(new RectangleF(pic.Center.X - hw / 2, pic.Bottom - 26, hw, 18), 6, Color.Black * (0.55f * a));
            g.TextCentred(hint, pic.Center.X, pic.Bottom - 21, Ui.Ink * a, 1f, false);
        }
        if (_toast > 0)
        {
            float a = MathF.Min(1, _toast);
            float tw = MathF.Max(200, Gfx.TextWidth(_toastText) + 20);
            g.RoundRect(new RectangleF(pic.Center.X - tw / 2, pic.Y + 10, tw, 20), 6, Color.Black * (0.7f * a));
            g.TextCentred(_toastText, pic.Center.X, pic.Y + 16, Ui.Gold * a, 1f, false);
        }
        if (_paused) DrawPause(g);
    }

    /// <summary>The QUIT button at the top left: one tap or click returns to the menu (true when it has).</summary>
    private bool DrawQuit(Gfx g)
    {
        var s = g.Safe;
        // in the left margin beside the picture when it fits there
        float margin = PictureArea(g).X - s.X;
        float w = Math.Clamp(margin - 8, 34, 44);
        _quitRect = new RectangleF(margin >= 42 ? s.X + (margin - w) / 2 : s.X + 6, 8, w, 26);
        bool hover = Game.Input.Hover(_quitRect);
        g.RoundRect(_quitRect, 7, Color.Black * (hover ? 0.75f : 0.5f));
        g.RoundRect(_quitRect.Inflate(1), 8, _info.Accent * 0.3f);
        g.TextCentred("QUIT", _quitRect.Center.X, _quitRect.Y + 9, Color.White, 1f, false);
        if (!Game.Input.Tapped(_quitRect)) return false;
        Game.ChangeScreen(new MenuScreen(Game));
        return true;
    }

    private void DrawTouch(Gfx g, RectangleF pic)
    {
        var inp = Game.Input;
        var c = _info.Controls;
        var s = g.Safe;
        float leftW = pic.X - s.X, rightW = s.Right - pic.Right;
        bool overlay = leftW < 90;          // narrow screens (iPad): over the picture
        float alpha = overlay ? 0.45f : 0.8f;
        float lx = overlay ? s.X + 70 : s.X + leftW / 2;
        float rx = overlay ? s.Right - 50 : pic.Right + rightW / 2;

        // pause button
        _buttonAreas.Clear();
        _buttonAreas.Add(_quitRect.Inflate(4));
        TouchButtons.Clear();
        Picture = pic;
        var pr = new RectangleF(s.Right - 40, 8, 32, 26);
        _buttonAreas.Add(pr.Inflate(4));
        g.RoundRect(pr, 7, Color.Black * 0.5f);
        g.Rect(pr.X + 11, pr.Y + 7, 3, 12, Color.White * 0.9f);
        g.Rect(pr.X + 18, pr.Y + 7, 3, 12, Color.White * 0.9f);
        if (inp.Tapped(pr))
        {
            _paused = true;
            _pauseSel = 0;
            return;
        }
        // keyboard toggle
        var kr = new RectangleF(s.Right - 84, 8, 40, 26);
        _buttonAreas.Add(kr.Inflate(4));
        g.RoundRect(kr, 7, (_keyboard ? Ui.Gold * 0.6f : Color.Black * 0.5f));
        g.TextCentred("KEYS", kr.Center.X, kr.Y + 9, Color.White, 1f, false);
        if (inp.Tapped(kr)) _keyboard = !_keyboard;

        // d-pad
        var dirs = new List<TouchKey>();
        bool tilt = TiltOn;
        foreach (var t in c.Touch)
        {
            if (tilt && (c.TiltX && t.Slot is TouchSlot.Left or TouchSlot.Right || c.TiltY && t.Slot is TouchSlot.Up or TouchSlot.Down))
                continue;
            if (t.Slot is TouchSlot.Up or TouchSlot.Down or TouchSlot.Left or TouchSlot.Right) dirs.Add(t);
        }
        if (tilt)
        {
            DrawTiltBubble(g, new Vector2(lx, Gfx.Height - 86), alpha);
            _buttonAreas.Add(new RectangleF(lx - 50, Gfx.Height - 136, 100, 100));
        }
        else if (dirs.Count > 0)
        {
            var centre = new Vector2(lx, Gfx.Height - 86);
            float rad = 52;
            _buttonAreas.Add(new RectangleF(centre.X - rad * 1.7f, centre.Y - rad * 1.7f, rad * 3.4f, rad * 3.4f));
            g.Additive();
            g.GlowAt(centre, rad * 1.5f, _info.Accent * 0.08f);
            g.Alpha();
            g.RoundRect(new RectangleF(centre.X - rad, centre.Y - rad, rad * 2, rad * 2), rad, Color.Black * (0.35f * alpha));
            foreach (var d in dirs)
            {
                var dv = d.Slot switch
                {
                    TouchSlot.Up => new Vector2(0, -1), TouchSlot.Down => new Vector2(0, 1),
                    TouchSlot.Left => new Vector2(-1, 0), _ => new Vector2(1, 0),
                };
                bool held = false;
                foreach (var tp in inp.Touches)
                {
                    var v = tp - centre;
                    if (v.Length() > rad * 1.7f || v.Length() < 6) continue;
                    // 8-way: a touch on a diagonal holds both neighbours
                    float ang = MathF.Atan2(v.Y, v.X), da = MathF.Atan2(dv.Y, dv.X);
                    float diff = MathF.Abs(MathHelper.WrapAngle(ang - da));
                    if (diff < MathF.PI * 0.34f) held = true;
                }
                if (held) foreach (var k in d.Keys) _touchKeys.Add(k);
                var p = centre + dv * rad * 0.62f;
                var col = held ? _info.Accent : Color.White * alpha;
                DrawArrow(g, p, dv, 13, col);
            }
        }

        // action buttons: one column from the bottom (smaller when there are many)
        int nAct = 0;
        foreach (var a in c.Touch) if (a.Slot == TouchSlot.Action) nAct++;
        float br = nAct > 3 ? 21 : 27, step = nAct > 3 ? 47 : 62;
        int ai = 0;
        foreach (var a in c.Touch)
        {
            if (a.Slot != TouchSlot.Action) continue;
            float bx = rx, by = Gfx.Height - 16 - br - ai * step;
            var rr = new RectangleF(bx - br, by - br, br * 2, br * 2);
            _buttonAreas.Add(rr.Inflate(6));
            TouchButtons.Add((a.Label, rr));
            bool held = inp.Held(rr.Inflate(5));
            if (held) foreach (var k in a.Keys) _touchKeys.Add(k);
            g.RoundRect(rr, br, (held ? _info.Accent * 0.75f : Color.Black * (0.45f * alpha)));
            g.RoundRect(rr.Inflate(1.5f), br + 1.5f, (held ? Color.White : _info.Accent) * (0.35f * alpha));
            float ts = a.Label.Length > 5 ? 0.85f : a.Label.Length > 1 ? 1.1f : 1.6f;
            g.TextCentred(a.Label, bx, by - 4 * ts, Color.White * (alpha + 0.2f), ts, false);
            ai++;
        }

        // small keys in the top of the left margin (or over the picture's top left)
        var tops = new List<TouchKey>();
        foreach (var t in c.Touch)
            if (t.Slot == TouchSlot.Top) tops.Add(t);
        if (tops.Count > 0)
        {
            float x0 = s.X + 6, y0 = 40;        // below the QUIT button
            float area = overlay ? 150 : MathF.Max(70, leftW - 12);
            const float kw = 30, kh = 24, gap = 4;
            int cols = Math.Max(1, (int)((area + gap) / (kw + gap)));
            float x = x0, y = y0;
            int col = 0;
            foreach (var t in tops)
            {
                bool wide = t.Label.Length > 2;
                float w = wide ? MathF.Min(area, 66) : kw;
                if (wide && col > 0 || col >= cols)
                {
                    col = 0;
                    x = x0;
                    y += kh + gap;
                }
                var r = new RectangleF(x, y, w, kh);
                _buttonAreas.Add(r.Inflate(2));
                TouchButtons.Add((t.Label, r));
                bool held = inp.Held(r);
                if (held) foreach (var k in t.Keys) _touchKeys.Add(k);
                g.RoundRect(r, 6, held ? _info.Accent * 0.8f : Color.Black * (0.5f * alpha));
                g.RoundRect(r.Inflate(1), 7, _info.Accent * (0.25f * alpha));
                g.TextCentred(t.Label, r.Center.X, r.Y + 8, Color.White * (alpha + 0.2f), wide ? 1f : 1.2f, false);
                if (wide)
                {
                    col = cols;
                    continue;
                }
                x += kw + gap;
                col++;
            }
        }

        if (_keyboard) DrawKeyboard(g, pic);

        // a touch on the picture away from every button fires (tilt does the moving)
        if (c.TapFire.Length > 0)
            foreach (var tp in inp.Touches)
            {
                if (!pic.Contains(tp)) continue;
                bool onButton = false;
                foreach (var a in _buttonAreas)
                    if (a.Contains(tp)) { onButton = true; break; }
                if (!onButton) foreach (var k in c.TapFire) _touchKeys.Add(k);
            }
    }

    /// <summary>Where tilt stands in for the arrows: a spirit level showing the tilt; tap it to centre.</summary>
    private void DrawTiltBubble(Gfx g, Vector2 centre, float alpha)
    {
        var inp = Game.Input;
        var tilt = inp.Tilt;
        var c = _info.Controls;
        float rad = 40;
        var area = new RectangleF(centre.X - rad, centre.Y - rad, rad * 2, rad * 2);
        if (inp.Tapped(area.Inflate(8)))
        {
            tilt.Centre();
            Toast("TILT CENTRED");
        }
        g.RoundRect(area, rad, Color.Black * (0.35f * alpha));
        g.RoundRect(area.Inflate(1.5f), rad + 1.5f, _info.Accent * (0.3f * alpha));
        if (c.TiltPower)
        {
            // Lunar Lander: the motor setting, 0 to 9
            int p = tilt.Power();
            for (int i = 0; i < 9; i++)
            {
                var seg = new RectangleF(centre.X - 14, centre.Y + 26 - (i + 1) * 5.4f, 28, 4);
                g.RoundRect(seg, 2, (i < p ? _info.Accent : Color.White * 0.15f) * alpha);
            }
            g.TextCentred(p.ToString(), centre.X + 26, centre.Y - 6, Color.White * alpha, 1.4f, false);
            g.TextCentred("TILT", centre.X, centre.Y + rad + 6, Ui.Dim * alpha, 0.9f, false);
            return;
        }
        // the dead zone, and the bubble
        float k = rad / 25f;
        float dx = c.TiltX ? Math.Clamp(tilt.Roll, -25, 25) * k : 0;
        float dy = c.TiltY ? Math.Clamp(-tilt.Pitch, -25, 25) * k : 0;
        float dz = Input.Tilt.On * k;
        g.RoundRect(new RectangleF(centre.X - dz, centre.Y - (c.TiltY ? dz : 3), dz * 2, c.TiltY ? dz * 2 : 6), dz, Color.White * (0.12f * alpha));
        if (c.TiltX)
        {
            if (tilt.Left) DrawArrow(g, centre + new Vector2(-rad - 10, 0), new Vector2(-1, 0), 9, _info.Accent);
            if (tilt.Right) DrawArrow(g, centre + new Vector2(rad + 10, 0), new Vector2(1, 0), 9, _info.Accent);
        }
        if (c.TiltY)
        {
            if (tilt.Up) DrawArrow(g, centre + new Vector2(0, -rad - 10), new Vector2(0, -1), 9, _info.Accent);
            if (tilt.Down) DrawArrow(g, centre + new Vector2(0, rad + 10), new Vector2(0, 1), 9, _info.Accent);
        }
        var b = centre + new Vector2(dx, dy);
        g.Additive();
        g.GlowAt(b, 16, _info.Accent * 0.35f);
        g.Alpha();
        g.RoundRect(new RectangleF(b.X - 7, b.Y - 7, 14, 14), 7, Color.Lerp(_info.Accent, Color.White, 0.4f) * (alpha + 0.2f));
        g.TextCentred("TILT", centre.X, centre.Y + rad + 6, Ui.Dim * alpha, 0.9f, false);
    }

    private static void DrawArrow(Gfx g, Vector2 p, Vector2 dir, float size, Color c)
    {
        var side = new Vector2(-dir.Y, dir.X);
        var tip = p + dir * size * 0.8f;
        var a = p - dir * size * 0.5f + side * size * 0.75f;
        var b = p - dir * size * 0.5f - side * size * 0.75f;
        g.Line(a, tip, 4, c);
        g.Line(b, tip, 4, c);
    }

    private static readonly (string label, OricKey key, float width)[][] KeyRows =
    {
        new[] { ("1", OricKey.D1, 1f), ("2", OricKey.D2, 1f), ("3", OricKey.D3, 1f), ("4", OricKey.D4, 1f), ("5", OricKey.D5, 1f),
                ("6", OricKey.D6, 1f), ("7", OricKey.D7, 1f), ("8", OricKey.D8, 1f), ("9", OricKey.D9, 1f), ("0", OricKey.D0, 1f), ("DEL", OricKey.Del, 1.4f) },
        new[] { ("Q", OricKey.Q, 1f), ("W", OricKey.W, 1f), ("E", OricKey.E, 1f), ("R", OricKey.R, 1f), ("T", OricKey.T, 1f),
                ("Y", OricKey.Y, 1f), ("U", OricKey.U, 1f), ("I", OricKey.I, 1f), ("O", OricKey.O, 1f), ("P", OricKey.P, 1f), ("RET", OricKey.Return, 1.4f) },
        new[] { ("A", OricKey.A, 1f), ("S", OricKey.S, 1f), ("D", OricKey.D, 1f), ("F", OricKey.F, 1f), ("G", OricKey.G, 1f),
                ("H", OricKey.H, 1f), ("J", OricKey.J, 1f), ("K", OricKey.K, 1f), ("L", OricKey.L, 1f), ("UP", OricKey.Up, 1.2f) },
        new[] { ("Z", OricKey.Z, 1f), ("X", OricKey.X, 1f), ("C", OricKey.C, 1f), ("V", OricKey.V, 1f), ("B", OricKey.B, 1f),
                ("N", OricKey.N, 1f), ("M", OricKey.M, 1f), ("LEFT", OricKey.Left, 1.4f), ("DOWN", OricKey.Down, 1.4f), ("RIGHT", OricKey.Right, 1.4f) },
        new[] { ("SPACE", OricKey.Space, 6f) },
    };

    private void DrawKeyboard(Gfx g, RectangleF pic)
    {
        var inp = Game.Input;
        float kw = MathF.Min(36, (g.Safe.Width - 20) / 12.2f), kh = 26;
        float totalH = KeyRows.Length * (kh + 4) + 10;
        var panel = new RectangleF(g.Width / 2 - kw * 6.2f, Gfx.Height - totalH - 4, kw * 12.4f, totalH);
        _buttonAreas.Add(panel);
        g.Panel(panel, _info.Accent * 0.6f, 0.9f, 10);
        float y = panel.Y + 6;
        foreach (var row in KeyRows)
        {
            float rowW = 0;
            foreach (var k in row) rowW += k.width * kw + 3;
            float x = panel.Center.X - rowW / 2;
            foreach (var (label, key, width) in row)
            {
                var r = new RectangleF(x, y, width * kw, kh);
                bool held = inp.Held(r);
                if (held) _touchKeys.Add(key);
                g.RoundRect(r, 5, held ? _info.Accent * 0.8f : new Color(40, 44, 70));
                g.TextCentred(label, r.Center.X, r.Y + 9, Color.White, label.Length > 3 ? 0.8f : 1.1f, false);
                x += width * kw + 3;
            }
            y += kh + 4;
        }
    }

    private void DrawPause(Gfx g)
    {
        if (_keysEditor != null)
        {
            _keysEditor.Draw(g);
            return;
        }
        g.Rect(new RectangleF(0, 0, g.Width, Gfx.Height), Color.Black * 0.6f);
        if (_showHelp)
        {
            var hr = new RectangleF(g.Width / 2 - 200, 50, 400, 250);
            g.Panel(hr, _info.Accent, 0.95f, 12);
            g.GlowText("HOW TO PLAY", hr.Center.X, hr.Y + 12, _info.Accent, 1.8f, 0.4f);
            float y = hr.Y + 40;
            foreach (var line in _info.Controls.Help)
            {
                g.Text(line, hr.X + 24, y, Ui.Ink, 1.2f, false);
                y += 14;
            }
            if (!_bindings.IsEmpty)
            {
                // the keys the player has redefined
                var changed = new List<string>();
                foreach (var (label, key) in _info.Controls.Actions)
                    if (_save.Keys.ContainsKey(key.ToString()))
                        changed.Add(label.ToUpperInvariant() + " = " + (_bindings.HostFor(key) is Microsoft.Xna.Framework.Input.Keys hk ? KeyBindings.Name(hk) : "NONE"));
                foreach (var line in Gfx.Wrap("YOUR KEYS: " + string.Join(",  ", changed), hr.Width - 48, 1f))
                {
                    g.Text(line, hr.X + 24, y + 2, Ui.Gold, 1f, false);
                    y += 11;
                }
            }
            if (TouchUi && _info.Controls.TapFire.Length > 0)
            {
                g.Text("TOUCH THE PICTURE TO FIRE", hr.X + 24, y + 2, Ui.Gold, 1f, false);
                y += 11;
            }
            if (TiltOn)
                foreach (var line in Gfx.Wrap(_info.Controls.TiltHelp, hr.Width - 48, 1f))
                {
                    g.Text(line, hr.X + 24, y + 2, Ui.Gold, 1f, false);
                    y += 11;
                }
            g.TextCentred("TAP OR PRESS A KEY TO CLOSE", hr.Center.X, hr.Bottom - 16, Ui.Dim, 0.9f);
            return;
        }
        var items = PauseItems;
        float step = items.Length > 6 ? 32 : 35, h = 58 + items.Length * step;
        var r = new RectangleF(g.Width / 2 - 110, (Gfx.Height - h) / 2, 220, h);
        g.Panel(r, _info.Accent, 0.94f, 12);
        g.GlowText("PAUSED", r.Center.X, r.Y + 12, Color.White, 2f, 0.5f);
        g.TextCentred(_info.Title, r.Center.X, r.Y + 34, _info.Accent, 1f);
        for (int i = 0; i < items.Length; i++)
        {
            string label = items[i] switch
            {
                "LOOK" => Game.Enhanced ? "LOOK: ENHANCED" : "LOOK: ORIGINAL",
                "SOUND" => Game.SoundLabel,
                "TILT" => Game.Save.Tilt ? "TILT: ON" : "TILT: OFF",
                var s => s,
            };
            var br = new RectangleF(r.X + 18, r.Y + 52 + i * step, r.Width - 36, 28);
            if (Ui.Button(g, Game, br, label, _pauseSel == i, items[i] == "LOOK" ? Ui.Gold : _info.Accent, 1.3f))
            {
                _pauseSel = i;
                DoPause(i);
            }
        }
    }
}
