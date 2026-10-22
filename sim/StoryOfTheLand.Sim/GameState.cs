using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace StoryOfTheLand.Sim
{
    /// <summary>The stable codes <see cref="GameState.GameOver"/> can hold (they are in the state hash).</summary>
    public static class GameOverCodes
    {
        /// <summary>The chapter's end was reached.</summary>
        public const string EraEnded = "era_ended";
    }

    /// <summary>All mutable game data. Systems never share references between states: Step works on Clone().</summary>
    public sealed class GameState
    {
        public DateTime Date;
        /// <summary>The seed; the family game draws nothing, so it never changes.</summary>
        public ulong RngState;
        public int Turn;
        public Dictionary<string, double> Indicators = new Dictionary<string, double>();
        /// <summary>Permanent per-indicator offsets from event choices, added on top of base.</summary>
        public Dictionary<string, double> IndicatorShifts = new Dictionary<string, double>();
        public SortedSet<string> FiredEvents = new SortedSet<string>(StringComparer.Ordinal);
        public List<string> PendingEvents = new List<string>();
        /// <summary>The option each resolved event was resolved with ("" when it went unanswered).</summary>
        public Dictionary<string, string> ChosenOptions = new Dictionary<string, string>();
        /// <summary>Why the game ended, as one of the <see cref="GameOverCodes"/>; null while it is on. Never display it: use PlayerText.GameOver.</summary>
        public string? GameOver;
        /// <summary>F5: the family's village (a palestine/places.yaml id).</summary>
        public string Village = "";

        public GameState Clone()
        {
            var c = (GameState)MemberwiseClone();
            c.Indicators = new Dictionary<string, double>(Indicators);
            c.IndicatorShifts = new Dictionary<string, double>(IndicatorShifts);
            c.FiredEvents = new SortedSet<string>(FiredEvents, StringComparer.Ordinal);
            c.PendingEvents = new List<string>(PendingEvents);
            c.ChosenOptions = new Dictionary<string, string>(ChosenOptions);
            return c;
        }

        /// <summary>SHA-256 over a canonical text form (ordinal-sorted keys, doubles as IEEE bit patterns with -0.0
        /// written as 0.0), so hashes match across .NET, Mono and IL2CPP.</summary>
        public string Hash()
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(CanonicalText(false)));
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }

        /// <summary>The state in the order and form <see cref="Hash"/> hashes it, one line per section, with each number
        /// also written as a plain value (`name=1.5 [3FF8000000000000]`). For humans: a golden-hash failure diffs this
        /// (issue #256). It shares one writer with <see cref="Hash"/>, so the two cannot list different fields.</summary>
        public string Describe() => CanonicalText(true);

        string CanonicalText(bool readable)
        {
            // Hash text: one line, ';'-separated, closing brackets. Readable text: a section label per line, each entry
            // on its own indented line, no closing brackets. Both are written by the calls below, in the same order.
            var sb = new StringBuilder();
            void Nl() { if (readable) sb.Append('\n'); }
            void Entry() { if (readable) sb.Append("\n  "); }
            void Close(char c) { if (!readable) sb.Append(c); }
            void Num(string k, double v, bool inMap = false)
            {
                var bits = BitConverter.DoubleToInt64Bits(v == 0.0 ? 0.0 : v).ToString("X16", CultureInfo.InvariantCulture);
                if (readable) sb.Append(inMap ? "\n  " : "\n").Append(k).Append('=').Append((v == 0.0 ? 0.0 : v).ToString("R", CultureInfo.InvariantCulture)).Append(" [").Append(bits).Append(']');
                else sb.Append(k).Append('=').Append(bits).Append(';');
            }
            void Map(string name, Dictionary<string, double> m)
            {
                Nl(); sb.Append(name).Append('{');
                foreach (var kv in m.OrderBy(k => k.Key, StringComparer.Ordinal)) Num(kv.Key, kv.Value, true);
                Close('}');
            }
            void List(string name, IEnumerable<string> items)
            {
                Nl(); sb.Append(name).Append('[');
                if (readable) foreach (var i in items) { Entry(); sb.Append(i); }
                else sb.Append(string.Join(",", items));
                Close(']');
            }
            void Sep() { if (!readable) sb.Append(';'); }
            // The fixed fields (pol, eff, bud, pop and the others below) stay in the text at constant values, so the hashes of
            // the goldens and of existing saves stay stable.
            var none = new Dictionary<string, double>();
            sb.Append(Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(';')
              .Append(RngState).Append(';').Append("Historical").Append(';').Append(Turn).Append(';');
            Map("ind", Indicators); Map("pol", none); Map("eff", none); Map("shift", IndicatorShifts);
            Map("bud", none); Map("pop", none);
            List("sit", Array.Empty<string>());
            Num("treasury", 0); Num("debt", 0); Num("fx", 0);
            Nl(); sb.Append("rung=1;cf=0;");
            Num("mob", 0); Num("stab", 1.0); Num("att", 0);
            List("fired", FiredEvents);
            List("pending", PendingEvents);
            Nl(); sb.Append("chosen{");
            foreach (var kv in ChosenOptions.OrderBy(k => k.Key, StringComparer.Ordinal)) { Entry(); sb.Append(kv.Key).Append('=').Append(kv.Value); Sep(); }
            Close('}');
            Nl(); sb.Append("appt{"); Close('}');
            Nl(); sb.Append("under=;");
            Nl(); sb.Append("map["); Close(']');
            Nl(); sb.Append("mapturn=0001-01-01:0;");
            Nl(); sb.Append("over=").Append(GameOver ?? "");
            if (Village != "") { Sep(); Nl(); sb.Append("village=").Append(Village); }
            return sb.ToString();
        }
    }
}
