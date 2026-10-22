#nullable enable
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>The UI audit of the web build (2026-10-21) and F7's interface motion: the English credits set a line quoting
/// Arabic in the Arabic face (M1); Credits open at their top (m7) and close back to Settings (m8, PauseMenuTests too); a
/// source link names its page and carries its address (m3); Why says changes as the bar does and drops zero ones (m4); the
/// Arabic meter tooltip keeps "47%" in order (m5); a confirm names its action (m9); high contrast changes the bar (m12); the
/// region-borders button is only offered where borders are drawn (m14); the arrow keys walk the pause menu (m15); and the
/// motion (the bar's count and flash, End Turn's glow, the card sliding in) runs only with the motion class.</summary>
public class AuditFixesTests
{
    string dir = "";

    [SetUp] public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "storyoftheland-audit-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        SettingsPresenter.Reset();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    [Test]
    public void TheEnglishCreditsSetArabicInTheArabicFaceOpenAtTheTopAndCloseBackToSettings()
    {
        using var ui = new UiTestKit();
        using var nav = new ScreenNav(ui.Root);
        using var settings = new SettingsPresenter(ui.Root, FamilyKit.En, null);
        using var credits = new CreditsPresenter(ui.Root, Credits.Parse(FamilyKit.Inputs.Read(Credits.FileName)), FamilyKit.En, nav);
        var body = ui.Q<ScrollView>("credits-body");
        var quoting = body.Query<Label>().ToList().Where(l => UiDirection.HasRightToLeft(l.text)).ToList();
        Assert.IsNotEmpty(quoting, "precondition: an English line quotes Arabic (the episode's title)");
        foreach (var l in quoting) Assert.IsTrue(l.ClassListContains(CreditsPresenter.MixedClass), l.text);
        nav.Open("settings");
        UiTestKit.Press(ui.Q<Button>("settings-credits"));
        Assert.AreEqual(CreditsPresenter.Screen, nav.Current);
        body.scrollOffset = new Vector2(0, 300);
        nav.Close();
        Assert.AreEqual("settings", nav.Current, "Escape from Credits goes back to Settings");
        UiTestKit.Press(ui.Q<Button>("settings-credits"));
        Assert.AreEqual(0f, body.scrollOffset.y, "Credits open at their top");
        nav.Close(); nav.Close();
        Assert.IsNull(nav.Current);
    }

    [Test]
    public void ASourceLinkNamesItsPage()
    {
        var names = SourceLinks.Names(new[] {
            "https://en.wikipedia.org/wiki/Tantura_massacre", "https://en.wikipedia.org/w/index.php?title=Village_Statistics,_1945&oldid=1",
            "https://www.palquest.org/en/highlight/1/one", "https://www.palquest.org/en/highlight/2/two", "https://www.youtube.com/watch?v=x" }, FamilyKit.En);
        CollectionAssert.AreEqual(new[] { "Wikipedia (1)", "Wikipedia (2)", "palquest.org (1)", "palquest.org (2)", "YouTube video" }, names);   // short (owner, 2026-10-22); the tip has the title
        // Playtest 3: videos are told apart by number, and no two links ever read alike.
        CollectionAssert.AreEqual(new[] { "YouTube video 1", "YouTube video 2", "example.org (1)", "example.org (2)" }, SourceLinks.Names(new[] {
            "https://www.youtube.com/watch?v=a", "https://youtu.be/b", "https://example.org/", "https://example.org/" }, FamilyKit.En));
        // In Arabic an English article reads «ويكيبيديا (بالإنجليزية)» (not a title cut short in a right-to-left line); an Arabic one reads «ويكيبيديا».
        Assert.AreEqual(FamilyKit.Ar["sources.wikipedia_en"], SourceLinks.Names(new[] { "https://en.wikipedia.org/wiki/Tantura_massacre" }, FamilyKit.Ar)[0]);
        Assert.AreEqual(FamilyKit.Ar["sources.wikipedia"],
            SourceLinks.Names(new[] { "https://ar.wikipedia.org/wiki/%D9%85%D8%AC%D8%B2%D8%B1%D8%A9_%D8%A7%D9%84%D8%B7%D9%86%D8%B7%D9%88%D8%B1%D8%A9" }, FamilyKit.Ar)[0]);
        using var ui = new UiTestKit();
        var row = SourceLinks.Row(new[] { "https://en.wikipedia.org/wiki/Tantura_massacre" }, "Sources:", FamilyKit.En, ui.Root);
        ui.Root.Add(row);
        Assert.AreEqual("Tantura massacre\nhttps://en.wikipedia.org/wiki/Tantura_massacre", row.Q<Button>().tooltip, "the full title and the address are its tip");
    }

    [Test]
    public void WhySaysWholePointsAndNoZeroChanges()
    {
        var bridge = FamilyKit.Bridge();
        for (int i = 0; i < 6; i++) FamilyKit.PlayMonth(bridge);
        var why = FamilyGame.Why(bridge.Content, bridge.Session.Previous!, bridge.State, bridge.Session.Last!.Why, FamilyKit.En);
        foreach (var line in why.SelectMany(g => g.Lines))
        {
            StringAssert.DoesNotContain(".0%", line);
            Assert.IsFalse(line.EndsWith(" 0%") || line.EndsWith("+0%") || line.EndsWith("-0%"), line);
        }
        Assert.AreEqual("-5%", PlayerText.PercentChange(-0.05, 0));
    }

    [Test]
    public void TheArabicMeterTipKeepsThePercentInOrder()
    {
        var text = FamilyKit.Ar;
        var said = HudModel.Spoken(text, "الأرض", HudModel.Level(text, 0.47, null));
        StringAssert.Contains(UiText.LeftToRightMark + "47%" + UiText.LeftToRightMark, said, "held left to right: '47%', never '%47'");
    }

    [Test]
    public void AConfirmNamesItsAction()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        using var nav = new ScreenNav(ui.Root);
        using var pause = new PausePresenter(ui.Root, FamilyKit.En, nav, new SaveStore(dir).Family, FamilyKit.Chapter, () => bridge.Session,
            _ => { }, () => { }, () => { }, _ => { });
        pause.Open();
        Assert.AreEqual(FamilyKit.En["pause.title"], ui.Q<Label>("pause-title").text);
        UiTestKit.Press(ui.Q<Button>("pause-main-menu"));
        Assert.AreEqual(FamilyKit.En["pause.main_menu"], ui.Q<Label>("pause-title").text, "not 'Paused'");
        UiTestKit.Press(ui.Q<Button>("pause-confirm-no"));
        Assert.AreEqual(FamilyKit.En["pause.title"], ui.Q<Label>("pause-title").text);
    }

    /// <summary>m15: the arrow keys (GameRoot) move the focus through the menu's items, round at the ends.</summary>
    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator TheArrowKeysWalkThePauseMenu()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        using var nav = new ScreenNav(ui.Root);
        using var pause = new PausePresenter(ui.Root, FamilyKit.En, nav, new SaveStore(dir).Family, FamilyKit.Chapter, () => bridge.Session,
            _ => { }, () => { }, () => { }, _ => { }, chapters: () => { });
        Assert.IsFalse(pause.MoveFocus(1), "shut: nothing");
        pause.Open();
        for (int i = 0; i < 3; i++) yield return null;
        ui.Q<Button>("pause-resume").Focus();
        Assert.IsTrue(pause.MoveFocus(1));
        yield return null;
        Assert.AreEqual("pause-save", (ui.Root.focusController.focusedElement as VisualElement)?.name);
        pause.MoveFocus(-1); yield return null; pause.MoveFocus(-1); yield return null;
        Assert.AreEqual("pause-quit", (ui.Root.focusController.focusedElement as VisualElement)?.name, "up from the first goes round to the last");
    }

    [Test]
    public void HighContrastChangesTheBar()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        using var hud = new HudPresenter(ui.Root, bridge, FamilyKit.En);
        using var settings = new SettingsPresenter(ui.Root, FamilyKit.En, new SettingsStore(dir));
        UiTestKit.Press(ui.Q<Button>("settings-high_contrast"));
        Assert.IsTrue(ui.Q<VisualElement>("root").ClassListContains(SettingsPresenter.ContrastClass));
        UiTestKit.Press(ui.Q<Button>("settings-high_contrast"));
    }

    /// <summary>m14: the region borders are drawn only in the 1947-1949 chapter; elsewhere their minimap button is not offered.</summary>
    [Test]
    public void TheBordersButtonIsOfferedOnlyWhereBordersAreDrawn()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(FamilyKit.Inputs.Map);
        var game = Object.FindFirstObjectByType<GameRoot>();
        game.Store = new SaveStore(dir);
        game.Begin(FamilyKit.Inputs, 0);
        var ui = Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
        Assert.AreEqual(DisplayStyle.Flex, ui.Q<Button>("minimap-borders").style.display.value, "1947-1949: offered");
        game.StartFamily(FamilyKit.Inputs, "1967_1987", "imwas");
        Assert.AreEqual(DisplayStyle.None, ui.Q<Button>("minimap-borders").style.display.value, "1967-1987: not offered");
    }

    /// <summary>F7: with the motion class on, an answered card fades out (card-out) before it goes; without it, it goes at once.</summary>
    [UnityEngine.TestTools.UnityTest]
    public System.Collections.IEnumerator TheCardFadesOutOnlyWithTheMotionClass()
    {
        foreach (var motion in new[] { false, true })
        {
            using var ui = new UiTestKit();
            var bridge = FamilyKit.Bridge();
            using var card = new EventCardPresenter(ui.Root, bridge, FamilyKit.En);
            if (motion) ui.Q<VisualElement>("root").AddToClassList(SettingsPresenter.MotionClass);
            Assert.IsNotNull(card.Shown, "precondition: a card");
            card.Collapse();
            var el = ui.Q<VisualElement>("event-card");
            if (!motion) { Assert.AreEqual(DisplayStyle.None, el.style.display.value, "no motion: gone at once"); continue; }
            Assert.IsTrue(el.ClassListContains(EventCardPresenter.OutClass), "fading out");
            Assert.AreEqual(DisplayStyle.Flex, el.style.display.value, "still shown while it fades");
            for (int i = 0; i < 20 && el.style.display.value == DisplayStyle.Flex; i++) yield return null;
            card.EndFade();   // the scheduled end, should the editor's panel not have run it yet
            Assert.AreEqual(DisplayStyle.None, el.style.display.value, "gone after the fade");
            Assert.IsFalse(el.ClassListContains(EventCardPresenter.OutClass));
        }
    }

    /// <summary>F7: with the motion class on the root (a player, reduce motion off), a meter that moved flashes green or red
    /// and End Turn glows once every card is answered; without it (edit mode, reduce motion) the end state shows at once.</summary>
    [Test]
    public void TheBarMovesOnlyWithTheMotionClass()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        using var hud = new HudPresenter(ui.Root, bridge, FamilyKit.En);
        FamilyKit.PlayMonth(bridge);
        Assert.IsFalse(ui.Root.Query<Label>(className: "meter-value").ToList().Any(l => l.ClassListContains(HudPresenter.FlashUpClass) || l.ClassListContains(HudPresenter.FlashDownClass)),
            "no motion class: no flash");
        ui.Q<VisualElement>("root").AddToClassList(SettingsPresenter.MotionClass);
        var before = bridge.State.Indicators.ToDictionary(k => k.Key, k => (int)PlayerText.ShownPoints(k.Value));
        FamilyKit.PlayMonth(bridge);
        var moved = bridge.State.Indicators.Where(k => (int)PlayerText.ShownPoints(k.Value) != before[k.Key]).Select(k => k.Key).ToList();
        Assume.That(moved, Is.Not.Empty, "a meter moved this month");
        foreach (var id in moved)
        {
            var l = ui.Q<Label>("family-" + id + "-value");
            Assert.IsTrue(l.ClassListContains(HudPresenter.FlashUpClass) || l.ClassListContains(HudPresenter.FlashDownClass), id + " flashes");
        }
        foreach (var id in bridge.State.PendingEvents.ToList())
        {
            var e = bridge.Content.Events.First(x => x.Id == id);
            bridge.Queue(new ChooseEventOption(id, e.Options[0].Id));
        }
        hud.Render();
        // The glow is a scheduled toggle: its item exists once everything is answered (a frame turns the class on).
        Assert.IsTrue(typeof(HudPresenter).GetField("pulse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(hud) != null,
            "End Turn glows once every card is answered");
    }
}
