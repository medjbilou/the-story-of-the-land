#nullable enable
using System.IO;
using System.Linq;
using StoryOfTheLand.Game.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Renders the start screen's painting, Assets/UI/StartScreen/start-map.png (StartScreenTests): the 3D map alone at
/// 1920x1080 on the desktop tier, as MapCapture renders its promo poses: no UI (an editor render draws no UI Toolkit, so no
/// top bar, labels or legend), no units, no sub-district or armistice lines (MapRegions is never applied) and the hex grid
/// off. Run headless with graphics: Unity -batchmode -projectPath unity -executeMethod StartArtCapture.Render -quit.
/// -captureDir dir with any -startPose name:lon:lat:distanceM:yaw renders those poses there instead, to choose one.</summary>
public static class StartArtCapture
{
    public const string ArtPath = "Assets/UI/StartScreen/start-map.png";

    /// <summary>PromoPose's start120 (the game's 120 km starting distance over the centre, looking north): the sea, the
    /// coastal plain and the hills up to the Sea of Galilee.</summary>
    public static readonly PromoPose Pose = new PromoPose("start120", 35.00, 32.20, 120000f, 0f);

    public static void Render()
    {
        var args = System.Environment.GetCommandLineArgs();
        int at = System.Array.IndexOf(args, "-captureDir");
        var dir = at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
        var map = Object.FindFirstObjectByType<MapScene>();
        map.Build();
        if (VisualToggles.Names.Contains("grid")) VisualToggles.Set("grid", false);
        var cam = Camera.main; var rig = cam.GetComponent<CameraRig>();
        rig.TopInset = 0;
        int saved = QualitySettings.GetQualityLevel();
        try
        {
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "PC"), true);
            MapShaderGlobals.PinWaterTime(100f);
            if (dir == null)
            {
                Pose.Apply(rig, map.Frame);
                File.WriteAllBytes(ArtPath, Png(cam));   // the .meta and its GUID stay
                AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"START ART {ArtPath}");
                return;
            }
            Directory.CreateDirectory(dir);
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-startPose") continue;
                var f = args[i + 1].Split(':');
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var pose = new PromoPose(f[0], double.Parse(f[1], ci), double.Parse(f[2], ci), float.Parse(f[3], ci), float.Parse(f[4], ci));
                pose.Apply(rig, map.Frame);
                File.WriteAllBytes(Path.Combine(dir, pose.Name + ".png"), Png(cam));
            }
        }
        finally { MapShaderGlobals.PinWaterTime(null); QualitySettings.SetQualityLevel(saved, true); }
    }

    static byte[] Png(Camera cam)
    {
        var sample = RenderSample.Render(cam, 1920, 1080);
        var tex = new Texture2D(sample.Width, sample.Height, TextureFormat.RGB24, false);
        try { tex.SetPixels(sample.Pixels); tex.Apply(); return tex.EncodeToPNG(); }
        finally { Object.DestroyImmediate(tex); }
    }
}
