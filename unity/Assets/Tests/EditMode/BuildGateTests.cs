#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;

/// <summary>Final review I4: a shareable build (Build.Web -shareable) is refused while any sensitive family card lacks the
/// owner's sensitivity review, in any chapter, and the refusal names each one.</summary>
public class BuildGateTests
{
    static Type Build() => Type.GetType("StoryOfTheLand.Game.EditorTools.Build, Assembly-CSharp-Editor") ?? throw new AssertionException("Build exists");

    static List<Content> Chapters() => FamilyGame.Chapters.Select(c => ContentLoader.LoadChapter(Paths.Content, c.Id)).ToList();

    static string? Refusal(IEnumerable<Content> chapters) =>
        (string?)Build().GetMethod("ShareableRefusal", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object?[] { chapters });

    [Test]
    public void AShareableBuildWaitsForTheSensitivityReviewOfEveryChapter()
    {
        var chapters = Chapters();
        var open = chapters.SelectMany(ReleaseGate.SensitiveUnreviewed).ToList();
        Assert.IsNotEmpty(open, "the chapters carry sensitive cards (tags: [sensitive]) that no owner has reviewed yet");
        var refusal = Refusal(chapters);
        Assert.IsNotNull(refusal, "an unreviewed sensitive card blocks a shareable build");
        foreach (var id in open) StringAssert.Contains(id, refusal);
        foreach (var e in chapters.SelectMany(c => c.Events)) e.Status = ReleaseGate.ReviewedStatus;   // as the owner's sign-off would
        Assert.IsNull(Refusal(chapters), "all reviewed: the build may be shared");
    }

    /// <summary>Rereview: the gate applies to every player build made with -shareable (Build.Web and Build.Mac call the same
    /// check); without the flag, dev builds stay ungated (web_check needs them).</summary>
    [Test]
    public void TheShareableFlagGatesEveryPlayerBuild()
    {
        var gate = Build().GetMethod("GateRefusal", BindingFlags.Public | BindingFlags.Static)!;
        var chapters = Chapters();
        Assert.IsNull(gate.Invoke(null, new object?[] { new[] { "Unity", "-executeMethod", "StoryOfTheLand.Game.EditorTools.Build.Mac" }, chapters }), "a dev build is not gated");
        Assert.IsNotNull(gate.Invoke(null, new object?[] { new[] { "Unity", "-shareable" }, chapters }), "the sensitive cards are not reviewed yet");
        var source = System.IO.File.ReadAllText("Assets/Editor/Build.cs");
        foreach (var method in new[] { "public static void Web()", "public static void Mac()", "static void StandaloneMono(" })
        {
            var body = source.Substring(source.IndexOf(method, StringComparison.Ordinal), 400);
            StringAssert.Contains("RefuseUnlessShareable()", body, method + " checks the gate first");
        }
        foreach (var method in new[] { "public static void LinuxMono()", "public static void WindowsMono()" })
            StringAssert.Contains("=> StandaloneMono(", source.Substring(source.IndexOf(method, StringComparison.Ordinal), 120), method + " goes through the gated Mono build");
    }

    /// <summary>#482: Linux and Windows are built from the Mac on Mono (decisions O7, O8). The Standalone backend is one
    /// setting for all three desktop targets, so the Mono build must put the committed IL2CPP back afterwards.</summary>
    [Test]
    public void TheMonoPlayersRestoreTheCommittedBackend()
    {
        var t = Type.GetType("StoryOfTheLand.Game.EditorTools.Build, Assembly-CSharp-Editor")!;
        foreach (var name in new[] { "Mac", "LinuxMono", "WindowsMono" })
            Assert.IsNotNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.Static), name + " is an -executeMethod target");
        var source = System.IO.File.ReadAllText("Assets/Editor/Build.cs");
        var start = source.IndexOf("static void StandaloneMono(", StringComparison.Ordinal);
        var body = source.Substring(start, source.IndexOf("ExitIfFailed(ok);", start, StringComparison.Ordinal) - start);
        StringAssert.Contains("ScriptingImplementation.Mono2x", body);
        StringAssert.Contains("finally { ProjectSetup.Apply();", body, "IL2CPP is restored even when the build throws");
        Assert.AreEqual(UnityEditor.ScriptingImplementation.IL2CPP,
            UnityEditor.PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone), "the committed Standalone backend");
    }

    /// <summary>Review I1: every build writes event-photos.json beside its output, which tools/package_release.py reads. The
    /// game carries no event photographs: the list is empty.</summary>
    [Test]
    public void EveryBuildWritesThePhotoManifestForThePackager()
    {
        var t = Build();
        Assert.AreEqual("event-photos.json", t.GetField("PhotoManifestName", BindingFlags.Public | BindingFlags.Static)!.GetValue(null));
        var json = t.GetMethod("PhotoManifestJson", BindingFlags.Public | BindingFlags.Static)!;
        var dev = (string)json.Invoke(null, new object?[] { false })!;
        StringAssert.IsMatch("\"shareable\":\\s*false", dev);
        StringAssert.IsMatch("\"photos\":\\s*\\[\\s*\\]", dev);
        StringAssert.IsMatch("\"shareable\":\\s*true", (string)json.Invoke(null, new object?[] { true })!);
        var source = System.IO.File.ReadAllText("Assets/Editor/Build.cs");
        foreach (var method in new[] { "public static void Web()", "public static void Mac()" })
        {
            var body = source.Substring(source.IndexOf(method, StringComparison.Ordinal), 900);
            StringAssert.Contains("WritePhotoManifest(", body, method + " writes the manifest");
        }
    }
}
