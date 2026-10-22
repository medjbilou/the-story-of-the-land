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

/// <summary>promo-borderdata: the 1948 sub-districts (content/map/subdistrict_hexes_1948.yaml, baked by
/// tools/borders_bake.py from the true lines in subdistricts_1948.geojson) drawn by the border renderer in the 1947-1949
/// chapter, and kept out of the sim (the content hash does not move).</summary>
public class SubdistrictTests
{
    static GameInputs Inputs => FamilyKit.Inputs;
    static string FileText => File.ReadAllText(Path.Combine(Paths.Content, "map", Subdistricts1948.FileName));
    string saveDir = "";

    [SetUp] public void TempSaves() => saveDir = Path.Combine(Path.GetTempPath(), "storyoftheland-subd-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void DiscardScene()
    {
        MapShaderGlobals.PinWaterTime(null);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); VisualToggles.Clear();
        if (Directory.Exists(saveDir)) Directory.Delete(saveDir, true);
    }

    /// <summary>Map.unity started the way GameBoot.Ready starts it (GameSceneTests.Boot), saves in a temporary folder.</summary>
    static GameRoot Boot(string saveDir)
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var root = Object.FindFirstObjectByType<GameRoot>();
        root.Store = new SaveStore(saveDir);
        root.Begin(Inputs, 0);
        return root;
    }

    /// <summary>The first day the region layers show: 15 May 1948.</summary>
    static readonly DateTime Shown = new DateTime(1948, 5, 15);

    /// <summary>Moves the default game (the 1947-1949 chapter, from late 1947) to <paramref name="date"/>.</summary>
    static void MoveTo(GameRoot root, DateTime date)
    {
        var g = FamilyKit.Session(); g.State.Date = date;
        root.Bridge!.Replace(g);
    }

    /// <summary>Jerusalem's settlement hex (content/map/settlement_hexes.yaml, tools/settlement_hexes.py).</summary>
    static readonly (int q, int r) JerusalemHex = (0, 23);

    [Test]
    public void TheBakedFileListsTheSixteenSubdistrictsAndOnlyLandHexesOfTheMap()
    {
        var s = Subdistricts1948.Parse(FileText);
        var subs = s.Regions.Where(r => r.Style != BorderStyle.None).ToList();
        Assert.AreEqual(16, subs.Count);
        Assert.AreEqual(6, subs.Select(r => r.District).Distinct().Count());
        CollectionAssert.Contains(subs.Select(r => r.Name).ToList(), "Jerusalem Sub-District");
        Assert.AreEqual(1, s.Regions.Count(r => r.Style == BorderStyle.None), "one 'outside' region (the Mandate frontier)");
        StringAssert.Contains("nearest hex edge", s.Line);
        var land = MapBuilder.LoadHexes(Inputs.Map.HexesJson, "hexes.json").hexes.Where(h => h.land).Select(h => (h.q, h.r)).ToHashSet();
        Assert.IsTrue(land.SetEquals(s.Layer.Hexes), "every land hex, and no sea or lake hex, is in a region");
        Assert.AreEqual("Jerusalem Sub-District", s.SubdistrictAt(JerusalemHex.q, JerusalemHex.r)?.Name);
    }

    const string Head = "layer: x\nlabel: L\nshown_from: 1948-05-15\n";
    [TestCase("", "empty")]
    [TestCase(Head + "regions: []\n", "no regions")]
    [TestCase("layer: x\nshown_from: 1948-05-15\nregions:\n  - { id: 1, key: a, name: A, district: D, style: primary }\nhexes: []\n", "no label")]
    [TestCase("layer: x\nlabel: L\nshown_from: 15/05/1948\nregions:\n  - { id: 1, key: a, name: A, district: D, style: primary }\nhexes: []\n", "not a yyyy-MM-dd")]
    [TestCase("layer: x\nlabel: L\nshown_from: 1948-05-15\nshown_until: 1948-05-15\nregions:\n  - { id: 1, key: a, name: A, district: D, style: primary }\nhexes: []\n", "not after")]
    [TestCase(Head + "regions:\n  - { id: 1, key: a, name: A, district: D, style: purple }\nhexes: []\n", "unknown style")]
    [TestCase(Head + "regions:\n  - { id: 1, key: a, name: A, district: D, style: primary }\nhexes:\n  - [0, 0, 2]\n", "not listed")]
    [TestCase(Head + "regions:\n  - { id: 1, key: a, name: A, district: D, style: primary }\nhexes:\n  - [0, 0, 1]\n  - [0, 0, 1]\n", "twice")]
    [TestCase(Head + "regions:\n  - { id: 0, key: a, name: A, district: D, style: primary }\nhexes: []\n", "outside 1..")]
    public void MalformedFilesAreRefusedNamingTheFile(string yaml, string why)
    {
        var e = Assert.Throws<InvalidDataException>(() => Subdistricts1948.Parse(yaml));
        StringAssert.Contains(Subdistricts1948.FileName, e.Message); StringAssert.Contains(why, e.Message);
    }

    /// <summary>Owner ruling 2026-10-11 (review I4): the layer shows from the game's start until the 1949 armistice lines
    /// replace it (no end date until that data exists), labelled "Mandate sub-districts, 1947" on screen.</summary>
    [Test]
    public void TheLayerShowsFromTheStartUntilReplacedAndIsLabelled()
    {
        var s = Subdistricts1948.Parse(FileText);
        Assert.AreEqual(new System.DateTime(1948, 5, 15), s.ShownFrom);
        Assert.IsNull(s.ShownUntil, "no end until the armistice lines exist");
        Assert.IsFalse(s.ShowsOn(new System.DateTime(1948, 5, 14)));
        Assert.IsTrue(s.ShowsOn(new System.DateTime(1948, 5, 15)));
        Assert.IsTrue(s.ShowsOn(new System.DateTime(1952, 12, 31)));
        Assert.AreEqual("Mandate sub-districts, 1947", s.Label);
        Assert.IsNull(s.Layer.Banner, "the label is the game's UI label, not the 3D banner (the tilt-shift blurs that)");
        var ended = Subdistricts1948.Parse(FileText.Replace("shown_until: null", "shown_until: 1949-02-24"));
        Assert.IsTrue(ended.ShowsOn(new System.DateTime(1949, 2, 23)));
        Assert.IsFalse(ended.ShowsOn(new System.DateTime(1949, 2, 24)));
    }

    [Test]
    public void ALayerWithAnEndComesOffTheMapOnItsDate()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(Inputs.Map);
        var s = Subdistricts1948.Parse(FileText.Replace("shown_until: null", "shown_until: 1949-02-24"));
        s.Apply(new System.DateTime(1948, 5, 15));
        Assert.AreSame(s.Layer, MapBorders.Current);
        s.Apply(new System.DateTime(1949, 2, 24));
        Assert.IsNull(MapBorders.Current, "off on its end date");
    }

    /// <summary>Re-review 2 nit: a rebuilt map (MapBorders resets its layer) gets the sub-districts again on the next Apply.</summary>
    [Test]
    public void ARebuiltMapGetsTheLayerAgain()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>();
        map.Build(Inputs.Map);
        var s = Subdistricts1948.Parse(FileText);
        s.Apply(s.ShownFrom);
        Assert.AreSame(s.Layer, MapBorders.Current);
        map.Build(Inputs.Map);
        Assert.IsNull(MapBorders.Current, "a rebuild draws no layer");
        s.Apply(s.ShownFrom);
        Assert.AreSame(s.Layer, MapBorders.Current, "drawn again");
        Assert.IsTrue(s.Shown);
    }

    [Test]
    public void TheWebGLBuildShipsTheFileAndReadsIt()
    {
        CollectionAssert.Contains(ContentManifest.Collect(Paths.Content), "map/" + Subdistricts1948.FileName);
        Assert.AreEqual(FileText, Inputs.Map.RegionsYaml);
    }

    /// <summary>The owner's rule: the sim never reads the border data, so the content hash (saves, goldens) does not move
    /// whatever the file says.</summary>
    [Test]
    public void TheSimIgnoresTheFileTheContentHashDoesNotMove()
    {
        var copy = Path.Combine(saveDir, "content");
        foreach (var f in Directory.GetFiles(Paths.Content, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")))
        {
            var to = Path.Combine(copy, Path.GetRelativePath(Paths.Content, f));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Copy(f, to);
        }
        var before = ContentLoader.LoadChapter(copy, GameRoot.FrontLineChapter).Hash;
        Assert.AreEqual(FamilyKit.Chapter(GameRoot.FrontLineChapter).Hash, before);
        File.WriteAllText(Path.Combine(copy, "map", Subdistricts1948.FileName), "layer: changed\n");
        File.WriteAllText(Path.Combine(copy, "map", "subdistricts_1948.geojson"), "{}");
        Assert.AreEqual(before, ContentLoader.LoadChapter(copy, GameRoot.FrontLineChapter).Hash);
    }

    /// <summary>The default game (the 1947-1949 chapter) draws a region layer from 15 May 1948 without any flag, with its
    /// label shown, over the real sub-districts; nothing before.</summary>
    [Test]
    public void TheFrontLineChapterOutlinesTheRealSubdistricts()
    {
        var root = Boot(saveDir);
        var label = Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Label>("map-layer");
        Assert.Less(root.Bridge!.State.Date, Shown, "the chapter opens in late 1947");
        Assert.IsNull(MapBorders.Current, "nothing drawn before 15 May 1948");
        Assert.AreEqual(DisplayStyle.None, label.style.display.value);
        MoveTo(root, Shown);
        var current = MapBorders.Current;
        Assert.IsNotNull(current, "play draws a region layer without any flag");
        // Since #674 (owner decision of 16 October 2026) the layer shows sides (the 9 July 1948 lines, adjusted); the real
        // sub-districts stay loaded under them as the base (and outline the frontier)
        Assert.AreEqual("control_1948_49:opening", current!.Name);
        StringAssert.Contains("9 July 1948, adjusted", label.text, "labelled on screen");
        Assert.AreEqual(DisplayStyle.Flex, label.style.display.value, "shown while the layer is drawn");
        Assert.AreEqual(Subdistricts1948.Parse(FileText).Layer.Count, root.Regions!.Subdistricts.Layer.Count);
    }

    /// <summary>Rendered, in the gameplay03 frame: the real sub-district borders change the picture, and the "borders"
    /// toggle takes them off again.</summary>
    [UnityTest]
    public IEnumerator TheRealBordersDrawInTheGameplayFrame()
    {
        MoveTo(Boot(saveDir), Shown);
        yield return null;
        MapShaderGlobals.PinWaterTime(100f);
        RenderSample.TiltShift(false);
        var map = Object.FindFirstObjectByType<MapScene>();
        var cam = Camera.main!; var rig = cam.GetComponent<CameraRig>();
        PromoPose.Gameplay03.Apply(rig, map.Frame); rig.TopInset = 0; rig.Apply();
        // Steady frames where they are compared exactly (RenderSample.RenderSteady: the terrain pass's rare transient pixels, #440).
        var on = RenderSample.RenderSteady(cam, 480, 270);
        VisualToggles.Set(MapBorders.ToggleName, false);
        var off = RenderSample.Render(cam, 480, 270);
        var share = BorderTests.Changed(on, off, 0.02f);
        Debug.Log($"SUBDISTRICTS gameplay03: {share:P2} of pixels changed by the 1948 borders");
        Assert.Greater(share, 0.002f, "the 1948 sub-district borders are drawn");
        Assert.Less(share, 0.15f, "outlines, not fills");
        VisualToggles.Set(MapBorders.ToggleName, true);
        Assert.AreEqual(0f, BorderTests.Changed(RenderSample.RenderSteady(cam, 480, 270), on, 0f), "renders repeat exactly");
    }
}
