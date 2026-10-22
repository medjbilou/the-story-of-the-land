using StoryOfTheLand.Sim;

public class ReleaseGateTests
{
    /// <summary>R-GATE-001: an event with a sensitive tag must be sensitivity-reviewed before a shareable build
    /// (Build -shareable refuses while any chapter lists one).</summary>
    [Theory]
    [InlineData("displacement")]
    [InlineData("massacre")]
    public void AnUnreviewedSensitiveEventIsRefused(string tag)
    {
        var tagged = PalestineContentTests.Chapter.Replace("    scope: national\n", $"    scope: national\n    tags: [{tag}]\n");
        Assert.Equal(new[] { "e" }, ReleaseGate.SensitiveUnreviewed(PalestineContentTests.Load(tagged)));
        Assert.Empty(ReleaseGate.SensitiveUnreviewed(PalestineContentTests.Load(tagged.Replace("    scope: national\n", "    scope: national\n    status: sensitivity-reviewed\n"))));
        Assert.Empty(ReleaseGate.SensitiveUnreviewed(PalestineContentTests.Load(PalestineContentTests.Chapter)));
    }
}
