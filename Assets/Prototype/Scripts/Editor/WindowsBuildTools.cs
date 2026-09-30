using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneMoreThing.Editor
{
    public static class WindowsBuildTools
    {
        public static string OutputDirectory => Path.GetFullPath(Application.dataPath + "/../../../Builds/Windows");

        [MenuItem("One More Thing/Windows/Prepare house build")]
        public static void Prepare()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(HouseFeelTools.ScenePath);
            var hud = UnityEngine.Object.FindFirstObjectByType<RoutineHUD>();
            hud.ResolveReferences(); hud.ConfigureCanvas();
            if (!hud.HasRequiredReferences) throw new InvalidOperationException("House HUD has missing references.");
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(hud.GetComponent<UnityEngine.UI.CanvasScaler>());
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(HouseFeelTools.ScenePath, true) };
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.allowFullscreenSwitch = true;
            AssetDatabase.SaveAssets();
            Debug.Log("WINDOWS_PREPARED: house scene, responsive HUD, resizable window and borderless fullscreen.");
        }

        [MenuItem("One More Thing/Windows/Build house for Windows")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            Directory.CreateDirectory(OutputDirectory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { HouseFeelTools.ScenePath },
                locationPathName = Path.Combine(OutputDirectory, "One More Thing.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            Debug.Log("WINDOWS_BUILD_PASSED: " + OutputDirectory);
        }
    }
}
