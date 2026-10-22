using System;
using System.Collections.Generic;

namespace StoryOfTheLand.Sim
{
    /// <summary>A player order for one turn.</summary>
    public abstract class Command { public abstract string Kind { get; } }

    public sealed class ChooseEventOption : Command
    {
        public override string Kind => "choose_event_option";
        public string EventId { get; } public string OptionId { get; }
        public ChooseEventOption(string eventId, string optionId) { EventId = eventId; OptionId = optionId; }
    }

    public sealed class Rejection
    {
        public Command Command { get; } public string Reason { get; }
        public Rejection(Command command, string reason) { Command = command; Reason = reason; }
    }

    public sealed class StepResult
    {
        public GameState State { get; }
        public IReadOnlyList<Cause> Why { get; }
        public IReadOnlyList<Rejection> Rejected { get; }
        public StepResult(GameState state, IReadOnlyList<Cause> why, IReadOnlyList<Rejection> rejected) { State = state; Why = why; Rejected = rejected; }
    }

    /// <summary>Checks that every text an order carries can be saved: a null id or a lone surrogate would make the save
    /// unwritable or lossy, so Step refuses the whole batch before anything happens (a programmer error, like a null Command).</summary>
    static class CommandText
    {
        public static void Validate(IReadOnlyList<Command> commands)
        {
            foreach (var cmd in commands)
            {
                if (!(cmd is ChooseEventOption e)) throw new ArgumentException("unknown command type " + cmd.GetType().Name, "commands");
                Text(e.EventId, "EventId"); Text(e.OptionId, "OptionId");
            }
        }

        static void Text(string? s, string field)
        {
            if (s == null) throw new ArgumentNullException("commands", $"{field} cannot be null.");
            if (!SaveText.IsEncodable(s)) throw new ArgumentException($"{field} is not valid text (a lone surrogate).", "commands");
        }
    }
}
