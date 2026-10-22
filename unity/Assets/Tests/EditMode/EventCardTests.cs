#nullable enable
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>The family's decision card (EventCardPresenter): the pending event's title, text and choices, a choice queued
/// and applied at End Turn, the draft stamp, and the screenshot run that hides it.</summary>
public class EventCardTests
{
    static UiText Text => FamilyKit.En;
    /// <summary>The chapter's first card: on every new game of 1947-1949 it is the only one waiting.</summary>
    const string Partition = "e1947_11_partition";

    /// <summary>A new family game in the chapter's first month, its only card the Partition Resolution.</summary>
    static SimBridge AtPartition()
    {
        var bridge = FamilyKit.Bridge();
        CollectionAssert.AreEqual(new[] { Partition }, bridge.State.PendingEvents, "precondition: the chapter opens on the Partition Resolution");
        return bridge;
    }

    /// <summary>A family game with only this event pending, on its first day (as UiLayoutTests does).</summary>
    static SimBridge Showing(string eventId, Content? chapter = null)
    {
        var c = chapter ?? FamilyKit.Chapter();
        var g = GameSession.New(c, GoldenRun.Seed, GameRoot.AutomatedVillage);
        g.State.Date = c.Events.First(e => e.Id == eventId).From;
        g.State.PendingEvents.Clear(); g.State.PendingEvents.Add(eventId);
        return new SimBridge(g, Text);
    }

    [Test]
    public void TheCardShowsThePendingEventWithItsOptions()
    {
        using var ui = new UiTestKit();
        var card = new EventCardPresenter(ui.Root, AtPartition(), Text);
        Assert.AreEqual(Partition, card.Shown!.Id);
        Assert.AreEqual(DisplayStyle.Flex, ui.Q<VisualElement>("event-card").style.display.value);
        Assert.AreEqual("The Partition Resolution", ui.Q<Label>("event-title").text);
        StringAssert.Contains("adopted Resolution 181", ui.Q<Label>("event-text").text);
        Assert.AreEqual(DisplayStyle.None, ui.Q<Label>("event-note").style.display.value, "not a sensitive card");
        var options = ui.Q<VisualElement>("event-options").Query<Button>().ToList();
        CollectionAssert.AreEqual(new[] { "option-gather_news", "option-tend_land", "option-keep_deeds" }, options.Select(b => b.name).ToArray());
        Assert.AreEqual("Gather at the guest house to hear the news", options[0].text, "a family answers for free: no price");
    }

    [UnityTest]
    public System.Collections.IEnumerator TheTitleIsCentred()
    {
        using var ui = new UiTestKit();
        new EventCardPresenter(ui.Root, AtPartition(), Text);
        for (int i = 0; i < 10 && ui.Q<Label>("event-title").resolvedStyle.unityTextAlign == TextAnchor.UpperLeft; i++) yield return null;
        Assert.AreEqual(TextAnchor.MiddleCenter, ui.Q<Label>("event-title").resolvedStyle.unityTextAlign);
    }

    /// <summary>The owner's decision (2026-10-21): no card is stamped DRAFT, whatever its review status.</summary>
    [Test]
    public void NoCardIsStampedDraft()
    {
        Assert.AreEqual("draft", FamilyKit.Chapter().Events.First(e => e.Id == Partition).Status, "precondition: an unreviewed card");
        using var ui = new UiTestKit();
        new EventCardPresenter(ui.Root, Showing(Partition), Text);
        Assert.IsNull(ui.Root.Q("event-draft"));
        Assert.IsFalse(ui.Root.Query<Label>().ToList().Any(l => l.text == "DRAFT" || l.text == "مسودة"));
    }

    [Test]
    public void ADisposedCardHearsNothing()
    {
        using var ui = new UiTestKit();
        var bridge = AtPartition();
        var card = new EventCardPresenter(ui.Root, bridge, Text);
        Assert.IsNotNull(card.Shown);
        card.Dispose();
        ui.Q<Label>("event-title").text = "untouched";
        bridge.EndTurn();
        Assert.IsNotEmpty(bridge.State.PendingEvents, "precondition: the next month has a card the card would show");
        Assert.AreEqual("untouched", ui.Q<Label>("event-title").text);
        Assert.IsNull(ui.Root.Q<Button>("option-gather_news"), "option buttons are removed with the card");
        Assert.AreEqual(DisplayStyle.None, ui.Q<VisualElement>("event-card").style.display.value, "a disposed card is hidden");
    }

    /// <summary>A choice is an order: pressing it queues it, the card tells what followed until Continue, and End Turn
    /// records the family's choice.</summary>
    [Test]
    public void ChoosingQueuesTheAnswerAndTheTurnAppliesIt()
    {
        using var ui = new UiTestKit();
        var bridge = AtPartition();
        var card = new EventCardPresenter(ui.Root, bridge, Text);
        UiTestKit.Press(ui.Q<Button>("option-keep_deeds"));
        var queued = (ChooseEventOption)bridge.Queued.Single();
        Assert.AreEqual((Partition, "keep_deeds"), (queued.EventId, queued.OptionId));
        Assert.AreEqual(Partition, card.Shown?.Id, "the card stays with what followed");
        StringAssert.StartsWith("The father wrapped the land deeds", ui.Q<Label>("event-text").text);
        UiTestKit.Press(ui.Q<Button>("event-continue"));
        Assert.IsNull(card.Shown, "the card moves on: no other card waits this month");
        bridge.EndTurn();
        Assert.AreEqual("keep_deeds", bridge.State.ChosenOptions[Partition]);
    }

    // Fix round 1, I3: a one-option card renders one button, and pressing it answers the event.
    [Test]
    public void AOneOptionEventShowsOneButton()
    {
        using var ui = new UiTestKit();
        var bridge = Showing("e1948_01_ala");
        var card = new EventCardPresenter(ui.Root, bridge, Text);
        Assert.AreEqual("e1948_01_ala", card.Shown!.Id);
        var buttons = ui.Q<VisualElement>("event-options").Query<Button>().ToList();
        Assert.AreEqual(1, buttons.Count);
        Assert.AreEqual("option-continue", buttons[0].name);
        UiTestKit.Press(buttons[0]);
        var queued = (ChooseEventOption)bridge.Queued.Single();
        Assert.AreEqual(("e1948_01_ala", "continue"), (queued.EventId, queued.OptionId));
    }

    /// <summary>A screenshot run (?shot, -shot) captures the map, so the opening card (the Partition Resolution, waiting on
    /// every new game of the chapter) must not cover it. A smoke run presses the card's options, so it keeps the card even
    /// with shot; every other run, normal play first, shows it.</summary>
    [Test]
    public void AScreenshotRunHidesTheCardAndOnlyIt()
    {
        Assert.IsTrue(EventCardPresenter.HiddenInCaptures("https://x/?shot", new string[0], true));
        Assert.IsTrue(EventCardPresenter.HiddenInCaptures("https://x/?golden&shot", new string[0], true), "tools/art_checkpoint.sh's query");
        Assert.IsTrue(EventCardPresenter.HiddenInCaptures("https://x/?golden&shot&lang=ar", new string[0], true), "in Arabic too");
        Assert.IsTrue(EventCardPresenter.HiddenInCaptures(null, new[] { "player", "-shot" }, false));
        Assert.IsFalse(EventCardPresenter.HiddenInCaptures("https://x/", new string[0], true), "normal play shows the card");
        Assert.IsFalse(EventCardPresenter.HiddenInCaptures(null, new string[0], true), "no URL: normal play");
        Assert.IsFalse(EventCardPresenter.HiddenInCaptures(null, new[] { "player" }, false));
        Assert.IsFalse(EventCardPresenter.HiddenInCaptures("https://x/?golden", new string[0], true));
        Assert.IsFalse(EventCardPresenter.HiddenInCaptures("https://x/?vprofile", new string[0], true), "the frame profile prices the card");
        Assert.IsFalse(EventCardPresenter.HiddenInCaptures("https://x/?screenshot=1", new string[0], true), "a whole parameter only");
        Assert.IsFalse(EventCardPresenter.HiddenInCaptures("https://x/?smoke=5&shot", new string[0], true), "a smoke run presses the card");
        Assert.IsFalse(EventCardPresenter.HiddenInCaptures(null, new[] { "player", "-smoke", "5", "-shot" }, false));
    }

    [Test]
    public void AHiddenCardLeavesTheMapClearAndANormalOneCoversIt()
    {
        foreach (var hidden in new[] { true, false })
        {
            using var ui = new UiTestKit();
            var bridge = FamilyKit.Bridge();
            var card = new EventCardPresenter(ui.Root, bridge, Text, hiddenInCapture: hidden);
            var hud = ui.Root.Q("root") ?? ui.Root;
            Assert.AreEqual(Partition, card.Shown?.Id, "a new game opens on the Partition Resolution either way: the sim is untouched");
            Assert.AreEqual(hidden ? DisplayStyle.None : DisplayStyle.Flex, ui.Q<VisualElement>("event-card").style.display.value, $"hidden {hidden}");
            Assert.AreEqual(!hidden, hud.ClassListContains(EventCardPresenter.OpenClass), $"the scrim, hidden {hidden}");
            Assert.AreEqual(DisplayStyle.None, ui.Q<Button>("event-reopen").style.display.value, "no Decision waiting button");
            card.Render();                                                // a later render (any bridge change) keeps it so
            Assert.AreEqual(hidden ? DisplayStyle.None : DisplayStyle.Flex, ui.Q<VisualElement>("event-card").style.display.value, $"re-rendered, hidden {hidden}");
            Assert.AreEqual(!hidden, hud.ClassListContains(EventCardPresenter.OpenClass));
            card.Dispose();
        }
    }

    // --- Footage (owner, 2026-10-22: "Map + checked clips") ---------------------------------------------------------------

    const string ClipUrl = "https://www.aljazeera.com/video/partition";

    /// <summary>A footage.yaml with one clip of the Partition card, signed off or not (a hand-edited file: the converter
    /// writes signed-off clips only), and a signed-off clip of another event.</summary>
    static Footage FootageFile(bool signed) => Footage.Parse(
        "footage:\n" +
        "  - {\"event_id\": \"" + Partition + "\", \"url\": \"" + ClipUrl + "\", \"publisher\": \"Al Jazeera English\", \"title\": \"Partition\", " +
        "\"title_ar\": \"التقسيم\", \"date\": \"2017-11-29\", \"duration_s\": 120, \"description\": \"d\", \"description_ar\": \"و\", " +
        "\"checked_by\": \"" + (signed ? "jdoe" : "") + "\", \"checked_on\": \"" + (signed ? "2026-10-22" : "") + "\"}\n" +
        "  - {\"event_id\": \"e1948_04_deir_yassin\", \"url\": \"https://www.aljazeera.com/video/other\", \"publisher\": \"Al Jazeera English\", " +
        "\"title\": \"t\", \"title_ar\": \"ع\", \"date\": \"2018\", \"description\": \"d\", \"description_ar\": \"و\", \"checked_by\": \"jdoe\", \"checked_on\": \"2026-10-22\"}\n");

    [Test]
    public void AnUncheckedClipShowsNoFootageButton()
    {
        using var ui = new UiTestKit();
        var opened = new System.Collections.Generic.List<string>();
        new EventCardPresenter(ui.Root, Showing(Partition), Text, clipsFor: FootageFile(signed: false).For, openUrl: opened.Add);
        var row = ui.Q<VisualElement>("event-footage");
        Assert.AreEqual(0, row.childCount, "no Watch footage until the owner signs the clip off");
        Assert.AreEqual(DisplayStyle.None, row.style.display.value);
        Assert.IsEmpty(opened);
    }

    [Test]
    public void ACheckedClipShowsTheButtonAndItsContentNoteBeforeOpening()
    {
        using var ui = new UiTestKit();
        var opened = new System.Collections.Generic.List<string>();
        new EventCardPresenter(ui.Root, Showing(Partition), Text, clipsFor: FootageFile(signed: true).For, openUrl: opened.Add);
        var row = ui.Q<VisualElement>("event-footage");
        Assert.AreEqual(DisplayStyle.Flex, row.style.display.value);
        var watch = row.Q<Button>("event-watch-0");
        Assert.AreEqual("Watch footage", watch.text);
        Assert.AreEqual(1, row.Query<Button>().ToList().Count, "only this event's clip");

        UiTestKit.Press(watch);
        Assert.AreEqual("Content note: this clip shows killing and violence.", row.Q<Label>("event-footage-note").text);
        Assert.AreEqual("Open", row.Q<Button>("event-footage-open").text);
        Assert.AreEqual("Cancel", row.Q<Button>("event-footage-cancel").text);
        Assert.IsEmpty(opened, "nothing opens before Open");

        UiTestKit.Press(row.Q<Button>("event-footage-cancel"));
        Assert.IsNull(row.Q<Label>("event-footage-note"), "Cancel takes the note down");
        Assert.IsEmpty(opened, "Cancel opens nothing");

        UiTestKit.Press(row.Q<Button>("event-watch-0"));
        UiTestKit.Press(row.Q<Button>("event-footage-open"));
        CollectionAssert.AreEqual(new[] { ClipUrl }, opened);
        Assert.IsNotNull(row.Q<Button>("event-watch-0"), "the button is back for another look");
    }

    [Test]
    public void TheFootageStringsAreInArabic()
    {
        var ar = FamilyKit.Ar;
        Assert.AreEqual("شاهد المقطع", ar["event.watch"]);
        Assert.AreEqual("تنبيه: يحتوي المقطع على مشاهد قتل وعنف.", ar["event.footage_note"]);
        Assert.AreEqual(("فتح", "إلغاء"), (ar["event.footage_open"], ar["event.footage_cancel"]));
    }

    /// <summary>The shipped file (tools/footage_to_yaml.py) parses, and holds only signed-off clips of known events.</summary>
    [Test]
    public void TheShippedFootageIsSignedOffOnly()
    {
        var text = FamilyKit.Inputs.Read(Footage.FileName);
        Assert.IsNotNull(text, "content/palestine/footage.yaml exists");
        var footage = Footage.Parse(text);
        Assert.IsTrue(footage.All.All(c => c.CheckedBy.Length > 0 && c.CheckedOn.Length > 0 && c.Url.StartsWith("https://")));
    }
}
