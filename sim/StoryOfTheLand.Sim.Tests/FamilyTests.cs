using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StoryOfTheLand.Sim;

/// <summary>F5 (docs/palestine-model.md): the family's game on the shipped chapters, places and fates. Mirrors the
/// prototype's CampaignTests (unity/Assets/Tests/CampaignTests.cs at the project root).</summary>
public class FamilyTests
{
    /// <summary>The repository's content/ folder, found above the test binaries.</summary>
    internal static readonly string Root = ContentRoot();

    static string ContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "content", ContentLoader.PalestineDir, "model.yaml"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("content/palestine/model.yaml not found above the test folder"), "content");
    }
    static readonly Command[] None = Array.Empty<Command>();
    static readonly Dictionary<string, Content> Chapters = new[] { "1947_1949", "1967_1987", "2023_2026" }
        .ToDictionary(id => id, id => ContentLoader.LoadChapter(Root, id));

    static GameSession SkipTo(GameSession g, int year, int month)
    {
        while (g.State.Date < new DateTime(year, month, 1)) g.Step(None);
        return g;
    }

    [Theory]
    [InlineData("tantura")]
    [InlineData("khan_yunis")]
    [InlineData("imwas")]
    [InlineData("haifa")]
    public void NationalEventsReachEveryFamily(string village)
    {
        var c = Chapters["1947_1949"];
        var g = GameSession.New(c, 1, village);
        Assert.Equal(new DateTime(1947, 11, 1), g.State.Date);
        while (g.State.GameOver == null)
        {
            Assert.All(c.Events.Where(e => e.Scope == "national" && e.From == g.State.Date), e => Assert.Contains(e.Id, g.State.PendingEvents));
            g.Step(None);
        }
        Assert.Equal(c.End, g.State.Date);
    }

    [Fact]
    public void RegionEventsReachOnlyNearbyVillages()
    {
        var c = Chapters["1947_1949"];
        Assert.True(Family.Near(c, "tantura", "haifa"), "Haifa is about 23 km from Tantura");
        Assert.False(Family.Near(c, "tantura", "jaffa"), "Jaffa is about 65 km from Tantura");
        var tantura = SkipTo(GameSession.New(c, 1, "tantura"), 1948, 4).State.PendingEvents;
        Assert.Contains("e1948_04_haifa", tantura);
        Assert.DoesNotContain("e1948_04_jaffa_siege", tantura);
        var jaffa = SkipTo(GameSession.New(c, 1, "jaffa"), 1948, 4).State.PendingEvents;
        Assert.Contains("e1948_04_jaffa_siege", jaffa);
        Assert.DoesNotContain("e1948_04_haifa", jaffa);
    }

    [Fact]
    public void ATanturaFamilyGetsItsSourcedFateInMay1948AndItsChoiceApplies()
    {
        var c = Chapters["1947_1949"];
        var g = SkipTo(GameSession.New(c, 1, "tantura"), 1948, 4);
        Assert.DoesNotContain("fate_depopulated", g.State.FiredEvents);
        g.Step(None);
        Assert.Equal(new DateTime(1948, 5, 1), g.State.Date);
        Assert.Contains("fate_depopulated", g.State.PendingEvents);
        Assert.Contains("e1948_05_tantura", g.State.PendingEvents);
        Assert.Contains(g.Last!.Why, w => w.Message == "New: The day Tantura was emptied");   // {village_en} filled

        var fate = c.Events.Single(e => e.Id == "fate_depopulated");
        Assert.Equal("يوم التهجير من الطنطورة", Family.Fill(c, g.State, fate.TitleAr));   // {village_ar} filled
        var before = new Dictionary<string, double>(g.State.Indicators);
        var r = g.Step(new Command[] { new ChooseEventOption("fate_depopulated", "key_and_deeds") });
        Assert.Empty(r.Rejected);
        Assert.Equal("key_and_deeds", r.State.ChosenOptions["fate_depopulated"]);
        Assert.Equal("", r.State.ChosenOptions["e1948_05_tantura"]);   // unanswered: it happened, with no choice
        // Documents moves only by the choice this month: +10 points.
        Assert.Equal(before["documents"] + 0.10, r.State.Indicators["documents"], 9);
        Assert.Contains(r.Why, w => w.System == "events" && w.Target == "home" && Math.Abs(w.Delta + 0.15) < 1e-9 && w.Message.Contains("The day Tantura was emptied"));
        Assert.Contains(r.Why, w => w.System == "events" && w.Target == "documents" && Math.Abs(w.Delta - 0.10) < 1e-9);
        Assert.Contains(r.Why, w => w.Target == "fate_depopulated" && w.Message == "Chosen: Carry the key and the land deeds");
        Assert.Contains(r.Why, w => w.Target == "e1948_05_tantura" && w.Message == "Unanswered: no choice was made");

        // Tantura's destroyed and built_over fates are undated: kept in the data, never fired.
        Assert.Contains(c.Fates, f => f.Place == "tantura" && f.Fate == "destroyed" && f.Year == null);
        while (g.State.GameOver == null) g.Step(None);
        Assert.DoesNotContain("fate_destroyed", g.State.FiredEvents);
        Assert.DoesNotContain("fate_built_over", g.State.FiredEvents);
    }

    [Fact]
    public void The1967ChapterFiresImwassFateInJune1967AndAYearOnlyFateInDecember()
    {
        var c = Chapters["1967_1987"];
        var g = GameSession.New(c, 1, "imwas");
        Assert.Equal(new DateTime(1967, 6, 1), g.State.Date);
        Assert.Contains("e1967_06_naksa", g.State.PendingEvents);
        Assert.Contains("fate1967_depopulated", g.State.PendingEvents);
        // Imwas's destruction has a year (1967) and no month: it counts from December.
        SkipTo(g, 1967, 11);
        Assert.DoesNotContain("fate1967_destroyed", g.State.FiredEvents);
        g.Step(None);
        Assert.Contains("fate1967_destroyed", g.State.PendingEvents);
        // Another family hears of neither.
        Assert.DoesNotContain("fate1967_depopulated", GameSession.New(c, 1, "khan_yunis").State.PendingEvents);
    }

    [Fact]
    public void TheGazaChapterOpensOnTheSeventhOfOctoberForKhanYunis()
    {
        var g = GameSession.New(Chapters["2023_2026"], 1, "khan_yunis");
        Assert.Equal(new DateTime(2023, 10, 1), g.State.Date);
        Assert.Contains("gz_2023_10_attack", g.State.PendingEvents);
    }

    [Fact]
    public void TheSaveRoundTripsWithTheVillage()
    {
        var c = Chapters["1947_1949"];
        var g = SkipTo(GameSession.New(c, 42, "tantura"), 1948, 5);
        g.Step(new Command[] { new ChooseEventOption("fate_depopulated", "food_children"), new ChooseEventOption("nowhere", "x") });
        Assert.Single(g.Last!.Rejected);   // saved and replayed with its refusal
        var text = g.Save.Write();
        Assert.StartsWith("storyoftheland-save 3\n", text);
        Assert.Contains("\nruleset family-0.1\nvillage tantura\n", text);
        var back = GameSession.Load(c, text, out var warnings);
        Assert.Empty(warnings);
        Assert.Equal("tantura", back.State.Village);
        Assert.Equal(g.State.Hash(), back.State.Hash());
        // Another village is another game: its replay does not end on the saved state.
        Assert.Throws<SaveException>(() => GameSession.Load(c, text.Replace("village tantura", "village imwas"), out _));
        Assert.Throws<SaveException>(() => GameSession.Load(c, text.Replace("village tantura", "village -"), out _));
        Assert.Throws<SaveException>(() => GameSession.Load(c, text.Replace("village tantura", "village nowhere"), out _));
    }

    static string Play(ulong seed, string village)
    {
        var c = Chapters["1947_1949"];
        var g = GameSession.New(c, seed, village);
        while (g.State.GameOver == null)
            g.Step(g.State.PendingEvents.Select(id => (Command)new ChooseEventOption(id, c.Events.Single(e => e.Id == id).Options.Last().Id)).ToList());
        return g.State.Hash();
    }

    [Fact]
    public void TheSameSeedAndVillageGiveTheSameStateHash()
    {
        Assert.Equal(Play(7, "tantura"), Play(7, "tantura"));
        Assert.NotEqual(Play(7, "tantura"), Play(7, "khan_yunis"));
        Assert.Contains("village=tantura", GameSession.New(Chapters["1947_1949"], 7, "tantura").State.Describe());
    }

    [Fact]
    public void AChapterNeedsAKnownVillage()
    {
        var c = Chapters["1947_1949"];
        Assert.Equal(SaveGame.FamilyRuleset, c.Ruleset);
        Assert.Matches("^[0-9a-f]{64}$", c.Hash);
        Assert.Throws<ArgumentException>(() => Simulation.NewGame(c, 1, "nowhere"));
    }

    /// <summary>The family goldens (content/palestine/goldens.yaml, docs/determinism.md), played as Unity's GoldenCheck plays
    /// them: the golden seed, every card answered with its first choice, to the chapter's end.</summary>
    [Fact]
    public void TheFamilyGoldensHold()
    {
        var lines = File.ReadAllLines(Path.Combine(Root, ContentLoader.PalestineDir, "goldens.yaml")).Where(l => l.StartsWith("family-", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(lines);
        foreach (var line in lines)
        {
            var (name, expected) = (line.Split(':')[0], line.Split(':')[1].Trim());
            var parts = name.Split('-');
            var c = ContentLoader.LoadChapter(Root, parts[1]);
            var g = GameSession.New(c, GoldenRun.Seed, parts[2]);
            while (g.State.GameOver == null)
                g.Step(g.State.PendingEvents.Select(id => (Command)new ChooseEventOption(id, c.Events.Single(e => e.Id == id).Options[0].Id)).ToList());
            Assert.True(expected == g.State.Hash(), $"{name}: {g.State.Hash()}, expected {expected}\n{g.State.Describe()}");
        }
    }
}
