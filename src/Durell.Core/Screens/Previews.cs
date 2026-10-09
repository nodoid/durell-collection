using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Durell.Games;
using Durell.Graphics;
using Durell.Machine;
using Durell.Programs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Screens;

/// <summary>
/// Every game made ready for the menu: run silently (on background threads, all at once - this is
/// slow on phones) up to its preview moment, kept running as the menu's live card, and its picture
/// drawn into a small thumbnail texture in the current look.
/// </summary>
internal sealed class Previews
{
    private const int TW = 320, TH = 240;
    private readonly DurellGame _game;
    private readonly Dictionary<string, byte[]> _frames = new();
    private readonly Dictionary<string, RenderTarget2D> _thumbs = new();
    private readonly Dictionary<string, Look> _looks = new();
    private readonly Dictionary<string, GameProgram> _programs = new();
    private readonly ConcurrentQueue<(string Id, Look Look, GameProgram Program, byte[] Frame)> _done = new();
    private bool _thumbsEnhanced;
    private bool _started;

    public Previews(DurellGame game) => _game = game;

    public int Total => Catalog.All.Count;
    public int Ready => _frames.Count;

    /// <summary>Starts preparing every game in the background (once).</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        foreach (var info in Catalog.All)
        {
            var i = info;
            Task.Run(() =>
            {
                var look = i.Look();
                var p = i.Demo(look);
                _done.Enqueue((i.Id, look, p, (byte[])p.Index.Clone()));
            });
        }
    }

    /// <summary>Takes in the games that are ready (call once per update); true when all are.</summary>
    public bool Prepare()
    {
        Start();
        while (_done.TryDequeue(out var d))
        {
            _frames[d.Id] = d.Frame;
            _looks[d.Id] = d.Look;
            _programs[d.Id] = d.Program;
        }
        return _frames.Count >= Total;
    }

    /// <summary>The game running at its preview moment (the menu's live card), or null until ready.</summary>
    public GameProgram? Program(string id) => _programs.TryGetValue(id, out var p) ? p : null;
    public Look? LookFor(string id) => _looks.TryGetValue(id, out var l) ? l : null;

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
