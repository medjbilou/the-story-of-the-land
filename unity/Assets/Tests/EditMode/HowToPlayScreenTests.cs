#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>The short How to play screen (#1041) of the family game: side-menu navigation, its six lines in both
/// languages, and its two required layouts.</summary>
public class HowToPlayScreenTests
{
    static UiText En => FamilyKit.En;
    static UiText Ar => FamilyKit.Ar;

    [Test]
    public void TheSideMenuOpensAndClosesTheScreenInBothLanguages([Values(false, true)] bool arabic)
    {
        var text = arabic ? Ar : En;
        using var ui = new UiTestKit();
        using var nav = new ScreenNav(ui.Root);
        using var guide = new HowToPlayPresenter(ui.Root, text);
        var screen = ui.Q<VisualElement>("screen-how-to-play");

        Assert.IsNotNull(ui.Q<VisualElement>("left-column").Q<Button>("nav-how-to-play"), "its button is in the left column, with Why and Settings");
        Assert.IsFalse(ScreenNav.IsOpen(screen), "hidden at start");
        ui.Press("nav-how-to-play");
        Assert.IsTrue(ScreenNav.IsOpen(screen), "the side-menu button opens it");
        Assert.AreEqual("how-to-play", nav.Current);
        Assert.AreEqual(text["how_to_play.title"], ui.Q<Label>("how-to-play-title").text);
        // The family game's turn loop, one line per key: the month, the cards, the bar, End turn, Why, the narration.
        CollectionAssert.AreEqual(new[] { "turn", "cards", "bar", "end_turn", "why", "narration" }, HowToPlayPresenter.Keys);
        foreach (var key in HowToPlayPresenter.Keys)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(text["how_to_play." + key]), key);
            Assert.AreEqual(text["how_to_play." + key], ui.Q<Label>("how-to-play-" + key.Replace('_', '-')).text, key);
        }
        Assert.AreEqual(HowToPlayPresenter.Keys.Length, ui.Q<ScrollView>("how-to-play-body").Query<Label>(className: "how-to-play-line").ToList().Count, "six lines, no more");
        ui.Press("nav-how-to-play");
        Assert.IsFalse(ScreenNav.IsOpen(screen), "pressing the same button closes it");
        Assert.IsNull(nav.Current);
    }

    [UnityTest]
    public IEnumerator TheGuideFitsLaptopAndPhoneInBothLanguages()
    {
        var problems = new List<string>();
        foreach (var arabic in new[] { false, true })
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(390, 844) })
                yield return Rendered(size.x, size.y, arabic, root =>
                {
                    string at = $"{(arabic ? "ar" : "en")} {size.x}x{size.y}";
                    var screen = root.Q("screen-how-to-play").worldBound;
                    var window = root.worldBound;
                    if (!(screen.width > 0 && screen.height > 0)) problems.Add($"{at}: screen is not visible");
                    if (screen.xMin < window.xMin || screen.yMin < window.yMin || screen.xMax > window.xMax || screen.yMax > window.yMax)
                        problems.Add($"{at}: screen {screen} leaves window {window}");
                    var body = root.Q<ScrollView>("how-to-play-body").worldBound;
                    foreach (var line in root.Q<ScrollView>("how-to-play-body").Query<Label>().ToList())
                    {
                        if (string.IsNullOrWhiteSpace(line.text)) problems.Add($"{at}: {line.name} is empty");
                        if (line.worldBound.xMin < body.xMin - 1 || line.worldBound.xMax > body.xMax + 1)
                            problems.Add($"{at}: {line.name} {line.worldBound} overflows body {body}");
                    }
                });
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    static IEnumerator Rendered(int width, int height, bool arabic, System.Action<VisualElement> check)
    {
        var rt = new RenderTexture(width, height, 0);
        var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
        ps.targetTexture = rt;
        var go = new GameObject("HowToPlayLayoutTest");
        go.SetActive(false);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = ps;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
        go.SetActive(true);
        var root = doc.rootVisualElement;
        root.Q("root").RemoveFromClassList("loading");
        if (arabic) root.Q("root").AddToClassList(UiLanguage.ArabicClass);
        var text = arabic ? Ar : En;
        var bridge = FamilyKit.Bridge(text);
        var hud = new HudPresenter(root, bridge, text);
        var guide = new HowToPlayPresenter(root, text);
        var nav = new ScreenNav(root);
        nav.Open("how-to-play");
        try
        {
            for (int i = 0; i < 30 && !(root.Q("screen-how-to-play").layout.width > 0); i++) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
            check(root);
        }
        finally
        {
            nav.Dispose(); guide.Dispose(); hud.Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}
