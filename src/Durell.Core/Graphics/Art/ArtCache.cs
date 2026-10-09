using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Graphics.Art;

/// <summary>The artwork as textures, drawn from <see cref="ArtCatalog"/> the first time each piece is needed.</summary>
internal static class ArtCache
{
    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static Dictionary<string, Func<Canvas>>? _makers;

    public static Texture2D Get(GraphicsDevice device, string name)
    {
        if (Textures.TryGetValue(name, out var t)) return t;
        if (_makers == null)
        {
            _makers = new Dictionary<string, Func<Canvas>>();
            foreach (var (n, make) in ArtCatalog.All) _makers[n] = make;
        }
        if (Drawn.TryRemove(name, out var ready)) t = ready.ToTexture(device);
        else t = _makers.TryGetValue(name, out var m) ? m().ToTexture(device) : Missing(device);
        Textures[name] = t;
        return t;
    }

    /// <summary>Draws every piece whose name starts with <paramref name="prefix"/> now (e.g. when a game starts).</summary>
    public static void Warm(GraphicsDevice device, string prefix)
    {
        foreach (var (n, _) in ArtCatalog.All)
            if (n.StartsWith(prefix, StringComparison.Ordinal)) Get(device, n);
    }

    /// <summary>Artwork drawn in the background, waiting to become textures.</summary>
    private static readonly ConcurrentDictionary<string, Canvas> Drawn = new();
    private static int _drawnCount;
    private static bool _drawing;

    public static int Total => ArtCatalog.All.Count;
    /// <summary>Pieces drawn so far (background or not).</summary>
    public static int Done => Math.Min(Total, Math.Max(_drawnCount, Textures.Count));

    /// <summary>Draws all the artwork on background threads (once); <see cref="Upload"/> makes the textures.</summary>
    public static void DrawAllInBackground()
    {
        if (_drawing) return;
        _drawing = true;
        Task.Run(() => Parallel.ForEach(ArtCatalog.All, item =>
        {
            Drawn[item.Name] = item.Make();
            System.Threading.Interlocked.Increment(ref _drawnCount);
        }));
    }

    /// <summary>Turns up to <paramref name="budget"/> drawn pieces into textures (main thread).</summary>
    public static void Upload(GraphicsDevice device, int budget)
    {
        foreach (var name in Drawn.Keys)
        {
            if (budget-- <= 0) break;
            if (!Textures.ContainsKey(name) && Drawn.TryRemove(name, out var c)) Textures[name] = c.ToTexture(device);
        }
    }

    private static Texture2D Missing(GraphicsDevice d)
    {
        var t = new Texture2D(d, 1, 1);
        t.SetData(new[] { Microsoft.Xna.Framework.Color.Magenta });
        return t;
    }
}
