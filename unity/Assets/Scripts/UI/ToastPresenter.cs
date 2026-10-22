#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>A short message under the top bar: why orders were refused (spec section 7: the UI shows the stated
    /// reason), or a note such as a save warning. Hidden when there is nothing to say; cleared by the next order.</summary>
    public sealed class ToastPresenter : System.IDisposable
    {
        readonly Label label; readonly VisualElement hud; readonly UiText text; readonly SimBridge bridge;
        readonly System.Action<IReadOnlyList<StoryOfTheLand.Sim.Rejection>> onTurnEnded;

        public ToastPresenter(VisualElement root, SimBridge bridge, UiText text)
        {
            label = root.Q<Label>("toast"); hud = root.Q("root") ?? root; this.text = text; this.bridge = bridge;
            onTurnEnded = r => Show(r.Select(x => text.Format("toast.refused", x.Reason)));
            bridge.TurnEnded += onTurnEnded;
            bridge.Refused += Refuse;
            bridge.Changed += Clear;
            Clear();
        }

        public void Dispose() { bridge.TurnEnded -= onTurnEnded; bridge.Refused -= Refuse; bridge.Changed -= Clear; }

        /// <summary>Shows the lines as they are (none = hide).</summary>
        public void Show(IEnumerable<string> lines)
        {
            var list = lines.ToList();
            bool was = label.style.display.value == DisplayStyle.Flex && label.text.Length > 0;
            label.text = string.Join("\n", list);
            label.style.display = list.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            // Playtest 3: a toast that appears slides in (F7's motion: StoryOfTheLand.uss .toast-from).
            if (!was && list.Count > 0 && hud.ClassListContains(SettingsPresenter.MotionClass)) EventCardPresenter.Enter(label, "toast-from", 0);
        }

        public void Show(string line) => Show(new[] { line });

        /// <summary>Adds a line under what is shown (alone when nothing is), e.g. a failed autosave after the turn's
        /// refusals.</summary>
        public void Append(string line) =>
            Show(label.style.display.value == DisplayStyle.Flex && label.text.Length > 0 ? label.text + "\n" + line : line);

        /// <summary>Shows why an order was refused ("Not done: ...").</summary>
        public void Refuse(string reason) => Show(text.Format("toast.refused", reason));

        public void Clear() => Show(new string[0]);
    }
}
