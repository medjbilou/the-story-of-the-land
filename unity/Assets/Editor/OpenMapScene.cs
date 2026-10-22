#nullable enable
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace StoryOfTheLand.Game.EditorTools
{
    /// <summary>A fresh clone opens on Unity's empty "Untitled" scene, and new contributors could not find the game
    /// (it is Assets/Map.unity, the boot scene). Once the editor is ready (not compiling or importing, with a valid
    /// active scene), this opens the map once per session, and only when nothing the user made could be lost: one
    /// untitled, unedited scene, outside play mode and batch mode. On a first launch the domain loads before Unity has
    /// created its starting scene, so the decision waits on EditorApplication.update instead of running at load.</summary>
    [InitializeOnLoad]
    public static class OpenMapScene
    {
        public const string MapScenePath = "Assets/Map.unity";
        const string SessionKey = "StoryOfTheLand.OpenMapScene.Done";

        static OpenMapScene()
        {
            if (UnityEngine.Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.update += Poll;
        }

        public static bool ShouldOpen(string activeScenePath, bool activeSceneDirty, int loadedScenes, bool isPlaying,
            bool isBatchMode, bool alreadyDoneThisSession) =>
            !alreadyDoneThisSession && !isBatchMode && !isPlaying && loadedScenes == 1
            && string.IsNullOrEmpty(activeScenePath) && !activeSceneDirty;

        /// <summary>True once the editor has finished starting: nothing compiling or importing and a valid scene.</summary>
        public static bool EditorReady(bool isCompiling, bool isUpdating, bool activeSceneValid) =>
            !isCompiling && !isUpdating && activeSceneValid;

        static void Poll()
        {
            var active = SceneManager.GetActiveScene();
            if (!EditorReady(EditorApplication.isCompiling, EditorApplication.isUpdating, active.IsValid())) return;
            EditorApplication.update -= Poll;
            var open = ShouldOpen(active.path, active.isDirty, SceneManager.sceneCount,
                EditorApplication.isPlayingOrWillChangePlaymode, UnityEngine.Application.isBatchMode,
                SessionState.GetBool(SessionKey, false));
            SessionState.SetBool(SessionKey, true);
            if (open && AssetDatabase.LoadAssetAtPath<SceneAsset>(MapScenePath) != null)
                EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
        }
    }
}
