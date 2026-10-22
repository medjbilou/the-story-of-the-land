using System.IO;
using StoryOfTheLand.Game.Boot;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>Copies the content players read (ContentManifest.Collect: the map files and the family game's content/
    /// palestine/) into Assets/StreamingAssets/content with a manifest.json and a bundle.json (every text in one file, which
    /// the web boot fetches: #1054) before every player build (git-ignored output), so players read the same content the
    /// editor reads from the repo. The family goldens are content/palestine/goldens.yaml.
    /// Run alone: Unity -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -executeMethod StoryOfTheLand.Game.EditorTools.ContentSync.Sync</summary>
    public sealed class ContentSync : IPreprocessBuildWithReport
    {
        public static string Target => Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "content");

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Sync();

        public static void Sync()
        {
            var files = ContentManifest.Copy(Paths.Content, Target);
            try { ContentManifest.Verify(Paths.Content, Target); }
            catch (InvalidDataException e) { throw new BuildFailedException("ContentSync: " + e.Message); }
            UnityEngine.Debug.Log($"ContentSync: {files.Count} files to {Target}");
        }
    }
}
