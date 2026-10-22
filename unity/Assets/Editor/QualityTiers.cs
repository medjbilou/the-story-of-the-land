#nullable enable
using StoryOfTheLand.Game.Map;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>Writes the visual v1 tier decisions (QualityTierValues; docs/visual-v1.md "Tier decisions") to the URP assets:
    /// web (Mobile_*): Forward, no depth or opaque copy, one hard cascade, no MSAA; desktop (PC_*): two soft cascades at Medium
    /// (F6), no MSAA (F6: SMAA on the camera instead, CameraRig), no depth or opaque copy, SSAO off (the baked AO replaces it); the tilt-shift's resolution on both; the bloom of the pipeline
    /// assets' own volume profile. Idempotent. Run headless:
    /// Unity -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -executeMethod StoryOfTheLand.Game.EditorTools.QualityTiers.Apply</summary>
    public static class QualityTiers
    {
        public const string WebAsset = "Assets/Settings/Mobile_RPAsset.asset", DesktopAsset = "Assets/Settings/PC_RPAsset.asset";
        public const string WebRenderer = "Assets/Settings/Mobile_Renderer.asset", DesktopRenderer = "Assets/Settings/PC_Renderer.asset";

        /// <summary>Every code-driven project setting in one batch call (ProjectSetup's name, id and build settings, then the
        /// tiers): what restores the settings after `git checkout -- unity/Assets/Settings unity/ProjectSettings` (F6, 2026-10-20:
        /// a reset brought SSAO back into PC_Renderer and the old product name). SettingsGuardTests catches such a reset.
        /// -executeMethod StoryOfTheLand.Game.EditorTools.QualityTiers.RestoreAll</summary>
        public static void RestoreAll()
        {
            ProjectSetup.Apply();
            Apply();
        }

        public static void Apply()
        {
            Pipeline(WebAsset, QualityTierValues.WebRenderScale, 1, QualityTierValues.WebShadowMap, soft: false, QualityTierValues.WebMsaa);
            Pipeline(DesktopAsset, 1f, QualityTierValues.DesktopCascades, QualityTierValues.DesktopShadowMap, soft: true, QualityTierValues.DesktopMsaa);
            var pc = new SerializedObject(Load(DesktopAsset));
            pc.FindProperty("m_SoftShadowQuality").intValue = QualityTierValues.DesktopSoftShadowQuality;
            pc.FindProperty("m_Cascade2Split").floatValue = QualityTierValues.DesktopCascade2Split;
            pc.ApplyModifiedPropertiesWithoutUndo();
            Renderer(WebRenderer, QualityTierValues.WebTiltShiftDownsample, ssao: null);
            Renderer(DesktopRenderer, QualityTierValues.DesktopTiltShiftDownsample, ssao: false);
            Bloom(WebAsset, QualityTierValues.Bloom);
            Bloom(DesktopAsset, QualityTierValues.Bloom);
            AssetDatabase.SaveAssets();
        }

        static UniversalRenderPipelineAsset Load(string path) =>
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path) ?? throw new System.IO.FileNotFoundException(path);

        static void Pipeline(string path, float renderScale, int cascades, int shadowMap, bool soft, int msaa)
        {
            var so = new SerializedObject(Load(path));
            so.FindProperty("m_MSAA").intValue = msaa;
            so.FindProperty("m_RenderScale").floatValue = renderScale;
            so.FindProperty("m_ShadowCascadeCount").intValue = cascades;
            so.FindProperty("m_MainLightShadowmapResolution").intValue = shadowMap;
            so.FindProperty("m_SoftShadowsSupported").boolValue = soft;
            so.FindProperty("m_RequireDepthTexture").boolValue = false;
            so.FindProperty("m_RequireOpaqueTexture").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Renderer(string path, int tiltShiftDownsample, bool? ssao)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path) ?? throw new System.IO.FileNotFoundException(path);
            for (int i = data.rendererFeatures.Count - 1; i >= 0; i--)
            {
                var f = data.rendererFeatures[i];
                if (f is TiltShiftFeature t) { t.Downsample = tiltShiftDownsample; EditorUtility.SetDirty(t); }
                else if (f != null && ssao == false && f.GetType().Name == "ScreenSpaceAmbientOcclusion") Remove(data, i);
                else if (f != null && ssao == true && f.GetType().Name == "ScreenSpaceAmbientOcclusion") { f.SetActive(true); EditorUtility.SetDirty(f); }
            }
            EditorUtility.SetDirty(data);
        }

        /// <summary>Removes the renderer feature at <paramref name="index"/> and its sub-asset. An SSAO feature that is only
        /// switched off breaks the Unity 6.6 player: the build strips its resources, but URP still creates the feature and
        /// fails to build the pipeline, so every frame is blank (2026-10-19).</summary>
        static void Remove(UniversalRendererData data, int index)
        {
            var feature = data.rendererFeatures[index];
            var so = new SerializedObject(data);
            so.FindProperty("m_RendererFeatures").DeleteArrayElementAtIndex(index);
            so.FindProperty("m_RendererFeatureMap").DeleteArrayElementAtIndex(index);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.RemoveObjectFromAsset(feature);
            UnityEngine.Object.DestroyImmediate(feature, true);
        }

        /// <summary>The pipeline asset's own volume profile (today the template's Settings/SampleSceneProfile on both tiers)
        /// keeps its Neutral tonemapper either way: it is what keeps the tonemapper's variants in builds (GradeTests).</summary>
        static void Bloom(string path, bool on)
        {
            var profile = Load(path).volumeProfile ?? throw new System.InvalidOperationException(path + " has no volume profile");
            if (profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom)) { bloom.active = on; EditorUtility.SetDirty(bloom); }
            EditorUtility.SetDirty(profile);
        }
    }
}
