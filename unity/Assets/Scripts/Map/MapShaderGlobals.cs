#nullable enable
using UnityEngine;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>The global shader values StoryOfTheLandMap.hlsl reads, set when the map is built (MapBuilder.Build): the map's
    /// size, sea level and exaggeration, its origin and hex size, the biome colour texture and the haze. Globals, not
    /// material values, so the shared editor-made materials are never changed or instanced at run time.</summary>
    public static class MapShaderGlobals
    {
        public static readonly int Map = Shader.PropertyToID("_ScMap"), MapOrigin = Shader.PropertyToID("_ScMapOrigin"),
            Biome = Shader.PropertyToID("_ScBiomeTex"), Haze = Shader.PropertyToID("_ScHaze"), HazeWater = Shader.PropertyToID("_ScHazeWater"),
            HazeRange = Shader.PropertyToID("_ScHazeRange"), WaterOn = Shader.PropertyToID("_ScWaterOn"), Grid = Shader.PropertyToID("_ScGrid"),
            WaterTime = Shader.PropertyToID("_ScWaterTime"), Cloud = Shader.PropertyToID("_ScCloud"), Wind = Shader.PropertyToID("_ScWind"), LandOn = Shader.PropertyToID("_ScLandOn"),
            SeaTexture = Shader.PropertyToID("_ScSeaTexture"), SeaCalm = Shader.PropertyToID("_ScSeaCalm"),
            SeaFoam = Shader.PropertyToID("_ScSeaFoam"), DuneRect = Shader.PropertyToID("_ScDuneRect"),
            DuneRamp = Shader.PropertyToID("_ScDuneRamp"), DuneCount = Shader.PropertyToID("_ScDuneCount"),
            Detail = Shader.PropertyToID("_ScDetail"), DetailTex = Shader.PropertyToID("_ScDetailTex");

        /// <summary>F6 (StoryOfTheLandDetail.hlsl): the ground's rock and soil detail (Resources, CC0 Poly Haven scans packed by
        /// luminance; ASSETS.yaml) and its strength, and the hill country's terraces. The desktop tier (PC, Settings' High) only:
        /// 0 on the Low tier, and the shader compiles both out on WebGL.</summary>
        public const string DetailResource = "Map/TerrainDetail";
        public const float DetailStrength = 0.55f, TerraceStrength = 1f;

        /// <summary>Pins the water's clock (waves, surf) to the given seconds, or back to Unity's time with null: for tests and
        /// frame captures that must render chosen moments (promo-sea review I2, M5).</summary>
        public static void PinWaterTime(float? seconds)
        {
            PinnedWaterTime = seconds;
            Shader.SetGlobalVector(WaterTime, seconds.HasValue ? new Vector4(seconds.Value, 1f, 0f, 0f) : Vector4.zero);
        }

        /// <summary>The water's pinned clock (PinWaterTime), or null while it runs on Unity's time.</summary>
        public static float? PinnedWaterTime { get; private set; }

        /// <summary>F7 (the owner: "keep the game ... not freezed or static"): the drifting cloud shadows
        /// (StoryOfTheLandMap.hlsl ScCloudShade, desktop tier): how much of the sun a cloud takes, the blobs' size and the
        /// wind (m/s at the map's scale: a blob crosses the gameplay view in about a minute). In play only, so an edit-mode
        /// render (captures, render tests) draws the map as before; the "clouds" toggle prices them.</summary>
        public const float CloudStrength = 0.16f, CloudSizeM = 14000f, CloudWindMps = 500f;
        /// <summary>F7 trim: no clouds for a camera farther than this (the overview: the blobs are too small to read there,
        /// and the noise cost about 0.9 ms at diorama02 on the desktop tier).</summary>
        public const float CloudFarM = 90000f;
        static bool cloudsOn = true;

        /// <summary>F7: the trees' wind (Kit.shader ScSway): the wind's clock is the water's (so a pinned clock holds the
        /// trees still), and the trees sway only in play and without reduced motion. Set once per camera by VegetationLayer;
        /// no allocation.</summary>
        public static void SetWind(bool sway) =>
            Shader.SetGlobalVector(Wind, new Vector4(PinnedWaterTime ?? Time.timeSinceLevelLoad, sway ? 1f : 0f, 0f, 0f));

        /// <summary>F7: the moving sky for one camera (VegetationLayer, once per camera): the trees sway and the clouds drift
        /// only in play (<paramref name="playing"/>), on the desktop tier (<paramref name="desktopTier"/>: Low keeps its frame
        /// budget), without reduced motion, and the clouds only nearer than <see cref="CloudFarM"/> with the "clouds" toggle on.
        /// No allocation.</summary>
        public static void SetSky(bool playing, bool desktopTier, float viewDistanceM)
        {
            bool live = playing && desktopTier;
            SetWind(live && !GraphicsQuality.ReducedMotion);
            bool clouds = live && cloudsOn && viewDistanceM <= CloudFarM;
            Shader.SetGlobalVector(Cloud, new Vector4(clouds ? CloudStrength : 0f, CloudSizeM, CloudWindMps, 0f));
        }

        /// <summary>A warm, dusty haze over land (sRGB): warmer than the ground it covers (R - B 0.26), so the far land reads
        /// as dust, not as a cool milky veil (Task 6 review I1).</summary>
        public static readonly Color HazeColour = new Color(0.84f, 0.74f, 0.58f);
        public const float HazeMax = 0.45f;
        /// <summary>A cooler, weaker haze over water (review I2), so the sea stays the promo's saturated blue towards the
        /// horizon: 0.4 of the land's strength. Issue #709: a deeper blue (was 0.62, 0.74, 0.86), so diorama02's sea reads
        /// as blue as promo 02's: per-material warmth R - B -0.235, was -0.197 (promo -0.260, band [-0.312, -0.208]). The
        /// water haze starts at 120 km (ScWaterHazeLeadM), so the gameplay pose's water (48-91 km) does not change.</summary>
        public static readonly Color WaterHazeColour = new Color(0.42f, 0.62f, 0.88f);
        public const float WaterHazeMax = HazeMax * 0.4f;
        /// <summary>The aerial haze is 1 - exp(-max(d - start, 0) / scale), times HazeMax: none within 200 km (the gameplay
        /// pose, 48-91 km, has none: its depth cue is the tilt-shift), about 2% at the country-wide pose's nearest land
        /// (about 205 km) and 17% at its horizon (about 400 km): a depth cue, not a veil (review I1).</summary>
        public const float HazeStartM = 200000f, HazeDistanceM = 250000f;
        /// <summary>Real elevations between which the haze thickens, low in the Jordan rift.</summary>
        public const float RiftHazeTopM = -100f, RiftHazeFullM = -400f;

        /// <summary>Hex line opacity on land and water, and width in pixels (box-filtered, the same at every zoom). Light,
        /// faint lines as in the promo: water 0.10 (Task 8 review I2: 0.35 gave sea lines 2.6x their background's luma
        /// against the promo's 1.4x). Land 0.35, not 0.25 (merge-point carry, Task 8 N2): after the grade the light land
        /// lines vanished on the dry-grass plain at both promo poses, where promo 02 and 03 show a faint land lattice.</summary>
        public const float GridLandOpacity = 0.35f, GridWaterOpacity = 0.10f, GridWidthPx = 1.5f;

        static Vector4 haze, hazeWater, detailOn;
        static bool detailToggle = true, hazeToggle = true;

        /// <summary>The haze as the "haze" toggle and Settings (GraphicsQuality.Haze) both want it: on only when both are.</summary>
        internal static void ApplyHaze()
        {
            bool on = hazeToggle && GraphicsQuality.Haze;
            Shader.SetGlobalVector(Haze, on ? haze : new Vector4(haze.x, haze.y, haze.z, 0f));
            Shader.SetGlobalVector(HazeWater, on ? hazeWater : new Vector4(hazeWater.x, hazeWater.y, hazeWater.z, 0f));
        }
        static int highLevel = -1;

        /// <summary>The detail's strengths for <paramref name="level"/>: the desktop tier's (PC) when the toggle is on, else 0, so
        /// the Low tier's GPU skips it (StoryOfTheLandDetail.hlsl's uniform branch).</summary>
        public static Vector4 DetailAt(int level, bool toggle, Vector4 on, int high) => toggle && level == high ? on : Vector4.zero;

        /// <summary>The terrain graph's global keyword (tools/terrain_graph.py): on, the desktop tier's variant with the detail
        /// code; off, a variant without any of it (F6 round 3: the code alone cost about 1 ms at the close views).</summary>
        public static readonly UnityEngine.Rendering.GlobalKeyword DetailKeyword = UnityEngine.Rendering.GlobalKeyword.Create("_SC_DETAIL");

        static void SetDetail(UnityEngine.Rendering.ScriptableRenderContext _, System.Collections.Generic.List<Camera> __)
        {
            var v = DetailAt(QualitySettings.GetQualityLevel(), detailToggle && GraphicsQuality.Detail, detailOn, highLevel);
            Shader.SetGlobalVector(Detail, v);
            Shader.SetKeyword(DetailKeyword, v != Vector4.zero);
        }

        public static void Apply(Terrain terrain, HeightmapMeta meta, float hexSizeM, Texture2D biome, MapRuntimeAssets assets)
        {
            var size = terrain.terrainData.size; var o = terrain.transform.position;
            float Y(float elevM) => o.y + (elevM - meta.elev_min_m) * MapBuilder.VerticalExaggeration;
            Shader.SetGlobalVector(Map, new Vector4(size.x, size.z, Y(0f), MapBuilder.VerticalExaggeration));
            Shader.SetGlobalVector(MapOrigin, new Vector4(o.x, o.y, o.z, hexSizeM));
            Shader.SetGlobalTexture(Biome, biome);
            var c = HazeColour.linear;
            haze = new Vector4(c.r, c.g, c.b, HazeMax);
            var w = WaterHazeColour.linear;
            hazeWater = new Vector4(w.r, w.g, w.b, WaterHazeMax);
            Shader.SetGlobalVector(HazeRange, new Vector4(Y(RiftHazeTopM), Y(RiftHazeFullM), HazeDistanceM, HazeStartM));
            Shader.SetGlobalVector(Grid, new Vector4(GridLandOpacity, GridWaterOpacity, GridWidthPx, 1f));
            assets.OnTeardown(VisualToggles.Register("grid", on => Shader.SetGlobalVector(Grid, new Vector4(GridLandOpacity, GridWaterOpacity, GridWidthPx, on ? 1f : 0f))));
            // Land use (issue #33, StoryOfTheLandLand.hlsl): fields, farm tracks, terraces and scrub; off = the bare biome ground.
            Shader.SetGlobalFloat(LandOn, 1f);
            assets.OnTeardown(VisualToggles.Register("land", on => Shader.SetGlobalFloat(LandOn, on ? 1f : 0f)));
            Shader.SetGlobalFloat(WaterOn, 1f);
            assets.OnTeardown(VisualToggles.Register("water", on => Shader.SetGlobalFloat(WaterOn, on ? 1f : 0f)));
            // The far sea's texture (sea round 2, #388: StoryOfTheLandMap.hlsl's mottle, ripple and whitecaps); its own toggle, so
            // the profile prices it and the seam test can pin it off.
            Shader.SetGlobalFloat(SeaTexture, 1f);
            assets.OnTeardown(VisualToggles.Register("seatexture", on => Shader.SetGlobalFloat(SeaTexture, on ? 1f : 0f)));
            // Sea boats (#388, StoryOfTheLandMap.hlsl): the far sea's calm and broad swells at the gameplay zoom, and the coastal
            // shelf at the overview; its own toggle, so the profile prices it and the tests can compare with it off.
            Shader.SetGlobalFloat(SeaCalm, 1f);
            assets.OnTeardown(VisualToggles.Register("seacalm", on => Shader.SetGlobalFloat(SeaCalm, on ? 1f : 0f)));
            // Sea round 3 (#388, StoryOfTheLandMap.hlsl): the surf's lace net; its own toggle, so the profile prices it.
            Shader.SetGlobalFloat(SeaFoam, 1f);
            assets.OnTeardown(VisualToggles.Register("foam", on => Shader.SetGlobalFloat(SeaFoam, on ? 1f : 0f)));
            // F7: the drifting cloud shadows, in play only (CloudStrength); its own toggle, so the profile prices them.
            cloudsOn = true;
            SetSky(false, false, 0f);
            assets.OnTeardown(VisualToggles.Register("clouds", on => cloudsOn = on));
            // The dune coasts (#690, DuneCoasts.json): the water pass widens the surf there, in map metres.
            var duneRect = new Vector4[DuneCoasts.MaxBands]; var duneRamp = new Vector4[DuneCoasts.MaxBands];
            int dunes = DuneCoasts.ToMapMetres(new GeoFrame(meta.lon0, meta.lon1, meta.lat0, meta.lat1, size.x, size.z), duneRect, duneRamp);
            Shader.SetGlobalVectorArray(DuneRect, duneRect);
            Shader.SetGlobalVectorArray(DuneRamp, duneRamp);
            Shader.SetGlobalFloat(DuneCount, dunes);
            var detail = Resources.Load<Texture2D>(DetailResource);
            Shader.SetGlobalTexture(DetailTex, detail != null ? detail : Texture2D.grayTexture);
            detailOn = detail != null ? new Vector4(DetailStrength, TerraceStrength, 0f, 0f) : Vector4.zero;
            detailToggle = true;
            highLevel = System.Array.IndexOf(QualitySettings.names, GraphicsQuality.HighTier);
            SetDetail(default, null!);
            assets.OnTeardown(VisualToggles.Register("detail", on => { detailToggle = on; SetDetail(default, null!); }));
            // Every frame, before the cameras: the quality level changes under the map (Settings, the editor captures).
            UnityEngine.Rendering.RenderPipelineManager.beginContextRendering += SetDetail;
            assets.OnTeardown(() => UnityEngine.Rendering.RenderPipelineManager.beginContextRendering -= SetDetail);
            hazeToggle = true;
            ApplyHaze();
            assets.OnTeardown(VisualToggles.Register("haze", on => { hazeToggle = on; ApplyHaze(); }));
        }
    }

    /// <summary>The baked map textures (tools/terrain_bake.py), shared by the editor scripts and the tests.</summary>
    public static class MapTexturePaths
    {
        public const string Bake = "Assets/Textures/Map/terrain_bake.png";
        public const string Coast = "Assets/Textures/Map/coast.png";
        /// <summary>The ambient occlusion on its own, uncompressed (R8): BC1 stepped it on broad slopes (Task 6 fix round 1).</summary>
        public const string Ao = "Assets/Textures/Map/ao.png";
        /// <summary>The land (tools/land_bake.py, issue #33): rgb its linear colour, a its weight over the biome ground.</summary>
        public const string Land = "Assets/Textures/Map/landuse.png";
        /// <summary>The terrain water's mesh as heightmap samples (MapBuilder.WaterMeshBytes; Editor TerrainWaterBake), so a
        /// player does not derive it at boot (issue #195).</summary>
        public const string WaterMesh = "Assets/Textures/Map/terrain_water.bytes";
    }
}
