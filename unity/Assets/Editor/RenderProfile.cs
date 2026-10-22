#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using StoryOfTheLand.Game.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>F6: the frame-time profile when no player can draw (a locked Mac: a windowed player pauses without focus, and a
    /// -batchmode player renders nothing, so its -vprofile reads about 0.1 ms). In the editor, with graphics, it renders the map
    /// camera into a 1920x1080 target with the tier's own MSAA, once per frame, and waits for the GPU each time (a 1-pixel
    /// read-back), so a frame's time is the CPU's submit plus the GPU's work, without the swap chain or vsync. Every quality
    /// tier, the promo poses and the close-ups (PromoPose.All, CloseUps) plus Tantura and the Judean hills; per pose the
    /// configurations "all", each of <see cref="Toggles"/> off alone, and "none", Repeats times in FrameProfile.Order. The lines
    /// are FrameProfile's (the pose named "tier/pose"). Comparable with itself (before / after on the same machine), not with
    /// a player's numbers. Run:
    /// Unity -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -buildTarget OSXUniversal
    /// -executeMethod StoryOfTheLand.Game.EditorTools.RenderProfile.Run -logFile &lt;file&gt; (under tools/machine_lock.py --exclusive).</summary>
    public static class RenderProfile
    {
        public const int WarmupFrames = 30, MeasuredFrames = 200, Repeats = 3, Width = 1920, Height = 1080;
        public static readonly string[] Toggles = { "detail", "kits", "shadows", "tiltshift", "trees" };

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Map.unity", OpenSceneMode.Single);
            var map = Object.FindFirstObjectByType<MapScene>() ?? throw new System.InvalidOperationException("Assets/Map.unity has no MapScene");
            map.Build();
            var cam = Camera.main; var rig = cam.GetComponent<CameraRig>();
            // Optional subsets for a quick look (comma lists): -rpPoses tantura15,jaffa12 -rpConfigs none,all -rpTiers Mobile.
            var args = System.Environment.GetCommandLineArgs();
            string[]? Only(string flag) { int i = System.Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length ? args[i + 1].Split(',') : null; }
            var onlyPoses = Only("-rpPoses"); var onlyConfigs = Only("-rpConfigs"); var onlyTiers = Only("-rpTiers");
            var poses = new List<PromoPose>(PromoPose.All);
            poses.AddRange(PromoPose.CloseUps);
            poses.Add(new PromoPose("tantura15", 34.925, 32.61, 15000f, 10f));
            poses.Add(new PromoPose("jerusalemhills20", 35.17, 31.79, 20000f, 0f));
            var configs = new List<(string label, string? off)> { ("all", null) };
            foreach (var t in Toggles) if (VisualToggles.Names.Contains(t)) configs.Add(("-" + t, t));
            configs.Add(("none", "*"));
            if (onlyConfigs != null) configs = configs.Where(c => onlyConfigs.Contains(c.label)).ToList();
            if (onlyPoses != null) poses = poses.Where(p => onlyPoses.Contains(p.Name)).ToList();
            // Which allocation counter this runtime keeps (FrameProfile's gc_kb_frame): a known 1 MB array through both. On
            // Unity 6.6's Mono the thread counter reads 0 (not kept), so the player's profile uses the heap's growth.
            long t0 = System.GC.GetAllocatedBytesForCurrentThread(), m0 = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            var junk = new byte[1 << 20]; System.GC.KeepAlive(junk);
            Debug.Log($"PROFILE allocprobe 1MB thread_bytes={System.GC.GetAllocatedBytesForCurrentThread() - t0} " +
                      $"mono_used={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - m0}");
            var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            int saved = QualitySettings.GetQualityLevel();
            var clock = new Stopwatch();
            try
            {
                for (int level = 0; level < QualitySettings.names.Length; level++)
                {
                    if (onlyTiers != null && !onlyTiers.Contains(QualitySettings.names[level])) continue;
                    QualitySettings.SetQualityLevel(level, true);
                    int msaa = UniversalRenderPipeline.asset != null ? UniversalRenderPipeline.asset.msaaSampleCount : 1;
                    var rt = new RenderTexture(Width, Height, 24) { antiAliasing = msaa };
                    cam.targetTexture = rt;
                    Debug.Log($"PROFILE tier={QualitySettings.names[level]} msaa={msaa} editor-render {Width}x{Height}");
                    try
                    {
                        foreach (var pose in poses)
                        {
                            pose.Apply(rig, map.Frame);
                            var name = QualitySettings.names[level] + "/" + pose.Name;
                            var results = configs.ToDictionary(c => c.label, c => new List<(float avg, float p50, float p95, float p99)>());
                            for (int rep = 0; rep < Repeats; rep++)
                                foreach (var i in FrameProfile.Order(configs.Count, rep))
                                {
                                    var (label, off) = configs[i];
                                    VisualToggles.SetAll(off != "*");
                                    if (off != null && off != "*") VisualToggles.Set(off, false);
                                    var times = new List<float>(MeasuredFrames);
                                    for (int f = 0; f < WarmupFrames + MeasuredFrames; f++)
                                    {
                                        clock.Restart();
                                        cam.Render();
                                        RenderTexture.active = rt; probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false); RenderTexture.active = null;
                                        if (f >= WarmupFrames) times.Add((float)clock.Elapsed.TotalMilliseconds);
                                    }
                                    Debug.Log(FrameProfile.Line(label, name, $"{Width}x{Height}", UniversalRenderPipeline.asset != null ? UniversalRenderPipeline.asset.renderScale : 1f, rep, times));
                                    // The map's own draw counts for the last render (UnityStats reads 0 for a scripted cam.Render in batch
                                    // mode): the kits' merged static meshes, the trees' instanced calls and instances, the close models'.
                                    if (rep == 0)
                                    {
                                        var veg = map.GetComponentInChildren<StoryOfTheLand.Game.Map.Kits.VegetationLayer>();
                                        var towns = map.GetComponentInChildren<StoryOfTheLand.Game.Map.Kits.TownModelLayer>();
                                        Debug.Log($"PROFILE stats visual={label} pose={name} kit_draws={map.KitStats?.Draws ?? 0} " +
                                                  $"tree_draws={(veg != null ? veg.LastDraws : 0)} tree_instances={(veg != null ? veg.LastDrawn : 0)} " +
                                                  $"model_draws={(towns != null ? towns.LastDraws : 0)} model_instances={(towns != null ? towns.LastDrawn : 0)}");
                                    }
                                    results[label].Add(FrameStats.Percentiles(times));
                                }
                            foreach (var (label, _) in configs) Debug.Log(FrameProfile.Summary(label, name, results[label]));
                        }
                    }
                    finally { cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); }
                }
            }
            finally
            {
                VisualToggles.SetAll(true);
                QualitySettings.SetQualityLevel(saved, true);
                Object.DestroyImmediate(probe);
            }
            Debug.Log("PROFILE visual done");
        }
    }
}
