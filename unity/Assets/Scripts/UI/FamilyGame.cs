#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StoryOfTheLand.Sim;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>F5 (docs/f5-unity-integration.md): what the family game's screens share. The seven chapters with their years
    /// (titles: the strings chapter.&lt;id&gt;), the places a family can pick and their regions, the village search, the
    /// family save's chapter line, the text of a family card in the UI's language, and the family's Why lines. No rule of the
    /// game lives here: the sim decides what happens (Family, EventSystem), this only says it.</summary>
    public static class FamilyGame
    {
        /// <summary>The chapters in time order (content/palestine/events/&lt;id&gt;.yaml), with the first and last year of each
        /// (the years in its file name, which its own header comment gives).</summary>
        public static readonly IReadOnlyList<(string Id, int From, int To)> Chapters = new[]
        {
            ("1880_1946", 1880, 1946), ("1947_1949", 1947, 1949), ("1950_1966", 1950, 1966), ("1967_1987", 1967, 1987),
            ("1987_2000", 1987, 2000), ("2000_2023", 2000, 2023), ("2023_2026", 2023, 2026),
        };

        /// <summary>The chapter a family game plays: LoadChapter makes the chapter its one era, named by its id.</summary>
        public static string ChapterOf(Content c) => c.Eras.Count > 0 ? c.Eras[0].Id : "";

        /// <summary>Whether the UI is Arabic (the strings' ui.lang).</summary>
        public static bool Arabic(UiText t) => t["ui.lang"] == UiLanguage.Arabic;

        /// <summary>A chapter's years in the UI's language: "1947–1949", "من 1947 إلى 1949".</summary>
        public static string Years(UiText t, int from, int to) => t.Format("chapter.years", from, to);

        // ---- The places ---------------------------------------------------------------------------------------------

        /// <summary>The region names of the picker's groups, in their order (Family.Regions: inside the 1948 lines, the West
        /// Bank, Gaza).</summary>
        public static IReadOnlyList<string> Regions => Family.Regions;

        /// <summary>The place types of the Palestinian towns and villages a family can live in. places.yaml also holds the
        /// Jewish towns, colonies and kibbutzim of 1945 (types town, kibbutz, moshav), which are not.</summary>
        static readonly HashSet<string> FamilyTypes = new HashSet<string> { "arab_town", "village" };
        /// <summary>The cities and towns of mixed population in 1945 whose Palestinian families the chapters follow too
        /// (places.yaml gives them the type city or town). ponytail: a hand list of four ids; a "mixed" flag in places.tsv
        /// would replace it once the prototype's map file carries one.</summary>
        static readonly HashSet<string> MixedPlaces = new HashSet<string> { "jerusalem", "haifa", "tiberias", "safed" };

        /// <summary>Whether a family can come from <paramref name="p"/>: a Palestinian town or village, or a mixed city.</summary>
        public static bool FamilyPlace(Place p) => FamilyTypes.Contains(p.Type) || MixedPlaces.Contains(p.Id);

        /// <summary>A place's name in the UI's language: name_ar in Arabic (its English name only where it has none).</summary>
        public static string Name(Place p, bool arabic) => arabic && p.NameAr.Length > 0 ? StripMarks(p.NameAr) : p.Name;

        /// <summary>The places a family can pick, by region in <see cref="Regions"/> order, each group in alphabetical order of
        /// its name in the UI's language: the search key's letters (Fold: an Arabic name by its letters with أ إ آ as ا, the
        /// article included, as an Arabic index lists them), in code-point order, which is the Arabic alphabet's and, case
        /// folded, the Latin one's. No culture is asked for: the web player has none.</summary>
        public static IReadOnlyList<(string Region, IReadOnlyList<Place> Places)> PlacesByRegion(Content c, bool arabic) =>
            Regions.Select(r => (r, (IReadOnlyList<Place>)c.Places.Where(p => FamilyPlace(p) && Family.Region(c, p.Id) == r)
                .OrderBy(p => Fold(Name(p, arabic)), StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal).ToList())).ToList();

        /// <summary>Each family place's name on the picker: its name in the UI's language, and where two places share that name
        /// (three villages called الطيرة), the nearest town after it, "الطيرة (قرب حيفا)", so a family can tell its own. The
        /// nearest town is the nearest arab_town or city by the sim's flat measure (Family.Near's).</summary>
        public static Dictionary<string, string> PickerNames(Content c, bool arabic, UiText t)
        {
            var places = c.Places.Where(FamilyPlace).ToList();
            var towns = c.Places.Where(p => p.Type == "arab_town" || p.Type == "city").ToList();
            var shared = new HashSet<string>(places.GroupBy(p => Name(p, arabic)).Where(g => g.Count() > 1).Select(g => g.Key));
            double Km2(Place a, Place b) { double dx = (a.Lon - b.Lon) * 94.6, dy = (a.Lat - b.Lat) * 111.2; return dx * dx + dy * dy; }
            return places.ToDictionary(p => p.Id, p =>
            {
                var name = Name(p, arabic);
                if (!shared.Contains(name)) return name;
                // The nearest town no other place is named after.
                var town = towns.Where(x => x.Id != p.Id && !shared.Contains(Name(x, arabic)))
                    .OrderBy(x => Km2(p, x)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                return town == null ? name : t.Format("picker.near", name, Name(town, arabic));
            });
        }

        /// <summary>Whether a place's name in either language contains <paramref name="query"/>, compared loosely: case and
        /// Arabic diacritics left out, and the letters a searcher may type for one another made one (أ إ آ ٱ → ا, ة → ه,
        /// ى → ي), so "عمواس" finds عِمواس and "tantura" finds Tantura. An empty query matches every place.</summary>
        public static bool Matches(Place p, string query)
        {
            var q = Fold(query);
            return q.Length == 0 || Fold(p.Name).Contains(q) || Fold(p.NameAr).Contains(q);
        }

        /// <summary>The search key of a text: lower case, no Arabic diacritics, tatweel or direction marks, the variant
        /// letters folded (<see cref="Matches"/>), spaces trimmed.</summary>
        public static string Fold(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var b = new StringBuilder(s!.Length);
            foreach (var ch in s.Trim().ToLowerInvariant())
            {
                if (ch >= 'ً' && ch <= 'ٟ' || ch == 'ٰ' || ch == 'ـ' || ch == '‎' || ch == '‏') continue;
                b.Append(ch switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ة' => 'ه', 'ى' => 'ي', _ => ch });
            }
            return b.ToString();
        }

        /// <summary>A name without the direction marks the prototype's map file left in some Arabic names (Imwas's U+200F
        /// and U+200E, docs/palestine-model.md): the UI sets each text's direction itself (UiDirection).</summary>
        public static string StripMarks(string s) => s.Replace("‎", "").Replace("‏", "");

        // ---- The save -----------------------------------------------------------------------------------------------

        /// <summary>The family save's first line names its chapter: a SaveGame does not, and only that chapter's content
        /// replays it (its content hash and village).</summary>
        public const string ChapterLine = "chapter ";

        /// <summary>The text the family autosave keeps: the chapter line, then the sim's save.</summary>
        public static string SaveText(GameSession s) => ChapterLine + ChapterOf(s.Content) + "\n" + s.Save.Write();

        /// <summary>Splits a family save into its chapter and the sim's save text; throws SaveException when the chapter line
        /// is missing or names no chapter.</summary>
        public static (string Chapter, string Save) SplitSave(string text)
        {
            var end = text.IndexOf('\n');
            var first = end < 0 ? text : text.Substring(0, end).TrimEnd('\r');
            var chapter = first.StartsWith(ChapterLine, StringComparison.Ordinal) ? first.Substring(ChapterLine.Length) : "";
            if (end < 0 || Chapters.All(x => x.Id != chapter)) throw new SaveException(1, "the family save does not name a chapter");
            return (chapter, text.Substring(end + 1));
        }

        // ---- A card's text ------------------------------------------------------------------------------------------

        /// <summary>An event's title in the UI's language (title_ar in Arabic), filled for this family (Family.Fill:
        /// {village_ar} / {village_en}).</summary>
        public static string Title(Content c, GameState s, GameEvent e, bool arabic) =>
            Fill(c, s, arabic && e.TitleAr.Length > 0 ? e.TitleAr : e.Title);

        /// <summary>An event's text in the UI's language (text_ar in Arabic), filled as <see cref="Title"/>.</summary>
        public static string Text(Content c, GameState s, GameEvent e, bool arabic) =>
            Fill(c, s, arabic && e.TextAr.Length > 0 ? e.TextAr : e.Text);

        /// <summary>A choice's label in the UI's language, filled.</summary>
        public static string Label(Content c, GameState s, GameEvent e, EventOption o, bool arabic) =>
            Fill(c, s, arabic && o.TextAr.Length > 0 ? o.TextAr : o.Text);

        /// <summary>What followed a choice, in the UI's language (outcome_ar / outcome), filled; "" when the choice has none.</summary>
        public static string Outcome(Content c, GameState s, GameEvent e, EventOption o, bool arabic) =>
            Fill(c, s, arabic && o.OutcomeAr.Length > 0 ? o.OutcomeAr : o.Outcome);

        static string Fill(Content c, GameState s, string text) =>
            text.Length == 0 ? "" : StripMarks(Family.Fill(c, s, text));

        /// <summary>Whether a card opens with the content note (docs/CONTENT_POLICY.md rule 9): its tag "sensitive".</summary>
        public static bool Sensitive(GameEvent e) => e.Tags.Contains("sensitive");

        /// <summary>A source link's short text: the address's host without "www." ("palquest.org", "ar.wikipedia.org").</summary>
        public static string Host(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) ? (u.Host.StartsWith("www.", StringComparison.Ordinal) ? u.Host.Substring(4) : u.Host) : url;

        /// <summary>A resource's name in the UI's language (model.yaml name / name_ar).</summary>
        public static string Resource(Content c, string id, bool arabic) =>
            arabic ? PlayerText.IndicatorAr(c, id) : PlayerText.Target(c, id);

        // ---- The map ------------------------------------------------------------------------------------------------

        /// <summary>F5 step 6: each place's state on the map at <paramref name="date"/>, in the UI's words (map.fate.*): its last
        /// dated fate that has come by then (a fate with a year and no month counts from December, as the sim fires it) when
        /// that fate is depopulated, destroyed or built over. An undated fate never shows, as it never fires.</summary>
        public static Dictionary<string, string> Fates(Content c, DateTime date, UiText t)
        {
            var shown = new Dictionary<string, string>();
            foreach (var f in c.Fates)   // in time order per place (fates.yaml), so a later fate replaces an earlier one
            {
                if (!f.Year.HasValue || new DateTime(f.Year.Value, f.Month ?? 12, 1) > date) continue;
                if (f.Fate == "inhabited") shown.Remove(f.Place);
                else shown[f.Place] = t["map.fate." + f.Fate];
            }
            return shown;
        }

        // ---- Why ----------------------------------------------------------------------------------------------------

        /// <summary>One group of the family's Why screen: a heading and its lines.</summary>
        public readonly struct WhyGroup
        {
            public readonly string Heading; public readonly IReadOnlyList<string> Lines;
            public WhyGroup(string heading, IReadOnlyList<string> lines) { Heading = heading; Lines = lines; }
        }

        /// <summary>The family's Why screen in the UI's language, from the sim's own causes for the turn (StepResult.Why),
        /// read by their structure and not their English messages: each card the month resolved (a cause whose target is
        /// the event, "chosen" or "unanswered", EventSystem.Resolve) with what was chosen and each resource change after it;
        /// then the cards the new month brought ("New: ...", EventSystem.Trigger); then the chapter's end. The state before
        /// the turn names the cards as they read then.</summary>
        /// <summary>Playtest 3: what the choices made this turn moved resource <paramref name="id"/> by, as applied (clamped):
        /// the chosen options' own effects, which the sim applies after the event's own (EventSystem.Resolve), so a card's
        /// first Effects.Count changes are the event's. The rest of the turn's change came from elsewhere.</summary>
        public static double FromChoices(Content c, GameState after, IReadOnlyList<Cause> why, string id)
        {
            var events = c.Events.ToDictionary(e => e.Id);
            double sum = 0; int own = 0; bool chosen = false;
            foreach (var w in why)
            {
                if (w.System != "events") { chosen = false; continue; }
                if (events.TryGetValue(w.Target, out var e))
                {
                    chosen = !w.Message.StartsWith("New:", StringComparison.Ordinal) && after.ChosenOptions.TryGetValue(e.Id, out var o) && o.Length > 0;
                    own = chosen ? e.Effects.Count : 0;
                    continue;
                }
                if (own > 0) { own--; continue; }
                if (chosen && w.Target == id) sum += w.Delta;
            }
            return sum;
        }

        public static IReadOnlyList<WhyGroup> Why(Content c, GameState before, GameState after, IReadOnlyList<Cause> why, UiText t)
        {
            bool arabic = Arabic(t);
            var events = c.Events.ToDictionary(e => e.Id);
            var resources = new HashSet<string>(c.Indicators.Select(i => i.Id));
            var groups = new List<WhyGroup>();
            var fresh = new List<string>();
            string? heading = null; List<string>? lines = null;
            void Close() { if (heading != null) groups.Add(new WhyGroup(heading, lines!)); heading = null; lines = null; }
            foreach (var w in why)
            {
                if (w.System == "events" && events.TryGetValue(w.Target, out var e))
                {
                    if (w.Message.StartsWith("New:", StringComparison.Ordinal)) { fresh.Add(Title(c, after, e, arabic)); continue; }
                    Close();
                    heading = Title(c, before, e, arabic);
                    var chosen = after.ChosenOptions.TryGetValue(e.Id, out var id) ? e.Options.FirstOrDefault(o => o.Id == id) : null;
                    lines = new List<string> { chosen == null ? t["why.family.unanswered"] : t.Format("event.you_chose", Label(c, before, e, chosen, arabic)) };
                }
                // Audit m4: in whole points, as the bar and the chips say them ("-5%", not "-5.0%"), and no line for a change
                // that rounds to nothing.
                else if (w.System == "events" && resources.Contains(w.Target) && lines != null && Math.Abs(w.Delta) >= 0.005)   // half a point: what rounds to a whole one
                    lines.Add(t.Format("why.family.change", Resource(c, w.Target, arabic), PlayerText.PercentChange(w.Delta, 0)));
            }
            Close();
            if (fresh.Count > 0) groups.Add(new WhyGroup(t["why.family.new"], fresh));
            if (after.GameOver != null) groups.Add(new WhyGroup(t["why.family.ended"], Array.Empty<string>()));
            return groups;
        }
    }
}
