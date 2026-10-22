using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StoryOfTheLand.Sim
{
    /// <summary>Load-time checks for a chapter's events.</summary>
    static class EventRules
    {
        public static readonly HashSet<string> SensitiveTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "holocaust", "terror", "massacre", "war-casualties", "displacement", "sensitive" };
        /// <summary>F4 (docs/palestine-model.md): an event's scope, and the placeholders a fate:* event fills from the family's village.</summary>
        static readonly Regex Scope = new Regex(@"^(national|region:[a-z0-9_]+|fate:(depopulated|destroyed|built_over))$");
        static readonly HashSet<string> VillagePlaceholders = new HashSet<string> { Family.VillageEn, Family.VillageAr };

        public static void Validate(Content c, HashSet<string> indicators)
        {
            var ids = new HashSet<string>();
            foreach (var e in c.Events)
            {
                string F(string field) => $"events[{e.Id}].{field}";
                if (!ContentLoader.Id.IsMatch(e.Id)) throw new ContentException(e.File, F("id"), ContentLoader.IdRule);
                if (!ids.Add(e.Id)) throw new ContentException(e.File, F("id"), "duplicate event id");
                ContentLoader.Status(e.File, F("status"), e.Status);
                ContentLoader.Review(e.File, $"events[{e.Id}]", e.ReviewedBy, e.ReviewedByGiven, e.ReviewedOn, e.ReviewedOnGiven);
                ContentLoader.Period(e.File, $"events[{e.Id}]", e.From, e.To);
                if (e.Day != null && (e.Day < 1 || e.Day > DateTime.DaysInMonth(e.From.Year, e.From.Month)))
                    throw new ContentException(e.File, F("day"), $"day {e.Day} is not a day of {e.From:yyyy-MM}");
                if (e.Scope != "" && !Scope.IsMatch(e.Scope)) throw new ContentException(e.File, F("scope"), $"unknown scope '{e.Scope}' (national, region:<place> or fate:depopulated|destroyed|built_over)");
                CheckPlaceholders(e, F("title"), e.Title);
                CheckPlaceholders(e, F("text"), e.Text);
                if (e.Options.Count == 0) throw new ContentException(e.File, F("options"), "at least one option required");
                void CheckEffect(string at, OptionEffect fx)
                {
                    if (!indicators.Contains(fx.Target)) throw new ContentException(e.File, F($"{at}.{fx.Target}"), "unknown target");
                    ContentLoader.Finite(e.File, F($"{at}.{fx.Target}.delta"), fx.Delta);
                }
                foreach (var fx in e.Effects) CheckEffect("effects", fx);
                var optionIds = new HashSet<string>();
                foreach (var o in e.Options)
                {
                    if (!ContentLoader.Id.IsMatch(o.Id)) throw new ContentException(e.File, F($"options[{o.Id}].id"), ContentLoader.IdRule);
                    if (!optionIds.Add(o.Id)) throw new ContentException(e.File, F($"options[{o.Id}].id"), "duplicate option id");
                    CheckPlaceholders(e, F($"options[{o.Id}].text"), o.Text);
                    foreach (var fx in o.Effects) CheckEffect($"options[{o.Id}].effects", fx);
                }
            }
        }

        /// <summary>The only placeholders are {village_en} and {village_ar}, and only in a fate:* event (Family.Fill).</summary>
        static void CheckPlaceholders(GameEvent e, string field, string text)
        {
            if (Family.Placeholder.Replace(text, "").IndexOfAny(new[] { '{', '}' }) >= 0)
                throw new ContentException(e.File, field, "stray '{' or '}' outside a {placeholder}");
            foreach (Match m in Family.Placeholder.Matches(text))
                if (!e.Scope.StartsWith("fate:", StringComparison.Ordinal) || !VillagePlaceholders.Contains(m.Groups[1].Value))
                    throw new ContentException(e.File, field, $"unknown placeholder '{m.Value}' ({{village_en}} and {{village_ar}} fill only a fate:* event)");
        }
    }
}
