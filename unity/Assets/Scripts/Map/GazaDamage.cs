#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>The destruction of Gaza, October 2023 on, as UNOSAT's satellite images record it (content/map/gaza_damage_2023.yaml,
    /// tools/gaza_damage_bake.py): per cell of about 300 m, by which image half its damaged and half its destroyed structures
    /// had been recorded. On a game month the terrain greys the cells recorded damaged by the month's end and darkens those
    /// recorded destroyed (StoryOfTheLandEra.hlsl), and lays smoke over the cells whose destruction the images of the month's
    /// span record (the destruction was happening then; not in the months of a truce or ceasefire, the file's quiet spans).
    /// No raw points, no casualties: buildings only. Display only: the sim never reads the file.</summary>
    public sealed class GazaDamage
    {
        public const string FileName = "gaza_damage_2023.yaml";

        /// <summary>Shown: the images recorded by the month's end (0: none, before the war); Smoke: the images whose span (from
        /// the image before, or the war's first day) overlaps the month, as a range (0, 0: none).</summary>
        public readonly struct State : IEquatable<State>
        {
            public readonly int Shown, SmokeFrom, SmokeTo;
            public State(int shown, int smokeFrom, int smokeTo) { Shown = shown; SmokeFrom = smokeFrom; SmokeTo = smokeTo; }
            public bool Equals(State o) => Shown == o.Shown && SmokeFrom == o.SmokeFrom && SmokeTo == o.SmokeTo;
            public override bool Equals(object? o) => o is State s && Equals(s);
            public override int GetHashCode() => Shown * 961 + SmokeFrom * 31 + SmokeTo;
        }

        public readonly double Lon0, Lat0, DLon, DLat;
        public readonly int Nx, Ny;
        public readonly DateTime WarFrom;
        public readonly IReadOnlyList<DateTime> Images;
        readonly List<(DateTime from, DateTime? until)> quiet;
        readonly string tex;

        GazaDamage(Dictionary<string, string> v, List<(DateTime, DateTime?)> quietSpans)
        {
            Lon0 = Num(v, "lon0"); Lat0 = Num(v, "lat0"); DLon = Num(v, "dlon"); DLat = Num(v, "dlat");
            Nx = (int)Num(v, "nx"); Ny = (int)Num(v, "ny");
            WarFrom = Day(Req(v, "war_from"), "war_from");
            var list = Req(v, "images").Trim();
            if (!list.StartsWith("[") || !list.EndsWith("]")) throw new InvalidDataException($"{FileName}: images is not a list");
            Images = list.Substring(1, list.Length - 2).Split(',').Select(s => Day(s.Trim(), "images")).ToList();
            for (int i = 1; i < Images.Count; i++)
                if (Images[i] <= Images[i - 1]) throw new InvalidDataException($"{FileName}: images are not in date order");
            if (Images.Count == 0 || Images.Count > 254 || Images[0] < WarFrom) throw new InvalidDataException($"{FileName}: bad images");
            quiet = quietSpans;
            tex = Req(v, "tex");
        }

        /// <summary>Reads the file's top-level scalars and its quiet spans (the cell rows are the bake's).</summary>
        public static GazaDamage Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException($"{FileName}: empty");
            var v = new Dictionary<string, string>(StringComparer.Ordinal);
            var spans = new List<(DateTime, DateTime?)>();
            string? section = null;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == ' ')
                {
                    if (section == "quiet" && line.TrimStart().StartsWith("- ["))
                    {
                        var parts = line.Trim().Substring(3).TrimEnd(']').Split(',');
                        var until = parts[1].Trim();
                        spans.Add((Day(parts[0].Trim(), "quiet"), until == "null" ? (DateTime?)null : Day(until, "quiet")));
                    }
                    continue;
                }
                int c = line.IndexOf(':');
                if (c <= 0) continue;
                section = line.Substring(0, c);
                var value = line.Substring(c + 1);
                int note = value.IndexOf("   #", StringComparison.Ordinal);
                v[section] = (note >= 0 ? value.Substring(0, note) : value).Trim();
            }
            return new GazaDamage(v, spans);
        }

        static string Req(Dictionary<string, string> v, string key) =>
            v.TryGetValue(key, out var s) && s.Length > 0 ? s : throw new InvalidDataException($"{FileName}: {key} is missing");

        static double Num(Dictionary<string, string> v, string key) =>
            double.TryParse(Req(v, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : throw new InvalidDataException($"{FileName}: {key} is not a number");

        static DateTime Day(string s, string key) =>
            DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d
                : throw new InvalidDataException($"{FileName}: {key} '{s}' is not a yyyy-MM-dd date");

        /// <summary>What the game month holding <paramref name="date"/> shows (the game moves a month a turn).</summary>
        public State On(DateTime date)
        {
            var m0 = new DateTime(date.Year, date.Month, 1);
            var m1 = m0.AddMonths(1);
            if (m1 <= WarFrom) return default;
            int shown = Images.Count(d => d < m1);
            bool still = quiet.Any(q => q.from <= m0 && (q.until == null || m1.AddDays(-1) <= q.until.Value));
            int lo = 0, hi = 0;
            for (int k = 1; k <= Images.Count && !still; k++)
            {
                var start = k == 1 ? WarFrom : Images[k - 2].AddDays(1);
                if (start < m1 && Images[k - 1] >= m0) { if (lo == 0) lo = k; hi = k; }
            }
            return new State(shown, lo, hi);
        }

        static readonly int Tex = Shader.PropertyToID("_ScWarTex"), Rect = Shader.PropertyToID("_ScWarRect"), Grid = Shader.PropertyToID("_ScWarGrid"),
            War = Shader.PropertyToID("_ScWar");
        MapRuntimeAssets? owner;
        GeoFrame frame;
        bool decoded;
        State? drawn;

        /// <summary>Gives the built map the layer (MapScene.Build); nothing is decoded until the war's first month.</summary>
        public void Attach(GeoFrame mapFrame, MapRuntimeAssets assets)
        {
            frame = mapFrame; owner = assets ?? throw new ArgumentNullException(nameof(assets));
            decoded = false; drawn = null;
            Shader.SetGlobalVector(War, Vector4.zero);
            assets.OnTeardown(() => { Shader.SetGlobalVector(War, Vector4.zero); owner = null; decoded = false; drawn = null; });
        }

        /// <summary>Draws what <see cref="On"/> gives for <paramref name="date"/>; a month that changes nothing leaves the map alone.</summary>
        public void Show(DateTime date)
        {
            if (owner == null) return;
            var s = On(date);
            if (drawn is State old && old.Equals(s)) return;
            drawn = s;
            if (s.Shown == 0 && s.SmokeFrom == 0) { Shader.SetGlobalVector(War, Vector4.zero); return; }
            if (!decoded)
            {
                var t = owner.Own(new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { name = "GazaDamage", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
                if (!t.LoadImage(Convert.FromBase64String(tex), true) || t.width != Nx || t.height != Ny)
                    throw new InvalidDataException($"{FileName}: tex is not a {Nx} x {Ny} PNG");
                double w = frame.Lon1 - frame.Lon0, h = frame.Lat1 - frame.Lat0;
                Shader.SetGlobalTexture(Tex, t);
                Shader.SetGlobalVector(Rect, new Vector4((float)((Lon0 - frame.Lon0) / w), (float)((Lat0 - frame.Lat0) / h), (float)(w / (Nx * DLon)), (float)(h / (Ny * DLat))));
                Shader.SetGlobalVector(Grid, new Vector4(Nx, Ny, 0, 0));
                decoded = true;
            }
            Shader.SetGlobalVector(War, new Vector4(s.Shown, s.SmokeFrom, s.SmokeTo, 1));
        }

        /// <summary>The legend's lines on <paramref name="date"/> (null before the war): map.war_damage ({0} the latest image's
        /// date, through Control1948.DateText) once an image is recorded, and map.war_smoke while destruction is drawn.</summary>
        public string? Label(DateTime date, Func<string, string> text)
        {
            var s = On(date);
            var lines = new List<string>();
            if (s.Shown > 0) lines.Add(string.Format(CultureInfo.InvariantCulture, text("map.war_damage"), Control1948.DateText(Images[s.Shown - 1], text)));
            if (s.SmokeFrom > 0) lines.Add(text("map.war_smoke"));
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }
    }
}
