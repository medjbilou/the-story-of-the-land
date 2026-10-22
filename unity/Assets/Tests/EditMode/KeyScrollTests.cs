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

/// <summary>Accessibility audit H3 (#41): PageUp, PageDown, Home and End scroll the open screen's list, or the open decision
/// card's text, from the keyboard (KeyScroll), since those panels hold labels that Tab never reaches and the arrows pan the
/// camera.</summary>
public class KeyScrollTests
{
    static UiText Text => FamilyKit.En;

    /// <summary>A real panel in the playtest window (1568x752) with the game's UXML.</summary>
    sealed class Rig : IDisposable
    {
        public readonly VisualElement Root;
        readonly GameObject go; readonly PanelSettings ps; readonly RenderTexture rt;
        readonly List<IDisposable> owned = new List<IDisposable>();

        public Rig()
        {
            rt = new RenderTexture(1568, 752, 0);
            ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            ps.targetTexture = rt;
            go = new GameObject("KeyScrollTest");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            Root = doc.rootVisualElement;
            Root.Q("root")?.RemoveFromClassList("loading");
        }

        public T Own<T>(T d) where T : IDisposable { owned.Add(d); return d; }

        public void Dispose()
        {
            for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
        }
    }

    static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    /// <summary>Holds a view to <paramref name="height"/> px, so its content overflows whatever the window.</summary>
    static void Shorten(ScrollView view, float height)
    {
        view.style.height = height; view.style.minHeight = height; view.style.maxHeight = height; view.style.flexGrow = 0;
    }

    /// <summary>PageDown moves one view less a line, End reaches the bottom, PageUp moves back and Home reaches the top; at
    /// the top PageUp does nothing. The focus never moves.</summary>
    static void PagesThrough(VisualElement root, ScrollView view, string? screen, bool cardOpen, string where)
    {
        var focused = root.panel.focusController.focusedElement;
        float high = view.verticalScroller.highValue, page = Mathf.Max(24f, view.contentViewport.layout.height - 24f);
        Assert.Greater(high, page, where + ": precondition, the view overflows by more than a page");
        Assert.AreEqual(0f, view.scrollOffset.y, 0.5f, where + ": precondition, at the top");
        Assert.IsTrue(KeyScroll.Press(root, screen, cardOpen, KeyScroll.Key.PageDown), where + ": PageDown scrolls");
        Assert.AreEqual(Mathf.Min(page, high), view.scrollOffset.y, 0.5f, where + ": PageDown moves one view less a line");
        Assert.IsTrue(KeyScroll.Press(root, screen, cardOpen, KeyScroll.Key.End), where + ": End scrolls");
        Assert.AreEqual(high, view.scrollOffset.y, 0.5f, where + ": End reaches the bottom");
        Assert.IsFalse(KeyScroll.Press(root, screen, cardOpen, KeyScroll.Key.PageDown), where + ": PageDown at the bottom");
        Assert.IsTrue(KeyScroll.Press(root, screen, cardOpen, KeyScroll.Key.PageUp), where + ": PageUp scrolls");
        Assert.AreEqual(high - page, view.scrollOffset.y, 0.5f, where + ": PageUp moves one view less a line");
        Assert.IsTrue(KeyScroll.Press(root, screen, cardOpen, KeyScroll.Key.Home), where + ": Home scrolls");
        Assert.AreEqual(0f, view.scrollOffset.y, 0.5f, where + ": Home reaches the top");
        Assert.IsFalse(KeyScroll.Press(root, screen, cardOpen, KeyScroll.Key.PageUp), where + ": PageUp at the top");
        Assert.AreSame(focused, root.panel.focusController.focusedElement, where + ": the keys move no focus");
    }

    [UnityTest]
    public IEnumerator ThePageKeysScrollTheWhyListHowToPlayAndPeople()
    {
        using var r = new Rig();
        var bridge = FamilyKit.Bridge();
        r.Own(new PeoplePresenter(r.Root, Figures.Parse(FamilyKit.Inputs.Read(Figures.FileName)), GameRoot.AutomatedChapter, Text));
        var nav = r.Own(new ScreenNav(r.Root));
        r.Own(new WhyPresenter(r.Root, bridge, Text));
        r.Own(new HowToPlayPresenter(r.Root, Text));
        FamilyKit.PlayMonth(bridge);
        foreach (var (screen, view) in new[] { ("why", "why-rows"), ("how-to-play", "how-to-play-body"), (PeoplePresenter.Screen, "people-list") })
        {
            var v = r.Root.Q<ScrollView>(view);
            Shorten(v, 80f);
            nav.Open(screen);
            yield return Frames(4);
            PagesThrough(r.Root, v, screen, false, screen);
        }
    }

    /// <summary>Playtest: the Arabic People screen opened part-way down, its first card cut off. Every opening starts at
    /// the top, in both languages.</summary>
    [UnityTest] public IEnumerator PeopleOpensAtTheFirstCardInEnglish() => PeopleOpensAtTheTop(false);
    [UnityTest] public IEnumerator PeopleOpensAtTheFirstCardInArabic() => PeopleOpensAtTheTop(true);

    static IEnumerator PeopleOpensAtTheTop(bool arabic)
    {
        using var r = new Rig();
        if (arabic) r.Root.Q("root")?.AddToClassList(UiLanguage.ArabicClass);
        r.Own(new PeoplePresenter(r.Root, Figures.Parse(FamilyKit.Inputs.Read(Figures.FileName)), GameRoot.AutomatedChapter, arabic ? FamilyKit.Ar : Text));
        var nav = r.Own(new ScreenNav(r.Root));
        var v = r.Root.Q<ScrollView>("people-list");
        Shorten(v, 80f);
        nav.Open(PeoplePresenter.Screen);
        yield return Frames(4);
        Assert.Greater(v.verticalScroller.highValue, 0f, "precondition: the list overflows");
        Assert.AreEqual(0f, v.scrollOffset.y, 0.5f, "the first opening");
        v.scrollOffset = new Vector2(0f, v.verticalScroller.highValue);
        nav.Open(null);
        yield return Frames(2);
        nav.Open(PeoplePresenter.Screen);
        Assert.AreEqual(0f, v.scrollOffset.y, 0.5f, "at once");
        yield return Frames(4);
        Assert.AreEqual(0f, v.scrollOffset.y, 0.5f, "after the layout");
    }

    [UnityTest]
    public IEnumerator TheCardsTextScrollsOnlyWhileTheCardIsOpenAndNoScreenCoversIt()
    {
        using var r = new Rig();
        var bridge = FamilyKit.Bridge();
        var nav = r.Own(new ScreenNav(r.Root));
        r.Own(new WhyPresenter(r.Root, bridge, Text));
        var card = r.Own(new EventCardPresenter(r.Root, bridge, Text));
        var text = r.Root.Q<ScrollView>("event-scroll");
        Shorten(text, 16f);
        yield return Frames(4);
        Assert.IsNotNull(card.Shown, "precondition: the card is open");
        Assert.IsFalse(card.Collapsed, "precondition: the card is not hidden");
        Assert.Greater(text.worldBound.height, 0f, "precondition: the card's text is laid out");
        Assert.IsFalse(KeyScroll.Press(r.Root, null, false, KeyScroll.Key.PageDown), "a hidden card's text does not scroll");
        Assert.AreEqual(0f, text.scrollOffset.y, 0.5f, "hidden card");
        nav.Open("why");
        yield return Frames(2);
        KeyScroll.Press(r.Root, "why", true, KeyScroll.Key.PageDown);
        Assert.AreEqual(0f, text.scrollOffset.y, 0.5f, "an open screen takes the keys, not the card under it");
        nav.Open(null);
        yield return Frames(4);
        PagesThrough(r.Root, text, null, true, "event text");
    }

    [UnityTest]
    public IEnumerator NothingScrollsUnderTheStartScreen()
    {
        using var r = new Rig();
        var bridge = FamilyKit.Bridge();
        var nav = r.Own(new ScreenNav(r.Root));
        r.Own(new WhyPresenter(r.Root, bridge, Text));
        FamilyKit.PlayMonth(bridge);
        var rows = r.Root.Q<ScrollView>("why-rows");
        Shorten(rows, 40f);
        nav.Open("why");
        r.Root.Q("root").AddToClassList(StartScreenPresenter.OpenClass);
        yield return Frames(4);
        Assert.Greater(rows.verticalScroller.highValue, 0f, "precondition: the list overflows");
        foreach (KeyScroll.Key key in Enum.GetValues(typeof(KeyScroll.Key)))
            Assert.IsFalse(KeyScroll.Press(r.Root, "why", false, key), key + " under the start screen");
        Assert.AreEqual(0f, rows.scrollOffset.y, 0.5f, "the list did not move");
    }
}
