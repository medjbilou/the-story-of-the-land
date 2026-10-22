using System;
using System.Collections.Generic;
using System.Linq;

namespace StoryOfTheLand.Sim
{
    public static class EventSystem
    {
        static GameEvent Get(Content c, string id) => c.Events.First(e => e.Id == id);

        /// <summary>Fires every unfired event that reaches the family (Family.Reaches) and whose window [From, To) overlaps the
        /// month just played, [<paramref name="turnStart"/>, s.Date), or contains s.Date; <paramref name="turnStart"/> defaults
        /// to s.Date (game start). A family's month brings every such event (the prototype's rule), in date order: From, then
        /// Day (a month-wide event, with none, first), then id.
        /// Late fire: an event whose window overlapped the month just played can fire after its window has ended.</summary>
        public static void Trigger(Content c, GameState s, List<Cause> why, DateTime? turnStart = null)
        {
            var start = turnStart ?? s.Date;
            var fired = c.Events
                .Where(e => !s.FiredEvents.Contains(e.Id) && e.From <= s.Date && (e.To == null || start < e.To.Value))
                .Where(e => Family.Reaches(c, s, e))
                .OrderBy(e => e.From).ThenBy(e => e.Day ?? 0).ThenBy(e => e.Id, StringComparer.Ordinal)
                .ToList();
            foreach (var e in fired)
            {
                s.FiredEvents.Add(e.Id);
                s.PendingEvents.Add(e.Id);
                why.Add(new Cause("events", e.Id, 0, $"New: {Title(c, s, e)}"));
            }
        }

        /// <summary>Returns null on success, or the reason the choice was rejected.</summary>
        public static string? Choose(Content c, GameState s, string eventId, string optionId, List<Cause> why)
        {
            if (!s.PendingEvents.Contains(eventId)) return "That event is not waiting for a decision";
            var option = Get(c, eventId).Options.FirstOrDefault(o => o.Id == optionId);
            if (option == null) return "That event has no such option";
            Resolve(c, s, eventId, option, "chosen", why);
            return null;
        }

        /// <summary>F5: a family card left unanswered still happens. Its own effects apply, no option's do, and the option
        /// recorded for it is "" (no option has that id), as in the prototype's month end.</summary>
        public static void LetPass(Content c, GameState s, List<Cause> why)
        {
            foreach (var id in s.PendingEvents.ToList()) Resolve(c, s, id, NoAnswer, "unanswered", why);
        }

        static readonly EventOption NoAnswer = new EventOption { Id = "", Text = "no choice was made" };

        static void Resolve(Content c, GameState s, string eventId, EventOption o, string how, List<Cause> why)
        {
            s.PendingEvents.Remove(eventId);
            s.ChosenOptions[eventId] = o.Id;
            var e = Get(c, eventId);
            var title = Title(c, s, e);
            why.Add(new Cause("events", eventId, 0, $"{how}: {Family.Fill(c, s, o.Text)}"));
            // F4: the event's own effects first, then the option's.
            foreach (var fx in e.Effects.Concat(o.Effects))
            {
                var change = Shift(c, s, fx.Target, fx.Delta);
                why.Add(new Cause("events", fx.Target, change, $"{PlayerText.Change(c, fx.Target, change)} ({title})"));
            }
        }

        /// <summary>Moves a resource's level by <paramref name="delta"/> through its permanent shift, clamped to [0, 1]: the
        /// shift is set so the level lands exactly on the visible result. Returns the change actually applied (0 when the
        /// resource was already at the clamp).</summary>
        static double Shift(Content c, GameState s, string indicator, double delta)
        {
            var ind = c.Indicators.First(i => i.Id == indicator);
            var inner = Math.Max(0, Math.Min(1, ind.Base));
            var level = Math.Max(0, Math.Min(1, inner + s.IndicatorShifts[ind.Id]));
            var newLevel = Math.Max(0, Math.Min(1, level + delta));
            s.IndicatorShifts[ind.Id] = newLevel - inner;
            s.Indicators[ind.Id] = newLevel;
            return newLevel - level;
        }

        /// <summary>The card's title as the player sees it now, with the family's village filled in.</summary>
        public static string Title(Content c, GameState s, GameEvent e) => Family.Fill(c, s, e.Title);
    }
}
