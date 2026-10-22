using StoryOfTheLand.Sim;

/// <summary>A sign-off's shape (#375): `reviewed_by` a GitHub handle, `reviewed_on` a day. The map's readers in Unity check
/// their rows with it (ReviewRecordCasesTests runs the shared cases, tools/tests/data/review_record_cases.json).</summary>
public class ReviewRecordTests
{
    [Theory]
    [InlineData("jdoe", true)]
    [InlineData("a", true)]
    [InlineData("jdoe-2", true)]
    [InlineData("jdoe-4", true)]
    [InlineData("JDoe3", true)]
    [InlineData("", false)]
    [InlineData("jdoe@example.org", false)]
    [InlineData("jdoe-", false)]
    [InlineData("j_doe", false)]
    [InlineData("dependabot[bot]", false)]
    public void TheHandleShapeIsGitHubs(string handle, bool ok) => Assert.Equal(ok, ReviewRecord.IsHandle(handle));

    [Theory]
    [InlineData("2026-10-15", true)]
    [InlineData("2024-02-29", true)]
    [InlineData("2026-02-30", false)]
    [InlineData("15 October 2026", false)]
    [InlineData("2026-10-15\n", false)]
    public void TheDayIsARealYyyyMmDd(string day, bool ok) => Assert.Equal(ok, ReviewRecord.IsDay(day));

    [Fact]
    public void ProblemNamesTheMissingOrBadKey()
    {
        Assert.Null(ReviewRecord.Problem(false, null, false, null));
        Assert.Null(ReviewRecord.Problem(true, "jdoe", true, "2026-10-15"));
        Assert.Equal("reviewed_on", ReviewRecord.Problem(true, "jdoe", false, null)!.Value.field);
        Assert.Equal("reviewed_by", ReviewRecord.Problem(false, null, true, "2026-10-15")!.Value.field);
        Assert.Equal("reviewed_by", ReviewRecord.Problem(true, "jdoe@example.org", true, "2026-10-15")!.Value.field);
        Assert.Equal("reviewed_on", ReviewRecord.Problem(true, "jdoe", true, "yesterday")!.Value.field);
    }
}
