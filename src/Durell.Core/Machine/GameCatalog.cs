using System;
using System.Collections.Generic;
using System.Reflection;

namespace Durell.Machine;

/// <summary>The translated Oric programs in the collection.</summary>
internal static class ProgramCatalog
{
    private static readonly Dictionary<string, Func<Cpu6502>> Factories = new()
    {
        ["harrier"] = () => new Games.Harrier.HarrierCode(),
        ["scuba"] = () => new Games.Scuba.ScubaCode(),
        ["starfighter"] = () => new Games.StarFighter.StarFighterCode(),
        ["galaxy"] = () => new Games.Galaxy.GalaxyCode(),
        ["turbo"] = () => new Games.Turbo.TurboCode(),
    };

    public static IEnumerable<string> Names => Factories.Keys;

    public static OricMachine Create(string name)
    {
        var cpu = Factories[name]();
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name + ".snap")
                      ?? throw new InvalidOperationException("missing snapshot " + name);
        return new OricMachine(cpu, OricMachine.LoadSnapshot(s));
    }
}
