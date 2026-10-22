#nullable enable
using NUnit.Framework;
using StoryOfTheLand.Game.Map;
using UnityEditor;
using UnityEngine;

/// <summary>F7 (the owner, 21 Oct 2026: "animations and cool stuff and keep the game interactive with player not freezed or
/// static"): the trees' wind sway, the drifting cloud shadows and the camera's glide to a place, each still under reduced
/// motion and absent from edit-mode renders.</summary>
public class F7AmbientTests
{
    bool motion; float? pinned;

    [SetUp]
    public void Save() { motion = GraphicsQuality.ReducedMotion; pinned = MapShaderGlobals.PinnedWaterTime; }

    [TearDown]
    public void Restore()
    {
        GraphicsQuality.ReducedMotion = motion;
        MapShaderGlobals.PinWaterTime(pinned);
        MapShaderGlobals.SetSky(false, false, 0f);
    }

    /// <summary>The trims (F7 quiet profile): the sky moves only in play on the desktop tier (Low keeps its budget), the
    /// clouds not at the overview, and reduced motion stills the trees.</summary>
    [Test]
    public void TheSkyMovesOnlyInPlayOnTheDesktopTierAndNotAtTheOverview()
    {
        GraphicsQuality.ReducedMotion = false;
        (float sway, float cloud) Sky() => (Shader.GetGlobalVector("_ScWind").y, Shader.GetGlobalVector("_ScCloud").x);
        MapShaderGlobals.SetSky(true, true, 60000f);
        Assert.AreEqual((1f, MapShaderGlobals.CloudStrength), Sky(), "desktop, gameplay zoom");
        MapShaderGlobals.SetSky(true, true, MapShaderGlobals.CloudFarM + 1f);
        Assert.AreEqual((1f, 0f), Sky(), "the overview: no clouds");
        MapShaderGlobals.SetSky(true, false, 60000f);
        Assert.AreEqual((0f, 0f), Sky(), "the Low tier: still");
        MapShaderGlobals.SetSky(false, true, 60000f);
        Assert.AreEqual((0f, 0f), Sky(), "edit mode: as before");
        GraphicsQuality.ReducedMotion = true;
        MapShaderGlobals.SetSky(true, true, 60000f);
        Assert.AreEqual(0f, Sky().sway, "reduced motion stills the trees");
    }

    /// <summary>Only the trees sway: the tree material carries the shear, the kit material none (so no building moves).</summary>
    [Test]
    public void OnlyTheTreesSway()
    {
        var tree = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tree.mat");
        var kit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Kit.mat");
        Assert.IsTrue(tree.HasProperty("_Sway"), "Kit.shader has the sway");
        Assert.Greater(tree.GetFloat("_Sway"), 0f, "the trees sway");
        Assert.Less(tree.GetFloat("_Sway"), 0.05f, "gently: under about 3 degrees");
        Assert.AreEqual(0f, kit.GetFloat("_Sway"), "the buildings stand still");
    }

    /// <summary>The wind's clock is the water's: pinned (a capture, reduced motion) it holds the trees at one pose; and the
    /// sway is off unless asked for (edit-mode renders draw the trees as before).</summary>
    [Test]
    public void TheWindFollowsTheWatersClockAndIsOffUnlessAsked()
    {
        MapShaderGlobals.PinWaterTime(42f);
        MapShaderGlobals.SetWind(true);
        var w = Shader.GetGlobalVector("_ScWind");
        Assert.AreEqual(42f, w.x, 1e-4f); Assert.AreEqual(1f, w.y);
        MapShaderGlobals.SetWind(false);
        Assert.AreEqual(0f, Shader.GetGlobalVector("_ScWind").y, "no sway");
        Assert.Greater(MapShaderGlobals.CloudStrength, 0f); Assert.LessOrEqual(MapShaderGlobals.CloudStrength, 0.25f, "a soft shade");
    }

    /// <summary>The glide: smooth to the place over EaseSeconds, stopped where it is by the player's own input, a jump under
    /// reduced motion.</summary>
    [Test]
    public void TheCameraGlidesToAPlaceAndThePlayerCanStopIt()
    {
        var go = new GameObject("rig", typeof(Camera));
        try
        {
            var rig = go.AddComponent<CameraRig>();
            GraphicsQuality.ReducedMotion = false;
            rig.LookAt = Vector3.zero;
            var place = new Vector3(10000f, 0f, 0f);
            rig.EaseTo(place);
            Assert.IsTrue(rig.Easing);
            rig.StepEase(CameraRig.EaseSeconds / 2f, moved: false);
            Assert.AreEqual(5000f, rig.LookAt.x, 1f, "halfway at half time (smoothstep)");
            rig.StepEase(CameraRig.EaseSeconds, moved: false);
            Assert.AreEqual(place.x, rig.LookAt.x, 1e-3f); Assert.IsFalse(rig.Easing);
            rig.EaseTo(Vector3.zero);
            rig.StepEase(0.1f, moved: false);
            float at = rig.LookAt.x;
            rig.StepEase(0.1f, moved: true);
            Assert.AreEqual(at, rig.LookAt.x, "the player's input stops it where it is");
            Assert.IsFalse(rig.Easing);
            GraphicsQuality.ReducedMotion = true;
            rig.EaseTo(place);
            Assert.AreEqual(place.x, rig.LookAt.x, 1e-3f, "reduced motion: there at once");
            Assert.IsFalse(rig.Easing);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
