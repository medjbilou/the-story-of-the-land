#nullable enable
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StoryOfTheLand.Game.UI;
using StoryOfTheLand.Sim;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>#595: a refused order names the kind of thing (event, option) in the UI's language. Each kind word is a string,
/// order.kind.&lt;kind&gt;, in strings.en.yaml and strings.ar.yaml; English reads as it did before. The family game's only
/// order is a card's choice: any other is refused as not valid.</summary>
public class SimBridgeKindTests
{
    static UiText En => FamilyKit.En;
    static UiText Ar => FamilyKit.Ar;
    static readonly Regex Latin = new Regex("[A-Za-z]");

    /// <summary>The kinds SimBridge.Validate can name: every Unknown("kind", …) call in its source. A call whose kind is
    /// not a literal fails the count, so a new kind cannot slip past this test.</summary>
    static string[] KindsInTheBridge()
    {
        var source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/UI/SimBridge.cs"));
        var calls = Regex.Matches(source, @"(?<![\w.])Unknown\(").Count - 1;   // less the method's own declaration
        var kinds = Regex.Matches(source, @"(?<![\w.])Unknown\(""([a-z_]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        Assert.AreEqual(calls, kinds.Count, "every Unknown(...) call names its kind as a literal");
        return kinds.Distinct().OrderBy(k => k, System.StringComparer.Ordinal).ToArray();
    }

    [Test]
    public void EveryKindTheBridgeCanNameHasAnEnglishAndAnArabicWord()
    {
        var kinds = KindsInTheBridge();
        CollectionAssert.AreEqual(new[] { "event", "option" }, kinds, "the kinds are read from the source");
        var en = En;
        var ar = Ar;
        foreach (var k in kinds)
        {
            // English as before #595: the word the bridge passed.
            Assert.AreEqual(k.Replace('_', ' '), en["order.kind." + k], k);
            var word = ar["order.kind." + k];
            Assert.IsTrue(UiDirection.HasRightToLeft(word), k + ": " + word);
            Assert.IsFalse(Latin.IsMatch(word), k + ": " + word);
        }
    }

    /// <summary>A choice for an event or an option the content does not have names its kind; an order of any other kind is
    /// not valid here.</summary>
    [Test]
    public void AnEnglishRefusalReadsAsBefore()
    {
        var bridge = FamilyKit.Bridge();
        var pending = bridge.State.PendingEvents[0];
        LogAssert.Expect(LogType.Log, new Regex("unknown event 'no_such_event'"));
        Assert.AreEqual("Unknown event", bridge.Queue(new ChooseEventOption("no_such_event", "x")));
        LogAssert.Expect(LogType.Log, new Regex("unknown option 'nowhere'"));
        Assert.AreEqual("Unknown option", bridge.Queue(new ChooseEventOption(pending, "nowhere")));
        Assert.AreEqual("That order is not valid", bridge.Queue(new OtherOrder()));
        Assert.IsEmpty(bridge.Queued, "nothing refused is queued");
    }

    /// <summary>The issue's done-when: an unknown id queued with the Arabic strings gives a reason with no Latin kind word
    /// (no Latin letter at all), for both kinds.</summary>
    [Test]
    public void AnArabicRefusalHasNoEnglishWord()
    {
        var ar = Ar;
        var bridge = FamilyKit.Bridge(ar);
        LogAssert.Expect(LogType.Log, new Regex("unknown event 'no_such_event'"));
        var ev = bridge.Queue(new ChooseEventOption("no_such_event", "x"));
        Assert.AreEqual(ar.Format("order.unknown", ar["order.kind.event"]), ev);
        LogAssert.Expect(LogType.Log, new Regex("unknown option 'nowhere'"));
        var option = bridge.Queue(new ChooseEventOption(bridge.State.PendingEvents[0], "nowhere"));
        Assert.AreEqual(ar.Format("order.unknown", ar["order.kind.option"]), option);
        foreach (var reason in new[] { ev!, option! })
        {
            Assert.IsFalse(Latin.IsMatch(reason), reason);
            Assert.IsTrue(UiDirection.RightToLeft(reason), reason);
        }
    }
}
