using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace StoryOfTheLand.Sim
{
    // Plain data classes filled by YamlDotNet (UnderscoredNamingConvention: TitleAr <- title_ar).

    public sealed class Source { public string Url { get; set; } = ""; public DateTime Checked { get; set; } }

    /// <summary>The shape of a sign-off's `reviewed_by`: a GitHub handle (letters and digits, single hyphens between them,
    /// at most 39 characters, and at least one letter, so a bare number such as 1234 reads the same in every reader), so never an email, an @-name, a display name or a bot account ("name[bot]"). Shared with
    /// the map's readers in Unity.</summary>
    public static class ReviewRecord
    {
        static readonly System.Text.RegularExpressions.Regex Handle =
            new System.Text.RegularExpressions.Regex(@"^(?=[A-Za-z0-9-]*[A-Za-z])[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}\z");

        public static bool IsHandle(string? s) => s != null && Handle.IsMatch(s);

        static readonly System.Text.RegularExpressions.Regex Day = new System.Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2}\z");

        /// <summary>Exactly YYYY-MM-DD, and a real day (no 30 February).</summary>
        public static bool IsDay(string? s) => s != null && Day.IsMatch(s)
            && DateTime.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _);

        /// <summary>What is wrong with a row's sign-off, as (field, message), or null when it is absent or whole: both keys or
        /// neither (a key that is present but blank counts as present), the handle's shape, and the day as YYYY-MM-DD.</summary>
        public static (string field, string message)? Problem(bool byGiven, string? by, bool onGiven, string? on)
        {
            if (!byGiven && !onGiven) return null;
            if (!onGiven) return ("reviewed_on", "required with reviewed_by: the day the person signed off (YYYY-MM-DD)");
            if (!byGiven) return ("reviewed_by", "required with reviewed_on: the reviewer's GitHub handle");
            if (!IsHandle(by)) return ("reviewed_by", "must be the reviewer's GitHub handle (letters, digits and single hyphens; no @, email or display name)");
            if (!IsDay(on))
                return ("reviewed_on", "must be the day of the sign-off, YYYY-MM-DD");
            return null;
        }
    }

    /// <summary>A chapter of the game: the half-open range [From, To). A chapter's content has one, and its To ends the game.</summary>
    public sealed class Era
    {
        public string Id { get; set; } = "";
        /// <summary>The chapter name the player sees.</summary>
        public string Name { get; set; } = "";
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }

    /// <summary>A family resource (palestine/model.yaml): a level from 0 to 1.</summary>
    public sealed class Indicator
    {
        public string Id { get; set; } = "";
        public double Base { get; set; }
        /// <summary>Display name shown to the player (Cause text); required.</summary>
        public string Name { get; set; } = "";
        /// <summary>The Arabic display name; required.</summary>
        public string NameAr { get; set; } = "";
    }

    public sealed class Rules
    {
        /// <summary>The chapter's first month: its first event's `from`.</summary>
        public DateTime Start { get; set; }
    }

    public sealed class OptionEffect { public string Target { get; set; } = ""; public double Delta { get; set; } }

    public sealed class EventOption
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        /// <summary>F4: the Arabic option text, and what happened to the family after it (English and Arabic).</summary>
        public string TextAr { get; set; } = "";
        public string Outcome { get; set; } = "";
        public string OutcomeAr { get; set; } = "";
        public List<OptionEffect> Effects { get; set; } = new List<OptionEffect>();
    }

    public sealed class GameEvent
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        /// <summary>F4 (docs/palestine-model.md): the Arabic title and text; empty when the event has none.</summary>
        public string TitleAr { get; set; } = "";
        public string TextAr { get; set; } = "";
        /// <summary>F4: whose family the event reaches: national, region:&lt;place&gt; or fate:&lt;fate&gt;. Empty means national.</summary>
        public string Scope { get; set; } = "";
        /// <summary>F4: the places.tsv id of where the event happened, or empty.</summary>
        public string Place { get; set; } = "";
        /// <summary>F4: the event's own effects, applied when it resolves, before the option's.</summary>
        public List<OptionEffect> Effects { get; set; } = new List<OptionEffect>();
        public List<string> Tags { get; set; } = new List<string>();
        public DateTime From { get; set; }
        public DateTime? To { get; set; }
        /// <summary>The day of the <see cref="From"/> month the event happened, where its text dates it; null for a month-wide
        /// event. A turn's cards come in date order (EventSystem.Trigger).</summary>
        public int? Day { get; set; }
        public List<EventOption> Options { get; set; } = new List<EventOption>();
        public List<Source> Sources { get; set; } = new List<Source>();
        /// <summary>draft until reviewed; a sensitive event needs sensitivity-reviewed before a shareable build (ReleaseGate).</summary>
        public string Status { get; set; } = "draft";
        /// <summary>The person who signed this row off: a GitHub handle, written only by that person (owner, 15 October 2026,
        /// #375: "Yes, humans only"; scripts/oss/reviewed_by_guard.py). Empty when no one has; with <see cref="ReviewedOn"/>.</summary>
        public string ReviewedBy { get => reviewedBy ?? ""; set => reviewedBy = value ?? ""; }
        /// <summary>True when the file has the key at all, even blank: a blank sign-off is an error.</summary>
        [YamlIgnore] public bool ReviewedByGiven => reviewedBy != null;
        string? reviewedBy;
        /// <summary>The day of the sign-off, YYYY-MM-DD, as the file writes it (read as text, so the loader checks the same
        /// shape as tools/review_record.py and the map's readers); set exactly when <see cref="ReviewedBy"/> is.</summary>
        public string ReviewedOn { get => reviewedOn ?? ""; set { reviewedOn = value ?? ""; ReviewedOnGiven = true; } }
        /// <summary>True when the file has the key at all, even blank.</summary>
        [YamlIgnore] public bool ReviewedOnGiven { get; private set; }
        string? reviewedOn;
        /// <summary>Path relative to content/palestine/, set by the loader (not read from YAML).</summary>
        [YamlIgnore] public string File { get; set; } = "";
    }

    /// <summary>One hex of content/map/hexes.json (written by tools/hexgrid.py; axial q, r, pointy-top). MapOverrides checks
    /// its overrides against them.</summary>
    public sealed class MapHex
    {
        public int Q { get; set; }
        public int R { get; set; }
        public double Lon { get; set; }
        public double Lat { get; set; }
        public double ElevM { get; set; }
        public bool Land { get; set; }
        public string Biome { get; set; } = "";
    }

    /// <summary>One chapter of The Story of the Land, as ContentLoader.LoadChapter reads it.</summary>
    public sealed class Content
    {
        /// <summary>The chapter as one era (LoadChapter makes it from the events' windows).</summary>
        public List<Era> Eras = new List<Era>();
        /// <summary>The ruleset a save names (SaveGame.Ruleset).</summary>
        public string Ruleset => SaveGame.FamilyRuleset;
        /// <summary>The day play ends, a boundary the last turn reaches: the chapter's end.</summary>
        public DateTime End => Eras[Eras.Count - 1].To;
        public List<Indicator> Indicators = new List<Indicator>();
        public Rules Rules = new Rules();
        public List<GameEvent> Events = new List<GameEvent>();
        /// <summary>F5: the places a family can live in (palestine/places.yaml), and their sourced fates in file order
        /// (palestine/fates.yaml).</summary>
        public List<Place> Places = new List<Place>();
        public List<VillageFate> Fates = new List<VillageFate>();
        /// <summary>The money families used, by period and region, and the places outside the 1949 lines
        /// (palestine/currency.yaml; <see cref="Family.CurrencyAt"/>).</summary>
        public List<Currency> Currencies = new List<Currency>();
        public List<PlaceRegion> PlaceRegions = new List<PlaceRegion>();
        /// <summary>SHA-256 (lowercase hex) over every file the loader read, in read order, with CRLF read as LF. A save
        /// records it; loading a save made with other content shows a warning (spec section 7).</summary>
        public string Hash = "";
    }

    public sealed class ContentException : Exception
    {
        public string File { get; }
        public string Field { get; }
        public ContentException(string file, string field, string message) : base($"{file}: {field}: {message}")
        { File = file; Field = field; }
    }
}
