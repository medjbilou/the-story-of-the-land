#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.Rendering.Universal;

/// <summary>F6 (2026-10-20): `git checkout -- unity/Assets/Settings unity/ProjectSettings` once reset the settings to an
/// old commit, bringing SSAO back into PC_Renderer (on Unity 6.6 a renderer with an SSAO feature blanks every frame of the
/// player: QualityTiers.Remove) and a wrong product name and bundle id. These fail on such a reset; the fix is
/// QualityTiers.RestoreAll.</summary>
public class SettingsGuardTests
{
    static string Const(string name) =>
        (string)Type.GetType("StoryOfTheLand.Game.EditorTools.ProjectSetup, Assembly-CSharp-Editor")!.GetField(name)!.GetValue(null)!;

    [TestCase("Assets/Settings/PC_Renderer.asset")]
    [TestCase("Assets/Settings/Mobile_Renderer.asset")]
    public void NoRendererHasAnSsaoFeature(string path)
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        Assert.IsNotNull(data, path);
        CollectionAssert.IsEmpty(data.rendererFeatures.Where(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion").ToList(),
            path + ": run QualityTiers.RestoreAll");
    }

    [Test]
    public void ThePlayerHasTheGamesNameAndId()
    {
        Assert.AreEqual(Const("ProductName"), PlayerSettings.productName, "run QualityTiers.RestoreAll");
        Assert.AreEqual(Const("ProductName"), PlayerSettings.companyName);
        Assert.AreEqual(Const("AppId"), PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone));
        Assert.IsFalse(PlayerSettings.SplashScreen.show, "F6 round 4: no splash screen holds the start (ProjectSetup.Apply)");
    }
}
