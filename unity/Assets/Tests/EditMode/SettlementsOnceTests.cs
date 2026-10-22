#nullable enable
using System.IO;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Boot;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Issue #321 (T57 (c)): settlements_1948.yaml was parsed at boot by the map (MapScene.Build, the markers, kits
/// and footprints) and again by GameRoot, only for the settlements' names (an info panel's id-to-name). The game has no
/// info panel and GameRoot reads no names, so a boot parses the file once: the map's parse.</summary>
public class SettlementsOnceTests
{
    string saveDir = "";

    [SetUp]
    public void SetUp() => saveDir = Path.Combine(Path.GetTempPath(), "storyoftheland-settlonce-" + System.Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        VisualToggles.Clear();
        if (Directory.Exists(saveDir)) Directory.Delete(saveDir, true);
    }

    static GameInputs Inputs() => GameInputs.FromDisk(Paths.Content, Paths.StreamingMap);

    /// <summary>One parse a boot: building the map and starting the game parses the file once.</summary>
    [Test]
    public void ABootParsesTheSettlementsOnce()
    {
        var inputs = Inputs();
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>();
        int before = SettlementPlacer.Parses;
        map.Build(inputs.Map);
        var root = Object.FindFirstObjectByType<GameRoot>();
        root.Store = new SaveStore(saveDir);
        root.Begin(inputs, 0);
        Assert.AreEqual(1, SettlementPlacer.Parses - before, "settlements_1948.yaml parsed once a boot");
    }
}
