#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Boot;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>F6, the tabbed Settings screen (SettingsPresenter): one tab's page at a time; each row applies at once and is kept
/// across a reload (display, audio, accessibility; the quality, blur, narration and language rows are in SettingsScreenTests
/// and ArabicUiTests); the Controls tab reads the real bindings; the language switches live with the pause menu; the screen is
/// mirrored in Arabic; Tab walks the tabs, then the open page; and every tab fits at 1280x720, 1920x1080 and 390x844 in both
/// languages.</summary>
public class SettingsTabsTests
{
    string dir = "";
    float volume;
    int level;
    float[] scales = new float[0];

    static UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset Tier(int i) =>
        (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)QualitySettings.GetRenderPipelineAssetAt(i);

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "sc-tabs-" + Guid.NewGuid().ToString("N"));
        volume = AudioListener.volume;
        level = QualitySettings.GetQualityLevel();
        scales = new float[QualitySettings.names.Length];
        for (int i = 0; i < scales.Length; i++) scales[i] = Tier(i).renderScale;
    }

    /// <summary>The rendering knobs at their own look (a new session).</summary>
    static void KnobsBack()
    {
        GraphicsQuality.Shadows = GraphicsQuality.ShadowChoice.Soft; GraphicsQuality.RenderScale = null;
        GraphicsQuality.Antialiasing = GraphicsQuality.AntialiasingChoice.Auto; GraphicsQuality.Detail = true; GraphicsQuality.Haze = true;
        GraphicsQuality.ReducedMotion = false;
    }

    [TearDown]
    public void TearDown()
    {
        SettingsPresenter.Reset();
        AudioListener.volume = volume; Narrator.Volume = 1f; Narrator.Muted = false;
        GraphicsQuality.SetFramePacing(true, 0);
        KnobsBack();
        // The editor's quality level and the URP assets are the project's: put back what a preset switched.
        GraphicsQuality.Apply(GraphicsQuality.Choice.Auto);
        QualitySettings.SetQualityLevel(level, true);
        for (int i = 0; i < scales.Length; i++) Tier(i).renderScale = scales[i];
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    /// <summary>The Settings presenter (and a HUD, the screens and their nav) on Game.uxml at a window's size.</summary>
    sealed class Rig : IDisposable
    {
        readonly GameObject go; readonly RenderTexture rt;
        public readonly PanelSettings Panel;
        public readonly VisualElement Root; public readonly ScreenNav Nav; public readonly SettingsPresenter Settings;
        readonly List<IDisposable> owned = new List<IDisposable>();

        public Rig(string dir, bool arabic = false, int w = 1920, int h = 1080)
        {
            rt = new RenderTexture(w, h, 0);
            Panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            Panel.targetTexture = rt;
            go = new GameObject("SettingsTabsTest"); go.SetActive(false);
            var doc = go.AddComponent<UIDocument>(); doc.panelSettings = Panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            Root = doc.rootVisualElement;
            Root.Q("root").RemoveFromClassList("loading");
            if (arabic) Root.Q("root").AddToClassList(UiLanguage.ArabicClass);
            var text = arabic ? FamilyKit.Ar : FamilyKit.En;
            var bridge = FamilyKit.Bridge(text);
            owned.Add(new HudPresenter(Root, bridge, text));
            owned.Add(Nav = new ScreenNav(Root));
            owned.Add(Settings = new SettingsPresenter(Root, text, new SettingsStore(dir), null, Panel));
            owned.Add(new CreditsPresenter(Root, Credits.Parse(FamilyKit.Inputs.Read(Credits.FileName)), text, Nav));
            if (arabic) owned.Add(new UiDirection(Root, bridge));
        }

        public Button B(string name) { var b = Root.Q<Button>(name); Assert.IsNotNull(b, name); return b; }
        public string Selected(string row) => Root.Query<Button>().ToList().Where(b => b.name.StartsWith("settings-" + row + "-") && b.ClassListContains(SettingsPresenter.SelectedClass)).Select(b => b.name).SingleOrDefault() ?? "";

        public void Dispose()
        {
            for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(Panel); rt.Release(); Object.DestroyImmediate(rt);
        }
    }

    static IEnumerator Frames(int n = 3) { for (int i = 0; i < n; i++) yield return null; }

    [Test]
    public void EachTabShowsItsOwnPageAndTheScreenOpensWhereItWasLeft()
    {
        using (var rig = new Rig(dir))
        {
            Assert.AreEqual("display", SettingsPresenter.Tab, "Display first");
            foreach (var tab in SettingsPresenter.Tabs)
            {
                UiTestKit.Press(rig.B("settings-tab-" + tab));
                foreach (var other in SettingsPresenter.Tabs)
                {
                    Assert.AreEqual(other == tab ? DisplayStyle.Flex : DisplayStyle.None, rig.Root.Q("settings-page-" + other).style.display.value, $"{tab}: page {other}");
                    Assert.AreEqual(other == tab, rig.B("settings-tab-" + other).ClassListContains(SettingsPresenter.TabSelectedClass), $"{tab}: tab {other} marked");
                }
            }
            UiTestKit.Press(rig.B("settings-tab-audio"));
        }
        using (var rig = new Rig(dir))   // the language switch builds the screen again: it opens on the same tab
        {
            Assert.AreEqual(DisplayStyle.Flex, rig.Root.Q("settings-page-audio").style.display.value);
            Assert.IsTrue(rig.Root.Q("settings-page-about").Contains(rig.Root.Q("settings-credits")), "audit m10: the credits row is shown once, in About");
        }
    }

    /// <summary>Each Display, Audio and Accessibility row takes effect at once, says what is set, and is kept in settings.txt,
    /// so a reload (the statics start over; only the file remains) applies it again.</summary>
    [Test]
    public void EachRowAppliesAtOnceAndIsKeptAcrossAReload()
    {
        var store = new SettingsStore(dir);
        Vector2Int reference;
        using (var rig = new Rig(dir))
        {
            reference = rig.Panel.referenceResolution;
            // Display: the window mode, vertical sync and the frame-rate limit (set by vsync while it is on).
            UiTestKit.Press(rig.B("settings-window-borderless"));
            Assert.AreEqual("settings-window-borderless", rig.Selected("window"));
            Assert.AreEqual("borderless", store.Read(SettingsPresenter.WindowKey));
            Assert.AreEqual(FullScreenMode.Windowed, SettingsPresenter.Mode("windowed"));
            Assert.IsTrue(GraphicsQuality.VSync, "vertical sync on by default");
            Assert.IsFalse(rig.B("settings-fps-60").enabledSelf, "the limit waits for vsync to be off");
            UiTestKit.Press(rig.B("settings-vsync"));
            Assert.IsFalse(GraphicsQuality.VSync, "off at once");
            Assert.AreEqual(FamilyKit.En["settings.off"], rig.B("settings-vsync").text, "the switch says what is set");
            Assert.AreEqual((1, -1), GraphicsQuality.FramePacing(true, 60), "vsync on leaves the rate to the display");
            UiTestKit.Press(rig.B("settings-fps-60"));
            Assert.AreEqual(60, GraphicsQuality.FrameCap);
            Assert.AreEqual((0, 60), GraphicsQuality.FramePacing(GraphicsQuality.VSync, GraphicsQuality.FrameCap), "60 frames a second at most");
            Assert.AreEqual(("off", "60"), (store.Read(SettingsPresenter.VSyncKey), store.Read(SettingsPresenter.FrameCapKey)));
            // Audio: the master and narration volumes step by ten.
            UiTestKit.Press(rig.B("settings-volume-prev")); UiTestKit.Press(rig.B("settings-volume-prev"));
            Assert.AreEqual(0.8f, AudioListener.volume, 1e-4f);
            Assert.AreEqual(FamilyKit.En.Format("settings.percent", 80), rig.Root.Q<Label>("settings-volume-value").text);
            Assert.IsTrue(rig.B("settings-volume-next").enabledSelf, "more is offered below 100%");
            UiTestKit.Press(rig.B("settings-narration_volume-prev"));
            Assert.AreEqual(0.9f, Narrator.Volume, 1e-4f);
            Assert.AreEqual(("80", "90"), (store.Read(SettingsPresenter.VolumeKey), store.Read(SettingsPresenter.NarrationVolumeKey)));
            // Accessibility: the text size scales the panel; high contrast marks the root.
            UiTestKit.Press(rig.B("settings-text_size-130"));
            Assert.AreEqual(Mathf.RoundToInt(reference.x / 1.3f), rig.Panel.referenceResolution.x, "130%: the panel's reference shrinks by 1.3");
            UiTestKit.Press(rig.B("settings-high_contrast"));
            Assert.IsTrue(rig.Root.Q("root").ClassListContains(SettingsPresenter.ContrastClass));
            Assert.AreEqual(("130", "on"), (store.Read(SettingsPresenter.TextSizeKey), store.Read(SettingsPresenter.ContrastKey)));
        }
        // The reload: every static back to its default, only the file remains.
        SettingsPresenter.Reset(); AudioListener.volume = 1f; Narrator.Volume = 1f; GraphicsQuality.SetFramePacing(true, 0);
        using (var rig = new Rig(dir))
        {
            Assert.AreEqual("settings-window-borderless", rig.Selected("window"));
            Assert.IsFalse(GraphicsQuality.VSync); Assert.AreEqual(60, GraphicsQuality.FrameCap);
            Assert.AreEqual(0.8f, AudioListener.volume, 1e-4f); Assert.AreEqual(0.9f, Narrator.Volume, 1e-4f);
            Assert.AreEqual(Mathf.RoundToInt(reference.x / 1.3f), rig.Panel.referenceResolution.x);
            Assert.IsTrue(rig.Root.Q("root").ClassListContains(SettingsPresenter.ContrastClass));
            Assert.AreEqual("settings-text_size-130", rig.Selected("text_size"));
        }
    }

    /// <summary>The Graphics tab drives the rendering side's knobs (GraphicsQuality): each applies at once and is kept; a
    /// preset sets them all (Medium: High's tier, hard shadows, a 0.9 render scale, the fast edge smoothing), a knob changed
    /// after it marks no preset, and a reload restores the preset and the knobs. Reduce motion is in Accessibility.</summary>
    [Test]
    public void TheGraphicsKnobsApplyAndThePresetsSetThem()
    {
        var store = new SettingsStore(dir);
        using (var rig = new Rig(dir))
        {
            Assert.AreEqual("settings-quality-auto", rig.Selected("quality"), "Automatic at its own look by default");
            UiTestKit.Press(rig.B("settings-quality-medium"));
            Assert.AreEqual(GraphicsQuality.Choice.High, GraphicsQuality.Current, "Medium is High's tier");
            Assert.AreEqual(GraphicsQuality.ShadowChoice.Hard, GraphicsQuality.Shadows);
            Assert.AreEqual(0.9f, GraphicsQuality.RenderScale!.Value, 1e-4f);
            Assert.AreEqual(GraphicsQuality.AntialiasingChoice.Fxaa, GraphicsQuality.Antialiasing);
            Assert.AreEqual(("settings-quality-medium", "settings-shadows-hard", "settings-render_scale-90", "settings-antialiasing-fxaa"),
                (rig.Selected("quality"), rig.Selected("shadows"), rig.Selected("render_scale"), rig.Selected("antialiasing")));
            Assert.AreEqual(("medium", "hard", "90", "fxaa"), (store.Read(SettingsPresenter.QualityKey), store.Read(SettingsPresenter.ShadowsKey),
                store.Read(SettingsPresenter.RenderScaleKey), store.Read(SettingsPresenter.AntialiasingKey)), "each kept");
            UiTestKit.Press(rig.B("settings-shadows-off"));
            Assert.AreEqual(GraphicsQuality.ShadowChoice.Off, GraphicsQuality.Shadows, "off at once");
            Assert.AreEqual("", rig.Selected("quality"), "a knob changed after the preset marks none");
            UiTestKit.Press(rig.B("settings-quality-high"));
            Assert.AreEqual((GraphicsQuality.ShadowChoice.Soft, (float?)null, GraphicsQuality.AntialiasingChoice.Auto),
                (GraphicsQuality.Shadows, GraphicsQuality.RenderScale, GraphicsQuality.Antialiasing), "High: every knob at the tier's own look");
            Assert.AreEqual("settings-render_scale-preset", rig.Selected("render_scale"));
            UiTestKit.Press(rig.B("settings-render_scale-70"));
            Assert.AreEqual(GraphicsQuality.MinRenderScale, GraphicsQuality.RenderScale!.Value, 1e-4f);
            UiTestKit.Press(rig.B("settings-antialiasing-smaa"));
            Assert.AreEqual(GraphicsQuality.AntialiasingChoice.Smaa, GraphicsQuality.Antialiasing);
            UiTestKit.Press(rig.B("settings-detail")); UiTestKit.Press(rig.B("settings-haze")); UiTestKit.Press(rig.B("settings-reduced_motion"));
            Assert.AreEqual((false, false, true), (GraphicsQuality.Detail, GraphicsQuality.Haze, GraphicsQuality.ReducedMotion));
            Assert.AreEqual(FamilyKit.En["settings.on"], rig.B("settings-reduced_motion").text);
        }
        KnobsBack(); GraphicsQuality.Apply(GraphicsQuality.Choice.Auto);   // the reload
        using (var rig = new Rig(dir))
        {
            Assert.AreEqual(GraphicsQuality.Choice.High, GraphicsQuality.Current);
            Assert.AreEqual(0.7f, GraphicsQuality.RenderScale!.Value, 1e-4f);
            Assert.AreEqual(GraphicsQuality.AntialiasingChoice.Smaa, GraphicsQuality.Antialiasing);
            Assert.AreEqual((false, false, true), (GraphicsQuality.Detail, GraphicsQuality.Haze, GraphicsQuality.ReducedMotion));
            Assert.AreEqual("", rig.Selected("quality"), "custom after the reload too");
        }
    }

    /// <summary>A display lists one resolution per refresh rate: each size is offered once, smallest first, with the window's
    /// own size among them.</summary>
    [Test]
    public void EachResolutionIsOfferedOnceSmallestFirst()
    {
        CollectionAssert.AreEqual(new[] { "1280x720", "1280x800", "1700x980", "1920x1080" },
            SettingsPresenter.Resolutions(new[] { (1920, 1080), (1280, 720), (1920, 1080), (1280, 800), (1280, 720) }, "1700x980"));
        CollectionAssert.AreEqual(new[] { "1280x720" }, SettingsPresenter.Resolutions(new (int, int)[0], "1280x720"));
        // The row offers the display's own list (Screen.resolutions, one per size) and starts on the window's current size.
        using var rig = new Rig(dir);
        var listed = Screen.resolutions.Select(r => $"{r.width}x{r.height}").Distinct().Count();
        var current = $"{Screen.width}x{Screen.height}";
        Assert.AreEqual(FamilyKit.En.Format("settings.resolution_value", Screen.width, Screen.height), rig.Root.Q<Label>("settings-resolution-value").text, "the current size is set");
        Assert.AreEqual(Screen.resolutions.Any(r => $"{r.width}x{r.height}" == current) ? listed : listed + 1,
            SettingsPresenter.Resolutions(Screen.resolutions.Select(r => (r.width, r.height)), current).Length);
    }

    /// <summary>The Controls tab reads the camera's real bindings (the Camera action map) and names the keys in the UI's
    /// language: letters as printed, arrows as printed in English and in words in Arabic, the mouse in words.</summary>
    [Test]
    public void TheControlsTabReadsTheRealBindings([Values(false, true)] bool arabic)
    {
        var text = arabic ? FamilyKit.Ar : FamilyKit.En;
        var controls = SettingsPresenter.Controls(text).ToDictionary(c => c.id, c => c.keys);
        CollectionAssert.IsSubsetOf(new[] { "pause", "end_turn", "pan", "drag", "zoom", "orbit", "narration", "scroll", "focus", "press" }, controls.Keys.ToList());
        // Arabic names the arrows in words: its fonts have no arrow glyphs (playtest: four empty boxes).
        Assert.AreEqual(arabic ? "W A S D · فوق يسار تحت يمين" : "W A S D · ↑ ← ↓ →", controls["pan"]);
        Assert.AreEqual(text["controls.key.right_button"] + " + " + text["controls.key.mouse_move"], controls["drag"]);
        Assert.AreEqual(text["controls.key.wheel"], controls["zoom"]);
        Assert.AreEqual("Q E", controls["orbit"]);
        StringAssert.StartsWith("Esc", controls["pause"]);
        using var rig = new Rig(dir, arabic);
        foreach (var (id, keys) in controls)
        {
            Assert.AreEqual(text["controls." + id], rig.Root.Q("settings-control-" + id).Q<Label>(className: "settings-name").text);
            Assert.AreEqual(keys, rig.Root.Q<Label>("settings-control-" + id + "-keys").text);
        }
    }

    /// <summary>In the game, the language switch puts every screen in the new language at once, the pause menu with them:
    /// Settings stays open on its Language tab, and closing it brings back the pause menu it was opened from, in Arabic.</summary>
    [Test]
    public void TheLanguageSwitchesLiveWithThePauseMenu()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(FamilyKit.Inputs.Map);
        var game = Object.FindFirstObjectByType<GameRoot>();
        game.Store = new SaveStore(dir);
        game.Settings = new SettingsStore(Path.Combine(dir, "settings"));
        game.Begin(FamilyKit.Inputs, 0);
        var ui = Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
        game.Pause!.Open();
        UiTestKit.Press(ui.Q<Button>("pause-settings"));
        UiTestKit.Press(ui.Q<Button>("settings-tab-language"));
        UiTestKit.Press(ui.Q<Button>("settings-language-ar"));
        Assert.IsTrue(ui.Q("root").ClassListContains(UiLanguage.ArabicClass), "Arabic at once");
        Assert.IsTrue(ScreenNav.IsOpen(ui.Q("screen-settings")), "Settings stays open");
        Assert.AreEqual(DisplayStyle.Flex, ui.Q("settings-page-language").style.display.value, "on its Language tab");
        foreach (var tab in SettingsPresenter.Tabs) Assert.AreEqual(FamilyKit.Ar["settings.tab." + tab], ui.Q<Button>("settings-tab-" + tab).text);
        UiTestKit.Press(ui.Q<Button>("screen-close-settings"));
        Assert.IsTrue(game.Pause!.IsOpen, "the pause menu it came from is back");
        Assert.AreEqual(FamilyKit.Ar["pause.title"], ui.Q<Label>("pause-title").text);
        Assert.AreEqual(FamilyKit.Ar["pause.resume"], ui.Q<Button>("pause-resume").text);
    }

    /// <summary>In Arabic every word on every tab is Arabic: the tabs, each row's name, the switches and the choices, the
    /// notes. Only what is printed on a key or names a language in itself keeps its Latin letters ("English", W, Esc).</summary>
    [Test]
    public void InArabicEveryWordIsArabic()
    {
        using var rig = new Rig(dir, arabic: true);
        var latin = new System.Text.RegularExpressions.Regex("[A-Za-z]");
        var words = rig.Root.Q("screen-settings").Query<TextElement>().ToList()
            .Where(t => !string.IsNullOrEmpty(t.text) && !t.ClassListContains("settings-keys") && t.name != "settings-language-en" && t.name != "screen-close-settings").ToList();
        Assert.Greater(words.Count, 30, "precondition: every tab's rows");
        foreach (var t in words) Assert.IsFalse(latin.IsMatch(t.text), $"'{t.name}': Latin letters in '{t.text}'");
    }

    /// <summary>Tab walks the tabs in order, then the open page's controls, never a hidden page's.</summary>
    [UnityTest]
    public IEnumerator TabWalksTheTabsThenTheOpenPage()
    {
        using var rig = new Rig(dir);
        rig.Nav.Open("settings");
        rig.Settings.ShowTab("audio");
        yield return Frames();
        var first = rig.B("settings-tab-display");
        first.Focus();
        yield return Frames();
        var seen = new List<string>();
        for (int i = 0; i < 12; i++)
        {
            var at = (VisualElement)rig.Root.panel.focusController.focusedElement;
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next)) { e.target = at; at.SendEvent(e); }
            yield return null;
            seen.Add((rig.Root.panel.focusController.focusedElement as VisualElement)?.name ?? "none");
        }
        int others = SettingsPresenter.Tabs.Length - 1;
        CollectionAssert.AreEqual(SettingsPresenter.Tabs.Skip(1).Select(t => "settings-tab-" + t), seen.Take(others), "the tabs in order: " + string.Join(", ", seen));
        CollectionAssert.AreEqual(new[] { "settings-volume-prev", "settings-narration", "settings-narration_volume-prev" }, seen.Skip(others).Take(3),
            "then the open page, in its order (a stepper at 100% offers only less): " + string.Join(", ", seen));
    }

    /// <summary>Arabic mirrors the screen: the tabs run from the right, and each row's name is at the right of its choices.</summary>
    [UnityTest]
    public IEnumerator TheScreenIsMirroredInArabic()
    {
        using var rig = new Rig(dir, arabic: true);
        rig.Nav.Open("settings");
        rig.Settings.ShowTab("display");
        yield return Frames(4);
        Assert.Greater(rig.B("settings-tab-display").worldBound.center.x, rig.B("settings-tab-graphics").worldBound.center.x, "the first tab at the right");
        var label = rig.Root.Q<Label>("settings-window-label").worldBound;
        Assert.Greater(label.center.x, rig.B("settings-window-windowed").worldBound.xMax, "the row's name at the right of its choices");
        Assert.AreEqual(LanguageDirection.RTL, rig.Root.Q<Label>("settings-window-label").languageDirection);
    }

    /// <summary>At 1280x720, 1920x1080 and a phone held upright (390x844), in English and Arabic, every tab's rows lie inside
    /// the screen's width, below the top bar, every text whole; the tabs and the credits row inside the screen.</summary>
    /// <summary>Playtest of the web build: a short tab (Language; Display on the web) left a large blank panel under its rows.
    /// The screen now ends just under its last row and the credits row, and a long tab still fills the room.</summary>
    [UnityTest]
    public IEnumerator AShortTabLeavesNoBlankPanelAndALongOneStillFillsTheRoom()
    {
        foreach (var arabic in new[] { false, true })
        {
            using var rig = new Rig(dir, arabic);
            rig.Nav.Open("settings");
            var screen = rig.Root.Q("screen-settings");
            rig.Settings.ShowTab("language");
            yield return Frames(4);
            var last = rig.Root.Q("settings-page-language").Children().Last().worldBound;   // the credits row is in About now (audit m10)
            float shortHeight = screen.worldBound.height;
            Assert.Less(screen.worldBound.yMax - last.yMax, 48f, $"{(arabic ? "ar" : "en")}: blank panel under the Language tab");
            Assert.AreEqual(SettingsPresenter.ContentHeight(screen, rig.Root.Q<ScrollView>("settings-body")), shortHeight, 2f, "held to its content");
            rig.Settings.ShowTab("controls");
            yield return Frames(4);
            Assert.Greater(screen.worldBound.height, shortHeight + 100f, $"{(arabic ? "ar" : "en")}: the Controls tab is taller than Language");
        }
    }

    /// <summary>Audit M2, m11: at 130% text at 1280x720, in English and Arabic, Settings keeps a reading width (it starts by the
    /// left column below the one-row bar), no row's name is squeezed to a letter a line, its choices wrap instead of being
    /// clipped, no horizontal scroller shows; and an On switch carries the selected gold fill, Off does not.</summary>
    [UnityTest]
    public IEnumerator At130PercentEveryTabStillReads()
    {
        var problems = new List<string>();
        foreach (var arabic in new[] { false, true })
        {
            using var rig = new Rig(dir, arabic, 1280, 720);
            UiTestKit.Press(rig.B("settings-text_size-130"));
            rig.Nav.Open("settings");
            foreach (var tab in SettingsPresenter.Tabs)
            {
                rig.Settings.ShowTab(tab);
                yield return Frames(5);
                var at = $"{(arabic ? "ar" : "en")} 130% {tab}";
                var screen = rig.Root.Q("screen-settings").worldBound;
                if (screen.width < 600f) problems.Add($"{at}: the screen is {screen.width:0} px wide");
                if (!Inside(screen, rig.Root.worldBound)) problems.Add($"{at}: the screen {screen} leaves the window");
                var body = rig.Root.Q<ScrollView>("settings-body");
                if (body.horizontalScroller.resolvedStyle.display != DisplayStyle.None && body.horizontalScroller.worldBound.height > 0)
                    problems.Add($"{at}: a horizontal scroller shows");
                foreach (var name in rig.Root.Q("settings-page-" + tab).Query<Label>(className: "settings-name").ToList())
                    if (name.worldBound.width < 100f) problems.Add($"{at}: '{name.text}' is squeezed to {name.worldBound.width:0} px");
                foreach (var t in rig.Root.Q("settings-page-" + tab).Query<TextElement>().ToList().Where(t => !string.IsNullOrEmpty(t.text)))
                {
                    if (t.worldBound.xMin < screen.xMin - 0.5f || t.worldBound.xMax > screen.xMax + 0.5f) problems.Add($"{at}: '{t.text}' {t.worldBound} leaves {screen}");
                    if (!UiLayoutTests.TextFits(t)) problems.Add($"{at}: '{t.text}' is cut");
                }
            }
            Assert.IsTrue(rig.B("settings-tiltshift").ClassListContains(SettingsPresenter.SelectedClass), "On: the selected fill");
            UiTestKit.Press(rig.B("settings-tiltshift"));
            Assert.IsFalse(rig.B("settings-tiltshift").ClassListContains(SettingsPresenter.SelectedClass), "Off: plain");
            UiTestKit.Press(rig.B("settings-tiltshift"));
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [UnityTest]
    public IEnumerator EveryTabFitsTheWindowInBothLanguages()
    {
        var problems = new List<string>();
        foreach (var arabic in new[] { false, true })
            foreach (var (w, h) in new[] { (1280, 720), (1920, 1080), (390, 844) })
            {
                using var rig = new Rig(dir, arabic, w, h);
                rig.Nav.Open("settings");
                foreach (var tab in SettingsPresenter.Tabs)
                {
                    rig.Settings.ShowTab(tab);
                    yield return Frames(4);
                    var at = $"{(arabic ? "ar" : "en")} {w}x{h} {tab}";
                    var screen = rig.Root.Q("screen-settings").worldBound;
                    if (!Inside(screen, rig.Root.worldBound)) problems.Add($"{at}: the screen {screen} leaves the window");
                    if (screen.yMin < rig.Root.Q("top-bar").worldBound.yMax - 0.5f) problems.Add($"{at}: the screen starts in the top bar");
                    foreach (var name in new[] { "settings-tabs", "settings-body" })
                        if (!Inside(rig.Root.Q(name).worldBound, screen)) problems.Add($"{at}: {name} {rig.Root.Q(name).worldBound} leaves the screen {screen}");
                    var page = rig.Root.Q("settings-page-" + tab);
                    foreach (var t in page.Query<TextElement>().ToList().Where(t => !string.IsNullOrEmpty(t.text)))
                    {
                        var r = t.worldBound;
                        if (r.xMin < screen.xMin - 0.5f || r.xMax > screen.xMax + 0.5f) problems.Add($"{at}: '{t.text}' {r} leaves the screen's width {screen}");
                        if (!UiLayoutTests.TextFits(t)) problems.Add($"{at}: '{t.text}' is cut ({t.contentRect})");
                    }
                    foreach (var b in rig.Root.Q("settings-tabs").Query<Button>().ToList())
                        if (!UiLayoutTests.TextFits(b)) problems.Add($"{at}: the tab '{b.text}' is cut");
                }
            }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    static bool Inside(Rect inner, Rect outer) =>
        inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;
}
