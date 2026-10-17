using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WinjayXR.EditorTools
{
    /// <summary>
    /// Command-line APK build for Meta Quest:
    /// Unity.exe -batchmode -quit -projectPath <project> -executeMethod WinjayXR.EditorTools.WinjayBuildScript.BuildQuest
    /// </summary>
    public static class WinjayBuildScript
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        public static void BuildQuest()
        {
            try
            {
                EnsureScene();
                WinjayProjectSetup.SetupAll();

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                        BuildTargetGroup.Android, BuildTarget.Android))
                    {
                        Debug.LogError("[WINJAY] Failed to switch to the Android platform. Make sure the Android Build Support module is installed.");
                        EditorApplication.Exit(1);
                        return;
                    }
                }

                if (!Directory.Exists("Builds")) Directory.CreateDirectory("Builds");
                EditorUserBuildSettings.buildAppBundle = false;

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Builds/WinjayXR.apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                };

                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                Debug.Log("[WINJAY] Build finished: " + summary.result +
                    " — size " + (summary.totalSize / (1024 * 1024)) + " MB — " +
                    summary.outputPath);
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("[WINJAY] Build failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureScene()
        {
            if (File.Exists(ScenePath) && EditorBuildSettings.scenes.Length > 0) return;
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }
    }
}
