#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>Playtest 3: the start screen's language toggle and How to play; Why's sign no longer a question mark; each
/// resource's tip says what it means, how much of the change came from the choices, and that 0% does not end the game;
/// Home or Safety at 0% is said once; the outcome says other events count too; a card's figure line opens People at that
/// person, marked; a button's tip never sits over the meter tip; a toast slides in; and the Arabic UI mirrors the
/// screen's corners (LeftColumnTests and MinimapReliefTests check the fit at every window).</summary>
public class Playtest3Tests
{
    static UiText En => FamilyKit.En;
    static UiText Ar => FamilyKit.Ar;

    [Test]
    public void TheStartScreenSwitchesTheLanguageAndShowsHowToPlay()
    {
        using var ui = new UiTestKit();
        ui.Root.Q("root").RemoveFromClassList("loading");
        var bridge = FamilyKit.Bridge();
        using var toast = new ToastPresenter(ui.Root, bridge, En);
        using var newGame = new NewGamePresenter(ui.Root, bridge, toast, En, () => { });
        var switched = new List<string>(); int howTo = 0;
        using (var start = new StartScreenPresenter(ui.Root, newGame, false, () => { }, null, En, switched.Add, () => howTo++))
        {
            start.Show();
            Assert.AreEqual(DisplayStyle.Flex, ui.Q<VisualElement>("start-languages").style.display.value, "the toggle shows");
            Assert.IsTrue(ui.Q<Button>("start-language-en").ClassListContains(SettingsPresenter.SelectedClass), "English is marked");
            Assert.IsFalse(ui.Q<Button>("start-language-ar").ClassListContains(SettingsPresenter.SelectedClass));
            Assert.AreEqual("عربي", ui.Q<Button>("start-language-ar").text, "Arabic is named in Arabic");
            ui.Press("start-language-en");
            CollectionAssert.IsEmpty(switched, "the language in use is not switched to again");
            ui.Press("start-language-ar");
            CollectionAssert.AreEqual(new[] { UiLanguage.Arabic }, switched);
            Assert.AreEqual(En["hud.nav.how_to_play"], ui.Q<Button>("start-how-to-play").text);
            ui.Press("start-how-to-play");
            Assert.AreEqual(1, howTo);
        }
        using (var start = new StartScreenPresenter(ui.Root, newGame, false, () => { }))
        {
            start.Show();
            Assert.AreEqual(DisplayStyle.None, ui.Q<VisualElement>("start-languages").style.display.value, "no toggle without a switch");
            Assert.AreEqual(DisplayStyle.None, ui.Q<Button>("start-how-to-play").style.display.value, "no How to play without the hint");
        }
    }

    /// <summary>Why? tells last month again (a clock turning back); How to play takes the question mark.</summary>
    [Test]
    public void WhyAndHowToPlayHaveTheirOwnSigns()
    {
        using var ui = new UiTestKit();
        Assert.AreEqual("history", ui.Q<Button>("nav-why").Q<UiIcon>().Glyph);
        Assert.AreEqual("question", ui.Q<Button>("nav-how-to-play").Q<UiIcon>().Glyph);
        CollectionAssert.DoesNotContain(HudPresenter.FamilyGlyphs.Values, "question", "no resource has the help sign");
    }

    [Test]
    public void EveryResourceSaysWhatItMeansAndThatZeroDoesNotEndTheGame()
    {
        var c = FamilyKit.Chapter();
        foreach (var t in new[] { En, Ar })
        {
            var low = new GameState(); var mid = new GameState();
            foreach (var i in c.Indicators) { low.Indicators[i.Id] = 0; mid.Indicators[i.Id] = 0.5; }
            foreach (var i in c.Indicators)
            {
                Assert.IsTrue(t.Has("hud.meaning." + i.Id), $"{t["ui.lang"]}: hud.meaning.{i.Id}");
                Assert.AreEqual(t["hud.meaning." + i.Id], HudModel.Explained(t, c, null, mid, null, i.Id), $"{i.Id} at 50%: its meaning alone");
                Assert.AreEqual(t["hud.meaning." + i.Id] + "\n" + t["hud.low"], HudModel.Explained(t, c, null, low, null, i.Id), $"{i.Id} at 0%");
            }
        }
    }

    [Test]
    public void HomeOrSafetyAtZeroIsSaidOnceOnTheTurnItHappens()
    {
        GameState State(double home, double safety) { var s = new GameState(); s.Indicators["home"] = home; s.Indicators["safety"] = safety; return s; }
        CollectionAssert.AreEqual(new[] { En["hud.zero.home"] }, HudModel.ZeroLines(En, State(0.3, 0.3), State(0, 0.2)).ToList());
        CollectionAssert.AreEqual(new[] { En["hud.zero.home"], En["hud.zero.safety"] }, HudModel.ZeroLines(En, State(0.1, 0.1), State(0.004, 0)).ToList(),
            "0.4% shows as 0%");
        CollectionAssert.IsEmpty(HudModel.ZeroLines(En, State(0, 0), State(0, 0)).ToList(), "said once: not again the next turn");
        CollectionAssert.IsEmpty(HudModel.ZeroLines(En, null, State(0, 0)).ToList(), "nothing before the first turn");
    }

    /// <summary>The tip splits a turn's change: the chosen option's own effects (applied after the card's own, EventSystem)
    /// are the choices'; the card's own and every other cause are the rest.</summary>
    [Test]
    public void TheTipSplitsTheChangeBetweenTheChoicesAndTheRest()
    {
        var c = FamilyKit.Chapter();
        var e = c.Events.FirstOrDefault(x => x.Effects.Count > 0 && x.Options.Any(o => o.Effects.Count > 0))
                ?? c.Events.First(x => x.Options.Any(o => o.Effects.Count > 0));
        var option = e.Options.First(o => o.Effects.Count > 0);
        var id = option.Effects[0].Target;
        var why = new List<Cause> { new Cause("events", e.Id, 0, "chosen: " + option.Id) };
        why.AddRange(e.Effects.Concat(option.Effects).Select(fx => new Cause("events", fx.Target, fx.Delta, "change")));
        why.Add(new Cause("family", id, 0.03, "elsewhere"));
        var mine = option.Effects.Where(fx => fx.Target == id).Sum(fx => fx.Delta);
        var after = new GameState();
        after.ChosenOptions[e.Id] = option.Id;
        Assert.AreEqual(mine, FamilyGame.FromChoices(c, after, why, id), 1e-9, "the option's own effects only");
        after.ChosenOptions[e.Id] = "";
        Assert.AreEqual(0, FamilyGame.FromChoices(c, after, why, id), 1e-9, "an unanswered card is no choice");

        after.ChosenOptions[e.Id] = option.Id;
        var before = new GameState();
        foreach (var i in c.Indicators) { before.Indicators[i.Id] = 0.5; after.Indicators[i.Id] = 0.5; }
        after.Indicators[id] = 0.5 + why.Where(w => w.Target == id).Sum(w => w.Delta);
        double chosen = PlayerText.ShownPoints(mine), total = PlayerText.ShownPoints(after.Indicators[id]) - PlayerText.ShownPoints(0.5);
        var line = En.Format("hud.split", PlayerText.PercentChange(chosen / 100, 0), PlayerText.PercentChange((total - chosen) / 100, 0));
        Assume.That(chosen != 0 && total != chosen, "the content's option moves it by a whole point and the rest does too");
        StringAssert.Contains(line, HudModel.Explained(En, c, before, after, why, id));
    }

    [Test]
    public void AChoicesOutcomeSaysOtherEventsCountToo()
    {
        using var ui = new UiTestKit();
        ui.Root.Q("root").RemoveFromClassList("loading");
        var bridge = FamilyKit.Bridge();
        using var card = new EventCardPresenter(ui.Root, bridge, En);
        var e = bridge.Content.Events.First(x => x.Id == bridge.State.PendingEvents[0]);
        var option = e.Options.FirstOrDefault(o => o.Effects.Count > 0);
        Assume.That(option, Is.Not.Null, "the opening card has a choice with effects");
        ui.Press("option-" + option!.Id);
        Assert.AreEqual(En["event.others"], ui.Q<Label>("event-others").text);
    }

    [Test]
    public void AToastSlidesInOnlyWithMotion()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        using var toast = new ToastPresenter(ui.Root, bridge, En);
        var label = ui.Q<Label>("toast");
        toast.Show("A note");
        Assert.IsFalse(label.ClassListContains("toast-from"), "no motion class: no slide");
        toast.Clear();
        ui.Root.Q("root").AddToClassList(SettingsPresenter.MotionClass);
        toast.Show("A note");
        Assert.IsTrue(label.ClassListContains("toast-from"), "it starts above its place");
        label.RemoveFromClassList("toast-from");
        toast.Append("Another");
        Assert.IsFalse(label.ClassListContains("toast-from"), "a toast already showing does not slide again");
    }

    /// <summary>A card's figure line opens People scrolled to that person's card (in the player the screen had no layout
    /// yet on the first tick, so it stayed at the top), and marks it until the screen opens again.</summary>
    [UnityTest]
    public IEnumerator AFigureLineOpensPeopleAtThatPersonMarked()
    {
        var figures = Figures.Parse(FamilyKit.Inputs.Read(Figures.FileName));
        var last = figures.Of(GameRoot.AutomatedChapter).Last().Id;
        yield return Rendered(1280, 720, false, Check);

        IEnumerator Check(VisualElement root, SimBridge bridge, UiText text)
        {
            var people = new PeoplePresenter(root, figures, GameRoot.AutomatedChapter, text);
            var nav = new ScreenNav(root);
            try
            {
                for (int i = 0; i < 3; i++) yield return null;
                nav.Open(PeoplePresenter.Screen);
                people.ShowFigure(last);
                for (int i = 0; i < 10; i++) yield return null;
                var list = root.Q<ScrollView>("people-list");
                var card = root.Q("figure-" + last);
                Assert.IsTrue(card.ClassListContains(PeoplePresenter.ShownClass), "the card is marked");
                Assert.Greater(list.scrollOffset.y, 0f, "the list is scrolled down to the last figure");
                Assert.IsTrue(list.contentViewport.worldBound.Overlaps(card.worldBound), $"the card {card.worldBound} is in view {list.contentViewport.worldBound}");
                nav.Close(); nav.Open(PeoplePresenter.Screen);
                Assert.IsFalse(card.ClassListContains(PeoplePresenter.ShownClass), "opened again from the column: no mark");
            }
            finally { nav.Dispose(); people.Dispose(); }
        }
    }

    /// <summary>A rail button's tip left showing (its focus kept) is taken down when the meter tip opens.</summary>
    [UnityTest]
    public IEnumerator TheMeterTipTakesDownAButtonsTip()
    {
        yield return Rendered(1920, 1080, false, Check);

        IEnumerator Check(VisualElement root, SimBridge bridge, UiText text)
        {
            var hudTip = root.Q<Label>(HoverTip.TipName);
            root.Q<Button>("nav-why").Focus();
            yield return null;
            Assert.AreEqual(DisplayStyle.Flex, hudTip.style.display.value, "precondition: the focused button's tip shows");
            var meter = root.Q("meter-family-land");
            using (var ev = ClickEvent.GetPooled()) { ev.target = meter; meter.SendEvent(ev); }
            yield return null;
            Assert.AreEqual(DisplayStyle.Flex, root.Q(MeterTip.TipName).style.display.value, "precondition: the meter tip shows");
            Assert.AreEqual(DisplayStyle.None, hudTip.style.display.value, "the button's tip is down");
        }
    }

    /// <summary>At 1280x720 and 1920x1080 the Arabic UI is the English one mirrored: the column, its strip, the minimap's
    /// column and the minimap, the border caption, an open screen, the toast and Skip each sit where the English one sits,
    /// reflected about the window's middle.</summary>
    [UnityTest]
    public IEnumerator TheArabicLayoutIsTheEnglishOneMirrored()
    {
        var names = new[] { "left-column", "event-gutter", "right-column", "minimap", "map-layer", "screen-why", "toast", GameRoot.SkipName };
        foreach (var (w, h) in new[] { (1280, 720), (1920, 1080) })
        {
            var rects = new Dictionary<bool, Dictionary<string, Rect>>();
            foreach (var arabic in new[] { false, true })
                yield return Rendered(w, h, arabic, Measure);
            var problems = new List<string>();
            foreach (var n in names)
            {
                Rect en = rects[false][n], ar = rects[true][n];
                if (!(en.width > 0)) { problems.Add($"{w}x{h}: precondition: {n} shows"); continue; }
                float width = rects[false]["window"].width;
                // The border caption's words are set in each language's face (Noto Naskh in Arabic: the same words come out
                // wider, on shorter lines), so it mirrors by the corner it hangs from: its outer side and its bottom.
                bool sized = n != "map-layer";
                if (Mathf.Abs(ar.xMin - (width - en.xMax)) > 1f || (sized && Mathf.Abs(ar.width - en.width) > 1f)
                    || (sized ? Mathf.Abs(ar.yMin - en.yMin) : Mathf.Abs(ar.yMax - en.yMax)) > 1f)
                    problems.Add($"{w}x{h}: {n} is {ar} in Arabic, not {en} mirrored");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));

            IEnumerator Measure(VisualElement root, SimBridge bridge, UiText text)
            {
                var arabic = UiLanguage.Within(root.Q("root"));
                root.Q("screen-why").style.display = DisplayStyle.Flex;   // shown as ScreenNav shows it, without hiding Skip
                {
                    var toast = root.Q<Label>("toast"); toast.text = "A note"; toast.style.display = DisplayStyle.Flex;
                    var layer = root.Q<Label>("map-layer"); layer.text = "Mandate sub-districts, 1947"; layer.style.display = DisplayStyle.Flex;
                    root.Q(GameRoot.SkipName).AddToClassList(GameRoot.SkipShownClass);
                    root.Q("root").AddToClassList(EventCardPresenter.ModalClass);   // the column's strip shows under an open card
                    for (int i = 0; i < 4; i++) yield return null;
                    var r = rects[arabic] = names.ToDictionary(n => n, n => root.Q(n).worldBound);
                    r["window"] = root.worldBound;
                }
            }
        }
    }

    /// <summary>Renders Game.uxml at w x h with the HUD after one month, then runs <paramref name="check"/>, which may wait frames.</summary>
    static IEnumerator Rendered(int w, int h, bool arabic, Func<VisualElement, SimBridge, UiText, IEnumerator> check)
    {
        var rt = new RenderTexture(w, h, 0);
        var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
        ps.targetTexture = rt;
        var go = new GameObject("Playtest3"); go.SetActive(false);
        var doc = go.AddComponent<UIDocument>(); doc.panelSettings = ps;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
        go.SetActive(true);
        var root = doc.rootVisualElement;
        root.Q("root").RemoveFromClassList("loading");
        if (arabic) root.Q("root").AddToClassList(UiLanguage.ArabicClass);
        var text = arabic ? Ar : En;
        var bridge = FamilyKit.Bridge(text);
        var hud = new HudPresenter(root, bridge, text);
        try
        {
            FamilyKit.PlayMonth(bridge);
            for (int i = 0; i < 30 && !(root.Q("meter-family-livelihood").layout.width > 0); i++) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
            var inner = check(root, bridge, text);
            while (inner.MoveNext()) yield return inner.Current;
        }
        finally
        {
            hud.Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}
