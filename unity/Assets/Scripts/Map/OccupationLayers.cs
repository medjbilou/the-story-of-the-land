#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>The occupation on the map, June 1967 on (content/map/occupation_layers.yaml, tools/occupation_bake.py from the
    /// true shapes of tools/occupation_digitise.py): the occupied West Bank and Gaza Strip, Oslo II's areas, the wall, Gaza's
    /// fence and access-restricted area, and the settlements by founding year, each from its own sourced date (the file's
    /// *_from keys). The terrain draws them from three textures (StoryOfTheLandEra.hlsl), not snapped to hexes. Display only:
    /// the sim never reads the file. The textures are decoded the first time a date needs them (no cost before June 1967).</summary>
    public sealed class OccupationLayers
    {
        public const string FileName = "occupation_layers.yaml";

        /// <summary>What the map shows on a date. SettlementYear: settlements founded up to that year (0: none); Wall: 0 none,
        /// 1 the route being built (2002-2017), 2 as OCHA mapped it in January 2018.</summary>
        public readonly struct State : IEquatable<State>
        {
            public readonly bool Occupied, Oslo, Fence, Ara, Evacuated, Footprints;
            public readonly int SettlementYear, Wall;
            public State(bool occupied, bool oslo, bool fence, bool ara, int settlementYear, bool evacuated, bool footprints, int wall)
            { Occupied = occupied; Oslo = oslo; Fence = fence; Ara = ara; SettlementYear = settlementYear; Evacuated = evacuated; Footprints = footprints; Wall = wall; }
            public bool Any => Occupied;
            public bool Equals(State o) => Occupied == o.Occupied && Oslo == o.Oslo && Fence == o.Fence && Ara == o.Ara && SettlementYear == o.SettlementYear
                && Evacuated == o.Evacuated && Footprints == o.Footprints && Wall == o.Wall;
            public override bool Equals(object? o) => o is State s && Equals(s);
            public override int GetHashCode() => (SettlementYear * 397) ^ (Wall << 8) ^ (Occupied ? 1 : 0) ^ (Oslo ? 2 : 0) ^ (Fence ? 4 : 0) ^ (Ara ? 16 : 0) ^ (Evacuated ? 32 : 0) ^ (Footprints ? 64 : 0);
        }

        public readonly double Lon0, Lat0, DLon, DLat;
        public readonly int Nx, Ny;
        public readonly float DistUnitM;
        public readonly DateTime OccupiedFrom, FenceFrom, OsloFrom, AraFrom, WallFrom, EvacuatedOn, FootprintsFrom, WallMappedFrom;
        readonly string[] textures;

        OccupationLayers(Dictionary<string, string> v)
        {
            Lon0 = Num(v, "lon0"); Lat0 = Num(v, "lat0"); DLon = Num(v, "dlon"); DLat = Num(v, "dlat");
            Nx = (int)Num(v, "nx"); Ny = (int)Num(v, "ny"); DistUnitM = (float)Num(v, "dist_unit_m");
            if (Nx < 1 || Ny < 1 || DLon <= 0 || DLat <= 0 || DistUnitM <= 0) throw new InvalidDataException($"{FileName}: a bad grid");
            OccupiedFrom = Day(v, "occupied_from"); FenceFrom = Day(v, "fence_from"); OsloFrom = Day(v, "oslo_from"); AraFrom = Day(v, "ara_from");
            WallFrom = Day(v, "wall_from"); EvacuatedOn = Day(v, "evacuated_on"); FootprintsFrom = Day(v, "footprints_from"); WallMappedFrom = Day(v, "wall_mapped_from");
            textures = new[] { Req(v, "tex1"), Req(v, "tex2"), Req(v, "tex3") };
        }

        /// <summary>Reads the file's top-level scalars (the settlement and source lists are for the tools and tests). Throws
        /// InvalidDataException naming the file and the key that is missing or malformed.</summary>
        public static OccupationLayers Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException($"{FileName}: empty");
            var v = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == ' ' || line[0] == '#' || line[0] == '-') continue;
                int c = line.IndexOf(": ", StringComparison.Ordinal);
                if (c <= 0) continue;
                var value = line.Substring(c + 2);
                int note = value.IndexOf("   #", StringComparison.Ordinal);
                v[line.Substring(0, c)] = (note >= 0 ? value.Substring(0, note) : value).Trim();
            }
            return new OccupationLayers(v);
        }

        static string Req(Dictionary<string, string> v, string key) =>
            v.TryGetValue(key, out var s) && s.Length > 0 ? s : throw new InvalidDataException($"{FileName}: {key} is missing");

        static double Num(Dictionary<string, string> v, string key) =>
            double.TryParse(Req(v, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : throw new InvalidDataException($"{FileName}: {key} is not a number");

        static DateTime Day(Dictionary<string, string> v, string key) =>
            DateTime.TryParseExact(Req(v, key), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d
                : throw new InvalidDataException($"{FileName}: {key} is not a yyyy-MM-dd date");

        /// <summary>What shows on <paramref name="date"/>: nothing before the occupation (5 June 1967); then each layer from
        /// its own date. Settlements by founding year (Wikidata's year: a settlement shows from 1 January of it), those
        /// emptied in August 2005 gone from that day.</summary>
        public State On(DateTime date)
        {
            if (date < OccupiedFrom) return default;
            int wall = date >= WallMappedFrom ? 2 : date >= WallFrom ? 1 : 0;
            return new State(true, date >= OsloFrom, date >= FenceFrom, date >= AraFrom, date.Year, date >= EvacuatedOn, date >= FootprintsFrom, wall);
        }

        static readonly int Tex1 = Shader.PropertyToID("_ScEraTex1"), Tex2 = Shader.PropertyToID("_ScEraTex2"), Tex3 = Shader.PropertyToID("_ScEraTex3"),
            Rect = Shader.PropertyToID("_ScEraRect"), Grid = Shader.PropertyToID("_ScEraGrid"), A = Shader.PropertyToID("_ScEraA"), B = Shader.PropertyToID("_ScEraB");

        MapRuntimeAssets? owner;
        GeoFrame frame;
        bool decoded;
        State? shown;

        /// <summary>Gives the built map the layer (MapScene.Build): nothing is drawn or decoded yet; the map's teardown takes
        /// it off.</summary>
        public void Attach(GeoFrame mapFrame, MapRuntimeAssets assets)
        {
            frame = mapFrame; owner = assets ?? throw new ArgumentNullException(nameof(assets));
            decoded = false; shown = null;
            Shader.SetGlobalVector(Grid, Vector4.zero);
            assets.OnTeardown(() => { Shader.SetGlobalVector(Grid, Vector4.zero); owner = null; decoded = false; shown = null; });
        }

        /// <summary>Draws what <see cref="On"/> gives for <paramref name="date"/>; a date that changes nothing leaves the map
        /// alone. Before the first date nothing is decoded.</summary>
        public void Show(DateTime date)
        {
            if (owner == null) return;
            var s = On(date);
            if (shown is State old && old.Equals(s)) return;
            shown = s;
            if (s.Any && !decoded) Decode();
            Shader.SetGlobalVector(A, new Vector4(s.Occupied ? 1 : 0, s.Oslo ? 1 : 0, s.Fence ? 1 : 0, s.Ara ? 1 : 0));
            Shader.SetGlobalVector(B, new Vector4(s.SettlementYear, s.Evacuated ? 1 : 0, s.Footprints ? 1 : 0, s.Wall));
            if (decoded) Shader.SetGlobalVector(Grid, new Vector4(Nx, Ny, DistUnitM, s.Any ? 1 : 0));
        }

        void Decode()
        {
            var ids = new[] { Tex1, Tex2, Tex3 };
            for (int i = 0; i < 3; i++)
            {
                var t = owner!.Own(new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
                { name = "Occupation" + (i + 1), filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
                if (!t.LoadImage(Convert.FromBase64String(textures[i]), true) || t.width != Nx || t.height != Ny)
                    throw new InvalidDataException($"{FileName}: tex{i + 1} is not a {Nx} x {Ny} PNG");
                Shader.SetGlobalTexture(ids[i], t);
            }
            double w = frame.Lon1 - frame.Lon0, h = frame.Lat1 - frame.Lat0;
            Shader.SetGlobalVector(Rect, new Vector4((float)((Lon0 - frame.Lon0) / w), (float)((Lat0 - frame.Lat0) / h),
                (float)(w / (Nx * DLon)), (float)(h / (Ny * DLat))));
            decoded = true;
        }

        /// <summary>The map label on <paramref name="date"/> (null before June 1967): map.occ_legend, then the parts shown
        /// joined by " · " (map.occ_land or, from Oslo II, map.occ_oslo; map.occ_wall_building or map.occ_wall; map.occ_fence;
        /// map.occ_ara; map.occ_settlements), then map.occ_sources. <paramref name="text"/> gives a UI string by key.</summary>
        public string? Label(DateTime date, Func<string, string> text)
        {
            var s = On(date);
            if (!s.Any) return null;
            var parts = new List<string> { text(s.Oslo ? "map.occ_oslo" : "map.occ_land") };
            if (s.Wall > 0) parts.Add(text(s.Wall == 2 ? "map.occ_wall" : "map.occ_wall_building"));
            if (s.Fence) parts.Add(text("map.occ_fence"));
            if (s.Ara) parts.Add(text("map.occ_ara"));
            parts.Add(text("map.occ_settlements"));
            return text("map.occ_legend") + "\n" + string.Join(" · ", parts) + "\n" + text("map.occ_sources");
        }
    }
}
