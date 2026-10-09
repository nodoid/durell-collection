using Durell.Games;
using Durell.Games.Lunar;
using Durell.Input;
using Durell.Machine;
using Microsoft.Xna.Framework;
using Xunit;

namespace Durell.Tests;

public class TiltTests
{
    /// <summary>Gravity for a device held tipped back by <paramref name="back"/> degrees from upright
    /// and rolled <paramref name="roll"/> degrees clockwise (right side down).</summary>
    private static Vector3 Held(float back, float roll)
    {
        float b = MathHelper.ToRadians(back), r = MathHelper.ToRadians(roll);
        var g = new Vector3(0, -MathF.Cos(b), -MathF.Sin(b));
        return new Vector3(g.Y * -MathF.Sin(r), g.Y * MathF.Cos(r), g.Z);
    }

    private static Tilt Settle(Tilt t, Vector3 g)
    {
        t.Provider = () => g;
        for (int i = 0; i < 40; i++) t.Update();
        return t;
    }

    [Fact]
    public void NeutralIsWhereverPlayStarted()
    {
        var t = Settle(new Tilt(), Held(40, 0));
        Assert.True(t.Available);
        Assert.InRange(t.Pitch, -0.5f, 0.5f);
        Assert.InRange(t.Roll, -0.5f, 0.5f);
        Assert.False(t.Left || t.Right || t.Up || t.Down);
    }

    [Fact]
    public void DirectionsFollowTheTilt()
    {
        var t = Settle(new Tilt(), Held(40, 0));
        Settle(t, Held(40, 20));
        Assert.True(t.Right);
        Assert.False(t.Left);
        Settle(t, Held(40, -20));
        Assert.True(t.Left);
        Settle(t, Held(60, 0));          // top edge tipped away (screen leans back further)
        Assert.True(t.Up);
        Assert.False(t.Down);
        Settle(t, Held(20, 0));          // towards you
        Assert.True(t.Down);
    }

    [Fact]
    public void HysteresisHoldsInsideTheDeadZone()
    {
        var t = Settle(new Tilt(), Held(40, 0));
        Settle(t, Held(40, 7));
        Assert.False(t.Right);           // not yet past On
        Settle(t, Held(40, 12));
        Assert.True(t.Right);
        Settle(t, Held(40, 7));
        Assert.True(t.Right);            // still above Off
        Settle(t, Held(40, 3));
        Assert.False(t.Right);
    }

    [Fact]
    public void CentreTakesANewNeutral()
    {
        var t = Settle(new Tilt(), Held(40, 0));
        Settle(t, Held(40, 20));
        t.Centre();
        Settle(t, Held(40, 20));
        Assert.False(t.Right);
    }

    [Fact]
    public void NoSensorMeansNoTilt()
    {
        var t = new Tilt { Provider = () => null };
        t.Update();
        Assert.False(t.Available);
    }

    [Fact]
    public void PowerRisesAsTheTopComesTowardsYou()
    {
        var t = Settle(new Tilt(), Held(50, 0));
        Assert.Equal(0, t.Power());
        Settle(t, Held(35, 0));
        Assert.InRange(t.Power(), 3, 6);
        Settle(t, Held(15, 0));
        Assert.Equal(9, t.Power());
    }

    [Fact]
    public void EveryGameHasTiltHelp()
    {
        foreach (var g in Catalog.All) Assert.False(string.IsNullOrEmpty(g.Controls.TiltHelp), g.Id);
    }

    /// <summary>Lunar Lander only takes the motor keys from tilt while the module flies.</summary>
    [Fact]
    public void LunarFlagsFlight()
    {
        var p = (LunarProgram)Catalog.Get("lunar").Demo();
        bool flew = false;
        for (int f = 0; f < 3000 && !flew; f++)
        {
            p.ClearKeys();
            if (f % 40 < 3) p.SetKey(f % 80 < 40 ? OricKey.D2 : OricKey.Space, true);
            p.RunFrame();
            p.SkipAudio();
            flew = p.Flying;
        }
        Assert.True(flew);
    }
}
