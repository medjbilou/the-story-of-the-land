#nullable enable
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Boot;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>The occupation's map layers (content/map/occupation_layers.yaml, tools/occupation_bake.py): each part shows from
/// its own sourced date, nothing before June 1967, and the textures are decoded only when a date first needs them. Display
/// only: the sim never reads the file.</summary>
public class OccupationLayersTests
{
    static OccupationLayers Layers => OccupationLayers.Parse(File.ReadAllText(Path.Combine(Paths.Content, "map", OccupationLayers.FileName)));
    static UiText Text(string path) => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);
    static readonly int Grid = Shader.PropertyToID("_ScEraGrid"), A = Shader.PropertyToID("_ScEraA"), B = Shader.PropertyToID("_ScEraB"),
        Tex3 = Shader.PropertyToID("_ScEraTex3");

    [Test]
    public void NothingShowsBeforeTheOccupationOf5June1967()
    {
        var l = Layers;
        Assert.IsFalse(l.On(new DateTime(1967, 6, 4)).Any);
        Assert.IsFalse(l.On(new DateTime(1948, 5, 15)).Any);
        var s = l.On(new DateTime(1967, 6, 5));
        Assert.IsTrue(s.Occupied);
        Assert.IsFalse(s.Oslo || s.Fence || s.Ara || s.Evacuated || s.Footprints, "only the occupied land and the settlements of 1967");
        Assert.AreEqual(0, s.Wall);
        Assert.AreEqual(1967, s.SettlementYear);
    }

    [Test]
    public void EachLayerStartsOnItsSourcedDate()
    {
        var l = Layers;
        void Starts(Func<OccupationLayers.State, bool> part, DateTime day, string what)
        {
            Assert.IsFalse(part(l.On(day.AddDays(-1))), what + " shows the day before " + day.ToString("yyyy-MM-dd"));
            Assert.IsTrue(part(l.On(day)), what + " is missing on " + day.ToString("yyyy-MM-dd"));
        }
        Starts(s => s.Fence, new DateTime(1994, 1, 1), "Gaza's fence");
        Starts(s => s.Oslo, new DateTime(1995, 9, 28), "Oslo II's areas");
        Starts(s => s.Ara, new DateTime(2000, 9, 1), "the access-restricted area");
        Starts(s => s.Wall >= 1, new DateTime(2002, 6, 1), "the wall's route");
        Starts(s => s.Evacuated, new DateTime(2005, 8, 22), "the August 2005 evacuations");
        Starts(s => s.Footprints, new DateTime(2011, 1, 1), "the built-up areas");
        Starts(s => s.Wall == 2, new DateTime(2018, 1, 1), "the wall as mapped in 2018");
        Assert.AreEqual(1977, l.On(new DateTime(1977, 11, 1)).SettlementYear, "settlements by founding year");
    }

    [Test]
    public void TheLegendNamesWhatIsDrawnInBothLanguages()
    {
        var l = Layers;
        foreach (var path in new[] { UiPaths.Strings, UiPaths.StringsArabic })
        {
            var t = Text(path);
            Assert.IsNull(l.Label(new DateTime(1966, 12, 1), k => t[k]), "no legend before June 1967");
            var early = l.Label(new DateTime(1980, 1, 1), k => t[k])!;
            StringAssert.StartsWith(t["map.occ_legend"], early);
            StringAssert.Contains(t["map.occ_land"], early);
            StringAssert.DoesNotContain(t["map.occ_wall"], early);
            var late = l.Label(new DateTime(2024, 1, 1), k => t[k])!;
            foreach (var key in new[] { "map.occ_oslo", "map.occ_wall", "map.occ_fence", "map.occ_ara", "map.occ_settlements", "map.occ_sources" })
                StringAssert.Contains(t[key], late, key + " in " + path);
            StringAssert.Contains(t["map.occ_wall_building"], l.Label(new DateTime(2010, 1, 1), k => t[k])!);
        }
    }

    [Test]
    public void TheTexturesAreDecodedOnlyWhenADateNeedsThemAndLeaveWithTheMap()
    {
        var l = Layers;
        var go = new GameObject("assets");
        try
        {
            var assets = go.AddComponent<MapRuntimeAssets>();
            l.Attach(new GeoFrame(34.2, 35.9, 29.45, 33.35, 160000f, 430000f), assets);
            Shader.SetGlobalTexture(Tex3, null);
            l.Show(new DateTime(1960, 1, 1));
            Assert.AreEqual(0f, Shader.GetGlobalVector(Grid).w, "off before 1967");
            Assert.IsNull(Shader.GetGlobalTexture(Tex3), "nothing decoded before 1967");
            l.Show(new DateTime(2024, 3, 1));
            var tex = Shader.GetGlobalTexture(Tex3);
            Assert.IsNotNull(tex);
            Assert.AreEqual((l.Nx, l.Ny), (tex.width, tex.height));
            Assert.AreEqual(new Vector4(l.Nx, l.Ny, l.DistUnitM, 1f), Shader.GetGlobalVector(Grid));
            Assert.AreEqual(new Vector4(1, 1, 1, 1), Shader.GetGlobalVector(A));
            Assert.AreEqual(new Vector4(2024, 1, 1, 2), Shader.GetGlobalVector(B));
            assets.RunTeardown();
            Assert.AreEqual(0f, Shader.GetGlobalVector(Grid).w, "the map's teardown takes the layer off");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void AMalformedFileNamesTheFileAndTheKey()
    {
        var bad = File.ReadAllText(Path.Combine(Paths.Content, "map", OccupationLayers.FileName)).Replace("oslo_from: 1995-09-28", "oslo_from: 28 Sep 1995");
        var e = Assert.Throws<InvalidDataException>(() => OccupationLayers.Parse(bad));
        StringAssert.Contains(OccupationLayers.FileName, e!.Message);
        StringAssert.Contains("oslo_from", e.Message);
    }

    static GazaDamage Damage => GazaDamage.Parse(File.ReadAllText(Path.Combine(Paths.Content, "map", GazaDamage.FileName)));

    [Test]
    public void GazasDestructionShowsByUnosatsImagesAndSmokesOnlyOutsideTheTruces()
    {
        var d = Damage;
        Assert.AreEqual(14, d.Images.Count);
        Assert.AreEqual(default(GazaDamage.State), d.On(new DateTime(2023, 9, 1)), "nothing before the war");
        var oct = d.On(new DateTime(2023, 10, 1));
        Assert.AreEqual((1, 1, 2), (oct.Shown, oct.SmokeFrom, oct.SmokeTo), "October 2023: the 15 October image; smoke over what it (from 7 October) and the 7 November image record");
        var nov = d.On(new DateTime(2023, 11, 1));
        Assert.AreEqual((3, 2, 4), (nov.Shown, nov.SmokeFrom, nov.SmokeTo), "November 2023: the 7 and 26 November images; the truce covers a week only");
        var feb = d.On(new DateTime(2025, 2, 1));
        Assert.AreEqual(11, feb.Shown, "the 25 February 2025 image");
        Assert.AreEqual(0, feb.SmokeFrom, "no new destruction drawn inside the January-March 2025 ceasefire");
        var after = d.On(new DateTime(2026, 3, 1));
        Assert.AreEqual((14, 0), (after.Shown, after.SmokeFrom), "the last image (11 October 2025) stays; nothing new drawn after the ceasefire");
    }

    [Test]
    public void GazasDamageLegendNamesTheImageDateInBothLanguages()
    {
        var d = Damage;
        foreach (var path in new[] { UiPaths.Strings, UiPaths.StringsArabic })
        {
            var t = Text(path);
            Assert.IsNull(d.Label(new DateTime(2023, 6, 1), k => t[k]));
            var l = d.Label(new DateTime(2024, 6, 1), k => t[k])!;
            StringAssert.Contains(Control1948.DateText(new DateTime(2024, 5, 3), k => t[k]), l, path);
            StringAssert.Contains(t["map.war_smoke"], l, path);
            StringAssert.DoesNotContain(t["map.war_smoke"], d.Label(new DateTime(2025, 2, 1), k => t[k])!, path);
        }
    }

    /// <summary>Not a check: the layers as a player sees them, into STORY_OCC_DIR (1920x1080): the whole map in 1980, 1996
    /// and 2024, the wall round al-Quds and Qalandiya, Qalqilya up close, and Gaza in 2004 and 2024. Run on demand:
    /// -testFilter OccupationLayersTests.Capture.</summary>
    [UnityTest, Explicit]
    public IEnumerator Capture()
    {
        var dir = Environment.GetEnvironmentVariable("STORY_OCC_DIR") ?? Path.Combine(Path.GetTempPath(), "storyoftheland-occ");
        Directory.CreateDirectory(dir);
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>();
        map.Build(GameInputs.FromDisk(Paths.Content, Paths.StreamingMap).Map);
        MapShaderGlobals.PinWaterTime(100f);
        var cam = Camera.main!;
        var rig = cam.GetComponent<CameraRig>();
        var shots = new (string name, double lon, double lat, float dist, int y, int m)[]
        {
            ("map-1980", 35.05, 31.75, CameraController.MaxDistance, 1980, 1), ("map-1996", 35.05, 31.75, CameraController.MaxDistance, 1996, 1),
            ("map-2024", 35.05, 31.75, CameraController.MaxDistance, 2024, 1), ("quds-2024", 35.22, 31.83, 30000f, 2024, 1),
            ("qalqilya-2024", 34.98, 32.19, 12000f, 2024, 1), ("gaza-2004", 34.40, 31.42, 45000f, 2004, 1), ("gaza-2023-11", 34.40, 31.42, 45000f, 2023, 11),
            ("gaza-2024-06", 34.40, 31.42, 45000f, 2024, 6), ("gaza-2025-02", 34.40, 31.42, 45000f, 2025, 2), ("gaza-2025-12", 34.40, 31.42, 45000f, 2025, 12),
            ("gazacity-2025-12", 34.46, 31.51, 15000f, 2025, 12),
        };
        try
        {
            foreach (var s in shots)
            {
                map.Occupation!.Show(new DateTime(s.y, s.m, 1));
                map.Damage!.Show(new DateTime(s.y, s.m, 1));
                new PromoPose(s.name, s.lon, s.lat, s.dist, 0f).Apply(rig, map.Frame);
                yield return null;
                var shot = RenderSample.Render(cam, 1920, 1080);
                var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                tex.SetPixels(shot.Pixels); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, s.name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }
}
