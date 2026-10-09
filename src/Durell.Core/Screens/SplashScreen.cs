using System;
using Durell.Graphics;
using Durell.Graphics.Art;
using Microsoft.Xna.Framework;

namespace Durell.Screens;

/// <summary>
/// Phones and tablets: shown at launch while the games are made ready for the menu and the artwork is
/// drawn (both on background threads), with a progress bar; then it hands over to the menu.
/// </summary>
internal sealed class SplashScreen : Screen
{
    private const float MinShow = 1.5f;
    private float _time;
    private float _leave = -1;

    public SplashScreen(DurellGame game) : base(game)
    {
    }

    public override void Enter()
    {
        Game.Previews.Start();
        ArtCache.DrawAllInBackground();
    }

    private float Progress
    {
        get
        {
            float games = Game.Previews.Ready / (float)Game.Previews.Total;
            float art = ArtCache.Done / (float)Math.Max(1, ArtCache.Total);
            return games * 0.7f + art * 0.3f;
        }
    }

    public override void Update(float dt)
    {
        _time += dt;
        bool ready = Game.Previews.Prepare();
        ArtCache.Upload(Game.Gfx.Device, 6);
        if (_leave < 0 && ready && ArtCache.Done >= ArtCache.Total && _time > MinShow) _leave = _time;
        // don't keep the player waiting for the last few pieces of art forever
        if (_leave < 0 && ready && _time > 12) _leave = _time;
        if (_leave >= 0 && _time - _leave > 0.35f) Game.ChangeScreen(new MenuScreen(Game));
    }

    public override void Draw(Gfx g)
    {
        float t = g.Time;
        float fade = _leave >= 0 ? 1 - MathF.Min(1, (_time - _leave) / 0.35f) : MathF.Min(1, _time * 3);
        Ui.Backdrop(g, t, new Color(220, 110, 255));
        var safe = g.Safe;
        float cx = g.Width / 2;
        // a hovering invader from Galaxy above the title
        var alien = ArtCache.Get(g.Device, "galaxy-alien-red" + (((int)(t * 6)) & 1));
        g.Sprite(alien, new Vector2(cx, 92 + MathF.Sin(t * 2) * 4), 70, MathF.Sin(t * 1.5f) * 0.06f, Color.White * fade);
        g.GlowText("THE DURELL COLLECTION", cx, 150, Ui.Gold * fade, 3f, 0.8f);
        g.TextCentred("CLASSIC ORIC GAMES FROM DURELL SOFTWARE, 1983-1986", cx, 182, Ui.Dim * fade, 1f);
        // progress
        float w = MathF.Min(320, safe.Width - 60);
        var bar = new RectangleF(cx - w / 2, 230, w, 10);
        g.RoundRect(bar, 5, new Color(30, 34, 60) * fade);
        g.RoundRect(new RectangleF(bar.X, bar.Y, MathF.Max(10, w * Progress), bar.Height), 5, Ui.Gold * fade);
        g.TextCentred(Game.Previews.Ready < Game.Previews.Total ? "GETTING THE GAMES READY" : "DRAWING THE ARTWORK", cx, 250, Ui.Ink * (0.8f * fade), 1f);
        g.TextCentred("Originals ported by PFJ. Original versions copyright Durell Software.", cx,
            MathF.Min(safe.Bottom, Gfx.Height) - 20, Ui.Dim * (0.8f * fade), 1f, false);
    }
}
