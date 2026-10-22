using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StoryOfTheLand.Sim
{
    public sealed class SaveException : Exception
    {
        /// <summary><see cref="Code"/> of a save made under an older ruleset or save schema:
        /// R-SAVE-001.</summary>
        public const string OlderRules = "older_rules";
        /// <summary><see cref="Code"/> of a save made by a newer version of the game: a newer schema, or a ruleset this build
        /// does not know (R-SAVE-002).</summary>
        public const string NewerVersion = "newer_version";

        /// <summary>1-based line of the save text the error is on (0 when it concerns the whole save).</summary>
        public int Line { get; }
        /// <summary>A stable refusal code the UI translates (<see cref="OlderRules"/>, <see cref="NewerVersion"/>), or null for a
        /// damaged save, whose reason the UI words itself. The English message never asks the player to start a new game:
        /// the UI says that once (N4).</summary>
        public string? Code { get; }
        public SaveException(int line, string message, string? code = null)
            : base(line > 0 ? FormattableString.Invariant($"line {line}: {message}") : message) { Line = line; Code = code; }
        /// <summary>A save whose replay threw <paramref name="inner"/>: kept, with its stack, for the log (#631).</summary>
        public SaveException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// A save is seed + command log + content hash + schema version (spec section 3); loading replays it. Text format,
    /// one item per line:
    ///   storyoftheland-save 3          schema version (a schema 1 or 2 save is from older rules)
    ///   content &lt;hex&gt;             Content.Hash of the content it was played with
    ///   seed &lt;ulong&gt;  /  mode Historical
    ///   ruleset family-0.1         the rules it was played under (Content.Ruleset); the retired rulesets (legacy,
    ///                              gdd-0.1) are older rules (R-SAVE-001), any other was made by a newer version (R-SAVE-002)
    ///   village ID                 F5: the family's village (GameState.Village)
    ///   turn                       starts the next turn's command list (one per Simulation.Step)
    ///   event ID OPTION            a ChooseEventOption
    ///   end &lt;hex&gt;                 GameState.Hash() after the last turn (checks the replay)
    /// Strings are percent-escaped (SaveText).
    /// </summary>
    public sealed class SaveGame
    {
        public const int SchemaVersion = 3;
        /// <summary>F5: the ruleset of a chapter of The Story of the Land (Content.Ruleset).</summary>
        public const string FamilyRuleset = "family-0.1";
        /// <summary>The retired rulesets, whose saves this game refuses as made under older rules.</summary>
        static readonly string[] EarlierRulesets = { "legacy", "gdd-0.1" };
        /// <summary>Turns are a month long, so 2000 turns is 166 years, far past any chapter; a save with more is hostile or damaged.</summary>
        public const int MaxTurns = 2000;
        const string Magic = "storyoftheland-save";

        /// <summary>Each turn as its canonical command lines. The save keeps text, not the caller's objects, so nothing
        /// outside can change a recorded turn; <see cref="Turns"/> parses fresh copies on demand.</summary>
        readonly List<List<string>> turns = new List<List<string>>();

        public string ContentHash { get; }
        public ulong Seed { get; }
        /// <summary>The ruleset the game was played under: <see cref="FamilyRuleset"/> (R-SAVE-001).</summary>
        public string Ruleset => FamilyRuleset;
        /// <summary>F5: the family's village (GameState.Village).</summary>
        public string Village { get; }
        /// <summary>The recorded turns as new command objects each time it is read (changing them changes nothing in the save).</summary>
        public IReadOnlyList<IReadOnlyList<Command>> Turns =>
            turns.Select(t => (IReadOnlyList<Command>)t.Select(l => ParseCommand(0, l)).ToList().AsReadOnly()).ToList().AsReadOnly();
        /// <summary>GameState.Hash() after the last recorded turn (the new game's hash before any turn).</summary>
        public string FinalHash { get; private set; }

        public SaveGame(string contentHash, ulong seed, string initialHash, string village)
        {
            ContentHash = contentHash ?? throw new ArgumentNullException(nameof(contentHash));
            if (village == null || !ContentLoader.Id.IsMatch(village))
                throw new ArgumentException($"a family game names its village (got '{village}')", nameof(village));
            Seed = seed; Village = village;
            FinalHash = initialHash ?? throw new ArgumentNullException(nameof(initialHash));
        }

        /// <summary>Appends one turn: the commands passed to Simulation.Step and the state it returned.</summary>
        internal void Record(IReadOnlyList<Command> commands, GameState after)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (after == null) throw new ArgumentNullException(nameof(after));
            turns.Add(commands.Select(Format).ToList());
            FinalHash = after.Hash();
        }

        public string Write()
        {
            var sb = new StringBuilder();
            sb.Append(Magic).Append(' ').Append(SchemaVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("content ").Append(ContentHash).Append('\n');
            sb.Append("seed ").Append(Seed.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("mode Historical\n");
            sb.Append("ruleset ").Append(Ruleset).Append('\n');
            sb.Append("village ").Append(Esc(Village)).Append('\n');
            foreach (var t in turns)
            {
                sb.Append("turn\n");
                foreach (var line in t) sb.Append(line).Append('\n');
            }
            sb.Append("end ").Append(FinalHash).Append('\n');
            return sb.ToString();
        }

        static string Esc(string s) => SaveText.Escape(s);

        static string Format(Command cmd) => cmd is ChooseEventOption e
            ? $"event {Esc(e.EventId)} {Esc(e.OptionId)}"
            : throw new ArgumentException($"Cannot save command kind '{cmd.Kind}'", nameof(cmd));

        /// <summary>Parses save text; any malformed line throws SaveException naming the line.</summary>
        public static SaveGame Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);   // a byte-order mark is not part of the save
            var lines = text.Replace("\r\n", "\n").Split('\n');
            int n = lines.Length;
            while (n > 0 && lines[n - 1] == "") n--;
            if (n < 5) throw new SaveException(0, "Not a save of this game (too short)");
            if (lines.Take(n).Count(l => l == "turn") > MaxTurns)
                throw new SaveException(0, FormattableString.Invariant($"save has {lines.Take(n).Count(l => l == "turn")} turns; the most this game plays is {MaxTurns}"));
            string[] Head(int i, string key, int parts)
            {
                var f = lines[i].Split(' ');
                if (f[0] != key || f.Length != parts) throw new SaveException(i + 1, $"expected '{key}'");
                return f;
            }
            var magic = Head(0, Magic, 2);
            if (!int.TryParse(magic[1], NumberStyles.None, CultureInfo.InvariantCulture, out var version)) throw new SaveException(1, "bad schema version");
            if (version > SchemaVersion) throw new SaveException(1, FormattableString.Invariant($"save schema version {version} is newer than this game ({SchemaVersion})"), SaveException.NewerVersion);
            if (version < 1) throw new SaveException(1, FormattableString.Invariant($"unknown save schema version {version}"));
            // R-SAVE-001: a schema 1 or 2 save was made under older rules, which this game does not play.
            if (version < SchemaVersion) throw new SaveException(0, "This save was made with an earlier version of the game's rules.", SaveException.OlderRules);
            var content = Head(1, "content", 2)[1];
            if (!IsHash(content)) throw new SaveException(2, "bad content hash");
            if (!ulong.TryParse(Head(2, "seed", 2)[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seed)) throw new SaveException(3, "bad seed");
            if (Head(3, "mode", 2)[1] != "Historical") throw new SaveException(4, "bad mode");
            if (n < 7) throw new SaveException(0, "Not a save of this game (too short)");
            var ruleset = Head(4, "ruleset", 2)[1];
            if (EarlierRulesets.Contains(ruleset)) throw new SaveException(0, "This save was made with an earlier version of the game's rules.", SaveException.OlderRules);
            // R-SAVE-002 (N2): a ruleset this build does not know comes from a newer version, like a newer schema.
            if (ruleset != FamilyRuleset)
                throw new SaveException(5, $"This save was made with a newer version of the game (ruleset '{ruleset}').", SaveException.NewerVersion);
            var village = SaveText.Unescape(Head(5, "village", 2)[1]) ?? "";
            if (!ContentLoader.Id.IsMatch(village)) throw new SaveException(6, "a family game needs its village");
            var end = Head(n - 1, "end", 2)[1];
            if (!IsHash(end)) throw new SaveException(n, "bad end hash");
            var save = new SaveGame(content, seed, end, village);
            List<string>? current = null;
            for (int i = 6; i < n - 1; i++)
            {
                if (lines[i] == "turn") { current = new List<string>(); save.turns.Add(current); continue; }
                if (current == null) throw new SaveException(i + 1, "command before the first 'turn'");
                current.Add(Format(ParseCommand(i + 1, lines[i])));   // canonical form, validated now
            }
            return save;
        }

        static bool IsHash(string s) => s.Length == 64 && s.All(ch => (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'));

        static Command ParseCommand(int line, string text)
        {
            var f = text.Split(' ');
            if (f[0] != "event") throw new SaveException(line, $"unknown command '{f[0]}'");
            if (f.Length != 3) throw new SaveException(line, "'event' needs 2 values");
            string Str(string s) => SaveText.Unescape(s) ?? throw new SaveException(line, $"bad text '{s}'");
            return new ChooseEventOption(Str(f[1]), Str(f[2]));
        }
    }

    /// <summary>The save format's own percent-encoding (RFC 3986: unreserved characters stay, everything else is %XX of its
    /// UTF-8 bytes), so a save does not depend on the runtime's Uri class (Mono and .NET differ).</summary>
    internal static class SaveText
    {
        static readonly System.Text.UTF8Encoding Strict = new System.Text.UTF8Encoding(false, true);

        static bool Unreserved(byte b) => (b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9') || b == '-' || b == '.' || b == '_' || b == '~';

        /// <summary>False for a string that is not valid Unicode text (a lone surrogate), which cannot be saved losslessly.</summary>
        public static bool IsEncodable(string s)
        {
            try { Strict.GetBytes(s); return true; }
            catch (System.Text.EncoderFallbackException) { return false; }
        }

        public static string Escape(string s)
        {
            var sb = new StringBuilder();
            foreach (var b in Strict.GetBytes(s))
            {
                if (Unreserved(b)) sb.Append((char)b);
                else sb.Append('%').Append("0123456789ABCDEF"[b >> 4]).Append("0123456789ABCDEF"[b & 15]);
            }
            return sb.ToString();
        }

        /// <summary>Null when the text has a malformed escape or is not valid UTF-8.</summary>
        public static string? Unescape(string s)
        {
            var bytes = new List<byte>();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '%') { if (s[i] > 127) return null; bytes.Add((byte)s[i]); continue; }
                if (i + 2 >= s.Length) return null;
                int hi = HexVal(s[i + 1]), lo = HexVal(s[i + 2]);
                if (hi < 0 || lo < 0) return null;
                bytes.Add((byte)(hi * 16 + lo));
                i += 2;
            }
            try { return Strict.GetString(bytes.ToArray()); }
            catch (System.Text.DecoderFallbackException) { return null; }
        }

        static int HexVal(char c) => c >= '0' && c <= '9' ? c - '0' : c >= 'A' && c <= 'F' ? c - 'A' + 10 : c >= 'a' && c <= 'f' ? c - 'a' + 10 : -1;
    }
}
