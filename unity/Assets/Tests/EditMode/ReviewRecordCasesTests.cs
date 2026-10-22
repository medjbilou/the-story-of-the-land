#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Map.Kits;
using YamlDotNet.Serialization;

/// <summary>A person's sign-off (#375) reads the same in every reader: the map's two readers here take the one shared
/// list of cases (tools/tests/data/review_record_cases.json) that the sim's loader (ReviewRecordTests) and the Python
/// tools (test_review_record.py) also take.</summary>
public class ReviewRecordCasesTests
{
    public sealed class Case
    {
        public string? By, On, Field; public bool NonScalar;
        public override string ToString() => $"{By ?? "(absent)"} | {On ?? "(absent)"}";
    }

    public static IEnumerable<Case> Cases()
    {
        var path = Path.Combine(Paths.RepoRoot, "tools", "tests", "data", "review_record_cases.json");
        // JSON is YAML: YamlDotNet keeps null (the key is absent) apart from "" (the key is there, blank).
        var doc = new DeserializerBuilder().Build().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
        foreach (Dictionary<object, object?> c in (List<object>)doc["cases"])
            yield return new Case
            {
                By = c["by"] as string, On = c["on"] as string, Field = c["field"] as string,
                NonScalar = c.TryGetValue("nonscalar", out var n) && n as string == "true",
            };
    }

    static string Lines(Case c, string indent) =>
        (c.By == null ? "" : (indent + "reviewed_by: " + c.By).TrimEnd() + "\n") + (c.On == null ? "" : (indent + "reviewed_on: " + c.On).TrimEnd() + "\n");

    static void Expect(Case c, TestDelegate parse)
    {
        if (c.Field == null) { Assert.DoesNotThrow(parse); return; }
        var ex = Assert.Throws<InvalidDataException>(parse);
        if (!c.NonScalar) StringAssert.Contains(c.Field, ex.Message);
    }

    [TestCaseSource(nameof(Cases))]
    public void LocalitiesAgree(Case c) => Expect(c, () => Localities.Parse(
        "places:\n- id: a\n  name: X\n  lon: 35.0\n  lat: 32.0\n  type: village\n  landmark: mosque\n  population: 1200\n" + Lines(c, "  ")
        + "  checked: '2026-10-11'\n  status: draft\n  sources:\n  - https://example.org/x\n", (34.2, 35.9, 29.45, 33.35), new[] { "tel_aviv" }, "loc.yaml"));

    [TestCaseSource(nameof(Cases))]
    public void ThePopulationFileAgrees(Case c) => Expect(c, () => KitPopulation.Parse(
        "settlements:\n- id: a\n  population: 1\n  status: verified\n" + Lines(c, "  ")
        + "  sources:\n  - url: https://example.org/x\n    checked: '2026-10-11'\n", "pop.yaml"));
}
