using System;
using System.Collections.Generic;
using System.Linq;

namespace StoryOfTheLand.Sim
{
    /// <summary>One playthrough: the current state, the turn before it, the last step's report, and the save that
    /// records every turn. The Unity SimBridge wraps this; tests can use it directly.</summary>
    public sealed class GameSession
    {
        public Content Content { get; }
        public GameState State { get; private set; }
        /// <summary>The state before the last step (null before the first), for "change since last turn".</summary>
        public GameState? Previous { get; private set; }
        /// <summary>The last step's result (null before the first): its Why and Rejected lists.</summary>
        public StepResult? Last { get; private set; }
        /// <summary>The step before Last (null before the second step), for a change that compares two steps' results (the
        /// top bar's arrivals). A loaded session has it from its replay, like Previous and Last. Not part of the state or
        /// the save.</summary>
        public StepResult? BeforeLast { get; private set; }
        public SaveGame Save { get; }

        GameSession(Content c, GameState s, SaveGame save) { Content = c; State = s; Save = save; }

        /// <summary>F5: a new game of one chapter for a family in <paramref name="village"/> (Simulation.NewGame).</summary>
        public static GameSession New(Content c, ulong seed, string village)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            var s = Simulation.NewGame(c, seed, village);
            return new GameSession(c, s, new SaveGame(c.Hash, seed, s.Hash(), village));
        }

        /// <summary>Runs one turn and records it in the save.</summary>
        public StepResult Step(IReadOnlyList<Command> commands)
        {
            var r = Simulation.Step(Content, State, commands);
            Save.Record(commands, r.State);
            Previous = State; State = r.State; BeforeLast = Last; Last = r;
            return r;
        }

        /// <summary>Load warning: the save was made with other content, so the replayed game may differ (spec section 7).</summary>
        public const string ContentChanged = "content_changed";
        /// <summary>Load warning: made with other content, the replay then ended on a different state than the saved game.</summary>
        public const string ReplayDiffers = "replay_differs";

        /// <summary>Parses and replays a save. Throws SaveException when the text is malformed, or when the content is the
        /// same but the replay ends on a different hash (a determinism bug). Content made with other content loads with a
        /// warning (spec section 7), and a second warning if the replay then differs from the saved game. The warnings are
        /// kinds (<see cref="ContentChanged"/>, <see cref="ReplayDiffers"/>), not display text: the UI says each in the
        /// player's language, as it does SaveException.Code (#637).</summary>
        public static GameSession Load(Content c, string saveText, out IReadOnlyList<string> warnings)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            var saved = SaveGame.Parse(saveText);   // refuses a save with older rules (R-SAVE-001) or a newer one (R-SAVE-002)
            var list = new List<string>();
            bool sameContent = saved.ContentHash == c.Hash;
            if (!sameContent) list.Add(ContentChanged);
            if (c.Places.All(p => p.Id != saved.Village))
                throw new SaveException(6, $"unknown village '{saved.Village}'");
            var session = New(c, saved.Seed, saved.Village);
            int turnNo = 0;
            try { foreach (var turn in saved.Turns) { session.Step(turn); turnNo++; } }
            catch (SaveException) { throw; }
            catch (Exception e) { throw new SaveException(FormattableString.Invariant($"turn {turnNo + 1}: {e.Message}"), e); }
            if (session.State.Hash() != saved.FinalHash)
            {
                if (sameContent) throw new SaveException(0, "The replay does not match the saved game: the save is damaged or was edited, or the game has a bug.");
                list.Add(ReplayDiffers);
            }
            warnings = list;
            return session;
        }
    }
}
