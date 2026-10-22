#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game.Map;
using UnityEngine;

/// <summary>Task 9 fix round 1 (review I3): "?vprofile&amp;vmoving" prices every toggle while the camera turns, so a feature
/// that only costs when the view changes (the map labels' placement) is measured too. Additive: without the flag the
/// profile is unchanged (VisualHarnessTests.TheProfileStartsWithOneDiscardedPassOfEveryConfiguration).</summary>
public class FrameProfileMovingTests
{
    [TestCase("http://localhost/?vprofile&vmoving", true)]
    [TestCase("http://localhost/?vprofile", false)]
    [TestCase("http://localhost/?vmoving-x", false)]
    public void TheMovingVariantIsAskedForByItsOwnFlag(string url, bool expected)
    {
        Assert.AreEqual(expected, FrameProfile.MovingRequested(url, new string[0], true));
        Assert.AreEqual(expected, FrameProfile.MovingRequested(null, expected ? new[] { "-vmoving" } : new string[0], false));
    }

    [Test]
    public void TheMovingVariantTurnsTheCameraHalfADegreeEveryFrameFromEachPose()
    {
        var go = new GameObject("cam", typeof(Camera)); var rig = go.AddComponent<CameraRig>();
        var frame = new GeoFrame(34.2, 35.9, 29.45, 33.35, 161529f, 431223f);
        VisualToggles.Register("grade", _ => { });
        var log = new List<string>(); var yaws = new List<float>();
        try
        {
            var run = FrameProfile.Run(rig, frame, log.Add, false, moving: true);
            while (run.MoveNext()) yaws.Add(rig.Yaw);
        }
        finally { Object.DestroyImmediate(go); VisualToggles.Clear(); }
        int configs = 3, perRun = FrameProfile.WarmupFrames + FrameProfile.MeasuredFrames;
        Assert.AreEqual(configs * FrameProfile.WarmupFrames + PromoPose.All.Count * FrameProfile.Repeats * configs * perRun, yaws.Count);
        // Every measured run starts at its pose's yaw and turns MovingDegreesPerFrame each frame.
        int start = configs * FrameProfile.WarmupFrames, i = 0;
        foreach (var pose in PromoPose.All)
            for (int run = 0; run < FrameProfile.Repeats * configs; run++, i++)
                for (int f = 0; f < perRun; f++)
                    Assert.AreEqual(pose.Yaw + FrameProfile.MovingDegreesPerFrame * (f + 1), yaws[start + i * perRun + f], 1e-3f, $"{pose.Name} run {run} frame {f}");
        Assert.AreEqual(0.5f, FrameProfile.MovingDegreesPerFrame);
        foreach (var pose in PromoPose.All)
        {
            Assert.AreEqual(FrameProfile.Repeats * configs, log.Count(l => l.StartsWith("PROFILE visual=") && l.Contains($" pose={pose.Name}-moving ")), pose.Name);
            Assert.AreEqual(configs, log.Count(l => l.StartsWith("PROFILE summary") && l.Contains($" pose={pose.Name}-moving ")), pose.Name);
        }
        Assert.AreEqual("PROFILE visual done", log.Last());
    }
}
