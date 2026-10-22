#nullable enable
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>Shadows at map scale (research notes §0.2): URP has no per-camera shadow distance, so the active URP asset's
    /// shadowDistance follows the camera, and Restore puts the asset's own value back (the map registers Restore as a
    /// teardown action, so destroying the map, a rebuild or the end of a test leaves the asset as it was; in the editor,
    /// leaving play mode and an assembly reload restore it too).
    /// URP fades shadows by straight-line distance from the camera, starting at (1 - cascadeBorder) of the shadow distance
    /// (URP 17.3 Shadows.hlsl, ShadowUtils.GetScaleAndBiasForLinearDistanceFade), so the fit is the farthest ground point of
    /// the view, measured as a straight line, divided by (1 - cascadeBorder): the whole view is inside the unfaded range
    /// (review I1). It is recomputed on every rig Apply from the camera's own corner rays (top inset and aspect included).</summary>
    public static class ShadowFit
    {
        public const float MinDistanceM = 3000f;
        /// <summary>The rig's far clip plane: at 250 km, a 21:9 window and the largest top inset the top corners are 550 km away.</summary>
        public const float MaxDistanceM = 1_000_000f;
        /// <summary>The ground plane the corner rays are cut with: the terrain's lowest point (its elev_min sits at y = 0), so
        /// every real ground hit is nearer than the plane's.</summary>
        public const float GroundY = 0f;

        static UniversalRenderPipelineAsset? touched;
        static float original;

        static readonly Vector2[] Corners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };

        /// <summary>Straight-line distance from the camera to the farthest of the four viewport corners' ground hits (a corner
        /// that does not look down counts as MaxDistanceM).</summary>
        public static float FarthestGroundHit(Camera cam)
        {
            float far = 0f;
            foreach (var v in Corners)
            {
                var ray = cam.ViewportPointToRay(new Vector3(v.x, v.y, 0f));
                if (ray.direction.y > -1e-4f) return MaxDistanceM;
                var hit = ray.origin + ray.direction * ((GroundY - ray.origin.y) / ray.direction.y);
                far = Mathf.Max(far, (hit - cam.transform.position).magnitude);
            }
            return far;
        }

        /// <summary>The shadow distance that keeps the whole view of <paramref name="cam"/> unfaded with the given cascade border.</summary>
        public static float Distance(Camera cam, float cascadeBorder) =>
            Mathf.Clamp(FarthestGroundHit(cam) / (1f - Mathf.Clamp(cascadeBorder, 0f, 0.9f)), MinDistanceM, MaxDistanceM);

        public static void Apply(Camera cam)
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset == null || cam == null) return;
            if (touched != asset) { Restore(); touched = asset; original = asset.shadowDistance; }   // a quality-level switch
            asset.shadowDistance = Distance(cam, asset.cascadeBorder);
        }

        public static void Restore()
        {
            if (touched != null) touched.shadowDistance = original;
            touched = null;
        }

#if UNITY_EDITOR
        /// <summary>Review M2: a domain reload drops the map's teardown list and this class's memory of the original, and leaving
        /// play mode should not depend on the map's OnDestroy order; both restore here.</summary>
        [UnityEditor.InitializeOnLoadMethod]
        static void HookEditor()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void OnBeforeAssemblyReload() => Restore();

        public static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) Restore();
        }
#endif
    }
}
