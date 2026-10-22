using System.Collections.Generic;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;

public class SimSmokeTests
{
    /// <summary>The default chapter loads, and a family turn is deterministic on the editor's runtime.</summary>
    [Test]
    public void ContentLoads_AndStepIsDeterministicInsideUnity()
    {
        var content = FamilyKit.Chapter();
        var a = Simulation.NewGame(content, 42UL, GameRoot.AutomatedVillage);
        var b = Simulation.NewGame(content, 42UL, GameRoot.AutomatedVillage);
        var ra = Simulation.Step(content, a, new List<Command>());
        var rb = Simulation.Step(content, b, new List<Command>());
        Assert.AreEqual(ra.State.Hash(), rb.State.Hash());
        Assert.IsNotEmpty(ra.State.Hash());
    }

    /// <summary>The save format's strict UTF-8 decoding, on the Unity editor's Mono runtime (it must not depend on the
    /// BCL's Uri class): an overlong escape is refused, a surrogate pair round-trips.</summary>
    [Test]
    public void SaveTextIsStrictUtf8OnMono()
    {
        var hash = new string('a', 64);
        var overlong = "storyoftheland-save 3\ncontent " + hash + "\nseed 1\nmode Historical\nruleset family-0.1\nvillage tantura\nturn\nevent a%C0%AF x\nend " + hash + "\n";
        var ex = Assert.Throws<SaveException>(() => SaveGame.Parse(overlong));
        Assert.AreEqual("line 8: bad text 'a%C0%AF'", ex.Message);

        // A family game saves its choices as text; an unknown event is refused by the sim but still recorded and replayed.
        var content = FamilyKit.Chapter();
        var g = FamilyKit.Session(seed: 1UL);
        g.Step(new List<Command> { new ChooseEventOption("a\uD83D\uDE00b", "x") });
        var text = g.Save.Write();
        StringAssert.Contains("event a%F0%9F%98%80b x", text);
        var loaded = GameSession.Load(content, text, out _);
        Assert.AreEqual(g.State.Hash(), loaded.State.Hash());
        Assert.AreEqual(text, loaded.Save.Write());
    }
}
