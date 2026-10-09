using System;
using System.Collections.Generic;
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
        t = _makers.TryGetValue(name, out var m) ? m().ToTexture(device) : Missing(device);
        Textures[name] = t;
        return t;
    }

    /// <summary>Draws every piece whose name starts with <paramref name="prefix"/> now (e.g. when a game starts).</summary>
    public static void Warm(GraphicsDevice device, string prefix)
    {
        foreach (var (n, _) in ArtCatalog.All)
            if (n.StartsWith(prefix, StringComparison.Ordinal)) Get(device, n);
    }

    private static Texture2D Missing(GraphicsDevice d)
    {
        var t = new Texture2D(d, 1, 1);
        t.SetData(new[] { Microsoft.Xna.Framework.Color.Magenta });
        return t;
    }
}
