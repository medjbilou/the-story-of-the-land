#nullable enable
using System.Collections.Generic;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Boot;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor;
using UnityEngine;

/// <summary>The family game for the tests: the repository's inputs and chapters (read once), the default game the automated
/// runs play (GameRoot.AutomatedChapter for a family of GameRoot.AutomatedVillage, golden seed), and the UI's strings.</summary>
public static class FamilyKit
{
    static GameInputs? inputs;
    /// <summary>The map inputs and the content reader, from the repository (as the editor's GameBoot reads them).</summary>
    public static GameInputs Inputs => inputs ??= GameInputs.FromDisk(Paths.Content, Paths.StreamingMap);

    static readonly Dictionary<string, Content> chapters = new Dictionary<string, Content>();
    /// <summary>A chapter's content (content/palestine), loaded once. Tests must not change it.</summary>
    public static Content Chapter(string id = GameRoot.AutomatedChapter) =>
        chapters.TryGetValue(id, out var c) ? c : chapters[id] = ContentLoader.LoadChapter(Paths.Content, id);

    /// <summary>A fresh family game: the automated runs' by default.</summary>
    public static GameSession Session(string chapter = GameRoot.AutomatedChapter, string village = GameRoot.AutomatedVillage, ulong seed = GoldenRun.Seed) =>
        GameSession.New(Chapter(chapter), seed, village);

    public static UiText En => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.Strings).text);
    public static UiText Ar => UiText.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiPaths.StringsArabic).text, "strings.ar.yaml");

    /// <summary>A bridge over <see cref="Session"/>, in English unless <paramref name="text"/> is given.</summary>
    public static SimBridge Bridge(UiText? text = null, string chapter = GameRoot.AutomatedChapter, string village = GameRoot.AutomatedVillage, ulong seed = GoldenRun.Seed) =>
        new SimBridge(Session(chapter, village, seed), text ?? En);

    /// <summary>Answers every open card with its first choice: End Turn then ends the turn (SimBridge.Undecided).</summary>
    public static void AnswerAll(SimBridge bridge)
    {
        foreach (var id in bridge.State.PendingEvents)
        {
            var e = System.Linq.Enumerable.First(bridge.Content.Events, x => x.Id == id);
            bridge.Queue(new ChooseEventOption(id, e.Options[0].Id));
        }
    }

    /// <summary>Answers every open card with its first choice, then ends the turn: a month as the smoke run plays it.</summary>
    public static StepResult PlayMonth(SimBridge bridge)
    {
        AnswerAll(bridge);
        return bridge.EndTurn();
    }
}

/// <summary>An order the family game does not have (it has only ChooseEventOption): SimBridge must refuse it, not throw.</summary>
public sealed class OtherOrder : Command { public override string Kind => "other"; }
