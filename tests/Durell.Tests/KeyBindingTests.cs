using Durell.Input;
using Durell.Machine;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Durell.Tests;

public class KeyBindingTests
{
    [Fact]
    public void OriginalKeysByDefault()
    {
        var b = KeyBindings.From(new());
        Assert.Equal(OricKey.Space, b.Map(Keys.Space));
        Assert.Equal(OricKey.Z, b.Map(Keys.Z));
        Assert.Equal(OricKey.D3, b.Map(Keys.D3));
        Assert.Equal(Keys.Enter, b.HostFor(OricKey.Return));
        Assert.Equal(Keys.Q, b.HostFor(OricKey.Q));
    }

    [Fact]
    public void AMovedActionLeavesItsOldKey()
    {
        // fire rockets (Oric SPACE) moved to Left Ctrl
        var b = KeyBindings.From(new() { ["Space"] = "LeftControl" });
        Assert.Equal(OricKey.Space, b.Map(Keys.LeftControl));
        Assert.Null(b.Map(Keys.Space));
        Assert.Equal(Keys.LeftControl, b.HostFor(OricKey.Space));
    }

    [Fact]
    public void ATakenKeyLeavesItsOldAction()
    {
        // fire (SPACE) moved onto the UP arrow: UP no longer climbs
        var b = KeyBindings.From(new() { ["Space"] = "Up" });
        Assert.Equal(OricKey.Space, b.Map(Keys.Up));
        Assert.Null(b.HostFor(OricKey.Up));
    }

    [Fact]
    public void EveryActionHasAKeyAtFirst()
    {
        var b = KeyBindings.From(new());
        foreach (var g in Durell.Games.Catalog.All)
            foreach (var (label, key) in g.Controls.Actions)
                Assert.True(b.HostFor(key) != null, $"{g.Id}: {label} has no key");
    }
}
