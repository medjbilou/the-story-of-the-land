#nullable enable
using System.Linq;
using StoryOfTheLand.Sim;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>"Why did this happen?" (spec section 4, step 6): the last month in the UI's language (FamilyGame.Why): each
    /// card, what the family chose and what changed.</summary>
    public sealed class WhyPresenter : System.IDisposable
    {
        readonly VisualElement screen; readonly SimBridge bridge; readonly UiText text;

        public WhyPresenter(VisualElement root, SimBridge bridge, UiText text)
        {
            screen = root.Q<VisualElement>("screen-why"); this.bridge = bridge; this.text = text;
            screen.Q<Label>("why-screen-title").text = text["why.title"];
            bridge.Changed += Render;
            Render();
        }

        /// <summary>Unsubscribes from the bridge (the owner calls this when disabled).</summary>
        public void Dispose() { bridge.Changed -= Render; }

        GameSession? shownSession; StepResult? shown;

        /// <summary>Shows the session's last turn. Does nothing when that same result is already on screen.</summary>
        public void Render() => Show(bridge.Session.Last);

        /// <summary>Test seam (internal; the tests see it through InternalsVisibleTo). Shows <paramref name="last"/> (a turn's result, or null for "nothing yet"). The same session and result
        /// as last time are not rebuilt; a new turn, a new game or a loaded save are.</summary>
        internal void Show(StepResult? last)
        {
            if (shownSession == bridge.Session && shown == last && screen.Q<ScrollView>("why-rows").childCount > 0) return;
            shownSession = bridge.Session; shown = last;
            var rows = screen.Q<ScrollView>("why-rows");
            rows.Clear();
            var prev = bridge.Session.Previous;
            screen.Q<Label>("why-turn").text = last == null || prev == null ? "" : text.Format("why.turn",
                StoryOfTheLand.Game.Map.Control1948.DateText(prev.Date, k => text[k]), StoryOfTheLand.Game.Map.Control1948.DateText(bridge.State.Date, k => text[k]));   // PlayerText.Date is always English (#701)
            if (last == null) { rows.Add(new Label(text["why.empty"]) { name = "why-empty" }); return; }
            if (last.Why.Count == 0) { rows.Add(new Label(text["why.nothing"]) { name = "why-nothing" }); return; }
            if (prev == null) return;
            int i = 0;
            foreach (var g in FamilyGame.Why(bridge.Content, prev, bridge.State, last.Why, text))
            {
                var header = new Label(g.Heading) { name = "why-family-" + i };
                header.AddToClassList("why-group");
                rows.Add(header);
                foreach (var l in g.Lines) { var line = new Label(l) { name = "why-" + i++ }; line.AddToClassList("why-line"); rows.Add(line); }
                i++;
            }
        }
    }
}
