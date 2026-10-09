using System;
using System.Collections.Generic;

namespace Durell.Graphics.Art;

/// <summary>Every piece of the ENHANCED artwork by name (the games draw from it; tools/Durell.ArtLab previews it).</summary>
internal static class ArtCatalog
{
    public static readonly List<(string Name, Func<Canvas> Make)> All = new();

    static ArtCatalog()
    {
        HarrierArt.Register(All);
        ScubaArt.Register(All);
        GalaxyArt.Register(All);
        StarFighterArt.Register(All);
        LunarArt.Register(All);
    }
}
