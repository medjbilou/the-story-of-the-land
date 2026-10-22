#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using StoryOfTheLand.Game.Map;
using UnityEngine;

/// <summary>Playtest 2, P2-1: the biome blend was the largest avoidable boot phase (4.4 s of 14.3 s at an 8x slower CPU).
/// Its grid moved from a tuple-keyed dictionary to flat arrays; the weights must stay bit-identical to the original
/// algorithm (kept here as the reference), so the map looks exactly the same.</summary>
public class BiomeBlenderSpeedTests
{
    /// <summary>The original BiomeBlender.Weights, verbatim in its arithmetic and iteration order.</summary>
    sealed class Reference
    {
        readonly Dictionary<(int, int), List<(Vector2 p, int layer)>> grid = new Dictionary<(int, int), List<(Vector2, int)>>();
        readonly List<(Vector2 p, int layer)> all = new List<(Vector2, int)>();
        readonly float radius, cell; readonly int layerCount;
        public Reference(IReadOnlyList<Vector2> centres, IReadOnlyList<int> layers, float hexSizeM, int layerCount)
        {
            this.layerCount = layerCount; radius = BiomeBlender.BlendRadiusHexes * hexSizeM; cell = radius;
            for (int i = 0; i < centres.Count; i++)
            {
                var c = (centres[i], layers[i]); all.Add(c);
                var k = Key(centres[i]);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<(Vector2, int)>();
                l.Add(c);
            }
        }
        (int, int) Key(Vector2 p) => (Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell));
        public void Weights(Vector2 p, float[] w)
        {
            Array.Clear(w, 0, w.Length);
            var (cx, cy) = Key(p); float total = 0f;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (grid.TryGetValue((cx + dx, cy + dy), out var l))
                        foreach (var c in l)
                        {
                            float t = (c.p - p).magnitude / radius;
                            if (t >= 1f) continue;
                            float f = 1f - t * t * (3f - 2f * t);
                            w[c.layer] += f; total += f;
                        }
            if (total > 0f) { for (int i = 0; i < w.Length; i++) w[i] /= total; return; }
            float best = float.MaxValue; int layer = 0;
            foreach (var c in all) { float d = (c.p - p).sqrMagnitude; if (d < best) { best = d; layer = c.layer; } }
            w[layer] = 1f;
        }
    }

    [Test]
    public void TheFastBlendGivesExactlyTheOriginalWeights()
    {
        var rng = new System.Random(1948);
        var centres = new List<Vector2>(); var layers = new List<int>();
        const float hex = 5000f;
        for (int q = -3; q < 40; q++)                                   // a pointy-top hex lattice, like the map's
            for (int r = 0; r < 90; r++)
            {
                centres.Add(new Vector2(hex * Mathf.Sqrt(3) * (q + r / 2f), hex * 1.5f * r));
                layers.Add(rng.Next(7));
            }
        var fast = new BiomeBlender(centres, layers, hex, 7);
        var reference = new Reference(centres, layers, hex, 7);
        float[] a = new float[7], b = new float[7];
        for (int i = 0; i < 20000; i++)
        {
            // inside the lattice, at its edges and well outside it (the nearest-centre fallback)
            var p = new Vector2((float)(rng.NextDouble() * 400000 - 60000), (float)(rng.NextDouble() * 760000 - 60000));
            fast.Weights(p, a); reference.Weights(p, b);
            for (int k = 0; k < 7; k++)
                Assert.AreEqual(BitConverter.SingleToInt32Bits(b[k]), BitConverter.SingleToInt32Bits(a[k]), $"point {p} layer {k}: {a[k]} vs {b[k]}");
        }
    }
}
