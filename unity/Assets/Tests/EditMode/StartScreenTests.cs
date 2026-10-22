#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Boot;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>The start screen (StartScreenPresenter): the quoted lines are word for word the official texts, Continue only
/// with a family save, New Game opening the picker (through its two presses when there is a save), Escape, automated runs
/// skipping it, its layout at the four window sizes it must fit, and the Arabic laid out right to left.</summary>
public class StartScreenTests
{
    static UiText Text => FamilyKit.En;
    static string Source(string name) => File.ReadAllText(Path.Combine(Paths.RepoRoot, "docs", "sources", "unga-194", name));

    readonly List<string> temp = new List<string>();
    SaveStore TempStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "storyoftheland-start-" + Guid.NewGuid().ToString("N"));
        temp.Add(dir);
        return new SaveStore(dir);
    }

    [TearDown]
    public void Clean()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // after the scene tests
        foreach (var d in temp) { try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch (Exception) { } }
        temp.Clear();
    }

    // --- The text ---------------------------------------------------------------------------------------------------

    /// <summary>The quoted lines exactly as Game.uxml shows them, checked twice: against these literals (a change shows in
    /// review) and as verbatim passages of the sources in docs/sources/unga-194 (paragraph 11 of resolution 194 (III): the
    /// United Nations' official Arabic translation, the English as adopted). A leading or trailing ellipsis marks a passage
    /// cut from a longer sentence. The date lines under them are attribution, not quotation, and are not listed.</summary>
    static readonly (string name, string text, string source)[] Quoted =
    {
        ("start-ar-proclamation", "تقرر وجوب السماح بالعودة في أقرب وقت ممكن للاجئين الراغبين في العودة إلى ديارهم والعيش بسلام مع جيرانهم…", "ar.txt"),
        ("start-en-proclamation", "Resolves that the refugees wishing to return to their homes and live at peace with their neighbours should be permitted to do so at the earliest practicable date…", "en.txt"),
    };

    [Test]
    public void TheQuotedLinesAreWordForWordTheOfficialTexts()
    {
        using var ui = new UiTestKit();
        foreach (var (name, text, source) in Quoted)
        {
            var shown = ui.Q<Label>(name).text;
            Assert.AreEqual(text, shown, name);
            var passage = shown.Trim('…');
            StringAssert.Contains(passage, Source(source), $"{name} is not a verbatim passage of docs/sources/unga-194/{source}");
            Assert.IsFalse(Regex.IsMatch(shown, "[\u200E\u200F\u202A-\u202E\u2066-\u2069]"), $"{name}: no direction marks; the text generator orders it");
        }
        StringAssert.Contains("194 (III)", ui.Q<Label>("start-en-date").text);
        StringAssert.Contains("194", ui.Q<Label>("start-ar-date").text);
        StringAssert.Contains("resolution 194 (III)", ui.Q<Label>("start-source").text);
    }

    [UnityTest]
    public IEnumerator TheArabicIsSetInNotoNaskhArabicThroughTheAdvancedTextGenerator()
    {
        using var ui = new RenderedUi();
        ui.Root.Q("root").AddToClassList(StartScreenPresenter.OpenClass);
        for (int i = 0; i < 3; i++) yield return null;                 // resolved styles need a style pass
        var font = AssetDatabase.LoadAssetAtPath<Font>(UiPaths.FontArabic);
        Assert.IsNotNull(font, UiPaths.FontArabic);
        foreach (var name in new[] { "start-title", "start-ar-proclamation", "start-ar-date" })
        {
            var label = ui.Q<Label>(name);
            Assert.AreEqual("Advanced", label.resolvedStyle.unityTextGenerator.ToString(), name);
            Assert.AreEqual(LanguageDirection.RTL, label.languageDirection, $"{name}: a right-to-left paragraph (its full stop goes on the left)");
            var fontAsset = label.resolvedStyle.unityFontDefinition.fontAsset;
            var resolved = fontAsset != null ? fontAsset.sourceFontFile : label.resolvedStyle.unityFontDefinition.font;
            Assert.AreSame(font, resolved, name);
            foreach (var c in label.text.Where(c => c > 0x7f))
                Assert.IsTrue(font.HasCharacter(c), $"{name}: Noto Naskh Arabic has no glyph for U+{(int)c:X4}");
        }
    }

    /// <summary>The advanced text generator is on for the project (Project Settings, UI Toolkit, Enable Advanced Text) and
    /// the panel settings carry its ICU data, so a player build lays the Arabic out right to left (UiAssets.Create).</summary>
    [Test]
    public void AdvancedTextIsOnAndThePanelCarriesItsIcuData()
    {
        var settings = File.ReadAllText(Path.Combine(Paths.RepoRoot, "unity", "ProjectSettings", "UIToolkitProjectSettings.asset"));
        StringAssert.Contains("m_EnableAdvancedText: 1", settings);
        var icu = new SerializedObject(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel)).FindProperty("m_ICUDataAsset");
        Assert.IsNotNull(icu);
        Assert.IsNotNull(icu.objectReferenceValue, "StoryOfTheLandPanel.asset has no ICU data (run UiAssets.Create)");
        StringAssert.Contains("m_ICUDataAsset: {fileID: 20204, guid: 0000000000000000f000000000000000, type: 0}",
            File.ReadAllText(Path.Combine(Paths.RepoRoot, "unity", UiPaths.Panel)), "saved in the asset, not only assigned on load");
    }

    // --- Automation ---------------------------------------------------------------------------------------------------

    [Test]
    public void AutomatedRunsSkipTheScreen()
    {
        var none = new string[0];
        foreach (var q in new[] { "?golden", "?golden&smoke=5", "?smoke", "?smoke=5", "?golden&shot", "?shot", "?vprofile", "?profile",
                     "?borderproof", "?nostart", "?bootlog&golden" })
            Assert.IsTrue(StartScreenPresenter.Skipped("http://localhost:8000/index.html" + q, none, webgl: true), q);
        // "units" is no skip flag.
        foreach (var q in new[] { "", "?bootlog", "?navlog", "?camlog", "?smoke=0", "?goldenx", "?nostartx", "?shots", "?units" })
            Assert.IsFalse(StartScreenPresenter.Skipped("http://localhost:8000/index.html" + q, none, webgl: true), q);
        foreach (var args in new[] { new[] { "-golden" }, new[] { "-smoke", "5" }, new[] { "-smoke" }, new[] { "-shot" }, new[] { "-vprofile" },
                     new[] { "-profile" }, new[] { "-" + BorderProof.Flag }, new[] { "-nostart" } })
            Assert.IsTrue(StartScreenPresenter.Skipped(null, args, webgl: false), string.Join(" ", args));
        Assert.IsFalse(StartScreenPresenter.Skipped(null, new[] { "-batchmode" }, webgl: false));
        Assert.IsFalse(StartScreenPresenter.Skipped(null, new[] { "-units" }, webgl: false));
        Assert.IsFalse(StartScreenPresenter.Skipped(null, new[] { "-smoke", "0" }, webgl: false));
        // A flag on the desktop does not count on the web and the other way round (each instrument's own rule).
        Assert.IsFalse(StartScreenPresenter.Skipped("http://localhost/index.html", new[] { "-golden" }, webgl: true));
        Assert.IsFalse(StartScreenPresenter.Skipped("http://localhost/index.html?golden", none, webgl: false));
    }

    // --- Behaviour ------------------------------------------------------------------------------------------------------

    /// <summary>Game.uxml on a panel that renders into a 1920x1080 texture, so layout, style and focus run as in a player
    /// once frames pass (UiTestKit's panel has no target and is not laid out in edit mode).</summary>
    sealed class RenderedUi : IDisposable
    {
        readonly GameObject go; readonly PanelSettings ps; readonly RenderTexture rt;
        public readonly UIDocument Doc;
        public VisualElement Root => Doc.rootVisualElement;

        public RenderedUi(int w = 1920, int h = 1080)
        {
            rt = new RenderTexture(w, h, 0);
            ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            go = new GameObject("StartScreenTest");
            go.SetActive(false);
            Doc = go.AddComponent<UIDocument>();
            Doc.panelSettings = ps;
            Doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
        }

        public T Q<T>(string name) where T : VisualElement
        {
            var e = Root.Q<T>(name);
            Assert.IsNotNull(e, name);
            return e;
        }

        public void Press(string name) => UiTestKit.Press(Q<Button>(name));

        public void Dispose() { Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); }
    }

    /// <summary>The session GameRoot starts behind the screen: the family autosave in <paramref name="store"/> (the family
    /// store), else the default family game.</summary>
    static GameSession Started(SaveStore store, List<string> notes, out bool resumed)
    {
        var continued = GameStart.ContinueFamily(FamilyKit.Inputs.Chapter, store, notes, Text);
        resumed = continued != null;
        return continued ?? FamilyKit.Session(seed: 4);
    }

    /// <summary>The start the way GameRoot makes it: the family autosave continued (or the default family game), the
    /// toast, New Game and the screen. The picker is a counter: New Game's presses that would open it.</summary>
    sealed class Rig : IDisposable
    {
        public readonly RenderedUi Ui = new RenderedUi();
        public readonly SimBridge Bridge;
        public readonly ToastPresenter Toast;
        public readonly NewGamePresenter NewGame;
        public readonly StartScreenPresenter Start;
        public readonly List<bool> Shown = new List<bool>();
        public readonly List<string> Notes = new List<string>();
        public int Picks;
        readonly IDisposable autosave;

        public Rig(SaveStore store)
        {
            Bridge = new SimBridge(Started(store, Notes, out var resumed), Text);
            Toast = new ToastPresenter(Ui.Root, Bridge, Text);
            NewGame = new NewGamePresenter(Ui.Root, Bridge, Toast, Text, () => Picks++);
            autosave = GameStart.Autosave(Bridge, store, Text);
            Ui.Root.Q("root").RemoveFromClassList("loading");
            Start = new StartScreenPresenter(Ui.Root, NewGame, resumed, () => Picks++, Shown.Add);
            Start.Show();
        }

        public bool ContinueShown => Ui.Q<Button>("start-continue").style.display.value == DisplayStyle.Flex;
        public void Dispose() { Start.Dispose(); autosave.Dispose(); NewGame.Dispose(); Toast.Dispose(); Ui.Dispose(); }
    }

    /// <summary>Writes a family autosave (the chapter line, then the sim's save) of the default game after
    /// <paramref name="turns"/> months, each card answered with its first choice.</summary>
    static SaveStore WithSave(SaveStore store, int turns)
    {
        var played = FamilyKit.Bridge(seed: 9);
        for (int i = 0; i < turns; i++) FamilyKit.PlayMonth(played);
        store.WriteAutosave(GameStart.SaveText(played.Session));
        return store;
    }

    [Test]
    public void WithoutASaveThereIsNoContinueAndNewGameOpensThePickerAtOnce()
    {
        var store = TempStore();
        using var rig = new Rig(store);
        Assert.IsTrue(rig.Start.IsOpen);
        Assert.IsFalse(rig.Start.Resumed);
        Assert.IsFalse(rig.ContinueShown, "no save: no Continue");
        var session = rig.Bridge.Session;
        rig.Ui.Press("start-continue");                                  // hidden, and ignored even if pressed
        Assert.IsTrue(rig.Start.IsOpen);
        Assert.AreEqual(0, rig.Picks);
        rig.Ui.Press("start-new-game");
        Assert.AreEqual(1, rig.Picks, "one press: there is nothing to lose, the picker opens");
        Assert.IsFalse(rig.NewGame.Armed, "no confirmation was asked for");
        Assert.IsTrue(rig.Start.IsOpen, "the screen stays up under the picker until a village is picked");
        Assert.AreSame(session, rig.Bridge.Session, "nothing starts before a village is picked");
        Assert.AreEqual(new[] { true }, rig.Shown.ToArray(), "the camera keys stay stopped");
        Assert.IsFalse(Directory.Exists(store.Directory) && Directory.GetFiles(store.Directory).Length > 0);
    }

    [Test]
    public void ContinueShowsOnlyWithASaveAndLoadsIt()
    {
        var store = WithSave(TempStore(), 3);
        var saved = File.ReadAllText(store.AutosavePath);
        using var rig = new Rig(store);
        Assert.IsTrue(rig.Start.Resumed);
        Assert.IsTrue(rig.ContinueShown);
        Assert.AreEqual(3, rig.Bridge.State.Turn, "the session under the screen is the save, replayed");
        rig.Ui.Press("start-continue");
        Assert.IsFalse(rig.Start.IsOpen);
        Assert.AreEqual(3, rig.Bridge.State.Turn);
        Assert.AreEqual(saved, File.ReadAllText(store.AutosavePath), "Continue never touches the save");
        rig.Bridge.EndTurn();
        Assert.AreEqual(4, rig.Bridge.State.Turn, "and play goes on from it");
    }

    [Test]
    public void AnUnreadableSaveOffersNoContinue()
    {
        var store = TempStore();
        store.WriteAutosave("not a save\n");
        LogAssert.Expect(LogType.Warning, new Regex("Family autosave could not be loaded"));
        using var rig = new Rig(store);
        Assert.IsFalse(rig.Start.Resumed);
        Assert.IsFalse(rig.ContinueShown, "the save was set aside: there is nothing to continue");
        Assert.AreEqual(1, rig.Notes.Count, "the player is told");
        Assert.IsNull(store.ReadAutosave(), "set aside");
    }

    /// <summary>With a save, New Game asks first; the second press opens the picker, and the save stays until a village
    /// is picked (GameRoot.StartFamily replaces it).</summary>
    [Test]
    public void NewGameOverASaveGoesThroughTheConfirmation()
    {
        var store = WithSave(TempStore(), 2);
        var saved = File.ReadAllText(store.AutosavePath);
        using var rig = new Rig(store);
        rig.Ui.Press("start-new-game");
        Assert.IsTrue(rig.Start.IsOpen, "the first press only asks");
        Assert.IsTrue(rig.NewGame.Armed);
        Assert.AreEqual(0, rig.Picks, "no picker yet");
        Assert.AreEqual(Text["newgame.confirm"], rig.Ui.Q<Label>("toast").text);
        Assert.AreEqual(DisplayStyle.Flex, rig.Ui.Q<Label>("toast").style.display.value);
        Assert.IsTrue(File.Exists(store.AutosavePath), "one press never wipes the save");
        Assert.AreEqual(2, rig.Bridge.State.Turn);
        rig.Ui.Press("start-new-game");
        Assert.AreEqual(1, rig.Picks, "the second press opens the picker");
        Assert.IsFalse(rig.NewGame.Armed);
        Assert.AreEqual(DisplayStyle.None, rig.Ui.Q<Label>("toast").style.display.value, "the prompt is taken down");
        Assert.IsTrue(rig.Start.IsOpen, "the screen stays up under the picker");
        Assert.AreEqual(2, rig.Bridge.State.Turn, "nothing starts before a village is picked");
        Assert.AreEqual(saved, File.ReadAllText(store.AutosavePath), "the save is kept until then");
    }

    [Test]
    public void EscapeAndContinueWithdrawTheRequest()
    {
        var store = WithSave(TempStore(), 2);
        using var rig = new Rig(store);
        rig.Ui.Press("start-new-game");
        Assert.IsTrue(rig.NewGame.Armed);
        using (var e = NavigationCancelEvent.GetPooled()) { e.target = rig.Ui.Q<Button>("start-new-game"); rig.Ui.Q<Button>("start-new-game").SendEvent(e); }
        Assert.IsFalse(rig.NewGame.Armed, "Escape withdraws it");
        Assert.AreEqual(DisplayStyle.None, rig.Ui.Q<Label>("toast").style.display.value, "and takes the prompt down");
        Assert.IsTrue(rig.Start.IsOpen, "Escape does not leave the screen");
        Assert.IsFalse(rig.Start.Escape(), "nothing left to withdraw");
        rig.Ui.Press("start-new-game");
        Assert.IsTrue(rig.NewGame.Armed);
        rig.Ui.Press("start-continue");
        Assert.IsFalse(rig.Start.IsOpen);
        Assert.IsFalse(rig.NewGame.Armed, "Continue withdraws it: the top bar's New Game starts again from one press");
        Assert.AreEqual(DisplayStyle.None, rig.Ui.Q<Label>("toast").style.display.value);
        rig.Ui.Press("new-game");
        Assert.AreEqual(2, rig.Bridge.State.Turn, "a single press on the top bar only asks");
        Assert.IsTrue(File.Exists(store.AutosavePath));
    }

    [UnityTest]
    public IEnumerator WhileItShowsTheHudIsHiddenAndOnlyItsButtonsTakeFocus()
    {
        var store = WithSave(TempStore(), 1);
        using var rig = new Rig(store);
        yield return null; yield return null;
        foreach (var hud in new[] { "top-bar", "left-column", "right-column", "map-labels" })
            Assert.AreEqual(DisplayStyle.None, rig.Ui.Root.Q(hud).resolvedStyle.display, hud);
        foreach (var b in new[] { "end-turn", "new-game", "nav-why", "nav-how-to-play", "minimap-overview" })
        {
            Assert.IsFalse(rig.Ui.Q<Button>(b).canGrabFocus, $"{b} is out of reach under the start screen");
            rig.Ui.Q<Button>(b).Focus();
            Assert.AreNotSame(rig.Ui.Q<Button>(b), rig.Ui.Root.focusController?.focusedElement, $"{b} cannot be focused");
        }
        var cont = rig.Ui.Q<Button>("start-continue");
        cont.Focus();
        Assert.AreSame(cont, rig.Ui.Root.focusController?.focusedElement, "Continue has the focus");
        // Tab walks the screen's buttons only: Continue and New Game.
        var seen = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            var at = (VisualElement)rig.Ui.Root.focusController!.focusedElement;
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next)) { e.target = at; at.SendEvent(e); }
            yield return null;
            seen.Add((rig.Ui.Root.focusController.focusedElement as VisualElement)?.name ?? "none");
        }
        Assert.AreEqual("start-new-game", seen[0], "Tab moves from Continue to New Game: " + string.Join(", ", seen));
        Assert.IsTrue(seen.All(n => n == "start-continue" || n == "start-new-game"), "Tab stays on the screen's buttons: " + string.Join(", ", seen));
        rig.Ui.Press("start-continue");
        yield return null; yield return null;
        Assert.AreEqual(DisplayStyle.Flex, rig.Ui.Root.Q("top-bar").resolvedStyle.display);
        Assert.AreEqual(DisplayStyle.None, rig.Ui.Root.Q("start-screen").resolvedStyle.display);
        Assert.IsTrue(rig.Ui.Q<Button>("end-turn").canGrabFocus, "the HUD is back");
    }

    /// <summary>#41 M1: closed from the keyboard (Continue: New Game leaves it up under the picker), the screen gives the
    /// focus to End Turn rather than to nothing, so the next Tab or Enter goes on from the top bar.</summary>
    [UnityTest]
    public IEnumerator ClosingItFromTheKeyboardFocusesEndTurn()
    {
        using var rig = new Rig(WithSave(TempStore(), 1));
        yield return null; yield return null;
        const string name = "start-continue";
        var button = rig.Ui.Q<Button>(name);
        button.Focus();
        yield return null;
        Assert.AreSame(button, rig.Ui.Root.focusController?.focusedElement, name + ": precondition, focused");
        rig.Ui.Press(name);
        yield return null; yield return null; yield return null;
        Assert.IsFalse(rig.Start.IsOpen, name + ": closed");
        Assert.AreSame(rig.Ui.Q<Button>("end-turn"), rig.Ui.Root.focusController?.focusedElement, name + ": End Turn has the focus");
    }

    /// <summary>#41 M1: closed with the mouse, the screen hands no focus on (PointerBlur's rule: a click leaves no focus
    /// fill behind), so End Turn is not lit.</summary>
    [UnityTest]
    public IEnumerator ClosingItWithThePointerFocusesNothing()
    {
        using var rig = new Rig(WithSave(TempStore(), 1));
        yield return null; yield return null;
        PressWithPointer(rig.Ui.Q<Button>("start-continue"));
        yield return null; yield return null; yield return null;
        Assert.IsFalse(rig.Start.IsOpen, "precondition: the click closed the screen");
        Assert.AreNotSame(rig.Ui.Q<Button>("end-turn"), rig.Ui.Root.focusController?.focusedElement, "End Turn is not lit by a mouse click");
    }

    /// <summary>A mouse press as the player makes it: pointer down (which also gives the button the focus), then the
    /// button's Clickable fires `clicked` on pointer up. UiTestKit.Press is the keyboard's Submit instead.</summary>
    static void PressWithPointer(Button b)
    {
        b.Focus();
        using (var down = PointerDownEvent.GetPooled()) { down.target = b; b.SendEvent(down); }
        using var click = ClickEvent.GetPooled();
        click.target = b;
        typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(EventBase) }, null)!.Invoke(b.clickable, new object[] { click });
    }

    /// <summary>#400: under the start screen nothing of the live HUD takes the focus. With a save from a later month and a
    /// card waiting, the card's options and the minimap's buttons are live, and still none of them takes the focus under
    /// the start screen. Built in GameRoot's order: the HUD, ScreenNav and the event card, then the screen.</summary>
    [UnityTest]
    public IEnumerator ASaveWithACardWaitingKeepsTheLiveHudOutOfReach()
    {
        var played = FamilyKit.Bridge(seed: 9);
        FamilyKit.PlayMonth(played);
        for (int i = 0; i < 26 && played.State.PendingEvents.Count == 0 && played.State.GameOver == null; i++) FamilyKit.PlayMonth(played);
        Assert.IsNotEmpty(played.State.PendingEvents, "precondition: a later month with a card waiting");
        var store = TempStore();
        store.WriteAutosave(GameStart.SaveText(played.Session));
        using var ui = new RenderedUi();
        var bridge = new SimBridge(Started(store, new List<string>(), out var resumed), Text);
        var owned = new List<IDisposable>();
        try
        {
            ui.Root.Q("root").RemoveFromClassList("loading");
            owned.Add(new HudPresenter(ui.Root, bridge, Text));
            owned.Add(new ScreenNav(ui.Root));
            var card = new EventCardPresenter(ui.Root, bridge, Text, underStartScreen: true); owned.Add(card);
            var toast = new ToastPresenter(ui.Root, bridge, Text); owned.Add(toast);
            var newGame = new NewGamePresenter(ui.Root, bridge, toast, Text, () => { }); owned.Add(newGame);
            var start = new StartScreenPresenter(ui.Root, newGame, resumed, () => { }); owned.Add(start);
            start.Show();
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(resumed, "precondition: the save is continued");
            Assert.IsNotNull(ui.Root.Q("event-options").Q<Button>(), "precondition: an event's options are shown");
            var overview = ui.Q<Button>("minimap-overview");
            void NoneReachable(string where)
            {
                foreach (var region in new[] { "top-bar", "left-column", "right-column", "event-layer" })
                    foreach (var b in ui.Root.Q(region).Query<Button>().ToList())
                    {
                        Assert.IsFalse(b.canGrabFocus, $"{where}: {region} {b.name} cannot take the focus under the start screen");
                        b.Focus();
                        Assert.AreNotSame(b, ui.Root.focusController?.focusedElement, $"{where}: {region} {b.name} is not focused");
                    }
            }
            NoneReachable("under the start screen");
            ui.Press("start-continue");
            yield return null; yield return null;
            // The waiting event's card opens in the middle (decision cards) and takes the focus (review S1); its options are
            // in reach, the right column under its backdrop is not; Collapse gives it back.
            yield return null;
            Assert.AreSame(ui.Root.Q("event-card"), ui.Root.panel.focusController.focusedElement, "the event card has the focus once the screen closes");
            Assert.IsTrue(ui.Root.Q("event-options").Q<Button>().canGrabFocus, "the event's options are in reach once the screen closes");
            Assert.IsFalse(overview.canGrabFocus, "the minimap waits under the decision card's backdrop");
            card.Collapse();
            yield return null;
            Assert.IsTrue(overview.canGrabFocus, "the minimap's buttons are back once the card is hidden");
        }
        finally { for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose(); }
    }

    // --- Layout ------------------------------------------------------------------------------------------------------

    /// <summary>The picture is at least 1920x1080 at 16:9 (StartScreenPresenter.ArtAspect), imported at its own size, so
    /// a 1080p window never shows an upscale (#268).</summary>
    [Test]
    public void ThePaintingIsNativeAtLeast1080p()
    {
        const string path = "Assets/UI/StartScreen/start-map.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.GetSourceTextureWidthAndHeight(out var w, out var h);
        Assert.GreaterOrEqual(w, 1920, "source width");
        Assert.GreaterOrEqual(h, 1080, "source height");
        Assert.AreEqual(StartScreenPresenter.ArtAspect, (float)w / h, 0.002f, "16:9");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Assert.AreEqual((w, h), (tex.width, tex.height), "imported at its own size (maxTextureSize does not shrink it)");
    }

    /// <summary>At each window size the owner named (a 1080p and a 768p screen, a small 16:10 window, a phone held upright):
    /// the screen covers the window; the painting keeps its 16:9 (a full-width band below 16:9, so nothing is cropped) and
    /// starts at the window's top, anchored at its top centre so a crop takes both sides alike; the panel, its lines and
    /// both buttons are inside the window and the panel; no line overlaps another or is squeezed below its font size; the
    /// panel leaves the painting's upper half (the flags and the table) clear.</summary>
    [UnityTest]
    public IEnumerator ItFitsEveryWindowSize()
    {
        foreach (var (w, h) in new[] { (1920, 1080), (1366, 768), (960, 600), (390, 844) })
        {
            var rt = new RenderTexture(w, h, 0);
            var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            var go = new GameObject("StartLayout");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            var root = doc.rootVisualElement;
            var store = WithSave(TempStore(), 1);
            var bridge = new SimBridge(Started(store, new List<string>(), out var resumed), Text);
            var toast = new ToastPresenter(root, bridge, Text);
            var newGame = new NewGamePresenter(root, bridge, toast, Text, () => { });
            var start = new StartScreenPresenter(root, newGame, resumed, () => { });
            try
            {
                root.Q("root").RemoveFromClassList("loading");
                start.Show();
                for (int i = 0; i < 20 && (float.IsNaN(root.Q("start-panel").worldBound.width) || root.Q("start-panel").worldBound.width <= 0); i++) yield return null;
                for (int i = 0; i < 4; i++) yield return null;      // the band height is set from the first layout
                var window = root.panel.visualTree.worldBound;
                Rect R(string name) => root.Q(name).worldBound;
                string at = $"{w}x{h} ({window.width:0}x{window.height:0} logical)";
                bool Inside(Rect inner, Rect outer) => inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;

                Assert.IsTrue(Inside(window, R("start-screen")), $"{at}: the screen {R("start-screen")} covers the window");
                var art = R("start-art");
                var panel = R("start-panel");
                Assert.AreEqual(window.width, art.width, 0.5f, $"{at}: the painting is full width");
                Assert.IsTrue(Inside(panel, window), $"{at}: the panel {panel} is inside the window");
                var tall = window.width / window.height < StartScreenPresenter.ArtAspect;
                if (root.Q("start-screen").ClassListContains(StartScreenPresenter.BandClass))
                {
                    Assert.IsTrue(tall, at);
                    Assert.AreEqual(art.width / StartScreenPresenter.ArtAspect, art.height, 2f, $"{at}: the band keeps 16:9 (to pixel snapping): nothing is cropped");
                    Assert.IsTrue(Inside(art, window), $"{at}: the band {art} is inside the window");
                    Assert.GreaterOrEqual(panel.yMin, art.yMax - 0.5f, $"{at}: the panel {panel} is under the band {art}");
                    Assert.AreEqual(art.yMin - window.yMin, window.yMax - panel.yMax, 20f, $"{at}: the band and the panel are centred together");
                }
                else
                {
                    Assert.AreEqual(window.yMin, art.yMin, 0.5f, $"{at}: the painting starts at the top");
                    Assert.AreEqual(window.height, art.height, 0.5f, $"{at}: the painting fills the window");
                    Assert.AreEqual(BackgroundPositionKeyword.Center, root.Q("start-art").resolvedStyle.backgroundPositionX.keyword, $"{at}: a crop takes both sides alike (the aisle stays in the middle)");
                    Assert.AreEqual(BackgroundPositionKeyword.Top, root.Q("start-art").resolvedStyle.backgroundPositionY.keyword, $"{at}: a crop keeps the top (the flags and the table)");
                    // Cover, anchored at the top: the painting's upper half (the flags and the table) stays clear of the panel.
                    var painted = Mathf.Max(window.height, window.width / StartScreenPresenter.ArtAspect);
                    Assert.GreaterOrEqual(panel.yMin, window.yMin + painted * 0.5f - 0.5f, $"{at}: the panel {panel} covers the painting's upper half");
                }
                if (h > w) Assert.IsTrue(root.Q("start-screen").ClassListContains(StartScreenPresenter.BandClass), $"{at}: a phone held upright stacks the painting over the panel");
                var lines = new[] { "start-title", "start-ar-proclamation", "start-en-proclamation", "start-ar-date", "start-en-date", "start-source", "start-actions" };
                foreach (var name in lines)
                {
                    var r = R(name);
                    Assert.IsTrue(Inside(r, panel), $"{at}: {name} {r} leaves the panel {panel}");
                    if (root.Q(name) is Label l) Assert.GreaterOrEqual(r.height + 0.5f, l.resolvedStyle.fontSize, $"{at}: {name} collapsed");
                }
                for (int i = 1; i < lines.Length; i++)
                    Assert.LessOrEqual(R(lines[i - 1]).yMax, R(lines[i]).yMin + 0.5f, $"{at}: {lines[i - 1]} overlaps {lines[i]}");
                foreach (var b in new[] { "start-continue", "start-new-game" })
                {
                    var r = R(b);
                    Assert.AreEqual(DisplayStyle.Flex, root.Q(b).resolvedStyle.display, $"{at}: {b} shows");
                    Assert.IsTrue(Inside(r, R("start-actions")) && Inside(r, panel), $"{at}: {b} {r} is inside the panel");
                    Assert.GreaterOrEqual(r.height, 40f, $"{at}: {b} is a usable target");
                }
                Assert.IsFalse(R("start-continue").Overlaps(R("start-new-game")), at);
            }
            finally
            {
                start.Dispose(); newGame.Dispose(); toast.Dispose();
                Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
    }

    // --- The scene ---------------------------------------------------------------------------------------------------

    static GameInputs Inputs => FamilyKit.Inputs;

    GameRoot OpenScene(SaveStore store)
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var root = Object.FindFirstObjectByType<GameRoot>();
        root.Store = store;
        return root;
    }

    static VisualElement Ui => Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
    static bool StartOpen => Ui.Q("root").ClassListContains(StartScreenPresenter.OpenClass);

    /// <summary>Map.unity as a player boots it: the start screen over the continued family save, the camera keys off until
    /// it closes.</summary>
    [Test]
    public void ThePlayerBootShowsTheScreenOverTheContinuedSaveAndStopsTheCamera()
    {
        var store = TempStore();
        WithSave(store.Family, 2);
        var root = OpenScene(store);
        var cam = Object.FindFirstObjectByType<CameraController>();
        root.Begin(Inputs, 0, startScreen: true);
        Assert.IsTrue(StartOpen);
        Assert.IsFalse(Ui.Q("root").ClassListContains("loading"));
        Assert.AreEqual(2, root.Bridge!.State.Turn, "the map's first turn waits behind the screen: the session is the save");
        Assert.AreEqual(GameRoot.AutomatedVillage, root.Bridge.State.Village);
        Assert.IsFalse(cam.enabled, "the camera keys are off");
        UiTestKit.Press(Ui.Q<Button>("start-continue"));
        Assert.IsFalse(StartOpen);
        Assert.IsTrue(cam.enabled);
        root.enabled = false;                                             // disposing with the screen open gives the camera back too
        root.enabled = true;
        root.Begin(Inputs, 0, startScreen: true);
        Assert.IsTrue(StartOpen && !cam.enabled);
        root.enabled = false;
        Assert.IsTrue(cam.enabled);
        Assert.IsFalse(StartOpen);
    }

    /// <summary>Automation never meets the screen: a smoke run (no New Game, so no screen even when asked), the tests'
    /// and tools' Begin(inputs, n), and the Ready path in the editor (not playing).</summary>
    [Test]
    public void AutomatedBootsNeverShowTheScreen()
    {
        var root = OpenScene(TempStore());
        LogAssert.Expect(LogType.Warning, "SMOKE FAIL the smoke run needs play mode or a player");
        root.Begin(Inputs, 5, startScreen: true);
        Assert.IsFalse(StartOpen, "smoke");
        root.Begin(Inputs, 0);
        Assert.IsFalse(StartOpen, "Begin(inputs, n)");
        root.Begin(Inputs);
        Assert.IsFalse(StartOpen, "GameBoot.Ready outside play mode");
        Assert.IsTrue(Object.FindFirstObjectByType<CameraController>().enabled);
    }

    // --- Right to left -----------------------------------------------------------------------------------------------

    /// <summary>The Arabic is laid out right to left (#193), rendered through the start screen's own style and direction
    /// (Noto Naskh Arabic, the advanced generator, RTL). "ه ــــــ" is a heh (a dense round letter) then a word of six tatweels
    /// (a long thin line) in logical order: right to left, the heh is drawn on the right and the line on its left, so the ink
    /// leans right.
    /// (Unity 6.6's standard generator orders Arabic too, so it is no control here.) "ه ." (a space between, so the stop stands apart) checks the paragraph's direction: a right-to-left
    /// paragraph puts the full stop on the left of the heh (ink leans right), a left-to-right one (the second control) on its
    /// right.</summary>
    [UnityTest]
    public IEnumerator ArabicIsLaidOutRightToLeft()
    {
        const int W = 400, H = 320;
        var rt = new RenderTexture(W, H, 0);
        var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
        ps.targetTexture = rt;
        ps.scaleMode = PanelScaleMode.ConstantPixelSize; ps.scale = 1f;
        ps.clearColor = true; ps.colorClearValue = Color.black;
        var go = new GameObject("RtlTest");
        go.SetActive(false);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = ps;
        go.SetActive(true);
        doc.rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/StoryOfTheLand.uss"));
        void Make(int row, string text, LanguageDirection direction = LanguageDirection.RTL)
        {
            var l = new Label(text);
            l.AddToClassList("start-ar"); l.languageDirection = direction;
            l.style.position = Position.Absolute; l.style.left = 20; l.style.top = row * 80; l.style.width = 360; l.style.height = 80;
            l.style.fontSize = 60; l.style.color = Color.white; l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            doc.rootVisualElement.Add(l);
        }
        Make(0, "\u0647 \u0640\u0640\u0640\u0640\u0640\u0640");   // ه ــــــ
        Make(2, "\u0647 .");                            // ه .
        Make(3, "\u0647 .", LanguageDirection.LTR);      // the control: a left-to-right paragraph
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        try
        {
            // The ink's centre of mass relative to the middle of its extent, as a share of the extent: > 0 leans right.
            float Lean(int row)
            {
                var cols = new float[W];
                for (int y = H - 80 * (row + 1); y < H - 80 * row; y++)          // ReadPixels row 0 is the bottom
                    for (int x = 0; x < W; x++) cols[x] += tex.GetPixel(x, y).grayscale;
                float total = cols.Sum();
                if (total < 20f) return float.NaN;
                int first = Array.FindIndex(cols, c => c > 0.5f), last = Array.FindLastIndex(cols, c => c > 0.5f);
                float centre = 0; for (int x = 0; x < W; x++) centre += x * cols[x];
                centre /= total;
                return (centre - (first + last) / 2f) / Mathf.Max(1, last - first);
            }
            float rtl = float.NaN, stop = float.NaN, ltrStop = float.NaN;
            for (int frame = 0; frame < 30 && (float.IsNaN(rtl) || float.IsNaN(stop) || float.IsNaN(ltrStop)); frame++)
            {
                yield return null;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                RenderTexture.active = null;
                rtl = Lean(0); stop = Lean(2); ltrStop = Lean(3);
            }
            Assert.IsFalse(float.IsNaN(rtl) || float.IsNaN(stop) || float.IsNaN(ltrStop), "the labels were not drawn (no ink in the render)");
            Assert.AreEqual("Advanced", doc.rootVisualElement[0].resolvedStyle.unityTextGenerator.ToString());
            Assert.Greater(rtl, 0.05f, $"the start screen's Arabic is drawn right to left: the ink leans right ({rtl:0.000})");
            Assert.Less(ltrStop, -0.05f, $"control: a left-to-right paragraph puts the full stop on the right ({ltrStop:0.000})");
            Assert.Greater(stop, 0.05f, $"a right-to-left paragraph: the full stop is on the left ({stop:0.000})");
        }
        finally { Object.DestroyImmediate(tex); Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); }
    }
}
