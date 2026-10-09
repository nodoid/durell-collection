using System;
using System.Collections.Generic;
using Durell.Games;
using Durell.Graphics;
using Durell.Input;
using Durell.Machine;
using Durell.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Durell.Screens;

/// <summary>
/// Redefines a game's keys: a list of what each key does with the key that does it now. Choose one
/// (arrows and RETURN, or click) and press the new key; a key already used elsewhere moves to the new
/// action. ESC cancels a choice or closes. Saved with the game's high scores.
/// </summary>
internal sealed class KeysEditor
{
    private readonly DurellGame _game;
    private readonly GameInfo _info;
    private readonly GameSave _save;
    private int _sel;
    private bool _waiting;
    private float _opened;

    public KeysEditor(DurellGame game, GameInfo info)
    {
        _game = game;
        _info = info;
        _save = game.Save.For(info.Id);
        _opened = game.Clock;
    }

    public KeyBindings Bindings => KeyBindings.From(_save.Keys);

    private int Rows => _info.Controls.Actions.Length + 2;     // + reset, done

    /// <summary>Handles input; returns false once the editor is closed.</summary>
    public bool Update()
    {
        var inp = _game.Input;
        if (_game.Clock - _opened < 0.15f) return true;
        if (_waiting)
        {
            var k = inp.NewKey();
            if (k == null) return true;
            _waiting = false;
            if (k == Keys.Escape || KeyBindings.Reserved(k.Value)) return true;
            Assign(_info.Controls.Actions[_sel].Key, k.Value);
            return true;
        }
        if (inp.Up) _sel = (_sel + Rows - 1) % Rows;
        if (inp.Down) _sel = (_sel + 1) % Rows;
        if (inp.Back) return false;
        if (inp.Confirm) return Choose(_sel);
        return true;
    }

    private bool Choose(int row)
    {
        int n = _info.Controls.Actions.Length;
        if (row < n)
        {
            _sel = row;
            _waiting = true;
            return true;
        }
        if (row == n)
        {
            _save.Keys.Clear();
            _game.PersistSave();
            return true;
        }
        return false;
    }

    private void Assign(OricKey action, Keys host)
    {
        // the key leaves whatever it did before; the action leaves its old key
        var drop = new List<string>();
        foreach (var (o, h) in _save.Keys)
            if (h == host.ToString() || o == action.ToString()) drop.Add(o);
        foreach (var o in drop) _save.Keys.Remove(o);
        if (OricKeyboard.HostKey(action) != host) _save.Keys[action.ToString()] = host.ToString();
        else
        {
            // back on its own key: but if another action had taken that key, take it back
            foreach (var (o, h) in new Dictionary<string, string>(_save.Keys))
                if (h == host.ToString()) _save.Keys.Remove(o);
        }
        _game.PersistSave();
    }

    public void Draw(Gfx g)
    {
        var inp = _game.Input;
        var actions = _info.Controls.Actions;
        var b = Bindings;
        float rowH = actions.Length > 8 ? 15 : 18;
        float h = 52 + (actions.Length + 2) * rowH + 14;
        var r = new RectangleF(g.Width / 2 - 200, (Gfx.Height - h) / 2, 400, h);
        g.Rect(new RectangleF(0, 0, g.Width, Gfx.Height), Color.Black * 0.6f);
        g.Panel(r, _info.Accent, 0.95f, 12);
        g.GlowText(_info.Title + " - KEYS", r.Center.X, r.Y + 12, _info.Accent, 1.6f, 0.4f);
        g.TextCentred(_waiting ? "PRESS THE NEW KEY  (ESC CANCELS)" : "CHOOSE ONE, THEN PRESS ITS NEW KEY", r.Center.X, r.Y + 34, _waiting ? Ui.Gold : Ui.Dim, 1f);
        float y = r.Y + 52;
        for (int i = 0; i < actions.Length + 2; i++)
        {
            var row = new RectangleF(r.X + 16, y, r.Width - 32, rowH - 2);
            bool focused = i == _sel;
            if (focused) g.RoundRect(row, 5, _info.Accent * 0.25f);
            if (!_waiting && inp.Tapped(row))
            {
                _sel = i;
                if (!Choose(i)) _closeRequested = true;
            }
            if (i < actions.Length)
            {
                var (label, key) = actions[i];
                g.Text(label.ToUpperInvariant(), row.X + 8, row.Y + (rowH - 10) / 2, Ui.Ink, 1.1f, false);
                string name;
                Color col;
                if (_waiting && focused)
                {
                    name = ((int)(_game.Clock * 3) & 1) == 0 ? "?" : "";
                    col = Ui.Gold;
                }
                else
                {
                    var host = b.HostFor(key);
                    name = host is Keys k ? KeyBindings.Name(k) : "NONE";
                    bool changed = _save.Keys.ContainsKey(key.ToString());
                    col = host == null ? new Color(255, 90, 80) : changed ? Ui.Gold : Color.White;
                }
                g.TextRight(name, row.Right - 8, row.Y + (rowH - 10) / 2, col, 1.1f, false);
            }
            else
            {
                string label = i == actions.Length ? "RESET TO THE ORIGINAL KEYS" : "DONE";
                g.TextCentred(label, row.Center.X, row.Y + (rowH - 10) / 2, i == actions.Length ? Ui.Dim : Color.White, 1.1f, false);
            }
            y += rowH;
        }
    }

    private bool _closeRequested;

    /// <summary>True once DONE was clicked (checked by the owner after drawing).</summary>
    public bool CloseRequested => _closeRequested;
}
