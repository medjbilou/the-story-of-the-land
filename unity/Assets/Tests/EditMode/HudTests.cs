#nullable enable
using System.Collections;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>The family game's top bar (HudPresenter, HudModel), the bridge it reads (SimBridge), the toast and the screen
/// navigation. The bar shows the family's ten resources (0..1, as 0-100 with "%") and the money in use.</summary>
public class HudTests
{
    static UiText Text => FamilyKit.En;
    static UiText Ar => FamilyKit.Ar;
    static SimBridge NewBridge() => FamilyKit.Bridge();
    static Indicator[] Resources(SimBridge b) => b.Content.Indicators.ToArray();

    /// <summary>The first card of the opening month, answered with its first option: a valid order.</summary>
    static ChooseEventOption FirstChoice(SimBridge b)
    {
        var id = b.State.PendingEvents.First();
        return new ChooseEventOption(id, b.Content.Events.First(e => e.Id == id).Options[0].Id);
    }

    /// <summary>Owner, 2026-10-21: End Turn waits while a card has no answer. It stays pressable (it keeps the focus) but
    /// dim, says why in its tooltip and calls decisionWaiting; once every card is answered it ends the turn.</summary>
    [Test]
    public void EndTurnWaitsUntilEveryCardIsAnswered()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        int waited = 0;
        using var hud = new HudPresenter(ui.Root, bridge, Text, decisionWaiting: () => waited++);
        Assert.IsTrue(bridge.Undecided, "precondition: the opening month has a card");
        var endTurn = ui.Q<Button>("end-turn");
        Assert.IsTrue(endTurn.ClassListContains(HudPresenter.WaitingClass), "dim while a card waits");
        Assert.AreEqual(Text["hud.end_turn_wait"], endTurn.tooltip);
        ui.Press("end-turn");
        Assert.AreEqual(0, bridge.State.Turn, "the turn did not end");
        Assert.AreEqual(1, waited, "it said why");
        FamilyKit.AnswerAll(bridge);
        Assert.IsFalse(endTurn.ClassListContains(HudPresenter.WaitingClass), "lit once every card is answered");
        Assert.AreEqual("", endTurn.tooltip);
        ui.Press("end-turn");
        Assert.AreEqual(1, bridge.State.Turn, "the turn ended");
        Assert.AreEqual(1, waited);
    }

    [Test]
    public void TheTopBarShowsTheOpeningOfTheGame()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        using var hud = new HudPresenter(ui.Root, bridge, Text);
        var start = bridge.State.Date;
        Assert.AreEqual(start.Year.ToString(System.Globalization.CultureInfo.InvariantCulture), ui.Q<Label>("year").text);
        Assert.AreEqual("Turn 0", ui.Q<Label>("turn").text);
        Assert.AreEqual(PlayerText.Month(start), ui.Q<Label>("date").text);
        Assert.IsNull(ui.Root.Q("score"), "the family game has no score");
        foreach (var i in Resources(bridge))
        {
            Assert.AreEqual(PlayerText.Percent(bridge.State.Indicators[i.Id]), ui.Q<Label>("family-" + i.Id + "-value").text, i.Id);   // the 0-100 scale, with its unit
            Assert.AreEqual("", ui.Q<Label>("family-" + i.Id + "-delta").text, i.Id + ": no previous turn yet");
        }
    }

    /// <summary>#687: the plate's date is in the UI's language: "أيار 1950" in Arabic (playtest 3: the turn's month), every
    /// month with its Levantine name (PlayerText.LevantineMonths) and no Latin letter.</summary>
    [Test]
    public void TheDatePlateIsInArabicInTheArabicUi()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge(Ar);
        using var hud = new HudPresenter(ui.Root, bridge, Ar);
        var months = new[] { "كانون الثاني", "شباط", "آذار", "نيسان", "أيار", "حزيران", "تموز", "آب", "أيلول", "تشرين الأول", "تشرين الثاني", "كانون الأول" };
        for (int m = 1; m <= 12; m++)
        {
            bridge.State.Date = new System.DateTime(1950, m, 1);
            hud.Render();
            var date = ui.Q<Label>("date").text;
            Assert.AreEqual($"{months[m - 1]} 1950", date);
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(date, "[A-Za-z]"), date);
        }
    }

    /// <summary>#687, playtest 3: in English the plate names the turn's month, PlayerText.Month's "May 1948", on every day of a
    /// leap year (every month, every day number).</summary>
    [Test]
    public void TheDatePlateInEnglishNamesTheMonth()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        using var hud = new HudPresenter(ui.Root, bridge, Text);
        for (var d = new System.DateTime(1948, 1, 1); d.Year == 1948; d = d.AddDays(1))
        {
            bridge.State.Date = d;
            hud.Render();
            Assert.AreEqual(PlayerText.Month(d), ui.Q<Label>("date").text);
        }
    }

    /// <summary>F5 step 4: a family resource on the bar. Its value is the indicator on the 0-100 scale; after a turn it
    /// shows the change, and its tooltip says both, under the caption the bar shows (the resource's name in model.yaml).</summary>
    [Test]
    public void AResourceMeterShowsItsLevelAndItsChange()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        using var hud = new HudPresenter(ui.Root, bridge, Text);
        var land = bridge.Content.Indicators.First(i => i.Id == "land");
        var meter = ui.Q<VisualElement>("meter-family-land");
        Assert.AreEqual(land.Name, meter.Q<Label>(className: "meter-name").text);
        Assert.AreEqual("region", meter.Q<UiIcon>().Glyph);
        Assert.AreEqual(PlayerText.Percent(bridge.State.Indicators["land"]), ui.Q<Label>("family-land-value").text);
        Assert.AreEqual("", ui.Q<Label>("family-land-delta").text, "no change before the first turn");
        Assert.AreEqual($"{land.Name}: {PlayerText.Percent(bridge.State.Indicators["land"])}", meter.tooltip);
        FamilyKit.PlayMonth(bridge);
        var prev = bridge.Session.Previous!; var now = bridge.State;
        now.Indicators["land"] = 0.634; prev.Indicators["land"] = 0.584;         // 58% -> 63%: up 5 points
        hud.Render();
        Assert.AreEqual("63%", ui.Q<Label>("family-land-value").text);
        Assert.AreEqual("+5%", ui.Q<Label>("family-land-delta").text);
        Assert.IsTrue(ui.Q<Label>("family-land-delta").ClassListContains("delta-up"));
        Assert.AreEqual($"{land.Name}: 63%, up 5 points since last turn", meter.tooltip);
        now.Indicators["land"] = 0.574;                                         // 58% -> 57%: down 1 point
        hud.Render();
        Assert.AreEqual("-1%", ui.Q<Label>("family-land-delta").text);
        Assert.IsTrue(ui.Q<Label>("family-land-delta").ClassListContains("delta-down"));
        Assert.AreEqual($"{land.Name}: 57%, down 1 point since last turn", meter.tooltip);
        using var arUi = new UiTestKit();
        using var arHud = new HudPresenter(arUi.Root, FamilyKit.Bridge(Ar), Ar);
        Assert.AreEqual(land.NameAr, arUi.Q<VisualElement>("meter-family-land").Q<Label>(className: "meter-name").text);
        StringAssert.StartsWith(land.NameAr + ": ", arUi.Q<VisualElement>("meter-family-land").tooltip);
    }

    [Test]
    public void EndTurnAdvancesTheGameAndShowsChanges()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        using var hud = new HudPresenter(ui.Root, bridge, Text);
        var start = bridge.State.Date;
        FamilyKit.AnswerAll(bridge);
        ui.Press("end-turn");
        Assert.AreEqual(1, bridge.State.Turn);
        Assert.AreEqual("Turn 1", ui.Q<Label>("turn").text);
        Assert.Greater(bridge.State.Date, start);
        Assert.AreEqual(PlayerText.Month(bridge.State.Date), ui.Q<Label>("date").text);   // monthly turns
        PlayUntilAChange(bridge);
        foreach (var i in Resources(bridge))
        {
            var m = HudModel.Level(Text, bridge.State.Indicators[i.Id], bridge.Session.Previous!.Indicators[i.Id]);
            var delta = ui.Q<Label>("family-" + i.Id + "-delta");
            Assert.AreEqual(m.Delta, delta.text, i.Id);
            Assert.AreEqual(m.Sign < 0, delta.ClassListContains("delta-down"), i.Id);
            Assert.AreEqual(m.Sign > 0, delta.ClassListContains("delta-up"), i.Id);
        }
    }

    /// <summary>The sim's own refusals come back at End Turn and show in the toast: a choice for a card that is not waiting
    /// for one (a real event id, so the bridge lets it through).</summary>
    [Test]
    public void RefusedOrdersAreShownWithTheirReason()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        var text = Text;
        using var hud = new HudPresenter(ui.Root, bridge, text);
        using var toast = new ToastPresenter(ui.Root, bridge, text);
        Assert.AreEqual(DisplayStyle.None, ui.Q<Label>("toast").style.display.value);
        var later = bridge.Content.Events.First(e => !bridge.State.PendingEvents.Contains(e.Id));
        Assert.IsNull(bridge.Queue(new ChooseEventOption(later.Id, later.Options[0].Id)));
        FamilyKit.AnswerAll(bridge);
        ui.Press("end-turn");
        Assert.AreEqual("Not done: That event is not waiting for a decision", ui.Q<Label>("toast").text);
        Assert.AreEqual(DisplayStyle.Flex, ui.Q<Label>("toast").style.display.value);
    }

    [Test]
    public void NavigationOpensOneScreenAtATime()
    {
        using var ui = new UiTestKit();
        using var nav = new ScreenNav(ui.Root);
        Assert.IsNull(nav.Current);
        ui.Press("nav-why");
        Assert.AreEqual("why", nav.Current);
        Assert.AreEqual(DisplayStyle.Flex, ui.Q<VisualElement>("screen-why").style.display.value);
        Assert.AreEqual(DisplayStyle.None, ui.Q<VisualElement>("screen-settings").style.display.value);
        ui.Press("nav-settings");
        Assert.AreEqual(DisplayStyle.None, ui.Q<VisualElement>("screen-why").style.display.value);
        ui.Press("nav-settings");
        Assert.IsNull(nav.Current);
    }

    [UnityTest]
    public IEnumerator AtStartNoScreenIsShownAndTheMiddleOfTheScreenIsNotUi()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        using var nav = new ScreenNav(ui.Root);
        using var toast = new ToastPresenter(ui.Root, bridge, Text);
        foreach (var name in new[] { "screen-why", "screen-how-to-play", "screen-settings", "toast" })
            Assert.AreEqual(DisplayStyle.None, ui.Q<VisualElement>(name).style.display.value, name);
        for (int i = 0; i < 10 && float.IsNaN(ui.Root.worldBound.width); i++) yield return null;
        var panel = ui.Root.panel;
        var middle = ui.Root.worldBound.center;
        Assert.IsNull(panel.Pick(middle), "the middle of the screen must reach the map");
        // screen coordinates are y-up; UiPointer flips them
        var screenH = (float)Screen.height;
        var asScreen = new Vector2(middle.x, screenH - middle.y);
        Assert.IsFalse(UiPointer.IsOverUi(panel, asScreen, screenH));
    }

    /// <summary>Unknown ids and an order the family game does not have are refused by the bridge, never thrown, and the next
    /// turn runs.</summary>
    [Test]
    public void BadIdsAreRefusedNotThrownAndTheNextTurnRuns()
    {
        var bridge = NewBridge();
        var refusals = new System.Collections.Generic.List<string>();
        bridge.Refused += refusals.Add;
        Assert.IsNotNull(bridge.Queue(new OtherOrder()));
        Assert.IsNotNull(bridge.Queue(new ChooseEventOption("nope", "nope")));
        Assert.IsNotNull(bridge.Queue(new ChooseEventOption(null!, null!)));
        Assert.IsNotNull(bridge.Queue(new ChooseEventOption(bridge.Content.Events[0].Id, "nope")));
        Assert.IsNotNull(bridge.Queue(new ChooseEventOption(bridge.Content.Events[0].Id, null!)));
        Assert.AreEqual(0, bridge.Queued.Count);
        Assert.AreEqual(5, refusals.Count);
        Assert.AreEqual(Text["order.invalid"], refusals[0], "an order the family game does not have");
        Assert.DoesNotThrow(() => bridge.EndTurn());
        Assert.AreEqual(1, bridge.State.Turn);
    }

    [Test]
    public void ADisposedPresenterHearsNothing()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        var hud = new HudPresenter(ui.Root, bridge, Text);
        var toast = new ToastPresenter(ui.Root, bridge, Text);
        var nav = new ScreenNav(ui.Root);
        hud.Dispose(); toast.Dispose(); nav.Dispose();
        ui.Q<Label>("turn").text = "untouched";
        ui.Q<Label>("toast").text = "untouched";
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("unknown event 'nope'"));
        bridge.Queue(new ChooseEventOption("nope", "nope"));
        bridge.EndTurn();
        ui.Press("end-turn");
        ui.Press("nav-why");
        Assert.AreEqual("untouched", ui.Q<Label>("turn").text);
        Assert.AreEqual("untouched", ui.Q<Label>("toast").text);
        Assert.AreEqual(1, bridge.State.Turn, "End Turn button no longer wired (only the direct call ran)");
        Assert.IsNull(nav.Current);
        Assert.AreEqual(DisplayStyle.None, ui.Q<VisualElement>("screen-why").style.display.value);
    }

    [Test]
    public void ToastShowsQueueRefusalsAndClearsOnTheNextChange()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        using var toast = new ToastPresenter(ui.Root, bridge, Text);
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("unknown event 'nope'"));   // the id: log only (#622)
        Assert.IsNotNull(bridge.Queue(new ChooseEventOption("nope", "nope")));
        Assert.AreEqual("Not done: Unknown event", ui.Q<Label>("toast").text);
        Assert.AreEqual(DisplayStyle.Flex, ui.Q<Label>("toast").style.display.value);
        Assert.IsNull(bridge.Queue(FirstChoice(bridge)));
        Assert.AreEqual(DisplayStyle.None, ui.Q<Label>("toast").style.display.value);
    }

    /// <summary>Fix round 3 (m-1) and fix round 1 (I2): every change carries its sign and its unit ("%"), and a change that
    /// rounds to zero between two equal shown values is empty, never "-0%".</summary>
    [Test]
    public void ChangesShowSignsAndZeroDeltasAreEmpty()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        FamilyKit.PlayMonth(bridge);
        using var hud = new HudPresenter(ui.Root, bridge, Text);
        var prev = bridge.Session.Previous!;
        var now = bridge.State;
        foreach (var i in Resources(bridge)) { now.Indicators[i.Id] = 0.55; prev.Indicators[i.Id] = 0.50; }   // up 5 points
        hud.Render();
        foreach (var i in Resources(bridge))
        {
            var delta = ui.Q<Label>("family-" + i.Id + "-delta");
            Assert.AreEqual("+5%", delta.text, i.Id);
            Assert.IsTrue(delta.ClassListContains("delta-up"), i.Id);
            Assert.IsFalse(delta.ClassListContains("delta-down"), i.Id);
        }
        now.Indicators["land"] = 0.5004; prev.Indicators["land"] = 0.4996;     // down 0.08 points: both show 50%
        hud.Render();
        Assert.AreEqual("", ui.Q<Label>("family-land-delta").text);
        Assert.IsFalse(ui.Q<Label>("family-land-delta").ClassListContains("delta-up"));
        now.Indicators["land"] = 0.4996; prev.Indicators["land"] = 0.5004;
        hud.Render();
        Assert.AreEqual("", ui.Q<Label>("family-land-delta").text);
        Assert.IsFalse(ui.Q<Label>("family-land-delta").ClassListContains("delta-down"));
    }

    [Test]
    public void TheHudIsTheSameUnderAGermanCulture()
    {
        var bridge = NewBridge();
        PlayUntilAChange(bridge);
        var inv = BarTexts(bridge);
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.AreEqual(inv, BarTexts(bridge));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
    }

    /// <summary>Every label the bar shows and every meter's tooltip, rendered now.</summary>
    static string BarTexts(SimBridge bridge)
    {
        using var ui = new UiTestKit();
        using var hud = new HudPresenter(ui.Root, bridge, Text);
        var bar = ui.Q<VisualElement>("top-bar");
        return string.Join("\n", bar.Query<Label>().ToList().Select(l => l.name + "=" + l.text)
            .Concat(bar.Query(className: "meter").ToList().Select(m => m.name + "~" + m.tooltip)));
    }

    [Test]
    public void ReplaceDropsOrdersAndBeforeLastSpansTwoTurns()
    {
        var bridge = NewBridge();
        Assert.IsNull(bridge.BeforeLast);
        bridge.EndTurn();
        Assert.IsNull(bridge.BeforeLast);                        // only one step has run
        var first = bridge.Session.Last;
        bridge.EndTurn();
        Assert.AreSame(first, bridge.BeforeLast);
        Assert.AreNotSame(first, bridge.Session.Last);
        Assert.IsNull(bridge.Queue(FirstChoice(bridge)));
        int changed = 0; bridge.Changed += () => changed++;
        bridge.Replace(FamilyKit.Session());
        Assert.AreEqual(1, changed);
        Assert.AreEqual(0, bridge.Queued.Count);
        Assert.IsNull(bridge.BeforeLast);
        Assert.AreEqual(0, bridge.State.Turn);
    }

    /// <summary>#622: a refusal names the kind of thing and never shows the content id or the exception's message, in
    /// either language; both stay in the log for a bug report.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void ARefusalShowsNoIdOrExceptionTextInEitherLanguage(bool arabic)
    {
        var text = arabic ? Ar : Text;
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("unknown event 'curfew'"));
        var unknown = FamilyKit.Bridge(text).Queue(new ChooseEventOption("curfew", "x"));
        Assert.AreEqual(text.Format("order.unknown", text["order.kind.event"]), unknown);   // the kind in the UI's language (#595)
        StringAssert.DoesNotContain("curfew", unknown);

        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("stack detail 42"));
        var failing = new SimBridge(FamilyKit.Session(), text, (s, cmds) => throw new System.InvalidOperationException("stack detail 42"));
        Assert.IsNull(failing.Queue(FirstChoice(failing)));
        var reason = failing.EndTurn().Rejected.Single().Reason;
        Assert.AreEqual(text["order.failed"], reason);
        StringAssert.DoesNotContain("stack detail", reason);
    }

    /// <summary>Any exception from the sim (not only ArgumentException; final review M7) keeps the state, clears the queue
    /// and shows the reason: End Turn can never soft-lock. An unexpected kind is also logged in full.</summary>
    [TestCase(typeof(System.ArgumentException))]
    [TestCase(typeof(System.InvalidOperationException))]
    [TestCase(typeof(System.NullReferenceException))]
    public void ABatchTheSimRefusesOutrightKeepsTheStateAndShowsTheReason(System.Type kind)
    {
        using var ui = new UiTestKit();
        // Every kind leaves a console trace (rereview): a refusal as a warning, anything else as the full exception.
        if (kind == typeof(System.ArgumentException)) LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("End Turn refused.*boom"));
        else LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
        var bridge = new SimBridge(FamilyKit.Session(), Text,
            (s, cmds) => throw (System.Exception)System.Activator.CreateInstance(kind, "boom")!);
        using var toast = new ToastPresenter(ui.Root, bridge, Text);
        var order = FirstChoice(bridge);
        Assert.IsNull(bridge.Queue(order));
        System.Collections.Generic.IReadOnlyList<Rejection>? seen = null;
        bridge.TurnEnded += r => seen = r;
        var hash = bridge.State.Hash();
        Assert.DoesNotThrow(() => bridge.EndTurn());
        Assert.AreEqual(hash, bridge.State.Hash());
        Assert.AreEqual(0, bridge.Queued.Count);
        Assert.IsNull(bridge.BeforeLast);
        Assert.AreSame(order, seen![0].Command);
        Assert.AreEqual("Not done: The turn could not run. Your orders were cleared. Give them again.", ui.Q<Label>("toast").text);   // "boom": the log only (#622)
    }

    /// <summary>#912: the sim and autosave have advanced before End Turn notifies presenters. A broken presenter is
    /// logged, but cannot leave every subscriber registered after it showing the previous turn.</summary>
    [Test]
    public void AThrowingEndTurnSubscriberDoesNotStopTheOthers()
    {
        var bridge = NewBridge();
        var changed = 0;
        var ended = 0;
        bridge.Changed += () => throw new System.InvalidOperationException("changed presenter failed");
        bridge.Changed += () => changed++;
        bridge.TurnEnded += _ => throw new System.InvalidOperationException("turn-ended presenter failed");
        bridge.TurnEnded += _ => ended++;
        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("changed presenter failed"));
        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("turn-ended presenter failed"));

        Assert.DoesNotThrow(() => bridge.EndTurn());

        Assert.AreEqual(1, bridge.State.Turn, "the simulation stepped exactly once");
        Assert.AreEqual(1, changed, "later Changed subscribers still refresh");
        Assert.AreEqual(1, ended, "later TurnEnded subscribers still run");
    }

    /// <summary>Playtest 1, item 11: pressing the open screen's button closes it, every time, with every presenter
    /// GameRoot creates for the bar, the card and the screens listening, with the button focused (as after a mouse click),
    /// and across End Turn.</summary>
    [Test]
    public void PressingTheOpenScreensButtonClosesItAcrossTurns()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        var owned = new System.Collections.Generic.List<System.IDisposable>();   // fix round 1: nothing outlives the test
        T Own<T>(T d) where T : System.IDisposable { owned.Add(d); return d; }
        try
        {
            Own(new HudPresenter(ui.Root, bridge, Text));
            var nav = Own(new ScreenNav(ui.Root));
            Own(new EventCardPresenter(ui.Root, bridge, Text));
            Own(new WhyPresenter(ui.Root, bridge, Text));
            Own(new HowToPlayPresenter(ui.Root, Text));
            Own(new SettingsPresenter(ui.Root, Text, null));
            DisplayStyle Shown(string screen) => ui.Q<VisualElement>("screen-" + screen).style.display.value;
            for (int turn = 0; turn < 4; turn++)
            {
                foreach (var screen in ScreenNav.Screens)
                {
                    ui.Press("nav-" + screen);
                    Assert.AreEqual(DisplayStyle.Flex, Shown(screen), $"turn {turn}: {screen} did not open");
                    ui.Q<Button>("nav-" + screen).Focus();
                    ui.Press("nav-" + screen);
                    Assert.AreEqual(DisplayStyle.None, Shown(screen), $"turn {turn}: {screen} did not close");
                    Assert.IsNull(nav.Current);
                }
                var open = ScreenNav.Screens[turn % ScreenNav.Screens.Length];
                ui.Press("nav-" + open);
                FamilyKit.AnswerAll(bridge);
                ui.Press("end-turn");
                Assert.AreEqual(turn + 1, bridge.State.Turn, $"turn {turn}: End Turn did not run");
                Assert.AreEqual(DisplayStyle.Flex, Shown(open), $"turn {turn}: End Turn closed {open}");
                ui.Press("nav-" + open);
                Assert.AreEqual(DisplayStyle.None, Shown(open), $"turn {turn}: {open} did not close after End Turn");
            }
        }
        finally { DisposeAll(owned); }
        // Every presenter let go of the bridge and the buttons: a later turn writes nothing and End Turn does nothing.
        ui.Q<Label>("turn").text = "untouched";
        ui.Q<Label>("why-turn").text = "untouched";
        bridge.EndTurn();
        var turnAfter = bridge.State.Turn;
        ui.Press("end-turn");
        Assert.AreEqual("untouched", ui.Q<Label>("turn").text, "HudPresenter still listens");
        Assert.AreEqual("untouched", ui.Q<Label>("why-turn").text, "WhyPresenter still listens");
        Assert.AreEqual(turnAfter, bridge.State.Turn, "End Turn is still wired");
    }

    /// <summary>Disposes test-owned presenters newest first, as GameRoot does.</summary>
    static void DisposeAll(System.Collections.Generic.List<System.IDisposable> owned)
    {
        for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose();
        owned.Clear();
    }

    /// <summary>Fix round 2 (N4, N5): no script rescales a sim number for display itself: 0..1 values go through
    /// PlayerText.Scale / Percent / PercentChange. Scans all of Assets/Scripts for "* 100", "100f", "/ 0.01" and "100.0".
    /// Allowed, each for a reason that is not a displayed number:</summary>
    static readonly (string file, string contains, string why)[] AllowedScale =
    {
        ("MinimapPresenter.cs", "Length.Percent(", "the view frame's position and size in percent of the picture (layout, not text)"),
        ("MapShaderGlobals.cs", "RiftHazeTopM = -100f", "a real elevation in metres for the rift haze (shader input, not a displayed number)"),
        ("SettlementKits.cs", "DefaultHouses = 100f", "the owner's symbolic scale for houses and trees (100x real size), not a displayed number"),
        ("RouteLayer.cs", "CorridorMarginM = 100f", "a clearance in map metres that kit buildings and trees keep from a route (geometry, not a displayed number)"),
        ("CameraController.cs", "PixelsPerNotch = 100f", "wheel pixels in one mouse notch, to scale the browser's wheel events (input, not a displayed number)"),
        ("WildlifeLayer.cs", "MinimumCliffElevationM = 100f", "a real elevation in metres an ibex hex must reach (placement, not a displayed number)"),
        ("SettingsPresenter.cs", "CultureInfo.InvariantCulture) / 100f", "a volume chosen in percent to Unity's 0-1 volume (input, not a displayed number)"),
        ("GraphicsQuality.cs", "ReducedMotionWaterTime = 100f", "the water shader's time held still under reduced motion (seconds, not a displayed number)"),
        ("SettingsPresenter.cs", "* 100f / percent", "the text size in percent to the panel's reference resolution (layout, not a displayed number)"),
    };

    [Test]
    public void NoScriptRescalesADisplayedNumberItself()
    {
        var pattern = new System.Text.RegularExpressions.Regex(@"\*\s*100\b|\b100f\b|/\s*0\.01\b|\b100\.0\b|\*\s*100f");
        foreach (var f in System.IO.Directory.GetFiles("Assets/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            foreach (var line in System.IO.File.ReadAllLines(f))
                if (pattern.IsMatch(line) && !System.Array.Exists(AllowedScale, a => System.IO.Path.GetFileName(f) == a.file && line.Contains(a.contains)))
                    Assert.Fail($"{System.IO.Path.GetFileName(f)}: {line.Trim()}");
        foreach (var a in AllowedScale) Assert.IsNotEmpty(a.why);
    }

    /// <summary>Fix round 2 (N4): percentages have one formatter, PlayerText; no UI string formats a number as "%" itself.</summary>
    [Test]
    public void NoUiStringFormatsAPercentItself()
    {
        var yaml = AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Strings).text;
        foreach (var line in yaml.Split('\n'))
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(line, @"\{\d+:[^}]*\}%"), "percent format in strings.en.yaml: " + line);
    }

    /// <summary>Fix round 1, M3: the top-bar captions are the content names: each resource's name in model.yaml, in the
    /// order model.yaml gives them, with the money beside Livelihood.</summary>
    [Test]
    public void TopBarCaptionsAreTheContentNames()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        using var hud = new HudPresenter(ui.Root, bridge, Text);
        var shown = ui.Q<VisualElement>("family-meters").Children().Select(m => m.name).ToList();
        var expected = Resources(bridge).SelectMany(i => i.Id == "livelihood" ? new[] { "meter-family-livelihood" } : new[] { "meter-family-" + i.Id }).ToList();
        CollectionAssert.AreEqual(expected, shown);
        foreach (var i in Resources(bridge))
            Assert.AreEqual(i.Id == HudPresenter.MoneyMeter   // Livelihood carries the money's sign (the owner's choice (a))
                    ? Text.Format("hud.livelihood_money", PlayerText.Target(bridge.Content, i.Id), HudPresenter.CurrencySign(bridge.Content, bridge.State, false))
                    : PlayerText.Target(bridge.Content, i.Id), ui.Q<VisualElement>("meter-family-" + i.Id).Q<Label>(className: "meter-name").text, i.Id);
    }

    /// <summary>Fix round 2 (R2-3, playtest 1 item 11): two Submit events on the Settings button within one frame (a
    /// double click, or a click plus Enter) each toggle the screen, as ScreenNav documents: no debounce. From closed the
    /// result is closed, from open it is open; the same input always gives the same state.</summary>
    [Test]
    public void TwoPressesInOneFrameToggleTwice()
    {
        using var ui = new UiTestKit();
        var nav = new ScreenNav(ui.Root);
        try
        {
            var settings = ui.Q<Button>("nav-settings");
            var frame = Time.frameCount;
            UiTestKit.Press(settings); UiTestKit.Press(settings);
            Assert.AreEqual(frame, Time.frameCount, "both presses in the same frame");
            Assert.IsNull(nav.Current, "closed -> open -> closed");
            Assert.AreEqual(DisplayStyle.None, ui.Q<VisualElement>("screen-settings").style.display.value);
            UiTestKit.Press(settings);                                   // open it
            UiTestKit.Press(settings); UiTestKit.Press(settings);
            Assert.AreEqual("settings", nav.Current, "open -> closed -> open");
            Assert.AreEqual(DisplayStyle.Flex, ui.Q<VisualElement>("screen-settings").style.display.value);
        }
        finally { nav.Dispose(); }
    }

    /// <summary>Fix round 3 (m-1): the top bar's change agrees with the two values it shows.</summary>
    [Test]
    public void TheShownChangeAgreesWithTheShownValues()
    {
        var m = HudModel.Level(Text, 0.634, 0.626);     // "63%" -> "63%"
        Assert.AreEqual("63%", m.Value);
        Assert.AreEqual("", m.Delta, "no change shown between two equal shown values");
        Assert.AreEqual(0, m.Sign);
        m = HudModel.Level(Text, 0.626, 0.624);         // "62%" -> "63%"
        Assert.AreEqual("63%", m.Value);
        Assert.AreEqual("+1%", m.Delta);
        Assert.AreEqual(1, m.Sign);
        m = HudModel.Level(Text, 0.626, null);          // before the first turn
        Assert.AreEqual("63%", m.Value);
        Assert.AreEqual("", m.Delta);
    }

    /// <summary>Plays months (each card's first choice) until some resource shows a change, so a test built on it never
    /// passes by checking nothing. Fails if none comes within the chapter.</summary>
    static void PlayUntilAChange(SimBridge bridge)
    {
        bool Changed() => bridge.Session.Previous is GameState p
            && bridge.Content.Indicators.Any(i => HudModel.Level(Text, bridge.State.Indicators[i.Id], p.Indicators[i.Id]).Sign != 0);
        for (int i = 0; i < 26 && !Changed() && bridge.State.GameOver == null; i++) FamilyKit.PlayMonth(bridge);
        Assert.IsTrue(Changed(), "no resource changed within the chapter");
    }

    /// <summary>#398: each meter's tooltip says its value and the change since last turn in words: the resources in points;
    /// with no change (turn 0, or one that rounds to zero) only the caption and the value. The money's tooltip is its name,
    /// sign and smaller unit (HudPresenter.CurrencyTip).</summary>
    [Test]
    public void EachMeterSaysItsValueAndTheChangeSinceLastTurn()
    {
        using var ui = new UiTestKit();
        var bridge = NewBridge();
        using var hud = new HudPresenter(ui.Root, bridge, Text);
        foreach (var i in Resources(bridge))                                                      // turn 0: no change
        {
            var said = $"{i.Name}: {PlayerText.Percent(bridge.State.Indicators[i.Id])}";
            // The owner's choice (a), 2026-10-21: Livelihood's tooltip goes on with the money in use (HudPresenter.CurrencyTip).
            if (i.Id == HudPresenter.MoneyMeter) said += " " + HudPresenter.CurrencyTip(bridge.Content, bridge.State, Text);
            Assert.AreEqual(said, ui.Q<VisualElement>("meter-family-" + i.Id).tooltip, i.Id);
        }
        StringAssert.Contains("Money in use: ", ui.Q<VisualElement>("meter-family-livelihood").tooltip);
        Assert.IsNull(ui.Root.Q("meter-family-currency"), "no bare currency cell");
        FamilyKit.PlayMonth(bridge);
        var prev = bridge.Session.Previous!; var now = bridge.State;
        now.Indicators["land"] = 0.40; prev.Indicators["land"] = 0.35;          // up 5 points
        now.Indicators["home"] = 0.40; prev.Indicators["home"] = 0.41;          // down 1 point
        hud.Render();
        string Name(string id) => PlayerText.Target(bridge.Content, id);
        Assert.AreEqual($"{Name("land")}: 40%, up 5 points since last turn", ui.Q<VisualElement>("meter-family-land").tooltip);
        Assert.AreEqual($"{Name("home")}: 40%, down 1 point since last turn", ui.Q<VisualElement>("meter-family-home").tooltip);
        // Every tooltip agrees with the meter's shown change: "up" with a gain, "down" with a loss, neither without one.
        foreach (var i in Resources(bridge))
        {
            var tip = ui.Q<VisualElement>("meter-family-" + i.Id).tooltip; var delta = ui.Q<Label>("family-" + i.Id + "-delta");
            StringAssert.StartsWith(i.Name + ": ", tip, i.Id);
            Assert.AreEqual(delta.ClassListContains("delta-up"), tip.Contains(", up "), i.Id + ": " + tip);
            Assert.AreEqual(delta.ClassListContains("delta-down"), tip.Contains(", down "), i.Id + ": " + tip);
        }
    }

    /// <summary>#398 review S3, #193: in each language the bar's visible caption is the caption its tooltip says
    /// (HudPresenter sets both from FamilyGame.Resource), so an Arabic bar never shows "Land" over a tooltip in Arabic.</summary>
    [Test]
    public void TheSpokenCaptionsAreTheBarsCaptions()
    {
        foreach (var (language, text) in new[] { ("ar", Ar), ("en", Text) })
        {
            using var ui = new UiTestKit();
            var bridge = FamilyKit.Bridge(text);
            using var hud = new HudPresenter(ui.Root, bridge, text);
            foreach (var i in Resources(bridge))
            {
                var meter = ui.Q<VisualElement>("meter-family-" + i.Id);
                var shown = meter.Q<Label>(className: "meter-name").text;
                var name = language == "ar" ? i.NameAr : i.Name;
                if (i.Id == HudPresenter.MoneyMeter) StringAssert.StartsWith(name, shown, $"{language}: Livelihood, with the money's sign");
                else Assert.AreEqual(name, shown, $"{language}: {i.Id}");
                StringAssert.StartsWith(name + ":", meter.tooltip, $"{language}: {i.Id}'s tooltip says the caption it shows");
            }
        }
    }

    /// <summary>#398: an Arabic UI says every meter in Arabic through the real path (the presenter with the UI's own
    /// strings), the change with the Arabic plural rule (hud.say_points), and no English word.</summary>
    [Test]
    public void TheSpokenTextCoversArabic()
    {
        var ar = Ar;
        var bridge = FamilyKit.Bridge(ar);
        PlayUntilAChange(bridge);
        using var ui = new UiTestKit();
        using var hud = new HudPresenter(ui.Root, bridge, ar);
        var prev = bridge.Session.Previous!; var now = bridge.State;
        foreach (var i in Resources(bridge))
        {
            var tip = ui.Q<VisualElement>("meter-family-" + i.Id).tooltip;
            StringAssert.StartsWith(i.NameAr + ": ", tip, i.Id);
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(tip, "[A-Za-z]{3,}"), $"{i.Id}: English words in the Arabic text: {tip}");
            var points = (long)System.Math.Abs(PlayerText.ShownPoints(now.Indicators[i.Id]) - PlayerText.ShownPoints(prev.Indicators[i.Id]));
            if (points > 0) StringAssert.Contains(ar.Plural("hud.say_points", points, (double)points), tip, i.Id);
        }
        now.Indicators["land"] = 0.40; prev.Indicators["land"] = 0.43;          // down 3 points
        hud.Render();
        Assert.AreEqual(ar.Format("hud.say_down", bridge.Content.Indicators.First(i => i.Id == "land").NameAr, "40%", ar.Plural("hud.say_points", 3, 3.0)),
            ui.Q<VisualElement>("meter-family-land").tooltip);
        StringAssert.Contains("بانخفاض", ui.Q<VisualElement>("meter-family-land").tooltip);
    }

    /// <summary>#398 review S1: a resumed game shows the changes the player saw before the reload, on every meter.
    /// GameSession.Load replays the save's own commands and checks the hash, so the replay's last steps are the player's.
    /// Both ways a save arrives: at start (a new bridge) and through Replace.</summary>
    [Test]
    public void AResumedGameShowsTheChangesOfTheSessionThatSavedIt()
    {
        var played = NewBridge();
        PlayUntilAChange(played);
        using var saved = new UiTestKit();
        using var savedHud = new HudPresenter(saved.Root, played, Text);
        var saveText = played.Session.Save.Write();
        var ids = Resources(played).Select(i => i.Id).ToList();
        Assert.IsTrue(ids.Any(id => saved.Q<Label>("family-" + id + "-delta").text != ""), "precondition: a change to carry over");
        foreach (var how in new[] { "start", "replace" })
        {
            using var ui = new UiTestKit();
            var loaded = GameSession.Load(FamilyKit.Chapter(), saveText, out var warnings);
            Assert.IsEmpty(warnings, how);
            SimBridge bridge;
            if (how == "start") bridge = new SimBridge(loaded, Text);
            else { bridge = NewBridge(); FamilyKit.PlayMonth(bridge); FamilyKit.PlayMonth(bridge); bridge.Replace(loaded); }
            using var hud = new HudPresenter(ui.Root, bridge, Text);
            Assert.AreEqual(played.State.Turn, bridge.State.Turn, how);
            foreach (var id in ids)
            {
                var before = saved.Q<Label>("family-" + id + "-delta"); var after = ui.Q<Label>("family-" + id + "-delta");
                Assert.AreEqual(before.text, after.text, $"{how}: {id}");
                Assert.AreEqual(before.ClassListContains("delta-up"), after.ClassListContains("delta-up"), $"{how}: {id}");
                Assert.AreEqual(before.ClassListContains("delta-down"), after.ClassListContains("delta-down"), $"{how}: {id}");
                Assert.AreEqual(saved.Q<VisualElement>("meter-family-" + id).tooltip, ui.Q<VisualElement>("meter-family-" + id).tooltip, $"{how}: {id}");
            }
        }
    }
}
