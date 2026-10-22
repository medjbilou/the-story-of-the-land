#nullable enable
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>Each top-bar number explains itself (#1037, MeterTip): on hover, or after a tap, a tip under the bar gives the
/// meter in words (its tooltip), inside the window. Its second line (playtest 3) says what the resource means
/// (HudModel.Explained); the family's month is told on its cards and in Why.</summary>
public class MeterTipTests
{

    static void Enter(VisualElement e) { using var ev = PointerEnterEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = e.worldBound.center }); ev.target = e; e.SendEvent(ev); }
    static void Leave(VisualElement e) { using var ev = PointerLeaveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = Vector2.zero }); ev.target = e; e.SendEvent(ev); }
    static void Tap(VisualElement e) { using var ev = ClickEvent.GetPooled(); ev.target = e; e.SendEvent(ev); }

    /// <summary>Shown only when MeterTip set it so (an unset inline display reads as Flex, the default).</summary>
    static bool Shown(VisualElement tip) => tip.style.display.keyword == StyleKeyword.Undefined && tip.style.display.value == DisplayStyle.Flex;
    static string Words(VisualElement root) => root.Q<Label>("meter-tip-words").text;
    static string CausesLine(VisualElement root) => root.Q<Label>("meter-tip-causes").text;

    /// <summary>After the fixed opening month, at 1280x720 and 390x844 in English and Arabic: hovering the bar's first and
    /// last resources and the money shows each one's words and meaning under the bar and inside the window; leaving hides it.</summary>
    [UnityTest]
    public IEnumerator HoverExplainsANumberUnderTheBarInsideTheWindow()
    {
        var problems = new List<string>();
        foreach (var arabic in new[] { false, true })
            foreach (var (w, h) in new[] { (1280, 720), (390, 844) })
            {
                string at = $"{(arabic ? "ar" : "en")} {w}x{h}";
                yield return Rendered(w, h, arabic, Check);

                IEnumerator Check(VisualElement root, SimBridge bridge, UiText text)
                {
                    var tip = root.Q(MeterTip.TipName);
                    var ids = bridge.Content.Indicators;
                    foreach (var id in new[] { ids[0].Id, ids[ids.Count - 1].Id, "livelihood" })   // Livelihood carries the money now
                    {
                        var meter = root.Q("meter-family-" + id);
                        Enter(meter);
                        for (int i = 0; i < 3; i++) yield return null;   // the tip is placed once its width is laid out
                        if (!Shown(tip)) { problems.Add($"{at} {id}: hovering shows no tip"); continue; }
                        if (string.IsNullOrEmpty(meter.tooltip)) problems.Add($"{at} {id}: precondition: the meter has its words");
                        if (Words(root) != meter.tooltip) problems.Add($"{at} {id}: the tip's words are '{Words(root)}', not its tooltip '{meter.tooltip}'");
                        if (!CausesLine(root).StartsWith(text["hud.meaning." + id]) || root.Q("meter-tip-causes").style.display.value != DisplayStyle.Flex)
                            problems.Add($"{at} {id}: the second line is '{CausesLine(root)}', not what {id} means");
                        if (UiDirection.RightToLeft(Words(root)) != arabic) problems.Add($"{at} {id}: the words are not in the UI's language");
                        Rect t = tip.worldBound, m = meter.worldBound, win = root.worldBound;
                        if (t.xMin < win.xMin - 0.5f || t.xMax > win.xMax + 0.5f || t.yMax > win.yMax + 0.5f) problems.Add($"{at} {id}: the tip {t} leaves the window {win}");
                        if (t.yMin < m.yMax) problems.Add($"{at} {id}: the tip {t} is not under its number {m}");
                        Leave(meter);
                        if (Shown(tip)) problems.Add($"{at} {id}: the tip stays after the pointer leaves");
                    }
                }
            }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    /// <summary>A touchscreen has no hover: a tap pins the tip, a tap on another number moves it, a second tap closes it, and
    /// End Turn closes it.</summary>
    [UnityTest]
    public IEnumerator ATapPinsTheTipAndASecondTapOrEndTurnClosesIt()
    {
        var problems = new List<string>();
        yield return Rendered(390, 844, false, Check);

        IEnumerator Check(VisualElement root, SimBridge bridge, UiText text)
        {
            var tip = root.Q(MeterTip.TipName);
            VisualElement money = root.Q("meter-family-livelihood"), land = root.Q("meter-family-land");   // the money is in Livelihood now
            Enter(money); Tap(money); Leave(money);
            yield return null;
            if (!Shown(tip)) problems.Add("a tap does not keep the tip");
            Tap(land);
            yield return null;
            if (!Shown(tip) || Words(root) != land.tooltip) problems.Add($"a tap on another number does not move the tip: '{Words(root)}'");
            Tap(land);
            if (Shown(tip)) problems.Add("a second tap does not close the tip");
            Tap(money);
            if (!Shown(tip)) problems.Add("a tap after closing does not open it again");
            bridge.EndTurn();
            if (Shown(tip)) problems.Add("End Turn does not close the tip");
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    /// <summary>Renders Game.uxml at w x h with the HUD after one month, then runs <paramref name="check"/>, which may wait frames.</summary>
    static IEnumerator Rendered(int w, int h, bool arabic, System.Func<VisualElement, SimBridge, UiText, IEnumerator> check)
    {
        var rt = new RenderTexture(w, h, 0);
        var ps = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
        ps.targetTexture = rt;
        var go = new GameObject("MeterTipLayout"); go.SetActive(false);
        var doc = go.AddComponent<UIDocument>(); doc.panelSettings = ps;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
        go.SetActive(true);
        var root = doc.rootVisualElement;
        root.Q("root").RemoveFromClassList("loading");
        if (arabic) root.Q("root").AddToClassList(UiLanguage.ArabicClass);
        var text = arabic ? FamilyKit.Ar : FamilyKit.En;
        var bridge = FamilyKit.Bridge(text);
        var hud = new HudPresenter(root, bridge, text);
        try
        {
            FamilyKit.PlayMonth(bridge);
            for (int i = 0; i < 30 && !(root.Q("meter-family-livelihood").layout.width > 0); i++) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
            var inner = check(root, bridge, text);
            while (inner.MoveNext()) yield return inner.Current;
        }
        finally
        {
            hud.Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(ps); rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}
