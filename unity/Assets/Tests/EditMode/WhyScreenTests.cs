using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>The Why screen of the family game (FamilyGame.Why): the last month's cards, what the family chose and what
/// changed, the span of the month, and when it is rebuilt.</summary>
public class WhyScreenTests
{
    static UiText Text => FamilyKit.En;

    /// <summary>The rows on screen, headings and lines in order, as "# heading" and "line".</summary>
    static List<string> Rows(UiTestKit ui) =>
        ui.Q<ScrollView>("why-rows").Query<Label>().ToList()
            .Where(l => l.ClassListContains("why-group") || l.ClassListContains("why-line"))
            .Select(l => l.ClassListContains("why-group") ? "# " + l.text : l.text).ToList();

    [Test]
    public void EachCardOfTheLastMonthIsListedWithWhatTheFamilyChose()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        new WhyPresenter(ui.Root, bridge, Text);
        Assert.AreEqual("Nothing has happened yet: end the turn to see its causes.", ui.Q<Label>("why-empty").text);
        var before = bridge.State.Clone();
        var cards = bridge.State.PendingEvents.Select(id => bridge.Content.Events.First(e => e.Id == id)).ToList();
        Assert.IsNotEmpty(cards, "precondition: the first month has cards");
        FamilyKit.PlayMonth(bridge);
        Assert.AreEqual("1 November 1947 to 1 December 1947", ui.Q<Label>("why-turn").text);
        var rows = Rows(ui);
        foreach (var e in cards)
        {
            var heading = rows.IndexOf("# " + FamilyGame.Title(bridge.Content, before, e, false));
            Assert.GreaterOrEqual(heading, 0, $"{e.Id} has its heading: " + string.Join(" | ", rows));
            Assert.AreEqual(Text.Format("event.you_chose", FamilyGame.Label(bridge.Content, before, e, e.Options[0], false)), rows[heading + 1],
                $"{e.Id}: what the family chose comes first");
        }
        Assert.IsFalse(rows.Any(r => r.Contains("{") || r.Contains("}")), "every line is filled");
    }

    [Test]
    public void TheRowsAreTheFamilysWhyInOrder()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        new WhyPresenter(ui.Root, bridge, Text);
        var r = bridge.EndTurn();
        var expected = FamilyGame.Why(bridge.Content, bridge.Session.Previous!, bridge.State, r.Why, Text)
            .SelectMany(g => new[] { "# " + g.Heading }.Concat(g.Lines)).ToList();
        Assert.IsNotEmpty(expected);
        Assert.AreEqual(expected, Rows(ui));
        StringAssert.Contains(Text["why.family.unanswered"], string.Join("\n", expected), "a card left unanswered says so");
    }

    [Test]
    public void ADisposedPanelHearsNothing()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        var why = new WhyPresenter(ui.Root, bridge, Text);
        why.Dispose();
        bridge.EndTurn();
        Assert.IsNotNull(ui.Root.Q<Label>("why-empty"), "still the empty note: the panel did not re-render");
    }

    static (UiTestKit ui, SimBridge bridge, WhyPresenter why) Open()
    {
        var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        return (ui, bridge, new WhyPresenter(ui.Root, bridge, Text));
    }

    /// <summary>A cause that names no card and no family resource (another system's) is not shown, raw or otherwise.</summary>
    [Test]
    public void ACauseThatIsNoCardIsNotShown()
    {
        var (ui, bridge, why) = Open();
        using (ui)
        {
            bridge.EndTurn();
            var real = bridge.Session.Last!;
            var rows = Rows(ui);
            var why2 = new List<Cause>(real.Why) { new Cause("zzz", "t", 0, "something odd happened") };
            why.Show(new StepResult(real.State, why2, real.Rejected));
            Assert.AreEqual(rows, Rows(ui));
            Assert.IsFalse(ui.Q<ScrollView>("why-rows").Query<Label>().ToList().Any(l => l.text.Contains("zzz") || l.text.ToLowerInvariant().Contains("something odd")),
                "the raw cause is never shown");
        }
    }

    [Test]
    public void ATurnWithNoCausesSaysSo()
    {
        var (ui, bridge, why) = Open();
        using (ui)
        {
            bridge.EndTurn();
            var real = bridge.Session.Last!;
            why.Show(new StepResult(real.State, new Cause[0], real.Rejected));
            Assert.AreEqual("Nothing notable happened this turn.", ui.Q<Label>("why-nothing").text);
        }
    }

    [Test]
    public void ASecondTurnReplacesTheFirstAndAnUnchangedResultIsNotRebuilt()
    {
        var (ui, bridge, why) = Open();
        using (ui)
        {
            bridge.EndTurn();
            var first = ui.Q<ScrollView>("why-rows").Query<Label>(className: "why-line").ToList();
            why.Render();
            Assert.AreSame(first[0], ui.Q<ScrollView>("why-rows").Query<Label>(className: "why-line").ToList()[0], "same result: no rebuild");
            var r = bridge.EndTurn();
            var second = ui.Q<ScrollView>("why-rows").Query<Label>(className: "why-line").ToList();
            Assert.AreEqual(FamilyGame.Why(bridge.Content, bridge.Session.Previous!, bridge.State, r.Why, Text).Sum(g => g.Lines.Count), second.Count);
            Assert.AreNotSame(first[0], second[0]);
            Assert.AreEqual("1 December 1947 to 1 January 1948", ui.Q<Label>("why-turn").text);
        }
    }

    [Test]
    public void ReplacingTheSessionRerenders()
    {
        var (ui, bridge, why) = Open();
        using (ui)
        {
            bridge.EndTurn();
            Assert.IsNull(ui.Root.Q<Label>("why-empty"));
            bridge.Replace(FamilyKit.Session());
            Assert.IsNotNull(ui.Root.Q<Label>("why-empty"), "a new game has no last turn");
            var loaded = FamilyKit.Session();
            loaded.Step(new Command[0]);
            bridge.Replace(loaded);
            Assert.IsNull(ui.Root.Q<Label>("why-empty"));
            Assert.AreEqual(FamilyGame.Why(loaded.Content, loaded.Previous!, loaded.State, loaded.Last!.Why, Text).Sum(g => g.Lines.Count),
                ui.Q<ScrollView>("why-rows").Query<Label>(className: "why-line").ToList().Count);
        }
    }

    /// <summary>#701: in the Arabic UI the month's span names its months in Arabic (date.*), not "1 November 1947".</summary>
    [Test]
    public void InArabicTheTurnsSpanIsAnArabicDate()
    {
        var ar = FamilyKit.Ar;
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge(ar);
        new WhyPresenter(ui.Root, bridge, ar);
        bridge.EndTurn();
        Assert.AreEqual("من 1 تشرين الثاني 1947 إلى 1 كانون الأول 1947", ui.Q<Label>("why-turn").text);
    }

    /// <summary>Playtest 1, item 5: every date the player sees goes through StoryOfTheLand.Sim.PlayerText ("15 May 1948") or,
    /// where the UI's language names the month, through the date.* strings (Control1948.DateText, #687, #701), so no UI
    /// string carries a date format of its own.</summary>
    [Test]
    public void NoUiStringFormatsADateItself()
    {
        var yaml = AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Strings).text;
        foreach (var line in yaml.Split('\n'))
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(line, "yyyy|MMM|MM-dd|:d "), "date format in strings.en.yaml: " + line);
    }
}
