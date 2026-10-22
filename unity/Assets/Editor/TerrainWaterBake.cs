using System.IO;
using StoryOfTheLand.Game.Map;
using UnityEditor;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>Bakes the terrain water's mesh (MapBuilder.WaterMeshGrid) from the shipped heightmap into
    /// MapTexturePaths.WaterMesh, so a player places its vertices instead of deriving it at boot (issue #195). Re-run it
    /// whenever heightmap.r16z changes (SeaTests fails until then; the map also falls back to deriving it). Run headless:
    /// Unity -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -executeMethod
    /// StoryOfTheLand.Game.EditorTools.TerrainWaterBake.Bake</summary>
    public static class TerrainWaterBake
    {
        public static byte[] Bytes()
        {
            var (meta, heights) = HeightmapLoader.Load(Paths.StreamingMap);
            var (grid, indices) = MapBuilder.WaterMeshGrid(meta.width, MapBuilder.TerrainSize(meta), heights, meta.elev_min_m);
            return MapBuilder.WaterMeshBytes(meta.width, heights, grid, indices);
        }

        public static void Bake()
        {
            File.WriteAllBytes(MapTexturePaths.WaterMesh, Bytes());
            AssetDatabase.ImportAsset(MapTexturePaths.WaterMesh);
        }
    }
}
