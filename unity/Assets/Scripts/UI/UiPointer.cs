#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>"Is the pointer over the UI?" for camera and map clicks (tech notes section 5). Input System positions are
    /// y-up; RuntimePanelUtils.ScreenToPanel does not flip, and panel space is y-down, so y is flipped first. Elements with
    /// picking-mode Ignore (the root) let the pointer through to the map.</summary>
    public static class UiPointer
    {
        public static Vector2 ToPanel(IPanel panel, Vector2 screenYUp, float screenHeight) =>
            RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenYUp.x, screenHeight - screenYUp.y));

        public static bool IsOverUi(IPanel? panel, Vector2 screenYUp, float screenHeight) =>
            panel != null && panel.Pick(ToPanel(panel, screenYUp, screenHeight)) != null;
    }
}
