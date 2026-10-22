#nullable enable
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StoryOfTheLand.Game.UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

/// <summary>F5 step 8: each family chapter's narrated intro, which file it plays in each language, and that every chapter
/// ships its intro in both. The Narration switch and its kept choice are in SettingsScreenTests.</summary>
public class NarratorTests
{
    [Test]
    public void TheIntroScriptOf1947NamesTheFirstWarChapterAndTheOthersTheirOwn()
    {
        Assert.AreEqual("1947", Narrator.ScriptId("1947_1949"));
        foreach (var (id, _, _) in FamilyGame.Chapters.Where(c => c.Id != "1947_1949")) Assert.AreEqual(id, Narrator.ScriptId(id));
    }

    [Test]
    public void ArabicPrefersTheHabibiRenderThenChatterboxAndEnglishIsChatterbox()
    {
        CollectionAssert.AreEqual(new[] { "intro_1947.ar.habibi.ogg", "intro_1947.ar.ogg" }, Narrator.Files("1947_1949", UiLanguage.Arabic));
        CollectionAssert.AreEqual(new[] { "intro_1967_1987.en.ogg" }, Narrator.Files("1967_1987", UiLanguage.English));
    }

    /// <summary>Playtest of the web build: "Streaming of 'ogg' on this platform is not supported", then a
    /// NullReferenceException, and no narration. The web player downloads the whole file; desktop keeps its setting.</summary>
    [Test]
    public void TheWebPlayerDownloadsTheWholeFileInsteadOfStreamingIt()
    {
        using var web = Narrator.Request("file:///intro.mp3", web: true);
        Assert.IsFalse(((DownloadHandlerAudioClip)web.downloadHandler).streamAudio, "the web player streams nothing");
        using var desktop = Narrator.Request("file:///intro.ogg", web: false);
        using var plain = UnityWebRequestMultimedia.GetAudioClip("file:///intro.ogg", AudioType.OGGVORBIS);
        Assert.AreEqual(((DownloadHandlerAudioClip)plain.downloadHandler).streamAudio, ((DownloadHandlerAudioClip)desktop.downloadHandler).streamAudio,
            "desktop is unchanged");
    }

    /// <summary>Web build 11:33: "playing … 0.0 s". The clip plays only once its data has loaded; a failed decode is not
    /// waited on.</summary>
    [Test]
    public void ItWaitsWhileTheClipDecodesAndNotAfter()
    {
        Assert.IsTrue(Narrator.Decoding(AudioDataLoadState.Unloaded));
        Assert.IsTrue(Narrator.Decoding(AudioDataLoadState.Loading));
        Assert.IsFalse(Narrator.Decoding(AudioDataLoadState.Loaded));
        Assert.IsFalse(Narrator.Decoding(AudioDataLoadState.Failed));
    }

    [Test]
    public void AFileThatDoesNotLoadGivesOneWarningAndNoClip()
    {
        var path = Path.Combine(Path.GetTempPath(), "narrator-missing-" + System.Guid.NewGuid().ToString("N") + ".ogg");
        using var req = Narrator.Request(new System.Uri(path).AbsoluteUri, web: false);
        var op = req.SendWebRequest();
        var until = System.DateTime.UtcNow.AddSeconds(10);
        while (!op.isDone && System.DateTime.UtcNow < until) System.Threading.Thread.Sleep(10);
        LogAssert.Expect(LogType.Warning, new Regex(@"^\[Narrator\] missing\.ogg: "));
        Assert.IsNull(Narrator.Clip(req, "missing.ogg"));
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>UI audit of the web build: the web player loads no Ogg Vorbis, so it plays an MP3 copy of every intro.</summary>
    [Test]
    public void TheWebPlayerPlaysAnMp3CopyOfEveryIntroInBothLanguages([Values(UiLanguage.English, UiLanguage.Arabic)] string language)
    {
        CollectionAssert.AreEqual(new[] { "intro_1947.ar.habibi.mp3", "intro_1947.ar.mp3" }, Narrator.Files("1947_1949", UiLanguage.Arabic, web: true));
        foreach (var (chapter, _, _) in FamilyGame.Chapters)
        {
            var mp3 = Path.Combine(Application.streamingAssetsPath, "narration", Narrator.Files(chapter, language, web: true)[0]);
            Assert.IsTrue(File.Exists(mp3), mp3 + ": run tools/narration_web.py");
            var head = new byte[3];
            using (var f = File.OpenRead(mp3)) f.Read(head, 0, head.Length);
            Assert.IsTrue(System.Text.Encoding.ASCII.GetString(head) == "ID3" || (head[0] == 0xFF && (head[1] & 0xE0) == 0xE0), mp3 + ": an MP3");
        }
    }

    [Test]
    public void EveryChapterShipsItsIntroInBothLanguagesAsOggVorbis([Values(UiLanguage.English, UiLanguage.Arabic)] string language)
    {
        foreach (var (chapter, _, _) in FamilyGame.Chapters)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "narration", Narrator.Files(chapter, language)[0]);
            Assert.IsTrue(File.Exists(path), path);
            var head = new byte[64];
            using (var f = File.OpenRead(path)) f.Read(head, 0, head.Length);
            var magic = System.Text.Encoding.ASCII.GetString(head);
            StringAssert.StartsWith("OggS", magic, path);
            StringAssert.Contains("vorbis", magic, path + ": Vorbis (Unity plays no Opus)");
        }
    }
}
