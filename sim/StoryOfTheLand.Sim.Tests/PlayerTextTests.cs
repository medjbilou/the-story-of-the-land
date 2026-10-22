using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using StoryOfTheLand.Sim;

/// <summary>Player-facing text (spec: player-facing text): Cause messages use display names from content and the one date
/// format, never content ids or ISO dates (playtest 1, items 4 and 5), and start with a capital (playtest 2, P2-6).</summary>
public class PlayerTextTests
{
    static readonly string Root = FamilyTests.Root;
    static readonly Content Chapter = ContentLoader.LoadChapter(Root, "1947_1949");
    static readonly Regex IsoDate = new Regex(@"\d{4}-\d{2}-\d{2}");

    public static IEnumerable<object[]> Chapters() =>
        Directory.GetFiles(Path.Combine(Root, ContentLoader.PalestineDir, "events"), "*.yaml").OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    /// <summary>Every Cause of a whole chapter for a family of <paramref name="village"/>, answering every card with its first option.</summary>
    static List<string> Messages(Content c, string village)
    {
        var g = GameSession.New(c, GoldenRun.Seed, village);
        var messages = new List<string>();
        while (g.State.GameOver == null)
            messages.AddRange(g.Step(g.State.PendingEvents.Select(id => (Command)new ChooseEventOption(id, c.Events.Single(e => e.Id == id).Options[0].Id)).ToList())
                .Why.Select(w => w.Message));
        return messages;
    }

    [Theory]
    [MemberData(nameof(Chapters))]
    public void NoCauseShowsAContentIdOrAnIsoDateAndEveryOneStartsWithACapital(string chapter)
    {
        var c = ContentLoader.LoadChapter(Root, chapter);
        var village = c.Events.Select(e => e.Place).FirstOrDefault(p => p != "") ?? c.Places[0].Id;
        var messages = Messages(c, village).Distinct().ToList();
        Assert.NotEmpty(messages);
        var ids = c.Events.Select(e => e.Id).Concat(c.Indicators.Select(i => i.Id)).Concat(c.Events.SelectMany(e => e.Options.Select(o => o.Id)))
            .Where(id => id.Contains('_')).Distinct().Select(id => new Regex($@"\b{Regex.Escape(id)}\b")).ToList();
        var bad = messages.Where(m => IsoDate.IsMatch(m) || ids.Any(r => r.IsMatch(m)) || m.Length == 0 || char.IsLower(m[0])).ToList();
        Assert.True(bad.Count == 0, "Cause text shows ids or ISO dates, or starts lowercase:\n" + string.Join("\n", bad.Take(20)));
    }

    [Fact]
    public void DatesUseTheDayMonthYearFormat()
    {
        Assert.Equal("15 May 1948", PlayerText.Date(new DateTime(1948, 5, 15)));
        Assert.Equal("1 January 1950", PlayerText.Date(new DateTime(1950, 1, 1)));
        Assert.Equal("May 1948", PlayerText.Month(new DateTime(1948, 5, 15)));
    }

    [Fact]
    public void ArabicDatesUseTheLevantineMonthsAndWesternDigits()
    {
        Assert.Equal("15 أيار 1948", PlayerText.Date(new DateTime(1948, 5, 15), arabic: true));
        Assert.Equal("تشرين الأول 2023", PlayerText.Month(new DateTime(2023, 10, 1), arabic: true));
        Assert.Equal("كانون الثاني 1987", PlayerText.Month(new DateTime(1987, 1, 1), arabic: true));
        Assert.Equal("15 May 1948", PlayerText.Date(new DateTime(1948, 5, 15), arabic: false));
        Assert.Equal(12, PlayerText.LevantineMonths.Length);
    }

    [Fact]
    public void TheResourceScaleAndChanges()
    {
        Assert.Equal(30.0, PlayerText.Scale(0.3), 9);
        Assert.Equal("Land +4.1%", PlayerText.Change(Chapter, "land", 0.041));
        Assert.Equal("Land", PlayerText.Target(Chapter, "land"));
        Assert.Equal("الأرض", PlayerText.IndicatorAr(Chapter, "land"));
    }

    [Fact]
    public void ZeroIsUnsignedAndTheSignGoesFirst()
    {
        Assert.Equal("0%", PlayerText.Percent(-0.004));
        Assert.Equal("55%", PlayerText.Percent(0.55));
        Assert.Equal("-5%", PlayerText.Percent(-0.05));
        Assert.Equal("0.0%", PlayerText.PercentChange(-0.0004));
        Assert.Equal("0.0%", PlayerText.PercentChange(0.0004));
        Assert.Equal("+4.1%", PlayerText.PercentChange(0.041));
        Assert.Equal("-4.0%", PlayerText.PercentChange(-0.04));
        Assert.Equal("+5%", PlayerText.PercentChange(0.05, 0));
        Assert.Equal("0%", PlayerText.PercentChange(-0.004, 0));
    }

    [Theory]
    [InlineData(0.626, 0.634, "0%")]       // "63%" -> "63%": no change shown (rounded difference would say "+1%")
    [InlineData(0.624, 0.626, "+1%")]      // "62%" -> "63%": "+1%" (the raw difference rounds to "0%")
    [InlineData(0.55, 0.50, "-5%")]
    public void TheShownChangeIsTheDifferenceOfTheShownValues(double before, double now, string shown)
    {
        Assert.Equal(shown, PlayerText.PercentChangeShown(before, now));
        Assert.Equal(PlayerText.ShownPoints(now) - PlayerText.ShownPoints(before), double.Parse(shown.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ANonFiniteNumberIsAProgrammerError(double v)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerText.Percent(v));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerText.PercentChange(v));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerText.Scale(v));
    }

    /// <summary>Review M3: no silent fallback to the id.</summary>
    [Fact]
    public void AnUnknownIdIsAnError()
    {
        Assert.Throws<InvalidOperationException>(() => PlayerText.Target(Chapter, "no_such_thing"));
        Assert.Throws<InvalidOperationException>(() => PlayerText.IndicatorAr(Chapter, "no_such_thing"));
        Assert.Throws<InvalidOperationException>(() => PlayerText.GameOver("coalition_collapse"));
    }
}
