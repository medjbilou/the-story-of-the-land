#nullable enable
using System;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Boot;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using System.Linq;

public class GoldenCheckTests
{
    static string Goldens() =>
        FamilyKit.Inputs.Read(GoldenCheck.FileName) ?? throw new AssertionException("content/" + GoldenCheck.FileName + " is missing");

    static string Pinned => GoldenCheck.Parse(Goldens()).Single(x => x.name == DefaultGolden).hash;
    const string DefaultGolden = "family-" + GameRoot.AutomatedChapter + "-" + GameRoot.AutomatedVillage;

    /// <summary>Every golden in the repository's goldens.yaml plays to its pinned hash in the editor, one OK line each.</summary>
    [Test]
    public void TheCheckReportsEveryFamilyGoldenAsOkInTheEditor()
    {
        var text = Goldens();
        var (ok, lines) = GoldenCheck.Run(FamilyKit.Inputs.Chapter, text);
        foreach (var line in lines) TestContext.WriteLine(line);   // the full-set evidence in the results XML
        Assert.IsTrue(ok, string.Join("\n", lines));
        var expected = GoldenCheck.Parse(text).Select(x => "GOLDEN OK " + x.name + " " + x.hash).ToArray();
        CollectionAssert.AreEqual(expected, lines);
    }

    /// <summary>The file pins the automated runs' game (the default chapter and village), and every golden names a chapter
    /// of the game.</summary>
    [Test]
    public void TheFilePinsTheDefaultGameAndOnlyRealChapters()
    {
        var parsed = GoldenCheck.Parse(Goldens());
        CollectionAssert.Contains(parsed.Select(x => x.name).ToList(), DefaultGolden);
        var chapters = FamilyGame.Chapters.Select(c => c.Id).ToList();
        foreach (var (name, hash) in parsed)
        {
            CollectionAssert.Contains(chapters, name.Split('-')[1], name);
            Assert.AreEqual(64, hash.Length, name);
        }
    }

    /// <summary>The check plays what the file lists, not a list of its own (#394): a wrong hash, an unknown chapter or
    /// village, an empty or a malformed file each give a FAIL line and fail the run; none is skipped.</summary>
    [Test]
    public void TheCheckFailsLoudlyOnAWrongHashAnUnknownChapterOrVillageOrABadFile()
    {
        var h = Pinned;
        var text = "# c\n" + DefaultGolden + ": " + h + "\nfamily-1947_1949-made_up_village: " + h + "\nfamily-1999_2000-tantura: " + h
            + "\n" + DefaultGolden + ": " + new string('0', 64) + "\n";
        var (ok, lines) = GoldenCheck.Run(FamilyKit.Chapter, text);
        Assert.IsFalse(ok);
        Assert.AreEqual(4, lines.Count, string.Join("\n", lines));
        Assert.AreEqual("GOLDEN OK " + DefaultGolden + " " + h, lines[0]);
        StringAssert.StartsWith("GOLDEN FAIL family-1947_1949-made_up_village ", lines[1]);
        StringAssert.StartsWith("GOLDEN FAIL family-1999_2000-tantura ", lines[2]);
        Assert.AreEqual("GOLDEN FAIL " + DefaultGolden + " " + h + " expected " + new string('0', 64), lines[3]);

        foreach (var empty in new[] { "# nothing pinned\n", "", null })
        {
            var (emptyOk, emptyLines) = GoldenCheck.Run(FamilyKit.Chapter, empty);
            Assert.IsFalse(emptyOk);
            Assert.AreEqual("GOLDEN FAIL manifest has no goldens", emptyLines.Single());
        }
        foreach (var bad in new[] { "hash1950 " + h + "\n", "family-1947_1949: " + h + "\n", DefaultGolden + ":\n", "tantura-1947_1949-x: " + h + "\n" })
        {
            Assert.Throws<FormatException>(() => GoldenCheck.Parse(bad), bad);
            var (badOk, badLines) = GoldenCheck.Run(FamilyKit.Chapter, DefaultGolden + ": " + h + "\n" + bad);
            Assert.IsFalse(badOk, bad);
            StringAssert.StartsWith("GOLDEN FAIL manifest ", badLines.Single(), bad);
        }
    }

    [TestCase("http://localhost:8000/index.html?golden", true)]
    [TestCase("http://localhost:8000/index.html?smoke=5&golden", true)]
    [TestCase("https://html.itch.zone/html/123/golden-project/index.html", false)]
    [TestCase("http://localhost:8000/index.html?goldenrod=1", false)]
    [TestCase("http://localhost:8000/index.html", false)]
    [TestCase("http://localhost:8000/index.html?golden.y", false)]          // Task 24 review M2: a whole query parameter
    [TestCase("http://localhost:8000/index.html?golden-x&smoke=5", false)]
    [TestCase("http://localhost:8000/index.html?golden#top", true)]
    [TestCase("http://localhost:8000/index.html?golden=1", true)]
    public void TheGoldenFlagIsAQueryParameterNotAPathWord(string url, bool expected) =>
        Assert.AreEqual(expected, UrlFlags.Has(url, "golden"));

    [Test]
    public void IntegerFlagsReadTheirValue()
    {
        Assert.AreEqual(5, UrlFlags.Int("http://x/index.html?golden&smoke=5", "smoke"));
        Assert.AreEqual(0, UrlFlags.Int("http://x/smoke=5/index.html", "smoke"));
    }

    [Test]
    public void AnOverflowingIntegerFlagReadsAsAbsent() =>
        Assert.AreEqual(0, UrlFlags.Int("http://x/index.html?smoke=99999999999999999999", "smoke"));

    [Test]
    public void ABootErrorQuitsWithCode1OnlyForADesktopGoldenRun()
    {
        Assert.AreEqual(1, GoldenCheck.QuitCodeOnError(requested: true, webgl: false));
        Assert.IsNull(GoldenCheck.QuitCodeOnError(requested: true, webgl: true));
        Assert.IsNull(GoldenCheck.QuitCodeOnError(requested: false, webgl: false));
    }

    /// <summary>Task 24 review M2: golden, profile and camlog share one rule (UrlFlags.Has): the flag is a whole query
    /// parameter, bare or with a value, ending at '&amp;', '#' or the end.</summary>
    [TestCase("profile")]
    [TestCase("camlog")]
    [TestCase("golden")]
    public void EveryBooleanFlagIsAWholeQueryParameter(string flag)
    {
        Assert.IsTrue(UrlFlags.Has($"http://x/index.html?{flag}", flag));
        Assert.IsTrue(UrlFlags.Has($"http://x/index.html?smoke=5&{flag}&y", flag));
        Assert.IsFalse(UrlFlags.Has($"http://x/index.html?{flag}-x", flag));
        Assert.IsFalse(UrlFlags.Has($"http://x/index.html?{flag}s", flag));
        Assert.IsFalse(UrlFlags.Has($"http://x/{flag}/index.html", flag));
    }
}
