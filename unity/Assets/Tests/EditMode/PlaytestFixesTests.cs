#nullable enable
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>F6, a first-time player's playtest (web build, 1600x900): the card's count of other cards in proper singular and
/// plural (Arabic by its own number rule), a choice's effects as chips under its outcome, and the money as
/// its sign alone in the bar. The chapter's end, the first-turn hint, the quiet month and the pause menu's Chapters are in
/// PauseMenuTests.</summary>
public class PlaytestFixesTests
{
    string dir = "";

    [SetUp] public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "storyoftheland-playtest-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    [Test]
    public void TheOtherCardsAreCountedInProperSingularAndPlural()
    {
        Assert.AreEqual("1 more card this turn", FamilyKit.En.Plural("event.more", 1));
        Assert.AreEqual("2 more cards this turn", FamilyKit.En.Plural("event.more", 2));
        var ar = FamilyKit.Ar;
        Assert.AreEqual("بطاقة أخرى في هذا الدور", ar.Plural("event.more", 1));
        Assert.AreEqual("بطاقتان أخريان في هذا الدور", ar.Plural("event.more", 2));
        StringAssert.Contains("بطاقات", ar.Plural("event.more", 3), "3-10: plural");
        StringAssert.Contains("بطاقةً", ar.Plural("event.more", 11), "11-99: singular, accusative");
    }

    /// <summary>Playtest 2: the hint explains the coloured changes, and quiet months are counted in all five Arabic forms.</summary>
    [Test]
    public void TheHintExplainsTheChangesAndQuietMonthsAreCounted()
    {
        using var ui = new UiTestKit();
        var bridge = FamilyKit.Bridge();
        using var nav = new ScreenNav(ui.Root);
        using var pause = new PausePresenter(ui.Root, FamilyKit.En, nav, new SaveStore(dir).Family, FamilyKit.Chapter, () => bridge.Session,
            _ => { }, () => { }, null, _ => { });
        Assert.AreEqual(FamilyKit.En["hint.delta"], ui.Q<Label>("pause-hint-delta").text);
        Assert.AreEqual("1 quiet month passed.", FamilyKit.En.Plural("skip.passed", 1));
        Assert.AreEqual("5 quiet months passed.", FamilyKit.En.Plural("skip.passed", 5));
        var ar = FamilyKit.Ar;
        Assert.AreEqual("مرّ شهر هادئ.", ar.Plural("skip.passed", 1));
        Assert.AreEqual("مرّ شهران هادئان.", ar.Plural("skip.passed", 2));
        StringAssert.Contains("أشهر هادئة", ar.Plural("skip.passed", 4));
        StringAssert.Contains("شهرًا هادئًا", ar.Plural("skip.passed", 12));
    }

    [Test]
    public void TheMoneyShowsItsSignInTheBarAndItsNameInTheTip([Values(false, true)] bool arabic)
    {
        var bridge = FamilyKit.Bridge(arabic ? FamilyKit.Ar : FamilyKit.En);
        var cur = Family.CurrencyAt(bridge.Content, bridge.State)!;
        Assert.AreEqual(arabic ? cur.SymbolAr : cur.Symbol, HudPresenter.CurrencySign(bridge.Content, bridge.State, arabic));
        StringAssert.Contains(arabic ? cur.NameAr : cur.Name, HudPresenter.CurrencyTip(bridge.Content, bridge.State, arabic ? FamilyKit.Ar : FamilyKit.En));
    }

    /// <summary>A choice with effects, once made, shows what it changes under its outcome: a chip per effect (the resource's
    /// name and its change, as the sim applies them), in both languages. (Not under every choice: that made the tallest
    /// card reach the toast's place, DecisionCardTests.)</summary>
    [Test]
    public void EachChoiceShowsWhatItChanges([Values(false, true)] bool arabic)
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        Object.FindFirstObjectByType<MapScene>().Build(FamilyKit.Inputs.Map);
        var game = Object.FindFirstObjectByType<GameRoot>();
        game.Store = new SaveStore(dir); game.Language = arabic ? UiLanguage.Arabic : null;
        game.Begin(FamilyKit.Inputs, 0);
        var ui = Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
        var text = arabic ? FamilyKit.Ar : FamilyKit.En;
        EventOption? option = null;
        for (int guard = 0; guard < 30 && game.Bridge!.State.GameOver == null; guard++)
        {
            option = game.Card!.Shown?.Options.FirstOrDefault(o => o.Effects.Count > 0);
            if (option != null) break;
            FamilyKit.PlayMonth(game.Bridge);
        }
        Assert.IsNotNull(option, "precondition: a card with a choice that has effects");
        var c = game.Bridge!.Content;
        Assert.IsNull(ui.Q("event-effects"), "no chips before the choice");
        UiTestKit.Press(ui.Q<Button>("option-" + option!.Id));
        Assert.IsNotNull(ui.Q<Button>("event-continue"), "a choice with effects stays up with what followed");
        Assert.AreEqual(text["event.effects"], ui.Q<Label>("event-effects-caption").text, "the outcome names what the choice changes");
        var row = ui.Q("event-effects");
        Assert.AreEqual(option.Effects.Count, row.childCount);
        foreach (var fx in option.Effects)
        {
            var chip = row.Q("event-effects-" + fx.Target);
            Assert.AreEqual(FamilyGame.Resource(c, fx.Target, arabic), chip.Q<Label>(className: "effect-name").text);
            Assert.AreEqual(PlayerText.PercentChange(fx.Delta, 0), chip.Q<Label>(className: "effect-delta").text);
        }
    }
}
