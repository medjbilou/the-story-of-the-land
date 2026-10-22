#nullable enable
using StoryOfTheLand.Game.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>Tells the camera what share of the window the top bar covers (CameraRig.TopInset), so the map is framed in
    /// the area below the bar and labels near the top of the view are not hidden under it (playtest 1, item 3). Follows
    /// the bar and the window as they change size; disposing gives the camera the whole screen back.</summary>
    public sealed class TopBarInset : System.IDisposable
    {
        readonly VisualElement root, bar; readonly CameraRig rig;

        public TopBarInset(VisualElement root, CameraRig rig)
        {
            this.root = root; this.rig = rig;
            bar = root.Q<VisualElement>("top-bar");
            bar.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            Update();
        }

        void OnGeometry(GeometryChangedEvent e) => Update();

        /// <summary>Sets the inset from the current layout (0 until the panel has one).</summary>
        public void Update()
        {
            if (rig == null) return;   // the camera went first (scene teardown)
            var window = root.panel?.visualTree.worldBound.height ?? float.NaN;
            var height = bar.worldBound.height;
            rig.TopInset = window > 0 && !float.IsNaN(height) && height > 0 ? Mathf.Clamp01(height / window) : 0f;
            rig.ApplyProjection();
        }

        public void Dispose()
        {
            bar.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            if (rig == null) return;
            rig.TopInset = 0f;
            rig.ApplyProjection();
        }
    }
}
