#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StoryOfTheLand.Sim;
using UnityEngine;

namespace StoryOfTheLand.Game.Boot
{
    /// <summary>The list of content files a player needs, so a WebGL build (no directory listing over HTTP) can fetch
    /// them: content/manifest.json in StreamingAssets, {"files": [...]} with paths relative to content/, '/' separated,
    /// ordinal order. The editor build step (ContentSync) copies the files and writes the manifest, and packs every listed
    /// file's text into content/bundle.json, which is what the web boot fetches: one request instead of one per file
    /// (#1054). Desktop players and the editor read the files themselves.</summary>
    public static class ContentManifest
    {
        public const string FileName = "manifest.json";
        /// <summary>Every listed file's text in one file: {"files": [...], "texts": [...]}, the same order (#1054).</summary>
        public const string BundleFileName = "bundle.json";
        /// <summary>The map's hex table, always in the manifest. The map files players read are this and the map/*.yaml
        /// (settlements, regions, routes); the precipitation files are tool inputs only.</summary>
        public const string HexesJson = "map/hexes.json";

        /// <summary>Map files players never download: routes_1948.yaml is the hand-written input of tools/routes_1948.py (the
        /// map reads its traced output, review M10) and localities_1948.yaml the sourced input of tools/localities_bake.py (the
        /// map reads its baked runtime form); units_1948.yaml, locations_1948.yaml (the forts and passes) and
        /// settlement_hexes.yaml are map data the game does not read at run time.</summary>
        public static readonly IReadOnlyCollection<string> ToolInputs = new HashSet<string> { "map/routes_1948.yaml", "map/localities_1948.yaml",
            "map/units_1948.yaml", "map/locations_1948.yaml", "map/settlement_hexes.yaml" };

        /// <summary>Baked map files that are not YAML, listed when present: the runtime forms of the 1948 localities and of the
        /// route paths and the region files (boot budget, issue #321). Their YAML still ships: each runtime file is checked
        /// against it.</summary>
        public static readonly IReadOnlyList<string> RuntimeData = new[] { "map/localities_1948.tsv", "map/route_paths_1948.tsv",
            "map/subdistrict_hexes_1948.tsv", "map/armistice_hexes_1949.tsv", "map/control_hexes_1948_49.tsv" };

        [Serializable] sealed class ManifestJson { public string[] files = new string[0]; }
        [Serializable] sealed class BundleJson { public string[] files = new string[0]; public string[] texts = new string[0]; }

        /// <summary>The family game's folders, whose *.yaml ship: palestine/ (model, places, fates, currency, figures, credits,
        /// goldens) and palestine/events/ (one file per chapter; ContentLoader.LoadChapter reads them when a chapter is picked,
        /// F5 step 2).</summary>
        public static readonly IReadOnlyList<string> FamilyFolders = new[] { ContentLoader.PalestineDir, ContentLoader.PalestineDir + "/events" };

        /// <summary>Every *.yaml in map/ (but <see cref="ToolInputs"/>) and the <see cref="FamilyFolders"/>, plus map/hexes.json
        /// and the <see cref="RuntimeData"/> present; ordinal order.</summary>
        public static List<string> Collect(string contentDir)
        {
            IEnumerable<string> Yaml(string sub) => Directory.GetFiles(Path.Combine(contentDir, sub), "*.yaml").Select(f => sub + "/" + Path.GetFileName(f));
            foreach (var need in new[] { HexesJson }.Concat(FamilyFolders))
                if (!File.Exists(Path.Combine(contentDir, need)) && !Directory.Exists(Path.Combine(contentDir, need))) throw new FileNotFoundException("content missing", need);
            return Yaml("map").Concat(FamilyFolders.SelectMany(Yaml)).Append(HexesJson)
                .Concat(RuntimeData.Where(f => File.Exists(Path.Combine(contentDir, f))))
                .Where(f => !ToolInputs.Contains(f)).OrderBy(f => f, StringComparer.Ordinal).ToList();
        }

        public static string Write(IEnumerable<string> files) => JsonUtility.ToJson(new ManifestJson { files = files.ToArray() }, true);

        /// <summary>Parses manifest.json; throws InvalidDataException when it is not a manifest.</summary>
        public static List<string> Parse(string json)
        {
            ManifestJson? m;
            try { m = JsonUtility.FromJson<ManifestJson>(json); }
            catch (ArgumentException e) { throw new InvalidDataException($"{FileName}: malformed JSON ({e.Message})", e); }
            if (m?.files == null || m.files.Length == 0) throw new InvalidDataException($"{FileName}: no files");
            return m.files.ToList();
        }

        /// <summary>bundle.json for <paramref name="files"/> (paths relative to <paramref name="contentDir"/>), each text
        /// read as the disk path reads it (File.ReadAllText), so the content hash is the disk's.</summary>
        public static string WriteBundle(string contentDir, IReadOnlyList<string> files) => JsonUtility.ToJson(new BundleJson
        {
            files = files.ToArray(),
            texts = files.Select(f => File.ReadAllText(Path.Combine(contentDir, f))).ToArray(),
        });

        /// <summary>Parses bundle.json into the file list (as manifest.json lists it) and each file's text; throws
        /// InvalidDataException when it is not a bundle.</summary>
        public static (List<string> Files, Dictionary<string, string> Texts) ParseBundle(string json)
        {
            BundleJson? b;
            try { b = JsonUtility.FromJson<BundleJson>(json); }
            catch (ArgumentException e) { throw new InvalidDataException($"{BundleFileName}: malformed JSON ({e.Message})", e); }
            if (b?.files == null || b.files.Length == 0) throw new InvalidDataException($"{BundleFileName}: no files");
            if (b.texts == null || b.texts.Length != b.files.Length)
                throw new InvalidDataException($"{BundleFileName}: {b.files.Length} files but {b.texts?.Length ?? 0} texts");
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < b.files.Length; i++)
                if (!texts.TryAdd(b.files[i], b.texts[i])) throw new InvalidDataException($"{BundleFileName}: {b.files[i]} twice");
            return (b.files.ToList(), texts);
        }

        /// <summary>Checks a copy made by Copy: manifest.json equals Collect(contentDir) and every listed file exists, and
        /// bundle.json lists the same files with each one's text as the source has it. Throws InvalidDataException naming
        /// the problem.</summary>
        public static void Verify(string contentDir, string targetDir)
        {
            var expected = Collect(contentDir);
            var written = Parse(File.ReadAllText(Path.Combine(targetDir, FileName)));
            if (!expected.SequenceEqual(written))
                throw new InvalidDataException($"{FileName} in {targetDir} does not match the content files ({written.Count} listed, {expected.Count} expected)");
            foreach (var f in written)
                if (!File.Exists(Path.Combine(targetDir, f))) throw new InvalidDataException($"{f}: listed in {FileName} but missing from {targetDir}");
            var bundle = Path.Combine(targetDir, BundleFileName);
            if (!File.Exists(bundle)) throw new InvalidDataException($"{BundleFileName}: missing from {targetDir}");
            var (files, texts) = ParseBundle(File.ReadAllText(bundle));
            if (!expected.SequenceEqual(files))
                throw new InvalidDataException($"{BundleFileName} in {targetDir} does not match the content files ({files.Count} listed, {expected.Count} expected)");
            foreach (var f in files)
                if (texts[f] != File.ReadAllText(Path.Combine(contentDir, f))) throw new InvalidDataException($"{f}: its text in {BundleFileName} differs from the source");
        }

        /// <summary>Replaces <paramref name="targetDir"/> with a copy of the collected files plus manifest.json and bundle.json.</summary>
        public static List<string> Copy(string contentDir, string targetDir)
        {
            var files = Collect(contentDir);
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            foreach (var f in files)
            {
                var to = Path.Combine(targetDir, f);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(Path.Combine(contentDir, f), to);
            }
            File.WriteAllText(Path.Combine(targetDir, FileName), Write(files));
            File.WriteAllText(Path.Combine(targetDir, BundleFileName), WriteBundle(contentDir, files));
            return files;
        }
    }
}
