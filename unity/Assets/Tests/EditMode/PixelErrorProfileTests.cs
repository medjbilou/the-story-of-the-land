using System.Collections.Generic;
using NUnit.Framework;
using StoryOfTheLand.Game.Map;

public class PixelErrorProfileTests
{
    [Test]
    public void TheProfileLineReportsAverageAndP95()
    {
        var frames = new List<float>();
        for (int i = 1; i <= 100; i++) frames.Add(i);
        Assert.AreEqual("PROFILE pixelError=1 frames=100 avg_ms=50.50 p95_ms=95.00", PixelErrorProfile.Line(1f, frames));
    }

    [TestCase("http://localhost:8000/index.html?profile", true)]
    [TestCase("http://localhost:8000/index.html?golden&profile", true)]
    [TestCase("https://html.itch.zone/html/123/profile-pics/index.html", false)]
    [TestCase("http://localhost:8000/index.html?profile-x", false)]           // Task 24 review M2
    public void TheProfileFlagIsAQueryParameter(string url, bool expected) =>
        Assert.AreEqual(expected, PixelErrorProfile.Requested(url, new string[0], true));

    [Test]
    public void DesktopTakesTheCommandLineFlag()
    {
        Assert.IsTrue(PixelErrorProfile.Requested(null, new[] { "StoryOfTheLand", "-profile" }, false));
        Assert.IsFalse(PixelErrorProfile.Requested("http://x/?profile", new[] { "StoryOfTheLand" }, false));
    }

    /// <summary>Task 24 review M6: no frames is an argument error, not an index error.</summary>
    [Test]
    public void AnEmptyFrameListIsAnArgumentError() =>
        Assert.Throws<System.ArgumentException>(() => PixelErrorProfile.Line(1f, new List<float>()));
}
