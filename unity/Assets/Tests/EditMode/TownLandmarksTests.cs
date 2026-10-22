#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.Map.Collection;
using StoryOfTheLand.Game.Map.Kits;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Issue #663, owner ruling of 17 October 2026: "The nine whole-town models ship (this reverses the 15 Oct 'layout
/// references only' ruling). They are slimmed, streamed after the first frame, and pass the load gate." Tal Heuberger's
/// seven town models the family game draws stand as overview landmarks on their towns (#507's mapping), streamed through
/// CollectionStream, and they fail open. (The ma'abara and the shikun models are not drawn.)</summary>
public class TownLandmarksTests
{
    Action<string, CollectionDetail, Action<GameObject?>> saved = null!;
    readonly List<(string id, CollectionDetail detail, Action<GameObject?> done)> asked = new List<(string, CollectionDetail, Action<GameObject?>)>();

    [SetUp]
    public void Seam()
    {
        saved = TownLandmarks.Request;
        asked.Clear();
        TownLandmarks.Request = (id, detail, done) => asked.Add((id, detail, done));
    }

    [TearDown]
    public void Reset()
    {
        TownLandmarks.Request = saved;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    static readonly string[] Towns =
    {
        "tel-aviv-white-city-v1", "jerusalem-stone-city-v1", "haifa-port-city-v1", "moshava-town-v1", "nahalal-moshav-v1",
        "kibbutz-compound-v2", "stone-village-v1",
    };

    static MapScene OpenMap()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>();
        map.Build();
        return map;
    }

    static GameObject Far(string id) => AssetDatabase.LoadAssetAtPath<GameObject>(CollectionCatalog.PrefabPath(id, CollectionDetail.Far));

    void AnswerAll()
    {
        var pending = asked.ToList();
        asked.Clear();
        foreach (var (id, _, done) in pending) done(Far(id));
    }

    [Test]
    public void EveryTownModelIsUsedAndEachIsInTheStreamedTownsBundle()
    {
        var used = TownLandmarks.ById.Values.Concat(TownLandmarks.ByType.Values).Distinct();
        CollectionAssert.AreEquivalent(Towns, used, "the seven town models, as #507 mapped them");
        foreach (var id in Towns) Assert.AreEqual(CollectionCatalog.Towns, CollectionCatalog.BundleOf(id), id);
        Assert.AreEqual(TownLandmarks.TelAviv, TownLandmarks.ById["tel_aviv"]);
        Assert.AreEqual(TownLandmarks.Kibbutz, TownLandmarks.ByType["kibbutz"]);
    }

    /// <summary>The towns with a whole-town model, in the file's order.</summary>
    static List<Settlement> Mapped(MapScene map) => map.Settlements.Where(s => TownLandmarks.ModelFor(s) != null).ToList();

    TownLandmarks ShowAll(MapScene map)
    {
        var c = FamilyKit.Chapter();
        var t = TownLandmarks.ShowAt(map)!;
        AnswerAll();
        return t;
    }

    /// <summary>Renders the map's own camera at <paramref name="distanceM"/> over <paramref name="at"/> (the hooks run as
    /// in the player: RenderPipelineManager.beginCameraRendering).</summary>
    static void RenderAt(MapScene map, Vector2 at, float distanceM)
    {
        var cam = Camera.main; var rig = cam.GetComponent<CameraRig>();
        rig.LookAt = new Vector3(at.x, 0f, at.y); rig.Distance = distanceM; rig.Yaw = 0f; rig.Apply();
        RenderSample.Render(cam, 320, 180);
    }

    /// <summary>Owner ruling, 18 October 2026 (on #917, #663, #659 and #700): the towns are Tal Heuberger's models, "Whole model far,
    /// buildings near". Far off, each mapped town's whole-town model stands ON its town, inside its kit footprint
    /// (MapScene.KitFootprints), as wide as its core (TownLandmarks.CoreOf: where most of its building models stand), not
    /// beside the town at a fixed 6 km.</summary>
    [Test]
    public void EveryTownLandmarkStandsOnItsTownSizedToItsCore()
    {
        var map = OpenMap();
        var t = ShowAll(map);
        CollectionAssert.IsEmpty(t.Missing);
        var footprint = new Dictionary<string, (Vector2 centre, float radius)>();
        foreach (var (centre, radius, id) in map.KitFootprints) if (!footprint.ContainsKey(id)) footprint[id] = (centre, radius);
        var mapped = Mapped(map);
        Assert.Greater(mapped.Count, 20);
        foreach (var s in mapped)
        {
            var child = t.transform.Find(s.Id);
            Assert.IsNotNull(child, s.Id + " has its landmark");
            var (centre, radius) = footprint[s.Id];
            var b = WorldBounds(child!.gameObject);
            Assert.Less(new Vector2(b.center.x - centre.x, b.center.z - centre.y).magnitude, radius, $"{s.Id} stands on its town");
            Assert.LessOrEqual(Mathf.Max(b.size.x, b.size.z), 1.12f * 2f * radius, $"{s.Id} is no wider than its footprint ({radius:0} m radius)");
            Assert.IsTrue(t.Seats.TryGetValue(s.Id, out var seat), s.Id);
            Assert.AreEqual(seat.Edge, Mathf.Max(b.size.x, b.size.z), 0.12f * seat.Edge, $"{s.Id} is as wide as its core ({0.5f * seat.Edge:0} m radius)");
        }
    }

    /// <summary>The coordinator for the owner, 18 October 2026: the whole-town model is sized to its town's core, "so Tel Aviv
    /// doesn't cover Jaffa and Haifa doesn't reach into the bay". No town's whole-town model covers another game settlement:
    /// no other settlement's kit footprint centre lies inside its ground rectangle (its world bounds on x/z).</summary>
    [Test]
    public void NoTownLandmarkCoversAnotherSettlement()
    {
        var map = OpenMap();
        var t = ShowAll(map);
        var ids = map.Settlements.Select(s => s.Id).ToHashSet();
        var centres = new Dictionary<string, Vector2>();
        foreach (var (c, _, id) in map.KitFootprints) if (ids.Contains(id) && !centres.ContainsKey(id)) centres[id] = c;
        var bad = new List<string>();
        foreach (var s in Mapped(map))
        {
            var b = WorldBounds(t.transform.Find(s.Id)!.gameObject);
            foreach (var (id, c) in centres)
                if (id != s.Id && c.x > b.min.x && c.x < b.max.x && c.y > b.min.z && c.y < b.max.z) bad.Add($"{s.Id}'s model covers {id}");
        }
        CollectionAssert.IsEmpty(bad);
    }

    /// <summary>No whole-town model reaches far into the sea or a lake: at most TownLandmarks.MaxWaterShare
    /// (a fifth; a coastal town keeps its shore) of the ground under its disc (its largest edge across) is water.</summary>
    [Test]
    public void NoTownLandmarkReachesIntoTheSea()
    {
        var map = OpenMap();
        var t = ShowAll(map);
        var ground = map.SceneryGround();
        var bad = new List<string>(); var lines = new List<string>();
        foreach (var s in Mapped(map))
        {
            var b = WorldBounds(t.transform.Find(s.Id)!.gameObject);
            var c = new Vector2(b.center.x, b.center.z);
            float r = 0.5f * Mathf.Max(b.size.x, b.size.z);
            int n = 0, wet = 0;
            for (int i = 0; i <= 10; i++)
                for (int k = 0; k <= 10; k++)
                {
                    var q = c + new Vector2(-r + 2f * r * i / 10f, -r + 2f * r * k / 10f);
                    if ((q - c).sqrMagnitude > r * r) continue;
                    n++; if (ground.Water(q.x, q.y)) wet++;
                }
            float share = n == 0 ? 0f : (float)wet / n;
            lines.Add($"{s.Id} {share:0.00}");
            if (share > 0.2f + 1e-3f) bad.Add($"{s.Id}: {share:P0} of its model's ground is water");
        }
        TestContext.WriteLine(string.Join(", ", lines));
        CollectionAssert.IsEmpty(bad);
    }

    /// <summary>The swap with #700's building models: the landmarks draw where TownModelLayer draws the kits' far view and
    /// hide where it draws the building models (TownModelLayer.CloseAt: in below CloseEnterM, out above CloseExitM), with
    /// the same hysteresis, at the start view (120 km), the gameplay view (60 km), both sides of the band and the closest
    /// zoom.</summary>
    [Test]
    public void TheLandmarksSwapWithTheBuildingModelsAtTheThreshold()
    {
        var map = OpenMap();
        var t = ShowAll(map);
        var layer = map.transform.Find(KitLayer.RootName).GetComponentInChildren<TownModelLayer>();
        Assert.IsTrue(layer.HasModels);
        var renderers = t.GetComponentsInChildren<Renderer>(true);
        Assert.Greater(renderers.Length, 0);
        var tel = map.KitFootprints.First(f => f.id == "tel_aviv").centre;
        foreach (var (d, close, why) in new[]
        {
            (120000f, false, "the start view"), (60000f, false, "the gameplay view"),
            (TownModelLayer.CloseEnterM + 1000f, false, "inside the band, coming in"), (TownModelLayer.CloseEnterM - 1000f, true, "past the threshold"),
            (TownModelLayer.CloseExitM - 2000f, true, "inside the band, going out"), (TownModelLayer.CloseExitM + 1000f, false, "out of the band"),
            (CameraController.MinDistance, true, "the closest zoom"),
        })
        {
            RenderAt(map, tel, d);
            Assert.AreEqual(close, layer.LastClose, $"{d / 1000f} km, {why}: the building models");
            Assert.IsTrue(renderers.All(r => r.forceRenderingOff == close), $"{d / 1000f} km, {why}: the whole-town models " + (close ? "hide" : "draw"));
        }
    }

    /// <summary>No procedural houses in a mapped town at any zoom (owner, 18 October 2026): its kit mesh never draws, its
    /// close mesh (the kit without its buildings: its mosques, churches, walls and port, as #700 keeps them) draws far and
    /// near. A town without a whole-town model keeps #700's swap: its kit far off, its close mesh up close.</summary>
    [Test]
    public void AMappedTownDrawsNoProceduralHousesAtAnyZoom()
    {
        var map = OpenMap();
        var t = ShowAll(map);
        var root = map.transform.Find(KitLayer.RootName);
        var mapped = Mapped(map);
        var plain = map.Settlements.First(s => TownLandmarks.ModelFor(s) == null);
        var plainKit = root.Find(plain.Id).GetComponent<MeshRenderer>();
        foreach (var (d, close) in new[] { (120000f, false), (60000f, false), (TownModelLayer.CloseEnterM - 1000f, true), (CameraController.MinDistance, true) })
        {
            RenderAt(map, map.KitFootprints.First(f => f.id == "tel_aviv").centre, d);
            foreach (var s in mapped)
            {
                Assert.IsTrue(root.Find(s.Id).GetComponent<MeshRenderer>().forceRenderingOff, $"{s.Id}'s kit houses at {d / 1000f} km");
                var closeR = root.Find(s.Id + KitLayer.CloseSuffix)?.GetComponent<MeshRenderer>();
                if (closeR != null) Assert.IsFalse(closeR.forceRenderingOff, $"{s.Id}'s landmarks and ground at {d / 1000f} km");
            }
            Assert.AreEqual(close, plainKit.forceRenderingOff, $"{plain.Id} (no whole-town model) keeps #700's swap at {d / 1000f} km");
        }
    }

    /// <summary>The fail-open check: without #700's building models (no model list) nothing would draw a mapped town up
    /// close, so its whole-town model stays at every zoom, and its kit houses still never draw.</summary>
    [UnityTest]
    public IEnumerator WithoutTheBuildingModelsTheWholeTownModelStaysUpClose()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>();
        map.ConfigureTownModels(null!);
        LogAssert.Expect(LogType.Error, new Regex("town models: the map scene has no model list"));
        map.Build();
        yield return null;
        var t = ShowAll(map);
        var root = map.transform.Find(KitLayer.RootName);
        Assert.IsFalse(root.GetComponentInChildren<TownModelLayer>().HasModels);
        RenderAt(map, map.KitFootprints.First(f => f.id == "tel_aviv").centre, CameraController.MinDistance);
        Assert.IsTrue(t.GetComponentsInChildren<Renderer>(true).All(r => !r.forceRenderingOff), "the whole-town models draw at the closest zoom");
        Assert.IsTrue(root.Find("tel_aviv").GetComponent<MeshRenderer>().forceRenderingOff, "Tel Aviv's kit houses still never draw");
    }

    /// <summary>Owner, 18 October 2026: the models "seem to be on their side on the map". Every Collection Far prefab's root
    /// carries the FBX axis conversion (-90 degrees about X; the .fbx.meta files have bakeAxisConversion 0), so a landmark
    /// must keep the prefab's own rotation under its yaw: its rotation is the yaw times the prefab's, and its height on the
    /// map is the model's own height (at the landmark's scale), not its length.</summary>
    [Test]
    public void EveryLandmarkStandsUprightAsItsModelDoes()
    {
        var map = OpenMap();
        var c = FamilyKit.Chapter();
        var t = TownLandmarks.ShowAt(map)!;
        AnswerAll();
        Assert.Greater(t.Placed.Count, 0);
        var heights = new Dictionary<string, float>();
        foreach (var (key, model, _) in t.Placed)
        {
            var prefab = Far(model);
            var child = t.transform.Find(key);
            Assert.IsNotNull(child, key);
            var tilt = t.Seats.TryGetValue(key, out var seat) ? Quaternion.FromToRotation(Vector3.up, seat.Normal) : Quaternion.identity;
            var want = t.transform.rotation * tilt * Quaternion.Euler(0f, TownLandmarks.YawOf(model), 0f) * prefab.transform.localRotation;
            Assert.Less(Quaternion.Angle(want, child!.rotation), 0.01f, $"{key} ({model}) keeps its prefab's rotation under its yaw and its seat's tilt");
            if (!heights.TryGetValue(model, out var h))
            {
                // The model as its prefab stands (unparented, its own rotation): a yaw about the vertical keeps this height.
                var probe = Object.Instantiate(prefab);
                try { heights[model] = h = WorldBounds(probe).size.y / probe.transform.localScale.y; }
                finally { Object.DestroyImmediate(probe); }
            }
            // Untilted (the seat's tilt taken off about the base), the landmark is as tall as its model at its scale.
            var keep = child.rotation; child.rotation = Quaternion.Inverse(tilt) * child.rotation;
            float scale = child.localScale.y;
            try { Assert.AreEqual(h * scale, WorldBounds(child.gameObject).size.y, 0.01f * h * scale, $"{key} ({model}) is as tall as its model"); }
            finally { child.rotation = keep; }
        }
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>The whole-town model on a slope (the kibbutz model cut into its hill at the closest zoom, on #917): each one
    /// is tilted to the least-squares plane of the ground under its footprint (at most TownLandmarks.MaxTiltDeg) and seated at
    /// its lowest point, so it never floats: no land under its footprint lies below its base plane. Degania Alef stands on a
    /// slope, so its model leans with the ground.</summary>
    [Test]
    public void AWholeTownModelFollowsItsGroundAndNeverFloats()
    {
        var map = OpenMap();
        var t = ShowAll(map);
        var ground = map.SceneryGround();
        var lines = new List<string>();
        foreach (var s in Mapped(map))
        {
            Assert.IsTrue(t.Seats.TryGetValue(s.Id, out var seat), s.Id);
            float tilt = Vector3.Angle(Vector3.up, seat.Normal);
            Assert.LessOrEqual(tilt, TownLandmarks.MaxTiltDeg + 0.01f, s.Id);
            float r = 0.5f * seat.Edge, deepest = 0f;
            for (int i = 0; i <= 10; i++)
                for (int k = 0; k <= 10; k++)
                {
                    var q = seat.Centre + new Vector2(-r + 2f * r * i / 10f, -r + 2f * r * k / 10f);
                    if ((q - seat.Centre).sqrMagnitude > 0.999f * r * r || !ground.Inside(q.x, q.y) || ground.Water(q.x, q.y)) continue;
                    var d = q - seat.Centre;
                    float plane = seat.BaseY - (seat.Normal.x * d.x + seat.Normal.z * d.y) / seat.Normal.y;
                    float above = ground.Y(q.x, q.y) - plane;
                    Assert.GreaterOrEqual(above, -0.5f, $"{s.Id} floats {-above:0.0} m above its ground at {q}");
                    deepest = Mathf.Max(deepest, above);
                }
            lines.Add($"{s.Id} tilt {tilt:0.0} deg, ground up to {deepest:0} m above its base");
        }
        TestContext.WriteLine(string.Join("; ", lines));
        Assert.Greater(Vector3.Angle(Vector3.up, t.Seats["degania_alef"].Normal), 0.5f, "Degania Alef leans with its slope");
    }

    [Test]
    public void ASpotIsTheFirstFreeOneAndNoneWhenAllAreTaken()
    {
        var town = new Vector2(10000f, 10000f);
        var discs = new List<(Vector2 centre, float radius)> { (town, 1500f), (town + new Vector2(4000f, 4000f), 2500f) };
        var spot = TownLandmarks.Spot(town, 1500f, 2000f, (p, r) => TownLandmarks.Clear(p, r, discs));
        Assert.IsTrue(spot.HasValue);
        Assert.IsTrue(TownLandmarks.Clear(spot!.Value, 2000f, discs), "clear of every disc");
        Assert.Less((spot.Value - town).magnitude, 1500f + 2000f * (TownLandmarks.Rings + 1), "beside the town");
        Assert.IsNull(TownLandmarks.Spot(town, 1500f, 2000f, (p, r) => false));
    }

    /// <summary>Nothing is drawn until the stream answers (it never serves a request in the frame that made it), and only
    /// the strategic zoom's mesh is asked for, once per model.</summary>
    [Test]
    public void TheLandmarksStreamInOncePerModel()
    {
        var map = OpenMap();
        var c = FamilyKit.Chapter();
        var t = TownLandmarks.ShowAt(map)!;
        Assert.IsNotNull(t);
        CollectionAssert.IsEmpty(t.Placed, "nothing before the stream answers");
        Assert.AreEqual(0, t.transform.childCount);
        Assert.Greater(asked.Count, 0);
        Assert.IsTrue(asked.All(a => a.detail == CollectionDetail.Far), "the far mesh only");
        Assert.AreEqual(asked.Count, asked.Select(a => a.id).Distinct().Count(), "each model once");
        AnswerAll();

        CollectionAssert.IsEmpty(t.Missing, "every wanted landmark found a spot");
        Assert.AreEqual(t.Wanted.Count, t.Placed.Count);
        Assert.AreEqual(1, t.Placed.Count(p => p.Key == "tel_aviv" && p.Model == TownLandmarks.TelAviv));
        Assert.AreEqual(map.Settlements.Count(s => s.Type == "kibbutz"), t.Placed.Count(p => p.Model == TownLandmarks.Kibbutz));
        // Owner on #917 (17 October 2026): "Keep at all seven" arab_town rows.
        Assert.AreEqual(7, map.Settlements.Count(s => s.Type == "arab_town"));
        Assert.AreEqual(7, t.Placed.Count(p => p.Model == TownLandmarks.StoneVillage), "the stone village at all seven");
    }

    [Test]
    public void AModelThatDoesNotStreamWarnsAndTheMapRunsOn()
    {
        var map = OpenMap();
        var c = FamilyKit.Chapter();
        var t = TownLandmarks.ShowAt(map)!;
        var tel = asked.Single(a => a.id == TownLandmarks.TelAviv);
        LogAssert.Expect(LogType.Warning, new Regex("^LANDMARKS: the town model tel-aviv-white-city-v1 did not stream"));
        tel.done(null);
        Assert.AreEqual(0, t.transform.childCount);
        foreach (var (id, _, done) in asked.Where(a => a.id != TownLandmarks.TelAviv).ToList()) done(Far(id));
        Assert.Greater(t.Placed.Count, 0, "the others are drawn");
        CollectionAssert.DoesNotContain(t.Placed.Select(p => p.Key), "tel_aviv");
        CollectionAssert.Contains(t.Missing, "tel_aviv");
    }
}
