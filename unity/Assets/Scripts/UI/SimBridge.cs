#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using StoryOfTheLand.Sim;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The UI's only door into the simulation (spec section 6). Holds the GameSession, collects this turn's
    /// orders, runs End Turn and tells the screens to refresh. The family game's only order is a card's choice
    /// (ChooseEventOption): it takes effect at End Turn, and a later choice for the same card replaces the earlier one.</summary>
    public sealed class SimBridge
    {
        readonly List<Command> queue = new List<Command>();

        public GameSession Session { get; private set; }
        public Content Content => Session.Content;
        public GameState State => Session.State;
        public IReadOnlyList<Command> Queued => queue;
        /// <summary>The orders the last turn ran with (empty until a turn has run, and after a refused batch).</summary>
        public IReadOnlyList<Command> LastBatch { get; private set; } = Array.Empty<Command>();
        /// <summary>The game date when the last turn's orders were given (the date before that turn ran).</summary>
        public DateTime LastBatchDate { get; private set; }
        /// <summary>The step before Session.Last (null until two turns have run), for "change since last turn". The session
        /// keeps it, so a loaded save has it from its replay (#398).</summary>
        public StepResult? BeforeLast => Session.BeforeLast;
        /// <summary>Raised after every change the screens show: an order queued or withdrawn, a turn, a load.</summary>
        public event Action? Changed;
        /// <summary>Raised after End Turn with the orders the simulation refused (possibly none).</summary>
        public event Action<IReadOnlyList<Rejection>>? TurnEnded;
        /// <summary>Raised when Queue refuses an order, with the player-facing reason.</summary>
        public event Action<string>? Refused;

        readonly UiText text;
        readonly Func<GameSession, IReadOnlyList<Command>, StepResult> step;

        /// <param name="step">How a turn is run; the default is Session.Step. Tests pass a stepper that throws, to prove
        /// that a batch the simulation refuses outright cannot soft-lock the game.</param>
        public SimBridge(GameSession session, UiText text, Func<GameSession, IReadOnlyList<Command>, StepResult>? step = null)
        {
            this.step = step ?? ((s, cmds) => s.Step(cmds));
            Session = session ?? throw new ArgumentNullException(nameof(session));
            this.text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>The order's slot in the queue: a later order with the same key replaces the earlier one.</summary>
        internal static string Key(Command c) => c is ChooseEventOption e ? "event:" + e.EventId : c.Kind;

        /// <summary>The queued order with this key, or null.</summary>
        public Command? QueuedLike(Command c) => queue.FirstOrDefault(q => Key(q) == Key(c));

        /// <summary>A card is still waiting for the player's answer (no choice queued for it): End Turn waits for it.</summary>
        public bool Undecided => State.GameOver == null && State.PendingEvents.Any(e => QueuedLike(new ChooseEventOption(e, "")) == null);

        /// <summary>Null if the id is known, else the player-facing reason. The player sees only the kind of thing (#622: a
        /// content id means nothing to them), in the UI's language: the string order.kind.&lt;kind&gt; (#595). The id goes to
        /// the log, for a bug report.</summary>
        string? Unknown(string kind, string? id, IEnumerable<string> known)
        {
            if (!string.IsNullOrEmpty(id) && known.Contains(id)) return null;
            UnityEngine.Debug.Log($"Order refused: unknown {kind.Replace('_', ' ')} '{id}'");
            return text.Format("order.unknown", text["order.kind." + kind]);
        }

        /// <summary>Checks every id an order carries against the content, so End Turn can never be handed an id the
        /// simulation would throw on. Returns the player-facing reason, or null.</summary>
        string? Validate(Command c)
        {
            if (!(c is ChooseEventOption e)) return text["order.invalid"];   // the family game has no other order
            var ev = Content.Events.FirstOrDefault(x => x.Id == e.EventId);
            return ev == null ? Unknown("event", e.EventId, new string[0]) : Unknown("option", e.OptionId, ev.Options.Select(x => x.Id));
        }

        string? Refuse(string reason) { Refused?.Invoke(reason); return reason; }

        static void Notify(Action? handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }

        static void Notify<T>(Action<T>? handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }

        /// <summary>Queues an order (replacing one with the same key). Returns null, or why it was refused here: an id
        /// the content does not have, or the game is over. A refusal is also raised as <see cref="Refused"/>.</summary>
        public string? Queue(Command c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            var bad = Validate(c);
            if (bad != null) return Refuse(bad);
            if (State.GameOver != null) return Refuse(text["order.game_over"]);
            var old = QueuedLike(c);
            if (old != null) queue[queue.IndexOf(old)] = c; else queue.Add(c);
            Changed?.Invoke();
            return null;
        }

        public void Withdraw(Command c)
        {
            if (queue.Remove(c)) Changed?.Invoke();
        }

        /// <summary>Runs the turn with the queued orders, clears the queue, and notifies. If the simulation refuses the
        /// batch outright (any exception: an ArgumentException is a refusal, anything else is also logged), the state is unchanged, the queue is cleared, and the reason is raised
        /// through TurnEnded like any refusal.</summary>
        public StepResult EndTurn()
        {
            StepResult r;
            var batch = queue.ToList();
            var date = State.Date;
            try { r = step(Session, batch); }
            catch (Exception e)   // any exception (M3 final review M7): never leave a queue that fails on every End Turn
            {
                // A refusal (ArgumentException) is a clear warning, anything else the full exception: both leave a trace.
                if (e is ArgumentException) UnityEngine.Debug.LogWarning("End Turn refused: " + e.Message);
                else UnityEngine.Debug.LogException(e);
                queue.Clear();
                LastBatch = Array.Empty<Command>();
                var reason = text["order.failed"];   // the message stays in the log above, not on screen (#622)
                var failed = new StepResult(State, new Cause[0], batch.Select(x => new Rejection(x, reason)).ToList());
                Notify(Changed);
                Notify(TurnEnded, failed.Rejected);
                return failed;
            }
            LastBatch = batch; LastBatchDate = date;
            queue.Clear();
            Notify(Changed);
            Notify(TurnEnded, r.Rejected);
            return r;
        }

        /// <summary>Switches to another session (a loaded save), dropping queued orders.</summary>
        public void Replace(GameSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            LastBatch = Array.Empty<Command>();
            queue.Clear();
            Changed?.Invoke();
        }
    }
}
