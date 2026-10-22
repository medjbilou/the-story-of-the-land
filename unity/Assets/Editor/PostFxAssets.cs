#nullable enable
using System.Linq;
using StoryOfTheLand.Game.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>Creates TiltShift.mat and adds one active TiltShiftFeature using it to both URP renderers (web: Mobile, desktop:
    /// PC). Idempotent. Adding a feature needs the renderer's feature list and its local-file-id map kept in step, as URP's own
    /// renderer inspector does. Run headless:
    /// Unity -batchmode -quit -projectPath unity -CI false -GITHUB_ACTIONS false -TF_BUILD false -executeMethod StoryOfTheLand.Game.EditorTools.PostFxAssets.Create</summary>
    public static class PostFxAssets
    {
        public static readonly string[] Renderers = { "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/PC_Renderer.asset" };

        public static void Create()
        {
            var shader = Shader.Find("StoryOfTheLand/TiltShift") ?? throw new System.InvalidOperationException("shader not found: StoryOfTheLand/TiltShift");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PostFxPaths.TiltShiftMaterial);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, PostFxPaths.TiltShiftMaterial); }
            else mat.shader = shader;
            EditorUtility.SetDirty(mat);
            foreach (var path in Renderers)
                AddFeature(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path) ?? throw new System.IO.FileNotFoundException("renderer missing", path), mat);
            AssetDatabase.SaveAssets();
        }

        static void AddFeature(ScriptableRendererData data, Material mat)
        {
            var feature = data.rendererFeatures.OfType<TiltShiftFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<TiltShiftFeature>();
                feature.name = "TiltShift";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.SaveAssetIfDirty(data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++; features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            feature.Material = mat;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature); EditorUtility.SetDirty(data);
        }
    }
}
