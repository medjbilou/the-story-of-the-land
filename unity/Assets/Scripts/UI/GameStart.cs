#nullable enable
using System;
using System.Collections.Generic;
using StoryOfTheLand.Sim;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>Starting a session: continue the family autosave when there is one (replayed to the same state hash).
    /// Anything the player should know (a content-change warning, an unreadable save) goes to notes. No failure while
    /// reading or replaying the autosave stops the game from starting: the file is set aside. A new game is
    /// GameRoot.StartFamily.</summary>
    public static class GameStart
    {
        public static ulong NewSeed() => (ulong)DateTime.UtcNow.Ticks;

        /// <summary>Continues the family autosave (<paramref name="store"/> is SaveStore.Family) in the chapter its first
        /// line names, loaded through <paramref name="chapter"/> (GameInputs.Chapter) and replayed as GameSession.Load does;
        /// null when there is none. Nothing here stops the start: an unreadable save is set aside with one localised note
        /// (the sim's reason goes to the log only, #631), and a warning kind (the sim returns kinds, not text: #637) is said
        /// in the player's language, the kind going to the log.</summary>
        public static GameSession? ContinueFamily(Func<string, Content> chapter, SaveStore store, List<string> notes, UiText text)
        {
            try
            {
                var saved = store.ReadAutosave();
                if (saved == null) return null;
                var (id, save) = FamilyGame.SplitSave(saved);
                var loaded = GameSession.Load(chapter(id), save, out var warnings);
                foreach (var kind in warnings)
                {
                    UnityEngine.Debug.LogWarning("Family autosave loaded with a warning: " + kind);
                    if (text.Has("save.warning." + kind)) notes.Add(text["save.warning." + kind]);
                }
                return loaded;
            }
            catch (Exception e)
            {
                try { store.SetAsideAutosave(); } catch (Exception) { /* even this must not stop the start */ }
                UnityEngine.Debug.LogWarning("Family autosave could not be loaded: " + e);
                if (e is SaveException refused && refused.Code != null && text.Has("save.refused." + refused.Code))
                    notes.Add(text["save.refused." + refused.Code]);
                else notes.Add(text.Format("save.unreadable", text["save.reason_other"]));
                return null;
            }
        }

        /// <summary>F6: the game a save holds (a pause-menu slot or the autosave), replayed as Continue replays it, or null when
        /// there is none or it cannot be replayed (<paramref name="readable"/> false; the reason goes to the log). Nothing is set
        /// aside: the slot list only looks.</summary>
        public static GameSession? Peek(Func<string, Content> chapter, SaveStore store, out bool readable)
        {
            readable = true;
            try
            {
                var saved = store.ReadAutosave();
                if (saved == null) return null;
                var (id, save) = FamilyGame.SplitSave(saved);
                return GameSession.Load(chapter(id), save, out _);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"The save in {store.Directory} could not be read: {e.Message}");
                readable = false;
                return null;
            }
        }

        /// <summary>Writes the save after every turn. A write that fails (a full or read-only disk) never stops the game:
        /// the reason goes to <paramref name="onError"/>. Dispose the result to stop autosaving.</summary>
        public static IDisposable Autosave(SimBridge bridge, SaveStore store, UiText text, Action<string>? onError = null)
        {
            void Save(IReadOnlyList<Rejection> _)
            {
                try { store.WriteAutosave(SaveText(bridge.Session)); }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning("Autosave could not be written: " + e);
                    onError?.Invoke(text["save.write_failed"]);
                }
            }
            bridge.TurnEnded += Save;
            return new Unsubscribe(() => bridge.TurnEnded -= Save);
        }

        /// <summary>The autosave's text: the sim's save, with the chapter line first (FamilyGame.SaveText).</summary>
        public static string SaveText(GameSession s) => FamilyGame.SaveText(s);

        sealed class Unsubscribe : IDisposable
        {
            readonly Action undo;
            public Unsubscribe(Action undo) { this.undo = undo; }
            public void Dispose() => undo();
        }
    }
}
