using System;
using StoryOfTheLand.Sim;

/// <summary>Fixed reference values for the generator. See docs/determinism.md.</summary>
public class DeterminismTests
{
    /// <summary>The first three outputs of Vigna's reference splitmix64.c (https://prng.di.unimi.it/splitmix64.c)
    /// for each seed, taken from the reference C code, not from this implementation.</summary>
    [Theory]
    [InlineData(0UL, 0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL, 0x06C45D188009454FUL)]
    [InlineData(1234567UL, 6457827717110365317UL, 3203168211198807973UL, 9817491932198370423UL)]
    public void SplitMix64MatchesTheReferenceVectors(ulong seed, ulong first, ulong second, ulong third)
    {
        var rng = new Rng(seed);
        Assert.Equal(first, rng.NextULong());
        Assert.Equal(second, rng.NextULong());
        Assert.Equal(third, rng.NextULong());
    }
}
