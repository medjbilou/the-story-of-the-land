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

/// <summary>Not a check: the screens as a player sees them, for review, at 1600x900 in Arabic and English. Capture (F5,
/// docs/f5-unity-integration.md step 10): the start screen, the chapter and village pickers, the family's top bar in 1948
/// (Tantura), 1967 (Imwas) and 2023 (Khan Yunis), a sensitive card with its note, a card showing its outcome, the People
/// screen and Credits, into STORY_F5_DIR. CaptureF3: the start screen, the top bar in
/// 1948, How to play, Settings and the whole map in 1948 with its front lines and their attributed legend, into
/// STORY_F3_DIR. An editor render draws no UI Toolkit, so the map is rendered by its camera and the UI into a texture
/// of its own, and the two are laid over each other. Run on demand: -runTests -testPlatform EditMode -testFilter
/// FamilyCaptures.</summary>
public class FamilyCaptures
{
    const int W = 1600, H = 900;

    [UnityTest, Explicit]
    public IEnumerator Capture()
    {
        var dir = Environment.GetEnvironmentVariable("STORY_F5_DIR") ?? Path.Combine(Path.GetTempPath(), "storyoftheland-f5");
        Directory.CreateDirectory(dir);
        var inputs = GameInputs.FromDisk(Paths.Content, Paths.StreamingMap);
        foreach (var lang in new[] { "ar", "en" })
        {
            var saves = Path.Combine(Path.GetTempPath(), "storyoftheland-f5-saves-" + Guid.NewGuid().ToString("N"));
            EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
            var map = Object.FindFirstObjectByType<MapScene>(); map.Build(inputs.Map);
            MapShaderGlobals.PinWaterTime(100f);
            var doc = Object.FindFirstObjectByType<UIDocument>();
            var rt = new RenderTexture(W, H, 24);   // a stencil for the clipped panels (the card is overflow: hidden with rounded corners)
            // The game's panel draws over the camera and never clears; into a texture of its own it must, or each shot keeps the last.
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
                    typeof(GameRoot).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(root, null);   // the pills follow the camera
                    yield return null;
                }
                Save(Path.Combine(dir, $"{lang}-{name}.png"), cam, rt);
            }
            try
            {
                root.Begin(inputs, 0, startScreen: true);
                yield return Shot("01-start");
                UiTestKit.Press(ui.Q<Button>("start-new-game"));
                yield return Shot("02-chapters");
                UiTestKit.Press(ui.Q<Button>("chapter-1947_1949"));
                yield return Shot("03-villages");
                // 1948, Tantura: the bar over the map, the card hidden.
                UiTestKit.Press(ui.Q<Button>("village-tantura"));
                var bridge = root.Bridge!;
                while (bridge.State.Date < new DateTime(1948, 4, 1)) bridge.EndTurn();
                if (root.Card!.Shown != null) UiTestKit.Press(ui.Q<Button>("event-hide"));
                yield return Shot("04-hud-1948-tantura");
                // May 1948: Tantura's cards; the first sensitive one with its note, then a card's outcome.
                bridge.EndTurn();
                var card = root.Card!;
                card.Reopen();
                for (int guard = 0; card.Shown != null && !FamilyGame.Sensitive(card.Shown) && guard < 20; guard++)
                    UiTestKit.Press(ui.Q("event-options").Q<Button>());
                if (ui.Q<Button>("event-continue") != null) UiTestKit.Press(ui.Q<Button>("event-continue"));
                yield return Shot("05-card-sensitive");
                for (int guard = 0; card.Shown != null && guard < 20; guard++)
                {
                    var e = card.Shown;
                    UiTestKit.Press(ui.Q("event-options").Q<Button>());
                    if (ui.Q<Button>("event-continue") != null) break;
                    if (card.Shown == e) break;
                }
                yield return Shot("06-card-outcome");
                if (ui.Q<Button>("event-continue") != null) UiTestKit.Press(ui.Q<Button>("event-continue"));
                // People and Credits.
                UiTestKit.Press(ui.Q<Button>(PeoplePresenter.ButtonName));
                yield return Shot("07-people");
                UiTestKit.Press(ui.Q<Button>("nav-settings"));
                UiTestKit.Press(ui.Q<Button>("settings-credits"));
                yield return Shot("08-credits");
                // 1967, Imwas, and 2023, Khan Yunis.
                foreach (var (chapter, village, name) in new[] { ("1967_1987", "imwas", "09-hud-1967-imwas"), ("2023_2026", "khan_yunis", "10-hud-2023-khan-yunis") })
                {
                    root.StartFamily(inputs, chapter, village);
                    if (root.Card!.Shown != null) UiTestKit.Press(ui.Q<Button>("event-hide"));
                    yield return Shot(name);
                }
                // The first card of the Gaza chapter, open.
                root.Card!.Reopen();
                yield return Shot("11-card-2023");
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

    [UnityTest, Explicit]
    public IEnumerator CaptureF3()
    {
        var dir = Environment.GetEnvironmentVariable("STORY_F3_DIR") ?? Path.Combine(Path.GetTempPath(), "storyoftheland-f3");
        Directory.CreateDirectory(dir);
        var inputs = GameInputs.FromDisk(Paths.Content, Paths.StreamingMap);
        foreach (var lang in new[] { "ar", "en" })
        {
            var saves = Path.Combine(Path.GetTempPath(), "storyoftheland-f3-saves-" + Guid.NewGuid().ToString("N"));
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
                Save(Path.Combine(dir, $"{lang}-{name}.png"), cam, rt);
            }
            try
            {
                root.Begin(inputs, 0, startScreen: true);
                yield return Shot("01-start");
                UiTestKit.Press(ui.Q<Button>("start-new-game"));
                UiTestKit.Press(ui.Q<Button>("chapter-1947_1949"));
                UiTestKit.Press(ui.Q<Button>("village-tantura"));
                // August 1948: the front lines of 1948 are drawn (from 15 May 1948); the card is hidden.
                var bridge = root.Bridge!;
                while (bridge.State.Date < new DateTime(1948, 8, 1)) bridge.EndTurn();
                if (root.Card!.Shown != null) UiTestKit.Press(ui.Q<Button>("event-hide"));
                yield return Shot("02-hud-1948-tantura");
                UiTestKit.Press(ui.Q<Button>("nav-how-to-play"));
                yield return Shot("03-how-to-play");
                UiTestKit.Press(ui.Q<Button>("nav-settings"));
                yield return Shot("04-settings");
                UiTestKit.Press(ui.Q<Button>("nav-settings"));
                // The whole map (the minimap's button): the front lines and, beside the minimap, their legend.
                UiTestKit.Press(ui.Q<Button>("minimap-overview"));
                yield return Shot("05-map-1948-legend");
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

    /// <summary>The camera's render with the UI's texture laid over it (the UI is drawn with premultiplied alpha).</summary>
    internal static void Save(string path, Camera cam, RenderTexture ui)
    {
        int W = ui.width, H = ui.height;   // the UI's texture is the shot's size
        var map = RenderSample.Render(cam, W, H);
        var active = RenderTexture.active;
        RenderTexture.active = ui;
        var top = new Texture2D(W, H, TextureFormat.RGBA32, false);
        top.ReadPixels(new Rect(0, 0, W, H), 0, 0); top.Apply();
        RenderTexture.active = active;
        var over = top.GetPixels();
        var px = new Color[W * H];
        for (int i = 0; i < px.Length; i++)
        {
            var u = over[i]; var m = map.Pixels[i];
            px[i] = new Color(u.r + m.r * (1f - u.a), u.g + m.g * (1f - u.a), u.b + m.b * (1f - u.a), 1f);
        }
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(top); Object.DestroyImmediate(tex);
        Debug.Log("CAPTURE " + path);
    }
}
