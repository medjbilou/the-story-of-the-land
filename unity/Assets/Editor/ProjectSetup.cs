using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>Project settings (M0 API corrections and M3 web settings): IL2CPP for Standalone and WebGL, the
    /// StoryOfTheLand WebGL template (full-window canvas, autoSyncPersistentDataPath), compression off by default (the build
    /// script sets it per target), Map.unity as the only and first scene.
    /// Run headless: Unity -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -executeMethod StoryOfTheLand.Game.EditorTools.ProjectSetup.Apply</summary>
    public static class ProjectSetup
    {
        public const string BootScene = "Assets/Map.unity";
        public const string WebTemplate = "PROJECT:TheStoryOfTheLand";
        /// <summary>The game's name (company and product: window title, menu, Info.plist CFBundleName) and its bundle id.</summary>
        public const string ProductName = "The Story of the Land", AppId = "org.thestoryoftheland.game";

        public static void Apply()
        {
            PlayerSettings.companyName = ProductName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.WebGL.template = WebTemplate;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            // Issue #437: the files under Build/ are named by their content hash, so a browser that cached an earlier build
            // cannot mix its files into this one; the build's index.html names its own files.
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            // Debug.Log lines (GAME OK, TURN, PROFILE) without a script stack trace; warnings and errors keep theirs (Task 24 review M9).
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            // F6 round 4 (the owner: "be more fast loading"): no Unity splash screen; it held every desktop start for about two
            // seconds before the game's own first frame (-bootlog: "player started" at 2.2 s). Unity 6 lets every plan turn it off.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var template = player.FindProperty("templateDefaultScene");   // no public API; the sample scene it named is gone
            if (template != null && template.stringValue != BootScene) { template.stringValue = BootScene; player.ApplyModifiedPropertiesWithoutUndo(); }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
