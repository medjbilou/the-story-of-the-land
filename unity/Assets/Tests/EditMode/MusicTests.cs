#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>The music (MusicPlayer): every track ships for the desktop and the web with its ASSETS.yaml row, the main theme
/// is the start screen's, a war month plays the war theme and any other month its chapter's era theme, and the Music volume
/// row in Settings is kept across a reload.</summary>
public class MusicTests
{
    [Test]
    public void EveryTrackShipsInBothFormatsWithItsProvenance()
    {
        var globs = Regex.Matches(File.ReadAllText(Path.Combine(Paths.RepoRoot, "ASSETS.yaml")), "^\\s*- glob: \"([^\"]+)\"", RegexOptions.Multiline)
            .Cast<Match>().Select(m => new Regex("^" + Regex.Escape(m.Groups[1].Value).Replace("\\*", ".*").Replace("\\?", ".") + "$")).ToList();
        Assert.AreEqual(6, MusicPlayer.Tracks.Count(), "a main theme, four era themes and the war theme");
        foreach (var track in MusicPlayer.Tracks)
            foreach (var web in new[] { false, true })
            {
                var file = MusicPlayer.File(track, web);
                Assert.IsTrue(File.Exists(Path.Combine(Application.streamingAssetsPath, "music", file)), file + " is missing");
                var path = "unity/Assets/StreamingAssets/music/" + file;
                Assert.IsTrue(globs.Any(g => g.IsMatch(path)), path + " has no ASSETS.yaml row");
            }
        foreach (var (id, _, _) in FamilyGame.Chapters) Assert.IsTrue(MusicPlayer.Eras.ContainsKey(id), id + " has no era theme");
    }

    [Test]
    public void AWarMonthPlaysTheWarThemeAndAnyOtherItsEraTheme()
    {
        foreach (var month in new[] { new DateTime(1948, 5, 1), new DateTime(1967, 6, 1), new DateTime(1982, 9, 1), new DateTime(2009, 1, 1),
                     new DateTime(2012, 11, 1), new DateTime(2014, 7, 1), new DateTime(2021, 5, 15), new DateTime(2023, 10, 1), new DateTime(2026, 3, 1) })
            Assert.AreEqual(MusicPlayer.War, MusicPlayer.Theme("1947_1949", month), $"{month:yyyy-MM} is a war month");
        Assert.AreEqual("era_1880_1946", MusicPlayer.Theme("1880_1946", new DateTime(1936, 4, 1)));
        Assert.AreEqual("era_1947_1966", MusicPlayer.Theme("1947_1949", new DateTime(1949, 8, 1)), "after the last armistice");
        Assert.AreEqual("era_1947_1966", MusicPlayer.Theme("1950_1966", new DateTime(1956, 1, 1)));
        Assert.AreEqual("era_1967_2000", MusicPlayer.Theme("1967_1987", new DateTime(1967, 7, 1)), "after June 1967");
        Assert.AreEqual("era_1967_2000", MusicPlayer.Theme("1987_2000", new DateTime(1990, 10, 1)));
        Assert.AreEqual("era_2000_2026", MusicPlayer.Theme("2023_2026", new DateTime(2023, 9, 1)), "before 7 October 2023");
        Assert.AreNotEqual(MusicPlayer.Main, MusicPlayer.War);
    }

    [Test]
    public void TheMusicVolumeIsKeptAcrossAReload()
    {
        var dir = Path.Combine(Path.GetTempPath(), "music-volume-" + Guid.NewGuid().ToString("N"));
        try
        {
            MusicPlayer.Volume = MusicPlayer.DefaultVolume;
            using (var rig = new Rig(dir))
            {
                Assert.AreEqual(FamilyKit.En.Format("settings.percent", 60), rig.Root.Q<Label>("settings-music_volume-value").text, "60% by default");
                UiTestKit.Press(rig.Root.Q<Button>("settings-music_volume-prev"));
                Assert.AreEqual(0.5f, MusicPlayer.Volume, 1e-4f, "applies at once");
                Assert.AreEqual("50", new SettingsStore(dir).Read(SettingsPresenter.MusicVolumeKey));
            }
            MusicPlayer.Volume = MusicPlayer.DefaultVolume;   // the reload: only the file remains
            using (new Rig(dir)) Assert.AreEqual(0.5f, MusicPlayer.Volume, 1e-4f);
            using (var rig = new Rig(dir, arabic: true)) Assert.AreEqual("مستوى صوت الموسيقى", rig.Root.Q<Label>("settings-music_volume-label").text);
        }
        finally
        {
            MusicPlayer.Volume = MusicPlayer.DefaultVolume;
            SettingsPresenter.Reset();
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    sealed class Rig : IDisposable
    {
        readonly GameObject go; readonly RenderTexture rt; readonly PanelSettings panel; readonly SettingsPresenter settings;
        public readonly VisualElement Root;

        public Rig(string dir, bool arabic = false)
        {
            rt = new RenderTexture(1280, 720, 0);
            panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPaths.Panel));
            panel.targetTexture = rt;
            go = new GameObject("MusicTest"); go.SetActive(false);
            var doc = go.AddComponent<UIDocument>(); doc.panelSettings = panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPaths.GameUxml);
            go.SetActive(true);
            Root = doc.rootVisualElement;
            Root.Q("root").RemoveFromClassList("loading");
            settings = new SettingsPresenter(Root, arabic ? FamilyKit.Ar : FamilyKit.En, new SettingsStore(dir), null, panel);
        }

        public void Dispose()
        {
            settings.Dispose();
            Object.DestroyImmediate(go); Object.DestroyImmediate(panel); rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}
