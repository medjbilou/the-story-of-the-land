using System.IO;
using StoryOfTheLand.Game.Boot;
using StoryOfTheLand.Game.Map.Routes;
using UnityEditor;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>Bakes the route ribbons (RouteLayer.Plan, RouteLayer.Derive) from the shipped heightmap and
    /// route_paths_1948.yaml into RouteRibbonFile.AssetPath, so a player places them instead of deriving them at boot
    /// (issue #321). Re-run it whenever the heightmap, heightmap.json, the route file or RouteLayer's geometry changes
    /// (RouteTests.TheBakedRibbonsMatchTheRoutes and tools/tests/test_route_ribbon_bake.py fail until then; the map also
    /// falls back to deriving them). Run headless: Unity -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit
    /// -projectPath unity -executeMethod StoryOfTheLand.Game.EditorTools.RouteRibbonBake.Bake</summary>
    public static class RouteRibbonBake
    {
        public static byte[] Bytes() => RouteRibbonFile.Bake(GameInputs.MapFromDisk(Paths.Content, Paths.StreamingMap));

        public static void Bake()
        {
            File.WriteAllBytes(RouteRibbonFile.AssetPath, Bytes());
            AssetDatabase.ImportAsset(RouteRibbonFile.AssetPath);
        }
    }
}
