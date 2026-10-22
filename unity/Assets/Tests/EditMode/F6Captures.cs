#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Boot;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>Not a check: F6's screens as a player sees them, for review, at 1920x1080 in Arabic and English, into
/// STORY_F6_DIR: the top bar over the map in 1948, the pause menu, its save and load slots (a game saved in slot 1), a
/// confirm, and each Settings tab. The UI is laid over the camera's render as FamilyCaptures does. Run on demand:
/// -runTests -testPlatform EditMode -testFilter F6Captures.</summary>
public class F6Captures
{
    const int W = 1920, H = 1080;

    [UnityTest, Explicit]
    public IEnumerator Capture()
    {
        var dir = Environment.GetEnvironmentVariable("STORY_F6_DIR") ?? Path.Combine(Path.GetTempPath(), "storyoftheland-f6");
        Directory.CreateDirectory(dir);
        var inputs = GameInputs.FromDisk(Paths.Content, Paths.StreamingMap);
        foreach (var lang in new[] { "ar", "en" })
        {
            var saves = Path.Combine(Path.GetTempPath(), "storyoftheland-f6-saves-" + Guid.NewGuid().ToString("N"));
            EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
            var map = Object.FindFirstObjectByType<MapScene>(); map.Build(inputs.Map);
            MapShaderGlobals.PinWaterTime(100f);
            var doc = Object.FindFirstObjectByType<UIDocument>();
            var rt = new RenderTexture(W, H, 24);
            var ps = Object.Instantiate(doc.panelSettings); ps.targetTexture = rt; ps.clearColor = true; ps.colorClearValue = Color.clear;
            doc.panelSettings = ps;
            var root = Object.FindFirstObjectByType<GameRoot>();
            root.Store = new SaveStore(saves); root.Language = lang;
            var ui = doc.rootVisualElement;
            var cam = Camera.main!;
            IEnumerator Shot(string name)
            {
                for (int i = 0; i < 8; i++)
                {
                    typeof(GameRoot).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(root, null);
                    yield return null;
                }
                FamilyCaptures.Save(Path.Combine(dir, $"{lang}-{name}.png"), cam, rt);
            }
            try
            {
                root.Begin(inputs, 0, startScreen: true);
                UiTestKit.Press(ui.Q<Button>("start-new-game"));
                UiTestKit.Press(ui.Q<Button>("chapter-1947_1949"));
                UiTestKit.Press(ui.Q<Button>("village-tantura"));
                var bridge = root.Bridge!;
                while (bridge.State.Date < new DateTime(1948, 4, 1)) bridge.EndTurn();
                if (root.Card!.Shown != null) UiTestKit.Press(ui.Q<Button>("event-hide"));
                yield return Shot("01-topbar-1948");
                root.Pause!.Open();
                yield return Shot("02-pause");
                UiTestKit.Press(ui.Q<Button>("pause-save"));
                UiTestKit.Press(ui.Q<Button>("pause-slot-1"));
                yield return Shot("03-save-slots");
                UiTestKit.Press(ui.Q<Button>("pause-slots-back"));
                UiTestKit.Press(ui.Q<Button>("pause-load"));
                yield return Shot("04-load-slots");
                UiTestKit.Press(ui.Q<Button>("pause-slots-back"));
                UiTestKit.Press(ui.Q<Button>("pause-main-menu"));
                yield return Shot("05-confirm-main-menu");
                UiTestKit.Press(ui.Q<Button>("pause-confirm-no"));
                UiTestKit.Press(ui.Q<Button>("pause-settings"));
                int n = 6;
                foreach (var tab in SettingsPresenter.Tabs)
                {
                    UiTestKit.Press(ui.Q<Button>("settings-tab-" + tab));
                    yield return Shot($"{n++:00}-settings-{tab}");
                }
            }
            finally
            {
                MapShaderGlobals.PinWaterTime(null);
                SettingsPresenter.Reset();
                doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel);
                Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (Directory.Exists(saves)) Directory.Delete(saves, true);
            }
        }
    }

    /// <summary>Playtest 2 at 1280x720, Arabic and English: the top bar (each meter's name under it) over the map with a card
    /// open and its first choice with effects focused (what it affects, in the card's foot), into STORY_F6_DIR as
    /// p2-&lt;lang&gt;-hud-card.png. Run on demand: -testFilter F6Captures.CapturePlaytest2.</summary>
    [UnityTest, Explicit]
    public IEnumerator CapturePlaytest2()
    {
        var dir = Environment.GetEnvironmentVariable("STORY_F6_DIR") ?? Path.Combine(Path.GetTempPath(), "storyoftheland-f6");
        Directory.CreateDirectory(dir);
        var inputs = GameInputs.FromDisk(Paths.Content, Paths.StreamingMap);
        foreach (var (w, h) in new[] { (1280, 720), (1920, 1080) })
        foreach (var lang in new[] { "ar", "en" })
        {
            var saves = Path.Combine(Path.GetTempPath(), "storyoftheland-f6p2-saves-" + Guid.NewGuid().ToString("N"));
            EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
            var map = Object.FindFirstObjectByType<MapScene>(); map.Build(inputs.Map);
            MapShaderGlobals.PinWaterTime(100f);
            var doc = Object.FindFirstObjectByType<UIDocument>();
            var rt = new RenderTexture(w, h, 24);
            var ps = Object.Instantiate(doc.panelSettings); ps.targetTexture = rt; ps.clearColor = true; ps.colorClearValue = Color.clear;
            doc.panelSettings = ps;
            var root = Object.FindFirstObjectByType<GameRoot>();
            root.Store = new SaveStore(saves); root.Language = lang;
            var ui = doc.rootVisualElement;
            try
            {
                root.Begin(inputs, 0);
                var card = root.Card!;
                for (int guard = 0; guard < 30 && (card.Shown == null || !card.Shown.Options.Any(o => o.Effects.Count > 0)); guard++)
                    FamilyKit.PlayMonth(root.Bridge!);
                var with = card.Shown?.Options.FirstOrDefault(o => o.Effects.Count > 0);
                if (with != null) ui.Q<Button>("option-" + with.Id).Focus();
                for (int i = 0; i < 8; i++)
                {
                    typeof(GameRoot).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(root, null);
                    yield return null;
                }
                FamilyCaptures.Save(Path.Combine(dir, $"p2-{lang}-{w}-hud-card.png"), Camera.main!, rt);
            }
            finally
            {
                MapShaderGlobals.PinWaterTime(null);
                doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel);
                Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (Directory.Exists(saves)) Directory.Delete(saves, true);
            }
        }
    }
}
