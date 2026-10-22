#nullable enable
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>Fills the short, presentation-only guide to the family game's turn loop (#1041): one line per key in
    /// <see cref="Keys"/> (how_to_play.&lt;key&gt; in the UI's strings, label how-to-play-&lt;key&gt; in Game.uxml).</summary>
    public sealed class HowToPlayPresenter : System.IDisposable
    {
        public static readonly string[] Keys = { "turn", "cards", "bar", "end_turn", "why", "narration" };

        public HowToPlayPresenter(VisualElement root, UiText text)
        {
            var screen = root.Q<VisualElement>("screen-how-to-play");
            screen.Q<Label>("how-to-play-title").text = text["how_to_play.title"];
            foreach (var key in Keys)
                screen.Q<Label>("how-to-play-" + key.Replace('_', '-')).text = text["how_to_play." + key];
        }

        public void Dispose() { }
    }
}
