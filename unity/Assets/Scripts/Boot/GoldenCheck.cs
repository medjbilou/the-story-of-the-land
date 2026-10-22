#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using StoryOfTheLand.Sim;
using UnityEngine;

namespace StoryOfTheLand.Game.Boot
{
    /// <summary>Plays every family golden (content/palestine/goldens.yaml) in whatever runtime this is (Mono or IL2CPP in
    /// players, WebGL) and reports "GOLDEN OK &lt;name&gt; &lt;hash&gt;" or "GOLDEN FAIL &lt;name&gt; &lt;hash&gt; expected &lt;hash&gt;"
    /// (docs/determinism.md). A golden is named family-&lt;chapter&gt;-&lt;village&gt;: that chapter for a family of that village,
    /// with the golden seed (GoldenRun.Seed), every card answered with its first choice, to the chapter's end.
    /// Asked for by the URL query "golden" on WebGL, or the command-line flag -golden on desktop.</summary>
    public static class GoldenCheck
    {
        /// <summary>The goldens' file under content/: one "name: hash" line each, # comments.</summary>
        public const string FileName = ContentLoader.PalestineDir + "/goldens.yaml";
        const string Prefix = "family-";

        public static bool Requested() => Application.platform == RuntimePlatform.WebGLPlayer
            ? UrlFlags.Has(Application.absoluteURL, "golden")
            : Environment.GetCommandLineArgs().Contains("-golden");

        /// <summary>The exit code a boot error must quit with: 1 for a desktop -golden run (so it never hangs), else null.</summary>
        public static int? QuitCodeOnError(bool requested, bool webgl) => requested && !webgl ? 1 : (int?)null;

        /// <summary>The goldens in <paramref name="text"/> (goldens.yaml), in file order. Throws FormatException on a line that is
        /// not "family-&lt;chapter&gt;-&lt;village&gt;: &lt;hash&gt;".</summary>
        public static IReadOnlyList<(string name, string hash)> Parse(string? text) =>
            (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
                .Select(l =>
                {
                    var at = l.IndexOf(':');
                    var name = at < 0 ? "" : l.Substring(0, at).Trim(); var hash = at < 0 ? "" : l.Substring(at + 1).Trim();
                    if (!name.StartsWith(Prefix, StringComparison.Ordinal) || name.Split('-').Length != 3 || hash.Length == 0)
                        throw new FormatException($"{FileName}: a line must be 'family-<chapter>-<village>: <hash>' (got '{l}')");
                    return (name, hash);
                }).ToList();

        /// <summary>Plays every golden in its order, one OK or FAIL line each. A golden this code cannot play (an unknown
        /// chapter or village) or a run that throws is a FAIL line, never a skipped one, and an empty file fails.</summary>
        public static (bool ok, List<string> lines) Run(Func<string, Content> chapter, string? goldens)
        {
            IReadOnlyList<(string name, string hash)> list;
            try { list = Parse(goldens); }
            catch (Exception e) { return (false, new List<string> { $"GOLDEN FAIL manifest {e.Message}" }); }
            if (list.Count == 0) return (false, new List<string> { "GOLDEN FAIL manifest has no goldens" });
            var lines = new List<string>(); bool ok = true;
            foreach (var (name, expected) in list)
            {
                string hash;
                try { var parts = name.Split('-'); hash = Play(chapter(parts[1]), parts[2]).Hash(); }
                catch (Exception e) { ok = false; lines.Add($"GOLDEN FAIL {name} {e.GetType().Name}: {e.Message}"); continue; }
                if (hash == expected) lines.Add($"GOLDEN OK {name} {hash}");
                else { ok = false; lines.Add($"GOLDEN FAIL {name} {hash} expected {expected}"); }
            }
            return (ok, lines);
        }

        /// <summary>The run a golden pins: <paramref name="chapter"/> for a family of <paramref name="village"/>, golden seed,
        /// every pending card answered with its first choice, until the game is over (the chapter's end).</summary>
        public static GameState Play(Content chapter, string village)
        {
            var g = GameSession.New(chapter, GoldenRun.Seed, village);
            for (int guard = 0; g.State.GameOver == null; guard++)
            {
                if (guard > 10000) throw new InvalidOperationException("the chapter never ended");
                g.Step(g.State.PendingEvents.Select(id => (Command)new ChooseEventOption(id, chapter.Events.Single(e => e.Id == id).Options[0].Id)).ToList());
            }
            return g.State;
        }
    }
}
