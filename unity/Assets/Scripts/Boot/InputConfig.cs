#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;

namespace StoryOfTheLand.Game.Boot
{
    /// <summary>Input System settings the game needs (playtest 2, P2-4). Redundant-event merging is turned off: with it on,
    /// a burst of mouse events in one frame (a slow frame, a quick double click, a click and then another click elsewhere)
    /// can merge a button release into the move that follows it (FastMouse.MergeForward; its skip rule protects only the
    /// event right after a button change). The release then reaches the UI at the next click's position, so the first
    /// button's click is cancelled: a screen toggle or an option is lost while the next click goes through. With merging
    /// off every press and release keeps its own position; the cost is one action update per mouse event.</summary>
    public static class InputConfig
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply() => InputSystem.settings.disableRedundantEventsMerging = true;
    }
}
