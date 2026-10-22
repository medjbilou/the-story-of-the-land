using System;
using StoryOfTheLand.Sim;

public class CoreTests
{
    [Fact]
    public void RngIsDeterministicForSameSeed()
    {
        var a = new Rng(42); var b = new Rng(42);
        for (int i = 0; i < 100; i++) Assert.Equal(a.NextDouble(), b.NextDouble());
    }

    [Fact]
    public void RngValuesAreInUnitInterval()
    {
        var r = new Rng(7);
        for (int i = 0; i < 1000; i++) { var v = r.NextDouble(); Assert.True(v >= 0.0 && v < 1.0, $"NextDouble out of [0,1): {v}"); }
    }

    [Fact]
    public void HashIsStableAndSensitiveToState()
    {
        var s1 = new GameState { Date = new DateTime(1948, 5, 15), RngState = 1 };
        s1.Indicators["land"] = 0.5;
        var s2 = s1.Clone();
        Assert.Equal(s1.Hash(), s2.Hash());
        s2.Indicators["land"] = 0.6;
        Assert.NotEqual(s1.Hash(), s2.Hash());
        Assert.Equal(0.5, s1.Indicators["land"]); // clone is deep
    }
    [Fact]
    public void HashTreatsNegativeZeroAsZero()
    {
        var a = new GameState { Date = new DateTime(1948, 5, 15) };
        a.Indicators["land"] = 0.0;
        var b = a.Clone(); b.Indicators["land"] = -0.0;
        Assert.Equal(a.Hash(), b.Hash());
    }

    [Fact]
    public void HashSeesTheLastBitOfADouble()
    {
        var a = new GameState { Date = new DateTime(1948, 5, 15) };
        a.Indicators["land"] = 0.1;
        var b = a.Clone(); b.Indicators["land"] = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(0.1) + 1);
        Assert.NotEqual(a.Indicators["land"], b.Indicators["land"]);
        Assert.NotEqual(a.Hash(), b.Hash());
    }
    [Fact]
    public void CloneIsolatesIndicatorShiftsAndHashSeesThem()
    {
        var a = new GameState { Date = new DateTime(1948, 5, 15) };
        a.IndicatorShifts["land"] = 0.0;
        var b = a.Clone();
        b.IndicatorShifts["land"] = 0.1;
        Assert.Equal(0.0, a.IndicatorShifts["land"]);
        Assert.NotEqual(a.Hash(), b.Hash());
    }
}
