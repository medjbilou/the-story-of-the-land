#nullable enable
using System;
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

/// <summary>Small accessibility fixes (#41): Escape from a screen gives the focus to its left-column button (M1); text on the keyboard focus fill reaches
/// 4.5:1 (M3). The start screen and the decision card's own cases of M1 are in StartScreenTests and DecisionCardTests.</summary>
public class KeyboardFocusTests
{
    static UiText Text => FamilyKit.En;

    /// <summary>Game.uxml on a panel that renders into a texture of the window's size, so layout, style and focus run as in
    /// a player once frames pass.</summary>
    sealed class Panel : IDisposable
    {
        readonly GameObject go; readonly PanelSettings ps; readonly RenderTexture rt;
        public readonly VisualElement Root;
        public readonly List<IDisposable> Owned = new List<IDisposable>();

        public Panel(int w = 1920, int h = 1080)
        {
            rt = new RenderTexture(w, h, 0);
            ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            go = new GameObject("KeyboardFocusTest");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            Root = doc.rootVisualElement;
            Root.Q("root").RemoveFromClassList("loading");
        }

        public T Q<T>(string name) where T : VisualElement
        {
            var e = Root.Q<T>(name);
            Assert.IsNotNull(e, name);
            return e;
        }

        public Focusable? Focused => Root.panel.focusController.focusedElement;

        public void Dispose()
        {
            for (int i = Owned.Count - 1; i >= 0; i--) Owned[i].Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
        }
    }

    static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    static IEnumerator FocusOn(Panel p, VisualElement e)
    {
        e.Focus();
        for (int i = 0; i < 10 && p.Focused != e; i++) { yield return null; e.Focus(); }
    }

    /// <summary>M1: Escape from a screen whose contents had the focus gives it to the screen's left-column button, so Tab
    /// carries on from there; Escape with the focus on the top bar leaves it where it is.</summary>
    [UnityTest]
    public IEnumerator EscapeFromAScreenGivesTheFocusToItsButton()
    {
        using var p = new Panel();
        var bridge = FamilyKit.Bridge();
        p.Owned.Add(new WhyPresenter(p.Root, bridge, Text));
        p.Owned.Add(new HowToPlayPresenter(p.Root, Text));
        p.Owned.Add(new SettingsPresenter(p.Root, Text, null));
        var nav = new ScreenNav(p.Root); p.Owned.Add(nav);
        foreach (var screen in ScreenNav.Screens)
        {
            nav.Open(screen);
            yield return Frames(4);
            var inside = p.Root.Q("screen-" + screen).Query<Button>().ToList().First(b => b.enabledInHierarchy);
            yield return FocusOn(p, inside);
            Assert.AreSame(inside, p.Focused, screen + ": precondition, a control on the screen has the focus");
            using (var e = NavigationCancelEvent.GetPooled()) { e.target = inside; inside.SendEvent(e); }
            yield return Frames(3);
            Assert.IsNull(nav.Current, screen + ": Escape closed it");
            Assert.AreSame(p.Q<Button>("nav-" + screen), p.Focused, screen + ": its left-column button has the focus");
        }
        var endTurn = p.Q<Button>("end-turn");
        nav.Open("settings");
        yield return Frames(3);
        yield return FocusOn(p, endTurn);
        Assert.IsTrue(nav.Close());
        yield return Frames(3);
        Assert.AreSame(endTurn, p.Focused, "the focus was on the top bar: it stays there");
    }

    /// <summary>WCAG relative luminance, sRGB.</summary>
    static float Lum(Color c)
    {
        float Ch(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        return 0.2126f * Ch(c.r) + 0.7152f * Ch(c.g) + 0.0722f * Ch(c.b);
    }

    static float Ratio(Color a, Color b) { float x = Lum(a), y = Lum(b); return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f); }

    /// <summary>Computed contrast the focus-fill text must reach: 4.5:1 plus a margin, because anti-aliasing on the canvas
    /// takes about 0.4 off (ink at 4.60:1 computed measured 4.16 to 4.21:1 on the audit's captures).</summary>
    const float FocusTextRatio = 5f;

    /// <summary>M3: the label of every kind of button is at least 4.5:1 on the keyboard focus fill, with margin
    /// (FocusTextRatio), the gold ones (End Turn, Decision waiting) and the card's options included.</summary>
    [UnityTest]
    public IEnumerator TextOnTheFocusFillIsAtLeastFourPointFiveToOne()
    {
        using var p = new Panel();
        var host = new VisualElement();
        host.style.position = Position.Absolute; host.style.left = 40; host.style.top = 200;
        p.Q<VisualElement>("root").Add(host);
        var kinds = new[] { "", "end-turn", "event-reopen", EventCardPresenter.OptionClass };
        var buttons = kinds.Select(k =>
        {
            var b = new Button { text = "Focused " + k, name = "probe-" + (k == "" ? "plain" : k) };
            if (k != "") b.AddToClassList(k);
            // Decision waiting is hidden and absolutely placed by the stylesheet until shown.
            b.style.display = DisplayStyle.Flex; b.style.position = Position.Relative;
            host.Add(b);
            return b;
        }).ToList();
        yield return Frames(3);
        foreach (var b in buttons)
        {
            yield return FocusOn(p, b);
            yield return Frames(2);
            Assert.AreSame(b, p.Focused, b.name + ": focused");
            var fill = b.resolvedStyle.backgroundColor;
            Assert.AreEqual(1f, fill.a, 0.001f, b.name + ": the focus fill is opaque");
            var ratio = Ratio(b.resolvedStyle.color, fill);
            Assert.GreaterOrEqual(ratio, FocusTextRatio, $"{b.name}: its label is {ratio:0.00}:1 on the focus fill");
        }
    }
}
