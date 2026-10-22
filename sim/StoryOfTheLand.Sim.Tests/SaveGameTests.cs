using System;
using System.Linq;
using StoryOfTheLand.Sim;

/// <summary>Saves: seed + command log + content hash + schema version; loading replays to the same state hash. The round
/// trip with the village is in FamilyTests; these are the format's refusals.</summary>
public class SaveGameTests
{
    static readonly Content C = ContentLoader.LoadChapter(FamilyTests.Root, "1947_1949");

    /// <summary>Three months of Tantura's game, the first card of each month answered.</summary>
    static string Played()
    {
        var g = GameSession.New(C, 7, "tantura");
        for (int i = 0; i < 3; i++)
            g.Step(new Command[] { new ChooseEventOption(g.State.PendingEvents[0], C.Events.Single(e => e.Id == g.State.PendingEvents[0]).Options[0].Id) });
        return g.Save.Write();
    }

    static string Replace(int line, string text)
    {
        var lines = Played().Split('\n');
        lines[line < 0 ? lines.Length - 2 : line - 1] = text;
        return string.Join("\n", lines);
    }

    [Fact]
    public void TheSaveRecordsTheHeaderAndReplaysExactly()
    {
        var text = Played();
        Assert.StartsWith("storyoftheland-save 3\ncontent " + C.Hash + "\nseed 7\nmode Historical\nruleset family-0.1\nvillage tantura\nturn\nevent ", text);
        var g = GameSession.Load(C, text, out var warnings);
        Assert.Empty(warnings);
        Assert.Equal(text, g.Save.Write());
        Assert.Equal(3, g.State.Turn);
        Assert.NotNull(g.Previous);
    }

    // R-SAVE-001, R-SAVE-002
    [Theory]
    [InlineData(1, "storyoftheland-save 4", SaveException.NewerVersion)]
    [InlineData(1, "storyoftheland-save 2", SaveException.OlderRules)]   // a save from older rules
    [InlineData(5, "ruleset legacy", SaveException.OlderRules)]
    [InlineData(5, "ruleset gdd-0.1", SaveException.OlderRules)]
    [InlineData(5, "ruleset chess", SaveException.NewerVersion)]
    [InlineData(2, "content xyz", null)]
    [InlineData(4, "mode Open", null)]
    [InlineData(6, "village -", null)]
    [InlineData(6, "village nowhere", null)]   // a known id shape, but no such place
    [InlineData(-1, "end 12", null)]
    [InlineData(8, "policy rationing 3FE0000000000000", null)]   // an order this game does not have
    [InlineData(8, "event bad%ZZ x", null)]
    public void ABadLineIsRefusedWithItsCode(int line, string text, string? code)
    {
        var ex = Assert.Throws<SaveException>(() => GameSession.Load(C, Replace(line, text), out _));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void OtherContentLoadsWithAWarningAndAnEditedOrderIsDamage()
    {
        var text = Played();
        var other = text.Replace("content " + C.Hash, "content " + new string('a', 64));
        GameSession.Load(C, other, out var warnings);
        Assert.Contains(GameSession.ContentChanged, warnings);
        var lines = text.Split('\n');
        var i = Array.FindIndex(lines, l => l.StartsWith("event ", StringComparison.Ordinal));
        lines[i] = string.Join(" ", lines[i].Split(' ').Take(2)) + " " + C.Events.Single(e => e.Id == lines[i].Split(' ')[1]).Options.Last().Id;
        var ex = Assert.Throws<SaveException>(() => GameSession.Load(C, string.Join("\n", lines), out _));
        Assert.Contains("does not match the saved game", ex.Message);
    }

    [Fact]
    public void TruncatedCrLfAndBomText()
    {
        var text = Played();
        Assert.Throws<SaveException>(() => SaveGame.Parse(text.Substring(0, 40)));
        Assert.Equal(GameSession.Load(C, text, out _).State.Hash(), GameSession.Load(C, "﻿" + text.Replace("\n", "\r\n"), out _).State.Hash());
        var huge = string.Join("\n", text.Split('\n').Take(6)) + "\n" + string.Concat(Enumerable.Repeat("turn\n", SaveGame.MaxTurns + 1)) + "end " + new string('b', 64) + "\n";
        Assert.Contains("the most this game plays is", Assert.Throws<SaveException>(() => SaveGame.Parse(huge)).Message);
    }

    [Fact]
    public void TheEscapingIsOurOwnAndARecordedTurnIsImmutable()
    {
        var g = GameSession.New(C, 1, "tantura");
        var orders = new Command[] { new ChooseEventOption("a b/é", "x") };
        g.Step(orders);
        Assert.Contains("\nevent a%20b%2F%C3%A9 x\n", g.Save.Write());
        orders[0] = new ChooseEventOption("changed", "y");
        Assert.Equal("a b/é", ((ChooseEventOption)g.Save.Turns[0][0]).EventId);
        Assert.Throws<ArgumentNullException>(() => g.Step(new Command[] { new ChooseEventOption(null!, "x") }));
        Assert.Throws<ArgumentException>(() => g.Step(new Command[] { new ChooseEventOption("\ud800", "x") }));
    }
}
