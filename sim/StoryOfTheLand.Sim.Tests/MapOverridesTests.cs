using System.Collections.Generic;
using System.IO;
using StoryOfTheLand.Sim;

/// <summary>content/map/overrides.yaml, the map editor's override layer (#1009 step 1; owner rulings of 18 October 2026).</summary>
public class MapOverridesTests
{
    const string Empty = "terrain: []\ncontrol: []\nlocations: []\n";

    static readonly List<MapHex> Hexes = new List<MapHex>
    {
        new MapHex { Q = 1, R = 2, Land = true, Biome = "hills" },
        new MapHex { Q = 2, R = 2, Land = true, Biome = "desert" },
        new MapHex { Q = 3, R = 2, Land = false, Biome = "sea" },
    };

    static MapOverrides Checked(string yaml)
    {
        var o = MapOverrides.Parse(yaml);
        o.ValidateMap(Hexes, new[] { "town_a", "fort_a" });
        o.ValidateControl(new[] { (1, 2), (2, 2) }, new[] { "opening", "truce1" }, new[] { "I", "E", "c", "-" });
        return o;
    }

    [Fact]
    public void TheShippedFileIsEmptySoNothingMoves()
    {
        var o = MapOverrides.Parse(File.ReadAllText(Path.Combine(FamilyTests.Root, MapOverrides.FileName)));
        Assert.Empty(o.Terrain); Assert.Empty(o.Control); Assert.Empty(o.Locations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AMissingFileHasNoOverrides(string? yaml)
    {
        var o = MapOverrides.Parse(yaml);
        Assert.Empty(o.Terrain); Assert.Empty(o.Control); Assert.Empty(o.Locations);
    }

    [Fact]
    public void AValidFileChecks()
    {
        var o = Checked("terrain:\n  - { hex: { q: 1, r: 2 }, value: desert }\ncontrol:\n  - { hex: { q: 1, r: 2 }, period: truce1, value: E }\n"
            + "  - { hex: { q: 1, r: 2 }, period: opening, value: '-' }\nlocations:\n  - { id: fort_a, hex: { q: 2, r: 2 } }\n");
        Assert.True(o.TryLocation("fort_a", out var q, out var r));
        Assert.Equal((2, 2), (q, r));
        Assert.False(o.TryLocation("town_a", out _, out _));
    }

    [Fact]
    public void AControlOverrideReplacesOnePeriodOfOneHexAndNothingElse()
    {
        var o = Checked("terrain: []\ncontrol:\n  - { hex: { q: 1, r: 2 }, period: truce1, value: E }\nlocations: []\n");
        var periods = new[] { "opening", "truce1" };
        Assert.Equal("IE", o.ApplyControl(1, 2, periods, "II"));
        Assert.Equal("cc", o.ApplyControl(2, 2, periods, "cc"));   // another hex keeps its history
    }

    [Theory]
    [InlineData("control: []\nlocations: []\n", "terrain", "must be a list")]
    [InlineData("terrain: []\ncontrol: []\nlocations: []\nnotes: x\n", "line 4", "")]
    [InlineData("terrain:\n  - { value: hills }\ncontrol: []\nlocations: []\n", "terrain[0].hex", "required")]
    [InlineData("terrain:\n  - { hex: { q: 1, r: 2 }, value: hills }\n  - { hex: { q: 1, r: 2 }, value: desert }\ncontrol: []\nlocations: []\n", "terrain[1].hex", "a second override")]
    [InlineData("terrain: []\ncontrol:\n  - { hex: { q: 1, r: 2 }, period: opening, value: I }\n  - { hex: { q: 1, r: 2 }, period: opening, value: E }\nlocations: []\n", "control[1].period", "a second override")]
    [InlineData("terrain: []\ncontrol: []\nlocations:\n  - { id: town_a, hex: { q: 1, r: 2 } }\n  - { id: town_a, hex: { q: 2, r: 2 } }\n", "locations[1].id", "a second override")]
    [InlineData("terrain:\n  - { hex: { q: 9, r: 9 }, value: hills }\ncontrol: []\nlocations: []\n", "terrain[0].hex", "unknown hex q=9 r=9")]
    [InlineData("terrain:\n  - { hex: { q: 3, r: 2 }, value: hills }\ncontrol: []\nlocations: []\n", "terrain[0].hex", "not a land hex")]
    [InlineData("terrain:\n  - { hex: { q: 1, r: 2 }, value: tundra }\ncontrol: []\nlocations: []\n", "terrain[0].value", "unknown terrain value 'tundra'")]
    [InlineData("terrain:\n  - { hex: { q: 1, r: 2 }, value: sea }\ncontrol: []\nlocations: []\n", "terrain[0].value", "unknown terrain value 'sea'")]
    [InlineData("terrain: []\ncontrol: []\nlocations:\n  - { id: nowhere, hex: { q: 1, r: 2 } }\n", "locations[0].id", "unknown location id 'nowhere'")]
    [InlineData("terrain: []\ncontrol: []\nlocations:\n  - { id: town_a, hex: { q: 9, r: 9 } }\n", "locations[0].hex", "unknown hex q=9 r=9")]
    [InlineData("terrain: []\ncontrol: []\nlocations:\n  - { id: town_a, hex: { q: 3, r: 2 } }\n", "locations[0].hex", "not a land hex")]
    [InlineData("terrain: []\ncontrol:\n  - { hex: { q: 9, r: 9 }, period: opening, value: I }\nlocations: []\n", "control[0].hex", "unknown hex q=9 r=9")]
    [InlineData("terrain: []\ncontrol:\n  - { hex: { q: 1, r: 2 }, period: later, value: I }\nlocations: []\n", "control[0].period", "unknown period 'later'")]
    [InlineData("terrain: []\ncontrol:\n  - { hex: { q: 1, r: 2 }, period: opening, value: Z }\nlocations: []\n", "control[0].value", "unknown control value 'Z'")]
    public void InvalidInputNamesTheFileAndField(string yaml, string field, string message)
    {
        var ex = Assert.Throws<ContentException>(() => Checked(yaml));
        Assert.Equal(MapOverrides.FileName, ex.File);
        Assert.Equal(field, ex.Field);
        Assert.Contains(message, ex.Message);
    }
}
