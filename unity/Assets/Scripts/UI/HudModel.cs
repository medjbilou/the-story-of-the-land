#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using StoryOfTheLand.Sim;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>One top-bar indicator: its value and the change since the last turn (empty when there is none), and the
    /// two again in words (HudModel.Spoken): SayValue, and SayChange without its sign (empty when there is no change).</summary>
    public readonly struct Meter
    {
        public readonly string Value, Delta, SayValue, SayChange;
        /// <summary>+1 up, -1 down, 0 unchanged or unknown.</summary>
        public readonly int Sign;
        public Meter(string value, string delta, int sign, string sayValue, string sayChange)
        { Value = value; Delta = delta; Sign = sign; SayValue = sayValue; SayChange = sayChange; }
    }

    /// <summary>The top bar's meters in words and numbers, computed from simulation state only (no UI types), so it is
    /// testable on its own: the family's resources (0..1) show as 0-100 with "%".</summary>
    public static class HudModel
    {
        /// <summary>The meter in words, kept as its tooltip for a later accessibility layer (as the event card's alt text;
        /// no screen reader reads a runtime tooltip yet, #398): "Land: 62%, down 3 points since last turn"; with no change,
        /// the caption and the value only.</summary>
        public static string Spoken(UiText t, string name, Meter m)
        {
            // Audit m5: the Arabic "{0}: {1}" has no letter of its own, so Format leaves "47%" unmarked and a right-to-left
            // line drew "%47"; the value is held left to right here, as the bar shows it (the longer patterns have letters
            // and mark their values themselves).
            if (m.SayChange.Length == 0)
                return t.Format("hud.say", name, t["ui.lang"] == UiLanguage.Arabic ? UiText.LeftToRight(m.SayValue) : m.SayValue);
            return t.Format(m.Sign > 0 ? "hud.say_up" : "hud.say_down", name, m.SayValue, m.SayChange);
        }

        /// <summary>A 0..1 level as a meter on the 0-100 scale with "%", and its change since <paramref name="before"/> (none
        /// before the first turn).</summary>
        public static Meter Level(UiText t, double now, double? before)
        {
            var value = PlayerText.Percent(now);                   // the one formatter (fix round 2, N4)
            if (before == null) return new Meter(value, "", 0, value, "");
            // The difference of the two shown values, so the change always agrees with them (fix round 3, m-1).
            var points = PlayerText.ShownPoints(now) - PlayerText.ShownPoints(before.Value);
            var said = t.Plural("hud.say_points", (long)Math.Abs(points), Math.Abs(points));
            return points == 0 ? new Meter(value, "", 0, value, "") : new Meter(value, PlayerText.PercentChangeShown(before.Value, now), Math.Sign(points), value, said);
        }

        /// <summary>Playtest 3: at or under this many shown points a meter's tip adds that 0% does not end the game.</summary>
        public const double LowNotePoints = 10;

        /// <summary>Playtest 3: the tip's second line for resource <paramref name="id"/>: what it means (hud.meaning.*); this
        /// turn's change split between the choices made and everything else (cards' own effects, other events), when both
        /// moved it; and, when it is low, that 0% does not end the game.</summary>
        public static string Explained(UiText t, Content c, GameState? before, GameState after, IReadOnlyList<Cause>? why, string id)
        {
            var lines = new List<string>();
            if (t.Has("hud.meaning." + id)) lines.Add(t["hud.meaning." + id]);
            if (before != null && why != null)
            {
                var total = PlayerText.ShownPoints(after.Indicators[id]) - PlayerText.ShownPoints(before.Indicators[id]);
                var chosen = PlayerText.ShownPoints(FamilyGame.FromChoices(c, after, why, id));   // a change, in whole points as shown
                if (chosen != 0 && total - chosen != 0)
                    lines.Add(t.Format("hud.split", PlayerText.PercentChange(chosen / 100, 0), PlayerText.PercentChange((total - chosen) / 100, 0)));
            }
            if (PlayerText.ShownPoints(after.Indicators[id]) <= LowNotePoints) lines.Add(t["hud.low"]);
            return string.Join("\n", lines);
        }

        /// <summary>Playtest 3: the resources whose fall to 0% the toast tells, once, on the turn it happens.</summary>
        public static readonly string[] ZeroToasts = { "home", "safety" };

        /// <summary>The toast's lines for the resources of ZeroToasts shown at 0% now and not before (hud.zero.*).</summary>
        public static IEnumerable<string> ZeroLines(UiText t, GameState? before, GameState after) =>
            ZeroToasts.Where(id => after.Indicators.ContainsKey(id) && before != null && PlayerText.ShownPoints(after.Indicators[id]) == 0
                && PlayerText.ShownPoints(before.Indicators[id]) > 0).Select(id => t["hud.zero." + id]);
    }
}
