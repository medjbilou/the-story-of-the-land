using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace StoryOfTheLand.Sim
{
    /// <summary>A place a family can live in (palestine/places.yaml, written by tools/places_to_yaml.py).</summary>
    public sealed class Place
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string NameAr { get; set; } = "";
        public double Lon { get; set; }
        public double Lat { get; set; }
        public string Type { get; set; } = "";
        /// <summary>Village Statistics 1945; 0 = not recorded.</summary>
        [YamlMember(Alias = "population_1945")] public int Population1945 { get; set; }
        /// <summary>How a key place's figure was read (key_places.tsv); empty for the others.</summary>
        public string PopulationNote { get; set; } = "";
        public bool HasToday { get; set; }
        public List<string> Sources { get; set; } = new List<string>();
    }

    /// <summary>One sourced state of a place (palestine/fates.yaml, from village_fates.tsv), such as "depopulated in May 1948".</summary>
    public sealed class VillageFate
    {
        public string Place { get; set; } = "";
        /// <summary>inhabited, depopulated, destroyed or built_over.</summary>
        public string Fate { get; set; } = "";
        /// <summary>Null when the sources give no date: the fate is kept (a village's panel shows it) and never fires.</summary>
        public int? Year { get; set; }
        /// <summary>Null when only the year is known: the fate then counts from December, so it never comes earlier than
        /// the source supports (the prototype's rule).</summary>
        public int? Month { get; set; }
        public string Operation { get; set; } = "";
        public string Cause { get; set; } = "";
        public string Today { get; set; } = "";
        public string Confidence { get; set; } = "";
        public List<string> Sources { get; set; } = new List<string>();

        /// <summary>True when this fate happens in the month that starts on <paramref name="date"/>.</summary>
        public bool In(DateTime date) => Year == date.Year && (Month ?? 12) == date.Month;
    }

    /// <summary>The money Palestinian families used in one region for one period (palestine/currency.yaml, from
    /// content/currency.tsv). A currency the occupation imposed says so in its name.</summary>
    public sealed class Currency
    {
        public string Id { get; set; } = "";
        public int FromYear { get; set; }
        public int FromMonth { get; set; }
        /// <summary>The first month it no longer applies; null: still in use.</summary>
        public int? ToYear { get; set; }
        public int? ToMonth { get; set; }
        /// <summary>national (before the 1948 split), inside_1948, west_bank or gaza.</summary>
        public string Region { get; set; } = "";
        public string Name { get; set; } = "";
        public string NameAr { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string SymbolAr { get; set; } = "";
        public string Subunit { get; set; } = "";
        public string SubunitAr { get; set; } = "";
        public string Note { get; set; } = "";
        public List<string> Sources { get; set; } = new List<string>();

        public DateTime From => new DateTime(FromYear, FromMonth, 1);
        public DateTime? To => ToYear.HasValue && ToMonth.HasValue ? new DateTime(ToYear.Value, ToMonth.Value, 1) : (DateTime?)null;
        public bool In(DateTime date) => date >= From && (To == null || date < To);
    }

    /// <summary>A place outside the 1949 lines and its region (west_bank or gaza); every other place is inside_1948.</summary>
    public sealed class PlaceRegion
    {
        public string Place { get; set; } = "";
        public string Region { get; set; } = "";
        public string Note { get; set; } = "";
        public List<string> Sources { get; set; } = new List<string>();
    }

    /// <summary>F5 (docs/palestine-model.md): the family's game. Which events reach the family, and its monthly turn. A ported
    /// version of the prototype's Campaign (unity/Assets/Sim/Campaign.cs at the project root).</summary>
    public static class Family
    {
        public const double RegionKm = 25;
        // A flat approximation at about 31.7°N, as in the prototype: no trigonometry, so every runtime gets the same answer.
        const double KmPerDegLat = 111.2, KmPerDegLon = 94.6;
        public const string VillageEn = "village_en", VillageAr = "village_ar";

        /// <summary>National events reach every family. A region:&lt;place&gt; event reaches a family whose village is within
        /// <see cref="RegionKm"/> of that place. A fate:&lt;fate&gt; template reaches it in the month its own village has a
        /// dated fate of that kind. An event with no scope reaches everyone.</summary>
        public static bool Reaches(Content c, GameState s, GameEvent e)
        {
            if (e.Scope.StartsWith("region:", StringComparison.Ordinal)) return Near(c, s.Village, e.Scope.Substring(7));
            if (e.Scope.StartsWith("fate:", StringComparison.Ordinal))
            {
                var fate = e.Scope.Substring(5);
                return c.Fates.Any(f => f.Place == s.Village && f.Fate == fate && f.In(s.Date));
            }
            return true;
        }

        /// <summary>True when both places are known and at most <see cref="RegionKm"/> apart.</summary>
        public static bool Near(Content c, string village, string place)
        {
            var a = c.Places.FirstOrDefault(p => p.Id == village);
            var b = c.Places.FirstOrDefault(p => p.Id == place);
            if (a == null || b == null) return false;
            double dx = (b.Lon - a.Lon) * KmPerDegLon, dy = (b.Lat - a.Lat) * KmPerDegLat;
            return dx * dx + dy * dy <= RegionKm * RegionKm;
        }

        /// <summary>The placeholder shape: {village_en} or {village_ar}, the only ones a chapter has (EventRules).</summary>
        internal static readonly Regex Placeholder = new Regex(@"\{([a-z0-9_]+)\}");

        /// <summary><paramref name="text"/> with {village_en} or {village_ar} replaced by the family's village's name. The loader
        /// allows them only in fate:* events, so only a fate card is filled; a placeholder with no village stays as it is.</summary>
        public static string Fill(Content c, GameState s, string text) => Placeholder.Replace(text, m =>
        {
            var p = c.Places.FirstOrDefault(x => x.Id == s.Village);
            var key = m.Groups[1].Value;
            return p == null || (key != VillageEn && key != VillageAr) ? m.Value : key == VillageEn ? p.Name : p.NameAr;
        });

        public const string Inside1948 = "inside_1948", National = "national";
        public static readonly string[] Regions = { Inside1948, "west_bank", "gaza" };

        /// <summary>The village's region after 1948: west_bank or gaza when currency.yaml lists it, else inside_1948.</summary>
        public static string Region(Content c, string village) =>
            c.PlaceRegions.FirstOrDefault(r => r.Place == village)?.Region ?? Inside1948;

        /// <summary>The money a family of <paramref name="village"/> used in the month that starts on <paramref name="date"/>:
        /// its region's row, or a national one (before the 1948 split). The loader checks that exactly one covers every month
        /// of the chapter in every region; null outside them (a date no chapter reaches).</summary>
        public static Currency? CurrencyAt(Content c, string village, DateTime date)
        {
            var region = Region(c, village);
            return c.Currencies.FirstOrDefault(x => (x.Region == region || x.Region == National) && x.In(date));
        }

        /// <summary>The family's money this turn.</summary>
        public static Currency? CurrencyAt(Content c, GameState s) => CurrencyAt(c, s.Village, s.Date);

        public static GameState NewGame(Content c, ulong seed, string village)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (c.Places.All(p => p.Id != village)) throw new ArgumentException($"unknown village '{village}'", nameof(village));
            var s = new GameState { Date = c.Rules.Start, RngState = seed, Village = village };
            foreach (var i in c.Indicators) { s.Indicators[i.Id] = i.Base; s.IndicatorShifts[i.Id] = 0.0; }   // the family resources at their base
            EventSystem.Trigger(c, s, new List<Cause>());
            return s;
        }

        /// <summary>One month of a chapter, on the clone Simulation.Step made. The family answers its cards; each card it
        /// left unanswered still happens (its own effects apply) with no choice. Then the month ends and the next one's
        /// events arrive. No RNG draw.</summary>
        internal static void Step(Content c, GameState s, IReadOnlyList<Command> commands, List<Cause> why, List<Rejection> rejected)
        {
            foreach (ChooseEventOption cmd in commands)   // the only order (CommandText.Validate)
            {
                var error = EventSystem.Choose(c, s, cmd.EventId, cmd.OptionId, why);
                if (error != null) rejected.Add(new Rejection(cmd, error));
            }
            EventSystem.LetPass(c, s, why);
            var turnStart = s.Date;
            // A month, except that the turn which would reach the chapter's end, or leave under 14 days before it, ends on it.
            var next = s.Date.AddMonths(1);
            s.Date = next >= c.End || (c.End - next).TotalDays < 14 ? c.End : next;
            s.Turn++;
            if (s.Date >= c.End)
            {
                s.GameOver = GameOverCodes.EraEnded;
                why.Add(new Cause("era", s.GameOver, 0, PlayerText.GameOver(s.GameOver)));
                return;
            }
            EventSystem.Trigger(c, s, why, turnStart);
        }
    }
}
