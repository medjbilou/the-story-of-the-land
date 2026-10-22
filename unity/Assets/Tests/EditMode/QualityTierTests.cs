#nullable enable
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Plan Task 10: the tier decisions recorded in docs/visual-v1.md, pinned on the URP assets.</summary>
public class QualityTierTests
{
    // The asset paths repeat QualityTiers.WebAsset etc.: that class is in the editor assembly, which this one cannot see.
    static SerializedObject Asset(string path) => new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path));

    [Test]
    public void TheWebTierIsForwardWithoutDepthOrOpaqueCopiesAndOneHardCascade()
    {
        var web = Asset("Assets/Settings/Mobile_RPAsset.asset");
        Assert.IsFalse(web.FindProperty("m_RequireDepthTexture").boolValue, "no depth copy on the web");
        Assert.IsFalse(web.FindProperty("m_RequireOpaqueTexture").boolValue, "no opaque copy on the web");
        Assert.AreEqual(1, web.FindProperty("m_ShadowCascadeCount").intValue);
        Assert.AreEqual(QualityTierValues.WebShadowMap, web.FindProperty("m_MainLightShadowmapResolution").intValue);
        Assert.IsFalse(web.FindProperty("m_SoftShadowsSupported").boolValue);
        Assert.AreEqual(QualityTierValues.WebRenderScale, web.FindProperty("m_RenderScale").floatValue, 1e-4f);
        var renderer = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset"));
        Assert.AreEqual(0, renderer.FindProperty("m_RenderingMode").intValue, "Forward on WebGL (research notes 2.1)");
        var webData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
        Assert.IsFalse(webData.rendererFeatures.Any(f => f != null && f.isActive && f.GetType().Name == "ScreenSpaceAmbientOcclusion"), "no SSAO on the web");
        StringAssert.IsMatch(@"(?m)^\s+WebGL: 0\s*$", System.IO.File.ReadAllText("ProjectSettings/QualitySettings.asset"), "WebGL runs the Mobile tier (level 0)");
    }

    [Test]
    public void TheDesktopTierHasSoftCascadesAndNoSsao()
    {
        var pc = Asset("Assets/Settings/PC_RPAsset.asset");
        Assert.AreEqual(QualityTierValues.DesktopCascades, pc.FindProperty("m_ShadowCascadeCount").intValue, "F6: two cascades (were four)");
        Assert.AreEqual(QualityTierValues.DesktopCascade2Split, pc.FindProperty("m_Cascade2Split").floatValue, 1e-4f);
        Assert.AreEqual(1f, pc.FindProperty("m_RenderScale").floatValue, 1e-4f, "desktop renders 1:1");
        Assert.AreEqual(QualityTierValues.DesktopShadowMap, pc.FindProperty("m_MainLightShadowmapResolution").intValue);
        Assert.IsTrue(pc.FindProperty("m_SoftShadowsSupported").boolValue);
        Assert.IsFalse(pc.FindProperty("m_RequireDepthTexture").boolValue);
        Assert.IsFalse(pc.FindProperty("m_RequireOpaqueTexture").boolValue);
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        foreach (var f in data.rendererFeatures.Where(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"))
            Assert.IsFalse(f.isActive, "SSAO off: the baked AO replaces it");
    }

    [Test]
    public void BothTiersRunTheTiltShiftAtTheDecidedResolution()
    {
        foreach (var (path, downsample) in new[] { ("Assets/Settings/Mobile_Renderer.asset", QualityTierValues.WebTiltShiftDownsample), ("Assets/Settings/PC_Renderer.asset", QualityTierValues.DesktopTiltShiftDownsample) })
        {
            var f = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path).rendererFeatures.OfType<TiltShiftFeature>().Single();
            Assert.IsTrue(f.isActive, path); Assert.AreEqual(downsample, f.Downsample, path);
        }
    }
    /// <summary>The pipeline assets' own volume profile: bloom as decided (rendered, the template's 0.25 above 1 changed at
    /// most 2/255 of any pixel at either pose on either tier: measured in commit 8d45a93, docs/visual-v1.md "Tier decisions"), and the
    /// Neutral tonemapper kept, which keeps its variants in builds (GradeTests).</summary>
    [Test]
    public void BothTiersBloomAsDecidedAndKeepTheNeutralTonemapper()
    {
        foreach (var path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
        {
            var profile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path).volumeProfile;
            Assert.IsNotNull(profile, path);
            bool blooms = profile.TryGet<Bloom>(out var bloom) && bloom.active && bloom.intensity.value > 0f;
            Assert.AreEqual(QualityTierValues.Bloom, blooms, path);
            Assert.IsTrue(profile.TryGet<Tonemapping>(out var tm) && tm.active && tm.mode.value == TonemappingMode.Neutral, path);
        }
    }
}
