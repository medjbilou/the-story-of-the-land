#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StoryOfTheLand.Game.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>Headless player builds. Map.unity is the boot scene; ContentSync copies content/ into StreamingAssets
    /// first (IPreprocessBuildWithReport), and CollectionImport.BuildBundles puts The 1948 Collection's streamed bundles in
    /// StreamingAssets/collection. Exits non-zero on failure.
    ///   Web: Unity -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -buildTarget WebGL -executeMethod StoryOfTheLand.Game.EditorTools.Build.Web
    ///        [-compression disabled|gzip|brotli] [-decompressionFallback] [-out Builds/Web-disabled] [-textureSubtarget dxt|etc2|astc]
    ///        (-decompressionFallback: Unity's Decompression Fallback, for hosts that cannot set response headers,
    ///        like GitHub Pages; the website's demo build uses it, docs/RELEASING.md)
    ///        (-textureSubtarget: the compressed format of every automatic texture; the default build is DXT, for desktop
    ///        browsers; astc or etc2 makes a build for browsers without S3TC, as on phones)
    ///   Mac: Unity -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -buildTarget OSXUniversal -executeMethod StoryOfTheLand.Game.EditorTools.Build.Mac
    ///        [-out Builds/Mac/TheStoryOfTheLand.app] [-ffp-contract-off]
    ///   Linux (Mono): ... -buildTarget Linux64 -executeMethod StoryOfTheLand.Game.EditorTools.Build.LinuxMono [-out Builds/Linux/TheStoryOfTheLand.x86_64]
    ///   Windows (Mono): ... -buildTarget Win64 -executeMethod StoryOfTheLand.Game.EditorTools.Build.WindowsMono [-out Builds/Windows/TheStoryOfTheLand.exe]
    ///   Every player takes -shareable (the sensitivity gate). tools/release_build.py runs these under the machine lock.</summary>
    public static class Build
    {
        static string? Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static bool Flag(string name) => Environment.GetCommandLineArgs().Contains(name);

        public static WebGLCompressionFormat Compression(string? name) => (name ?? "disabled") switch
        {
            "disabled" => WebGLCompressionFormat.Disabled,
            "gzip" => WebGLCompressionFormat.Gzip,
            "brotli" => WebGLCompressionFormat.Brotli,
            _ => throw new ArgumentException($"-compression must be disabled, gzip or brotli (got '{name}')"),
        };

        /// <summary>The WebGL texture subtarget named by -textureSubtarget (Unity's own default when absent).</summary>
        public static WebGLTextureSubtarget? TextureSubtarget(string? name) => name switch
        {
            null => null,
            "dxt" => WebGLTextureSubtarget.DXT,
            "etc2" => WebGLTextureSubtarget.ETC2,
            "astc" => WebGLTextureSubtarget.ASTC,
            _ => throw new ArgumentException($"-textureSubtarget must be dxt, etc2 or astc (got '{name}')"),
        };

        /// <summary>Why a shareable build may not be made, or null: every family card with a sensitive tag needs the owner's
        /// sensitivity review first (status sensitivity-reviewed; docs/RELEASING.md), in every chapter.
        /// The game carries no event photographs or clips, so none is gated.</summary>
        public static string? ShareableRefusal(IEnumerable<StoryOfTheLand.Sim.Content> chapters)
        {
            var open = chapters.SelectMany(StoryOfTheLand.Sim.ReleaseGate.SensitiveUnreviewed).OrderBy(x => x, StringComparer.Ordinal).ToList();
            return open.Count == 0 ? null
                : "a shareable build needs the owner's sensitivity review of: " + string.Join(", ", open) + " (set status: sensitivity-reviewed; see docs/RELEASING.md)";
        }

        /// <summary>The gate for a build made with these command-line args: a -shareable build (Web or Mac) is refused
        /// until the reviews are done; a dev build is never gated (web_check and the golden checks need them).</summary>
        public static string? GateRefusal(string[] args, IEnumerable<StoryOfTheLand.Sim.Content> chapters) =>
            Array.IndexOf(args, "-shareable") >= 0 ? ShareableRefusal(chapters) : null;

        /// <summary>Logs "BUILD REFUSED ..." and exits 1 when the gate refuses this build; true if it did. The chapters checked
        /// are the repository's (content/palestine/), as ContentSync ships them.</summary>
        static bool RefuseUnlessShareable()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-shareable") < 0) return false;
            string? refusal;
            try { refusal = GateRefusal(args, FamilyGame.Chapters.Select(c => StoryOfTheLand.Sim.ContentLoader.LoadChapter(Paths.Content, c.Id)).ToList()); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException || e is StoryOfTheLand.Sim.ContentException)
            {
                refusal = "cannot read the chapters (" + Paths.Content + "): " + e.Message;
            }
            if (refusal == null) return false;
            Debug.LogError("BUILD REFUSED " + refusal);
            EditorApplication.Exit(1);
            return true;
        }

        /// <summary>The name of the manifest every build writes beside its output (review I1): which event photographs it
        /// carries (none) and whether it was a -shareable build. tools/package_release.py refuses a build folder
        /// without it, or with an unreviewed sensitive photograph, or not built -shareable.</summary>
        public const string PhotoManifestName = "event-photos.json";

        [Serializable] sealed class PhotoManifest { public int version = 1; public bool shareable; public List<PhotoRecord> photos = new List<PhotoRecord>(); }
        [Serializable] sealed class PhotoRecord { public string event_id = ""; public bool sensitive; public bool sensitivity_reviewed; }

        /// <summary>The manifest's JSON: no photographs.</summary>
        public static string PhotoManifestJson(bool shareable) => JsonUtility.ToJson(new PhotoManifest { shareable = shareable }, true) + "\n";

        /// <summary>Writes the manifest into <paramref name="folder"/> (the WebGL build folder; for the Mac app, the folder
        /// holding it, so the signed bundle is not touched).</summary>
        static void WritePhotoManifest(string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, PhotoManifestName), PhotoManifestJson(Flag("-shareable")));
        }

        public static void Web()
        {
            if (RefuseUnlessShareable()) return;
            var name = Arg("-compression") ?? "disabled";
            ProjectSetup.Apply();
            PlayerSettings.WebGL.compressionFormat = Compression(name);
            PlayerSettings.WebGL.decompressionFallback = Flag("-decompressionFallback");   // off by default: itch.io and web_check send .gz/.br with Content-Encoding (#879); Pages needs the fallback
            var savedSubtarget = EditorUserBuildSettings.webGLBuildSubtarget;
            var sub = TextureSubtarget(Arg("-textureSubtarget"));
            if (sub.HasValue) EditorUserBuildSettings.webGLBuildSubtarget = sub.Value;
            bool ok;
            var web = Arg("-out") ?? "Builds/Web-" + name;
            try { ok = Run(BuildTarget.WebGL, web, sub.HasValue ? (int)sub.Value : 0); if (ok) WritePhotoManifest(web); }
            finally { EditorUserBuildSettings.webGLBuildSubtarget = savedSubtarget; ProjectSetup.Apply(); }   // back to the committed settings
            ExitIfFailed(ok);
        }

        public static void Mac()
        {
            if (RefuseUnlessShareable()) return;
            ProjectSetup.Apply();
            // Apple clang fuses a*b+c into fmadd by default (tech notes 4.2); turn it off if the Mac golden check fails.
            PlayerSettings.SetAdditionalIl2CppArgs(Flag("-ffp-contract-off") ? "--compiler-flags=-ffp-contract=off" : "");
            bool ok;
            var app = Arg("-out") ?? "Builds/Mac/TheStoryOfTheLand.app";
            try { ok = Run(BuildTarget.StandaloneOSX, app); if (ok) WritePhotoManifest(Path.GetDirectoryName(Path.GetFullPath(app))!); }
            finally { PlayerSettings.SetAdditionalIl2CppArgs(""); AssetDatabase.SaveAssets(); }
            ExitIfFailed(ok);
        }

        /// <summary>The macOS player on Mono, for machines without the Mac IL2CPP module.</summary>
        public static void MacMono() => StandaloneMono(BuildTarget.StandaloneOSX, "Builds/Mac/TheStoryOfTheLand.app");

        /// <summary>The Linux x64 player on Mono, built from the Mac (decision O7; tools/release_build.py).</summary>
        public static void LinuxMono() => StandaloneMono(BuildTarget.StandaloneLinux64, "Builds/Linux/TheStoryOfTheLand.x86_64");

        /// <summary>The Windows x64 player on Mono: Unity builds a Windows IL2CPP player only on Windows (decision O8).</summary>
        public static void WindowsMono() => StandaloneMono(BuildTarget.StandaloneWindows64, "Builds/Windows/TheStoryOfTheLand.exe");

        /// <summary>Builds <paramref name="target"/> with the Mono backend, then puts the committed IL2CPP setting back
        /// (ProjectSetup.Apply): the Standalone backend is one setting for Mac, Linux and Windows.</summary>
        static void StandaloneMono(BuildTarget target, string defaultOut)
        {
            if (RefuseUnlessShareable()) return;
            ProjectSetup.Apply();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            bool ok;
            var player = Arg("-out") ?? defaultOut;
            try { ok = Run(target, player); if (ok) WritePhotoManifest(Path.GetDirectoryName(Path.GetFullPath(player))!); }
            finally { ProjectSetup.Apply(); AssetDatabase.SaveAssets(); }
            ExitIfFailed(ok);
        }

        /// <summary>Exits with code 1 after the caller's settings have been restored.</summary>
        static void ExitIfFailed(bool ok) { if (!ok) EditorApplication.Exit(1); }

        /// <summary>The real size of the build output (summary.totalSize also counts build intermediates).</summary>
        static double OutputMegabytes(string path)
        {
            if (File.Exists(path)) return new FileInfo(path).Length / 1048576.0;
            return Directory.Exists(path) ? new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1048576.0 : 0;
        }

        static bool Run(BuildTarget target, string location, int subtarget = 0)
        {
            // The 1948 Collection's streamed bundles (issue #664) go in StreamingAssets/collection first; their manifest keeps
            // the classes only they use (Animator, SkinnedMeshRenderer) in the player.
            var bundles = CollectionImport.BuildBundles(target);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.BootScene }, locationPathName = location, target = target, options = BuildOptions.None,
                subtarget = subtarget,   // WebGL: the texture subtarget (0, Generic: the project's own setting)
                assetBundleManifestPath = bundles,
            });
            var s = report.summary;
            Debug.Log($"BUILD {s.result} {target} -> {location}: {OutputMegabytes(location):0.0} MB on disk, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0}s");
            return s.result == BuildResult.Succeeded;
        }
    }
}
