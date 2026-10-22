using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StoryOfTheLand.Sim
{
    /// <summary>A chapter of The Story of the Land (content/palestine/events/&lt;chapter&gt;.yaml): its events in one list.
    /// Written by tools/tsv_to_events.py; each entry is a <see cref="GameEvent"/> (docs/palestine-model.md).</summary>
    public sealed class ChapterFile { public List<GameEvent> Events { get; set; } = new List<GameEvent>(); }
    /// <summary>palestine/places.yaml and fates.yaml, written by tools/places_to_yaml.py.</summary>
    public sealed class PlacesFile { public List<Place> Places { get; set; } = new List<Place>(); }
    public sealed class FatesFile { public List<VillageFate> Fates { get; set; } = new List<VillageFate>(); }
    /// <summary>palestine/currency.yaml, written by tools/currency_to_yaml.py.</summary>
    public sealed class CurrencyFile
    {
        public List<Currency> Currencies { get; set; } = new List<Currency>();
        public List<PlaceRegion> Regions { get; set; } = new List<PlaceRegion>();
    }

    public static class ContentLoader
    {
        sealed class ModelFile { public List<Indicator> Indicators { get; set; } = new List<Indicator>(); }

        /// <summary>The Palestinian content set under the content root.</summary>
        public const string PalestineDir = "palestine";
        /// <summary>The shape of every id (events, options, resources, places): safe in placeholders and save files.</summary>
        internal static readonly Regex Id = new Regex(@"^[a-z][a-z0-9_]*\z");
        internal const string IdRule = "must be lowercase letters, digits and underscores, starting with a letter";
        static readonly Regex ChapterId = new Regex("^[a-z0-9_]+$");
        static readonly HashSet<string> FateKinds = new HashSet<string> { "inhabited", "depopulated", "destroyed", "built_over" };

        static readonly IDeserializer Yaml = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithDuplicateKeyChecking()   // two equal keys would otherwise silently keep only the last value
            .Build();

        /// <summary>F4/F5: loads the family resources (palestine/model.yaml), one chapter's events, the places and the village
        /// fates, and checks the events (EventRules). <paramref name="readFile"/> takes a path relative to content/palestine/
        /// and returns the file's text, or null when it is missing (a WebGL build reads StreamingAssets through a URL). The
        /// chapter is one era from its first dated month to the end of its last (the window tools/tsv_to_events.py gives its
        /// fate templates). Content.Hash covers every file read.</summary>
        public static Content LoadChapter(Func<string, string?> readFile, string chapter)
        {
            if (readFile == null) throw new ArgumentNullException(nameof(readFile));
            if (!ChapterId.IsMatch(chapter ?? "")) throw new ArgumentException($"bad chapter id '{chapter}'", nameof(chapter));
            var read = new List<(string rel, string text)>();
            readFile = Hashed(readFile, read);
            var m = Read<ModelFile>(readFile, "model.yaml");
            var ids = new HashSet<string>();
            foreach (var i in m.Indicators)
            {
                var at = $"indicators[{i.Id}]";
                if (!Id.IsMatch(i.Id)) throw new ContentException("model.yaml", at + ".id", IdRule);
                if (!ids.Add(i.Id)) throw new ContentException("model.yaml", at + ".id", "duplicate id");
                Named("model.yaml", at + ".name", i.Name);
                Named("model.yaml", at + ".name_ar", i.NameAr);
                Unit("model.yaml", at + ".base", i.Base);
            }
            var rel = "events/" + chapter + ".yaml";
            var file = Read<ChapterFile>(readFile, rel);
            foreach (var e in file.Events) e.File = rel;
            if (file.Events.Count == 0) throw new ContentException(rel, "events", "a chapter needs at least one event");
            var c = new Content { Indicators = m.Indicators, Events = file.Events };
            EventRules.Validate(c, ids);
            c.Places = Read<PlacesFile>(readFile, "places.yaml").Places;
            c.Fates = Read<FatesFile>(readFile, "fates.yaml").Fates;
            var places = ValidatePlaces(c);
            foreach (var e in c.Events)
            {
                var at = $"events[{e.Id}]";
                if (e.Place != "" && !places.Contains(e.Place)) throw new ContentException(rel, at + ".place", $"unknown place '{e.Place}' (places.yaml)");
                if (e.Scope.StartsWith("region:", StringComparison.Ordinal) && !places.Contains(e.Scope.Substring(7)))
                    throw new ContentException(rel, at + ".scope", $"unknown place '{e.Scope.Substring(7)}' (places.yaml)");
            }
            var from = c.Events.Min(e => e.From);
            c.Eras = new List<Era> { new Era { Id = chapter!, Name = chapter!, From = from, To = c.Events.Max(e => e.To ?? from) } };
            c.Rules.Start = from;
            var money = Read<CurrencyFile>(readFile, "currency.yaml");
            c.Currencies = money.Currencies;
            c.PlaceRegions = money.Regions;
            ValidateCurrencies(c, places);
            c.Hash = HashOf(read);
            return c;
        }

        /// <summary>As <see cref="LoadChapter(Func{string, string?}, string)"/>, from &lt;contentRoot&gt;/palestine/ on disk.</summary>
        public static Content LoadChapter(string contentRoot, string chapter)
        {
            var root = Path.Combine(contentRoot, PalestineDir);
            return LoadChapter(rel => { var p = Path.Combine(root, rel); return File.Exists(p) ? File.ReadAllText(p) : null; }, chapter);
        }

        /// <summary>Places: unique ids of the id shape, a name, coordinates inside Mandate Palestine (the region rule measures
        /// from them). Fates: a known place and kind, a month only with a year, at least one https source. Returns the place ids.</summary>
        static HashSet<string> ValidatePlaces(Content c)
        {
            const string pf = "places.yaml", ff = "fates.yaml";
            Unique(pf, "places", c.Places.Select(p => p.Id));
            foreach (var p in c.Places)
            {
                var at = $"places[{p.Id}]";
                Named(pf, at + ".name", p.Name);
                if (!(p.Lon >= 34 && p.Lon <= 36 && p.Lat >= 29 && p.Lat <= 34)) throw new ContentException(pf, at, "lon/lat outside Mandate Palestine");
                Sourced(pf, at + ".sources", p.Sources);
            }
            var ids = new HashSet<string>(c.Places.Select(p => p.Id));
            for (int i = 0; i < c.Fates.Count; i++)
            {
                var f = c.Fates[i];
                var at = FormattableString.Invariant($"fates[{i}]");
                if (!ids.Contains(f.Place)) throw new ContentException(ff, at + ".place", $"unknown place '{f.Place}'");
                if (!FateKinds.Contains(f.Fate)) throw new ContentException(ff, at + ".fate", "must be inhabited, depopulated, destroyed or built_over");
                if (f.Month.HasValue && (!f.Year.HasValue || f.Month < 1 || f.Month > 12)) throw new ContentException(ff, at + ".month", "a month from 1 to 12, with a year");
                Sourced(ff, at + ".sources", f.Sources);
            }
            return ids;
        }

        /// <summary>currency.yaml: named, sourced rows of a known region, and places of known ids, listed once, in west_bank or
        /// gaza. Every month of the chapter, in every region, has exactly one currency, so the HUD always has one to show.</summary>
        static void ValidateCurrencies(Content c, HashSet<string> places)
        {
            const string f = "currency.yaml";
            Unique(f, "currencies", c.Currencies.Select(x => x.Id));
            foreach (var x in c.Currencies)
            {
                var at = $"currencies[{x.Id}]";
                if (x.Region != Family.National && !Family.Regions.Contains(x.Region)) throw new ContentException(f, at + ".region", "national, inside_1948, west_bank or gaza");
                if (x.FromMonth < 1 || x.FromMonth > 12 || x.FromYear < 1800 || x.ToYear.HasValue != x.ToMonth.HasValue || x.ToMonth < 1 || x.ToMonth > 12 || x.To <= x.From)
                    throw new ContentException(f, at, "a from month, and a later to month or none");
                foreach (var (field, text) in new[] { ("name", x.Name), ("name_ar", x.NameAr), ("symbol", x.Symbol), ("symbol_ar", x.SymbolAr) })
                    Named(f, at + "." + field, text);
                Sourced(f, at + ".sources", x.Sources);
            }
            Unique(f, "regions", c.PlaceRegions.Select(r => r.Place));
            foreach (var r in c.PlaceRegions)
            {
                var at = $"regions[{r.Place}]";
                if (!places.Contains(r.Place)) throw new ContentException(f, at, $"unknown place '{r.Place}' (places.yaml)");
                if (r.Region != "west_bank" && r.Region != "gaza") throw new ContentException(f, at + ".region", "west_bank or gaza (inside_1948 is the default)");
                Sourced(f, at + ".sources", r.Sources);
            }
            for (var d = c.Rules.Start; d < c.End; d = d.AddMonths(1))
                foreach (var region in Family.Regions)
                {
                    var n = c.Currencies.Count(x => (x.Region == region || x.Region == Family.National) && x.In(d));
                    if (n != 1) throw new ContentException(f, "currencies", FormattableString.Invariant($"{n} currencies for {region} in {d:yyyy-MM}, expected 1"));
                }
        }

        static void Sourced(string file, string field, List<string> urls)
        {
            if (urls.Count == 0 || urls.Any(u => !u.StartsWith("https://", StringComparison.Ordinal)))
                throw new ContentException(file, field, "at least one source, each an https:// URL");
        }

        static T Read<T>(Func<string, string?> readFile, string relPath)
        {
            string? text;
            try { text = readFile(relPath); }
            catch (IOException e) { throw new ContentException(relPath, "(file)", e.Message); }
            catch (UnauthorizedAccessException e) { throw new ContentException(relPath, "(file)", e.Message); }
            if (text == null) throw new ContentException(relPath, "(file)", "missing file");
            T result;
            try { result = Yaml.Deserialize<T>(text) ?? throw new ContentException(relPath, "(file)", "empty file"); }
            catch (YamlException e) { throw new ContentException(relPath, $"line {e.Start.Line}", e.InnerException?.Message ?? e.Message); }
            CheckNulls(relPath, "", result!);
            return result;
        }

        /// <summary>YAML's `key: ~` or `key:` sets a property to null: a string becomes "", a list or map is an error, and every
        /// text must be valid Unicode. Walks nested content types, naming the field.</summary>
        static void CheckNulls(string file, string path, object o)
        {
            foreach (var p in o.GetType().GetProperties().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                if (p.GetCustomAttribute<YamlIgnoreAttribute>() != null) continue;
                var key = (path == "" ? "" : path + ".") + UnderscoredNamingConvention.Instance.Apply(p.Name);
                var t = p.PropertyType;
                var v = p.GetValue(o);
                if (t == typeof(string)) { if (v == null) p.SetValue(o, ""); else Encodable(file, key, (string)v); }
                else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                {
                    if (v == null) throw new ContentException(file, key, "must be a list");
                    var list = (IList)v;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        if (item == null) throw new ContentException(file, FormattableString.Invariant($"{key}[{i}]"), "empty entry");
                        if (item is string si) Encodable(file, FormattableString.Invariant($"{key}[{i}]"), si);
                        if (!IsContentType(item.GetType())) continue;
                        var id = item.GetType().GetProperty("Id")?.GetValue(item) as string;
                        CheckNulls(file, string.IsNullOrEmpty(id) ? FormattableString.Invariant($"{key}[{i}]") : $"{key}[{id}]", item);
                    }
                }
                else if (v != null && IsContentType(t)) CheckNulls(file, key, v);
            }
        }

        static bool IsContentType(Type t) => t.IsClass && t != typeof(string) && t.Namespace == typeof(Content).Namespace;

        /// <summary>Every id and text must be valid Unicode (no lone surrogate), or orders and saves that carry it would fail
        /// later. Uses the same check as the save format.</summary>
        static void Encodable(string file, string field, string s)
        {
            if (!SaveText.IsEncodable(s)) throw new ContentException(file, field, "must be valid text (no lone surrogate)");
        }

        /// <summary>Wraps a reader so that every file it returns is recorded in <paramref name="read"/>, for <see cref="HashOf"/>.</summary>
        static Func<string, string?> Hashed(Func<string, string?> inner, List<(string rel, string text)> read) => rel =>
        {
            var text = inner(rel);
            if (text != null && text.Length > 0 && text[0] == '﻿') text = text.Substring(1);   // a byte-order mark is not content
            if (text != null) read.Add((rel, text));
            return text;
        };

        /// <summary>SHA-256 over "path\nlength\ntext" for each file, CRLF normalised to LF (a Windows checkout hashes alike).</summary>
        static string HashOf(List<(string rel, string text)> files)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (rel, text) in files)
            {
                var t = text.Replace("\r\n", "\n");
                sb.Append(rel).Append('\n').Append(t.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n').Append(t);
            }
            using var sha = System.Security.Cryptography.SHA256.Create();
            return string.Concat(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        }

        /// <summary>Every id has the id shape and is unique in its list.</summary>
        static void Unique(string file, string list, IEnumerable<string> ids)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                if (!Id.IsMatch(id)) throw new ContentException(file, $"{list}[{id}].id", IdRule);
                if (!seen.Add(id)) throw new ContentException(file, $"{list}[{id}].id", "duplicate id");
            }
        }

        static readonly HashSet<string> Statuses = new HashSet<string> { "draft", "fact-checked", "historian-reviewed", "sensitivity-reviewed" };

        /// <summary>A display name: required, and not blank.</summary>
        internal static void Named(string file, string field, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ContentException(file, field, "required (the name shown to the player)");
        }

        /// <summary>A row's sign-off (#375): reviewed_by and reviewed_on together, a GitHub handle, a day as YYYY-MM-DD (read as
        /// text, the same rule as tools/review_record.py and the map's readers). <paramref name="at"/> is the row's path, as in "events[e1]".</summary>
        internal static void Review(string file, string at, string by, bool byGiven, string on, bool onGiven)
        {
            var p = ReviewRecord.Problem(byGiven, by, onGiven, on);
            if (p != null) throw new ContentException(file, $"{at}.{p.Value.field}", p.Value.message);
        }

        internal static void Status(string file, string field, string status)
        {
            if (!Statuses.Contains(status))
                throw new ContentException(file, field, $"unknown status '{status}' (must be draft, fact-checked, historian-reviewed or sensitivity-reviewed)");
        }

        internal static void Finite(string file, string field, double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new ContentException(file, field, "must be a finite number");
        }

        internal static void Unit(string file, string field, double v)
        {
            if (!(v >= 0 && v <= 1)) throw new ContentException(file, field, "must be a number from 0 to 1");
        }

        /// <summary>from is required (a missing date deserialises to DateTime default); to, when given, must be after from.</summary>
        internal static void Period(string file, string path, DateTime from, DateTime? to)
        {
            if (from == default) throw new ContentException(file, path + ".from", "required");
            if (to.HasValue && to.Value <= from) throw new ContentException(file, path + ".to", FormattableString.Invariant($"must be after from ({from:yyyy-MM-dd})"));
        }
    }
}
