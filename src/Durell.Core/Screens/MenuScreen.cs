using System;
using System.Collections.Generic;
using Durell.Games;
using Durell.Graphics;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Durell.Screens;

/// <summary>
/// The collection's front screen: a carousel of the seven games (the chosen one running live), its
/// story and best score, and the buttons to play, to switch between the ENHANCED and ORIGINAL looks,
/// the sound, the help and the credits.
/// </summary>
internal sealed class MenuScreen : Screen
{
    private enum Focus { Games, Play, Look, Sound, Tilt, Help, Keys, About, Quit }
    private enum Overlay { None, Help, About }

    private int _sel;
    private float _scroll;
    private Focus _focus = Focus.Games;
    private Overlay _overlay;
    private GameProgram? _live;
    private Look? _liveLook;
    private string _liveId = "";
    private float _liveAcc;
    private float _enterTime;
    private KeysEditor? _keys;

    public MenuScreen(DurellGame game) : base(game)
    {
        _sel = Math.Max(0, Catalog.All.FindIndex(g => g.Id == game.Save.LastGame));
        _scroll = _sel;
    }

    private GameInfo Sel => Catalog.All[_sel];

    /// <summary>Opens the key editor for the chosen game (the capture tool uses this too).</summary>
    internal void OpenKeys() => _keys = new KeysEditor(Game, Sel);

    public override void Enter()
    {
        _enterTime = Game.Clock;
    }

    private void Choose(int i)
    {
        i = Math.Clamp(i, 0, Catalog.All.Count - 1);
        if (i == _sel) return;
        _sel = i;
        Game.Save.LastGame = Sel.Id;
        Game.SaveSoon();
    }

    private void Play()
    {
        Game.Save.LastGame = Sel.Id;
        Game.PersistSave();
        Game.ChangeScreen(new PlayScreen(Game, Sel));
    }

    public override void Update(float dt)
    {
        Game.Previews.Prepare();
        var inp = Game.Input;
        _scroll += (_sel - _scroll) * MathF.Min(1, dt * 10);

        // the chosen game runs on its card (silently, no keys)
        if (_liveId != Sel.Id)
        {
            // the prepared game keeps running as the live card (no new fast-forward: slow on phones)
            var ready = Game.Previews.Program(Sel.Id);
            _live = ready;
            _liveLook = ready != null ? Game.Previews.LookFor(Sel.Id) : null;
            if (ready != null) _liveId = Sel.Id;
            _liveAcc = 0;
        }
        if (_live != null)
        {
            _liveAcc += dt * 50;
            int n = 0;
            while (_liveAcc >= 1 && n++ < 3)
            {
                _liveAcc -= 1;
                _live.RunFrame();
                _liveLook?.AfterFrame(_live);
                _live.SkipAudio();
                if (_live.Faulted) _live.Reset();
            }
            if (_liveAcc > 3) _liveAcc = 0;
        }

        if (_keys != null)
        {
            if (!_keys.Update() || _keys.CloseRequested) _keys = null;
            return;
        }
        if (_overlay != Overlay.None)
        {
            if (inp.Back || inp.Confirm || inp.Taps.Count > 0) _overlay = Overlay.None;
            return;
        }

        if (inp.Left)
        {
            if (_focus == Focus.Games) Choose(_sel - 1);
            else if (_focus > Focus.Play)
            {
                _focus--;
                if (_focus == Focus.Keys && !ShowKeys) _focus--;
                if (_focus == Focus.Tilt && !ShowTilt) _focus--;
            }
        }
        if (inp.Right)
        {
            if (_focus == Focus.Games) Choose(_sel + 1);
            else if (_focus < (Game.CanQuit ? Focus.Quit : Focus.About))
            {
                _focus++;
                if (_focus == Focus.Tilt && !ShowTilt) _focus++;
                if (_focus == Focus.Keys && !ShowKeys) _focus++;
            }
        }
        if (inp.Down && _focus == Focus.Games) _focus = Focus.Play;
        if (inp.Up && _focus != Focus.Games) _focus = Focus.Games;
        if (inp.Key(Keys.L) || inp.ToggleLook || inp.Button(Buttons.Y)) Game.SetEnhanced(!Game.Enhanced);
        if (inp.Key(Keys.S)) Game.SetSound(!Game.SoundOn);
        if (inp.Key(Keys.OemMinus) || inp.Key(Keys.Subtract) || inp.Button(Buttons.LeftShoulder)) Game.StepVolume(-1);
        if (inp.Key(Keys.OemPlus) || inp.Key(Keys.Add) || inp.Button(Buttons.RightShoulder)) Game.StepVolume(1);
        if (inp.Key(Keys.H) || inp.Key(Keys.F1) || inp.Button(Buttons.X)) _overlay = Overlay.Help;
        if (inp.Key(Keys.K) && ShowKeys) _keys = new KeysEditor(Game, Sel);
        for (int k = 0; k < Catalog.All.Count; k++)
            if (inp.Key(Keys.D1 + k)) Choose(k);
        if (inp.Confirm)
        {
            switch (_focus)
            {
                case Focus.Games:
                case Focus.Play: Play(); break;
                case Focus.Look: Game.SetEnhanced(!Game.Enhanced); break;
                case Focus.Sound: Game.SetSound(!Game.SoundOn); break;
                case Focus.Tilt: Game.SetTilt(!Game.Save.Tilt); break;
                case Focus.Help: _overlay = Overlay.Help; break;
                case Focus.Keys: _keys = new KeysEditor(Game, Sel); break;
                case Focus.About: _overlay = Overlay.About; break;
                case Focus.Quit: Game.Exit(); break;
            }
        }
        if (inp.Back && Game.CanQuit && Game.Clock - _enterTime > 0.3f && !Game.IsMobile) Game.Exit();
        if (inp.ScrollDelta != 0) Choose(_sel + (inp.ScrollDelta < 0 ? 1 : -1));
    }

    /// <summary>Redefining keys is for keyboards (computers; tablets with a keyboard can use it too).</summary>
    private bool ShowKeys => !Game.Input.IsTouchDevice;

    /// <summary>Phones and tablets with an accelerometer offer tilt steering.</summary>
    private bool ShowTilt => Game.IsMobile && Game.Input.Tilt.Available;

    // ------------------------------------------------------------------ drawing

    private RectangleF CardRect(Gfx g, int i, out float scale)
    {
        float d = i - _scroll;
        float baseW = MathF.Min(168, g.Width * 0.27f);
        scale = 1f / (1f + MathF.Abs(d) * 0.28f);
        float w = baseW * scale, h = w * 0.75f;
        float spacing = baseW * 0.78f;
        float cx = g.Width / 2 + MathF.Sign(d) * (MathF.Min(MathF.Abs(d), 1) * spacing * 1.05f + MathF.Max(0, MathF.Abs(d) - 1) * spacing * 0.7f);
        float cy = 124;
        return new RectangleF(cx - w / 2, cy - h / 2, w, h);
    }

    public override void Draw(Gfx g)
    {
        float t = g.Time;
        Ui.Backdrop(g, t, Sel.Accent);
        var safe = g.Safe;

        // title
        g.GlowText("THE DURELL COLLECTION", g.Width / 2, 16, Ui.Gold, 3f, 0.8f);
        g.TextCentred("CLASSIC ORIC GAMES FROM DURELL SOFTWARE, 1983-1986", g.Width / 2, 46, Ui.Dim, 1f);

        // cards, far ones first
        var order = new List<int>();
        for (int i = 0; i < Catalog.All.Count; i++) order.Add(i);
        order.Sort((a, b) => MathF.Abs(b - _scroll).CompareTo(MathF.Abs(a - _scroll)));
        foreach (int i in order) DrawCard(g, i);

        // info panel
        var info = Sel;
        float px = MathF.Max(safe.X + 14, g.Width / 2 - 300), pw = MathF.Min(600, safe.Width - 28);
        px = g.Width / 2 - pw / 2;
        // the panel ends above the buttons (which sit higher on phones, clear of the home indicator)
        float buttonsTop = MathF.Min(safe.Bottom, Gfx.Height) - 48;
        var panel = new RectangleF(px, 214, pw, MathF.Min(86, buttonsTop - 6 - 214));
        g.Panel(panel, info.Accent * 0.7f, 0.78f, 10);
        g.Text(info.Title, panel.X + 12, panel.Y + 9, info.Accent, 2f);
        g.Text($"{info.Year}  -  {info.Author}", panel.X + 12, panel.Y + 28, Ui.Dim, 1f);
        var gs = Game.Save.For(info.Id);
        float scoreW = 118;
        var lines = Gfx.Wrap(info.Blurb, pw - scoreW - 34, 1f);
        int fit = Math.Max(1, (int)((panel.Height - 45) / 9.5f));
        for (int l = 0; l < lines.Count && l < Math.Min(5, fit); l++)
            g.Text(lines[l], panel.X + 12, panel.Y + 41 + l * 9.5f, Ui.Ink, 1f, false);
        var sbox = new RectangleF(panel.Right - scoreW - 10, panel.Y + 10, scoreW, panel.Height - 20);
        g.RoundRect(sbox, 7, Color.Black * 0.35f);
        g.TextCentred("HIGH SCORE", sbox.Center.X, sbox.Y + 8, Ui.Dim, 1f);
        string best = gs.Best > 0 ? gs.Best.ToString() : "-";
        g.GlowText(best, sbox.Center.X, sbox.Y + 22, Color.White, 2.2f, 0.4f);
        bool room = sbox.Height >= 58;      // a short panel (phones) leaves out the name / date line
        if (room && gs.BestName.Length > 0) g.TextCentred(gs.BestName, sbox.Center.X, sbox.Y + 44, Ui.Gold, 1f);
        else if (room && gs.BestDate.Length > 0) g.TextCentred(gs.BestDate, sbox.Center.X, sbox.Y + 44, Ui.Dim * 0.8f, 1f);
        g.TextCentred(gs.Plays == 1 ? "PLAYED ONCE" : gs.Plays > 1 ? $"PLAYED {gs.Plays} TIMES" : "NOT PLAYED YET", sbox.Center.X, sbox.Bottom - 13, Ui.Dim * 0.8f, 1f);

        // buttons
        var labels = new List<(Focus f, string text, float w)>
        {
            (Focus.Play, "PLAY", 96),
            (Focus.Look, Game.Enhanced ? "LOOK: ENHANCED" : "LOOK: ORIGINAL", 150),
            (Focus.Sound, Game.SoundLabel, 172),
        };
        if (ShowTilt) labels.Add((Focus.Tilt, Game.Save.Tilt ? "TILT: ON" : "TILT: OFF", 96));
        labels.AddRange(new (Focus f, string text, float w)[]
        {
            (Focus.Help, "HELP", 66),
        });
        if (ShowKeys) labels.Add((Focus.Keys, "KEYS", 66));
        labels.AddRange(new (Focus f, string text, float w)[]
        {
            (Focus.About, "ABOUT", 70),
        });
        if (Game.CanQuit) labels.Add((Focus.Quit, "QUIT", 62));
        float gap = 8, total = -gap;
        foreach (var l in labels) total += l.w + gap;
        float scaleB = MathF.Min(1, (safe.Width - 24) / total);
        float bx = g.Width / 2 - total * scaleB / 2;
        float by = MathF.Min(safe.Bottom, Gfx.Height) - 48;
        foreach (var (f, text, w) in labels)
        {
            var r = new RectangleF(bx, by, w * scaleB, 30);
            var accent = f == Focus.Play ? info.Accent : f == Focus.Look ? Ui.Gold : new Color(120, 140, 200);
            if (f == Focus.Sound)
            {
                // - and + at the ends step the volume; the middle turns the sound off and on
                float sw = MathF.Min(30, r.Height);
                var minus = new RectangleF(r.X, r.Y, sw, r.Height);
                var plus = new RectangleF(r.Right - sw, r.Y, sw, r.Height);
                var mid = new RectangleF(minus.Right + 2, r.Y, r.Width - 2 * sw - 4, r.Height);
                float ks = 1.3f * MathF.Min(1, scaleB * 1.05f);
                bool focused = _focus == f;
                if (Ui.Button(g, Game, minus, "-", focused, accent, 1.6f * MathF.Min(1, scaleB * 1.05f)) && _overlay == Overlay.None && _keys == null)
                {
                    _focus = f;
                    Game.StepVolume(-1);
                }
                if (Ui.Button(g, Game, plus, "+", focused, accent, 1.6f * MathF.Min(1, scaleB * 1.05f)) && _overlay == Overlay.None && _keys == null)
                {
                    _focus = f;
                    Game.StepVolume(1);
                }
                if (Ui.Button(g, Game, mid, text, focused, accent, ks) && _overlay == Overlay.None && _keys == null)
                {
                    _focus = f;
                    Game.SetSound(!Game.SoundOn);
                }
                // the level, as a row of ten pips under the label
                float pip = (mid.Width - 16) / 10f;
                for (int v = 0; v < 10; v++)
                    g.Rect(mid.X + 8 + v * pip, mid.Bottom - 5, pip - 1.5f, 2, (v < Game.VolumeLevel ? accent : Color.White * 0.15f));
                bx += (w + gap) * scaleB;
                continue;
            }
            if (Ui.Button(g, Game, r, text, _focus == f, accent, 1.3f * MathF.Min(1, scaleB * 1.05f)) && _overlay == Overlay.None && _keys == null)
            {
                _focus = f;
                switch (f)
                {
                    case Focus.Play: Play(); break;
                    case Focus.Look: Game.SetEnhanced(!Game.Enhanced); break;
                    case Focus.Sound: Game.SetSound(!Game.SoundOn); break;
                    case Focus.Tilt: Game.SetTilt(!Game.Save.Tilt); break;
                    case Focus.Keys: _keys = new KeysEditor(Game, Sel); break;
                    case Focus.Help: _overlay = Overlay.Help; break;
                    case Focus.About: _overlay = Overlay.About; break;
                    case Focus.Quit: Game.Exit(); break;
                }
            }
            bx += (w + gap) * scaleB;
        }
        if (!Game.Input.IsTouchDevice)
            g.TextCentred("ARROWS CHOOSE  -  RETURN PLAYS  -  L LOOK  -  K KEYS  -  -/+ VOLUME", g.Width / 2, panel.Bottom + 4, Ui.Dim * 0.6f, 0.8f, false);
        DrawCredits(g, Game.Clock - _enterTime);

        if (_overlay == Overlay.Help) DrawHelp(g);
        else if (_overlay == Overlay.About) DrawAbout(g);
        _keys?.Draw(g);
    }

    private const string Credits = "Originals ported by PFJ. Original versions copyright Durell Software.";

    /// <summary>The credit line along the bottom, crawling from left to right and repeating without a break.</summary>
    private static void DrawCredits(Gfx g, float t)
    {
        const float scale = 1f, speed = 40, gap = 90;
        float period = Gfx.TextWidth(Credits, scale) + gap;
        float y = MathF.Min(g.Safe.Bottom, Gfx.Height) - 11;
        // one copy starts centred, the others follow it in from the left
        float start = (g.Width - period + gap) / 2;
        float x = start + t * speed;
        x -= MathF.Ceiling(x / period) * period;
        for (; x < g.Width; x += period)
            g.Text(Credits, x, y, Ui.Dim * 0.85f, scale, false);
    }

    private void DrawCard(Gfx g, int i)
    {
        var info = Catalog.All[i];
        var r = CardRect(g, i, out float scale);
        bool sel = i == _sel;
        float a = MathF.Min(1, 0.35f + scale * 0.75f);
        if (sel)
        {
            g.Additive();
            g.GlowAt(r.Center, r.Width * 0.85f, info.Accent * 0.22f);
            g.Alpha();
        }
        g.RoundRect(r.Inflate(sel ? 3 : 1.5f), 8, (sel ? info.Accent : new Color(70, 80, 110)) * a);
        g.Rect(r, Color.Black);
        if (sel && _live != null && _liveLook != null)
        {
            _liveLook.Track.Blend = Math.Clamp(_liveAcc, 0, 1);
            Game.Renderer.Draw(g, _live.Index, r, _liveLook, Game.Enhanced, Game.Save.Scanlines);
        }
        else
        {
            var thumb = Game.Previews.Thumb(g, info.Id);
            if (thumb != null) g.Texture(thumb, r, Color.White * a);
            else g.TextCentred("...", r.Center.X, r.Center.Y - 4, Ui.Dim, 1.5f);
        }
        if (!sel) g.Rect(r, Color.Black * (0.45f * (1 - scale)));
        if (Game.Input.Tapped(r) && _overlay == Overlay.None && _keys == null)
        {
            if (sel) Play();
            else
            {
                Choose(i);
                _focus = Focus.Games;
            }
        }
        float ts = sel ? 1.3f : 1f * scale;
        g.TextCentred(info.Title, r.Center.X, r.Bottom + 6, (sel ? Color.White : Ui.Dim) * a, ts);
    }

    private void DrawHelp(Gfx g)
    {
        var info = Sel;
        var r = new RectangleF(g.Width / 2 - 230, 40, 460, 270);
        g.Rect(new RectangleF(0, 0, g.Width, Gfx.Height), Color.Black * 0.6f);
        g.Panel(r, info.Accent, 0.94f, 12);
        g.GlowText(info.Title + " - HOW TO PLAY", r.Center.X, r.Y + 12, info.Accent, 1.6f, 0.4f);
        float y = r.Y + 36;
        foreach (var line in info.Controls.Help)
        {
            g.Text(line, r.X + 22, y, Ui.Ink, 1.2f, false);
            y += 13;
        }
        if (Game.Input.IsTouchDevice && info.Controls.TiltHelp.Length > 0)
            foreach (var line in Gfx.Wrap(info.Controls.TiltHelp, r.Width - 44, 1f))
            {
                g.Text(line, r.X + 22, y + 2, Ui.Gold, 1f, false);
                y += 11;
            }
        y += 8;
        g.Text("IN THE COLLECTION", r.X + 22, y, Ui.Gold, 1.1f);
        y += 13;
        string[] general = Game.Input.IsTouchDevice
            ? new[] { "Tilt the device to move, touch the picture to fire. TILT on",
                      "the menu switches tilt off for on-screen arrows; tap the bubble",
                      "to re-centre. Buttons give the other keys; KEYS opens a full",
                      "Oric keyboard. II pauses; QUIT (top left) returns here." }
            : new[] { "The keyboard is the Oric's; KEYS (or K) redefines a game's keys.", "ESC pause   QUIT (top left) menu   F2 look   F11 full screen", "A gamepad works too (START pauses)." };
        foreach (var line in general)
        {
            g.Text(line, r.X + 22, y, Ui.Dim, 1f, false);
            y += 11;
        }
        g.TextCentred("TAP OR PRESS A KEY TO CLOSE", r.Center.X, r.Bottom - 16, Ui.Dim * 0.8f, 0.9f);
    }

    private void DrawAbout(Gfx g)
    {
        var r = new RectangleF(g.Width / 2 - 240, 34, 480, 284);
        g.Rect(new RectangleF(0, 0, g.Width, Gfx.Height), Color.Black * 0.6f);
        g.Panel(r, Ui.Gold, 0.94f, 12);
        g.GlowText("ABOUT", r.Center.X, r.Y + 12, Ui.Gold, 2f, 0.5f);
        string[] lines =
        {
            "Durell Software's games for the Oric Atmos and Oric-1:",
            "Harrier Attack and Scuba Dive by Ronald Jeffs, Star Fighter",
            "by Mike Highfield, and from the Galaxy 5 tape, Galaxy by",
            "Philip Dierks and Lunar Lander by Robert White.",
            "Turbo Esprit (Mike Richardson, 1986) is the Oric conversion",
            "of the ZX Spectrum game.",
            "",
            "Each game is its original program, translated instruction",
            "by instruction into native code - no emulator. ORIGINAL shows",
            "and sounds exactly as on the Oric; ENHANCED redraws it in high",
            "resolution with smooth scrolling. Harrier Attack 3D is the",
            "same game seen from a chase camera, with new sound.",
            "",
            "Collection by Paul F. Johnson. The games remain the",
            "property of their copyright holders (Durell Software).",
        };
        float y = r.Y + 40;
        foreach (var l in lines)
        {
            g.TextCentred(l, r.Center.X, y, l.Length == 0 ? Ui.Ink : Ui.Ink, 1f, false);
            y += 14.5f;
        }
        g.TextCentred("TAP OR PRESS A KEY TO CLOSE", r.Center.X, r.Bottom - 16, Ui.Dim * 0.8f, 0.9f);
    }
}
