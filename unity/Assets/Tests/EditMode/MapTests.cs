using NUnit.Framework;
using StoryOfTheLand.Game;
using StoryOfTheLand.Game.Map;
using UnityEditor;
using UnityEngine;

public class MapTests
{
    [Test]
    public void HeightmapRowZeroIsNorth_SoUnityRowZeroIsSouth()
    {
        // 3x3 raster: the north row is all 65535 and the rest 0. After loading, Unity row 2 (north) must be 1.
        var raw = new ushort[] { 65535, 65535, 65535, 0, 0, 0, 0, 0, 0 };
        var h = HeightmapLoader.ToUnityHeights(raw, 3);
        Assert.AreEqual(1f, h[2, 0], 1e-6); Assert.AreEqual(0f, h[0, 0], 1e-6);
    }

    [Test]
    public void GeoFrame_MapsCornersAndJerusalemEastOfTelAviv()
    {
        var f = new GeoFrame(34.2, 35.9, 29.45, 33.35, 161000f, 433000f);
        Assert.AreEqual(0f, f.ToWorld(34.2, 29.45, 0).x, 1e-3);
        Assert.AreEqual(433000f, f.ToWorld(35.9, 33.35, 0).z, 1);
        var telAviv = f.ToWorld(34.78, 32.08, 0); var jerusalem = f.ToWorld(35.22, 31.78, 0);
        Assert.Greater(jerusalem.x, telAviv.x);   // east
        Assert.Less(jerusalem.z, telAviv.z);      // south
    }

    [Test]
    public void PointyTopCornerZeroIsAtMinus30Degrees()
    {
        var c = HexMath.Corner(Vector2.zero, 1f, 0); // Red Blob: angle = 60*i - 30 degrees
        Assert.AreEqual(Mathf.Cos(-30 * Mathf.Deg2Rad), c.x, 1e-5);
        Assert.AreEqual(Mathf.Sin(-30 * Mathf.Deg2Rad), c.y, 1e-5);
    }

    [Test]
    public void TruncatedHeightmapIsRejectedWithExpectedSize()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hm_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "heightmap.json"),
            "{\"width\":3,\"height\":3,\"lon0\":34.2,\"lon1\":35.9,\"lat0\":29.45,\"lat1\":33.35,\"elev_min_m\":-427,\"elev_max_m\":2367,\"width_km\":161.5,\"length_km\":431.2,\"attribution\":\"x\"}");
        var twoByTwo = HeightmapCodec.Encode(new ushort[4], 2, 2);   // a valid stream of 8 bytes; 3x3 needs 18
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, HeightmapCodec.FileName), twoByTwo);
        var ex = Assert.Throws<System.IO.InvalidDataException>(() => HeightmapLoader.Load(dir));
        StringAssert.Contains("heightmap.r16z: 8 bytes after inflating, expected 18", ex.Message);
        var metaPath = System.IO.Path.Combine(dir, "heightmap.json");
        var fromBytes = Assert.Throws<System.IO.InvalidDataException>(() => HeightmapLoader.Load(System.IO.File.ReadAllText(metaPath), twoByTwo, metaPath));
        Assert.AreEqual(ex.Message, fromBytes.Message);
    }

    // M0 correction 1: no Shader.Find at runtime. Materials are editor-made assets passed into Build.
    const string TerrainMatPath = "Assets/Materials/Terrain.mat";

    static string HexesJson => System.IO.Path.Combine(Paths.Content, "map", "hexes.json");

    static Material TerrainMaterial()
    {
        var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMatPath);
        Assert.IsNotNull(terrainMat, TerrainMatPath);
        return terrainMat;
    }

    [Test]
    public void BuildUsesThePassedTerrainMaterialAndDrawsNoLineMesh()
    {
        var terrainMat = TerrainMaterial();
        Assert.AreEqual("StoryOfTheLand/StoryOfTheLandTerrain", terrainMat.shader.name);
        var go = MapBuilder.Build(Paths.StreamingMap, HexesJson, terrainMat);
        try
        {
            var terrain = go.GetComponent<Terrain>();
            Assert.AreSame(terrainMat, terrain.materialTemplate);
            Assert.IsNull(go.transform.Find("HexOverlay"), "the grid is drawn by the terrain shader (visual v1 Task 8)");
            Assert.AreEqual(2049, terrain.terrainData.heightmapResolution);
            Assert.AreEqual(1, terrain.terrainData.terrainLayers.Length);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void BuildRejectsMissingMaterials()
    {
        Assert.Throws<System.ArgumentNullException>(() => MapBuilder.Build(Paths.StreamingMap, HexesJson, null));
    }

    [Test]
    public void BiomeLayersAreMatte()
    {
        var terrainMat = TerrainMaterial();
        var go = MapBuilder.Build(Paths.StreamingMap, HexesJson, terrainMat);
        try
        {
            var layers = go.GetComponent<Terrain>().terrainData.terrainLayers;
            Assert.IsNotEmpty(layers);
            foreach (var l in layers)
            {
                Assert.AreEqual(0f, l.smoothness, $"{l.name} smoothness"); Assert.AreEqual(0f, l.metallic, $"{l.name} metallic");
                // URP Terrain/Lit uses diffuse alpha as smoothness and forces _SmoothnessN to 1 when the diffuse has alpha.
                Assert.IsFalse(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(l.diffuseTexture.graphicsFormat),
                    $"{l.name} diffuse must have no alpha channel");
            }
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void TerrainUsesFineLodSoTheCoarseSurfaceDoesNotCoverTheSelectionBand()
    {
        // At the default heightmapPixelError (5) far cameras get a coarse LOD whose surface sits above draped geometry (K2);
        // since visual v1 Task 8 that is the selection band (the hex grid is drawn by the terrain shader itself).
        var terrainMat = TerrainMaterial();
        var go = MapBuilder.Build(Paths.StreamingMap, HexesJson, terrainMat);
        try { Assert.AreEqual(MapBuilder.TerrainPixelError, go.GetComponent<Terrain>().heightmapPixelError); Assert.AreEqual(1f, MapBuilder.TerrainPixelError); }
        finally { Object.DestroyImmediate(go); }
    }

    // --- Runtime-created objects are owned and destroyed with the map (L5) ---

    [Test]
    public void DestroyingTheMapDestroysItsRuntimeAssets()
    {
        var terrainMat = TerrainMaterial();
        var go = MapBuilder.Build(Paths.StreamingMap, HexesJson, terrainMat);
        var data = go.GetComponent<Terrain>().terrainData;
        var owned = new System.Collections.Generic.List<Object> { data };
        foreach (var l in data.terrainLayers) { owned.Add(l); owned.Add(l.diffuseTexture); }
        Object.DestroyImmediate(go);
        foreach (var o in owned) Assert.IsTrue(o == null, $"{o} survived the map");
        Assert.IsTrue(terrainMat != null, "the material asset must not be destroyed");
    }

    // --- Biome blending (C2) ---

    const float S = 5000f; // hex size (circumradius) in metres
    static readonly float W = Mathf.Sqrt(3f) * S; // centre spacing of neighbouring pointy-top hexes

    [Test]
    public void TexelAtAHexCentreIsDominatedByThatHexsBiome()
    {
        var b = new BiomeBlender(new[] { Vector2.zero, new Vector2(W, 0) }, new[] { 0, 1 }, S, 3);
        var w = new float[3];
        b.Weights(Vector2.zero, w);
        Assert.Greater(w[0], 0.9f);
        b.Weights(new Vector2(W, 0), w);
        Assert.Greater(w[1], 0.9f);
    }

    [Test]
    public void TexelMidwayBetweenTwoBiomesBlendsBoth()
    {
        var b = new BiomeBlender(new[] { Vector2.zero, new Vector2(W, 0) }, new[] { 0, 1 }, S, 3);
        var w = new float[3];
        b.Weights(new Vector2(W / 2, 0), w);
        Assert.That(w[0], Is.InRange(0.3f, 0.7f)); Assert.That(w[1], Is.InRange(0.3f, 0.7f));
        Assert.AreEqual(0f, w[2], 1e-6);
    }

    [Test]
    public void BlendBandIsAboutSixTenthsOfAHex()
    {
        // Along the line between two neighbouring centres, the band where both biomes have > 10% weight spans ~0.6 hex widths.
        var b = new BiomeBlender(new[] { Vector2.zero, new Vector2(W, 0) }, new[] { 0, 1 }, S, 2);
        var w = new float[2]; float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i <= 1000; i++)
        {
            float x = W * i / 1000f; b.Weights(new Vector2(x, 0), w);
            if (w[0] > 0.1f && w[1] > 0.1f) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
        }
        Assert.That((hi - lo) / W, Is.InRange(0.55f, 0.65f));
    }

    [Test]
    public void BlendWeightsSumToOneEverywhere()
    {
        // A 7-hex flower with mixed biomes, sampled on a grid that also reaches outside it (fallback to nearest).
        var centres = new System.Collections.Generic.List<Vector2> { Vector2.zero };
        for (int i = 0; i < 6; i++) { float a = Mathf.Deg2Rad * 60f * i; centres.Add(new Vector2(W * Mathf.Cos(a), W * Mathf.Sin(a))); }
        var b = new BiomeBlender(centres, new[] { 0, 1, 2, 3, 1, 2, 0 }, S, 4);
        var w = new float[4];
        for (float x = -3 * S; x <= 3 * S; x += S / 7f)
            for (float y = -3 * S; y <= 3 * S; y += S / 7f)
            {
                b.Weights(new Vector2(x, y), w);
                float sum = 0; foreach (var v in w) { Assert.GreaterOrEqual(v, 0f); sum += v; }
                Assert.AreEqual(1f, sum, 1e-5, $"at {x},{y}");
            }
    }

    // --- Robustness (M2) ---

    static string TempFile(string name, string text)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "map_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var p = System.IO.Path.Combine(dir, name); System.IO.File.WriteAllText(p, text); return p;
    }

    [TestCase("", "empty")]
    [TestCase("   ", "empty")]
    [TestCase("{\"hex_size_km\":5,\"layout\":\"pointy\"}", "no hexes")]
    [TestCase("{\"hex_size_km\":5,\"layout\":\"pointy\",\"hexes\":null}", "no hexes")]
    [TestCase("{\"hex_size_km\":5,\"layout\":\"pointy\",\"hexes\":[]}", "no hexes")]
    [TestCase("{\"hex_size_km\":0,\"layout\":\"pointy\",\"hexes\":[{\"q\":0,\"r\":0,\"biome\":\"sea\"}]}", "hex_size_km")]
    [TestCase("{\"hex_size_km\":-5,\"layout\":\"pointy\",\"hexes\":[{\"q\":0,\"r\":0,\"biome\":\"sea\"}]}", "hex_size_km")]
    [TestCase("{\"hex_size_km\":5,\"layout\":\"flat\",\"hexes\":[{\"q\":0,\"r\":0,\"biome\":\"sea\"}]}", "layout")]
    [TestCase("{\"hex_size_km\":5,\"layout\":\"pointy\",\"hexes\":[{\"q\":3,\"r\":4,\"biome\":\"jungle\"}]}", "unknown biome 'jungle' at q=3 r=4")]
    [TestCase("{\"hex_size_km\":5,\"layout\":", "malformed JSON")]
    [TestCase("not json at all", "malformed JSON")]
    public void BadHexesJsonIsRejectedNamingFileAndProblem(string json, string problem)
    {
        var path = TempFile("hexes.json", json);
        var ex = Assert.Throws<System.IO.InvalidDataException>(() => MapBuilder.LoadHexes(path));
        StringAssert.Contains(path, ex.Message); StringAssert.Contains(problem, ex.Message);
        var fromText = Assert.Throws<System.IO.InvalidDataException>(() => MapBuilder.LoadHexes(json, path));
        Assert.AreEqual(ex.Message, fromText.Message);
    }

    const string GoodMeta = "\"lon0\":34.2,\"lon1\":35.9,\"lat0\":29.45,\"lat1\":33.35,\"elev_min_m\":-427,\"elev_max_m\":2367";

    [TestCase("", "empty")]
    [TestCase("{\"width\":0,\"height\":0,\"width_km\":161.5,\"length_km\":431.2," + GoodMeta + "}", "width")]
    [TestCase("{\"width\":-3,\"height\":-3,\"width_km\":161.5,\"length_km\":431.2," + GoodMeta + "}", "width")]
    [TestCase("{\"width\":3,\"height\":3,\"width_km\":0,\"length_km\":431.2," + GoodMeta + "}", "width_km")]
    [TestCase("{\"width\":3,\"height\":3,\"width_km\":161.5,\"length_km\":-1," + GoodMeta + "}", "length_km")]
    [TestCase("{\"width\":3,\"height\":3,\"width_km\":161.5,", "malformed JSON")]
    [TestCase("not json at all", "malformed JSON")]
    [TestCase("{\"width\":3,\"height\":3,\"width_km\":161.5,\"length_km\":431.2,\"lon0\":35.9,\"lon1\":35.9,\"lat0\":29.45,\"lat1\":33.35,\"elev_min_m\":-427,\"elev_max_m\":2367}", "lon1")]
    [TestCase("{\"width\":3,\"height\":3,\"width_km\":161.5,\"length_km\":431.2,\"lon0\":35.9,\"lon1\":34.2,\"lat0\":29.45,\"lat1\":33.35,\"elev_min_m\":-427,\"elev_max_m\":2367}", "lon1")]
    [TestCase("{\"width\":3,\"height\":3,\"width_km\":161.5,\"length_km\":431.2,\"lon0\":34.2,\"lon1\":35.9,\"lat0\":33.35,\"lat1\":33.35,\"elev_min_m\":-427,\"elev_max_m\":2367}", "lat1")]
    [TestCase("{\"width\":3,\"height\":3,\"width_km\":161.5,\"length_km\":431.2,\"lon0\":34.2,\"lon1\":35.9,\"lat0\":33.35,\"lat1\":29.45,\"elev_min_m\":-427,\"elev_max_m\":2367}", "lat1")]
    [TestCase("{\"width\":3,\"height\":3,\"width_km\":161.5,\"length_km\":431.2,\"lon0\":34.2,\"lon1\":35.9,\"lat0\":29.45,\"lat1\":33.35,\"elev_min_m\":100,\"elev_max_m\":100}", "elev_max_m")]
    [TestCase("{\"width\":3,\"height\":3,\"width_km\":161.5,\"length_km\":431.2,\"lon0\":34.2,\"lon1\":35.9,\"lat0\":29.45,\"lat1\":33.35,\"elev_min_m\":2367,\"elev_max_m\":-427}", "elev_max_m")]
    public void BadHeightmapMetaIsRejected(string json, string problem)
    {
        var meta = TempFile("heightmap.json", json);
        var dir = System.IO.Path.GetDirectoryName(meta);
        var packed = HeightmapCodec.Encode(new ushort[9], 3, 3);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, HeightmapCodec.FileName), packed);
        var ex = Assert.Throws<System.IO.InvalidDataException>(() => HeightmapLoader.Load(dir));
        StringAssert.Contains("heightmap.json", ex.Message); StringAssert.Contains(problem, ex.Message);
        var fromText = Assert.Throws<System.IO.InvalidDataException>(() => HeightmapLoader.Load(json, packed, meta));
        Assert.AreEqual(ex.Message, fromText.Message);
    }

    [Test]
    public void UnknownBiomeIsRejectedBeforeAnyObjectIsCreated()
    {
        var terrainMat = TerrainMaterial();
        var path = TempFile("hexes.json", "{\"hex_size_km\":5,\"layout\":\"pointy\",\"hexes\":[{\"q\":0,\"r\":0,\"biome\":\"sea\"},{\"q\":1,\"r\":0,\"biome\":\"tundra\"}]}");
        int terrainsBefore = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length;
        int dataBefore = Resources.FindObjectsOfTypeAll<TerrainData>().Length;
        var ex = Assert.Throws<System.IO.InvalidDataException>(() => MapBuilder.Build(Paths.StreamingMap, path, terrainMat));
        StringAssert.Contains("unknown biome 'tundra'", ex.Message);
        Assert.AreEqual(terrainsBefore, Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length, "orphan Terrain left behind");
        Assert.AreEqual(dataBefore, Resources.FindObjectsOfTypeAll<TerrainData>().Length, "orphan TerrainData left behind");
    }

    // --- Text/bytes inputs, for the WebGL build that cannot use System.IO on streamingAssetsPath (final fix 3, I2) ---

    static string MetaPath => System.IO.Path.Combine(Paths.StreamingMap, "heightmap.json");
    static string R16Path => System.IO.Path.Combine(Paths.StreamingMap, HeightmapCodec.FileName);

    [Test]
    public void HeightmapFromBytesMatchesHeightmapFromPath()
    {
        var (pm, ph) = HeightmapLoader.Load(Paths.StreamingMap);
        var (bm, bh) = HeightmapLoader.Load(System.IO.File.ReadAllText(MetaPath), System.IO.File.ReadAllBytes(R16Path), MetaPath);
        Assert.AreEqual(JsonUtility.ToJson(pm), JsonUtility.ToJson(bm));
        Assert.AreEqual(ph.GetLength(0), bh.GetLength(0)); Assert.AreEqual(ph.GetLength(1), bh.GetLength(1));
        int differ = 0;
        for (int y = 0; y < ph.GetLength(0); y++) for (int x = 0; x < ph.GetLength(1); x++) if (ph[y, x] != bh[y, x]) differ++;
        Assert.AreEqual(0, differ, "height samples that differ");
    }

    [Test]
    public void HexesFromTextMatchHexesFromPath()
    {
        var fromPath = MapBuilder.LoadHexes(HexesJson);
        var fromText = MapBuilder.LoadHexes(System.IO.File.ReadAllText(HexesJson), HexesJson);
        Assert.AreEqual(JsonUtility.ToJson(fromPath), JsonUtility.ToJson(fromText));
    }

    [Test]
    public void BuildFromReadInputsMatchesBuildFromPaths()
    {
        var terrainMat = TerrainMaterial();
        var a = MapBuilder.Build(Paths.StreamingMap, HexesJson, terrainMat);
        var (meta, heights) = HeightmapLoader.Load(System.IO.File.ReadAllText(MetaPath), System.IO.File.ReadAllBytes(R16Path), MetaPath);
        var b = MapBuilder.Build(meta, heights, MapBuilder.LoadHexes(System.IO.File.ReadAllText(HexesJson), HexesJson), terrainMat);
        try
        {
            var ta = a.GetComponent<Terrain>().terrainData; var tb = b.GetComponent<Terrain>(); Assert.IsNotNull(tb, "terrain from read inputs");
            Assert.AreEqual(ta.size, tb.terrainData.size);
            Assert.AreEqual(ta.heightmapResolution, tb.terrainData.heightmapResolution);
            Assert.AreEqual(ta.GetHeights(0, 0, ta.heightmapResolution, ta.heightmapResolution), tb.terrainData.GetHeights(0, 0, ta.heightmapResolution, ta.heightmapResolution));
        }
        finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
    }

    // --- The packed heightmap (issue #44): smaller download, the same terrain ---

    /// <summary>SHA-256 of the raw heightmap.r16 (8,396,802 bytes) shipped before issue #44 packed it.</summary>
    const string RawHeightmapSha256 = "b6882a77420efa90217cc65a9c353147569094cf16656d4d53c30abe0f41ac9e";

    [Test]
    public void ThePackedHeightmapDecodesToTheOldRawFileByteForByte()
    {
        var meta = HeightmapLoader.Load(Paths.StreamingMap).meta;
        var packed = System.IO.File.ReadAllBytes(R16Path);
        var raw = HeightmapCodec.Decode(packed, meta.width, meta.height);
        var bytes = new byte[raw.Length * 2];
        System.Buffer.BlockCopy(raw, 0, bytes, 0, bytes.Length);   // little-endian, as the raw file was
        using var sha = System.Security.Cryptography.SHA256.Create();
        Assert.AreEqual(RawHeightmapSha256, System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant());
        Assert.Less(packed.Length, 0.55 * bytes.Length, "the packed file is about half the raw grid");
        Assert.IsFalse(System.IO.File.Exists(System.IO.Path.Combine(Paths.StreamingMap, "heightmap.r16")), "the raw file is not shipped beside it");
    }

    [Test]
    public void TheCodecRoundTripsWraparoundDeltasAndRejectsACorruptStream()
    {
        var rng = new System.Random(44);
        foreach (var (w, h) in new[] { (1, 1), (3, 3), (5, 7), (64, 64) })
        {
            var raw = new ushort[w * h];
            for (int i = 0; i < raw.Length; i++) raw[i] = (ushort)rng.Next(0, 65536);
            raw[0] = 65535; raw[raw.Length - 1] = 0;
            CollectionAssert.AreEqual(raw, HeightmapCodec.Decode(HeightmapCodec.Encode(raw, w, h), w, h), $"{w}x{h}");
        }
        var packed = HeightmapCodec.Encode(new ushort[] { 1, 3, 0x0102, 0x0101 }, 2, 2);
        Assert.AreEqual(1u, HeightmapCodec.Adler32(new byte[0]));
        Assert.AreEqual(0x11E60398u, HeightmapCodec.Adler32(System.Text.Encoding.ASCII.GetBytes("Wikipedia")), "the RFC 1950 example");
        var corrupt = (byte[])packed.Clone(); corrupt[corrupt.Length - 1] ^= 1;
        StringAssert.Contains("checksum", Assert.Throws<System.IO.InvalidDataException>(() => HeightmapCodec.Decode(corrupt, 2, 2)).Message);
        StringAssert.Contains("not a zlib stream", Assert.Throws<System.IO.InvalidDataException>(() => HeightmapCodec.Decode(new byte[18], 3, 3)).Message);
        StringAssert.Contains("more than 2 bytes", Assert.Throws<System.IO.InvalidDataException>(() => HeightmapCodec.Decode(packed, 1, 1)).Message);
    }

    /// <summary>Review M1: a damaged file either fails with an InvalidDataException naming the file, whichever exception the
    /// platform's DeflateStream raises (Mono's native zlib reports a data error as an IOException), or still decodes to
    /// exactly the original grid (the size and the Adler-32 of the contents are checked; flipping only the final-block bit,
    /// for one, leaves the contents intact). Every byte of the file is flipped, one at a time.</summary>
    [Test]
    public void AByteFlippedAnywhereFailsNamingTheFileOrChangesNothing()
    {
        var rng = new System.Random(7);
        var raw = new ushort[32 * 32];
        for (int i = 0; i < raw.Length; i++) raw[i] = (ushort)(i * 37 + rng.Next(0, 300));   // compressible: Huffman blocks
        var packed = HeightmapCodec.Encode(raw, 32, 32);
        int failed = 0, intact = 0;
        foreach (byte mask in new byte[] { 0x01, 0x10, 0xFF })
            for (int at = 0; at < packed.Length; at++)
            {
                var bad = (byte[])packed.Clone(); bad[at] ^= mask;
                ushort[] got;
                try { got = HeightmapCodec.Decode(bad, 32, 32); }
                catch (System.IO.InvalidDataException e) { StringAssert.StartsWith("heightmap.r16z: ", e.Message, $"byte {at} ^ {mask:x2}"); failed++; continue; }
                catch (System.Exception e) { Assert.Fail($"byte {at} ^ {mask:x2}: {e.GetType().Name} {e.Message}"); throw; }
                CollectionAssert.AreEqual(raw, got, $"byte {at} ^ {mask:x2} decoded to a different grid");
                intact++;
            }
        Assert.Greater(failed, 3 * packed.Length * 9 / 10, $"{intact} of {3 * packed.Length} damaged files decoded intact");
    }

    [Test]
    public void TheLayoutIsRowDeltasInLowThenHighBytePlanes()
    {
        // tools/tests/test_heightmap_codec.py pins the same layout for the Python encoder; the cross-language guarantee is
        // ThePackedHeightmapDecodesToTheOldRawFileByteForByte (the committed file is written by the Python tool).
        var packed = HeightmapCodec.Encode(new ushort[] { 1, 3, 0x0102, 0x0101 }, 2, 2);
        using var body = new System.IO.MemoryStream(packed, 2, packed.Length - 6);
        using var inflate = new System.IO.Compression.DeflateStream(body, System.IO.Compression.CompressionMode.Decompress);
        var planes = new byte[8]; int got = 0, r;
        while ((r = inflate.Read(planes, got, planes.Length - got)) > 0) got += r;
        CollectionAssert.AreEqual(new byte[] { 1, 2, 0x02, 0xFF, 0, 0, 0x01, 0xFF }, planes);
    }

    // --- hexes.json and the heightmap must cover the same area (final fix 3, M3) ---

    [TestCase(1e-6, 0, 0, 0)]
    [TestCase(0, -1e-6, 0, 0)]
    [TestCase(0, 0, 1e-6, 0)]
    [TestCase(0, 0, 0, -1e-6)]
    public void HexBoundsThatDifferFromTheHeightmapAreRejectedBeforeAnyObjectIsCreated(double dLon0, double dLon1, double dLat0, double dLat1)
    {
        var terrainMat = TerrainMaterial();
        var meta = HeightmapLoader.Load(Paths.StreamingMap).meta;
        string D(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var json = "{\"bounds\":{\"lon0\":" + D(meta.lon0 + dLon0) + ",\"lon1\":" + D(meta.lon1 + dLon1) + ",\"lat0\":" + D(meta.lat0 + dLat0) + ",\"lat1\":" + D(meta.lat1 + dLat1)
            + "},\"hex_size_km\":5,\"layout\":\"pointy\",\"hexes\":[{\"q\":0,\"r\":0,\"lon\":35,\"lat\":32,\"land\":true,\"biome\":\"hills\"}]}";
        var path = TempFile("hexes.json", json);
        int terrainsBefore = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length;
        var ex = Assert.Throws<System.IO.InvalidDataException>(() => MapBuilder.Build(Paths.StreamingMap, path, terrainMat));
        StringAssert.Contains("hexes.json", ex.Message); StringAssert.Contains("heightmap.json", ex.Message); StringAssert.Contains("bounds", ex.Message);
        Assert.AreEqual(terrainsBefore, Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length, "orphan Terrain left behind");
    }

    [Test]
    public void RealHexesJsonCarriesTheHeightmapBounds()
    {
        var b = MapBuilder.LoadHexes(HexesJson).bounds; var meta = HeightmapLoader.Load(Paths.StreamingMap).meta;
        Assert.AreEqual(meta.lon0, b.lon0); Assert.AreEqual(meta.lon1, b.lon1); Assert.AreEqual(meta.lat0, b.lat0); Assert.AreEqual(meta.lat1, b.lat1);
    }
}
