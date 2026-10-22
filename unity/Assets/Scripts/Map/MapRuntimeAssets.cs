using System.Collections.Generic;
using UnityEngine;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>Owns the objects MapBuilder creates at runtime (TerrainData, TerrainLayers, their textures, the selection
    /// band's mesh) and destroys them with the map, so rebuilding the map does not leak them. [ExecuteAlways] so OnDestroy also
    /// runs in edit mode (edit-mode tests, editor capture tools).</summary>
    [ExecuteAlways]
    public sealed class MapRuntimeAssets : MonoBehaviour
    {
        readonly List<Object> owned = new List<Object>();
        readonly List<System.Action> teardown = new List<System.Action>();

        public T Own<T>(T o) where T : Object { owned.Add(o); return o; }

        /// <summary>Runs <paramref name="action"/> when the map is torn down (newest first), for state the map changes outside
        /// itself: the URP asset's shadow distance, global shader values, its VisualToggles switches
        /// (<c>assets.OnTeardown(VisualToggles.Register(...))</c>).</summary>
        public void OnTeardown(System.Action action) => teardown.Add(action ?? throw new System.ArgumentNullException(nameof(action)));

        /// <summary>Runs the teardown actions now, newest first, and forgets them, so a second call (or OnDestroy) does
        /// nothing. MapScene.Clear calls it before destroying the map: in play mode Destroy is deferred to the end of the
        /// frame, after a rebuilt map's Init has already saved state, so a late teardown would undo the new map's state
        /// (review I1). One action throwing is logged and does not stop the others (review M2).</summary>
        public void RunTeardown()
        {
            var actions = teardown.ToArray();
            teardown.Clear();
            for (int i = actions.Length - 1; i >= 0; i--)
            {
                try { actions[i](); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
        }

        void OnDestroy()
        {
            RunTeardown();
            foreach (var o in owned)
            {
                if (o == null) continue;
                if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
            }
            owned.Clear();
        }
    }
}
