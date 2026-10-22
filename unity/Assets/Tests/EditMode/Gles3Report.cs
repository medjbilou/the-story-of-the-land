#nullable enable
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Plan Task 3 Step 0: prints ShaderChecks.Gles3Errors for URP Lit, URP Terrain Lit and every StoryOfTheLand/ shader, so
/// a `require` is judged once, up front, instead of in the middle of a task. Run with -executeMethod Gles3Report.Run.</summary>
public static class Gles3Report
{
    static readonly string[] Urp = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Terrain/Lit" };

    public static void Run()
    {
        var shaders = Urp.Select(Shader.Find).ToList();
        shaders.AddRange(AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shaders" })
            .Select(g => AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(g))));
        foreach (var shader in shaders.Where(s => s != null).Distinct())
        {
            var errors = ShaderChecks.Gles3Errors(shader);
            Debug.Log($"GLES3REPORT {shader.name}: {errors.Count} error(s)");
            foreach (var e in errors) Debug.Log($"GLES3REPORT   {e}");
        }
        foreach (var name in Urp)
            if (Shader.Find(name) == null) Debug.LogError($"GLES3REPORT {name}: shader not found");
        Debug.Log("GLES3REPORT done");
    }
}
