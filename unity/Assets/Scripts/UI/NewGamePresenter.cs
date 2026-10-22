#nullable enable
using System;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>New game asks for a second press (no browser confirm dialogs in WebGL), then opens the chapter and village
    /// picker (<c>startNew</c>): the family save is replaced only when a village is picked (GameRoot.StartFamily), so going
    /// back from the picker loses nothing. Ending a turn in between cancels the request. The start screen's New game goes
    /// through the same two presses (Press) when there is a save, so neither button can wipe it with one stray press (#175, B6).</summary>
    public sealed class NewGamePresenter : IDisposable
    {
        readonly SimBridge bridge; readonly ToastPresenter toast; readonly UiText text; readonly Action startNew;
        readonly Button button; readonly Action<Action>? confirm;

        /// <param name="confirm">Playtest 2: asks "Your current game will be lost. Continue?" with Confirm and Cancel (the pause
        /// menu's confirm page) and runs its argument on Confirm; null keeps the two presses.</param>
        public NewGamePresenter(VisualElement root, SimBridge bridge, ToastPresenter toast, UiText text, Action startNew, Action<Action>? confirm = null)
        {
            this.confirm = confirm;
            this.bridge = bridge; this.toast = toast; this.text = text; this.startNew = startNew;
            button = root.Q<Button>("new-game");
            button.clicked += Press;
            bridge.Changed += Disarm;
        }

        // Any change (an order, a turn, a new session) means the prompt no longer describes what is on screen.
        void Disarm() => Armed = false;

        /// <summary>Unsubscribes from the bridge and the button (the owner calls this when disabled).</summary>
        public void Dispose() { button.clicked -= Press; bridge.Changed -= Disarm; }

        public bool Armed { get; private set; }

        /// <summary>Withdraws an open request (the start screen's Continue and Escape): disarms and takes the prompt off the
        /// toast. Nothing happens when no request is open.</summary>
        public void Cancel()
        {
            if (!Armed) return;
            Armed = false;
            toast.Clear();
        }

        /// <summary>One press of New game: with <c>confirm</c>, the question first; without, the first press arms and shows the
        /// prompt, the second opens the picker.</summary>
        public void Press()
        {
            if (confirm != null) { confirm(startNew); return; }
            if (!Armed) { Armed = true; toast.Show(text["newgame.confirm"]); return; }
            Armed = false;
            toast.Clear();
            startNew();   // the start screen, if up, stays under the picker until the new game starts
        }
    }
}
