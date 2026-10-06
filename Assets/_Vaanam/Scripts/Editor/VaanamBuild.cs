using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Vaanam.Editor
{
    /// <summary>
    /// One-click Windows build. Output goes to Builds/Windows/Vaanam.exe.
    /// Packaging/build-msix.ps1 turns that folder into an MSIX for the Store.
    /// </summary>
    public static class VaanamBuild
    {
        public const string ScenePath = "Assets/_Vaanam/Scenes/Vaanam.unity";
        public const string OutputDir = "Builds/Windows";

        [MenuItem("Vaanam/Build Windows Player")]
        public static void BuildWindowsMenu() => Debug.Log(BuildWindows());

        public static string BuildWindows()
        {
            if (!File.Exists(ScenePath)) VaanamSceneBuilder.BuildMainScene();

            PlayerSettings.companyName = "Vaanam";
            PlayerSettings.productName = "Vaanam";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.vaanam.sky");
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            Directory.CreateDirectory(OutputDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(OutputDir, "Vaanam.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string msg = $"Build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalSeconds:0}s -> {s.outputPath}";
            if (s.result != BuildResult.Succeeded) throw new System.Exception(msg);
            return msg;
        }
    }
}
