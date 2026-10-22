#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StoryOfTheLand.Game.Map;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Issue #195: which of a hex's edges border another region is baked into its own texel (MapBorders.Use), so the
/// terrain's shader reads one texel, not seven: the neighbours' six fetches had pushed the terrain's fragment shader past a
/// WebGL cost cliff (about 5 ms a frame at gameplay03, borders on or off). BorderTests prove the drawing unchanged.</summary>
public class BorderEdgeTests
{
    [TearDown]
    public void Reset() { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); VisualToggles.Clear(); }

    [Test]
    public void UseBakesWhichEdgesBorderAnotherRegion()
    {
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>(); map.Build();
        // A hex whose six neighbours are on the map: region 1 (primary) at the centre and at neighbours 0 and 3, region 2
        // (secondary) at neighbour 1, neighbour 2 in no region (no band against it), region 4 (primary, like the centre: a
        // shared edge, each side draws part of the band) at neighbour 4, region 3 (style none: still another region, as the
        // "outside" beyond the Mandate frontier) at neighbour 5.
        var steps = MapBorders.NeighbourSteps;
        var onMap = new HashSet<(int q, int r)>(map.Hexes.hexes.Select(h => (h.q, h.r)));
        var c = map.Hexes.hexes.First(h => steps.All(s => onMap.Contains((h.q + s.dq, h.r + s.dr))));
        int[] ids = { 1, 2, 0, 1, 4, 3 };
        var hexes = new Dictionary<(int q, int r), int> { [(c.q, c.r)] = 1 };
        for (int i = 0; i < 6; i++) if (ids[i] > 0) hexes[(c.q + steps[i].dq, c.r + steps[i].dr)] = ids[i];
        var styles = new Dictionary<int, BorderStyle> { [1] = BorderStyle.Primary, [2] = BorderStyle.Secondary, [3] = BorderStyle.None, [4] = BorderStyle.Primary };
        MapBorders.Use(new RegionLayer("edges", hexes, styles));
        var tex = (Texture2D)Shader.GetGlobalTexture("_ScRegionTex");
        var info = Shader.GetGlobalVector("_ScRegionInfo");
        Color32 At(int q, int r) => tex.GetPixel(q - (int)info.x, r - (int)info.y);
        var centre = At(c.q, c.r);
        // b: the style (primary, 1) in the low two bits, and above them the shared edges: only edge 4, whose neighbour is
        // primary too. Edge 1 (against secondary) and edge 5 (against style none, the frontier) keep the full band.
        Assert.AreEqual((1, 0, 1 | 1 << 4 << 2), ((int)centre.r, (int)centre.g, (int)centre.b), "region, style and the shared edge 4");
        Assert.AreEqual(0xC0 | 0b110010, (int)centre.a, "edges 1, 4 and 5 border another region; 0 and 3 are its own, 2 none");
        var n4 = At(c.q + steps[4].dq, c.r + steps[4].dr);
        Assert.AreEqual(1, n4.b & 3, "neighbour 4 is primary");
        Assert.AreNotEqual(0, (n4.b >> 2) & 1 << 1, "neighbour 4 shares its edge 1 (the opposite of 4) with the centre");
        Assert.AreEqual(2, (int)At(c.q + steps[1].dq, c.r + steps[1].dr).b, "the secondary neighbour shares no edge with the primary centre");
        Assert.AreEqual(0, (int)At(c.q + steps[5].dq, c.r + steps[5].dr).b, "a region of style none draws nothing, so shares nothing");
        Assert.AreEqual(0xC0 | 1 << 4 | 1 << 5, (int)At(c.q + steps[1].dq, c.r + steps[1].dr).a,
            "neighbour 1 sees the centre across its edge 4 (the opposite of 1) and neighbour 0 across its edge 5");
        MapBorders.Use(null);
        Assert.AreEqual(0, (int)At(c.q, c.r).a, "no layer: a cleared texel");
    }

    [Test]
    public void TheBordersReadOneTexelAPixel()
    {
        var src = Regex.Replace(File.ReadAllText("Assets/Shaders/StoryOfTheLandBorders.hlsl"), "//[^\n]*", "");
        Assert.AreEqual(1, Regex.Matches(src, @"ScRegionAt\(").Count - 1, "ScRegionAt called once (its definition aside)");
        Assert.AreEqual(1, Regex.Matches(src, "LOAD_TEXTURE2D").Count);
    }
}
