#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>The family autosave (SaveStore.Family, its chapter on its first line): written every turn, continued at the
/// next start (GameStart.ContinueFamily), set aside with one short localised note when it cannot be continued, and only
/// replaced when a new family game starts (GameRoot.StartFamily). New game's two presses (NewGamePresenter).</summary>
public class SaveUiTests
{
    static UiText Text => FamilyKit.En;
    static readonly List<string> TempFolders = new List<string>();
    static SaveStore TempStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "storyoftheland-saves-" + System.Guid.NewGuid().ToString("N"));
        TempFolders.Add(dir);
        return new SaveStore(dir);
    }

    [TearDown]
    public void RemoveTempFolders()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // after the scene tests
        foreach (var d in TempFolders) { try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch (System.Exception) { } }
        TempFolders.Clear();
    }

    /// <summary>GameStart.ContinueFamily as GameRoot calls it: the chapter the save names, from the repository's inputs
    /// (or <paramref name="content"/>).</summary>
    static GameSession? Continue(SaveStore store, List<string> notes, UiText? text = null, Content? content = null) =>
        GameStart.ContinueFamily(id => content ?? FamilyKit.Inputs.Chapter(id), store, notes, text ?? Text);

    /// <summary>The default family game after its first month, its first card (<paramref name="first"/>) answered with its
    /// first choice.</summary>
    static GameSession PlayedOneMonth(out string first)
    {
        var played = FamilyKit.Session(seed: 9);
        Assert.IsNotEmpty(played.State.PendingEvents, "precondition: a card waits in the first month");
        var id = played.State.PendingEvents.First();
        var e = played.Content.Events.First(x => x.Id == id);
        played.Step(new Command[] { new ChooseEventOption(id, e.Options[0].Id) });
        first = id;
        return played;
    }

    /// <summary>A save whose final hash is not the replay's.</summary>
    static string OtherEnd(string save) => Regex.Replace(save, "\nend ([0-9A-Fa-f]+)", m => "\nend " + new string('0', m.Groups[1].Length));

    /// <summary>A store whose reads and set-asides fail the way a broken disk does, deterministically.</summary>
    sealed class FaultyStore : SaveStore
    {
        readonly string? text;
        public bool SetAsideCalled;
        public FaultyStore(string? text) : base(Path.Combine(Path.GetTempPath(), "storyoftheland-faulty")) { this.text = text; }
        public override string? ReadAutosave() => text ?? throw new IOException("disk gone");
        public override void SetAsideAutosave() { SetAsideCalled = true; throw new System.UnauthorizedAccessException("no permission"); }
    }

    [Test]
    public void EveryTurnIsAutosavedAndTheNextStartContinuesIt()
    {
        var store = TempStore();
        var notes = new List<string>();
        var bridge = FamilyKit.Bridge(seed: 77);
        GameStart.Autosave(bridge, store, Text);
        FamilyKit.PlayMonth(bridge); FamilyKit.PlayMonth(bridge);
        Assert.IsTrue(File.Exists(store.AutosavePath));
        Assert.IsFalse(File.Exists(store.AutosavePath + ".tmp"));
        StringAssert.StartsWith(FamilyGame.ChapterLine + GameRoot.AutomatedChapter + "\n", File.ReadAllText(store.AutosavePath), "the chapter line first");
        var resumed = Continue(store, notes)!;
        Assert.IsEmpty(notes);
        Assert.AreEqual(bridge.State.Hash(), resumed.State.Hash());
        Assert.AreEqual(2, resumed.State.Turn);
        Assert.AreEqual(GameRoot.AutomatedVillage, resumed.State.Village);
    }

    [Test]
    public void AnUnreadableAutosaveIsSetAsideAndNotContinued()
    {
        var store = TempStore();
        store.WriteAutosave(FamilyGame.ChapterLine + GameRoot.AutomatedChapter + "\nstoryoftheland-save 9\ncontent x\nseed 1\nmode Historical\nend x\n");
        var notes = new List<string>();
        Assert.IsNull(Continue(store, notes));
        Assert.AreEqual(Text["save.refused." + SaveException.NewerVersion], notes[0]);   // a newer schema: R-SAVE-002 (N2)
        Assert.IsFalse(File.Exists(store.AutosavePath));
        Assert.IsTrue(File.Exists(Path.Combine(store.Directory, "autosave.bad.txt")));
    }

    [Test]
    public void ASaveWithAnUnsupportedModeIsSetAsideToo()
    {
        var store = TempStore();
        var good = GameStart.SaveText(FamilyKit.Session(seed: 9));
        var open = Regex.Replace(good, "\\nmode \\w+\\n", "\nmode Open\n");
        Assert.AreNotEqual(good, open, "precondition: the save has a mode line");
        store.WriteAutosave(open);
        var notes = new List<string>();
        Assert.IsNull(Continue(store, notes));
        Assert.AreEqual(new[] { Unreadable(Text) }, notes.ToArray());
        Assert.IsTrue(File.Exists(Path.Combine(store.Directory, "autosave.bad.txt")));
    }

    /// <summary>The note the player reads for any autosave that cannot be continued: a short reason in their language.</summary>
    static string Unreadable(UiText text) => text.Format("save.unreadable", text["save.reason_other"]);

    /// <summary>#631: every way a save is refused (a damaged line, another ruleset, a replay that ends elsewhere, a replay that
    /// throws) shows the player the same short reason in English and in Arabic, never the sim's English message; the full
    /// exception, with the replay's own exception inside it, goes to the log.</summary>
    [TestCase("strings.en.yaml", "damaged line")]
    [TestCase("strings.ar.yaml", "damaged line")]
    [TestCase("strings.en.yaml", "other ruleset")]
    [TestCase("strings.ar.yaml", "other ruleset")]
    [TestCase("strings.en.yaml", "replay differs")]
    [TestCase("strings.ar.yaml", "replay differs")]
    [TestCase("strings.en.yaml", "replay throws")]
    [TestCase("strings.ar.yaml", "replay throws")]
    public void AnUnreadableSaveShowsOnlyALocalisedReason(string file, string kind)
    {
        var text = UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Folder + "/" + file).text, file);
        Content? content = null;
        var played = PlayedOneMonth(out var first);
        var save = GameStart.SaveText(played);
        string logged;
        var expected = Unreadable(text);
        switch (kind)
        {
            case "damaged line":
                save = save.Replace("\nseed 9\n", "\nseed nine\n");
                logged = "line 3: bad seed";
                break;
            case "other ruleset":
                // A save of the older "legacy" ruleset has no village; the game refuses its ruleset.
                save = save.Replace("ruleset " + SaveGame.FamilyRuleset + "\nvillage " + GameRoot.AutomatedVillage + "\n", "ruleset legacy\nvillage -\n");
                logged = "version of the game's rules";
                // A coded refusal (R-SAVE-001): a save of other rules says so in its own string (N4).
                expected = text["save.refused." + SaveException.OlderRules];
                break;
            case "replay differs":
                save = OtherEnd(save);   // the same content, so a replay that ends elsewhere is refused
                logged = "The replay does not match the saved game";
                break;
            default:
                // Its own copy of the chapter, the same hash: the first month's choice can no longer be applied.
                content = ContentLoader.LoadChapter(Paths.Content, GameRoot.AutomatedChapter);
                content.Events.First(e => e.Id == first).Options[0].Effects = null!;
                logged = "turn 1: (.|\\n)*ArgumentNullException";
                break;
        }
        // "replay throws" keeps the save and changes the chapter it replays in; the other cases change the save.
        if (content == null) Assert.AreNotEqual(GameStart.SaveText(played), save, "precondition: the save was changed");
        else Assert.AreEqual(played.Content.Hash, content.Hash, "precondition: the same content, so the replay is not merely warned about");
        var store = TempStore();
        store.WriteAutosave(save);
        var notes = new List<string>();
        LogAssert.Expect(LogType.Warning, new Regex("Family autosave could not be loaded: (.|\\n)*" + logged));
        Assert.IsNull(Continue(store, notes, text, content));
        Assert.AreEqual(new[] { expected }, notes.ToArray());
        StringAssert.DoesNotContain("line", notes[0]);
        StringAssert.DoesNotContain("turn", notes[0]);
        Assert.IsTrue(File.Exists(Path.Combine(store.Directory, "autosave.bad.txt")));
    }

    /// <summary>A store whose autosave is refused with a code the strings do not have (a future SaveException code).</summary>
    sealed class UnknownCodeStore : SaveStore
    {
        public UnknownCodeStore(string dir) : base(dir) { }
        public override string? ReadAutosave() => throw new SaveException(0, "made by a future version", "a_code_with_no_string");
    }

    /// <summary>#637: a load warning kind with no string (a kind added to the sim before its strings) is only logged. It
    /// must not throw inside ContinueFamily, which would set a loadable save aside as unreadable.</summary>
    [Test]
    public void ALoadWarningKindWithNoStringIsOnlyLogged()
    {
        var yaml = AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Strings).text;
        var key = "save.warning." + GameSession.ContentChanged + ":";
        StringAssert.Contains("\n" + key, yaml);
        var text = UiText.Parse(string.Join("\n", yaml.Split('\n').Where(l => !l.StartsWith(key))));
        Assert.IsFalse(text.Has("save.warning." + GameSession.ContentChanged));
        var store = TempStore();
        var c = FamilyKit.Chapter();
        store.WriteAutosave(GameStart.SaveText(FamilyKit.Session(seed: 9)).Replace("content " + c.Hash, "content " + new string('0', 64)));
        var notes = new List<string>();
        LogAssert.Expect(LogType.Warning, "Family autosave loaded with a warning: " + GameSession.ContentChanged);
        Assert.IsNotNull(Continue(store, notes, text), "the save is continued, not set aside");
        Assert.IsEmpty(notes);
        Assert.IsTrue(File.Exists(store.AutosavePath));
        Assert.IsFalse(File.Exists(Path.Combine(store.Directory, "autosave.bad.txt")));
    }

    /// <summary>The start never blocks: a refusal code with no string falls back to the one short localised reason (#631),
    /// instead of throwing a missing-key error out of GameStart.ContinueFamily.</summary>
    [Test]
    public void ARefusalCodeWithNoStringFallsBackToTheShortReason()
    {
        var store = new UnknownCodeStore(TempStore().Directory);
        var notes = new List<string>();
        LogAssert.Expect(LogType.Warning, new Regex("Family autosave could not be loaded"));
        GameSession? session = null;
        Assert.DoesNotThrow(() => session = Continue(store, notes));
        Assert.IsNull(session);
        Assert.AreEqual(new[] { Unreadable(Text) }, notes.ToArray());
    }

    /// <summary>N4 (R-SAVE-001, R-SAVE-002): a save of other rules (an older ruleset, with no village), or from a newer
    /// version, is refused with the code's own string, in English and in Arabic, and the player reads "a new game has
    /// started" once.</summary>
    [TestCase("ruleset legacy\nvillage -", SaveException.OlderRules, "earlier version of the game's rules")]
    [TestCase("ruleset family-0.2\nvillage tantura", SaveException.NewerVersion, "newer version of the game")]
    public void ARefusedSaveIsSaidOnceInThePlayersLanguage(string ruleset, string code, string english)
    {
        var good = GameStart.SaveText(FamilyKit.Session(seed: 9));
        const string family = "ruleset family-0.1\nvillage tantura";
        StringAssert.Contains("\n" + family + "\n", good);
        foreach (var path in new[] { UiPaths.Strings, UiPaths.StringsArabic })
        {
            var text = UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);
            var store = TempStore();
            store.WriteAutosave(good.Replace(family, ruleset));
            var notes = new List<string>();
            LogAssert.Expect(LogType.Warning, new Regex("Family autosave could not be loaded"));
            Assert.IsNull(Continue(store, notes, text));
            Assert.AreEqual(new[] { text["save.refused." + code] }, notes.ToArray(), path);
        }
        var en = FamilyKit.En["save.refused." + code];
        StringAssert.Contains(english, en);
        Assert.AreEqual(1, Regex.Matches(en, "new game").Count, "said once (N4)");
    }

    /// <summary>#637: a save that loads with warnings (made with other content; and then a replay that ends elsewhere) is
    /// continued, and each warning kind is said in the player's language, English and Arabic, never the sim's English. The
    /// sim returns the kind; the kind goes to the log.</summary>
    [TestCase("strings.en.yaml", "content_changed")]
    [TestCase("strings.ar.yaml", "content_changed")]
    [TestCase("strings.en.yaml", "content_changed", "replay_differs")]
    [TestCase("strings.ar.yaml", "content_changed", "replay_differs")]
    public void ALoadWarningIsSaidInThePlayersLanguage(string file, params string[] kinds)
    {
        var text = UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Folder + "/" + file).text, file);
        var played = PlayedOneMonth(out _);
        var save = GameStart.SaveText(played).Replace("content " + played.Content.Hash, "content " + new string('0', 64));
        if (kinds.Length > 1) save = OtherEnd(save);   // other content, so a replay that ends elsewhere only warns
        var store = TempStore();
        store.WriteAutosave(save);
        var notes = new List<string>();
        foreach (var kind in kinds) LogAssert.Expect(LogType.Warning, "Family autosave loaded with a warning: " + kind);
        var session = Continue(store, notes, text);
        Assert.IsNotNull(session, "continued");
        Assert.AreEqual(1, session!.State.Turn);
        var expected = new List<string>();
        foreach (var kind in kinds) expected.Add(text.Has("save.warning." + kind) ? text["save.warning." + kind] : "<no string save.warning." + kind + ">");
        Assert.AreEqual(expected.ToArray(), notes.ToArray(), file);
    }

    [Test]
    public void AReadThatFailsAndASetAsideThatFailsNeverStopTheStart()
    {
        var store = new FaultyStore(null);
        var notes = new List<string>();
        LogAssert.Expect(LogType.Warning, new Regex("disk gone"));
        GameSession? session = null;
        Assert.DoesNotThrow(() => session = Continue(store, notes));
        Assert.IsNull(session);
        Assert.AreEqual(1, notes.Count);
        Assert.AreEqual("Your saved game could not be loaded (the file could not be read or replayed). A new game has started.", notes[0]);
        StringAssert.DoesNotContain("disk gone", notes[0]);          // raw exception text can carry file paths: it is logged, not shown
        Assert.IsTrue(store.SetAsideCalled);
    }

    [Test]
    public void AnUnparseableSaveWhoseSetAsideFailsNeverStopsTheStartEither()
    {
        var store = new FaultyStore("this is not a save\n");
        var notes = new List<string>();
        LogAssert.Expect(LogType.Warning, new Regex("Family autosave could not be loaded"));
        GameSession? session = null;
        Assert.DoesNotThrow(() => session = Continue(store, notes));
        Assert.IsNull(session);
        Assert.AreEqual(1, notes.Count);
        Assert.AreEqual(Unreadable(Text), notes[0]);
        Assert.IsTrue(store.SetAsideCalled);
    }

    [Test]
    public void ACrashBetweenDeletingAndMovingLosesNothing()
    {
        // WriteAutosave deletes the old file only after the .tmp is completely written; a crash after the delete leaves just the .tmp.
        var store = TempStore();
        var played = FamilyKit.Bridge(seed: 9);
        played.EndTurn(); played.EndTurn();
        Directory.CreateDirectory(store.Directory);
        File.WriteAllText(store.AutosavePath + ".tmp", GameStart.SaveText(played.Session));
        var notes = new List<string>();
        var resumed = Continue(store, notes)!;
        Assert.IsEmpty(notes);
        Assert.AreEqual(2, resumed.State.Turn);
        Assert.AreEqual(played.State.Hash(), resumed.State.Hash());
        Assert.IsTrue(File.Exists(store.AutosavePath), "the .tmp was promoted");
        Assert.IsFalse(File.Exists(store.AutosavePath + ".tmp"));
    }

    [Test]
    public void ALeftOverTmpIsIgnoredWhenTheAutosaveExists()
    {
        var store = TempStore();
        var played = FamilyKit.Bridge(seed: 9);
        played.EndTurn();
        store.WriteAutosave(GameStart.SaveText(played.Session));
        File.WriteAllText(store.AutosavePath + ".tmp", "half a sav");           // a partial write
        var resumed = Continue(store, new List<string>())!;
        Assert.AreEqual(1, resumed.State.Turn);
        Assert.AreEqual(played.State.Hash(), resumed.State.Hash());
    }

    [Test]
    public void ADisposedAutosaveStopsWriting()
    {
        var store = TempStore();
        var bridge = FamilyKit.Bridge(seed: 3);
        var autosave = GameStart.Autosave(bridge, store, Text);
        bridge.EndTurn();
        var before = File.ReadAllText(store.AutosavePath);
        autosave.Dispose();
        bridge.EndTurn();
        Assert.AreEqual(before, File.ReadAllText(store.AutosavePath));
    }

    [Test]
    public void AnOpenPromptIsClearedByAnyChange()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge(seed: 3);
        var toast = new ToastPresenter(ui.Root, bridge, Text);
        int picks = 0;
        var newGame = new NewGamePresenter(ui.Root, bridge, toast, Text, () => picks++);
        var session = bridge.Session;
        ui.Press("new-game");
        Assert.IsTrue(newGame.Armed);
        var id = bridge.State.PendingEvents.First();
        bridge.Queue(new ChooseEventOption(id, bridge.Content.Events.First(e => e.Id == id).Options[0].Id));   // any change: the prompt no longer applies
        Assert.IsFalse(newGame.Armed);
        ui.Press("new-game");
        Assert.AreEqual(0, picks, "one press after a change only arms again");
        Assert.AreSame(session, bridge.Session);
        Assert.IsTrue(newGame.Armed);
        Assert.AreEqual("Press New game again to abandon this game and start over.", ui.Q<Label>("toast").text);
        Assert.AreEqual(1, bridge.Queued.Count);
    }

    [Test]
    public void NewGameNeedsASecondPress()
    {
        using var ui = new UiTestKit();
        var store = TempStore();
        var bridge = FamilyKit.Bridge(seed: 3);
        GameStart.Autosave(bridge, store, Text);
        bridge.EndTurn();
        var saved = File.ReadAllText(store.AutosavePath);
        var toast = new ToastPresenter(ui.Root, bridge, Text);
        int picks = 0;
        var newGame = new NewGamePresenter(ui.Root, bridge, toast, Text, () => picks++);
        ui.Press("new-game");
        Assert.IsTrue(newGame.Armed);
        Assert.AreEqual("Press New game again to abandon this game and start over.", ui.Q<Label>("toast").text);
        Assert.AreEqual(0, picks);
        ui.Press("new-game");
        Assert.AreEqual(1, picks, "the second press opens the picker");
        Assert.IsFalse(newGame.Armed);
        Assert.AreEqual(DisplayStyle.None, ui.Q<Label>("toast").style.display.value, "the prompt is taken down");
        Assert.AreEqual(1, bridge.State.Turn, "the game goes on until a village is picked");
        Assert.AreEqual(saved, File.ReadAllText(store.AutosavePath), "and so does its save");
    }

    [Test]
    public void TheWebSaveFolderDoesNotDependOnThePageUrl()
    {
        // On the web Application.persistentDataPath is /idbfs/<hash of the page URL>; the save folder must not be under it.
        var persistent = "/idbfs/0a1b2c3d4e5f60718293a4b5c6d7e8f9";
        var web = SaveStore.DirectoryFor(RuntimePlatform.WebGLPlayer, persistent);
        Assert.AreEqual(SaveStore.WebDirectory, web);
        StringAssert.DoesNotContain(persistent, web);
        Assert.AreEqual(web, SaveStore.DirectoryFor(RuntimePlatform.WebGLPlayer, "/idbfs/ffffffffffffffffffffffffffffffff"));
        Assert.AreEqual(Path.Combine("/home/x/.config/game", "saves"), SaveStore.DirectoryFor(RuntimePlatform.OSXPlayer, "/home/x/.config/game"));
    }

    [Test]
    public void AFailingAutosaveWriteNeverStopsATurn()
    {
        var file = Path.Combine(Path.GetTempPath(), "storyoftheland-not-a-folder-" + System.Guid.NewGuid().ToString("N"));
        File.WriteAllText(file, "x");                                   // a file where the folder should be
        try
        {
            var bridge = FamilyKit.Bridge(seed: 3);
            var errors = new List<string>();
            GameStart.Autosave(bridge, new SaveStore(file), Text, errors.Add);
            Assert.DoesNotThrow(() => bridge.EndTurn());
            Assert.AreEqual(1, bridge.State.Turn);
            Assert.AreEqual(1, errors.Count);
            StringAssert.StartsWith("Your game could not be saved", errors[0]);
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void ASaveMadeWithOtherContentLoadsWithANote()
    {
        var store = TempStore();
        var c = FamilyKit.Chapter();
        var text = GameStart.SaveText(FamilyKit.Session(seed: 9)).Replace("content " + c.Hash, "content " + new string('0', c.Hash.Length));
        store.WriteAutosave(text);
        var notes = new List<string>();
        LogAssert.Expect(LogType.Warning, "Family autosave loaded with a warning: " + GameSession.ContentChanged);
        var session = Continue(store, notes);
        Assert.AreEqual(new[] { Text["save.warning." + GameSession.ContentChanged] }, notes.ToArray());   // #637
        Assert.IsTrue(File.Exists(store.AutosavePath), "a warning is not a failure: the file is kept until the next turn's autosave replaces it");
        Assert.AreEqual(0, session!.State.Turn);
    }

    [Test]
    public void ArmingIsCancelledByATurnAndADisposedButtonDoesNothing()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge(seed: 3);
        var toast = new ToastPresenter(ui.Root, bridge, Text);
        int picks = 0;
        var newGame = new NewGamePresenter(ui.Root, bridge, toast, Text, () => picks++);
        ui.Press("new-game");
        Assert.IsTrue(newGame.Armed);
        bridge.EndTurn();
        Assert.IsFalse(newGame.Armed);
        ui.Press("new-game");
        Assert.AreEqual(0, picks, "one press after a turn only arms again");
        newGame.Dispose();
        ui.Press("new-game"); ui.Press("new-game");
        Assert.AreEqual(0, picks);
    }

    [Test]
    public void ALingeringTmpIsSetAsideWhenThereIsNoAutosave()
    {
        // Promotion failed and the .tmp cannot be read: autosave.txt is missing, so the .tmp itself must be set aside,
        // or it would be found, rejected and noted on every start.
        var store = TempStore();
        Directory.CreateDirectory(store.Directory);
        File.WriteAllText(store.AutosavePath + ".tmp", "half a sav");
        store.SetAsideAutosave();
        Assert.IsFalse(File.Exists(store.AutosavePath + ".tmp"));
        Assert.IsTrue(File.Exists(Path.Combine(store.Directory, "autosave.bad.txt")));
        var notes = new List<string>();
        Assert.IsNull(Continue(store, notes));
        Assert.IsEmpty(notes, "nothing is left to reject on the next start");
    }

    [Test]
    public void ABrokenFirstWriteEndsSafely()
    {
        // A crash in the middle of the very first write leaves a partial .tmp and no autosave.txt.
        var store = TempStore();
        var good = GameStart.SaveText(FamilyKit.Session(seed: 9));
        Directory.CreateDirectory(store.Directory);
        File.WriteAllText(store.AutosavePath + ".tmp", good.Substring(0, good.Length / 2));
        var notes = new List<string>();
        LogAssert.Expect(LogType.Warning, new Regex("Family autosave could not be loaded"));
        Assert.IsNull(Continue(store, notes));
        Assert.AreEqual(1, notes.Count);
        Assert.IsFalse(File.Exists(store.AutosavePath + ".tmp"));
        Assert.IsFalse(File.Exists(store.AutosavePath));
        Assert.IsTrue(File.Exists(Path.Combine(store.Directory, "autosave.bad.txt")));
    }

    // --- A new family game replaces the save (GameRoot.StartFamily) -----------------------------------------------------

    /// <summary>Map.unity with the map built and the saves in <paramref name="store"/> (its Family folder).</summary>
    static GameRoot Scene(SaveStore store)
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(FamilyKit.Inputs.Map);
        var root = Object.FindFirstObjectByType<GameRoot>();
        root.Store = store;
        return root;
    }

    /// <summary>A family store whose old autosave cannot be deleted (a locked file).</summary>
    sealed class DeleteFailsStore : SaveStore
    {
        public DeleteFailsStore(string dir) : base(dir) { }
        public override SaveStore Family => this;
        public override void DeleteAutosave() => throw new IOException("locked");
    }

    [Test]
    public void AFailedDeleteStillStartsTheNewGame()
    {
        var store = new DeleteFailsStore(TempStore().Directory);
        var old = FamilyKit.Bridge(seed: 3);
        old.EndTurn();
        store.WriteAutosave(GameStart.SaveText(old.Session));
        var root = Scene(store);
        LogAssert.Expect(LogType.Warning, new Regex("Family autosave could not be deleted"));
        Assert.DoesNotThrow(() => root.StartFamily(FamilyKit.Inputs, "1967_1987", "imwas"));
        Assert.AreEqual(0, root.Bridge!.State.Turn, "the new game started");
        Assert.AreEqual("1967_1987", FamilyGame.ChapterOf(root.Bridge.Content));
        Assert.AreEqual(GameStart.SaveText(root.Bridge.Session), File.ReadAllText(store.AutosavePath), "its turn 0 replaced the old save");
    }

    [Test]
    public void NewGameAlsoRemovesALingeringTmp()
    {
        var store = TempStore();
        var played = FamilyKit.Bridge(seed: 3);
        played.EndTurn();
        var family = store.Family;
        Directory.CreateDirectory(family.Directory);
        File.WriteAllText(family.AutosavePath + ".tmp", GameStart.SaveText(played.Session));      // a complete save, only the .tmp
        var root = Scene(store);
        root.StartFamily(FamilyKit.Inputs, GameRoot.AutomatedChapter, GameRoot.AutomatedVillage);
        Assert.IsFalse(File.Exists(family.AutosavePath + ".tmp"));
        var notes = new List<string>();
        Assert.AreEqual(0, Continue(family, notes)!.State.Turn, "the next start continues the new game, not the abandoned one");
        Assert.IsEmpty(notes);
    }
}
