using System;
using System.Globalization;
using System.Linq;

namespace StoryOfTheLand.Sim
{
    /// <summary>Player-facing wording shared by the sim (Cause text, refusal reasons) and the UI: the one date format
    /// ("15 May 1948"), the display names content gives its ids, the family's money, and the one scale for resources
    /// (0-100, as the top bar shows them, written with "%"). No player-facing string shows a content id, an ISO date or a
    /// bare number (spec: player-facing text; playtest 1 items 4, 5 and 7). An id content does not name is an error, never
    /// shown as it is.</summary>
    public static class PlayerText
    {
        /// <summary>The player-facing reason for a <see cref="GameState.GameOver"/> code (see <see cref="GameOverCodes"/>):
        /// tests and goldens compare the code, the player reads this.</summary>
        public static string GameOver(string code) => code switch
        {
            GameOverCodes.EraEnded => "The chapter has ended",
            _ => throw Unknown("game over reason", code),
        };

        /// <summary>"15 May 1948".</summary>
        public static string Date(DateTime d) => d.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

        /// <summary>"May 1948".</summary>
        public static string Month(DateTime d) => d.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        /// <summary>The Levantine month names the chapters' Arabic uses (audits/arabic_*.md), January first.</summary>
        public static readonly string[] LevantineMonths =
        {
            "كانون الثاني", "شباط", "آذار", "نيسان", "أيار", "حزيران",
            "تموز", "آب", "أيلول", "تشرين الأول", "تشرين الثاني", "كانون الأول",
        };

        /// <summary>"15 أيار 1948" in Arabic (Western digits, as the chapters write them), else Date.</summary>
        public static string Date(DateTime d, bool arabic) =>
            arabic ? $"{d.Day.ToString(CultureInfo.InvariantCulture)} {LevantineMonths[d.Month - 1]} {d.Year.ToString(CultureInfo.InvariantCulture)}" : Date(d);

        /// <summary>"أيار 1948" in Arabic, else Month.</summary>
        public static string Month(DateTime d, bool arabic) =>
            arabic ? $"{LevantineMonths[d.Month - 1]} {d.Year.ToString(CultureInfo.InvariantCulture)}" : Month(d);

        static InvalidOperationException Unknown(string what, string id) =>
            new InvalidOperationException($"no display name for {what} '{id}' (content validation should have caught this)");

        /// <summary>The display name of what a cause changes: a resource (model.yaml names).</summary>
        public static string Target(Content c, string id) =>
            c.Indicators.FirstOrDefault(i => i.Id == id)?.Name ?? throw Unknown("indicator", id);

        /// <summary>The phrase with a capital first letter, to start a sentence ("A transit camp").</summary>
        public static string Capitalised(string phrase) =>
            phrase.Length == 0 ? phrase : char.ToUpperInvariant(phrase[0]) + phrase.Substring(1);

        static double Finite(double v, string name) =>
            double.IsNaN(v) || double.IsInfinity(v) ? throw new ArgumentOutOfRangeException(name, v, "not a finite number (a programmer error)") : v;

        static string Fixed(double abs, int decimals) => abs.ToString(decimals == 0 ? "0" : "0.0", CultureInfo.InvariantCulture);

        /// <summary>The one sign style: the sign goes before the number, and a value that rounds to zero prints unsigned.
        /// <paramref name="signed"/> adds "+" to positive values (changes).</summary>
        static string Signed(double rounded, string prefix, string body, bool signed) =>
            rounded == 0 ? prefix + body : (rounded < 0 ? "-" : signed ? "+" : "") + prefix + body;

        /// <summary>The family's money this turn, from the month and the village's region (palestine/currency.yaml):
        /// "Palestine pound, £P" or "الجنيه الفلسطيني، ج.ف". The sign is left out where it only repeats a word of the name,
        /// in either language: "الليرة العثمانية", not "الليرة العثمانية، ليرة". This is the family's only currency text.</summary>
        public static string Currency(Content c, GameState s, bool arabic = false)
        {
            var cur = Family.CurrencyAt(c, s) ?? throw new InvalidOperationException(
                $"no currency for '{s.Village}' in {Month(s.Date)} (content validation should have caught this)");
            var (name, symbol) = arabic ? (cur.NameAr, cur.SymbolAr) : (cur.Name, cur.Symbol);
            var repeats = name.Split(' ', '(', ')').Any(w => w == symbol || w == "ال" + symbol);
            return repeats ? name : name + (arabic ? "، " : ", ") + symbol;
        }

        /// <summary>The family's Livelihood line: "Livelihood 50% · Palestine pound, £P", or in Arabic, with the resource's
        /// Arabic name (palestine/model.yaml name_ar) and the number before its sign: "الرزق 50% · الجنيه الفلسطيني، ج.ف".</summary>
        public static string Livelihood(Content c, GameState s, bool arabic = false) =>
            (arabic ? IndicatorAr(c, "livelihood") : Target(c, "livelihood")) + " " + Percent(s.Indicators["livelihood"]) + " · " + Currency(c, s, arabic);

        /// <summary>The Arabic name of an indicator (model.yaml name_ar).</summary>
        public static string IndicatorAr(Content c, string id) =>
            c.Indicators.FirstOrDefault(i => i.Id == id)?.NameAr is { Length: > 0 } n ? n : throw Unknown("Arabic indicator", id);

        /// <summary>An indicator level (0..1) or a share on the scale the player sees: 0..100.</summary>
        public static double Scale(double level) => Finite(level, nameof(level)) * 100;

        /// <summary>The value the player sees for a level, in points: 0.626 shows as 63.</summary>
        public static double ShownPoints(double level) => Math.Round(Scale(level), 0, MidpointRounding.AwayFromZero);

        /// <summary>The change between two shown levels: the difference of the two shown (rounded) values, so it always
        /// agrees with them (0.626 -> 0.634 shows "63%" -> "63%" and no change; 0.624 -> 0.626 shows "+1%").</summary>
        public static string PercentChangeShown(double before, double now) => PercentChange((ShownPoints(now) - ShownPoints(before)) / 100, 0);

        /// <summary>"55%" for a level of 0.55; zero is "0%".</summary>
        public static string Percent(double level)
        {
            var r = ShownPoints(level);                               // the same rounding as PercentChangeShown
            return Signed(r, "", Fixed(Math.Abs(r), 0) + "%", signed: false);
        }

        /// <summary>"+4.1%" for a change of 0.041 (points on the 0-100 scale, <paramref name="decimals"/> 0 or 1); zero is
        /// "0.0%" (or "0%").</summary>
        public static string PercentChange(double change, int decimals = 1)
        {
            if (decimals != 0 && decimals != 1) throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "0 or 1");
            var r = Math.Round(Scale(change), decimals, MidpointRounding.AwayFromZero);
            return Signed(r, "", Fixed(Math.Abs(r), decimals) + "%", signed: true);
        }

        /// <summary>"Land +4.1%": the name and the change.</summary>
        public static string Change(Content c, string id, double change) => Target(c, id) + " " + PercentChange(change);
    }
}
