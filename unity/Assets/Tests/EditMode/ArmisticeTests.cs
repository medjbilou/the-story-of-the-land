#nullable enable
using System;
using System.Collections;
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

/// <summary>armistice-lines: the 1949 armistice lines (content/map/armistice_hexes_1949.yaml, baked by
/// tools/armistice_bake.py from the true lines in armistice_lines_1949.geojson) replace the Mandate sub-district layer per
/// front on each agreement's date, only for fronts whose line exists (owner rulings 2026-10-11), in the 1947-1949 chapter;
/// the sim never reads them.</summary>
public class ArmisticeTests
{
    static GameInputs Inputs => FamilyKit.Inputs;
    static string ArmText => File.ReadAllText(Path.Combine(Paths.Content, "map", Armistice1949.FileName));
    static string SubText => File.ReadAllText(Path.Combine(Paths.Content, "map", Subdistricts1948.FileName));
    static UiText Text => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Strings).text);
    static MapRegions Regions() => new MapRegions(Subdistricts1948.Parse(SubText), Armistice1949.Parse(ArmText));
    static readonly DateTime Start = new DateTime(1948, 5, 15), Egypt = new DateTime(1949, 2, 24), Lebanon = new DateTime(1949, 3, 23),
        Jordan = new DateTime(1949, 4, 3), Syria = new DateTime(1949, 7, 20);
    string saveDir = "";

    [SetUp] public void TempSaves() => saveDir = Path.Combine(Path.GetTempPath(), "storyoftheland-arm-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void DiscardScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); VisualToggles.Clear();
        if (Directory.Exists(saveDir)) Directory.Delete(saveDir, true);
    }

    [Test]
    public void TheBakedFileListsTheFourFrontsWithJordanLowAccuracy()
    {
        var a = Armistice1949.Parse(ArmText);
        CollectionAssert.AreEqual(new[] { "egypt", "lebanon", "jordan", "syria" }, a.Fronts.Select(f => f.Key).ToArray());
        Assert.AreEqual(Egypt, a.FrontOf("egypt")!.ShownFrom);
        Assert.AreEqual(Lebanon, a.FrontOf("lebanon")!.ShownFrom);
        Assert.AreEqual(Syria, a.FrontOf("syria")!.ShownFrom);
        var jordan = a.FrontOf("jordan")!;
        Assert.AreEqual(Jordan, jordan.ShownFrom);
        Assert.IsTrue(jordan.LowAccuracy, "traced from a low-resolution scan (owner ruling)");
        Assert.IsFalse(a.FrontOf("egypt")!.LowAccuracy);
        StringAssert.Contains("Jerusalem", jordan.Note);
        Assert.IsTrue(a.Lines.Where(l => l.Front == jordan).All(l => l.LowAccuracy));
        // line A in the Iraqi sector comes in by Art. VI.3's stages
        CollectionAssert.AreEquivalent(new[] { new DateTime(1949, 4, 3), new DateTime(1949, 5, 8), new DateTime(1949, 5, 22), new DateTime(1949, 7, 17) },
            a.Lines.Where(l => l.Front == jordan).Select(l => l.ShownFrom).Distinct().ToArray());
        StringAssert.Contains("nearest hex edge", a.Line);
        Assert.AreEqual(4, a.Zones.Count);
        Assert.AreEqual(12, a.Lines.Count);
        var mv = a.Lines.Single(l => l.Key == "egypt_mv1950_demarcation");
        Assert.AreEqual(new DateTime(1950, 2, 22), mv.ShownFrom);
        Assert.AreEqual(new DateTime(1950, 2, 22), mv.Signed);
        StringAssert.Contains("modus vivendi", mv.Agreement.ToLowerInvariant());
    }

    const string Head = "layer: x\nlabel: L\n";
    const string Fronts = "fronts:\n  - { key: e, name: E, agreement: A, signed: 1949-02-24, shown_from: 1949-02-24, note: '' }\n";
    [TestCase("", "empty")]
    [TestCase("layer: x\n" + Fronts, "no label")]
    [TestCase(Head + "fronts: []\n", "no fronts")]
    [TestCase(Head + "fronts:\n  - { key: j, name: J, agreement: A, signed: 1949-04-03, shown_from: null, note: '' }\n", "no note saying so")]
    [TestCase(Head + "fronts:\n  - { key: e, name: E, agreement: A, signed: 1949-02-24, shown_from: 1949-02-01, note: '' }\n", "before the agreement")]
    [TestCase(Head + Fronts + "zones:\n  - { id: 1, key: z, front: s, name: Z, shown_from: 1949-02-24, style: secondary }\n", "unknown front")]
    [TestCase(Head + Fronts + "zones:\n  - { id: 1, key: z, front: e, name: Z, shown_from: 1949-02-24, style: purple }\n", "unknown style")]
    [TestCase(Head + Fronts + "zones:\n  - { id: 1, key: z, front: e, name: Z, shown_from: 24/02/1949, style: secondary }\n", "not a yyyy-MM-dd")]
    [TestCase(Head + Fronts + "zone_hexes:\n  - [0, 0, 2]\n", "not listed")]
    [TestCase(Head + Fronts + "zones:\n  - { id: 1, key: z, front: e, name: Z, shown_from: 1949-02-24, style: secondary }\nzone_hexes:\n  - [0, 0, 1]\n  - [0, 0, 1]\n", "two zones")]
    public void MalformedFilesAreRefusedNamingTheFile(string yaml, string why)
    {
        var e = Assert.Throws<InvalidDataException>(() => Armistice1949.Parse(yaml));
        StringAssert.Contains(Armistice1949.FileName, e.Message); StringAssert.Contains(why, e.Message);
    }

    /// <summary>The Mandate layer's per-front ends follow each agreement; the layer as a whole stays as the base, since the
    /// lines do not yet enclose each side's area (the Egypt line stops at Gaza, the Jordan line leaves out Jerusalem).</summary>
    [Test]
    public void TheMandateLayerEndsPerFrontOnEachAgreementsDate()
    {
        var s = Subdistricts1948.Parse(SubText);
        Assert.AreEqual(Egypt, s.Fronts["egypt"]);
        Assert.AreEqual(Lebanon, s.Fronts["lebanon"]);
        Assert.AreEqual(Jordan, s.Fronts["jordan"]);
        Assert.AreEqual(Syria, s.Fronts["syria"]);
        Assert.IsFalse(s.FrontReplaced("egypt", Egypt.AddDays(-1)));
        Assert.IsTrue(s.FrontReplaced("egypt", Egypt));
        Assert.IsFalse(s.FrontReplaced("jordan", Jordan.AddDays(-1)));
        Assert.IsTrue(s.FrontReplaced("jordan", Jordan));
        Assert.IsTrue(s.FrontReplaced("syria", Syria));
        Assert.IsFalse(s.FrontReplaced("nowhere", Syria), "an unknown front never replaces the layer");
        Assert.IsNull(s.ShownUntil, "the base stays while the lines leave gaps");
        Assert.IsTrue(s.ShowsOn(new DateTime(1952, 12, 31)));
    }

    [Test]
    public void EachFrontSwitchesOnItsAgreementsDate()
    {
        var m = Regions();
        var a = m.Armistice!;
        Assert.AreSame(m.Subdistricts.Layer, m.LayerOn(Start), "the game's start: the Mandate layer alone");
        Assert.AreSame(m.Subdistricts.Layer, m.LayerOn(Egypt.AddDays(-1)));
        Assert.IsNull(m.LayerOn(Start.AddDays(-1)), "before the layer's window");
        var auja = a.Zones.Single(z => z.Key == "egypt_el_auja_dmz");
        var hex = a.HexesOf(auja).First();
        Assert.AreEqual(m.Subdistricts.Layer.RegionOf(hex.q, hex.r), m.LayerOn(Egypt.AddDays(-1))!.RegionOf(hex.q, hex.r));
        var egypt = m.LayerOn(Egypt)!;
        Assert.AreEqual(MapRegions.ZoneIdBase + auja.Id, egypt.RegionOf(hex.q, hex.r), "El Auja drawn from 24 Feb 1949");
        Assert.AreEqual(BorderStyle.Secondary, egypt.StyleOf(MapRegions.ZoneIdBase + auja.Id));
        Assert.AreEqual(m.Subdistricts.Layer.Count, egypt.Count, "the same hexes, regions overlaid");
        Assert.AreSame(egypt, m.LayerOn(Lebanon), "Lebanon adds no zone: its line is the frontier already drawn");
        var central = a.Zones.Single(z => z.Key == "syria_dmz_central");
        var c = a.HexesOf(central).First();
        Assert.AreNotEqual(MapRegions.ZoneIdBase + central.Id, m.LayerOn(Syria.AddDays(-1))!.RegionOf(c.q, c.r));
        Assert.AreEqual(MapRegions.ZoneIdBase + central.Id, m.LayerOn(Syria)!.RegionOf(c.q, c.r), "the Syrian DMZ from 20 Jul 1949");
        Assert.AreSame(m.LayerOn(Syria), m.LayerOn(new DateTime(1952, 12, 31)), "composed once per set of zones");
        var (on, none) = m.FrontsOn(new DateTime(1952, 1, 1));
        CollectionAssert.AreEqual(new[] { "egypt", "lebanon", "jordan", "syria" }, on.Select(f => f.Key).ToArray());
        Assert.AreEqual(0, none.Count);
        var (march, _) = m.FrontsOn(new DateTime(1949, 3, 1));
        CollectionAssert.AreEqual(new[] { "egypt" }, march.Select(f => f.Key).ToArray());
    }

    static string L(MapRegions m, DateTime d)
    {
        return m.Label(d, Text.Format);
    }

    [Test]
    public void TheLabelNamesTheFrontsShownJordanAsLowAccuracyAndThe1950Line()
    {
        var m = Regions();
        Assert.AreEqual("Mandate sub-districts, 1947", L(m, Start));
        Assert.AreEqual("Mandate sub-districts, 1947 · 1949 armistice lines: Egypt", L(m, new DateTime(1949, 3, 1)));
        Assert.AreEqual("Mandate sub-districts, 1947 · 1949 armistice lines: Egypt, Lebanon, Jordan (low accuracy)", L(m, new DateTime(1949, 4, 10)));
        StringAssert.EndsWith("Jordan (low accuracy), Syria", L(m, new DateTime(1949, 8, 1)));
        StringAssert.Contains("Egypt (and the 1950 modus vivendi)", L(m, new DateTime(1950, 3, 1)));
        StringAssert.DoesNotContain("modus vivendi", L(m, new DateTime(1950, 2, 21)));
        // a front without line data says so (the rule for any front the data leaves out)
        var noJordan = new MapRegions(Subdistricts1948.Parse(SubText), Armistice1949.Parse(
            "layer: x\nlabel: 1949 armistice lines\nfronts:\n" +
            "  - { key: egypt, name: Egypt, agreement: A, signed: 1949-02-24, shown_from: 1949-02-24, note: '' }\n" +
            "  - { key: jordan, name: Jordan, agreement: B, signed: 1949-04-03, shown_from: null, note: 'no line data' }\n"));
        StringAssert.Contains("Jordan: Mandate sub-districts, no armistice line data", L(noJordan, new DateTime(1949, 8, 1)));
    }

    static UiText ArText => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.StringsArabic).text, "strings.ar.yaml");

    static string LAr(MapRegions m, DateTime d)
    {
        return m.Label(d, ArText.Format);
    }

    /// <summary>The Arabic map label (#596): a value with no Arabic in it that an Arabic pattern inserts (the 1950 note,
    /// a front's English name) is put between left-to-right marks as UiText.Format does (#585), so the note "1950 modus
    /// vivendi" keeps its year before its words in the right-to-left line.</summary>
    [Test]
    public void TheArabicLabelMarksTheValuesItsArabicPatternsInsert()
    {
        const string Lrm = "\u200E";
        var sub = Subdistricts1948.Parse(SubText.Replace("label: \"Mandate sub-districts, 1947\"", "label: \"أقضية الانتداب، 1947\""));
        var arm = ArmText.Replace("label: \"1949 armistice lines\"", "label: \"خطوط الهدنة، 1949\"");
        // The fronts' names as they are today (English), inside the Arabic line.
        var m = new MapRegions(sub, Armistice1949.Parse(arm));
        Assert.AreEqual("أقضية الانتداب، 1947", LAr(m, Start));
        var en = LAr(m, new DateTime(1950, 3, 1));
        Assert.AreEqual("أقضية الانتداب، 1947 · خطوط الهدنة، 1949: " + Lrm + "Egypt" + Lrm + " (و" + Lrm + "1950 modus vivendi" + Lrm + "), Lebanon, "
            + Lrm + "Jordan" + Lrm + " (دقة منخفضة), Syria", en);
        Assert.IsTrue(UiDirection.RightToLeft(en), en);
        // Arabic names: an Arabic value is left alone, the note is still marked.
        var arNames = new MapRegions(sub, Armistice1949.Parse(arm.Replace("name: \"Egypt\"", "name: \"مصر\"").Replace("name: \"Jordan\"", "name: \"الأردن\"")));
        StringAssert.Contains(": مصر (و" + Lrm + "1950 modus vivendi" + Lrm + "), Lebanon, الأردن (دقة منخفضة), Syria", LAr(arNames, new DateTime(1950, 3, 1)));
        // A front without line data: its name is marked in the Arabic note that says so.
        var noJordan = new MapRegions(sub, Armistice1949.Parse(
            "layer: x\nlabel: خطوط الهدنة، 1949\nfronts:\n" +
            "  - { key: egypt, name: Egypt, agreement: A, signed: 1949-02-24, shown_from: 1949-02-24, note: '' }\n" +
            "  - { key: jordan, name: Jordan, agreement: B, signed: 1949-04-03, shown_from: null, note: 'no line data' }\n"));
        StringAssert.EndsWith(" · " + Lrm + "Jordan" + Lrm + ": أقضية الانتداب، لا توجد بيانات عن خط الهدنة", LAr(noJordan, new DateTime(1949, 8, 1)));
        // Taking the marks out gives string.Format's text back.
        Assert.AreEqual(string.Format(System.Globalization.CultureInfo.InvariantCulture, ArText["map.layer_with_armistice"], "أقضية الانتداب، 1947", "خطوط الهدنة، 1949",
            "Egypt (و1950 modus vivendi), Lebanon, Jordan (دقة منخفضة), Syria"), en.Replace(Lrm, ""));
    }

    static System.Collections.Generic.IEnumerable<(int q, int r)> Hexes() =>
        MapBuilder.LoadHexes(Inputs.Map.HexesJson, "hexes.json").hexes.Select(h => (h.q, h.r));

    [Test]
    public void ApplyDrawsTheComposedLayerOnItsDateAndBack()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var m = Regions();
        m.Apply(Start);
        Assert.AreSame(m.Subdistricts.Layer, MapBorders.Current);
        m.Apply(Egypt);
        Assert.AreSame(m.LayerOn(Egypt), MapBorders.Current, "the armistice zones drawn on 24 Feb 1949");
        Assert.IsTrue(m.Shown);
        m.Apply(Start);   // a new game
        Assert.AreSame(m.Subdistricts.Layer, MapBorders.Current);
    }

    /// <summary>The game's own wiring: in the 1947-1949 chapter (the default game), a date after the last agreement draws
    /// the map's region layer for that date and the label carries the armistice legend (the sub-district and armistice
    /// labels themselves are tested on MapRegions above; the 1948 sides in ControlTests).</summary>
    [Test]
    public void TheFrontLineChapterDrawsTheArmisticeLayerAndItsLegendAfterTheAgreements()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var root = Object.FindFirstObjectByType<GameRoot>();
        root.Store = new SaveStore(saveDir);
        root.Begin(Inputs, 0);
        var after = Syria.AddDays(30);
        var g = FamilyKit.Session(); g.State.Date = after;
        root.Bridge!.Replace(g);
        Assert.IsNotNull(MapBorders.Current);
        Assert.AreSame(root.Regions!.LayerOn(after), MapBorders.Current);
        StringAssert.StartsWith("The 1949 armistice agreements' lines", Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Label>("map-layer").text);
    }

    [Test]
    public void TheWebGLBuildShipsTheFileAndReadsIt()
    {
        CollectionAssert.Contains(ContentManifest.Collect(Paths.Content), "map/" + Armistice1949.FileName);
        Assert.AreEqual(ArmText, Inputs.Map.ArmisticeYaml);
    }

    /// <summary>Map-only data: the content hash (saves, goldens) does not move whatever the armistice files say.</summary>
    [Test]
    public void TheSimIgnoresTheFilesTheContentHashDoesNotMove()
    {
        var copy = Path.Combine(saveDir, "content");
        foreach (var f in Directory.GetFiles(Paths.Content, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")))
        {
            var to = Path.Combine(copy, Path.GetRelativePath(Paths.Content, f));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(f, to);
        }
        var before = ContentLoader.LoadChapter(copy, GameRoot.FrontLineChapter).Hash;
        Assert.AreEqual(FamilyKit.Chapter(GameRoot.FrontLineChapter).Hash, before);
        File.WriteAllText(Path.Combine(copy, "map", Armistice1949.FileName), "layer: changed\n");
        File.WriteAllText(Path.Combine(copy, "map", "armistice_lines_1949.geojson"), "{}");
        File.WriteAllText(Path.Combine(copy, "map", Subdistricts1948.FileName), SubText.Replace("until: 1949-02-24", "until: 1950-01-01"));
        Assert.AreEqual(before, ContentLoader.LoadChapter(copy, GameRoot.FrontLineChapter).Hash);
    }

    /// <summary>Review I-4: the longest label (from 22 Feb 1950: all four fronts, Jordan low-accuracy, the 1950 note) fits
    /// on screen beside the minimap with the real HUD (HudPresenter: the family bar) and the longest event card,
    /// at the card's window shapes and main's narrow ones: on screen, clear of the minimap and the top bar,
    /// its text not cut, at most four lines. Not clear of the card: since the owner's decision of 17 October 2026 (#849,
    /// issuecomment-6233870812, "Hide legend under card") the legend is hidden while the modal card is open
    /// (DecisionCardTests.TheLegendIsHiddenWhileTheCardIsOpen).</summary>
    [UnityTest]
    public IEnumerator TheLongestMapLabelFitsBesideTheEventCard()
    {
        var c = FamilyKit.Chapter(); var t = Text;
        var text = L(Regions(), new DateTime(1950, 3, 1));
        StringAssert.Contains("Jordan (low accuracy)", text);
        foreach (var (w, h) in new[] { (1920, 1080), (1280, 720), (1531, 784), (1366, 657), (2560, 1080), (1920, 900), (1280, 1024),
                                       (960, 600), (1024, 768), (390, 844), (375, 667), (1600, 757),
                                       // the two-row bar's shapes (#380's re-tier gives a square window two rows; review B1 of #831)
                                       (1024, 1024), (960, 1080), (800, 1000) })
        {
            var rt = new RenderTexture(w, h, 0);
            var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            var go = new GameObject("LabelLayout"); go.SetActive(false);
            var doc = go.AddComponent<UIDocument>(); doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            var root = doc.rootVisualElement;
            var bridge = FamilyKit.Bridge(t);
            var hud = new HudPresenter(root, bridge, t);
            var card = new EventCardPresenter(root, bridge, t);
            try
            {
                var g = FamilyKit.Session();
                var e = c.Events.OrderByDescending(x => x.Text.Length).First();
                g.State.Date = e.From; g.State.PendingEvents.Clear(); g.State.PendingEvents.Add(e.Id);
                bridge.Replace(g);
                var label = root.Q<Label>("map-layer");
                label.text = text; label.style.display = DisplayStyle.Flex;
                for (int i = 0; i < 6; i++) yield return null;
                Rect R(string n) => root.Q(n).worldBound;
                var lb = label.worldBound;
                var screen = root.worldBound;
                Assert.IsTrue(lb.xMin >= screen.xMin - 0.5f && lb.xMax <= screen.xMax + 0.5f && lb.yMin >= screen.yMin - 0.5f && lb.yMax <= screen.yMax + 0.5f,
                    $"{w}x{h}: the label {lb} leaves the screen {screen}");
                Assert.IsFalse(lb.Overlaps(R("minimap")), $"{w}x{h}: the label {lb} overlaps the minimap {R("minimap")}");
                Assert.IsFalse(lb.Overlaps(R("top-bar")), $"{w}x{h}: the top bar overlaps the label");
                var size = label.MeasureTextSize(text, lb.width - label.resolvedStyle.paddingLeft - label.resolvedStyle.paddingRight,
                    VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                Assert.LessOrEqual(size.y, lb.height + 1f, $"{w}x{h}: the label's text is cut off");
                Assert.LessOrEqual(size.y, 4 * label.resolvedStyle.fontSize * 1.4f, $"{w}x{h}: the label takes more than four lines ({size.y} px)");
            }
            finally { card.Dispose(); hud.Dispose(); Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt); }
        }
    }

    /// <summary>Review I-2: the switch per front comes from the Mandate layer's own per-front end
    /// (Subdistricts1948.FrontReplaced): moving Egypt's end moves when its zones are drawn, its lines named and the label
    /// lists it.</summary>
    [Test]
    public void TheMandateLayersFrontEndDrivesTheSwitch()
    {
        var later = new MapRegions(Subdistricts1948.Parse(SubText.Replace("{ key: egypt, until: 1949-02-24 }", "{ key: egypt, until: 1949-06-01 }")),
            Armistice1949.Parse(ArmText));
        var a = later.Armistice!;
        var auja = a.Zones.Single(z => z.Key == "egypt_el_auja_dmz");
        var (q, r) = a.HexesOf(auja).First();
        var march = new DateTime(1949, 3, 1);
        Assert.IsFalse(later.Subdistricts.FrontReplaced("egypt", march));
        Assert.IsNull(later.ZoneAt(q, r, march), "Egypt has not switched on the Mandate layer yet");
        Assert.AreSame(later.Subdistricts.Layer, later.LayerOn(march));
        StringAssert.DoesNotContain("Egypt", L(later, march));
        var gaza = a.Lines.Single(l => l.Key == "egypt_adl_gaza");
        var gh = Hexes().First(h => a.LinesAt(h.q, h.r, march).Contains(gaza));
        Assert.IsFalse(later.LinesAt(gh.q, gh.r, march).Contains(gaza));
        var june = new DateTime(1949, 6, 1);
        Assert.AreSame(auja, later.ZoneAt(q, r, june));
        Assert.AreEqual(MapRegions.ZoneIdBase + auja.Id, later.LayerOn(june)!.RegionOf(q, r));
        StringAssert.Contains("Egypt", L(later, june));
        Assert.IsTrue(later.LinesAt(gh.q, gh.r, june).Contains(gaza));
        // as shipped, the Mandate layer's ends are the agreements' dates
        Assert.AreEqual(auja.Key, Regions().ZoneAt(q, r, Egypt)?.Key);
    }
}
