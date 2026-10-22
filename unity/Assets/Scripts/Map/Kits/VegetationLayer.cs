#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StoryOfTheLand.Game.Map.Kits
{
    /// <summary>Draws the trees with GPU instancing (Graphics.RenderMeshInstanced, WebGL 2 instanced arrays): one batch list
    /// per tree kind and 30 km tile, each with its own bounds so off-screen tiles are culled. A tile's instances are shuffled
    /// once (seeded), so drawing a prefix thins it evenly: far tiles (the country-wide zoom) draw a share that falls with
    /// distance, where a tree is a pixel or less. Submitted for every camera from RenderPipelineManager.beginCameraRendering,
    /// so editor renders (MapCapture, the render-sample tests) see the trees as the player does.</summary>
    [ExecuteAlways]
    public sealed class VegetationLayer : MonoBehaviour
    {
        public const float TileM = 30000f;
        /// <summary>Tiles nearer than this draw every tree; beyond it the share falls as (ThinStartM / distance)^2. F6 round 5
        /// (the owner: "not show and hide and show and hide"): the share is drawn in steps with a band (<see cref="ThinLevel"/>),
        /// so a camera moving a little never adds or drops single trees.</summary>
        public const float ThinStartM = 110000f;
        /// <summary>F6: tiles farther than LodM draw the coarse meshes (Vegetation.BuildMesh far: a crown is then 15-20 px or
        /// less at 1080p), and tiles farther than ShadowM cast no shadow: at the gameplay zoom that is the top of the view,
        /// which the tilt-shift blurs, and the shadow pass redraws every tree it is given in each cascade.</summary>
        public const float LodM = 45000f, ShadowM = 90000f;
        /// <summary>F6 round 3: on the Low tier (any level but the desktop tier's), the frame-rate choice, the coarse meshes
        /// start at LowLodM, and only the full-detail trees within it cast shadows.</summary>
        public const float LowLodM = 15000f;
        public static float LodReach(bool desktopTier) => desktopTier ? LodM : LowLodM;
        public static float ShadowReach(bool desktopTier) => desktopTier ? ShadowM : LowLodM;
        int highLevel = -1;

        /// <summary>A tile's trees: <c>all</c> as planted (shuffled), <c>m</c> the ones drawn (all, less those under a unit
        /// figure: <see cref="Hide"/>), in the same order, so drawing a prefix still thins evenly; <c>crown</c> is the largest
        /// crown radius (m) among them.</summary>
        sealed class Tile
        {
            public TreeKind kind; public Bounds bounds; public float crown; public Matrix4x4[] all = System.Array.Empty<Matrix4x4>(), m = System.Array.Empty<Matrix4x4>();
            /// <summary>F6 round 4: which side of the level-of-detail and shadow distances the tile was drawn on last, so a
            /// camera at either distance never flips it back and forth (<see cref="Within"/>).</summary>
            public bool full, shadowed;   // false at first: the first frame takes the plain rule (Within)
            public int thin = -1;   // the thinning step drawn last (ThinLevel); -1 before the first frame
        }

        /// <summary>F6 round 4 (the owner: "not show and hide and show and hide"): a switch distance with a band of
        /// <see cref="SwitchBand"/> beyond it: a tile comes in within <paramref name="reach"/>, as before, and once in stays so
        /// until it is beyond reach x (1 + band). One-sided, so a camera that arrives from afar (a pose, a minimap jump) draws
        /// exactly what it did before the band; only a camera pulling back keeps the near look a little longer.</summary>
        public static bool Within(float distanceM, float reach, bool wasWithin) => distanceM < reach * (wasWithin ? 1f + SwitchBand : 1f);
        public const float SwitchBand = 0.1f;

        readonly List<Tile> tiles = new List<Tile>();
        readonly SortedDictionary<string, List<(Vector2 centre, float radius)>> holes = new SortedDictionary<string, List<(Vector2, float)>>(System.StringComparer.Ordinal);
        readonly Dictionary<TreeKind, Mesh> meshes = new Dictionary<TreeKind, Mesh>(), farMeshes = new Dictionary<TreeKind, Mesh>();
        Material? material;
        public bool Draw { get; set; } = true;
        public int Count { get; private set; }
        /// <summary>Instances submitted for the last camera, and the instanced draw calls that carried them (test and profiling
        /// aids).</summary>
        public int LastDrawn { get; private set; }
        public int LastDraws { get; private set; }
        /// <summary>Trees left out now because they stand under a unit figure (<see cref="Hide"/>).</summary>
        public int Hidden { get; private set; }

        /// <summary>A tree's crown radius (m) as placed: its kind's real crown times the instance's scale, with the planting
        /// margin (Vegetation.Plant keeps a crown this wide off the lots and routes).</summary>
        public static float CrownRadius(TreeKind kind, Matrix4x4 m) => Vegetation.CrownRadiusM(kind) * ((Vector3)m.GetColumn(0)).magnitude * 1.15f;

        public void Init(List<(TreeKind kind, Matrix4x4 m)> trees, Material treeMaterial, MapRuntimeAssets assets)
        {
            material = treeMaterial;
            highLevel = System.Array.IndexOf(QualitySettings.names, GraphicsQuality.HighTier);
            foreach (TreeKind k in System.Enum.GetValues(typeof(TreeKind)))
            {
                meshes[k] = assets.Own(Vegetation.BuildMesh(k));
                farMeshes[k] = assets.Own(Vegetation.BuildMesh(k, far: true));
            }
            var byTile = new Dictionary<(TreeKind, int, int), List<Matrix4x4>>();
            foreach (var (kind, m) in trees)
            {
                var p = m.GetColumn(3);
                var key = (kind, Mathf.FloorToInt(p.x / TileM), Mathf.FloorToInt(p.z / TileM));
                if (!byTile.TryGetValue(key, out var list)) byTile[key] = list = new List<Matrix4x4>();
                list.Add(m);
            }
            var keys = new List<(TreeKind, int, int)>(byTile.Keys);
            keys.Sort();
            tiles.Clear();
            foreach (var key in keys)
            {
                var list = byTile[key];
                var rnd = new KitRandom($"tile:{key.Item1}:{key.Item2}:{key.Item3}", 19);
                for (int i = list.Count - 1; i > 0; i--) { int j = rnd.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
                var b = new Bounds((Vector3)list[0].GetColumn(3), Vector3.zero);
                foreach (var m in list) b.Encapsulate((Vector3)m.GetColumn(3));
                b.Expand(new Vector3(400f, 800f, 400f) * (KitScale.Trees / KitScale.CountReference));   // the crowns round the trunk points
                float crown = 0f;
                foreach (var m in list) crown = Mathf.Max(crown, CrownRadius(key.Item1, m));
                var arr = list.ToArray();
                tiles.Add(new Tile { kind = key.Item1, bounds = b, crown = crown, all = arr, m = arr });
            }
            Count = trees.Count;
            holes.Clear(); Hidden = 0;
        }

        /// <summary>Leaves out <paramref name="map"/>'s kit trees under <paramref name="holes"/> for <paramref name="owner"/> (an
        /// empty list gives them back), at draw time (Hide), so the kits' layout never changes. A map without kits has no trees
        /// to hide.</summary>
        internal static void HideUnder(MapScene map, string owner, IReadOnlyList<(Vector2 centre, float radius)> holes)
        {
            if (map == null) return;
            var veg = map.GetComponentInChildren<VegetationLayer>(true);
            if (veg != null) veg.Hide(owner, holes);
        }

        /// <summary>Issue #249: leaves out, at draw time, every tree whose crown reaches into one of <paramref name="where"/>
        /// (centre x, z; radius m): the ground under the unit figures drawn now. Each caller (<paramref name="owner"/>: the
        /// units in play, the showcase) sets its own holes, and an empty list gives its trees back. The planting itself, and
        /// so the kits' layout, never changes; only the tiles a hole reaches are rebuilt, when a caller's holes change (a
        /// roster change), never per frame.</summary>
        public void Hide(string owner, IReadOnlyList<(Vector2 centre, float radius)> where)
        {
            if (where.Count == 0) holes.Remove(owner); else holes[owner] = new List<(Vector2, float)>(where);
            var all = new List<(Vector2 centre, float radius)>();
            foreach (var h in holes.Values) all.AddRange(h);
            int hidden = 0;
            var keep = new List<Matrix4x4>();
            foreach (var t in tiles)
            {
                if (!Reaches(t, all)) { t.m = t.all; continue; }
                keep.Clear();
                foreach (var m in t.all)
                {
                    if (Covered(t.kind, m, all)) hidden++; else keep.Add(m);
                }
                t.m = keep.Count == t.all.Length ? t.all : keep.ToArray();
            }
            Hidden = hidden;
        }

        static bool Reaches(Tile t, List<(Vector2 centre, float radius)> where)
        {
            foreach (var (c, r) in where)
            {
                float reach = r + t.crown;
                float dx = Mathf.Max(t.bounds.min.x - c.x, 0f, c.x - t.bounds.max.x), dz = Mathf.Max(t.bounds.min.z - c.y, 0f, c.y - t.bounds.max.z);
                if (dx * dx + dz * dz < reach * reach) return true;
            }
            return false;
        }

        static bool Covered(TreeKind kind, Matrix4x4 m, List<(Vector2 centre, float radius)> where)
        {
            var p = m.GetColumn(3); float crown = CrownRadius(kind, m);
            foreach (var (c, r) in where)
            {
                float dx = p.x - c.x, dz = p.z - c.y, reach = r + crown;
                if (dx * dx + dz * dz < reach * reach) return true;
            }
            return false;
        }

        /// <summary>Every tree as planted, hidden or not (kind, world matrix): a test aid.</summary>
        public IEnumerable<(TreeKind kind, Matrix4x4 m)> Planted()
        {
            foreach (var t in tiles) foreach (var m in t.all) yield return (t.kind, m);
        }

        /// <summary>The trees a camera near enough to draw every tree would submit now (kind, world matrix): a test aid.</summary>
        public IEnumerable<(TreeKind kind, Matrix4x4 m)> Drawable()
        {
            foreach (var t in tiles) foreach (var m in t.m) yield return (t.kind, m);
        }

        /// <summary>The thinning steps: step k draws the share 2^(-k x ThinStep) of a tile's trees (about 16% fewer a step),
        /// the same seeded prefix every time, so a step only ever adds or drops the trees past it.</summary>
        public const float ThinStep = 0.25f;
        /// <summary>How far past half a step the share must move before a tile takes another step.</summary>
        public const float ThinBand = 0.25f;

        /// <summary>F6 round 5: the thinning step for <paramref name="share"/> (0 = every tree), held at <paramref name="was"/>
        /// (-1: none yet) while the share stays within half a step plus <see cref="ThinBand"/> of it.</summary>
        public static int ThinLevel(float share, int was)
        {
            if (share >= 1f) return 0;
            float k = -Mathf.Log(Mathf.Max(share, 1e-6f), 2f) / ThinStep;
            return was >= 0 && Mathf.Abs(k - was) < 0.5f + ThinBand ? was : Mathf.RoundToInt(k);
        }

        /// <summary>The trees step <paramref name="level"/> draws of a tile of <paramref name="count"/>.</summary>
        public static int ThinCount(int count, int level) => level <= 0 ? count : Mathf.Min(count, Mathf.CeilToInt(count * Mathf.Pow(2f, -level * ThinStep)));

        void OnEnable() => RenderPipelineManager.beginCameraRendering += Submit;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Submit;

        /// <summary>F6: reused every camera and frame (the array-returning overload allocated one per call: per-frame GC).</summary>
        readonly Plane[] planes = new Plane[6];

        void Submit(ScriptableRenderContext context, Camera cam)
        {
            if (!Draw || material == null || cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            var eye = cam.transform.position;
            int drawn = 0, draws = 0;
            bool desktopTier = QualitySettings.GetQualityLevel() == highLevel;
            // F7: the trees' sway and the drifting clouds, for this camera (desktop tier, in play, not at the overview).
            MapShaderGlobals.SetSky(Application.isPlaying, desktopTier, TownModelLayer.ViewDistance(cam));
            float shadowReach = ShadowReach(desktopTier), lodReach = LodReach(desktopTier);
            foreach (var t in tiles)
            {
                // The shadow pass sees casters outside the view too, so cull on a widened box (the sun is 30 degrees up: a
                // crown's shadow reaches about twice its height).
                var wide = t.bounds; wide.Expand(new Vector3(3000f, 0f, 3000f));
                if (!GeometryUtility.TestPlanesAABB(planes, wide)) continue;
                float d = Mathf.Sqrt(t.bounds.SqrDistance(eye));
                float share = d <= ThinStartM ? 1f : Mathf.Clamp01((ThinStartM / d) * (ThinStartM / d));
                t.thin = ThinLevel(share, t.thin);
                int n = ThinCount(t.m.Length, t.thin);
                if (n <= 0) continue;
                t.full = Within(d, lodReach, t.full); t.shadowed = Within(d, shadowReach, t.shadowed);
                var rp = new RenderParams(material)
                {
                    worldBounds = t.bounds, camera = cam, shadowCastingMode = t.shadowed ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true, layer = gameObject.layer,
                };
                Graphics.RenderMeshInstanced(rp, t.full ? meshes[t.kind] : farMeshes[t.kind], 0, t.m, n);
                drawn += n; draws++;
            }
            LastDrawn = drawn; LastDraws = draws;
        }
    }
}
