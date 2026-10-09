using System;
using System.Collections.Generic;
using Durell.Games;
using Durell.Graphics;
using Durell.Machine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Screens;

/// <summary>
/// Picture thumbnails of every game for the menu: each game is run silently up to its title screen
/// once, and the picture drawn into a small texture in the current look.
/// </summary>
internal sealed class Previews
{
    private const int TW = 320, TH = 240;
    private readonly DurellGame _game;
    private readonly Dictionary<string, byte[]> _frames = new();
    private readonly Dictionary<string, RenderTarget2D> _thumbs = new();
    private readonly Dictionary<string, Look> _looks = new();
    private bool _thumbsEnhanced;
    private int _next;

    public Previews(DurellGame game) => _game = game;

    /// <summary>Runs one more game up to its title screen (called once per update until all are done).</summary>
    public bool Prepare()
    {
        if (_next >= Catalog.All.Count) return true;
        var info = Catalog.All[_next++];
        var look = info.Look();
        var p = info.Demo(look);
        _frames[info.Id] = (byte[])p.Index.Clone();
        _looks[info.Id] = look;
        return _next >= Catalog.All.Count;
    }

    public byte[]? Frame(string id) => _frames.TryGetValue(id, out var f) ? f : null;

    /// <summary>The thumbnail of a game (null until it is ready); call during Draw.</summary>
    public Texture2D? Thumb(Gfx g, string id)
    {
        bool enhanced = _game.Enhanced;
        if (enhanced != _thumbsEnhanced)
        {
            foreach (var t in _thumbs.Values) t.Dispose();
            _thumbs.Clear();
            _thumbsEnhanced = enhanced;
        }
        if (_thumbs.TryGetValue(id, out var rt)) return rt;
        if (!_frames.TryGetValue(id, out var frame)) return null;
        rt = new RenderTarget2D(g.Device, TW, TH, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
        g.End();
        var saveW = g.Width;
        var saveScale = g.Scale;
        g.Device.SetRenderTarget(rt);
        g.Device.Clear(Color.Black);
        g.BeginFrame(TW, 1, Vector2.Zero);
        _game.Renderer.Draw(g, frame, new RectangleF(0, 0, TW, TH), _looks[id], enhanced, false);
        g.End();
        g.Device.SetRenderTarget(null);
        g.BeginFrame(saveW, saveScale, _game.GfxOffset);
        _thumbs[id] = rt;
        return rt;
    }
}
