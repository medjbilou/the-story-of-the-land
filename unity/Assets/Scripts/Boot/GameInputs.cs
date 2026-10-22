#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Sim;
using UnityEngine;

namespace StoryOfTheLand.Game.Boot
{
    /// <summary>The map's raw inputs, already read (no System.IO needed afterwards).</summary>
    public sealed class MapInputs
    {
        public string HeightmapJson = "", HexesJson = "", SettlementsYaml = "";
        /// <summary>content/map/overrides.yaml, the map editor's overrides (#1009); "" when the file is missing (no overrides).</summary>
        public string OverridesYaml = "";
        /// <summary>content/map/subdistrict_hexes_1948.yaml: the 1948 sub-districts the map outlines (display only).</summary>
        public string RegionsYaml = "";
        /// <summary>The baked runtime forms of the region files the map reads at boot (tools/regions_bake.py; issue #321;
        /// "" when absent: the YAML is read, with a warning).</summary>
        public string RegionsTsv = "", ArmisticeTsv = "", ControlTsv = "";
        /// <summary>content/map/route_paths_1948.yaml: the traced railways and roads the map draws (issue #35; map-only).</summary>
        public string RoutesYaml = "";
        /// <summary>content/map/route_paths_1948.tsv, the baked runtime form of RoutesYaml the map reads at boot (issue #321;
        /// "" when absent: the YAML is read, with a warning).</summary>
        public string RoutesTsv = "";
        /// <summary>content/map/settlement_population_1945.yaml (the settlement kits' footprints; "" when absent). The sim
        /// never reads it.</summary>
        public string PopulationYaml = "";
        /// <summary>content/map/localities_1948.tsv, the baked runtime form of localities_1948.yaml (the 1948 localities and
        /// Bedouin camp areas the kits and labels draw; "" when absent). The sim never reads it.</summary>
        public string LocalitiesTsv = "";
        /// <summary>content/map/armistice_hexes_1949.yaml: the 1949 armistice lines drawn over it from their dates (display
        /// only; "" when absent: no armistice layer, like the other cosmetic map inputs).</summary>
        public string ArmisticeYaml = "";
        /// <summary>content/map/control_hexes_1948_49.yaml: who held which areas, 1948-49, by period (display only; "" when
        /// absent: the older drawing, sub-districts with the armistice zones over them).</summary>
        public string ControlYaml = "";
        /// <summary>content/map/occupation_layers.yaml: the occupation's layers, June 1967 on (display only; "" when absent: none).</summary>
        public string OccupationYaml = "";
        /// <summary>content/map/gaza_damage_2023.yaml: the destruction of Gaza as UNOSAT's images record it (display only; "" when absent: none).</summary>
        public string DamageYaml = "";
        /// <summary>heightmap.r16z as read: packed (HeightmapCodec); HeightmapLoader decodes it when the map is built.</summary>
        public byte[] HeightmapR16z = new byte[0];
    }

    /// <summary>Everything the game loads at start: the map inputs and a reader for the family game's content (content/
    /// palestine/: its chapters, figures and credits, loaded when asked for). Desktop and the editor read files (FromDisk);
    /// WebGL cannot read StreamingAssets with System.IO, so it fetches them with UnityWebRequest from the content bundle
    /// (FetchAsync, tech notes section 2; one file for every content text, #1054).</summary>
    public sealed class GameInputs
    {
        public MapInputs Map = null!;
        /// <summary>A content file's text by its path under content/ ('/' separated), or null when there is none: the disk
        /// (FromDisk) or the fetched bundle (FromTexts).</summary>
        public Func<string, string?> Read = _ => null;

        /// <summary>F5: one chapter of the family game (content/palestine/), loaded through <see cref="Read"/> and kept, so
        /// the picker and the game share one load.</summary>
        public Content Chapter(string chapter)
        {
            if (!chapters.TryGetValue(chapter, out var c))
                chapters[chapter] = c = ContentLoader.LoadChapter(rel => Read(ContentLoader.PalestineDir + "/" + rel), chapter);
            return c;
        }
        readonly Dictionary<string, Content> chapters = new Dictionary<string, Content>();

        public static MapInputs MapFromDisk(string contentDir, string mapDir) => new MapInputs
        {
            HeightmapJson = File.ReadAllText(Path.Combine(mapDir, "heightmap.json")),
            HeightmapR16z = File.ReadAllBytes(Path.Combine(mapDir, HeightmapCodec.FileName)),
            HexesJson = File.ReadAllText(Path.Combine(contentDir, "map", "hexes.json")),
            SettlementsYaml = File.ReadAllText(Path.Combine(contentDir, "map", SettlementPlacer.DefaultFileName)),
            OverridesYaml = ReadIfExists(Path.Combine(contentDir, MapOverrides.FileName)),
            RegionsYaml = File.ReadAllText(Path.Combine(contentDir, "map", Subdistricts1948.FileName)),
            RoutesYaml = ReadIfExists(Path.Combine(contentDir, "map", StoryOfTheLand.Game.Map.Routes.RoutePaths.DefaultFileName)),
            RoutesTsv = ReadIfExists(Path.Combine(contentDir, "map", StoryOfTheLand.Game.Map.Routes.RoutePaths.RuntimeFileName)),
            PopulationYaml = File.Exists(Path.Combine(contentDir, "map", StoryOfTheLand.Game.Map.Kits.KitPopulation.FileName))
                ? File.ReadAllText(Path.Combine(contentDir, "map", StoryOfTheLand.Game.Map.Kits.KitPopulation.FileName)) : "",
            LocalitiesTsv = ReadIfExists(Path.Combine(contentDir, "map", StoryOfTheLand.Game.Map.Kits.Localities.RuntimeFileName)),
            ArmisticeYaml = ReadIfExists(Path.Combine(contentDir, "map", Armistice1949.FileName)),
            RegionsTsv = ReadIfExists(Path.Combine(contentDir, "map", Subdistricts1948.RuntimeFileName)),
            ArmisticeTsv = ReadIfExists(Path.Combine(contentDir, "map", Armistice1949.RuntimeFileName)),
            ControlTsv = ReadIfExists(Path.Combine(contentDir, "map", Control1948.RuntimeFileName)),
            ControlYaml = ReadIfExists(Path.Combine(contentDir, "map", Control1948.FileName)),
            OccupationYaml = ReadIfExists(Path.Combine(contentDir, "map", OccupationLayers.FileName)),
            DamageYaml = ReadIfExists(Path.Combine(contentDir, "map", GazaDamage.FileName)),
        };

        /// <summary>A map-only, cosmetic input: "" when the file is missing (the map then skips that layer with a warning).</summary>
        static string ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

        /// <summary>The inputs from disk.</summary>
        public static GameInputs FromDisk(string contentDir, string mapDir) => new GameInputs
        {
            Map = MapFromDisk(contentDir, mapDir),
            Read = rel => { var p = Path.Combine(contentDir, rel); return File.Exists(p) ? File.ReadAllText(p) : null; },
        };

        /// <summary>Builds the inputs from fetched texts (keys are manifest paths) and the packed heightmap's bytes.</summary>
        public static GameInputs FromTexts(IReadOnlyDictionary<string, string> texts, string heightmapJson, byte[] r16z)
        {
            return new GameInputs
            {
                Read = rel => texts.TryGetValue(rel, out var t) ? t : null,
                Map = new MapInputs
                {
                    HeightmapJson = heightmapJson, HeightmapR16z = r16z,
                    HexesJson = texts["map/hexes.json"], SettlementsYaml = texts["map/" + SettlementPlacer.DefaultFileName],
                    OverridesYaml = texts.TryGetValue(MapOverrides.FileName, out var overrides) ? overrides : "",
                    RegionsYaml = texts["map/" + Subdistricts1948.FileName],
                    RoutesYaml = texts.TryGetValue("map/" + StoryOfTheLand.Game.Map.Routes.RoutePaths.DefaultFileName, out var routes) ? routes : "",
                    RoutesTsv = texts.TryGetValue("map/" + StoryOfTheLand.Game.Map.Routes.RoutePaths.RuntimeFileName, out var routesTsv) ? routesTsv : "",
                    PopulationYaml = texts.TryGetValue("map/" + StoryOfTheLand.Game.Map.Kits.KitPopulation.FileName, out var pop) ? pop : "",
                    LocalitiesTsv = texts.TryGetValue("map/" + StoryOfTheLand.Game.Map.Kits.Localities.RuntimeFileName, out var loc) ? loc : "",
                    ArmisticeYaml = texts.TryGetValue("map/" + Armistice1949.FileName, out var armistice) ? armistice : "",
                    RegionsTsv = texts.TryGetValue("map/" + Subdistricts1948.RuntimeFileName, out var regionsTsv) ? regionsTsv : "",
                    ArmisticeTsv = texts.TryGetValue("map/" + Armistice1949.RuntimeFileName, out var armisticeTsv) ? armisticeTsv : "",
                    ControlTsv = texts.TryGetValue("map/" + Control1948.RuntimeFileName, out var controlTsv) ? controlTsv : "",
                    ControlYaml = texts.TryGetValue("map/" + Control1948.FileName, out var control) ? control : "",
                    OccupationYaml = texts.TryGetValue("map/" + OccupationLayers.FileName, out var occupation) ? occupation : "",
                    DamageYaml = texts.TryGetValue("map/" + GazaDamage.FileName, out var damage) ? damage : "",
                },
            };
        }

        /// <summary>StreamingAssets as a URL: the page-relative URL on WebGL, a file:// URI elsewhere.</summary>
        public static string StreamingUrl() =>
            Application.streamingAssetsPath.Contains("://") ? Application.streamingAssetsPath : new Uri(Application.streamingAssetsPath).AbsoluteUri;

        static async Awaitable<string> Text(StreamingFetch.Download d, string name, float stallSeconds)
        {
            await StreamingFetch.Done(d, name, stallSeconds);
            try { return d.Request.downloadHandler.text; }
            finally { d.Release(); }
        }

        static async Awaitable<byte[]> Bytes(StreamingFetch.Download d, string name, float stallSeconds)
        {
            await StreamingFetch.Done(d, name, stallSeconds);
            try { return d.Request.downloadHandler.data; }
            finally { d.Release(); }
        }

        /// <summary>Issue #437: <paramref name="url"/> with b=<paramref name="buildId"/> added to its query when it is an
        /// http(s) URL and the id is not empty; a file path or file:// URI (desktop, the editor) is returned unchanged.
        /// StreamingAssets keep fixed names, so without it a browser could serve this build a previous build's cached file.</summary>
        public static string WithBuildId(string url, string buildId) =>
            buildId.Length == 0 || !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                ? url : url + (url.IndexOf('?') >= 0 ? "&" : "?") + "b=" + Uri.EscapeDataString(buildId);

        /// <summary>Fetches the content bundle (every content text in one file, ContentManifest.BundleFileName, so the boot
        /// makes a handful of requests and not one per file: #1054) and the heightmap's two files at once. A
        /// transient failure is retried (StreamingFetch.Done). When one download fails for good, the others still running are
        /// aborted and disposed. Every URL carries <paramref name="buildId"/> (WithBuildId; the player passes
        /// Application.buildGUID).</summary>
        public static Awaitable<GameInputs> FetchAsync(string baseUrl, string buildId = "") => FetchAsync(baseUrl, buildId, StreamingFetch.StallSeconds);

        /// <summary>FetchAsync with another stall limit (the stalled-server test uses a short one).</summary>
        internal static async Awaitable<GameInputs> FetchAsync(string baseUrl, string buildId, float stallSeconds)
        {
            StreamingFetch.Download Get(string path) => StreamingFetch.Get(WithBuildId(baseUrl + "/" + path, buildId));
            var bundle = Get("content/" + ContentManifest.BundleFileName);
            var meta = Get("map/heightmap.json");
            var r16z = Get("map/" + HeightmapCodec.FileName);
            try
            {
                var (_, texts) = ContentManifest.ParseBundle(await Text(bundle, ContentManifest.BundleFileName, stallSeconds));
                var metaText = await Text(meta, "heightmap.json", stallSeconds);
                var r16zBytes = await Bytes(r16z, HeightmapCodec.FileName, stallSeconds);
                DiagLog.BootPhase($"downloaded {texts.Count + 2} files");
                var inputs = FromTexts(texts, metaText, r16zBytes);
                DiagLog.BootPhase("content parsed");
                return inputs;
            }
            finally
            {
                bundle.Release(); meta.Release(); r16z.Release();
            }
        }
    }
}
