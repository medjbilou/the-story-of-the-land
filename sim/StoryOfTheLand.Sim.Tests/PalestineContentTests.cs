using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StoryOfTheLand.Sim;

/// <summary>F4 (docs/palestine-model.md): the family resources and the four chapters converted by tools/tsv_to_events.py
/// load, pass the event rules, and run through EventSystem: an option moves the family resources and writes the Cause log.</summary>
public class PalestineContentTests
{
    static readonly string Root = FamilyTests.Root;
    static readonly string[] Resources = { "land", "home", "family", "documents", "livelihood", "memory", "sumud", "community", "safety", "solidarity" };
    static readonly string[] ResourcesAr = { "الأرض", "البيت", "العائلة", "الوثائق", "الرزق", "الذاكرة", "الصمود", "المجتمع", "الأمان", "التضامن" };

    public static IEnumerable<object[]> Chapters() =>
        Directory.GetFiles(Path.Combine(Root, ContentLoader.PalestineDir, "events"), "*.yaml").OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    [Fact]
    public void TheFourChaptersAreThere() =>
        Assert.Equal(new[] { "1880_1946", "1947_1949", "1950_1966", "1967_1987", "1987_2000", "2000_2023", "2023_2026" }, Chapters().Select(x => (string)x[0]));

    [Theory]
    [MemberData(nameof(Chapters))]
    public void AChapterLoadsWithTheFamilyResourcesAndBothLanguages(string chapter)
    {
        var c = ContentLoader.LoadChapter(Root, chapter);
        Assert.Equal(Resources, c.Indicators.Select(i => i.Id));
        Assert.Equal(ResourcesAr, c.Indicators.Select(i => i.NameAr));
        Assert.NotEmpty(c.Events);
        foreach (var e in c.Events)
        {
            Assert.True(e.TitleAr != "" && e.TextAr != "" && e.Scope != "", e.Id);
            Assert.All(e.Options, o => Assert.True(o.TextAr != "", $"{e.Id}.{o.Id}"));
            Assert.All(e.Effects.Concat(e.Options.SelectMany(o => o.Effects)), fx => Assert.Contains(fx.Target, Resources));
        }
    }

    static GameState Start(Content c, DateTime date)
    {
        var s = new GameState { Date = date };
        foreach (var i in c.Indicators) { s.Indicators[i.Id] = i.Base; s.IndicatorShifts[i.Id] = 0; }
        return s;
    }

    [Theory]
    [MemberData(nameof(Chapters))]
    public void ChoosingAnOptionAppliesTheEventsAndTheOptionsEffects(string chapter)
    {
        var c = ContentLoader.LoadChapter(Root, chapter);
        var e = c.Events.Where(x => x.Scope == "national").OrderBy(x => x.From).ThenBy(x => x.Id, StringComparer.Ordinal).First();
        var o = e.Options[0];
        var s = Start(c, e.From);
        var why = new List<Cause>();
        EventSystem.Trigger(c, s, why);
        Assert.Contains(e.Id, s.PendingEvents);
        Assert.Null(EventSystem.Choose(c, s, e.Id, o.Id, why));
        foreach (var r in Resources)
        {
            var want = 0.5 + e.Effects.Concat(o.Effects).Where(fx => fx.Target == r).Sum(fx => fx.Delta);
            Assert.True(Math.Abs(s.Indicators[r] - Math.Max(0, Math.Min(1, want))) < 1e-9, $"{e.Id}.{o.Id}: {r} is {s.Indicators[r]}, want {want}");
        }
        Assert.Contains(why, w => w.System == "events" && w.Message.Contains(e.Title));
    }

    [Fact]
    public void AFateCardWaitsForTheFamilysVillage()
    {
        var c = ContentLoader.LoadChapter(Root, "1947_1949");
        Assert.Contains(c.Events, e => e.Scope.StartsWith("fate:"));
        var s = Start(c, c.Events.Max(e => e.To!.Value));
        EventSystem.Trigger(c, s, new List<Cause>(), c.Events.Min(e => e.From));
        Assert.DoesNotContain(s.FiredEvents, id => c.Events.First(e => e.Id == id).Scope.StartsWith("fate:"));
    }

    /// <summary>Playtest 3: a turn's cards come in date order. April 1948 brought al-Qastal (3 April), Deir Yassin (9 April)
    /// and Hayfa (21 April) in id order; they now come by day, and every dated month of every chapter is in day order, a
    /// month-wide event (no day) first.</summary>
    [Fact]
    public void AMonthsCardsComeInDateOrder()
    {
        var c = ContentLoader.LoadChapter(Root, "1947_1949");
        var s = Start(c, new DateTime(1948, 4, 1));
        s.Village = "tantura";   // in the Hayfa sub-district, so the region:haifa card reaches the family
        foreach (var e in c.Events.Where(e => e.From < s.Date)) s.FiredEvents.Add(e.Id);
        EventSystem.Trigger(c, s, new List<Cause>(), new DateTime(1948, 3, 1));
        var april = s.PendingEvents.Where(id => id.StartsWith("e1948_04_", StringComparison.Ordinal)).ToList();
        Assert.True(april.IndexOf("e1948_04_qastal") < april.IndexOf("e1948_04_deir_yassin"), string.Join(" ", april));
        Assert.True(april.IndexOf("e1948_04_deir_yassin") < april.IndexOf("e1948_04_haifa"), string.Join(" ", april));
        foreach (var chapter in Chapters().Select(x => (string)x[0]))
        {
            var ch = ContentLoader.LoadChapter(Root, chapter);
            foreach (var month in ch.Events.Where(e => !e.Scope.StartsWith("fate:")).GroupBy(e => e.From))
            {
                var st = Start(ch, month.Key);
                foreach (var e in ch.Events.Where(e => e.From != month.Key)) st.FiredEvents.Add(e.Id);
                st.Village = "tantura";
                EventSystem.Trigger(ch, st, new List<Cause>());
                var days = st.PendingEvents.Select(id => ch.Events.First(e => e.Id == id).Day ?? 0).ToList();
                Assert.Equal(days.OrderBy(d => d), days);
            }
        }
    }

    const string Model = "indicators:\n  - { id: land, base: 0.5, name: Land, name_ar: \"الأرض\" }\n";
    internal const string Chapter = "events:\n  - id: e\n    title: T\n    text: X\n    scope: national\n    from: 1948-01-01\n    to: 1948-02-01\n"
        + "    effects:\n      - { target: land, delta: -0.05 }\n    options:\n      - { id: a, text: A }\n    sources:\n      - { url: \"https://example.org\", checked: 2026-10-19 }\n";

    const string Places = "places:\n  - { id: tantura, name: Tantura, name_ar: \"الطنطورة\", lon: 34.9177, lat: 32.6094, sources: [\"https://example.org\"] }\n";

    const string Currency = "currencies:\n  - { id: p, from_year: 1927, from_month: 11, region: national, name: P, name_ar: \"ج\", symbol: P, symbol_ar: \"ج\", sources: [\"https://example.org\"] }\nregions: []\n";

    internal static Content Load(string chapter, string model = Model) =>
        ContentLoader.LoadChapter(rel => rel == "model.yaml" ? model : rel == "events/test.yaml" ? chapter
            : rel == "places.yaml" ? Places : rel == "fates.yaml" ? "fates: []\n" : rel == "currency.yaml" ? Currency : null, "test");

    [Theory]
    [InlineData("scope: national", "scope: village", "scope")]
    [InlineData("target: land", "target: morale", "unknown target")]
    [InlineData("    scope: national\n", "    scope: national\n    sensitiv: 1\n", "sensitiv")]
    [InlineData("title: T", "title: \"{village_en}\"", "placeholder")]
    [InlineData("scope: national", "scope: \"region:nowhere\"", "unknown place 'nowhere'")]
    [InlineData("    scope: national\n", "    scope: national\n    reviewed_by: someone@example.org\n    reviewed_on: 2026-10-15\n", "events[e].reviewed_by")]   // #375
    [InlineData("    scope: national\n", "    scope: national\n    reviewed_by: jdoe\n", "events[e].reviewed_on")]
    [InlineData("    to: 1948-02-01\n", "    to: 1948-02-01\n    day: 32\n", "events[e].day")]   // playtest 3: a day of the From month
    public void TheLoaderRefusesABadChapter(string from, string to, string message)
    {
        Assert.Single(Load(Chapter).Events);
        var ex = Assert.Throws<ContentException>(() => Load(Chapter.Replace(from, to)));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void TheLoaderRefusesAResourceWithoutAnArabicName()
    {
        var ex = Assert.Throws<ContentException>(() => Load(Chapter, Model.Replace(", name_ar: \"الأرض\"", "")));
        Assert.Contains("indicators[land].name_ar", ex.Message);
    }

    [Fact]
    public void AFateTemplateMayNameTheVillage() =>
        Assert.Single(Load(Chapter.Replace("scope: national", "scope: \"fate:destroyed\"").Replace("title: T", "title: \"{village_en} is destroyed\"")).Events);
}
