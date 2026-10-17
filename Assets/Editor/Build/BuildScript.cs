using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DroneTrainer.Build
{
    /// <summary>
    /// Automated Build Pipeline Script for compiling Windows Standalone (64-bit) desktop binaries.
    /// Can be executed via Unity Editor Menu (C-UAS Simulator -> Build Windows Standalone)
    /// or from automated command-line CI/CD runners:
    /// Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod DroneTrainer.Build.BuildScript.BuildWindowsStandalone -logFile build.log
    /// </summary>
    public static class BuildScript
    {
        private const string BuildOutputFolderName = "Builds/Windows";
        private const string ExecutableName = "DroneTrainer_CUAS.exe";

        [MenuItem("C-UAS Simulator/Build Windows Standalone (64-bit)", false, 100)]
        public static void BuildWindowsStandaloneFromMenu()
        {
            BuildWindowsStandalone(isDevelopmentBuild: false);
        }

        [MenuItem("C-UAS Simulator/Build Windows Standalone (Development / Debug)", false, 101)]
        public static void BuildWindowsStandaloneDevelopmentFromMenu()
        {
            BuildWindowsStandalone(isDevelopmentBuild: true);
        }

        /// <summary>
        /// Executes the 64-bit Windows Standalone build pipeline.
        /// </summary>
        /// <param name="isDevelopmentBuild">True to enable script debugging and profiling symbols.</param>
        public static void BuildWindowsStandalone(bool isDevelopmentBuild = false)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputDirectory = Path.Combine(projectRoot, BuildOutputFolderName);
            string destinationExePath = Path.Combine(outputDirectory, ExecutableName);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string[] scenes = CollectBuildScenes();
            if (scenes.Length == 0)
            {
                Debug.LogWarning("[BuildScript] No scenes were configured in EditorBuildSettings. Scanning for scene files in project.");
                scenes = DiscoverAllSceneFiles();
            }

            Debug.Log($"<color=#00e5ff><b>=== STARTING WINDOWS STANDALONE BUILD ===</b></color>\n" +
                      $"Target: StandaloneWindows64 | Output: {destinationExePath}\n" +
                      $"Development: {isDevelopmentBuild} | Scene Count: {scenes.Length}");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = destinationExePath,
                target = BuildTarget.StandaloneWindows64,
                options = isDevelopmentBuild 
                    ? BuildOptions.Development | BuildOptions.AllowDebugging 
                    : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=#55ff55><b>=== BUILD SUCCESSFUL ===</b></color>\n" +
                          $"Binary: {destinationExePath}\n" +
                          $"Total Size: {summary.totalSize / (1024f * 1024f):F2} MB | Duration: {summary.totalTime.TotalSeconds:F1}s\n" +
                          $"Warnings: {summary.totalWarnings} | Errors: {summary.totalErrors}");

                // Reveal output in Windows Explorer when run interactively
                if (!Application.isBatchMode)
                {
                    EditorUtility.RevealInFinder(destinationExePath);
                }
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError($"<color=#ff3333><b>=== BUILD FAILED ===</b></color>\n" +
                               $"Errors: {summary.totalErrors} | Warnings: {summary.totalWarnings}\n" +
                               $"Review Unity Console or build.log for detailed stack traces.");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static string[] CollectBuildScenes()
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    scenes.Add(scene.path);
                }
            }
            return scenes.ToArray();
        }

        private static string[] DiscoverAllSceneFiles()
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var scenes = new List<string>();

            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("Package") && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    scenes.Add(path);
                }
            }

            return scenes.ToArray();
        }
    }
}
