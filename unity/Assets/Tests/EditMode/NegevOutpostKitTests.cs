#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StoryOfTheLand.Game.Map;
using StoryOfTheLand.Game.Map.Kits;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>The Negev outpost kit (#32, SettlementKits.Outpost.cs): Revivim, one of the three lookouts of 1943, is drawn as
/// the lookouts' walled square yard with a lookout tower in its corner and its buildings along two sides, not as a
/// moshava's houses round a water tower.</summary>
public class NegevOutpostKitTests
{
    [TearDown]
    public void Restore()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        VisualToggles.Clear();
        KitScale.Reset();
    }

    /// <summary>Revivim has one walled yard and one lookout tower, the tower in a corner of the yard; its four buildings (main's
    /// floor is three, TownCensusTests.MainBuildings) stand wholly inside the yard's wall, clear of each other and of the
    /// tower, and it has no water tower. No other game settlement has a yard or a lookout tower.</summary>
    [Test]
    public void RevivimIsAWalledLookoutWithItsTowerInACorner()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>(); map.Build();
        TownCensusTests.Census(map, out var placed);
        var bad = new List<string>();
        foreach (var p in placed.Where(p => (p.Kind == "outpost_yard" || p.Kind == "lookout_tower") && p.Id != "revivim"))
            bad.Add($"{p.Id}: {p.Kind}");
        var mine = placed.Where(p => p.Id == "revivim").ToList();
        TestContext.WriteLine(string.Join("\n", mine.Select(p => $"{p.Kind} at {p.C} yaw {p.Yaw:0} {p.W:0}x{p.D:0} h {p.H:0}")));
        var yards = mine.Where(p => p.Kind == "outpost_yard").ToList();
        var towers = mine.Where(p => p.Kind == "lookout_tower").ToList();
        Assert.AreEqual(1, yards.Count, "Revivim's walled yard");
        Assert.AreEqual(1, towers.Count, "Revivim's lookout tower");
        var yard = yards[0];
        // The tower's centre sits within a tower's width of one of the yard's corners.
        var (tu, tv) = Local(yard, towers[0].C);
        float cornerGap = new Vector2(yard.W * 0.5f - Mathf.Abs(tu), yard.D * 0.5f - Mathf.Abs(tv)).magnitude;
        if (cornerGap > towers[0].W) bad.Add($"the tower stands {cornerGap:0} m from the yard's nearest corner");
        // The larger living building is recorded as the dining hall (the fortress's dining hall; a kibbutz has one, #820).
        Assert.AreEqual(1, mine.Count(p => p.Kind == "dining_hall"), "Revivim's dining hall, on the living side");
        var houses = mine.Where(p => p.Kind == "building" || p.Kind == "dining_hall").ToList();
        Assert.AreEqual(4, houses.Count, "two living and two service buildings");
        Assert.GreaterOrEqual(houses.Count, TownCensusTests.MainBuildings["revivim"], "main's floor");
        // Every building inside the wall's inner face (the wall is 0.7 m thick at the house scale), on the yard's own axes.
        float inner = 0.5f * yard.W - 0.7f * KitScale.Houses + 1f;
        foreach (var h in houses)
        {
            var (u, v) = Local(yard, h.C);
            if (Mathf.Abs(h.Yaw - yard.Yaw) > 0.01f) bad.Add($"a building at {h.C} not square to the yard");
            if (Mathf.Abs(u) + 0.5f * h.W > inner || Mathf.Abs(v) + 0.5f * h.D > inner) bad.Add($"a building through the wall at {h.C}");
        }
        // No two of the buildings and the tower overlap (boxes on the yard's axes, 1 m tolerance).
        var boxes = houses.Append(towers[0]).ToList();
        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
            {
                var (ui, vi) = Local(yard, boxes[i].C); var (uj, vj) = Local(yard, boxes[j].C);
                if (Mathf.Abs(ui - uj) < 0.5f * (boxes[i].W + boxes[j].W) - 1f && Mathf.Abs(vi - vj) < 0.5f * (boxes[i].D + boxes[j].D) - 1f)
                    bad.Add($"{boxes[i].Kind} at {boxes[i].C} overlaps {boxes[j].Kind} at {boxes[j].C}");
            }
        if (mine.Any(p => p.Kind == "water_tower")) bad.Add("Revivim still has a moshava's water tower");
        CollectionAssert.IsEmpty(bad);
    }

    /// <summary><paramref name="p"/> in <paramref name="frame"/>'s own axes (KitMesh.Box's yaw), from its centre.</summary>
    static (float u, float v) Local(SettlementKits.Placed frame, Vector2 p)
    {
        float a = frame.Yaw * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        var d = p - frame.C;
        return (d.x * ca + d.y * sa, -d.x * sa + d.y * ca);
    }
}
