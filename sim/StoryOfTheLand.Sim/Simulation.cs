using System;
using System.Collections.Generic;
using System.Linq;

namespace StoryOfTheLand.Sim
{
    /// <summary>The public entry point the Unity layer calls: start a game, then step it one turn at a time.</summary>
    public static class Simulation
    {
        /// <summary>F5: a new game of one chapter (ContentLoader.LoadChapter) for a family in <paramref name="village"/>, a
        /// palestine/places.yaml id.</summary>
        public static GameState NewGame(Content c, ulong seed, string village) => Family.NewGame(c, seed, village);

        /// <summary>Applies the family's answers in order (one that fails carries a reason), lets the unanswered cards pass,
        /// then advances one month (Family.Step). Never mutates <paramref name="state"/>.</summary>
        public static StepResult Step(Content c, GameState state, IReadOnlyList<Command> commands)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (commands.Any(x => x == null)) throw new ArgumentException("Commands cannot contain null.", nameof(commands));
            CommandText.Validate(commands);

            var s = state.Clone();
            var why = new List<Cause>();
            var rejected = new List<Rejection>();
            if (s.GameOver != null)
                foreach (var cmd in commands) rejected.Add(new Rejection(cmd, $"The game is over: {PlayerText.GameOver(s.GameOver)}"));
            else
                Family.Step(c, s, commands, why, rejected);
            return new StepResult(s, why, rejected);
        }
    }
}
