#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

/// <summary>F6, the pause menu (PausePresenter): Escape opens it when nothing else is open and closes it again; it covers the
/// game; Tab walks its items in order and the focus comes back when it closes; Settings, How to play and Credits bring it back
/// when their screen closes; Escape steps back one page; save to a slot and load from it, in the autosave's format; Main menu
/// and Quit ask first; and it fits at 1280x720, 1920x1080 and 390x844 in both languages, mirrored in Arabic.</summary>
public class PauseMenuTests
{
    static GameInputs Inputs => FamilyKit.Inputs;

    string dir = "";

    [SetUp] public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "storyoftheland-pause-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        SettingsPresenter.Reset();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    // ---- In the game (GameRoot routes Escape) ---------------------------------------------------------------------

    GameRoot Boot(string? language = null)
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var game = Object.FindFirstObjectByType<GameRoot>();
        game.Store = new SaveStore(dir);
        game.Language = language;
        game.Begin(Inputs, 0);
        return game;
    }

    static VisualElement Ui => Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

    /// <summary>A frame's end, as the player loop runs it: GameRoot notes what was open (edit mode runs no LateUpdate).</summary>
    static void EndFrame(GameRoot game) =>
        typeof(GameRoot).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(game, null);

    static void Escape(GameRoot game) { game.Escape(unfocused: true); EndFrame(game); }

    [Test]
    public void EscapeOpensTheMenuOnlyWhenNothingElseIsOpenAndClosesIt()
    {
        var game = Boot();
        var pause = game.Pause!;
        Assert.IsNotNull(game.Card!.Shown, "precondition: the opening card shows");
        EndFrame(game);
        Escape(game);
        Assert.IsTrue(game.Card.Collapsed, "the open card takes Escape first (Hide)");
        Assert.IsFalse(pause.IsOpen);
        Escape(game);
        Assert.IsTrue(pause.IsOpen, "with nothing open, Escape opens the pause menu");
        Assert.IsTrue(Ui.Q("root").ClassListContains(PausePresenter.OpenClass));
        foreach (var covered in new[] { "top-bar", "left-column", "right-column", "event-layer" })
            Assert.IsFalse(Ui.Q(covered).enabledInHierarchy, covered + " is covered: no key or click reaches it");
        Escape(game);
        Assert.IsFalse(pause.IsOpen, "Escape again closes it");
        Assert.IsTrue(Ui.Q("top-bar").enabledInHierarchy);

        UiTestKit.Press(Ui.Q<Button>("nav-settings"));
        EndFrame(game);
        Escape(game);
        Assert.IsFalse(ScreenNav.IsOpen(Ui.Q("screen-settings")), "Escape closes the open screen");
        Assert.IsFalse(pause.IsOpen, "and does not open the menu too");
        Escape(game);
        Assert.IsTrue(pause.IsOpen, "the next Escape does");
        Escape(game);

        game.Card.Reopen();
        EndFrame(game);
        // An Escape the focused card took through the UI's Cancel event in this frame: the card was open at the frame's start.
        game.Card.Escape();
        game.Escape(unfocused: false);
        Assert.IsFalse(pause.IsOpen, "the card's own Escape does not open the menu in the same frame");
    }

    /// <summary>The end-turn key (Enter, the keypad's too): it presses End Turn only with nothing open over the map (no open
    /// card, screen or pause menu) and no element focused (a focused control takes Enter as its own press, a text field as
    /// typing); on a finished game it says so on the toast and ends nothing.</summary>
    [Test]
    public void TheEndTurnKeyWorksOnlyWithNothingOpen()
    {
        var game = Boot();
        var bridge = game.Bridge!;
        int turn = bridge.State.Turn;
        Assert.IsNotNull(game.Card!.Shown, "precondition: a card is open");
        Assert.IsFalse(game.EndTurnKey(), "not over an open card");
        game.Card.Collapse();
        Ui.Q<Button>("new-game").Focus();
        if (Ui.focusController.focusedElement != null) Assert.IsFalse(game.EndTurnKey(), "not while a control has the focus");
        Ui.focusController.focusedElement?.Blur();
        // F6 round 5: a measuring run (-vprofile, -popcheck, -bootlog) takes no Enter, whoever is at the keyboard.
        StoryOfTheLand.Game.Boot.DiagLog.Automated = true;
        try { Assert.IsFalse(game.EndTurnKey(), "not in a measuring run"); }
        finally { StoryOfTheLand.Game.Boot.DiagLog.Automated = false; }
        Assert.AreEqual(turn, game.Bridge!.State.Turn);
        // Owner, 2026-10-21: with the card hidden but unanswered, Enter says so, opens the card and ends nothing.
        Assert.IsTrue(game.EndTurnKey(), "with a card unanswered, Enter says why");
        Assert.AreEqual(turn, game.Bridge!.State.Turn, "and ends nothing");
        Assert.AreEqual(FamilyKit.En["hud.end_turn_wait"], Ui.Q<Label>("toast").text);
        Assert.IsFalse(game.Card!.Collapsed, "the card opens again");
        FamilyKit.AnswerAll(game.Bridge!);
        Ui.focusController.focusedElement?.Blur();
        Assert.IsTrue(game.EndTurnKey());
        Assert.AreEqual(turn + 1, game.Bridge!.State.Turn, "with every card answered and nothing open, Enter ends the turn");
        game.Card!.Collapse();
        UiTestKit.Press(Ui.Q<Button>("nav-settings"));
        Assert.IsFalse(game.EndTurnKey(), "not under a screen");
        UiTestKit.Press(Ui.Q<Button>("nav-settings"));
        game.Pause!.Open();
        Assert.IsFalse(game.EndTurnKey(), "not under the pause menu");
        game.Pause.Close();
        Ui.focusController.focusedElement?.Blur();
        for (int guard = 0; game.Bridge!.State.GameOver == null && guard < 400; guard++) FamilyKit.PlayMonth(game.Bridge);
        Assert.IsNotNull(game.Bridge!.State.GameOver, "precondition: the chapter ends");
        // Playtest (F6): the chapter's last month opens its end; closed, End Turn (and Enter) opens it again, never a dead button.
        Assert.IsTrue(game.Pause!.IsOpen && Ui.Q("pause-end").style.display.value == DisplayStyle.Flex, "the chapter's end shows");
        game.Pause.Close();
        game.Card!.Collapse();
        Ui.focusController.focusedElement?.Blur();
        turn = game.Bridge.State.Turn;
        Assert.IsTrue(Ui.Q<Button>("end-turn").enabledSelf, "End Turn stays pressable");
        Assert.AreEqual(FamilyKit.En["hud.end_turn_over"], Ui.Q<Button>("end-turn").tooltip, "and says why");
        Assert.IsTrue(game.EndTurnKey());
        Assert.IsTrue(game.Pause.IsOpen && Ui.Q("pause-end").style.display.value == DisplayStyle.Flex, "Enter shows the chapter's end again");
        Assert.AreEqual(turn, game.Bridge.State.Turn);
    }

    /// <summary>Playtest (F6), first-time player at 1600x900 on the web build: the chapter's end (1 January 1950 ends 1947_1949)
    /// says "Chapter complete" with the chapter, its years and the village, and Next chapter starts the next chapter for the
    /// same village; the first new game shows the hint once per profile (kept in settings.txt); a month with nothing to decide
    /// says so on the toast; the pause menu's Chapters opens the chapter picker.</summary>
    [Test]
    public void TheChapterEndsWithAPanelAndNextChapterGoesOn()
    {
        var game = Boot();
        for (int guard = 0; game.Bridge!.State.GameOver == null && guard < 400; guard++) FamilyKit.PlayMonth(game.Bridge);
        Assert.AreEqual(new DateTime(1950, 1, 1), game.Bridge!.State.Date, "precondition: the chapter's end date");
        Assert.AreEqual(FamilyKit.En["chapter_end.title"], Ui.Q<Label>("pause-title").text);
        StringAssert.Contains("Tantura", Ui.Q<Label>("pause-end-summary").text);
        StringAssert.Contains(FamilyKit.En["chapter.1947_1949"], Ui.Q<Label>("pause-end-summary").text);
        UiTestKit.Press(Ui.Q<Button>("pause-end-next"));
        Assert.AreEqual("1950_1966", FamilyGame.ChapterOf(game.Bridge!.Content), "the next chapter");
        Assert.AreEqual(GameRoot.AutomatedVillage, game.Bridge.State.Village, "for the same family");
        Assert.IsNull(game.Bridge.State.GameOver);
    }

    [Test]
    public void TheFirstNewGameShowsTheHintOncePerProfile()
    {
        var game = Boot();
        var settings = new SettingsStore(Path.Combine(dir, "settings"));
        game.Settings = settings;
        game.StartFamily(Inputs, "1947_1949", "tantura");
        Assert.IsTrue(game.Pause!.IsOpen, "the hint shows on the first game");
        Assert.AreEqual(DisplayStyle.Flex, Ui.Q("pause-hint").style.display.value);
        Assert.AreEqual(FamilyKit.En["hint.goal"], Ui.Q<Label>("pause-hint-goal").text);
        Assert.AreEqual("seen", settings.Read(GameRoot.HintKey), "kept for the profile");
        UiTestKit.Press(Ui.Q<Button>("pause-hint-ok"));
        Assert.IsFalse(game.Pause.IsOpen, "Got it closes it");
        game.StartFamily(Inputs, "1967_1987", "imwas");
        Assert.IsFalse(game.Pause!.IsOpen, "never again for this profile");
    }

    [Test]
    public void AQuietMonthSaysSoAndChaptersOpensThePicker()
    {
        var game = Boot();
        bool quiet = false;
        for (int guard = 0; !quiet && game.Bridge!.State.GameOver == null && guard < 40; guard++)
        {
            FamilyKit.PlayMonth(game.Bridge);
            quiet = game.Bridge.State.PendingEvents.Count == 0 && game.Bridge.State.GameOver == null;
        }
        Assume.That(quiet, "the chapter has a month with no card");
        Assert.AreEqual(FamilyKit.En["toast.quiet"], Ui.Q<Label>("toast").text);
        game.Pause!.Open();
        UiTestKit.Press(Ui.Q<Button>("pause-chapters"));
        Assert.AreEqual(FamilyKit.En["restart.confirm"], Ui.Q<Label>("pause-confirm-text").text, "playtest 2: Chapters asks first");
        UiTestKit.Press(Ui.Q<Button>("pause-confirm-yes"));
        Assert.IsFalse(game.Pause.IsOpen, "the menu steps aside");
        Assert.IsTrue(game.Picker!.IsOpen, "for the chapter picker");
    }

    /// <summary>Playtest 2: New game over a game in progress asks "Your current game will be lost. Continue?" (Confirm,
    /// Cancel); Cancel leaves the game as it was, Confirm opens the chapter picker.</summary>
    [Test]
    public void NewGameAsksBeforeTheGameInProgressIsLost([Values(false, true)] bool arabic)
    {
        var game = Boot(arabic ? UiLanguage.Arabic : null);
        var text = arabic ? FamilyKit.Ar : FamilyKit.En;
        var session = game.Bridge!.Session;
        UiTestKit.Press(Ui.Q<Button>("new-game"));
        Assert.IsTrue(game.Pause!.IsOpen);
        Assert.AreEqual(text["restart.confirm"], Ui.Q<Label>("pause-confirm-text").text);
        Assert.AreEqual((text["restart.yes"], text["pause.cancel"]), (Ui.Q<Button>("pause-confirm-yes").text, Ui.Q<Button>("pause-confirm-no").text));
        UiTestKit.Press(Ui.Q<Button>("pause-confirm-no"));
        Assert.IsFalse(game.Pause.IsOpen, "Cancel: back to the game");
        Assert.IsFalse(game.Picker!.IsOpen);
        Assert.AreSame(session, game.Bridge!.Session);
        UiTestKit.Press(Ui.Q<Button>("new-game"));
        UiTestKit.Press(Ui.Q<Button>("pause-confirm-yes"));
        Assert.IsFalse(game.Pause.IsOpen);
        Assert.IsTrue(game.Picker!.IsOpen, "Confirm: the chapter picker");
    }

    /// <summary>Playtest 2: after a quiet month, "Skip to the next event" ends turns until a card comes (or the chapter ends,
    /// or 24 months), saying each month as it passes and then how many passed, with the plural rule.</summary>
    [Test]
    public void SkipToTheNextEventRunsTheQuietMonths()
    {
        var game = Boot();
        bool quiet = false;
        for (int guard = 0; !quiet && game.Bridge!.State.GameOver == null && guard < 40; guard++)
        {
            FamilyKit.PlayMonth(game.Bridge);
            quiet = game.Bridge.State.PendingEvents.Count == 0 && game.Bridge.State.GameOver == null;
        }
        Assume.That(quiet, "the chapter has a month with no card");
        var skip = Ui.Q<Button>(GameRoot.SkipName);
        Assert.IsTrue(skip.ClassListContains(GameRoot.SkipShownClass), "offered after the quiet month");
        Assert.AreEqual(FamilyKit.En["skip.button"], skip.text);
        int turn = game.Bridge!.State.Turn;
        game.SkipAhead();
        var s = game.Bridge!.State;
        int months = s.Turn - turn;
        Assert.That(months, Is.InRange(1, GameRoot.SkipCap));
        Assert.IsTrue(s.PendingEvents.Count > 0 || s.GameOver != null || months == GameRoot.SkipCap, "it stops at a card, the end, or the cap");
        if (s.GameOver == null) Assert.AreEqual(FamilyKit.En.Plural("skip.passed", months), Ui.Q<Label>("toast").text, "how many months passed");
        if (s.PendingEvents.Count > 0) Assert.IsFalse(skip.ClassListContains(GameRoot.SkipShownClass), "a card: the offer goes");
        Assert.AreEqual(FamilyKit.En["skip.button"], skip.text, "Stop again reads Skip");
    }

    [Test]
    public void TheStartScreenAndThePickerKeepEscape()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var game = Object.FindFirstObjectByType<GameRoot>();
        game.Store = new SaveStore(dir);
        game.Begin(Inputs, 0, startScreen: true);
        EndFrame(game);
        Escape(game); Escape(game);
        Assert.IsFalse(game.Pause!.IsOpen, "no pause menu over the start screen");
    }

    /// <summary>Save to a slot, then load from it: the slot holds the autosave's format (the chapter line, then the sim's
    /// save), names its chapter, village and month, and loading replays the game to the same state; saving over a used slot
    /// asks first, and so does loading.</summary>
    [Test]
    public void SaveToASlotAndLoadFromIt([Values(false, true)] bool arabic)
    {
        var game = Boot(arabic ? UiLanguage.Arabic : null);
        var text = arabic ? FamilyKit.Ar : FamilyKit.En;
        FamilyKit.PlayMonth(game.Bridge!); FamilyKit.PlayMonth(game.Bridge!);
        var saved = game.Bridge!.State;
        var (turn, hash, date) = (saved.Turn, saved.Hash(), saved.Date);
        game.Pause!.Open();
        UiTestKit.Press(Ui.Q<Button>("pause-save"));
        Assert.IsNull(Ui.Q("pause-autosave"), "the autosave is not a slot to save to");
        var slot = Ui.Q<Button>("pause-slot-1");
        Assert.AreEqual(text["slots.empty"], slot.Q<Label>(className: "pause-slot-detail").text);
        UiTestKit.Press(slot);
        Assert.AreEqual(text.Format("slots.saved", 1), Ui.Q<Label>("toast").text, "said on the toast");
        var file = new SaveStore(dir).Family.Slot(1);
        var (chapter, sim) = FamilyGame.SplitSave(file.ReadAutosave()!);
        Assert.AreEqual(GameRoot.AutomatedChapter, chapter, "the autosave's format: the chapter line first");
        Assert.AreEqual(game.Bridge.Session.Save.Write(), sim, "then the sim's save");
        slot = Ui.Q<Button>("pause-slot-1");
        var detail = slot.Q<Label>(className: "pause-slot-detail").text;
        StringAssert.Contains(text["chapter." + GameRoot.AutomatedChapter], detail, "the chapter's title");
        StringAssert.Contains(arabic ? "الطنطورة" : "Tantura", detail, "the village");
        StringAssert.Contains(PlayerText.Month(date, arabic), detail, "the turn's month, as the HUD says it");
        StringAssert.Contains(PlayerText.Date(DateTime.Now, arabic), slot.Q<Label>(className: "pause-slot-time").text, "when it was saved");
        UiTestKit.Press(slot);
        Assert.AreEqual(text.Format("slots.overwrite", 1), Ui.Q<Label>("pause-confirm-text").text, "a used slot asks first");
        UiTestKit.Press(Ui.Q<Button>("pause-confirm-no"));
        game.Pause.Close();

        FamilyKit.PlayMonth(game.Bridge); FamilyKit.PlayMonth(game.Bridge);
        Assert.AreNotEqual(turn, game.Bridge.State.Turn);
        game.Pause.Open();
        UiTestKit.Press(Ui.Q<Button>("pause-load"));
        Assert.IsFalse(Ui.Q<Button>("pause-autosave").enabledSelf, "this run keeps no autosave: nothing to load there");
        Assert.IsFalse(Ui.Q<Button>("pause-slot-2").enabledSelf, "an empty slot cannot be loaded");
        UiTestKit.Press(Ui.Q<Button>("pause-slot-1"));
        Assert.AreEqual(text["slots.load_confirm"], Ui.Q<Label>("pause-confirm-text").text, "loading asks first");
        UiTestKit.Press(Ui.Q<Button>("pause-confirm-yes"));
        Assert.AreEqual(turn, game.Bridge!.State.Turn, "the saved turn");
        Assert.AreEqual(hash, game.Bridge.State.Hash(), "replayed to the same state");
        Assert.IsFalse(game.Pause!.IsOpen, "the loaded game is played at once");
    }

    /// <summary>Main menu asks first, saves, and shows the start screen with Continue, which goes back to the same game.</summary>
    [Test]
    public void MainMenuAsksFirstSavesAndOffersContinue()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var game = Object.FindFirstObjectByType<GameRoot>();
        game.Store = new SaveStore(dir);
        game.Begin(Inputs, 0, startScreen: true);
        game.StartFamily(Inputs, "1967_1987", "imwas");                 // a player's game: it keeps the autosave
        FamilyKit.PlayMonth(game.Bridge!);
        var session = game.Bridge!.Session;
        game.Pause!.Open();
        UiTestKit.Press(Ui.Q<Button>("pause-main-menu"));
        Assert.AreEqual(FamilyKit.En["pause.confirm_main_menu"], Ui.Q<Label>("pause-confirm-text").text);
        UiTestKit.Press(Ui.Q<Button>("pause-confirm-no"));
        Assert.IsFalse(Ui.Q("root").ClassListContains(StartScreenPresenter.OpenClass), "Cancel stays in the game");
        UiTestKit.Press(Ui.Q<Button>("pause-main-menu"));
        UiTestKit.Press(Ui.Q<Button>("pause-confirm-yes"));
        Assert.IsTrue(Ui.Q("root").ClassListContains(StartScreenPresenter.OpenClass), "the start screen");
        Assert.AreEqual(DisplayStyle.Flex, Ui.Q<Button>("start-continue").style.display.value, "with Continue");
        Assert.AreEqual(FamilyGame.SaveText(session), new SaveStore(dir).Family.ReadAutosave(), "the game was saved first");
        UiTestKit.Press(Ui.Q<Button>("start-continue"));
        Assert.AreSame(session, game.Bridge!.Session, "Continue goes back to the same game");
    }

    // ---- The menu on its own ----------------------------------------------------------------------------------------

    /// <summary>The menu's presenters on Game.uxml at a window's size, with a game's HUD and screens.</summary>
    sealed class Rig : IDisposable
    {
        readonly GameObject go; readonly PanelSettings ps; readonly RenderTexture rt;
        public readonly VisualElement Root; public readonly ScreenNav Nav; public readonly PausePresenter Pause;
        public readonly SimBridge Bridge; public readonly UiText Text; public int Quits;
        readonly List<IDisposable> owned = new List<IDisposable>();

        public Rig(string dir, bool arabic = false, int w = 1920, int h = 1080, bool web = false)
        {
            rt = new RenderTexture(w, h, 0);
            ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            go = new GameObject("PauseTest"); go.SetActive(false);
            var doc = go.AddComponent<UIDocument>(); doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            Root = doc.rootVisualElement;
            Root.Q("root").RemoveFromClassList("loading");
            if (arabic) Root.Q("root").AddToClassList(UiLanguage.ArabicClass);
            Text = arabic ? FamilyKit.Ar : FamilyKit.En;
            Bridge = FamilyKit.Bridge(Text);
            var toast = Own(new ToastPresenter(Root, Bridge, Text));
            Own(new HudPresenter(Root, Bridge, Text));
            Nav = Own(new ScreenNav(Root));
            Own(new HowToPlayPresenter(Root, Text));
            Own(new SettingsPresenter(Root, Text, null));
            Own(new CreditsPresenter(Root, Credits.Parse(FamilyKit.Inputs.Read(Credits.FileName)), Text, Nav));
            var saves = new SaveStore(dir).Family;
            Pause = Own(new PausePresenter(Root, Text, Nav, saves, FamilyKit.Chapter, () => Bridge.Session, _ => { }, () => { },
                web ? null : (Action)(() => Quits++), toast.Show, chapters: () => { }));
            if (arabic) Own(new UiDirection(Root, Bridge));
        }

        T Own<T>(T d) where T : IDisposable { owned.Add(d); return d; }

        public Focusable? Focused => Root.panel.focusController.focusedElement;

        public void Dispose()
        {
            for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
        }
    }

    static IEnumerator Frames(int n = 3) { for (int i = 0; i < n; i++) yield return null; }

    /// <summary>Opened, the first item has the focus; Tab walks the items in order and stays in the menu; closed, the focus
    /// goes back to what had it (End Turn here).</summary>
    [UnityTest]
    public IEnumerator TabWalksTheItemsInOrderAndTheFocusComesBack()
    {
        using var rig = new Rig(dir);
        var endTurn = rig.Root.Q<Button>("end-turn");
        endTurn.Focus();
        yield return Frames();
        rig.Pause.Open();
        yield return Frames();
        Assert.AreEqual("pause-resume", (rig.Focused as VisualElement)?.name, "the first item has the focus");
        var seen = new List<string>();
        for (int i = 0; i < PausePresenter.Items.Length; i++)
        {
            var at = (VisualElement)rig.Focused!;
            using (var e = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next)) { e.target = at; at.SendEvent(e); }
            yield return null;
            seen.Add((rig.Focused as VisualElement)?.name ?? "none");
        }
        CollectionAssert.AreEqual(PausePresenter.Items.Skip(1).Select(i => "pause-" + i).Append("pause-resume"), seen,
            "Tab walks the items in order and comes round inside the menu: " + string.Join(", ", seen));
        UiTestKit.Press(rig.Root.Q<Button>("pause-resume"));
        yield return Frames();
        Assert.IsFalse(rig.Pause.IsOpen);
        Assert.AreSame(endTurn, rig.Focused, "the focus is back on End Turn");
    }

    /// <summary>Settings, How to play and Credits open their screen; when it closes (its X or Escape), the menu is back with
    /// the focus on the item that opened it. Escape steps back one page: a confirm or the slots to the items, the items to the
    /// game; a controller's Cancel does the same.</summary>
    [UnityTest]
    public IEnumerator ItsScreensComeBackToItAndEscapeStepsBackOnce()
    {
        using var rig = new Rig(dir);
        foreach (var (item, screen) in new[] { ("settings", "settings"), ("how-to-play", "how-to-play"), ("credits", "credits") })
        {
            rig.Pause.Open();
            UiTestKit.Press(rig.Root.Q<Button>("pause-" + item));
            Assert.AreEqual(screen, rig.Nav.Current, item + " opens its screen");
            Assert.IsFalse(rig.Pause.IsOpen, "the menu steps aside");
            rig.Nav.Close();
            Assert.IsTrue(rig.Pause.IsOpen, item + ": the menu is back");
            yield return Frames();
            Assert.AreEqual("pause-" + item, (rig.Focused as VisualElement)?.name, item + ": on its item");
            rig.Pause.Close();
        }
        // Settings, then Credits from Settings: the menu waits until no screen is open.
        rig.Pause.Open();
        UiTestKit.Press(rig.Root.Q<Button>("pause-settings"));
        UiTestKit.Press(rig.Root.Q<Button>("settings-credits"));
        Assert.IsFalse(rig.Pause.IsOpen);
        rig.Nav.Close();
        Assert.AreEqual("settings", rig.Nav.Current, "audit m8: Escape from Credits goes back to Settings, one level");
        Assert.IsFalse(rig.Pause.IsOpen);
        rig.Nav.Close();
        Assert.IsTrue(rig.Pause.IsOpen);

        UiTestKit.Press(rig.Root.Q<Button>("pause-save"));
        Assert.AreEqual(DisplayStyle.Flex, rig.Root.Q("pause-slots").style.display.value);
        Assert.IsTrue(rig.Pause.Escape());
        Assert.AreEqual(DisplayStyle.Flex, rig.Root.Q("pause-items").style.display.value, "Escape: from the slots to the items");
        UiTestKit.Press(rig.Root.Q<Button>("pause-main-menu"));
        Assert.AreEqual(DisplayStyle.Flex, rig.Root.Q("pause-confirm").style.display.value);
        using (var e = NavigationCancelEvent.GetPooled()) { var at = rig.Root.Q<Button>("pause-confirm-no"); e.target = at; at.SendEvent(e); }
        Assert.AreEqual(DisplayStyle.Flex, rig.Root.Q("pause-items").style.display.value, "a controller's Cancel: from the confirm to the items");
        Assert.IsTrue(rig.Pause.IsOpen);
        Assert.IsTrue(rig.Pause.Escape());
        Assert.IsFalse(rig.Pause.IsOpen, "from the items to the game");
        Assert.IsFalse(rig.Pause.Escape(), "shut, it takes no Escape");
    }

    /// <summary>Quit asks first and quits on Yes; the web player has no Quit.</summary>
    [Test]
    public void QuitAsksFirstAndIsNotOfferedOnTheWeb()
    {
        using (var rig = new Rig(dir))
        {
            rig.Pause.Open();
            UiTestKit.Press(rig.Root.Q<Button>("pause-quit"));
            Assert.AreEqual(rig.Text["pause.confirm_quit"], rig.Root.Q<Label>("pause-confirm-text").text);
            Assert.AreEqual(0, rig.Quits, "not before the answer");
            UiTestKit.Press(rig.Root.Q<Button>("pause-confirm-no"));
            Assert.AreEqual(0, rig.Quits);
            UiTestKit.Press(rig.Root.Q<Button>("pause-quit"));
            UiTestKit.Press(rig.Root.Q<Button>("pause-confirm-yes"));
            Assert.AreEqual(1, rig.Quits);
        }
        using (var rig = new Rig(dir, web: true))
            Assert.AreEqual(DisplayStyle.None, rig.Root.Q<Button>("pause-quit").style.display.value);
    }

    /// <summary>At 1280x720, 1920x1080 and a phone held upright (390x844), in English and Arabic, each page of the menu (the
    /// items, the slots with a save in one, a confirm) lies inside the window, every text whole; in Arabic each line is set
    /// right to left and the confirm's answer comes first at the right.</summary>
    [UnityTest]
    public IEnumerator EachPageFitsTheWindowInBothLanguages()
    {
        var problems = new List<string>();
        foreach (var arabic in new[] { false, true })
            foreach (var (w, h) in new[] { (1280, 720), (1920, 1080), (390, 844) })
            {
                using var rig = new Rig(dir, arabic, w, h);
                var at = $"{(arabic ? "ar" : "en")} {w}x{h}";
                new SaveStore(dir).Family.Slot(2).WriteAutosave(FamilyGame.SaveText(rig.Bridge.Session));
                rig.Pause.Open();
                foreach (var page in new[] { "items", "slots", "confirm" })
                {
                    if (page == "slots") UiTestKit.Press(rig.Root.Q<Button>("pause-load"));
                    if (page == "confirm") UiTestKit.Press(rig.Root.Q<Button>("pause-slot-2"));
                    yield return Frames(4);
                    var panel = rig.Root.Q("pause-panel").worldBound;
                    if (!Inside(panel, rig.Root.worldBound)) problems.Add($"{at} {page}: the panel {panel} leaves the window {rig.Root.worldBound}");
                    foreach (var t in rig.Root.Q("pause-panel").Query<TextElement>().ToList().Where(t => !string.IsNullOrEmpty(t.text) && Shown(t)))
                    {
                        if (!Inside(t.worldBound, panel)) problems.Add($"{at} {page}: '{t.text}' {t.worldBound} leaves the panel {panel}");
                        if (!UiLayoutTests.TextFits(t)) problems.Add($"{at} {page}: '{t.text}' is cut ({t.contentRect})");
                        if (arabic && UiDirection.RightToLeft(t.text) && t.languageDirection != LanguageDirection.RTL) problems.Add($"{at} {page}: '{t.text}' is not right to left");
                        if (arabic && System.Text.RegularExpressions.Regex.IsMatch(t.text, "[A-Za-z]")) problems.Add($"{at} {page}: Latin letters in '{t.text}'");
                    }
                    if (page == "confirm")
                    {
                        float yes = rig.Root.Q("pause-confirm-yes").worldBound.center.x, no = rig.Root.Q("pause-confirm-no").worldBound.center.x;
                        if (arabic ? yes < no : yes > no) problems.Add($"{at}: the answer is not first in the reading order");
                    }
                }
                Directory.Delete(dir, true);
            }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    static bool Shown(VisualElement e)
    {
        for (var x = e; x != null; x = x.parent) if (x.resolvedStyle.display == DisplayStyle.None) return false;
        return true;
    }

    static bool Inside(Rect inner, Rect outer) =>
        inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;
}
