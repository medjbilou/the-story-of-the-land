#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>The in-game footage viewer (owner, 2026-10-22): a signed-off Archive Genocide file plays over the card after its
/// content note; a publisher's page, and every clip in the web player, opens in the browser. No network: the clips are tiny
/// local files made here, in a temp folder.</summary>
public class FootageViewerTests
{
    const string Partition = "e1947_11_partition";
    const string Page = "https://www.aljazeera.com/video/partition";
    static UiText Text => FamilyKit.En;
    static string dir = "", videoUrl = "", photoUrl = "";

    [OneTimeSetUp]
    public void MakeClips()
    {
        dir = Path.Combine(Path.GetTempPath(), "sotl-footage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var frame = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        frame.SetPixels32(Enumerable.Repeat(new Color32(200, 160, 90, 255), 64 * 64).ToArray());
        frame.Apply();
        var video = Path.Combine(dir, "clip.webm");
        var attrs = new VideoTrackAttributes
        {
            frameRate = new MediaRational(10), width = 64, height = 64, includeAlpha = false, bitRateMode = UnityEditor.VideoBitrateMode.Low,
        };
        using (var encoder = new MediaEncoder(video, attrs))
            for (int i = 0; i < 20; i++) encoder.AddFrame(frame);
        var photo = Path.Combine(dir, "photo.png");
        File.WriteAllBytes(photo, frame.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(frame);
        videoUrl = new Uri(video).AbsoluteUri; photoUrl = new Uri(photo).AbsoluteUri;
    }

    [OneTimeTearDown]
    public void RemoveClips() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

    [TearDown]
    public void ReleaseTheMusic() => MusicPlayer.Held = false;

    static Clip Clip(string kind, string media, bool signed = true) => new Clip
    {
        EventId = Partition, Url = media.Length > 0 ? "https://archivegenocide.com/nodes/#focus=ev-0c46bb56f2" : Page,
        Publisher = "Eye on Palestine (Telegram, Apr-Dec 2025)", Title = "The clip", TitleAr = "المقطع",
        Description = "What the clip shows.", DescriptionAr = "ما يظهره المقطع.", Kind = kind, MediaUrl = media,
        AgEntryId = media.Length > 0 ? "113419" : "", PostUrl = media.Length > 0 ? "https://x.com/p/status/1" : "",
        CheckedBy = signed ? "jdoe" : "", CheckedOn = signed ? "2026-10-22" : "",
    };

    static Footage Of(params Clip[] clips) { var f = new Footage(); f.All.AddRange(clips); return f; }

    /// <summary>A family game with only the Partition card pending (as EventCardTests.Showing).</summary>
    static SimBridge AtPartition()
    {
        var c = FamilyKit.Chapter();
        var g = GameSession.New(c, GoldenRun.Seed, GameRoot.AutomatedVillage);
        g.State.Date = c.Events.First(e => e.Id == Partition).From;
        g.State.PendingEvents.Clear(); g.State.PendingEvents.Add(Partition);
        return new SimBridge(g, Text);
    }

    static (EventCardPresenter card, List<string> opened) Card(UiTestKit ui, Footage footage)
    {
        var opened = new List<string>();
        return (new EventCardPresenter(ui.Root, AtPartition(), Text, clipsFor: footage.For, openUrl: opened.Add), opened);
    }

    static void Watch(UiTestKit ui, int i)
    {
        UiTestKit.Press(ui.Root.Q<Button>("event-watch-" + i));
        UiTestKit.Press(ui.Root.Q<Button>("event-footage-open"));
    }

    static IEnumerator Until(Func<bool> done, float seconds = 20f)
    {
        var until = Time.realtimeSinceStartup + seconds;
        // Edit mode runs the player loop (where the VideoPlayer prepares and decodes) only when asked.
        while (!done() && Time.realtimeSinceStartup < until) { UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); yield return null; }
    }

    [Test]
    public void OnlyASignedOffFilePlaysInTheGameAfterItsContentNote()
    {
        using var ui = new UiTestKit();
        var (card, opened) = Card(ui, Of(Clip("page", ""), Clip("video", videoUrl, signed: false), Clip("video", videoUrl)));
        Assert.AreEqual(2, ui.Root.Q("event-footage").Query<Button>().ToList().Count, "the unsigned file has no button");

        Watch(ui, 0);
        CollectionAssert.AreEqual(new[] { Page }, opened, "a publisher's page opens in the browser");
        Assert.IsFalse(card.Viewer.IsOpen);

        UiTestKit.Press(ui.Root.Q<Button>("event-watch-1"));
        Assert.IsNotNull(ui.Root.Q<Label>("event-footage-note"), "the content note comes first");
        Assert.IsFalse(card.Viewer.IsOpen, "nothing plays before Open");
        Assert.IsFalse(MusicPlayer.Held);
        UiTestKit.Press(ui.Root.Q<Button>("event-footage-open"));
        Assert.IsTrue(card.Viewer.IsOpen);
        Assert.AreEqual(1, opened.Count, "the file plays in the game, not the browser");
        Assert.AreEqual(DisplayStyle.Flex, ui.Root.Q(FootageViewer.OverlayName).style.display.value);
        Assert.AreEqual("The clip", ui.Root.Q<Label>("footage-title").text);
        Assert.AreEqual("What the clip shows.", ui.Root.Q<Label>("footage-caption").text);
        Assert.AreEqual("Archive Genocide", ui.Root.Q<Button>("footage-source").text);
        StringAssert.Contains("Eye on Palestine", ui.Root.Q<Label>("footage-archive").text);
        Assert.IsTrue(MusicPlayer.Held, "the music holds while a video is open");

        UiTestKit.Press(ui.Root.Q<Button>("footage-post"));
        UiTestKit.Press(ui.Root.Q<Button>("footage-source"));
        CollectionAssert.AreEqual(new[] { Page, "https://x.com/p/status/1", "https://archivegenocide.com/nodes/#focus=ev-0c46bb56f2" }, opened);

        UiTestKit.Press(ui.Root.Q<Button>("footage-close"));
        Assert.IsFalse(card.Viewer.IsOpen);
        Assert.IsFalse(MusicPlayer.Held, "the music plays on");
        Assert.AreEqual(DisplayStyle.None, ui.Root.Q(FootageViewer.OverlayName).style.display.value);
    }

    /// <summary>The web player cannot read the archive's files (no CORS header): every clip opens in a new tab there.</summary>
    [Test]
    public void TheWebPlayerOpensEveryClipInTheBrowser()
    {
        var file = Clip("video", "https://media.archivegenocide.com/x/a.mp4");
        Assert.IsTrue(FootageViewer.Plays(file, web: false));
        Assert.IsFalse(FootageViewer.Plays(file, web: true));
        Assert.IsFalse(FootageViewer.Plays(Clip("page", ""), web: false));
        Assert.AreEqual("https://media.archivegenocide.com/x/a.mp4", FootageViewer.BrowserUrl(file));
        Assert.AreEqual(Page, FootageViewer.BrowserUrl(Clip("page", "")));
    }

    [UnityTest]
    public IEnumerator ALocalVideoPlaysAndEscapeStopsItAndFreesItsTexture()
    {
        using var ui = new UiTestKit();
        var (card, _) = Card(ui, Of(Clip("video", videoUrl)));
        Watch(ui, 0);
        var viewer = card.Viewer;
        yield return Until(() => viewer.Loaded || viewer.Failed);
        Assert.IsTrue(viewer.Loaded, "the local clip prepared");
        var target = viewer.Target;
        Assert.IsTrue(target != null && target.IsCreated());
        Assert.AreSame(target, viewer.Video!.targetTexture);
        Assert.AreEqual(DisplayStyle.None, ui.Root.Q<Label>("footage-status").style.display.value, "no loading line once it plays");

        Assert.IsTrue(card.Escape(), "Esc closes the viewer first");
        Assert.IsFalse(viewer.IsOpen);
        Assert.IsNull(viewer.Video);
        Assert.IsTrue(target == null, "the RenderTexture is destroyed");
        Assert.IsFalse(MusicPlayer.Held);
        Assert.IsFalse(card.Collapsed, "the card stays open under it");
    }

    [UnityTest]
    public IEnumerator ALocalPhotoShowsWithoutVideoControls()
    {
        using var ui = new UiTestKit();
        var (card, _) = Card(ui, Of(Clip("photo", photoUrl)));
        Watch(ui, 0);
        yield return Until(() => card.Viewer.Loaded || card.Viewer.Failed);
        Assert.IsTrue(card.Viewer.Loaded);
        Assert.AreEqual(DisplayStyle.None, ui.Root.Q("footage-controls").style.display.value);
        Assert.IsFalse(MusicPlayer.Held, "a photo leaves the music playing");
        card.Viewer.Close();
    }

    [UnityTest]
    public IEnumerator AFileThatDoesNotLoadSaysSoAndOffersTheBrowser()
    {
        LogAssert.ignoreFailingMessages = true;   // the VideoPlayer logs its own error for the missing file
        using var ui = new UiTestKit();
        var missing = new Uri(Path.Combine(dir, "missing.webm")).AbsoluteUri;
        var (card, opened) = Card(ui, Of(Clip("video", missing)));
        Watch(ui, 0);
        yield return Until(() => card.Viewer.Loaded || card.Viewer.Failed);
        Assert.IsTrue(card.Viewer.Failed);
        Assert.AreEqual("This clip could not be loaded. Open it in the browser.", ui.Root.Q<Label>("footage-status").text);
        Assert.IsFalse(MusicPlayer.Held);
        UiTestKit.Press(ui.Root.Q<Button>("footage-browser"));
        CollectionAssert.AreEqual(new[] { missing }, opened);
        card.Viewer.Close();
    }

    [Test]
    public void TheViewerStringsAreInArabic()
    {
        var ar = FamilyKit.Ar;
        Assert.AreEqual("تعذّر تحميل هذا المقطع. افتحه في المتصفح.", ar["footage.error"]);
        Assert.AreEqual("جارٍ تحميل المقطع…", ar["footage.loading"]);
        Assert.AreEqual(("تشغيل", "إيقاف مؤقت", "كتم الصوت", "إغلاق"), (ar["footage.play"], ar["footage.pause"], ar["footage.mute"], ar["footage.close"]));
        Assert.AreEqual("المنشور الأصلي", ar["footage.original_post"]);
    }
}
