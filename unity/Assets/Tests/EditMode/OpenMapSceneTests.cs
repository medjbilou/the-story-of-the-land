#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;

namespace StoryOfTheLand.Tests.EditMode
{
    /// <summary>A fresh clone opens on an empty "Untitled" scene; the editor hook opens Assets/Map.unity instead, but only
    /// when nothing the user made could be lost, never in batch mode or play mode, and once per editor session.</summary>
    public sealed class OpenMapSceneTests
    {
        static bool ShouldOpen(string activePath, bool dirty, int loaded, bool playing, bool batch, bool done)
        {
            var t = Type.GetType("StoryOfTheLand.Game.EditorTools.OpenMapScene, Assembly-CSharp-Editor");
            Assert.IsNotNull(t, "the OpenMapScene editor hook exists");
            var m = t!.GetMethod("ShouldOpen", BindingFlags.Public | BindingFlags.Static)!;
            return (bool)m.Invoke(null, new object[] { activePath, dirty, loaded, playing, batch, done })!;
        }

        [Test]
        public void AFreshUntitledEditorOpensTheMap() =>
            Assert.IsTrue(ShouldOpen("", dirty: false, loaded: 1, playing: false, batch: false, done: false));

        [TestCase("Assets/Other.unity", false, 1, false, false, false, TestName = "a saved scene the user opened stays")]
        [TestCase("", true, 1, false, false, false, TestName = "an edited untitled scene is never thrown away")]
        [TestCase("", false, 2, false, false, false, TestName = "several loaded scenes stay")]
        [TestCase("", false, 1, true, false, false, TestName = "never in play mode")]
        [TestCase("", false, 1, false, true, false, TestName = "never in batch mode (tests and builds)")]
        [TestCase("", false, 1, false, false, true, TestName = "only once per editor session")]
        public void LeavesTheEditorAlone(string path, bool dirty, int loaded, bool playing, bool batch, bool done) =>
            Assert.IsFalse(ShouldOpen(path, dirty, loaded, playing, batch, done));

        static bool EditorReady(bool compiling, bool updating, bool valid)
        {
            var t = Type.GetType("StoryOfTheLand.Game.EditorTools.OpenMapScene, Assembly-CSharp-Editor")!;
            return (bool)t.GetMethod("EditorReady", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { compiling, updating, valid })!;
        }

        [Test]
        public void TheDecisionWaitsUntilTheEditorHasFinishedStarting()
        {
            // A fresh clone's first launch loads the domain before Unity creates its starting scene; deciding then saw no
            // valid scene, marked the session done and never opened the map (found by opening a real fresh clone).
            Assert.IsTrue(EditorReady(false, false, true));
            Assert.IsFalse(EditorReady(true, false, true), "still compiling");
            Assert.IsFalse(EditorReady(false, true, true), "still importing");
            Assert.IsFalse(EditorReady(false, false, false), "no valid scene yet");
        }
    }
}
