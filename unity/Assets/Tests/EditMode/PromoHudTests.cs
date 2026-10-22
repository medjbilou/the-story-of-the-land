#nullable enable
using System.IO;
using System.Linq;
using System.Collections;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>The promo HUD pass (promo/final/03): the top bar's indicator icons, the round screen buttons and their lit
/// state, the event card's option signs and the panel trim. Behaviour is unchanged; these check the new pieces are wired
/// to the real elements and data.</summary>
public class PromoHudTests
{
    static UiText Text => FamilyKit.En;

    /// <summary>The opening month's first card's first option button (EventCardPresenter names it "option-" + its id).</summary>
    static string FirstOption(SimBridge b)
    {
        var id = b.State.PendingEvents.First();
        return "option-" + b.Content.Events.First(e => e.Id == id).Options[0].Id;
    }

    [Test]
    public void EveryIndicatorHasItsIcon()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        using var hud = new HudPresenter(ui.Root, bridge, Text);
        var meters = new[] { ("land", "region"), ("home", "house"), ("family", "people"), ("documents", "ledger"), ("livelihood", "coin"),
                             ("memory", "book"), ("sumud", "peaks"), ("community", "crowd"), ("safety", "shield"), ("solidarity", "rings") };
        CollectionAssert.AreEquivalent(bridge.Content.Indicators.Select(i => i.Id).ToArray(), meters.Select(m => m.Item1).ToArray(), "every resource (the money is beside Livelihood's name)");
        foreach (var (id, glyph) in meters)
        {
            var meter = "meter-family-" + id;
            var icon = ui.Q<VisualElement>(meter).Q<UiIcon>();
            Assert.IsNotNull(icon, meter);
            Assert.AreEqual(glyph, icon.Glyph, meter);
            CollectionAssert.Contains(UiIcon.Glyphs, glyph, meter + ": a glyph the icon can draw");
        }
    }

    [Test]
    public void EveryScreenButtonIsARoundIconWithItsName()
    {
        using var ui = new UiTestKit();
        foreach (var (button, name, glyph) in new[] { ("nav-why", "Why?", "history"), ("nav-how-to-play", "How to play", "question"),
                                                      ("nav-settings", "Settings", "gear"), ("new-game", "New game", "restart") })
        {
            var b = ui.Q<Button>(button);
            Assert.AreEqual(name, b.text, button + ": the name stays the button's text (legible, and what the event log records)");
            var disc = b.Q<UiIcon>(className: "nav-disc");
            Assert.IsNotNull(disc, button);
            Assert.AreEqual(glyph, disc.Glyph, button);
            Assert.AreEqual(PickingMode.Ignore, disc.pickingMode, button + ": a click on the disc reaches the button");
        }
    }

    [Test]
    public void TheOpenScreensButtonIsLit()
    {
        using var ui = new UiTestKit();
        var nav = new ScreenNav(ui.Root);
        try
        {
            Assert.IsFalse(ScreenNav.Screens.Any(s => ui.Q<Button>("nav-" + s).ClassListContains(ScreenNav.OpenClass)), "none lit at start");
            nav.Toggle("settings");
            foreach (var s in ScreenNav.Screens) Assert.AreEqual(s == "settings", ui.Q<Button>("nav-" + s).ClassListContains(ScreenNav.OpenClass), s);
            nav.Toggle("settings");
            Assert.IsFalse(ui.Q<Button>("nav-settings").ClassListContains(ScreenNav.OpenClass), "closing unlights it");
        }
        finally { nav.Dispose(); }
    }

    [Test]
    public void EachEventOptionCarriesItsSignAndStaysPressable()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        var shown = bridge.State.PendingEvents.First();
        var card = new EventCardPresenter(ui.Root, bridge, Text);
        var options = ui.Q<VisualElement>("event-options").Query<Button>().ToList();
        Assert.Greater(options.Count, 1);
        foreach (var b in options)
        {
            Assert.IsTrue(b.ClassListContains(EventCardPresenter.OptionClass), b.name);
            Assert.AreEqual(0, b.childCount, b.name + ": the sign is painted, so the button stays a leaf whose wrapped text is measured");
        }
        UiTestKit.Press(ui.Q<Button>(FirstOption(bridge)));
        Assert.IsNotNull(bridge.Queued.OfType<ChooseEventOption>().SingleOrDefault(c => c.EventId == shown), "the option still answers the event");
        var next = ui.Q<VisualElement>("event-options").Query<Button>().ToList();
        if (card.Shown?.Id == shown)   // the card stays with the outcome; its Continue carries the same sign
        {
            CollectionAssert.AreEqual(new[] { "event-continue" }, next.Select(b => b.name).ToArray());
            Assert.IsTrue(next[0].ClassListContains(EventCardPresenter.OptionClass));
            Assert.AreEqual(0, next[0].childCount);
            UiTestKit.Press(next[0]);
        }
        Assert.AreNotEqual(shown, card.Shown?.Id, "the answered card goes");
        card.Dispose();
    }

    [Test]
    public void ThePanelsCarryTheTrimAndItTakesNoClicks()
    {
        using var ui = new UiTestKit();
        foreach (var name in new[] { "event-card", "minimap" }.Concat(ScreenNav.Screens.Concat(ScreenNav.Extra).Select(x => "screen-" + x)))
        {
            var frame = ui.Q<VisualElement>(name).Children().OfType<UiFrame>().SingleOrDefault();
            Assert.IsNotNull(frame, name);
            Assert.AreEqual(PickingMode.Ignore, frame!.pickingMode, name);
        }
    }

    /// <summary>The bar at every window shape, after a month of the family game has played and with the widest values,
    /// changes and date (the tiers: HudPresenter). 16:9, 21:9 and 16:10 take the one-row bar, icon first (F6); 4:3 and 5:4 take
    /// the stacked bar without icons; square and narrower windows wrap into two rows. Everywhere:
    /// every meter label stays inside its meter and the bar, no two meters' labels overlap, the meters keep 5% spare room
    /// and stay 12 px clear of the screen buttons, End Turn and the badge, the badge holds its widest line inside its trim at
    /// a fixed width, and the panels start below the bar (reviews N2, N3, N5, N7).</summary>
    [UnityTest]
    public IEnumerator EveryWindowShapeFitsTheBarAfterATurn()
    {
        // (window, icons, stacked changes, two rows, three rows): 16:9 fullscreen; a 16:9 window; a 16:10 MacBook browser
        // (~1911 logical); 21:9 ultrawide (~2217); 16:10 fullscreen (~1821) keeps the icons on the stacked bar; 4:3 and 5:4
        // drop them; square and narrower wrap; a phone upright (390x844, ~790) takes three rows (#398).
        // HUD round 2 moved the four screen buttons to the left column (#602), so the bar holds about 248 px less and every
        // tier starts that much narrower (part 3): 16:10 and 4:3 now keep the one-row bar with its icons; 5:4 the stacked bar
        // with them. At 1024x768 the owner's text floor (#624) raises the small text to 20 px, and the bar takes the stacked
        // tier without icons (HudPresenter.FloorGrowth).
        // Social cohesion made seven meters (#380); the owner chose "tighten and re-tier" (16 October 2026). Only these rows
        // moved, each to the tier the seven meters' measured widths need (HudPresenter's notes): 1280x720 and 1366x768 (the
        // floor raises their captions to 18 and 17 px), 1920x1200 and 1440x900 (16:10) take the stacked bar with its icons;
        // 1280x1024 (5:4) drops them; 1024x1024 (square) takes two rows. Every fit assert below is unchanged.
        // The family bar's eleven meters (HudPresenter's notes): every window here, 16:9 and 21:9 included, took the
        // stacked bar. F6 (the owner: one row at 1920x1080 and above): below NarrowBelowPx the one row is icon first, its
        // captions in the tooltips, down to IconRowBelowPx: 16:9, 21:9 and 16:10 (1819 logical) are one row again, 1280x720 and
        // 1366x768 too (the floor's 18 and 17 px captions are hidden there, so they take no room).
        foreach (var (w, h, icons, narrow, twoRow, threeRow, percent) in new[] { (1920, 1080, true, false, false, false), (1280, 720, true, false, false, false),
                                                              (1366, 768, true, false, false, false), (1760, 1000, true, false, false, false),
                                                              (2560, 1080, true, false, false, false),
                                                              (1920, 1200, true, false, false, false), (1440, 900, true, false, false, false),
                                                              (1024, 768, false, true, false, false), (1280, 1024, false, true, false, false),
                                                              (1024, 1024, false, true, true, false), (960, 1080, false, true, true, false),
                                                              (800, 1000, false, true, true, false), (390, 844, false, true, true, true) }
            .Select(c => (c.Item1, c.Item2, c.Item3, c.Item4, c.Item5, c.Item6, 100))
            // Playtest 3: 130% text (SettingsPresenter.SetTextSize: the reference resolution over 1.3) makes a 16:9 window
            // about 1477x831 logical: the stacked bar without icons, flat (HudPresenter.FlatClass).
            .Concat(new[] { (1920, 1080, false, true, false, false, 130), (1280, 720, false, true, false, false, 130) }))
        {
            var rt = new RenderTexture(w, h, 0);
            var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            var reference = ps.referenceResolution;
            ps.referenceResolution = new Vector2Int(Mathf.RoundToInt(reference.x * 100f / percent), Mathf.RoundToInt(reference.y * 100f / percent));
            var go = new GameObject("CompactTest");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            var root = doc.rootVisualElement;
            HudPresenter? hud = null;
            try
            {
                // As in the game: the bar is laid out (the loading screen) before the HUD is made, and the window never resizes.
                for (int i = 0; i < 20 && !(root.Q("top-bar").worldBound.width > 0); i++) yield return null;
                root.Q("root").RemoveFromClassList("loading");
                var bridge = FamilyKit.Bridge();
                hud = new HudPresenter(root, bridge, Text);
                FamilyKit.PlayMonth(bridge);                            // real changes on every meter that moved
                for (int i = 0; i < 6; i++) yield return null;
                if (threeRow)
                {
                    // #380 (a consequence of the owner's "tighten + re-tier", 16 October 2026): a phone's seven meters take two lines with the turn-1 values
                    // too, so the bar's second meter line is never an empty band. A line's meters share their centre (.meters
                    // align-items: center); their tops differ with their captions' lines.
                    // (Within a line the centres can differ by a pixel: review of #831, so lines are 3 px apart or more.)
                    var centres = root.Q("family-meters").Children().Where(m => m.resolvedStyle.display != DisplayStyle.None)
                        .Select(m => m.worldBound.center.y).OrderBy(y => y).ToList();
                    var lines = 1 + centres.Zip(centres.Skip(1), (a, b) => b - a).Count(d => d > 3f);
                    // The family's eleven meters take two lines on a phone upright and three at 800x1000: never one overlong line.
                    Assert.That(lines, Is.InRange(2, 3), $"{w}x{h}: the meters' lines with the turn-1 values");
                }
                // Review P1's roomy promo sizes need the one-row bar with its captions, which the family's eleven meters take
                // only above 21:9 (HudPresenter.NarrowBelowPx): no window here is roomy.
                Assert.IsFalse(root.Q("top-bar").ClassListContains(HudPresenter.RoomyClass), $"{w}x{h}: roomy only on the one-row bar");
                // The widest realistic changes, formatted as HudModel formats them, the widest values (review N7), and the
                // widest date line.
                foreach (var r in bridge.Content.Indicators)
                {
                    root.Q<Label>("family-" + r.Id + "-delta").text = "-10%";
                    root.Q<Label>("family-" + r.Id + "-value").text = "100%";
                }
                root.Q<Label>("turn").text = Text.Format("hud.turn", 120);
                var dateLabel = root.Q<Label>("date");
                dateLabel.text = TopBarTests.WidestDate(dateLabel, Text);
                for (int i = 0; i < 6; i++) yield return null;          // the roomy bar steps back down if it must
                var bar = root.Q("top-bar"); var barRect = bar.worldBound;
                var where = $"{w}x{h} at {percent}% (bar {barRect.width:0} px)";
                Assert.AreEqual(percent > 100, root.Q("root").ClassListContains(HudPresenter.FlatClass), where + ": flat");
                if (percent > 100) Assert.LessOrEqual(barRect.height, HudPresenter.FlatBarPx + 0.5f, where + ": the flat bar's height");
                Assert.AreEqual(!icons, bar.ClassListContains(HudPresenter.CompactClass), where + ": compact (no icons)");
                Assert.AreEqual(narrow, root.Q("root").ClassListContains(HudPresenter.NarrowClass), where + ": narrow");
                Assert.AreEqual(!narrow, bar.ClassListContains(HudPresenter.IconRowClass), where + ": icon first on the one-row bar");
                Assert.AreEqual(twoRow, root.Q("root").ClassListContains(HudPresenter.TwoRowClass), where + ": two rows");
                Assert.AreEqual(threeRow, root.Q("root").ClassListContains(HudPresenter.ThreeRowClass), where + ": three rows");
                foreach (var icon in root.Q("family-meters").Query<UiIcon>().ToList())
                    Assert.AreEqual(icons ? DisplayStyle.Flex : DisplayStyle.None, icon.resolvedStyle.display, $"{where}: {icon.Glyph}");
                var badge = root.Q(className: "date-box");
                foreach (var l in badge.Query<Label>().ToList())
                    Assert.LessOrEqual(l.worldBound.xMax, badge.worldBound.xMax - 4f, $"{where}: '{l.text}' {l.worldBound} runs under the badge's trim {badge.worldBound}");
                var labels = new System.Collections.Generic.List<(string meter, Label label)>();
                foreach (var m in root.Q("family-meters").Children())
                    foreach (var l in m.Query<Label>().ToList().Where(l => !string.IsNullOrEmpty(l.text) && l.resolvedStyle.display != DisplayStyle.None))
                    {
                        labels.Add((m.name, l));
                        var r = l.worldBound;
                        Assert.LessOrEqual(r.xMax, m.worldBound.xMax + 0.5f, $"{where}: '{l.text}' {r} leaves {m.name} {m.worldBound} on the right");
                        Assert.GreaterOrEqual(r.xMin + 0.5f, m.worldBound.xMin, $"{where}: '{l.text}' leaves {m.name} on the left");
                        Assert.GreaterOrEqual(r.yMin, barRect.yMin + 1f, $"{where}: '{l.text}' leaves the bar at the top");
                        Assert.LessOrEqual(r.yMax, barRect.yMax - 1f, $"{where}: '{l.text}' leaves the bar at the bottom");
                    }
                foreach (var a in labels)
                    foreach (var b in labels)
                        if (a.meter != b.meter && a.label.worldBound.Overlaps(b.label.worldBound))
                            Assert.Fail($"{where}: '{a.label.text}' ({a.meter}) overlaps '{b.label.text}' ({b.meter})");
                // The meters never reach the screen buttons or End Turn (beside them on one row, above them on two), with a
                // margin for the browser's text measuring a little wider than the editor's.
                foreach (var other in new[] { root.Q("nav").worldBound, root.Q(className: "turn-box").worldBound, badge.worldBound })
                    foreach (var (meter, l) in labels)
                    {
                        var r = l.worldBound; r.xMax += 12f;
                        Assert.IsFalse(r.Overlaps(other), $"{where}: '{l.text}' ({meter}) {r} is within 12 px of {other}");
                    }
                // The meters are not squeezed: they take their natural widths with at least 5% to spare before the screen
                // buttons (or the bar's end, on two rows), room for the browser's text measuring a little wider than the editor's.
                var meters = root.Q("family-meters").Children().ToList();
                var used = meters[meters.Count - 1].worldBound.xMax - meters[0].worldBound.xMin;
                var limit = twoRow ? barRect.xMax - 14f : root.Q("nav").worldBound.xMin;
                Assert.GreaterOrEqual(limit - meters[meters.Count - 1].worldBound.xMax, 0.05f * used, $"{where}: the meters use {used:0} of {limit - meters[0].worldBound.xMin:0} px");
                // On a phone the meters wrap into quarters (#380), so the rule above holds by construction there: each meter's
                // labels keep 5% of its own quarter free instead (review of #831).
                if (threeRow)
                    foreach (var m in meters.Where(m => m.resolvedStyle.display != DisplayStyle.None))
                        foreach (var l in m.Query<Label>().ToList().Where(l => !string.IsNullOrEmpty(l.text)))
                        {
                            // The text's own width (a caption's box stretches to its column), at most its box (a wrapped caption).
                            var natural = l.MeasureTextSize(l.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                            var right = l.worldBound.xMin + Mathf.Min(natural, l.worldBound.width);
                            Assert.LessOrEqual(right, m.worldBound.xMax - 0.05f * m.worldBound.width,
                                $"{where}: '{l.text}' ends at {right:0} and leaves less than 5% of {m.name} {m.worldBound}");
                        }
                // Review P2: every screen button's caption fits its button (an icon-only button draws no caption: part 3).
                foreach (var nb in root.Q("nav").Query<Button>().ToList().Where(b => b.resolvedStyle.color.a > 0.01f))
                {
                    var natural = nb.MeasureTextSize(nb.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                    Assert.LessOrEqual(natural.x, nb.contentRect.width + 0.5f, $"{where}: the caption '{nb.text}' ({natural.x:0} px) overflows its button ({nb.contentRect.width:0} px)");
                }
                foreach (var e in new[] { root.Q("nav"), root.Q(className: "turn-box") })
                    Assert.LessOrEqual(e.worldBound.yMax, barRect.yMax + 0.5f, $"{where}: {e.name}{string.Join(".", e.GetClasses())} leaves the bar");
                // The date plate may hang below the one-row bar, as promo 03's does (part 3), but never past the panels under it.
                Assert.LessOrEqual(badge.worldBound.yMax, root.Q("left-column").worldBound.yMin, $"{where}: the plate {badge.worldBound} reaches the left column");
                Assert.GreaterOrEqual(root.Q("right-column").worldBound.yMin, barRect.yMax, where + ": the minimap column starts under the bar");
                var screen = root.Q("screen-why");
                screen.style.display = DisplayStyle.Flex;                // opened, as ScreenNav does
                for (int i = 0; i < 2; i++) yield return null;
                Assert.GreaterOrEqual(screen.worldBound.yMin, barRect.yMax, where + ": the screens start under the bar");
                screen.style.display = DisplayStyle.None;
                // The badge keeps its width for a short date line, so nothing shifts when the month changes.
                var wide = badge.worldBound.width;
                root.Q<Label>("turn").text = Text.Format("hud.turn", 1);
                root.Q<Label>("date").text = Text.Format("hud.date", PlayerText.Date(new System.DateTime(1948, 5, 1)));
                for (int i = 0; i < 2; i++) yield return null;
                Assert.AreEqual(wide, badge.worldBound.width, 0.5f, where + ": the badge's width follows its text");
            }
            finally { hud?.Dispose(); Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); }
        }
    }

    static void Near(Color e, Color a, string what) =>
        Assert.That(Mathf.Abs(e.r - a.r) + Mathf.Abs(e.g - a.g) + Mathf.Abs(e.b - a.b), Is.LessThan(0.02f), $"{what}: expected {e} got {a}");

    /// <summary>WCAG relative luminance of a colour composited over the navy behind the HUD.</summary>
    static float Luminance(Color c)
    {
        var navy = new Color(9 / 255f, 14 / 255f, 26 / 255f);
        float Ch(float v, float under) { var x = v * c.a + under * (1 - c.a); return x <= 0.04045f ? x / 12.92f : Mathf.Pow((x + 0.055f) / 1.055f, 2.4f); }
        return 0.2126f * Ch(c.r, navy.r) + 0.7152f * Ch(c.g, navy.g) + 0.0722f * Ch(c.b, navy.b);
    }

    static float Contrast(Color a, Color b) { float x = Luminance(a), y = Luminance(b); return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f); }

    /// <summary>Review I1: "open" and keyboard focus are separate signals, and a pointer click does not leave a screen
    /// button focused, so a screen just closed with the mouse never looks open.</summary>
    [UnityTest]
    public IEnumerator FocusAndOpenAreDistinctAndAPointerClickClearsFocus()
    {
        using var ui = new UiTestKit();
        var nav = new ScreenNav(ui.Root);
        var blur = new PointerBlur(ui.Root);
        var bridge = FamilyKit.Bridge();
        var option = FirstOption(bridge);
        var card = new EventCardPresenter(ui.Root, bridge, Text);
        ui.Q<VisualElement>("root").RemoveFromClassList("loading");   // ready: a hidden (loading) button takes no focus
        try
        {
            VisualElement Disc(string s) => ui.Q<Button>("nav-" + s).Q(className: "nav-disc");
            nav.Toggle("settings");
            var why = ui.Q<Button>("nav-why");
            why.Focus();
            for (int i = 0; i < 10 && why.focusController.focusedElement != why; i++) yield return null;
            yield return null; yield return null;
            Assert.AreSame(why, why.focusController.focusedElement, "precondition: the keyboard focus is on Why?");
            var open = Disc("settings").resolvedStyle; var focused = Disc("why").resolvedStyle;
            // Open: a gold-light rim on the raised navy disc. Focus: an ink rim on the warm focus fill, at least 2.5:1 apart.
            Assert.GreaterOrEqual(Contrast(open.backgroundColor, focused.backgroundColor), 2.5f, "focus and open differ in fill");
            Assert.Greater(Contrast(open.borderTopColor, focused.borderTopColor), 1.1f, "focus and open differ in rim colour");

            // Close Settings with the mouse: the click blurs it, and its disc returns to the unlit rim.
            var settings = ui.Q<Button>("nav-settings");
            settings.Focus();
            nav.Toggle("settings");
            using (var e = ClickEvent.GetPooled()) { e.target = settings; settings.SendEvent(e); }   // bubbles to PointerBlur
            yield return null; yield return null;
            Assert.AreNotSame(settings, settings.focusController.focusedElement, "a pointer click leaves no focus behind");
            Assert.IsFalse(settings.ClassListContains(ScreenNav.OpenClass));
            Near(new Color(196 / 255f, 160 / 255f, 92 / 255f), Disc("settings").resolvedStyle.borderTopColor, "the closed button's rim is unlit");
            Assert.AreEqual(Disc("how-to-play").resolvedStyle.borderTopWidth, Disc("settings").resolvedStyle.borderTopWidth, "the closed button looks like the others");

            // Every other Button too (review N1), the runtime-made event option included: a pointer click leaves no focus
            // fill. The click lands on the button's own text or on a child (the disc), and bubbles to the root's handler.
            var navy = new Color(9 / 255f, 14 / 255f, 26 / 255f);
            foreach (var (name, part) in new[] { ("end-turn", ""), ("new-game", "nav-disc"), (option, "") })
            {
                var b = ui.Q<Button>(name);
                VisualElement look = part == "" ? b : b.Q(className: part);
                b.Focus();
                for (int i = 0; i < 10 && b.focusController.focusedElement != b; i++) yield return null;
                Assert.AreSame(b, b.focusController.focusedElement, name + ": precondition, focused");
                VisualElement target = part == "" ? b : b.Q(className: part);
                using (var e = ClickEvent.GetPooled()) { e.target = target; target.SendEvent(e); }
                yield return null; yield return null;
                Assert.AreNotSame(b, b.focusController.focusedElement, name + ": a pointer click leaves no focus behind");
                Near(navy, look.resolvedStyle.backgroundColor, name + ": no focus fill after the click");
            }

            // Keyboard activation (Enter, Space: NavigationSubmitEvent) runs the button and keeps its focus (review N8): this pins
            // PointerBlur's assumption that only a pointer click raises ClickEvent.
            var endTurn = ui.Q<Button>("end-turn");
            int pressed = 0; System.Action count = () => pressed++;
            endTurn.clicked += count;
            endTurn.Focus();
            for (int i = 0; i < 10 && endTurn.focusController.focusedElement != endTurn; i++) yield return null;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = endTurn; endTurn.SendEvent(e); }
            yield return null;
            endTurn.clicked -= count;
            Assert.AreEqual(1, pressed, "Enter/Space pressed End Turn");
            Assert.AreSame(endTurn, endTurn.focusController.focusedElement, "keyboard activation keeps the focus");
        }
        finally { nav.Dispose(); blur.Dispose(); card.Dispose(); }
    }

    /// <summary>Review I3: keyboard focus changes End Turn, an event option and a screen button by at least 3:1 (WCAG 2.4.13).</summary>
    [UnityTest]
    public IEnumerator FocusChangesEachControlByAtLeastThreeToOne()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        var option = FirstOption(bridge);
        var card = new EventCardPresenter(ui.Root, bridge, Text);
        ui.Q<VisualElement>("root").RemoveFromClassList("loading");
        try
        {
            foreach (var (name, part) in new[] { ("end-turn", ""), (option, ""), ("nav-why", "nav-disc") })
            {
                var b = ui.Q<Button>(name);
                VisualElement target = part == "" ? b : b.Q(className: part);
                for (int i = 0; i < 5; i++) yield return null;
                var before = target.resolvedStyle;
                Color bgBefore = before.backgroundColor, rimBefore = before.borderTopColor;
                b.Focus();
                for (int i = 0; i < 10 && b.focusController.focusedElement != b; i++) yield return null;
                yield return null; yield return null;
                Assert.AreSame(b, b.focusController.focusedElement, name + ": focused");
                var after = target.resolvedStyle;
                var change = Mathf.Max(Contrast(bgBefore, after.backgroundColor), Contrast(rimBefore, after.borderTopColor));
                Assert.GreaterOrEqual(change, 3f, $"{name}: focus changes it by {change:0.00}:1");
                // The gold glyph on it (End Turn's hourglass, the disc's icon) turns gold-light: >= 3:1 on the focus fill (review N4).
                if (name != option)
                {
                    var glyphOn = part == "" ? (VisualElement)b : target;
                    Assert.IsTrue(UiIcon.IsFocused(glyphOn), name + ": its glyph is drawn lit");
                    var glyph = Contrast(UiIcon.LineColour(true), after.backgroundColor);
                    Assert.GreaterOrEqual(glyph, 3f, $"{name}: the lit glyph on the focus fill is {glyph:0.00}:1");
                }
                b.Blur();
            }
        }
        finally { card.Dispose(); }
    }

    /// <summary>Review M1: every glyph paints without an error (each is drawn in a laid-out panel, and a second callback on
    /// the same element proves the repaint ran).</summary>
    [UnityTest]
    public IEnumerator EveryGlyphDraws()
    {
        var rt = new RenderTexture(640, 360, 0);
        var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
        ps.targetTexture = rt;
        var go = new GameObject("GlyphTest");
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = ps;
        try
        {
            yield return null;
            var drawn = new System.Collections.Generic.HashSet<string>();
            foreach (var glyph in UiIcon.Glyphs)
            {
                var icon = new UiIcon { Glyph = glyph };
                icon.style.width = 32; icon.style.height = 32;
                var name = glyph;
                icon.generateVisualContent += _ => drawn.Add(name);
                doc.rootVisualElement.Add(icon);
            }
            doc.rootVisualElement.style.flexDirection = FlexDirection.Row; doc.rootVisualElement.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < 30 && drawn.Count < UiIcon.Glyphs.Count; i++) { doc.rootVisualElement.MarkDirtyRepaint(); yield return null; }
            CollectionAssert.AreEquivalent(UiIcon.Glyphs, drawn, "every glyph was painted");
            LogAssert.NoUnexpectedReceived();
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); }
    }

    /// <summary>Integration bug B6: the camera keys never move UI focus or press a button. The UI action map binds no
    /// keyboard keys to Navigate, and NavigationGuard drops every directional move at the root, so with focus on a HUD button
    /// the WASD/arrow moves (and the W/A/S/D key presses) leave focus where it is, press nothing and never arm New Game.
    /// Tab (Next) still moves focus, so the HUD stays keyboard-reachable.</summary>
    [UnityTest]
    public IEnumerator CameraKeysNeverMoveFocusOrPressAButton()
    {
        // The input asset: no keyboard binding on the UI map's Navigate action.
        var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        Assert.IsNotNull(actions);
        foreach (var b in actions.FindActionMap("UI", true).FindAction("Navigate", true).bindings)
            StringAssert.DoesNotStartWith("<Keyboard>", b.path, "Navigate must not read a camera key");

        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        var option = FirstOption(bridge);
        var toast = new ToastPresenter(ui.Root, bridge, Text);
        var newGame = new NewGamePresenter(ui.Root, bridge, toast, Text, () => { });
        var card = new EventCardPresenter(ui.Root, bridge, Text);
        var guard = new NavigationGuard(ui.Root);
        ui.Q<VisualElement>("root").RemoveFromClassList("loading");
        var presses = 0;
        var buttons = ui.Root.Query<Button>().ToList();
        System.Action count = () => presses++;
        foreach (var b in buttons) b.clicked += count;
        try
        {
            foreach (var name in new[] { "nav-why", "new-game", "end-turn", option })
            {
                var start = ui.Q<Button>(name);
                start.Focus();
                for (int i = 0; i < 10 && start.focusController.focusedElement != start; i++) yield return null;
                Assert.AreSame(start, start.focusController.focusedElement, name + ": precondition, focused");
                foreach (var d in new[] { NavigationMoveEvent.Direction.Right, NavigationMoveEvent.Direction.Left,
                                          NavigationMoveEvent.Direction.Up, NavigationMoveEvent.Direction.Down })
                {
                    using (var e = NavigationMoveEvent.GetPooled(d)) { e.target = start; start.SendEvent(e); }
                    yield return null;
                    Assert.AreSame(start, start.focusController.focusedElement, $"{name}: a {d} move left focus where it was");
                }
                foreach (var key in new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.Q, KeyCode.E,
                                          KeyCode.PageUp, KeyCode.PageDown, KeyCode.Home, KeyCode.End })
                {
                    using (var e = KeyDownEvent.GetPooled((char)0, key, EventModifiers.None)) { e.target = start; start.SendEvent(e); }
                    yield return null;
                    Assert.AreSame(start, start.focusController.focusedElement, $"{name}: {key} left focus where it was");
                }
                Assert.AreEqual(0, presses, name + ": the camera keys pressed no button");
                Assert.IsFalse(newGame.Armed, name + ": New Game is not armed");
            }
            // Tab still moves focus: the HUD stays reachable from the keyboard.
            var from = ui.Q<Button>("nav-why");
            from.Focus();
            for (int i = 0; i < 10 && from.focusController.focusedElement != from; i++) yield return null;
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next)) { e.target = from; from.SendEvent(e); }
            yield return null;
            Assert.AreNotSame(from, from.focusController.focusedElement, "Tab moves focus on");
            Assert.AreEqual(0, presses, "Tab pressed no button");
        }
        finally
        {
            foreach (var b in buttons) b.clicked -= count;
            guard.Dispose(); card.Dispose(); newGame.Dispose(); toast.Dispose();
        }
    }

    /// <summary>Integration bug B7: the toast is opaque and never covers an open screen or the minimap column.</summary>
    [UnityTest]
    public IEnumerator TheToastIsOpaqueAndClearOfTheScreens()
    {
        using var ui = new UiTestKit();
        var nav = new ScreenNav(ui.Root);
        ui.Q<VisualElement>("root").RemoveFromClassList("loading");
        var toast = ui.Q<Label>("toast");
        toast.text = "Not done: That event is not waiting for a decision";
        toast.style.display = DisplayStyle.Flex;
        try
        {
            foreach (var s in ScreenNav.Screens)
            {
                nav.Open(s);
                for (int i = 0; i < 3; i++) yield return null;
                Assert.AreEqual(1f, toast.resolvedStyle.backgroundColor.a, 1e-3f, "opaque");
                Assert.IsFalse(toast.worldBound.Overlaps(ui.Q<VisualElement>("screen-" + s).worldBound), $"the toast {toast.worldBound} covers {s}");
                Assert.IsFalse(toast.worldBound.Overlaps(ui.Q<VisualElement>("right-column").worldBound), "the toast covers the minimap column");
                Assert.GreaterOrEqual(toast.worldBound.yMin, ui.Q<VisualElement>("top-bar").worldBound.yMax, "the toast starts under the bar");
            }
        }
        finally { nav.Dispose(); }
    }
}
