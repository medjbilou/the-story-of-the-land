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
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>F5 (docs/f5-unity-integration.md): the family game on screen, step by step. 1: the sim plugin has the family
/// API. 2: content/palestine ships through the manifest and the bundle. 3: the start screen's New game leads through the
/// chapter and village picker to the family game, Continue loads its save, and the automated path plays the default game.
/// 4: the family's top bar. 5: its cards. 6: its map. 7: the People screen. 9: Credits.</summary>
public class FamilyGameTests
{
    static GameInputs? inputs;
    static GameInputs Inputs => inputs ??= GameInputs.FromDisk(Paths.Content, Paths.StreamingMap);
    static UiText Ar => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.StringsArabic).text, "strings.ar.yaml");
    static UiText En => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Strings).text);
    static readonly string[] ChapterIds = FamilyGame.Chapters.Select(c => c.Id).ToArray();

    string saveDir = "";
    [SetUp] public void TempSaves() => saveDir = Path.Combine(Path.GetTempPath(), "storyoftheland-family-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void DiscardScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (Directory.Exists(saveDir)) Directory.Delete(saveDir, true);
    }

    static VisualElement Ui => Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

    /// <summary>The map scene with a temporary save folder and the UI in <paramref name="language"/>.</summary>
    GameRoot Scene(string language)
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var root = Object.FindFirstObjectByType<GameRoot>();
        root.Store = new SaveStore(saveDir);
        root.Language = language;
        return root;
    }

    /// <summary>A family game started as the picker starts one.</summary>
    GameRoot Family(string language, string chapter, string village)
    {
        var root = Scene(language);
        root.StartFamily(Inputs, chapter, village);
        return root;
    }

    // --- Step 1: the plugin -------------------------------------------------------------------------------------------

    [Test]
    public void ThePluginPlaysAFamilyChapterAndSavesItsVillage()
    {
        var c = Inputs.Chapter("1947_1949");
        var g = GameSession.New(c, 7, "tantura");
        Assert.AreEqual("tantura", g.State.Village);
        Assert.AreEqual(SaveGame.FamilyRuleset, c.Ruleset);
        var save = g.Save.Write();
        StringAssert.Contains("village tantura", save);
        var back = GameSession.Load(c, save, out var warnings);
        Assert.IsEmpty(warnings);
        Assert.AreEqual(g.State.Hash(), back.State.Hash());
        StringAssert.Contains("ج.ف", PlayerText.Currency(c, g.State, true));
        Assert.AreEqual("1 " + PlayerText.LevantineMonths[g.State.Date.Month - 1] + " " + g.State.Date.Year, PlayerText.Date(g.State.Date, true));
    }

    // --- Step 2: the content ------------------------------------------------------------------------------------------

    [Test]
    public void ThePalestineContentShipsInTheManifestAndTheBundle()
    {
        var files = ContentManifest.Collect(Paths.Content);
        foreach (var f in new[] { "model.yaml", "places.yaml", "fates.yaml", "currency.yaml", "figures.yaml", "credits.yaml" })
            CollectionAssert.Contains(files, "palestine/" + f);
        foreach (var id in ChapterIds) CollectionAssert.Contains(files, "palestine/events/" + id + ".yaml");
        var (_, texts) = ContentManifest.ParseBundle(ContentManifest.WriteBundle(Paths.Content, files));
        var packed = GameInputs.FromTexts(texts, "", new byte[0]);
        foreach (var id in ChapterIds)
        {
            var disk = Inputs.Chapter(id);
            var fetched = packed.Chapter(id);
            Assert.AreEqual(disk.Hash, fetched.Hash, id + ": the bundle's chapter is the disk's");
            Assert.AreEqual(disk.Events.Count, fetched.Events.Count, id);
            Assert.Greater(disk.Places.Count, 200, id);
        }
        Assert.AreSame(Inputs.Chapter("1947_1949"), Inputs.Chapter("1947_1949"), "a chapter is loaded once");
    }

    // --- Step 3: a new game -------------------------------------------------------------------------------------------

    [Test]
    public void NewGameLeadsThroughTheChapterAndVillagePickerToTheFamilyGame([Values("en", "ar")] string language)
    {
        var t = language == "ar" ? Ar : En;
        var root = Scene(language);
        root.Begin(Inputs, 0, startScreen: true);
        Assert.IsNotNull(root.Bridge!.Content, "no family save: the default family game waits under the start screen");
        Assert.AreEqual(GameRoot.AutomatedChapter, FamilyGame.ChapterOf(root.Bridge.Content));
        Assert.AreEqual(GameRoot.AutomatedVillage, root.Bridge.State.Village);
        Assert.IsFalse(File.Exists(new SaveStore(saveDir).Family.AutosavePath), "nothing saved before a village is picked");
        Assert.AreEqual(DisplayStyle.None, Ui.Q<Button>("start-continue").style.display.value, "nothing to continue");
        UiTestKit.Press(Ui.Q<Button>("start-new-game"));
        var picker = root.Picker!;
        Assert.IsTrue(picker.IsOpen, "New game opens the picker");
        Assert.IsTrue(Ui.Q("root").ClassListContains(StartScreenPresenter.OpenClass), "over the start screen");
        var chapters = Ui.Query<Button>(className: FamilyPickerPresenter.ChapterClass).ToList();
        Assert.AreEqual(ChapterIds.Length, chapters.Count, "the seven chapters");
        foreach (var (id, from, to) in FamilyGame.Chapters)
        {
            Assert.AreEqual(t["chapter." + id], Ui.Q<Label>("chapter-title-" + id).text, id);
            Assert.AreEqual(t.Format("chapter.years", from, to), Ui.Q<Label>("chapter-years-" + id).text, id);
        }
        UiTestKit.Press(Ui.Q<Button>("chapter-1947_1949"));
        Assert.AreEqual("1947_1949", picker.Chapter);
        // The villages, by region, in the UI's language.
        var headings = Ui.Query<Label>(className: FamilyPickerPresenter.RegionClass).ToList().Select(l => l.text).ToList();
        CollectionAssert.AreEqual(new[] { "inside_1948", "west_bank", "gaza" }.Select(r => t["picker.region." + r]).ToList(), headings);
        var c = Inputs.Chapter("1947_1949");
        var tantura = c.Places.First(p => p.Id == "tantura");
        Assert.AreEqual(language == "ar" ? tantura.NameAr : tantura.Name, Ui.Q<Button>("village-tantura").text);
        Assert.IsNull(Ui.Q<Button>("village-tel_aviv"), "a Jewish town is no family's village");
        // Two places of one name are told apart by the nearest town.
        var tiras = Ui.Query<Button>(className: FamilyPickerPresenter.VillageClass).ToList().Where(b => b.text.StartsWith(language == "ar" ? "الطيرة" : "Tira")).Select(b => b.text).ToList();
        Assert.Greater(tiras.Count, 1);
        Assert.AreEqual(tiras.Count, tiras.Distinct().Count(), string.Join(" / ", tiras));
        StringAssert.Contains(language == "ar" ? "قرب" : "near", tiras[0]);
        Assert.IsNotNull(Ui.Q<Button>("village-khan_yunis"));
        // The search: either name, loosely.
        picker.Filter(language == "ar" ? "الطنطوره" : "TANTURA");
        Assert.AreEqual(1, Ui.Query<Button>(className: FamilyPickerPresenter.VillageClass).ToList().Count);
        Assert.AreEqual(t.Plural("picker.count", 1), Ui.Q<Label>("picker-count").text);
        picker.Filter("zzzz");
        Assert.IsNotNull(Ui.Q<Label>("picker-none"));
        picker.Filter("");
        // Back goes to the chapters, and again closes the picker.
        UiTestKit.Press(Ui.Q<Button>("picker-back"));
        Assert.IsNull(picker.Chapter);
        UiTestKit.Press(Ui.Q<Button>("chapter-1947_1949"));
        UiTestKit.Press(Ui.Q<Button>("village-tantura"));
        Assert.IsNotNull(root.Bridge!.Content, "the family game");
        Assert.AreEqual("tantura", root.Bridge.State.Village);
        Assert.AreEqual("1947_1949", FamilyGame.ChapterOf(root.Bridge.Content));
        Assert.IsFalse(Ui.Q("root").ClassListContains(StartScreenPresenter.OpenClass), "on the map");
        Assert.IsTrue(File.Exists(new SaveStore(saveDir).Family.AutosavePath), "turn 0 is saved at once");
    }

    [Test]
    public void ContinueLoadsTheFamilySaveAndTheAutomatedPathPlaysTheDefaultGame()
    {
        var root = Family("ar", "1967_1987", "imwas");
        var bridge = root.Bridge!;
        bridge.EndTurn();
        var hash = bridge.State.Hash();
        var saved = File.ReadAllText(new SaveStore(saveDir).Family.AutosavePath);
        StringAssert.StartsWith(FamilyGame.ChapterLine + "1967_1987\n", saved);
        root.Begin(Inputs, 0, startScreen: true);                       // a new start of the game
        Assert.IsNotNull(root.Bridge!.Content);
        Assert.AreEqual(hash, root.Bridge.State.Hash(), "the same family, replayed");
        Assert.AreEqual(DisplayStyle.Flex, Ui.Q<Button>("start-continue").style.display.value, "Continue is offered");
        UiTestKit.Press(Ui.Q<Button>("start-continue"));
        Assert.IsFalse(Ui.Q("root").ClassListContains(StartScreenPresenter.OpenClass));
        // New game with a save asks first (playtest 2: "Your current game will be lost. Continue?"), then opens the picker;
        // going back keeps the save.
        root.Begin(Inputs, 0, startScreen: true);
        UiTestKit.Press(Ui.Q<Button>("start-new-game"));
        Assert.IsFalse(root.Picker!.IsOpen, "the first press only asks");
        Assert.AreEqual(FamilyKit.Ar["restart.confirm"], Ui.Q<Label>("pause-confirm-text").text);
        UiTestKit.Press(Ui.Q<Button>("pause-confirm-yes"));
        Assert.IsTrue(root.Picker!.IsOpen);
        root.Picker.Back();
        Assert.IsTrue(File.Exists(new SaveStore(saveDir).Family.AutosavePath), "nothing is lost until a village is picked");
        // No start screen (the automated runs, -nostart): the default family game with the golden seed, and the player's
        // save is neither read nor written.
        var before = File.ReadAllText(new SaveStore(saveDir).Family.AutosavePath);
        root.Begin(Inputs, 0);
        var automated = root.Bridge!;
        Assert.IsNotNull(automated.Content);
        Assert.AreEqual(GameRoot.AutomatedChapter, FamilyGame.ChapterOf(automated.Content));
        Assert.AreEqual(GameRoot.AutomatedVillage, automated.State.Village);
        Assert.AreEqual(GoldenRun.Seed, automated.Session.Save.Seed, "the golden seed");
        Assert.AreEqual(0, automated.State.Turn, "a new game, not the save replayed");
        Assert.AreEqual(FamilyKit.Session().State.Hash(), automated.State.Hash());
        FamilyKit.PlayMonth(automated);
        Assert.AreEqual(before, File.ReadAllText(new SaveStore(saveDir).Family.AutosavePath), "the family save is untouched");
    }

    [Test]
    public void TheLaunchCheckAsksForAFamilyGameByFlag()
    {
        Assert.AreEqual(("1947_1949", "tantura"), GameRoot.FamilyRequested(null, new[] { "-batchmode", "-family", "1947_1949:tantura" }, webgl: false));
        Assert.AreEqual(("2023_2026", "khan_yunis"), GameRoot.FamilyRequested("http://localhost/index.html?lang=ar&family=2023_2026:khan_yunis", new string[0], webgl: true));
        foreach (var args in new[] { new string[0], new[] { "-family" }, new[] { "-family", "1947_1949" }, new[] { "-family", ":tantura" } })
            Assert.IsNull(GameRoot.FamilyRequested(null, args, webgl: false), string.Join(" ", args));
        Assert.IsNull(GameRoot.FamilyRequested("http://localhost/index.html?family=1947_1949", new string[0], webgl: true));
        // The check leaves the player's family save alone.
        var root = Scene("en");
        root.StartFamily(Inputs, "1947_1949", "tantura", keepSave: true);
        Assert.IsNotNull(root.Bridge!.Content);
        Assert.IsFalse(File.Exists(new SaveStore(saveDir).Family.AutosavePath));
        root.Bridge.EndTurn();
        Assert.IsFalse(File.Exists(new SaveStore(saveDir).Family.AutosavePath), "no autosave either");
    }

    [Test]
    public void ABrokenFamilySaveIsSetAsideWithANote()
    {
        var store = new SaveStore(saveDir).Family;
        store.WriteAutosave("chapter nowhere\nnot a save");
        var notes = new List<string>();
        Assert.IsNull(GameStart.ContinueFamily(Inputs.Chapter, store, notes, En));
        Assert.AreEqual(1, notes.Count);
        Assert.IsNull(store.ReadAutosave(), "set aside");
        Assert.Throws<SaveException>(() => FamilyGame.SplitSave("no chapter line"));
    }

    // --- Step 4: the top bar ------------------------------------------------------------------------------------------

    [Test]
    public void TheFamilyBarShowsTheTenResourcesAndTheMoneyAndHidesTheStatesMeters([Values("en", "ar")] string language)
    {
        bool arabic = language == "ar";
        var t = arabic ? Ar : En;
        var root = Family(language, "1947_1949", "tantura");
        var c = root.Bridge!.Content; var s = root.Bridge.State;
        Assert.IsTrue(Ui.Q("root").ClassListContains(HudPresenter.FamilyClass));
        var meters = Ui.Q("family-meters").Query(className: "meter").ToList();
        Assert.AreEqual(10, meters.Count, "ten resources, the money beside Livelihood's name (the owner's choice (a))");
        Assert.AreEqual(c.Indicators.Count, 10);
        foreach (var i in c.Indicators)
        {
            var m = Ui.Q("meter-family-" + i.Id);
            if (i.Id != HudPresenter.MoneyMeter) Assert.AreEqual(arabic ? i.NameAr : i.Name, m.Q<Label>(className: "meter-name").text, i.Id);
            Assert.AreEqual(PlayerText.Percent(s.Indicators[i.Id]), Ui.Q<Label>("family-" + i.Id + "-value").text, i.Id);
        }
        // The owner's choice (a): Livelihood's name carries the money's sign ("Livelihood · £P"); the money's full name, its
        // smaller unit and the note are in Livelihood's tooltip; no bare currency cell.
        var livelihood = Ui.Q("meter-family-livelihood");
        Assert.AreEqual(t.Format("hud.livelihood_money", arabic ? c.Indicators.First(i => i.Id == "livelihood").NameAr : c.Indicators.First(i => i.Id == "livelihood").Name,
            HudPresenter.CurrencySign(c, s, arabic)), livelihood.Q<Label>(className: "meter-name").text);
        StringAssert.Contains(arabic ? StoryOfTheLand.Sim.Family.CurrencyAt(c, s)!.NameAr : StoryOfTheLand.Sim.Family.CurrencyAt(c, s)!.Name, livelihood.tooltip);
        StringAssert.Contains(arabic ? "المِل" : "mil", livelihood.tooltip);
        Assert.IsNull(Ui.Q("meter-family-currency"));
        Assert.AreEqual(t.Format("hud.date", PlayerText.Month(s.Date, arabic)), Ui.Q<Label>("date").text);
        Assert.IsNotNull(Ui.Q<Button>(PeoplePresenter.ButtonName), "People in the left column");
        // After a month each change is said with the plural rule (hud.say_points).
        var card = root.Card!;
        for (int guard = 0; card.Shown != null && guard < 20; guard++) UiTestKit.Press(Ui.Q("event-options").Q<Button>());
        root.Bridge.EndTurn();
        var changed = c.Indicators.FirstOrDefault(i => Ui.Q<Label>("family-" + i.Id + "-delta").text.Length > 0);
        if (changed != null)
        {
            var points = (long)Math.Abs(PlayerText.ShownPoints(root.Bridge.State.Indicators[changed.Id]) - PlayerText.ShownPoints(root.Bridge.Session.Previous!.Indicators[changed.Id]));
            StringAssert.Contains(t.Plural("hud.say_points", points, (double)points), Ui.Q("meter-family-" + changed.Id).tooltip);
        }
    }

    [UnityTest]
    public IEnumerator TheStateMetersAndScreensAreGoneFromTheFamilyGame()
    {
        var root = Scene("ar");
        var restore = OnTexture(1600, 900);
        try
        {
            root.StartFamily(Inputs, "1947_1949", "tantura");
            for (int i = 0; i < 3; i++) yield return null;
            foreach (var name in new[] { "meters", "nav-cabinet", "nav-budget", "nav-defence", "info-panel", "attention", "score" })
                Assert.IsNull(Ui.Q(name), name + ": gone from the game");
            Assert.AreEqual(DisplayStyle.Flex, Ui.Q("nav-how-to-play").resolvedStyle.display, "How to play is shown");
            Assert.AreEqual(DisplayStyle.Flex, Ui.Q("family-meters").resolvedStyle.display);
            // Every family meter is inside the bar and they do not overlap the screen buttons or End Turn.
            var bar = Ui.Q("top-bar").worldBound;
            var nav = Ui.Q("nav").worldBound;
            foreach (var m in Ui.Q("family-meters").Query(className: "meter").ToList())
            {
                Assert.IsTrue(bar.Contains(m.worldBound.min) && bar.Contains(m.worldBound.max - Vector2.one), m.name + " in the bar");
                Assert.IsFalse(m.worldBound.Overlaps(nav), m.name + " clear of New game");
            }
        }
        finally { restore(); }
    }

    // --- Step 5: the cards --------------------------------------------------------------------------------------------

    [Test]
    public void AFamilyCardIsInTheUiLanguageWithItsNoteSourcesAndOutcome([Values("en", "ar")] string language)
    {
        bool arabic = language == "ar";
        var t = arabic ? Ar : En;
        var root = Family(language, "1947_1949", "tantura");
        var bridge = root.Bridge!; var c = bridge.Content;
        // Play to May 1948, where Tantura's own card comes (fate_depopulated, a sensitive card).
        for (int guard = 0; bridge.State.Date < new DateTime(1948, 5, 1) && guard < 40; guard++) bridge.EndTurn();
        var card = root.Card!;
        var seen = new List<GameEvent>();
        for (int guard = 0; card.Shown != null && guard < 30; guard++)
        {
            var e = card.Shown;
            seen.Add(e);
            Assert.AreEqual(FamilyGame.Title(c, bridge.State, e, arabic), Ui.Q<Label>("event-title").text, e.Id);
            Assert.AreEqual(FamilyGame.Text(c, bridge.State, e, arabic), Ui.Q<Label>("event-text").text, e.Id);
            Assert.AreEqual(FamilyGame.Sensitive(e) ? DisplayStyle.Flex : DisplayStyle.None, Ui.Q<Label>("event-note").style.display.value, e.Id);
            if (FamilyGame.Sensitive(e)) Assert.AreEqual(t["event.content_note"], Ui.Q<Label>("event-note").text);
            Assert.IsNull(Ui.Q("event-image"), "no portrait");
            Assert.AreEqual(e.Sources.Count, Ui.Q("event-sources").Query<Button>(className: SourceLinks.LinkClass).ToList().Count, e.Id);
            var options = Ui.Q("event-options").Query<Button>().ToList();
            Assert.AreEqual(e.Options.Select(o => FamilyGame.Label(c, bridge.State, e, o, arabic)).ToList(), options.Select(b => b.text).ToList(), e.Id);
            var first = e.Options[0];
            UiTestKit.Press(options[0]);
            var outcome = FamilyGame.Outcome(c, bridge.State, e, first, arabic);
            if (outcome.Length > 0)
            {
                Assert.AreSame(e, card.Shown, "the card stays with what followed");
                Assert.AreEqual(outcome, Ui.Q<Label>("event-text").text);
                Assert.AreEqual(t.Format("event.you_chose", FamilyGame.Label(c, bridge.State, e, first, arabic)), Ui.Q<Label>("event-chosen").text);
                UiTestKit.Press(Ui.Q<Button>("event-continue"));
            }
            Assert.AreNotSame(e, card.Shown);
        }
        Assert.IsTrue(seen.Any(e => e.Id == "fate_depopulated"), "Tantura's own card in May 1948: " + string.Join(", ", seen.Select(e => e.Id)));
        Assert.IsTrue(seen.Any(FamilyGame.Sensitive), "a sensitive card");
        if (arabic)
            foreach (var e in seen) Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(FamilyGame.Text(c, bridge.State, e, true), "[A-Za-z]{3,}"), e.Id + ": Arabic text");
        // The month's Why in the UI's language.
        bridge.EndTurn();
        var why = FamilyGame.Why(c, bridge.Session.Previous!, bridge.State, bridge.Session.Last!.Why, t);
        Assert.IsNotEmpty(why);
        Assert.IsTrue(why.Any(g => g.Heading == FamilyGame.Title(c, bridge.Session.Previous!, seen[0], arabic)));
    }

    // --- Step 6: the map ----------------------------------------------------------------------------------------------

    /// <summary>Owner (2026-10-22): the minimap's pan buttons move the view along the screen's own left and right.</summary>
    [Test]
    public void ThePanButtonsMoveTheViewLeftAndRight()
    {
        bool motion = GraphicsQuality.ReducedMotion;
        try
        {
            GraphicsQuality.ReducedMotion = true;   // the glide jumps, so the move shows at once
            var root = Family("en", "1947_1949", "tantura");
            root.Card!.Collapse();   // the open card's backdrop takes the map's buttons out of reach
            var rig = Camera.main!.GetComponent<CameraRig>();
            rig.Yaw = 0f;
            var x = rig.LookAt.x;
            UiTestKit.Press(Ui.Q<Button>("minimap-pan-right"));
            Assert.AreEqual(x + GameRoot.PanShare * rig.Distance, rig.LookAt.x, 1f, "right: east, looking north");
            UiTestKit.Press(Ui.Q<Button>("minimap-pan-left"));
            Assert.AreEqual(x, rig.LookAt.x, 1f, "left: back");
        }
        finally { GraphicsQuality.ReducedMotion = motion; }
    }

    /// <summary>Owner (2026-10-22): a turn's new village fates are the map's changes, the family's village first.</summary>
    [Test]
    public void ATurnsMapChangesPutTheFamilysVillageFirst()
    {
        var was = new Dictionary<string, string> { ["a"] = "Depopulated" };
        var now = new Dictionary<string, string> { ["a"] = "Depopulated", ["c"] = "Destroyed", ["b"] = "Built over", ["home"] = "Depopulated" };
        CollectionAssert.AreEqual(new[] { ("home", "Depopulated"), ("b", "Built over"), ("c", "Destroyed") },
            GameRoot.MapChanges(was, now, "home").ToArray());
        CollectionAssert.IsEmpty(GameRoot.MapChanges(now, now, "home"));
    }

    [Test]
    public void TheMapFramesAndMarksTheVillageNamesItInArabicAndShowsItsFate()
    {
        var root = Family("ar", "1947_1949", "tantura");
        var map = Object.FindFirstObjectByType<MapScene>();
        var rig = Camera.main!.GetComponent<CameraRig>();
        var place = root.Bridge!.Content.Places.First(p => p.Id == "tantura");
        var at = map.Frame.ToWorld(place.Lon, place.Lat, 0);
        Assert.Less(Vector2.Distance(new Vector2(rig.LookAt.x, rig.LookAt.z), new Vector2(at.x, at.z)), 1f, "framed on Tantura");
        Assert.AreEqual(GameRoot.VillageDistanceM, rig.Distance, 1f);
        var home = Ui.Q<Label>("label-tantura");
        Assert.IsNotNull(home);
        Assert.IsTrue(home.ClassListContains(MapLabelsPresenter.HomeClass), "the family's village is marked");
        var tanturaAr = map.Localities.First(p => p.Settlement.Id == "tantura").NameAr;
        Assert.AreEqual("الطنطورة", tanturaAr, "names_ar.yaml's name, inline in localities_1948");
        Assert.AreEqual(tanturaAr, home.text, "its Arabic name");
        Assert.AreEqual("يافا", Ui.Q<Label>("label-jaffa").text, "a settlement's name_ar");
        Assert.IsFalse(map.Settlements.Concat(map.Localities.Select(p => p.Settlement)).Any(x => System.Text.RegularExpressions.Regex.IsMatch(x.NameAr ?? "", "[\u0590-\u05FF]")), "no Hebrew");
        Assert.IsNull(Ui.Q<Label>("fate-tantura"), "no fate in December 1947");
        for (int guard = 0; root.Bridge.State.Date < new DateTime(1948, 6, 1) && guard < 40; guard++) root.Bridge.EndTurn();
        Assert.AreEqual(Ar["map.fate.depopulated"], Ui.Q<Label>("fate-tantura")?.text, "depopulated from May 1948");
        // English names in English.
        Family("en", "1947_1949", "tantura");
        Assert.AreEqual(place.Name, Ui.Q<Label>("label-tantura").text);
    }

    [Test]
    public void FatesComeOnTheirDatedMonthAndUndatedOnesNever()
    {
        var c = Inputs.Chapter("1947_1949");
        Assert.IsFalse(FamilyGame.Fates(c, new DateTime(1948, 4, 1), En).ContainsKey("tantura"));
        Assert.AreEqual(En["map.fate.depopulated"], FamilyGame.Fates(c, new DateTime(1948, 5, 1), En)["tantura"]);
        var undated = c.Fates.Where(f => !f.Year.HasValue && f.Fate != "inhabited").Select(f => f.Place).ToHashSet();
        var dated = c.Fates.Where(f => f.Year.HasValue && f.Fate != "inhabited").Select(f => f.Place).ToHashSet();
        var shown = FamilyGame.Fates(c, new DateTime(2026, 10, 1), En);
        foreach (var p in undated.Except(dated)) Assert.IsFalse(shown.ContainsKey(p), p);
    }

    // --- Step 7: People -----------------------------------------------------------------------------------------------

    [Test]
    public void ThePeopleScreenListsTheChaptersFiguresAndACardLinksToIt([Values("en", "ar")] string language)
    {
        bool arabic = language == "ar";
        var t = arabic ? Ar : En;
        var figures = Figures.Parse(Inputs.Read(Figures.FileName));
        Assert.Greater(figures.All.Count, 40);
        var root = Family(language, "1947_1949", "tantura");
        var shown = figures.Of("1947_1949");
        var cards = Ui.Q("people-list").Query(className: PeoplePresenter.CardClass).ToList();
        Assert.AreEqual(shown.Count, cards.Count);
        foreach (var f in shown)
        {
            var card = Ui.Q("figure-" + f.Id);
            Assert.AreEqual(f.NameIn(arabic), card.Q<Label>("figure-name-" + f.Id).text);
            StringAssert.Contains(f.BioIn(arabic), string.Join("\n", card.Query<Label>().ToList().Select(l => l.text)));
            Assert.AreEqual(f.Sensitive, card.Q<Label>("figure-note-" + f.Id) != null, f.Id + ": the content note");
            Assert.AreEqual(f.Sources.Count, card.Query<Button>(className: SourceLinks.LinkClass).ToList().Count, f.Id);
            Assert.IsNotNull(card.Q<UiFrame>(), "the ornamented frame, no portrait");
        }
        Assert.AreEqual(t["people.title"], Ui.Q<Label>("people-title").text);
        UiTestKit.Press(Ui.Q<Button>(PeoplePresenter.ButtonName));
        Assert.AreEqual(DisplayStyle.Flex, Ui.Q("screen-people").style.display.value, "the rail button opens it");
        // A card whose event a figure is related to names that figure; the link opens the screen.
        var related = Inputs.Chapter("1947_1949").Events.Select(e => (e, people: figures.For(e.Id))).First(x => x.people.Count > 0);
        var bridge = root.Bridge!;
        bridge.State.PendingEvents.Clear(); bridge.State.PendingEvents.Add(related.e.Id);
        root.Card!.Render();
        var link = Ui.Q<Button>("event-figure-" + related.people[0].Id);
        Assert.IsNotNull(link, related.e.Id);
        Assert.AreEqual(related.people[0].NameIn(arabic), link.text);
        // Owner (2026-10-22): the figure's portrait shows beside its name, unasked, when it has one.
        bool hasPicture = Resources.Load<Texture2D>(PeoplePresenter.PortraitFolder + related.people[0].Id) != null;
        Assert.AreEqual(hasPicture, Ui.Q("event-portrait-" + related.people[0].Id) != null, "the portrait on the card");
        UiTestKit.Press(Ui.Q<Button>("screen-close-people"));
        UiTestKit.Press(link);
        Assert.AreEqual(DisplayStyle.Flex, Ui.Q("screen-people").style.display.value, "the card's line opens People");
    }

    // --- Step 9: Credits ----------------------------------------------------------------------------------------------

    [Test]
    public void CreditsOpenFromSettingsInTheUiLanguage([Values("en", "ar")] string language)
    {
        bool arabic = language == "ar";
        var credits = Credits.Parse(Inputs.Read(Credits.FileName));
        Assert.AreEqual(credits.En.Count, credits.Ar.Count, "the same sections in both");
        Family(language, "2023_2026", "khan_yunis");
        UiTestKit.Press(Ui.Q<Button>("settings-credits"));
        Assert.AreEqual(DisplayStyle.Flex, Ui.Q("screen-credits").style.display.value);
        var headings = Ui.Q("credits-body").Query<Label>(className: "credits-heading").ToList().Select(l => l.text).ToList();
        CollectionAssert.AreEqual(credits.In(arabic).Select(s => s.Title).ToList(), headings);
        // The owner's ruling (2026-10-20): no notices on the screen; it points to the file every build carries.
        var all = string.Join(" ", Ui.Q("credits-body").Query<Label>().ToList().Select(l => l.text)).Replace("\u2060", "");
        StringAssert.Contains("THIRD-PARTY-NOTICES.md", all, "the notices are in the game folder");
        StringAssert.Contains(arabic ? "محمد جبيلو" : "Mohamed Jbilou", all, "the creator");
        Assert.IsEmpty(Ui.Q("credits-body").Query<Label>(className: "credits-quote").ToList(), "no quoted notices");
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Font>(UiPaths.FontCredits), UiPaths.FontCredits);
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Font>(UiPaths.FontCreditsBold), UiPaths.FontCreditsBold);
    }

    /// <summary>The map scene with its panel drawn into a texture of this size; the action it returns puts the panel back.</summary>
    static Action OnTexture(int width, int height)
    {
        var doc = Object.FindFirstObjectByType<UIDocument>();
        if (doc == null)
        {
            EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
            Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
            doc = Object.FindFirstObjectByType<UIDocument>();
        }
        var rt = new RenderTexture(width, height, 0);
        var ps = Object.Instantiate(doc.panelSettings);
        ps.targetTexture = rt;
        doc.panelSettings = ps;
        return () => { doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); };
    }
}
