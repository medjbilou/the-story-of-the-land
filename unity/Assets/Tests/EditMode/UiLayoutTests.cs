#nullable enable
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

/// <summary>The screen layout at 1920x1080 and 1280x720 (the panel scales with the screen against a 1080p reference,
/// so both resolve to the same logical layout): no fixed region overlaps another, no centre screen covers a fixed region,
/// and the top bar's date box, meters, screen buttons and End Turn box sit in order inside it. The family's meters are made
/// in code and fit at every tier (PromoHudTests). The decision card is not a fixed region: it opens in the middle over a
/// backdrop that covers the others (DecisionCardTests).</summary>
public class UiLayoutTests
{
    static readonly string[] Fixed = { "top-bar", "minimap" };
    static readonly string[] Screens = ScreenNav.Screens.Concat(ScreenNav.Extra).Select(s => "screen-" + s).ToArray();

    [UnityTest]
    public IEnumerator NothingOverlapsAt1080pAnd720p()
    {
        foreach (var (w, h) in new[] { (1920, 1080), (1280, 720), (1280, 1024), (1531, 784), (2560, 1080), (1920, 900) })
        {
            var rt = new RenderTexture(w, h, 0);
            var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            var go = new GameObject("LayoutTest");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            var root = doc.rootVisualElement;
            for (int i = 0; i < 20 && float.IsNaN(root.Q("minimap").worldBound.width); i++) yield return null;
            try
            {
                Rect R(string name) => root.Q(name).worldBound;
                foreach (var a in Fixed)
                    foreach (var b in Fixed)
                        if (string.CompareOrdinal(a, b) < 0) Assert.IsFalse(R(a).Overlaps(R(b)), $"{w}x{h}: {a} {R(a)} overlaps {b} {R(b)}");
                // No screen covers the top bar or the minimap.
                foreach (var s in Screens)
                    foreach (var f in Fixed)
                        Assert.IsFalse(R(s).Overlaps(R(f)), $"{w}x{h}: {s} {R(s)} overlaps {f} {R(f)}");
                Assert.LessOrEqual(R("family-meters").xMax, R("nav").xMin + 0.5f, $"{w}x{h}: the meters run into the screen buttons");
                Assert.LessOrEqual(R("top-bar").xMin, root.Q(className: "date-box").worldBound.xMin, $"{w}x{h}: date box starts outside the top bar");
                Assert.LessOrEqual(root.Q(className: "date-box").worldBound.xMax, R("family-meters").xMin + 0.5f, $"{w}x{h}: date box runs into the meters");
                Assert.LessOrEqual(R("nav").xMax, root.Q(className: "turn-box").worldBound.xMin + 0.5f, $"{w}x{h}: the screen buttons run into the End Turn box");
                Assert.LessOrEqual(root.Q(className: "turn-box").worldBound.xMax, R("top-bar").xMax + 0.5f, $"{w}x{h}: End Turn box overflows the top bar");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); }
        }
    }

    /// <summary>#400: in the two-row HUD every open screen takes the full width, and the stylesheet hides the minimap column
    /// under it (.hud-two-row.screen-open). At the start of the family game, with its first card waiting (collapsed, so its
    /// backdrop covers nothing), the minimap's buttons are live, and none may take the focus while hidden: when Why opens in
    /// two rows, and when a window already showing Why beside the column narrows into two rows (the tier changes under the
    /// open screen). Widening again, or closing the screen, brings them back. Any open screen covers the decision layer:
    /// its "Decision waiting" button is out of reach until the screen closes.</summary>
    [UnityTest]
    public IEnumerator TheTwoRowHudKeepsWhatAScreenCoversOutOfReach()
    {
        var rt = new RenderTexture(1920, 1080, 0);
        var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
        ps.targetTexture = rt;
        var go = new GameObject("TwoRowTest");
        go.SetActive(false);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = ps;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
        go.SetActive(true);
        var root = doc.rootVisualElement;
        var bridge = FamilyKit.Bridge();
        var owned = new List<System.IDisposable>();
        void Resize(int w, int h) { var old = rt; rt = new RenderTexture(w, h, 0); ps.targetTexture = rt; old.Release(); Object.DestroyImmediate(old); }
        try
        {
            root.Q("root").RemoveFromClassList("loading");
            owned.Add(new HudPresenter(root, bridge, Text));
            var nav = new ScreenNav(root); owned.Add(nav);
            var card = new EventCardPresenter(root, bridge, Text); owned.Add(card);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsNotNull(root.Q("event-options").Q<Button>(), "precondition: an event's options are shown");
            card.Collapse();
            for (int i = 0; i < 2; i++) yield return null;
            var overview = root.Q<Button>("minimap-overview");
            var waiting = root.Q<Button>("event-reopen");
            Assert.IsTrue(overview.enabledInHierarchy && overview.resolvedStyle.display == DisplayStyle.Flex, "precondition: the minimap's whole-map button is live");
            var covered = new[] { "right-column" };
            List<Button> Buttons() => covered.SelectMany(r => root.Q(r).Query<Button>().ToList()).ToList();
            void Out(Button b, string where)
            {
                Assert.IsFalse(b.canGrabFocus, $"{where}: {b.name} cannot take the focus");
                b.Focus();
                Assert.AreNotSame(b, b.focusController?.focusedElement, $"{where}: {b.name} is not focused");
            }
            void Reach(bool expected, string where)
            {
                // In reach: the button known to be shown. Out of reach: every button in the covered panels.
                if (expected) Assert.IsTrue(overview.canGrabFocus, $"{where}: {overview.name} can take the focus");
                else foreach (var b in Buttons()) Out(b, where);
                // Under any open screen the decision layer is hidden and out of reach; with none, "Decision waiting" is live.
                if (nav.Current != null) Out(waiting, where);
                else Assert.IsTrue(waiting.canGrabFocus, $"{where}: Decision waiting can take the focus");
            }

            // Why at 1080p: the minimap column stays beside it, in reach.
            nav.Open("why");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(root.Q("root").ClassListContains(HudPresenter.TwoRowClass), "precondition: one row at 1080p");
            Reach(true, "1080p, Why open");

            // The window narrows under the open screen: two rows, the screen full width, the panels hidden and out of reach.
            Resize(960, 1080);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(root.Q("root").ClassListContains(HudPresenter.TwoRowClass), "precondition: two rows at 960x1080");
            foreach (var r in covered) Assert.AreEqual(DisplayStyle.None, root.Q(r).resolvedStyle.display, r + " is hidden");
            Reach(false, "narrowed with Why open");

            // Closed, then Why opened again in two rows.
            nav.Open(null);
            for (int i = 0; i < 3; i++) yield return null;
            Reach(true, "two rows, no screen");
            nav.Open("why");
            for (int i = 0; i < 3; i++) yield return null;
            Reach(false, "two rows, Why opened");

            // Widened again with Why open: one row, the column back.
            Resize(1920, 1080);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(root.Q("root").ClassListContains(HudPresenter.TwoRowClass), "precondition: one row again");
            Reach(true, "widened with Why open");
        }
        finally
        {
            for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
        }
    }
    static UiText Text => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Strings).text);

    /// <summary>The window sizes a player is likely to have: 16:9, 5:4, the playtest's wide-and-short browser window
    /// (1531x784, about 1.95) and 21:9.</summary>
    static readonly (int w, int h)[] Windows = { (1531, 784), (2560, 1080), (1920, 1080), (1280, 1024), (1920, 900) };

    /// <summary>The real Game.uxml on the real PanelSettings (and so the real theme), rendered at <paramref name="w"/> x
    /// <paramref name="h"/>, with the HUD filled from the opening of the family game.</summary>
    static IEnumerator Filled(int w, int h, string? screen, System.Action<VisualElement> check)
    {
        var rt = new RenderTexture(w, h, 0);
        var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
        ps.targetTexture = rt;
        var go = new GameObject("LayoutTest");
        go.SetActive(false);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = ps;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
        go.SetActive(true);
        var root = doc.rootVisualElement;
        var bridge = FamilyKit.Bridge();
        var hud = new HudPresenter(root, bridge, Text);
        var nav = new ScreenNav(root);
        nav.Open(screen);
        try
        {
            for (int i = 0; i < 20 && (float.IsNaN(root.Q("minimap").worldBound.width) || root.Q("year").worldBound.height <= 0); i++) yield return null;
            yield return null;   // one more pass, so text measured after the first layout settles
            check(root);
        }
        finally
        {
            nav.Dispose(); hud.Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
        }
    }

    static bool Inside(Rect inner, Rect outer) =>
        inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;

    /// <summary>Playtest 1, item 1: every top-bar label (the year, the date, the family's values) lies inside the bar,
    /// clear of the window's top edge, and nothing else starts above the bar's bottom edge.</summary>
    [UnityTest]
    public IEnumerator TheTopBarHoldsItsContentInWideAndShortWindows()
    {
        foreach (var (w, h) in Windows)
            yield return Filled(w, h, null, root =>
            {
                var bar = root.Q("top-bar").worldBound;
                Assert.GreaterOrEqual(bar.yMin, -0.5f, $"{w}x{h}: the top bar starts above the window");
                var labels = root.Q("top-bar").Query<Label>().ToList().Where(l => !string.IsNullOrEmpty(l.text)).ToList();
                Assert.Greater(labels.Count, 10, "the HUD filled the bar");
                // The date plate hangs below the one-row bar, as promo 03's does (HUD round 2, part 3): its lines stay in the plate.
                var plate = root.Q(className: "date-box").worldBound;
                Assert.IsTrue(Inside(plate, root.worldBound), $"{w}x{h}: the plate {plate} leaves the window");
                foreach (var l in labels)
                {
                    bool inPlate = l.GetFirstAncestorOfType<VisualElement>() != null && root.Q(className: "date-box").Contains(l);
                    Assert.IsTrue(Inside(l.worldBound, inPlate ? plate : bar), $"{w}x{h}: '{l.text}' {l.worldBound} is outside the {(inPlate ? "plate " + plate : "top bar " + bar)}");
                    Assert.GreaterOrEqual(l.worldBound.yMin, bar.yMin + 2f, $"{w}x{h}: '{l.text}' sits flush against the top edge");
                }
                foreach (var name in Screens.Concat(new[] { "event-card", "toast" }))
                    if (root.Q(name).resolvedStyle.display != DisplayStyle.None)
                        Assert.GreaterOrEqual(root.Q(name).worldBound.yMin, bar.yMax - 0.5f, $"{w}x{h}: {name} starts under the top bar");
            });
    }

    /// <summary>Playtest 1, item 3: the camera learns how much of the window the top bar covers, at every window shape,
    /// so the map is framed below it; disposing gives the whole screen back.</summary>
    [UnityTest]
    public IEnumerator TheCameraIsToldWhatTheTopBarCovers()
    {
        var camGo = new GameObject("InsetCamera");
        camGo.AddComponent<Camera>();
        var rig = camGo.AddComponent<StoryOfTheLand.Game.Map.CameraRig>();
        try
        {
            foreach (var (w, h) in Windows)
            {
                yield return Filled(w, h, null, root =>
                {
                    var inset = new TopBarInset(root, rig);
                    var bar = root.Q("top-bar").worldBound.height;
                    var screen = root.panel.visualTree.worldBound.height;
                    Assert.Greater(bar, 60f, $"{w}x{h}: the bar is laid out (70 px one-row since HUD round 2, part 3)");
                    Assert.AreEqual(bar / screen, rig.TopInset, 1e-3f, $"{w}x{h}: the inset is the bar's share of the window");
                    inset.Dispose();
                    Assert.AreEqual(0f, rig.TopInset, $"{w}x{h}: disposing gives the screen back");
                });
            }
        }
        finally { Object.DestroyImmediate(camGo); }
    }

    // Task 24, seen in headed Chrome: the top bar's labels were taller than the bar and, centred, ran off the top of the
    // screen at every size; a screen's list grew out of its screen over the top bar. Checked with every screen filled by
    // its presenter (an event open on the card), at 16:9 and at the wider viewports a browser window leaves.
    [UnityTest]
    public IEnumerator FilledScreensStayInsideTheirPanelsAndTheTopBarFits()
    {
        var text = Text;
        var played = FamilyKit.Bridge();
        FamilyKit.PlayMonth(played);                                    // a Why report to show
        // Bounded (Task 24 review M5): content that no longer raises an event fails the test instead of hanging the run.
        for (int t = 0; played.State.PendingEvents.Count == 0; t++)
        {
            if (t >= 30) Assert.Fail("no event was raised within 30 months");
            played.EndTurn();
        }
        var g = played.Session;
        var problems = new List<string>();
        foreach (var (w, h) in new[] { (1920, 1080), (1280, 720), (1920, 960), (1600, 757), (1366, 657), (390, 844), (960, 600) })   // #210: and narrow windows
        {
            var rt = new RenderTexture(w, h, 0);
            var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            var go = new GameObject("LayoutTest");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            var root = doc.rootVisualElement;
            var bridge = new SimBridge(g, text);
            var disposables = new List<System.IDisposable>
            {
                new HudPresenter(root, bridge, text), new EventCardPresenter(root, bridge, text),
                new WhyPresenter(root, bridge, text), new HowToPlayPresenter(root, text),
            };
            var nav = new ScreenNav(root);
            try
            {
                for (int i = 0; i < 20 && float.IsNaN(root.Q("minimap").worldBound.width); i++) yield return null;
                yield return null;
                Rect R(VisualElement e) => e.worldBound;
                bool Inside(Rect inner, Rect outer) => inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;
                var bar = root.Q("top-bar");
                var plate = root.Q(className: "date-box");   // hangs below the one-row bar, as promo 03's (HUD round 2, part 3)
                if (!Inside(R(plate), R(root))) problems.Add($"{w}x{h}: the plate {R(plate)} leaves the window");
                foreach (var l in bar.Query<VisualElement>().ToList().Where(e => e != bar && e.resolvedStyle.display == DisplayStyle.Flex))
                {
                    var holder = l == plate || plate.Contains(l) ? (l == plate ? bar : plate) : bar;
                    if (l == plate) continue;
                    if (!Inside(R(l), R(holder))) problems.Add($"{w}x{h}: top bar child {l.name}/{(l as Label)?.text ?? l.GetClasses().FirstOrDefault()} {R(l)} leaves the {(holder == plate ? "plate" : "bar")} {R(holder)}");
                }
                var card = root.Q("event-card");
                if (!Inside(R(card), R(root.Q("event-layer")))) problems.Add($"{w}x{h}: event card {R(card)} leaves the decision layer {R(root.Q("event-layer"))}");
                if (R(card).yMin < R(bar).yMax - 0.5f) problems.Add($"{w}x{h}: event card {R(card)} starts over the top bar {R(bar)}");
                foreach (var s in ScreenNav.Screens)
                {
                    nav.Open(s);
                    for (int i = 0; i < 3; i++) yield return null;
                    var screen = root.Q("screen-" + s);
                    foreach (var e in screen.Children().Concat(screen.Query<ScrollView>().ToList()).Where(e => e.resolvedStyle.display == DisplayStyle.Flex))
                        if (!Inside(R(e), R(screen))) problems.Add($"{w}x{h}: {s} child {e.name}/{e.GetClasses().FirstOrDefault()} {R(e)} leaves the screen {R(screen)}");
                }
                nav.Open(null);
            }
            finally
            {
                nav.Dispose(); foreach (var d in disposables) d.Dispose();
                Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    /// <summary>A button's text fits its content box: on one line, or wrapped (white-space normal) within its height.</summary>
    internal static bool TextFits(TextElement b)
    {
        var box = b.contentRect;
        var natural = b.MeasureTextSize(b.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
        if (natural.x <= box.width + 1f) return true;
        if (b.resolvedStyle.whiteSpace != WhiteSpace.Normal) return false;
        var wrapped = b.MeasureTextSize(b.text, box.width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
        return wrapped.y <= box.height + 1f;
    }

    /// <summary>The window shapes the HUD supports (PromoHudTests.EveryWindowShapeFitsTheBarAfterATurn: one row, the
    /// stacked bar, two rows), a phone upright (three rows), a small laptop, the playtest's wide-and-short browser windows
    /// and 21:9.</summary>
    static readonly (int w, int h)[] CardWindows =
    {
        (1920, 1080), (1280, 720), (1366, 768), (1760, 1000), (1920, 1200), (1440, 900), (1024, 768), (1280, 1024),
        (1024, 1024), (960, 1080), (800, 1000), (390, 844), (960, 600), (1531, 784), (1568, 752), (2560, 1080), (1920, 900), (1366, 657),
    };

    /// <summary>Playtest 2, P2-3: in a window at least this tall (logically; 1366x657) the body and the options of every
    /// event fit without scrolling; shorter windows may scroll the body as the fallback.</summary>
    const float ScrollFallbackHeight = 990f;

    /// <summary>Decision cards (2026-10-13), after Task 24 review I2: every real event of every chapter, the tallest
    /// included, with its content note, people and sources, fits the decision layer at every window shape, the narrow two-
    /// and three-row HUDs included: the card is centred (between the layer's paddings), at most 1040 px wide, below the top
    /// bar and inside the layer; its option buttons stay inside it with their text.</summary>
    [UnityTest]
    public IEnumerator EveryEventCardFitsTheDecisionLayer()
    {
        var problems = new List<string>();
        var figures = Figures.Parse(FamilyKit.Inputs.Read(Figures.FileName));
        var chapters = FamilyGame.Chapters.Select(x => FamilyKit.Chapter(x.Id)).ToList();
        foreach (var (w, h) in CardWindows)
        {
            var rt = new RenderTexture(w, h, 0);
            var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            var go = new GameObject("LayoutTest");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            var root = doc.rootVisualElement;
            var bridge = FamilyKit.Bridge();
            var hud = new HudPresenter(root, bridge, Text);   // the bar's tier (one, two or three rows) sets the layer's top
            // The card's people, as GameRoot gives them: a card that names figures adds their links.
            var card = new EventCardPresenter(root, bridge, Text, figuresFor: figures.For);
            float tallest = 0; string tallestId = "";
            try
            {
                for (int i = 0; i < 20 && float.IsNaN(root.Q("minimap").worldBound.width); i++) yield return null;
                foreach (var chapter in chapters)
                foreach (var e in chapter.Events)
                {
                    var g = GameSession.New(chapter, GoldenRun.Seed, GameRoot.AutomatedVillage);
                    g.State.Date = e.From;                       // the event's own month
                    g.State.PendingEvents.Clear(); g.State.PendingEvents.Add(e.Id);
                    bridge.Replace(g);
                    for (int i = 0; i < 4; i++) yield return null;
                    CheckCard($"{w}x{h}: {e.Id}", root, problems);
                    var c = root.Q("event-card").worldBound;
                    if (c.height > tallest) { tallest = c.height; tallestId = e.Id; }
                    foreach (var b in root.Q("event-options").Query<Button>().ToList())
                    {
                        if (b.worldBound.yMax > c.yMax + 0.5f || b.worldBound.yMin < c.yMin - 0.5f || b.worldBound.xMax > c.xMax + 0.5f || b.worldBound.xMin < c.xMin - 0.5f)
                            problems.Add($"{w}x{h}: {e.Id}: option '{b.text}' {b.worldBound} is cut off by the card {c}");
                        // Playtest 2, P2-2: the option's text lies inside its button (it wraps; it is never cut at the edge).
                        if (!TextFits(b))
                            problems.Add($"{w}x{h}: {e.Id}: option text '{b.text}' does not fit its button {b.contentRect}");
                    }
                    // Playtest 2, P2-3: in a window at least as tall (logically) as 1366x657, the body and the options fit
                    // without scrolling; shorter windows may scroll the body as the fallback.
                    var scroll = root.Q<ScrollView>("event-scroll");
                    var logicalHeight = root.panel.visualTree.layout.height;
                    if (logicalHeight >= ScrollFallbackHeight && scroll.contentContainer.layout.height > scroll.contentViewport.layout.height + 1f)
                        problems.Add($"{w}x{h} (logical height {logicalHeight:0}): {e.Id}: the body scrolls ({scroll.contentContainer.layout.height:0} px of text in {scroll.contentViewport.layout.height:0} px)");
                }
                Debug.Log($"I2 {w}x{h}: tallest card {tallestId} {tallest:0} px");
                Assert.Greater(tallest, 300f, $"{w}x{h}: the cards were laid out (a short window shrinks the tallest to fit)");
            }
            finally
            {
                card.Dispose(); hud.Dispose();
                Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    /// <summary>The decision card's place: in the decision layer, centred across it, at most CardMaxWidth logical px wide and
    /// below the top bar, Hide inside it.</summary>
    // Tolerances: whole pixels at the panel's scale (2/3 at 1280x720; about 2 logical px per pixel at 390x844).
    static void CheckCard(string where, VisualElement root, List<string> problems)
    {
        var card = root.Q("event-card"); var layer = root.Q("event-layer"); var bar = root.Q("top-bar");
        var c = card.worldBound; var l = layer.worldBound;
        if (!Inside(c, l)) problems.Add($"{where}: the card {c} leaves the decision layer {l}");
        // Centred between the layer's paddings: on a phone held upright the layer keeps the left rail's room free at its left
        // (the owner's ruling on #859: no rail button under the card), so the card centres right of the rail.
        float padL = layer.resolvedStyle.paddingLeft, padR = layer.resolvedStyle.paddingRight;
        if (Mathf.Abs((c.xMin - l.xMin - padL) - (l.xMax - padR - c.xMax)) > 1f) problems.Add($"{where}: the card {c} is not centred in the layer {l} (paddings {padL}, {padR})");
        if (c.width > CardMaxWidth + 1.5f) problems.Add($"{where}: the card is {c.width:0} px wide, over {CardMaxWidth}");
        if (c.yMin < bar.worldBound.yMax - 0.5f) problems.Add($"{where}: the card {c} starts over the top bar {bar.worldBound}");
        var hide = root.Q("event-hide").worldBound;
        if (!Inside(hide, c)) problems.Add($"{where}: Hide {hide} is cut off by the card {c}");
    }

    /// <summary>The card's largest width (StoryOfTheLand.uss .event-card), logical px.</summary>
    const float CardMaxWidth = 1040f;

    /// <summary>#765: a boot failure on the loading line wraps inside the screen with a side margin, in English and in
    /// Arabic, at a phone upright, a small window and 1280x720. The line is set the way GameRoot.OnBootFailed sets it:
    /// boot.failed with boot.reason_other, right to left in the Arabic face.</summary>
    [UnityTest]
    public IEnumerator TheBootFailureLineWrapsInsideTheScreen()
    {
        const float SideMargin = 16f;   // logical px the text keeps from each edge of the panel
        var problems = new List<string>();
        var ar = UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.StringsArabic).text, "strings.ar.yaml");
        foreach (var (w, h) in new[] { (390, 844), (960, 600), (1280, 720) })
            foreach (var (lang, text) in new[] { ("en", Text), ("ar", ar) })
            {
                var rt = new RenderTexture(w, h, 0);
                var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
                ps.targetTexture = rt;
                var go = new GameObject("LoadingLineTest");
                go.SetActive(false);
                var doc = go.AddComponent<UIDocument>();
                doc.panelSettings = ps;
                doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
                go.SetActive(true);
                try
                {
                    var root = doc.rootVisualElement;
                    bool arabic = lang == "ar";
                    root.Q("root").EnableInClassList(UiLanguage.ArabicClass, arabic);
                    var line = root.Q<Label>("loading");
                    line.text = text.Format("boot.failed", text["boot.reason_other"]);
                    line.languageDirection = arabic ? LanguageDirection.RTL : LanguageDirection.Inherit;   // as GameRoot.Say: this Arabic line starts with Arabic, so RTL
                    for (int i = 0; i < 20 && !(line.contentRect.width > 0); i++) yield return null;
                    for (int i = 0; i < 4; i++) yield return null;
                    var where = $"{w}x{h} {lang}";
                    var panel = root.panel.visualTree.worldBound;
                    var box = line.worldBound;
                    if (box.xMin < panel.xMin - 0.5f || box.xMax > panel.xMax + 0.5f) problems.Add($"{where}: the line {box} leaves the panel {panel}");
                    if (line.contentRect.width > panel.width - 2 * SideMargin + 0.5f)
                        problems.Add($"{where}: the text may run to the panel's edge (box {line.contentRect.width:0} of {panel.width:0} px)");
                    if (!TextFits(line)) problems.Add($"{where}: '{line.text}' does not fit its line {line.contentRect}");
                }
                finally { Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); }
            }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }
}
