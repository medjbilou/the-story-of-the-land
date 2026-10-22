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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>The Arabic UI (#193): asked for by hand only ("?lang=ar", "-lang ar"), English otherwise; when asked, GameRoot
/// loads strings.ar.yaml and marks the root, and every text is set in the Arabic face through the advanced text
/// generator.</summary>
public class ArabicUiTests
{
    static GameInputs Inputs => FamilyKit.Inputs;
    static UiText Ar => FamilyKit.Ar;
    static UiText En => FamilyKit.En;

    string saveDir = "";

    [SetUp] public void TempSaves() => saveDir = Path.Combine(Path.GetTempPath(), "storyoftheland-arabic-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void DiscardScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (Directory.Exists(saveDir)) Directory.Delete(saveDir, true);
    }

    // --- The flag ---------------------------------------------------------------------------------------------------

    [Test]
    public void ArabicIsOnlyAskedForByHand()
    {
        var none = new string[0];
        foreach (var q in new[] { "?lang=ar", "?golden&lang=ar", "?lang=ar&smoke=5", "?lang=ar#top" })
            Assert.AreEqual(UiLanguage.Arabic, UiLanguage.Requested("http://localhost:8000/index.html" + q, none, webgl: true), q);
        foreach (var q in new[] { "", "?lang", "?lang=", "?lang=en", "?lang=arx", "?lang=AR", "?slang=ar", "?lang=he", "?lang=fr", "?golden" })
            Assert.AreEqual(UiLanguage.English, UiLanguage.Requested("http://localhost:8000/index.html" + q, none, webgl: true), q);
        Assert.AreEqual(UiLanguage.English, UiLanguage.Requested(null, none, webgl: true));
        Assert.AreEqual(UiLanguage.Arabic, UiLanguage.Requested(null, new[] { "-batchmode", "-lang", "ar" }, webgl: false));
        foreach (var args in new[] { none, new[] { "-lang" }, new[] { "-lang", "en" }, new[] { "-lang", "-smoke" }, new[] { "ar" }, new[] { "-language", "ar" }, new[] { "-lang", "he" } })
            Assert.AreEqual(UiLanguage.English, UiLanguage.Requested(null, args, webgl: false), string.Join(" ", args));
        // A flag on the desktop does not count on the web and the other way round (each instrument's own rule).
        Assert.AreEqual(UiLanguage.English, UiLanguage.Requested("http://localhost/index.html", new[] { "-lang", "ar" }, webgl: true));
        Assert.AreEqual(UiLanguage.English, UiLanguage.Requested("http://localhost/index.html?lang=ar", none, webgl: false));
    }

    // --- The game ---------------------------------------------------------------------------------------------------

    GameRoot Boot(string? language)
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var root = Object.FindFirstObjectByType<GameRoot>();
        root.Store = new SaveStore(saveDir);
        root.Language = language;
        root.Begin(Inputs, 0);
        return root;
    }

    static VisualElement Ui => Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

    [Test]
    public void TheGameIsEnglishUnlessArabicIsAskedFor()
    {
        var game = Boot(null);                                         // the test run's command line has no "-lang ar"
        Assert.IsFalse(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass));
        Assert.AreEqual(En.Format("hud.turn", game.Bridge!.State.Turn), Ui.Q<Label>("turn").text);
        Assert.IsTrue(Ui.Query<TextElement>().ToList().All(t => t.ClassListContains("start-ar") || t.languageDirection == LanguageDirection.Inherit),
            "English sets no paragraph direction");
    }

    [Test]
    public void AskedForArabicTheGameLoadsTheArabicStringsAndMarksTheRoot()
    {
        var game = Boot(UiLanguage.Arabic);
        Assert.IsTrue(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass));
        Assert.AreEqual(Ar.Format("hud.turn", game.Bridge!.State.Turn), Ui.Q<Label>("turn").text);
        StringAssert.Contains("الدور", Ui.Q<Label>("turn").text);
        // New Game in Arabic disposes the old UiDirection and makes a new one; the start screen's Arabic lines stay right to
        // left through it (neither takes theirs off nor sets it).
        game.Begin(Inputs, 0);
        TheDeclarationStaysRightToLeft("an Arabic restart");
        // Starting again in English takes the mark and every other direction off (New Game restarts through Begin).
        game.Language = UiLanguage.English;
        game.Begin(Inputs, 0);
        Assert.IsFalse(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass));
        Assert.AreEqual(En.Format("hud.turn", game.Bridge!.State.Turn), Ui.Q<Label>("turn").text);
        Assert.IsTrue(Ui.Query<TextElement>().ToList().All(t => t.ClassListContains("start-ar") || t.languageDirection == LanguageDirection.Inherit));
        TheDeclarationStaysRightToLeft("an English restart");
    }

    /// <summary>The language switch in Settings (#1036): a press rebuilds the UI in the other language at once, over the same
    /// game (this turn's orders kept), with Settings still open on the new choice; the choice is kept in settings.txt, so
    /// the next start (Language unset, as a reload leaves it) comes up in it.</summary>
    [Test]
    public void TheLanguageSwitchInSettingsChangesTheUiAtOnceAndIsKept()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var game = Object.FindFirstObjectByType<GameRoot>();
        game.Store = new SaveStore(saveDir);
        var settings = new SettingsStore(Path.Combine(saveDir, "settings"));
        game.Settings = settings;
        game.Begin(Inputs, 0);                                         // no file and no flag: English
        Assert.IsFalse(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass));
        Assert.IsTrue(Ui.Q<Button>("settings-language-en").ClassListContains(SettingsPresenter.SelectedClass), "English is marked");
        Assert.AreEqual("English", Ui.Q<Button>("settings-language-en").text);
        Assert.AreEqual("العربية", Ui.Q<Button>("settings-language-ar").text, "each language is named in itself");
        var session = game.Bridge!.Session;
        var card = game.Bridge.Content.Events.First(e => e.Id == game.Bridge.State.PendingEvents[0]);
        Assert.IsNull(game.Bridge.Queue(new StoryOfTheLand.Sim.ChooseEventOption(card.Id, card.Options[0].Id)));

        UiTestKit.Press(Ui.Q<Button>("settings-language-ar"));
        Assert.IsTrue(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass), "Arabic at once, no reload");
        Assert.AreEqual(Ar.Format("hud.turn", game.Bridge!.State.Turn), Ui.Q<Label>("turn").text);
        Assert.AreSame(session, game.Bridge.Session, "the same game goes on");
        Assert.AreEqual(1, game.Bridge.Queued.Count, "this turn's order is kept");
        Assert.IsTrue(ScreenNav.IsOpen(Ui.Q("screen-settings")), "Settings stays open");
        Assert.IsTrue(Ui.Q<Button>("settings-language-ar").ClassListContains(SettingsPresenter.SelectedClass), "Arabic is marked");
        Assert.IsFalse(Ui.Q<Button>("settings-language-en").ClassListContains(SettingsPresenter.SelectedClass));
        Assert.AreEqual(UiLanguage.Arabic, settings.Read(SettingsPresenter.LanguageKey), "kept in settings.txt");

        game.Language = null;                                          // a reload: only the file remains
        game.Begin(Inputs, 0);
        Assert.IsTrue(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass), "Arabic after a reload");

        UiTestKit.Press(Ui.Q<Button>("settings-language-en"));
        Assert.IsFalse(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass), "and English again at once");
        Assert.AreEqual(En.Format("hud.turn", game.Bridge!.State.Turn), Ui.Q<Label>("turn").text);
        Assert.AreEqual(UiLanguage.English, settings.Read(SettingsPresenter.LanguageKey));
    }

    /// <summary>A language named in the address or on the command line (the store page's "?lang=ar" link) holds for that
    /// visit over the stored choice; without one the stored choice holds; with neither, English.</summary>
    [Test]
    public void AFlagWinsOverTheStoredLanguage()
    {
        const string page = "http://localhost:8000/index.html";
        var none = new string[0];
        Assert.AreEqual(UiLanguage.English, UiLanguage.AtStart(null, page, none, webgl: true));
        Assert.AreEqual(UiLanguage.Arabic, UiLanguage.AtStart(UiLanguage.Arabic, page, none, webgl: true));
        Assert.AreEqual(UiLanguage.Arabic, UiLanguage.AtStart(UiLanguage.Arabic, page + "?golden", none, webgl: true));
        Assert.AreEqual(UiLanguage.Arabic, UiLanguage.AtStart(UiLanguage.English, page + "?lang=ar", none, webgl: true));
        Assert.AreEqual(UiLanguage.English, UiLanguage.AtStart(UiLanguage.Arabic, page + "?lang=en", none, webgl: true));
        Assert.AreEqual(UiLanguage.English, UiLanguage.AtStart("fr", page, none, webgl: true), "a value the game does not know");
        Assert.AreEqual(UiLanguage.Arabic, UiLanguage.AtStart(UiLanguage.Arabic, null, none, webgl: false));
        Assert.AreEqual(UiLanguage.Arabic, UiLanguage.AtStart(null, null, new[] { "-lang", "ar" }, webgl: false));
        Assert.AreEqual(UiLanguage.English, UiLanguage.AtStart(UiLanguage.Arabic, null, new[] { "-lang", "en" }, webgl: false));
    }

    static void TheDeclarationStaysRightToLeft(string after)
    {
        var lines = Ui.Query<TextElement>(className: "start-ar").ToList();
        Assert.IsNotEmpty(lines, "Game.uxml's Arabic start lines");
        foreach (var t in lines) Assert.AreEqual(LanguageDirection.RTL, t.languageDirection, $"after {after}: '{t.name}'");
    }

    // --- The direction ----------------------------------------------------------------------------------------------

    [Test]
    public void ATextIsRightToLeftWhenItsFirstLetterIsArabic()
    {
        foreach (var s in new[] { "الدور 1", "5  حرب شاملة", "الائتلاف: 30 من 37", "‏…نحن", "العربية" })
            Assert.IsTrue(UiDirection.RightToLeft(s), s);
        foreach (var s in new[] { "15 May 1948", "IL 20.0m", "+5%", "-5%", "30%", "", "David Ben-Gurion", "Mapai :حزب", "$40.0m", "1948" })
            Assert.IsFalse(UiDirection.RightToLeft(s), s);
        Assert.IsFalse(UiDirection.RightToLeft(null));
    }

    /// <summary>In Arabic each text takes its paragraph direction from its own words: the Arabic strings right to left, the
    /// numbers left to right (so "1948" is not reordered), the date in Arabic right to left (#687), and a text that changes follows.</summary>
    [Test]
    public void InArabicEachTextTakesItsDirectionFromItsOwnWords()
    {
        var game = Boot(UiLanguage.Arabic);
        Assert.AreEqual(LanguageDirection.RTL, Ui.Q<Label>("turn").languageDirection, "turn: Arabic");
        Assert.AreEqual(LanguageDirection.RTL, Ui.Q<Label>("date").languageDirection, "the date is in Arabic (#687)");
        Assert.AreEqual(LanguageDirection.LTR, Ui.Q<Label>("year").languageDirection, "digits alone");
        // A text that sets its own direction (the start screen's, an Arabic credit that opens with a Latin name) is left alone.
        foreach (var t in Ui.Query<TextElement>().ToList().Where(t => !t.ClassListContains("start-ar") && !t.ClassListContains(UiDirection.OwnDirectionClass)))
            Assert.AreEqual(UiDirection.RightToLeft(t.text) ? LanguageDirection.RTL : LanguageDirection.LTR, t.languageDirection, $"'{t.name}': {t.text}");
        // A text that changes takes its new direction at once.
        var turn = Ui.Q<Label>("turn");
        turn.text = "Turn 7";
        Assert.AreEqual(LanguageDirection.LTR, turn.languageDirection);
        turn.text = "الدور 7";
        Assert.AreEqual(LanguageDirection.RTL, turn.languageDirection);
        // After a turn the texts the turn rewrote keep the direction of their new words.
        game.Bridge!.EndTurn();
        Assert.AreEqual(LanguageDirection.RTL, turn.languageDirection, turn.text);
        Assert.AreEqual(LanguageDirection.RTL, Ui.Q<Label>("date").languageDirection, Ui.Q<Label>("date").text);
    }

    /// <summary>Many texts a presenter builds after boot get their words before they are on the panel (new Label(text)), so
    /// no text change reaches UiDirection: they take their direction when it walks the tree again, on the next frame after
    /// a submit, a click or a turn. Here: a label added before a submit, one added before a click, and the Why rows End
    /// Turn rebuilds (the month's cards, the family's choices and changes).</summary>
    [UnityTest]
    public IEnumerator InArabicTextsBuiltAfterBootTakeTheirDirectionAfterASubmitAClickOrATurn()
    {
        var restore = OnTexture(1920, 1080);
        try
        {
            var game = Object.FindFirstObjectByType<GameRoot>();
            game.Store = new SaveStore(saveDir);
            game.Language = UiLanguage.Arabic;
            game.Begin(Inputs, 0);
            for (int i = 0; i < 2; i++) yield return null;

            // A submit and a click: a label added with its words already set has no direction until the next frame.
            var submitted = new Label(Ar["why.family.unanswered"]);
            Ui.Q("root").Add(submitted);
            Assert.AreEqual(LanguageDirection.Inherit, submitted.languageDirection);
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = submitted; submitted.SendEvent(e); }
            for (int i = 0; i < 2; i++) yield return null;
            Directed(new TextElement[] { submitted }, "a submit");
            var clicked = new Label("15 May 1948");
            Ui.Q("root").Add(clicked);
            Assert.AreEqual(LanguageDirection.Inherit, clicked.languageDirection);
            using (var e = ClickEvent.GetPooled()) { e.target = clicked; clicked.SendEvent(e); }
            for (int i = 0; i < 2; i++) yield return null;
            Directed(new TextElement[] { clicked }, "a click");

            // A turn: End Turn rebuilds the Why rows.
            game.Bridge!.EndTurn();
            var built = Ui.Q<ScrollView>("why-rows").contentContainer.Query<TextElement>().ToList();
            Assert.IsTrue(built.All(t => t.languageDirection == LanguageDirection.Inherit), "the new rows have no direction before the next frame");
            for (int i = 0; i < 2; i++) yield return null;
            Directed(built, "a turn");
            Assert.IsTrue(built.Any(t => t.languageDirection == LanguageDirection.RTL), "the Arabic headers are right to left");
        }
        finally { restore(); }
    }

    static void Directed(IList<TextElement> texts, string after)
    {
        Assert.IsNotEmpty(texts, after);
        foreach (var t in texts)
            Assert.AreEqual(UiDirection.RightToLeft(t.text) ? LanguageDirection.RTL : LanguageDirection.LTR, t.languageDirection, $"after {after}: '{t.name}': {t.text}");
    }

    /// <summary>Opens the map scene with its panel drawn into a texture of this size; the action it returns puts the panel back.</summary>
    static Action OnTexture(int width, int height)
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var doc = Object.FindFirstObjectByType<UIDocument>();
        var rt = new RenderTexture(width, height, 0);
        var ps = Object.Instantiate(doc.panelSettings);
        ps.targetTexture = rt;
        doc.panelSettings = ps;
        return () => { doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); };
    }

    // --- The static texts ---------------------------------------------------------------------------------------------

    /// <summary>Every text Game.uxml writes itself that a player reads, and its string (#193). "a/b" is the element of class b
    /// inside the element named a. The presenter that owns each element sets it from the UI's strings at bind time.</summary>
    static readonly (string where, string key)[] StaticTexts =
    {
        ("loading", "boot.loading"),
        ("nav-why", "hud.nav.why"), ("nav-how-to-play", "hud.nav.how_to_play"),
        ("new-game", "hud.new_game"), ("end-turn", "hud.end_turn"),
        ("why-screen-title", "why.title"),
        ("how-to-play-title", "how_to_play.title"),
        // Settings' rows and tabs are made in code since F6 (SettingsPresenter; SettingsTabsTests checks them in Arabic).
        ("nav-settings", "hud.nav.settings"), ("settings-screen-title", "settings.title"),
        ("start-source", "start.source"),
        ("start-continue", "start.continue"), ("start-new-game", "start.new_game"), ("start-how-to-play", "hud.nav.how_to_play"),
    };

    /// <summary>Static texts that stay as Game.uxml writes them in every language: the game's English name under its Arabic
    /// one, and the resolution's English under each Arabic line. ("-5%" and "+5%" have no letters and need no string.)</summary>
    static readonly string[] NotTranslated = { "start-subtitle", "start-en-proclamation", "start-en-date" };

    static TextElement Static(VisualElement ui, string where)
    {
        var parts = where.Split('/');
        var e = parts.Length == 1 ? ui.Q<TextElement>(where) : ui.Q(parts[0])?.Q<TextElement>(className: parts[1]);
        Assert.IsNotNull(e, where);
        return e!;
    }

    /// <summary>English shows what it always showed: each English string is the text Game.uxml writes, and every text in
    /// Game.uxml with a letter in it is either one of StaticTexts, deliberately not translated, or a start line in Arabic
    /// (an English text with the start-ar class is not exempt). Game.uxml writes no tooltip: one with a letter would need
    /// a string too. Texts C# writes itself are not read here.</summary>
    [Test]
    public void EachEnglishStaticTextIsTheUxmlsOwnText()
    {
        using var ui = new UiTestKit();
        var listed = new HashSet<TextElement>();
        foreach (var (where, key) in StaticTexts)
        {
            var e = Static(ui.Root, where);
            Assert.AreEqual(e.text, En[key], $"{where}: {key}");
            listed.Add(e);
        }
        foreach (var name in NotTranslated) listed.Add(ui.Q<TextElement>(name));
        // Playtest 3: the start screen's toggle names each language in its own script, in both UIs.
        listed.Add(ui.Q<TextElement>("start-language-en")); listed.Add(ui.Q<TextElement>("start-language-ar"));
        var unlisted = ui.Root.Query<TextElement>().ToList()
            .Where(t => !listed.Contains(t) && !(t.ClassListContains("start-ar") && UiDirection.RightToLeft(t.text)) && (t.text ?? "").Any(char.IsLetter))
            .Select(t => $"'{t.name}' ({string.Join(" ", t.GetClasses())}): {t.text}").ToList();
        Assert.IsEmpty(unlisted, "a text Game.uxml writes that no string sets");
        var tooltips = ui.Root.Query<VisualElement>().ToList().Where(v => (v.tooltip ?? "").Any(char.IsLetter))
            .Select(v => $"'{v.name}': tooltip {v.tooltip}").ToList();
        Assert.IsEmpty(tooltips, "a tooltip Game.uxml writes that no string sets");
    }

    /// <summary>In Arabic every static text shows its strings.ar.yaml words, with no Latin letter, and takes
    /// its direction like any other text; an English restart puts the English back.</summary>
    [Test]
    public void InArabicEveryStaticTextIsTheArabicString()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var game = Object.FindFirstObjectByType<GameRoot>();
        game.Store = new SaveStore(saveDir);
        game.Language = UiLanguage.Arabic;
        game.Begin(Inputs, 0, startScreen: true);                       // the start screen sets its own texts
        var latin = new Regex("[A-Za-z]");
        foreach (var (where, key) in StaticTexts)
        {
            var e = Static(Ui, where);
            Assert.AreEqual(Ar[key], e.text, $"{where}: {key}");
            // No Latin letters.
            Assert.IsFalse(latin.IsMatch(e.text), $"{where}: Latin letters in '{e.text}'");
            Assert.AreEqual(LanguageDirection.RTL, e.languageDirection, $"{where}: {e.text}");
        }
        foreach (var name in NotTranslated) Assert.AreEqual(LanguageDirection.LTR, Ui.Q<TextElement>(name).languageDirection, name);

        game.Language = UiLanguage.English;
        game.Begin(Inputs, 0, startScreen: true);
        foreach (var (where, key) in StaticTexts)
        {
            var e = Static(Ui, where);
            Assert.AreEqual(En[key], e.text, $"after an English restart: {where}");
            Assert.AreEqual(LanguageDirection.Inherit, e.languageDirection, $"after an English restart: {where}");
        }
    }

    /// <summary>F5 step 6: in Arabic every map name pill shows its row's name_ar (content/map/names_ar.yaml, inline in each
    /// map file), the English name only where a place has none.</summary>
    [Test]
    public void InArabicEachMapPillShowsItsArabicName()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>();
        map.Build(Inputs.Map);
        var game = Object.FindFirstObjectByType<GameRoot>();
        game.Store = new SaveStore(saveDir);
        game.Language = UiLanguage.Arabic;
        game.Begin(Inputs, 0);
        Assert.AreEqual("موشاف نهلال", Ui.Q<Label>("label-nahalal").text);
        static string Ar(string ar, string en) => ar.Length > 0 ? ar : en;
        foreach (var s in map.Settlements) Assert.AreEqual(Ar(s.NameAr, s.Name), Ui.Q<Label>("label-" + s.Id).text, s.Id);
        foreach (var p in map.Localities.Where(p => map.Settlements.All(s => s.Id != p.Settlement.Id))) Assert.AreEqual(p.NameAr ?? p.Settlement.Name, Ui.Q<Label>("label-" + p.Settlement.Id).text, p.Settlement.Id);
    }

    /// <summary>The loading line is in the UI's language from the first frame, before Begin, in the Arabic face and right to
    /// left; a boot failure keeps both. English keeps its line, its face and no direction.</summary>
    [Test]
    public void TheLoadingLineIsInTheUisLanguageBeforeTheGameStarts()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var game = Object.FindFirstObjectByType<GameRoot>();
        var line = Ui.Q<Label>("loading");
        game.Language = UiLanguage.Arabic;
        Assert.IsNotNull(game.ShowLoading());
        Assert.AreEqual(Ar["boot.loading"], line.text);
        Assert.IsTrue(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass), "the Arabic face before Begin");
        Assert.AreEqual(LanguageDirection.RTL, line.languageDirection);
        LogAssert.Expect(LogType.Error, "BOOT FAIL palestine/figures.yaml: figures[x].name: required");
        Object.FindFirstObjectByType<GameBoot>().Fail("palestine/figures.yaml: figures[x].name: required");
        StringAssert.StartsWith("تعذّر تشغيل", line.text);
        Assert.IsTrue(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass));
        Assert.AreEqual(LanguageDirection.RTL, line.languageDirection);

        game.Language = UiLanguage.English;
        game.ShowLoading();
        Assert.AreEqual(En["boot.loading"], line.text);
        Assert.AreEqual("Loading the map of 1948…", line.text);
        Assert.IsFalse(Ui.Q("root").ClassListContains(UiLanguage.ArabicClass));
        Assert.AreEqual(LanguageDirection.Inherit, line.languageDirection);
    }

    /// <summary>A boot failure stays on the loading line whichever of GameBoot.Start and GameRoot.Start runs first (neither
    /// sets an execution order). On the desktop the load reads from disk, so its failure lands inside GameBoot.Start
    /// (GameSceneTests.AFailingLoadIsCaughtAndShownWithoutLocalPaths drives the same throw); GameRoot's Start must not
    /// write the loading line over it (M3 final review I5).</summary>
    [Test]
    public void ABootFailureStaysOnTheLoadingLineInEitherStartOrder([Values(UiLanguage.English, UiLanguage.Arabic)] string language)
    {
        foreach (var failFirst in new[] { true, false })
        {
            EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<GameRoot>();
            var boot = Object.FindFirstObjectByType<GameBoot>();
            game.Language = language;
            var order = failFirst ? "GameBoot.Start first" : "GameRoot.Start first";
            if (!failFirst) game.Started();
            LogAssert.Expect(LogType.Error, new Regex("BOOT FAIL Could not find file"));
            LogAssert.Expect(LogType.Exception, new Regex("Could not find file"));
            _ = boot.BootAsync(() => throw new IOException("Could not find file '/Users/someone/The Story of the Land.app/Contents/Resources/Data/StreamingAssets/map/heightmap.json'."));
            if (failFirst) game.Started();
            var t = language == UiLanguage.Arabic ? Ar : En;
            Assert.AreEqual(t.Format("boot.failed", t["boot.reason_other"]), Ui.Q<Label>("loading").text, $"{language}, {order}");   // #637
            Assert.AreEqual(language == UiLanguage.Arabic, Ui.Q("root").ClassListContains(UiLanguage.ArabicClass), $"{language}, {order}: the face");
        }
    }

    // --- The face ---------------------------------------------------------------------------------------------------

    /// <summary>The classes a rule of StoryOfTheLand.uss sets in EB Garamond (".year", ".why-group"); type selectors are left out.</summary>
    static string[] GaramondClasses()
    {
        var uss = Regex.Replace(File.ReadAllText(UiPaths.Folder + "/StoryOfTheLand.uss"), @"/\*.*?\*/", "", RegexOptions.Singleline);
        return Regex.Matches(uss, @"([^{}]+)\{([^{}]*)\}").Cast<Match>().Where(m => m.Groups[2].Value.Contains("EBGaramond"))
            .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"\.([\w-]+)").Cast<Match>().Select(c => c.Groups[1].Value)).Distinct().ToArray();
    }

    /// <summary>With .lang-ar on the root every text element of Game.uxml resolves Noto Naskh Arabic and the advanced
    /// generator, whatever rule styles it; without it (the control) the same elements resolve EB Garamond. So do labels
    /// with the classes only presenters add (the Why headers, the person cards, the map labels).</summary>
    [UnityTest]
    public IEnumerator InArabicEveryTextIsSetInTheArabicFaceThroughTheAdvancedGenerator()
    {
        var rt = new RenderTexture(1920, 1080, 0);
        var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
        ps.targetTexture = rt;
        var go = new GameObject("ArabicUiTest");
        go.SetActive(false);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = ps;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
        go.SetActive(true);
        // Settings' rows and tabs are made in code (F6): its presenter makes them, so they are checked too.
        var settings = new SettingsPresenter(doc.rootVisualElement, En, null);
        try
        {
            var arabic = AssetDatabase.LoadAssetAtPath<Font>(UiPaths.FontArabic);
            Assert.IsNotNull(arabic, UiPaths.FontArabic);
            var root = doc.rootVisualElement.Q("root");
            static Font? FontOf(TextElement e)
            {
                var d = e.resolvedStyle.unityFontDefinition;
                return d.fontAsset != null ? d.fontAsset.sourceFontFile : d.font;
            }
            // A class a rule sets in EB Garamond that no element of Game.uxml carries is added at runtime: a label carries it.
            var classes = GaramondClasses();
            Assert.Contains("why-group", classes, "the rules are read");
            foreach (var c in classes.Where(c => root.Q(className: c) == null))
            {
                var l = new Label("ا") { name = "runtime-" + c };
                l.AddToClassList(c);
                root.Add(l);
            }
            // The start screen's Arabic lines are Arabic in both languages (the resolution's own text and the title), and so is
            // Settings' button that names Arabic in Arabic (#1036).
            var texts = root.Query<TextElement>().ToList().Where(e => !e.ClassListContains("start-ar") && e.name != "settings-language-ar" && e.name != "start-language-ar").ToList();
            Assert.Greater(texts.Count, 30, "Game.uxml's labels and buttons");
            for (int i = 0; i < 3; i++) yield return null;             // resolved styles need a style pass
            foreach (var e in texts)
                Assert.AreNotSame(arabic, FontOf(e), $"English: '{e.name}' ({string.Join(" ", e.GetClasses())})");
            Assert.AreSame(arabic, FontOf(root.Q<TextElement>("settings-language-ar")), "English: 'العربية' has the Arabic face");
            Assert.AreSame(arabic, FontOf(root.Q<TextElement>("start-language-ar")), "English: 'عربي' has the Arabic face");
            root.AddToClassList(UiLanguage.ArabicClass);
            for (int i = 0; i < 3; i++) yield return null;
            foreach (var e in texts)
            {
                var what = $"'{e.name}' ({string.Join(" ", e.GetClasses())})";
                Assert.AreSame(arabic, FontOf(e), what);
                Assert.AreEqual("Advanced", e.resolvedStyle.unityTextGenerator.ToString(), what);
            }
        }
        finally { settings.Dispose(); Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); }
    }
}
