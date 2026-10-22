#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.Map.Kits;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>F6 graphics (owner, 19 Oct 2026: "good texture and graphics ... like big games ... render smooth max fps
/// possible"): the trees' Palestinian tones, the Arab villages' sabr hedges and olive groves, the ground detail and its CC0
/// texture, the desktop tier's MSAA, and the frame-pacing knobs Settings uses.</summary>
public class F6GraphicsTests
{
    [TearDown]
    public void Restore()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        VisualToggles.Clear();
        KitScale.Reset();
        GraphicsQuality.SetFramePacing(vsync: true, cap: 0);
        GraphicsQuality.Shadows = GraphicsQuality.ShadowChoice.Soft; GraphicsQuality.Detail = true; GraphicsQuality.Haze = true;
        GraphicsQuality.Antialiasing = GraphicsQuality.AntialiasingChoice.Auto; GraphicsQuality.ReducedMotion = false;
        if (GraphicsQuality.RenderScale != null) GraphicsQuality.RenderScale = null;
        if (GraphicsQuality.Current != GraphicsQuality.Choice.Auto) GraphicsQuality.Apply(GraphicsQuality.Choice.Auto);
    }

    static MapScene OpenMap()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>(); map.Build();
        return map;
    }

    /// <summary>A tree's vertex colours (sRGB) and heights (real m), part by part as Vegetation.Build lays them.</summary>
    static List<(Color c, float y)> Vertices(TreeKind kind)
    {
        var mesh = Vegetation.BuildMesh(kind);
        try { return mesh.colors32.Zip(mesh.vertices, (c, v) => ((Color)c, v.y)).ToList(); }
        finally { Object.DestroyImmediate(mesh); }
    }

    static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    /// <summary>The olive's crown is silver grey-green (green at least red, little saturation) and paler on top; its trunk
    /// (under 1.75 m) is dark and much darker than the crown.</summary>
    [Test]
    public void TheOlivesAreSilverGreyGreenWithDarkGnarledTrunks()
    {
        var v = Vertices(TreeKind.Olive);
        var crown = v.Where(x => x.y > 1.75f).Select(x => x.c).ToList(); var trunk = v.Where(x => x.y < 1.75f).Select(x => x.c).ToList();
        Assert.Greater(trunk.Count, 30, "a forked trunk: three shafts, not one");
        foreach (var c in crown)
        {
            Color.RGBToHSV(c, out float h, out float s, out _);
            Assert.GreaterOrEqual(c.g, c.r - 1f / 255f, $"crown {c}: green-grey, not brown");
            Assert.Less(s, 0.25f, $"crown {c}: grey, not saturated");
        }
        float crownLuma = crown.Average(Luma), trunkLuma = trunk.Average(Luma);
        Assert.Less(trunkLuma, 0.6f * crownLuma, "the trunk is dark against the crown");
        Assert.Greater(crown.Max(Luma), 0.62f, "the sunlit tops reach the leaves' silver (Vegetation.OliveTop)");
    }

    /// <summary>The citrus crown is dark green (green well above red and blue) with orange fruit dots: 3-12% of its vertices
    /// fruit, never a coat of them (promo 03's crowns read as orange-brown blobs).</summary>
    [Test]
    public void TheCitrusIsDarkGlossyGreenWithOrangeDots()
    {
        foreach (var kind in new[] { TreeKind.Citrus, TreeKind.CitrusPatch })
        {
            var crown = Vertices(kind).Where(x => x.y > 1.0f).Select(x => x.c).ToList();
            bool Fruit(Color c) => c.r > 0.6f && c.r > c.g + 0.2f;
            var leaves = crown.Where(c => !Fruit(c)).ToList();
            float fruit = crown.Count(Fruit) / (float)crown.Count;
            Assert.That(fruit, Is.InRange(0.03f, 0.12f), $"{kind}: the fruit's share of the crown");
            Assert.Greater(leaves.Count(c => c.g > c.r * 1.3f && c.g > c.b * 1.3f) / (float)leaves.Count, 0.95f, $"{kind}: the leaves are green");
            Assert.Less(leaves.Average(Luma), 0.25f, $"{kind}: dark green, not pale");
        }
    }

    /// <summary>The prickly pear: a clump of blue-green pads with a few red-orange fruits, every normal a unit vector (the
    /// WebGL NaN guard of KitTests.EveryPartHasAUnitNormal), not taller than a man and a half.</summary>
    [Test]
    public void TheSabrIsAClumpOfBlueGreenPads()
    {
        var km = Vegetation.Build(TreeKind.Cactus);
        Assert.AreEqual(0, km.BadNormals);
        var v = Vertices(TreeKind.Cactus);
        var pads = v.Where(x => x.y > 0.6f).Select(x => x.c).Where(c => !(c.r > c.g + 0.2f)).ToList();
        Assert.Greater(pads.Count(c => c.g > c.r && c.g > c.b) / (float)pads.Count, 0.95f, "green pads");
        Assert.Less(v.Max(x => x.y), 3.6f);
        Assert.Greater(Vegetation.CrownRadiusM(TreeKind.Cactus), 0.9f);
    }

    /// <summary>Round the Arab villages of 1948 (the localities' "village" and "arab_town" rows) the map plants sabr hedges
    /// near their edges and, in the hill country, olive groves beside them; and only there: no cactus stands far from every
    /// village.</summary>
    [UnityTest]
    public IEnumerator TheArabVillagesHaveSabrHedgesAndOliveGroves()
    {
        var map = OpenMap();
        yield return null;
        var veg = map.GetComponentInChildren<VegetationLayer>(); Assert.IsNotNull(veg);
        // The game's Arab towns and the 1948 localities' villages, each with its hedge's reach: its footprint's 1.4 radii
        // (Vegetation.Plant) plus a clump's jitter and crown.
        float hex = map.Hexes.hex_size_km * 1000f;
        var villages = map.Settlements.Concat(map.Localities.Select(p => p.Settlement)).Where(s => s.Type == "village" || s.Type == "arab_town")
            .Select(s => { var w = map.Frame.ToWorld(s.Lon, s.Lat, 0); return (c: new Vector2(w.x, w.z), reach: 1.4f * SettlementKits.FootprintRadiusM(s, hex) + 1000f); }).ToList();
        Assert.Greater(villages.Count, 200, "the 1948 Arab villages");
        var cactus = veg.Planted().Where(t => t.kind == TreeKind.Cactus).Select(t => new Vector2(t.m.m03, t.m.m23)).ToList();
        bool By((Vector2 c, float reach) v, Vector2 p) => (p - v.c).sqrMagnitude < v.reach * v.reach;
        int withHedge = villages.Count(v => cactus.Any(p => By(v, p)));
        TestContext.WriteLine($"{cactus.Count} sabr clumps round {villages.Count} villages; {withHedge} villages with a clump in reach");
        // Measured 2026-10-20: 930 clumps round 257 Arab villages and towns, 226 of them (88%) with a clump in their own reach
        // (the rest lose their arcs to neighbours' lots, routes and the grid guard); 249 of the localities' 250 within 12 km.
        Assert.Greater(cactus.Count, 3 * villages.Count, "hedges round the villages, not a clump each");
        Assert.IsTrue(cactus.All(p => villages.Any(v => By(v, p))), "every clump stands by a village");
        Assert.Greater(withHedge, 0.85f * villages.Count, "nearly every village has a hedge");
    }

    /// <summary>The ground detail: its CC0 texture imports as linear, repeating, mipmapped data and loads from Resources; the
    /// map sets it for the shaders and registers the "detail" toggle, which on the desktop tier changes the hill country's
    /// pixels at a close zoom (the shader compiles it out on WebGL).</summary>
    [UnityTest]
    public IEnumerator TheGroundDetailDrawsTheCC0TextureOnTheHills()
    {
        var path = "Assets/Resources/" + MapShaderGlobals.DetailResource + ".png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path); Assert.IsNotNull(imp, path);
        Assert.IsFalse(imp.sRGBTexture, "luminance data, not colour"); Assert.IsTrue(imp.mipmapEnabled, "the mips fade it with distance");
        Assert.AreEqual(TextureWrapMode.Repeat, imp.wrapMode);
        var tex = Resources.Load<Texture2D>(MapShaderGlobals.DetailResource); Assert.IsNotNull(tex);
        Assert.AreEqual(1024, tex.width, "1K, as F6's download budget asks");
        int saved = QualitySettings.GetQualityLevel(), high = System.Array.IndexOf(QualitySettings.names, GraphicsQuality.HighTier);
        QualitySettings.SetQualityLevel(high, true);   // the desktop tier: the Low tier skips the detail (MapShaderGlobals.DetailAt)
        try { yield return Draws(tex); }
        finally { QualitySettings.SetQualityLevel(saved, true); }
    }

    IEnumerator Draws(Texture2D tex)
    {
        var map = OpenMap();
        yield return null;
        Assert.AreSame(tex, Shader.GetGlobalTexture(MapShaderGlobals.DetailTex));
        Assert.Contains("detail", VisualToggles.Names.ToList());
        var cam = Camera.main; var rig = cam.GetComponent<CameraRig>();
        new PromoPose("judea15", 35.12, 31.72, 15000f, 0f).Apply(rig, map.Frame); rig.TopInset = 0; rig.Apply();
        RenderSample.TiltShift(false);
        VisualToggles.Set("trees", false); VisualToggles.Set("kits", false);
        VisualToggles.Set("detail", true); var on = RenderSample.Render(cam, 640, 360);
        VisualToggles.Set("detail", false); var off = RenderSample.Render(cam, 640, 360);
        VisualToggles.Set("detail", true);
        RenderSample.TiltShift(true);
        int changed = 0, n = 0;
        for (int y = 40; y < 320; y += 2)
            for (int x = 0; x < 640; x += 2, n++)
                if (Mathf.Abs(RenderSample.Luma(on.At(x, y)) - RenderSample.Luma(off.At(x, y))) > 2f / 255f) changed++;
        TestContext.WriteLine($"judea15: the detail changes {changed}/{n} sampled pixels");
        Assert.Greater(changed, n / 4, "the detail shows on the Judean hills");
    }

    /// <summary>The detail is the desktop tier's: its strengths there with the toggle on, 0 on any other level (the Low tier's
    /// GPU then skips it in StoryOfTheLandDetail.hlsl's uniform branch) or with the toggle off.</summary>
    [Test]
    public void TheGroundDetailIsTheDesktopTiers()
    {
        var on = new Vector4(MapShaderGlobals.DetailStrength, MapShaderGlobals.TerraceStrength, 0f, 0f);
        Assert.AreEqual(on, MapShaderGlobals.DetailAt(1, true, on, 1));
        Assert.AreEqual(Vector4.zero, MapShaderGlobals.DetailAt(0, true, on, 1), "the Low tier");
        Assert.AreEqual(Vector4.zero, MapShaderGlobals.DetailAt(1, false, on, 1), "the toggle off");
        Assert.AreEqual(Vector4.zero, MapShaderGlobals.DetailAt(1, true, on, -1), "a build without the desktop tier");
    }

    /// <summary>F6 round 4 (no "show and hide"): every distance switch has a band. A tree tile, once within the LOD or the
    /// shadow distance, stays so until 10% beyond it, and a town tile keeps its model level while the building is within 0.15 of a
    /// halving of that level's range, so a camera resting on a boundary never flips either back and forth.</summary>
    [Test]
    public void LevelSwitchesHoldABandAroundTheirDistance()
    {
        float reach = VegetationLayer.LodM;
        Assert.IsTrue(VegetationLayer.Within(reach * 1.05f, reach, wasWithin: true), "a near tile stays near just past the line");
        Assert.IsFalse(VegetationLayer.Within(reach * 1.15f, reach, wasWithin: true));
        Assert.IsTrue(VegetationLayer.Within(reach * 0.95f, reach, wasWithin: false), "a far tile comes in at the line, as before");
        Assert.IsFalse(VegetationLayer.Within(reach * 1.05f, reach, wasWithin: false));
        // A 1 km building at 60 degrees on a 1080 px screen: level 1 from about 30.9 km (256 px).
        float boundary = 1000f / (2f * Mathf.Tan(30f * Mathf.Deg2Rad)) * 1080f / (TownModelLayer.FullDetailPx / 2f);
        int fresh = TownModelLayer.LodFor(1000f, boundary * 0.98f, 60f, 1080f, 4);
        int held = TownModelLayer.LodFor(1000f, boundary * 1.02f, 60f, 1080f, 4, previous: fresh);
        Assert.AreEqual(fresh, held, "just across the boundary the tile keeps its level");
        Assert.AreNotEqual(fresh, TownModelLayer.LodFor(1000f, boundary * 1.3f, 60f, 1080f, 4, previous: fresh), "well across, it switches");
        Assert.AreEqual(TownModelLayer.LodFor(1000f, boundary * 1.02f, 60f, 1080f, 4), TownModelLayer.LodFor(1000f, boundary * 1.02f, 60f, 1080f, 4, -1));
    }

    /// <summary>F6 round 4: the kit-to-model swap dissolves over TownModelLayer.FadeSeconds in play; an edit-mode render
    /// (captures, tests) takes the end state at once, with the shared kit material and no fade block on any kit renderer,
    /// so nothing discards outside a swap (Kit.shader's _SC_FADE variant only).</summary>
    [UnityTest]
    public IEnumerator TheTownSwapEndsWholeAndDiscardFree()
    {
        var map = OpenMap();
        yield return null;
        var cam = Camera.main; var rig = cam.GetComponent<CameraRig>();
        var kits = map.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "StoryOfTheLand/Kit").ToList();
        Assert.Greater(kits.Count, 10);
        foreach (var km in new[] { 30000f, 60000f, 30000f })
        {
            new PromoPose("swap", 34.78, 32.08, km, 10f).Apply(rig, map.Frame);
            RenderSample.Render(cam, 64, 36);
            Assert.IsTrue(kits.All(r => !r.HasPropertyBlock() && !r.sharedMaterial.IsKeywordEnabled("_SC_FADE")), $"{km / 1000f} km: whole");
        }
        Assert.IsTrue(TownModelLayer.FadeSeconds > 0.1f && TownModelLayer.FadeSeconds < 1f);
    }

    sealed class SlowSource : StoryOfTheLand.Game.Map.Collection.CollectionStream.ISource
    {
        public readonly List<string> Loads = new List<string>();
        public GameObject? Prefab;
        public IEnumerator Open(string bundle, System.Action<string?> done) { yield return null; done(null); }
        public IEnumerator Load(string bundle, string path, System.Action<GameObject?> done) { Loads.Add(path); yield return null; done(Prefab); }
    }

    /// <summary>F6 round 4 (no pop-in): the stream answers its callers together, in one frame, once their queue is empty; and
    /// the prefetch loads only between callers, so a caller that asks while it runs is served first.</summary>
    [Test]
    public void TheStreamAnswersItsCallersTogetherAndBeforeThePrefetch()
    {
        var go = new GameObject("prefab");
        try
        {
            var src = new SlowSource { Prefab = go };
            var stream = new StoryOfTheLand.Game.Map.Collection.CollectionStream(src);
            var ids = StoryOfTheLand.Game.Map.Collection.CollectionCatalog.Ids.Take(4).ToList();
            stream.QueuePrefetch(ids.Skip(2));
            var got = new List<string>();
            int loadsAtAnswer = -1;
            stream.Enqueue(ids[0], StoryOfTheLand.Game.Map.Collection.CollectionDetail.Far, _ => { got.Add(ids[0]); loadsAtAnswer = src.Loads.Count; });
            stream.Enqueue(ids[1], StoryOfTheLand.Game.Map.Collection.CollectionDetail.Far, _ => got.Add(ids[1]));
            // Driven as the host's coroutine is: nested enumerators run in place (CollectionStreamTests.Step).
            var stack = new Stack<IEnumerator>(); stack.Push(stream.Run());
            bool Frame()
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    return true;
                }
                return false;
            }
            int frames = 0;
            while (got.Count == 0 && frames++ < 50) { Frame(); if (got.Count == 1) Assert.Fail("one answer before the other"); }
            CollectionAssert.AreEqual(new[] { ids[0], ids[1] }, got, "both callers in the same frame");
            Assert.AreEqual(2, loadsAtAnswer, "the callers are answered after their own two loads, before any prefetch load");
            for (int i = 0; i < 50; i++) Frame();
            Assert.AreEqual(2 + 2 * 2, src.Loads.Count, "then the prefetch: two ids at both details");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Application.backgroundLoadingPriority = ThreadPriority.Normal;
        }
    }

    /// <summary>F6 round 4: about a third of the village houses take the beaten-earth roof, by a hash of the place (no draw from
    /// the village's stream), a warm earth brown well darker than the lime roof.</summary>
    [Test]
    public void AThirdOfTheVillageRoofsAreBeatenEarth()
    {
        int earth = 0, n = 0;
        for (int i = 0; i < 60; i++)
            for (int j = 0; j < 60; j++, n++)
                if (SettlementKits.EarthRoofed(new Vector2(i * 913f + 17f, j * 677f + 5f))) earth++;
        Assert.That(earth / (float)n, Is.InRange(0.28f, 0.42f));
        var c = (Color)SettlementKits.EarthRoof(new Vector2(1234f, 5678f));
        Assert.Greater(c.r, c.b + 0.1f, "warm earth");
        Assert.Less(0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b, 0.65f, "darker than the lime roof");
        Assert.AreEqual(SettlementKits.EarthRoofed(new Vector2(1234f, 5678f)), SettlementKits.EarthRoofed(new Vector2(1234f, 5678f)), "the same house, the same roof");
    }

    /// <summary>The far trees' level of detail (VegetationLayer.LodM): every kind's coarse mesh has at least a quarter fewer
    /// vertices (the crowns' kinds), the same extent within 10% and only unit normals; beyond ShadowM the far tiles cast no
    /// shadow, and the order of the distances holds (full trees near, then coarse, then shadowless, then thinned).</summary>
    [Test]
    public void FarTreesAreCoarseAndCastNoShadowAtTheOverview()
    {
        foreach (TreeKind k in System.Enum.GetValues(typeof(TreeKind)))
        {
            var full = Vegetation.BuildMesh(k); var far = Vegetation.BuildMesh(k, far: true);
            try
            {
                Assert.AreEqual(0, Vegetation.Build(k, far: true).BadNormals, k + ": unit normals");
                if (k != TreeKind.Palm) Assert.Less(far.vertexCount, 0.75f * full.vertexCount, k + ": coarser");
                Assert.AreEqual(full.bounds.size.magnitude, far.bounds.size.magnitude, 0.1f * full.bounds.size.magnitude, k + ": the same tree");
            }
            finally { Object.DestroyImmediate(full); Object.DestroyImmediate(far); }
        }
        Assert.Less(VegetationLayer.LodM, VegetationLayer.ShadowM, "near trees full and shadowed");
        Assert.LessOrEqual(VegetationLayer.ShadowM, VegetationLayer.ThinStartM, "the shadowless trees begin before the thinned ones");
        Assert.AreEqual(VegetationLayer.ShadowM, VegetationLayer.ShadowReach(desktopTier: true));
        Assert.AreEqual(VegetationLayer.LowLodM, VegetationLayer.ShadowReach(desktopTier: false), "Low: only the full-detail trees cast shadows");
        Assert.AreEqual(VegetationLayer.LodM, VegetationLayer.LodReach(desktopTier: true));
        Assert.AreEqual(VegetationLayer.LowLodM, VegetationLayer.LodReach(desktopTier: false), "Low: coarse trees from nearer");
        Assert.Less(VegetationLayer.LowLodM, VegetationLayer.LodM);
    }

    /// <summary>The tiers' anti-aliasing: no MSAA on either URP asset (priced and dropped, QualityTierValues.DesktopMsaa), SMAA
    /// on the desktop tier's camera and none on the web tier's, as CameraRig sets it for the active tier; the desktop tier's
    /// soft shadows filter at Medium (QualityTiers.Apply writes Settings/PC_RPAsset and Mobile_RPAsset).</summary>
    [UnityTest]
    public IEnumerator TheDesktopTierSmoothsItsEdgesWithSmaa()
    {
        var pc = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        var web = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        Assert.AreEqual(1, pc.msaaSampleCount); Assert.AreEqual(1, web.msaaSampleCount);
        Assert.AreEqual(QualityTierValues.DesktopSoftShadowQuality, new SerializedObject(pc).FindProperty("m_SoftShadowQuality").intValue,
            "the desktop tier's soft shadows filter at Medium");
        Assert.AreEqual(UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing, QualityTierValues.PostAntialiasing(GraphicsQuality.HighTier));
        Assert.AreEqual(UnityEngine.Rendering.Universal.AntialiasingMode.None, QualityTierValues.PostAntialiasing(GraphicsQuality.LowTier));
        OpenMap();
        yield return null;
        var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(Camera.main);
        Assert.AreEqual(QualityTierValues.PostAntialiasing(QualitySettings.names[QualitySettings.GetQualityLevel()]), data.antialiasing,
            "CameraRig sets the active tier's anti-aliasing");
        // F6 round 3: a level switched directly (as the captures and RenderProfile do) is followed at the next render.
        int saved = QualitySettings.GetQualityLevel();
        try
        {
            foreach (var tier in new[] { GraphicsQuality.LowTier, GraphicsQuality.HighTier })
            {
                QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, tier), true);
                RenderSample.Render(Camera.main, 64, 36);
                Assert.AreEqual(QualityTierValues.PostAntialiasing(tier), data.antialiasing, tier + ": the camera follows the level");
            }
        }
        finally { QualitySettings.SetQualityLevel(saved, true); }
    }

    /// <summary>Settings > Graphics (F6), each knob applied at once and kept through a map rebuild: the sun's shadows (and the
    /// "shadows" toggle means the chosen kind), the detail (0 even on the desktop tier when off), the haze, the camera's
    /// anti-aliasing, the render scale (clamped to 0.7-1.0, held by Apply, back to the preset's on null), and reduced motion
    /// (the water's clock pinned).</summary>
    [UnityTest]
    public IEnumerator TheGraphicsKnobsApplyAtOnceAndSurviveARebuild()
    {
        int saved = QualitySettings.GetQualityLevel(), high = System.Array.IndexOf(QualitySettings.names, GraphicsQuality.HighTier);
        float scale = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.renderScale;
        var map = OpenMap();
        yield return null;
        var sun = MapLighting.FindSun(); Assert.IsNotNull(sun);
        Assert.AreEqual(LightShadows.Soft, sun!.shadows, "the default");
        GraphicsQuality.Shadows = GraphicsQuality.ShadowChoice.Hard; Assert.AreEqual(LightShadows.Hard, sun.shadows);
        VisualToggles.Set("shadows", false); VisualToggles.Set("shadows", true); Assert.AreEqual(LightShadows.Hard, sun.shadows, "the toggle keeps the choice");
        GraphicsQuality.Shadows = GraphicsQuality.ShadowChoice.Off;
        map.Build(); yield return null;
        Assert.AreEqual(LightShadows.None, MapLighting.FindSun()!.shadows, "kept through a rebuild");

        GraphicsQuality.Haze = false;
        Assert.AreEqual(0f, Shader.GetGlobalVector(MapShaderGlobals.Haze).w, "no land haze");
        map.Build(); yield return null;
        Assert.AreEqual(0f, Shader.GetGlobalVector(MapShaderGlobals.Haze).w, "kept through a rebuild");
        GraphicsQuality.Haze = true; Assert.Greater(Shader.GetGlobalVector(MapShaderGlobals.Haze).w, 0f);

        QualitySettings.SetQualityLevel(high, true);
        try
        {
            var cam = Camera.main;
            GraphicsQuality.Detail = false; RenderSample.Render(cam, 64, 36);
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(MapShaderGlobals.Detail), "off on the desktop tier too");
            Assert.IsFalse(Shader.IsKeywordEnabled(MapShaderGlobals.DetailKeyword), "off: the variant without the detail code");
            GraphicsQuality.Detail = true; RenderSample.Render(cam, 64, 36);
            Assert.Greater(Shader.GetGlobalVector(MapShaderGlobals.Detail).x, 0f);
            Assert.IsTrue(Shader.IsKeywordEnabled(MapShaderGlobals.DetailKeyword), "the desktop tier's variant");

            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            GraphicsQuality.Antialiasing = GraphicsQuality.AntialiasingChoice.Fxaa;
            Assert.AreEqual(UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing, data.antialiasing);
            GraphicsQuality.Antialiasing = GraphicsQuality.AntialiasingChoice.Off;
            Assert.AreEqual(UnityEngine.Rendering.Universal.AntialiasingMode.None, data.antialiasing);
            GraphicsQuality.Antialiasing = GraphicsQuality.AntialiasingChoice.Auto;
            Assert.AreEqual(UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing, data.antialiasing, "the desktop tier's own");
        }
        finally { QualitySettings.SetQualityLevel(saved, true); }

        try
        {
            GraphicsQuality.RenderScale = 0.5f;
            Assert.AreEqual(GraphicsQuality.MinRenderScale, GraphicsQuality.RenderScale, "clamped");
            Assert.AreEqual(0.7f, UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.renderScale, 1e-4f);
            GraphicsQuality.Apply(GraphicsQuality.Choice.Auto);
            Assert.AreEqual(0.7f, UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.renderScale, 1e-4f, "held by Apply");
            GraphicsQuality.RenderScale = null;
            Assert.AreEqual(scale, UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.renderScale, 1e-4f, "the preset's again");
        }
        finally { GraphicsQuality.RenderScale = null; UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.renderScale = scale; }

        GraphicsQuality.ReducedMotion = true;
        Assert.AreEqual(new Vector4(GraphicsQuality.ReducedMotionWaterTime, 1f, 0f, 0f), Shader.GetGlobalVector(MapShaderGlobals.WaterTime));
        GraphicsQuality.ReducedMotion = false;
        Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector(MapShaderGlobals.WaterTime));
    }

    /// <summary>Settings' frame pacing: vsync draws one frame per refresh (the cap is then moot); off, the cap holds, and 0
    /// is no cap at all (the highest frame rate the machine allows). A player starts with vsync, except under the frame-time
    /// instruments. The editor keeps the choice without touching QualitySettings (a project file).</summary>
    [Test]
    public void FramePacingIsVsyncByDefaultAndCapsOnlyWithoutIt()
    {
        Assert.AreEqual((1, -1), GraphicsQuality.FramePacing(vsync: true, cap: 60));
        Assert.AreEqual((0, 144), GraphicsQuality.FramePacing(vsync: false, cap: 144));
        Assert.AreEqual((0, -1), GraphicsQuality.FramePacing(vsync: false, cap: 0));
        Assert.IsTrue(GraphicsQuality.VSyncAtStart(new[] { "player" }));
        Assert.IsFalse(GraphicsQuality.VSyncAtStart(new[] { "player", "-vprofile" }));
        Assert.IsFalse(GraphicsQuality.VSyncAtStart(new[] { "player", "-profile" }));
        CollectionAssert.Contains(GraphicsQuality.FrameCaps, 0);
        int sync = QualitySettings.vSyncCount, target = Application.targetFrameRate;
        GraphicsQuality.SetFramePacing(vsync: false, cap: -5);
        Assert.IsFalse(GraphicsQuality.VSync); Assert.AreEqual(0, GraphicsQuality.FrameCap, "a negative cap is no cap");
        Assert.AreEqual(sync, QualitySettings.vSyncCount, "the editor's QualitySettings are untouched");
        Assert.AreEqual(target, Application.targetFrameRate);
    }
    /// <summary>F6 round 5: a thinned tree tile draws its share in steps of the same seeded prefix, and holds a step while
    /// the share wanders within a band of it, so a camera moving a little never adds or drops single trees; a full share
    /// draws every tree.</summary>
    [Test]
    public void ThinnedTreesStepWithABandInsteadOfPoppingSingleTrees()
    {
        Assert.AreEqual(0, VegetationLayer.ThinLevel(1f, 3));
        Assert.AreEqual(100, VegetationLayer.ThinCount(100, 0));
        int level = VegetationLayer.ThinLevel(0.5f, -1);
        Assert.AreEqual(4, level, "share 0.5 is step 4 (2^-1)");
        Assert.AreEqual(50, VegetationLayer.ThinCount(100, level));
        // A small move either way keeps the step (and so the very same trees).
        foreach (float share in new[] { 0.46f, 0.48f, 0.52f, 0.55f })
            Assert.AreEqual(level, VegetationLayer.ThinLevel(share, level), $"share {share}");
        // A move past the band takes the next step, and coming back holds it until past the band again.
        int next = VegetationLayer.ThinLevel(0.40f, level);
        Assert.AreEqual(5, next);
        Assert.AreEqual(next, VegetationLayer.ThinLevel(0.46f, next), "held on the way back");
        Assert.AreEqual(4, VegetationLayer.ThinLevel(0.52f, next));
        // Draw counts only fall as the steps go up: a step adds or drops a block of the prefix, never one tree back and forth.
        for (int k = 1; k < 30; k++) Assert.LessOrEqual(VegetationLayer.ThinCount(1000, k), VegetationLayer.ThinCount(1000, k - 1));
    }

    /// <summary>F6 round 5: the Collection's fade shader is in Resources (so a player carries it) and compiles; in edit mode
    /// a ModelFade shows and hides at once, through forceRenderingOff, as the layers did before.</summary>
    [Test]
    public void TheCollectionFadeShaderLoadsAndEditModeSwitchesAtOnce()
    {
        var shader = Resources.Load<Shader>("Map/CollectionFade");
        Assert.IsNotNull(shader, "Resources/Map/CollectionFade.shader");
        Assert.IsTrue(shader!.isSupported, "the fade shader compiles");
        Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "no shader error");
        // Round 6: its own shadow caster (the screen-door in shadow-map texels), so a fading model's shadow thins with it.
        var fadeMat = new Material(shader);
        try { Assert.GreaterOrEqual(fadeMat.FindPass("ShadowCaster"), 0, "a ShadowCaster pass"); }
        finally { Object.DestroyImmediate(fadeMat); }
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var r = go.GetComponent<Renderer>();
            var own = r.sharedMaterial;
            var fade = new StoryOfTheLand.Game.Map.Collection.ModelFade();
            fade.Set(r, false);
            Assert.IsTrue(r.forceRenderingOff, "hidden at once");
            fade.Set(r, true); fade.Step();
            Assert.IsFalse(r.forceRenderingOff, "shown at once");
            Assert.AreSame(own, r.sharedMaterial, "its own material, not a fade copy");
            Assert.AreEqual(0, fade.Fading);
        }
        finally { Object.DestroyImmediate(go); }
    }

    /// <summary>F6 round 5: the desktop measuring runs (-vprofile, -popcheck, -bootlog) are automated runs (DiagLog.Automated:
    /// the player keeps running out of focus and takes no gameplay keys or camera input); a plain start and the other
    /// diagnostics are not.</summary>
    [Test]
    public void OnlyTheMeasuringRunsAreAutomated()
    {
        foreach (var flag in new[] { "-vprofile", "-popcheck", "-bootlog" })
            Assert.IsTrue(StoryOfTheLand.Game.Boot.DiagLog.AutomatedRequested(new[] { "game", flag, "x" }), flag);
        Assert.IsFalse(StoryOfTheLand.Game.Boot.DiagLog.AutomatedRequested(new[] { "game" }), "normal play");
        Assert.IsFalse(StoryOfTheLand.Game.Boot.DiagLog.AutomatedRequested(new[] { "game", "-navlog", "-shot" }), "other diagnostics");
        Assert.IsFalse(StoryOfTheLand.Game.Boot.DiagLog.Automated, "the editor is not a measuring run");
    }
}
