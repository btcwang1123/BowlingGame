using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Windows build of the bowling scene, to Builds/Windows/Bowling.exe (Bowling > Build Windows).
public static class BowlingBuild
{
    [MenuItem("Bowling/Build Windows")]
    public static void Windows()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Bowling/Scenes/Bowling.unity" },
            locationPathName = "Builds/Windows/Bowling.exe",
            target = BuildTarget.StandaloneWindows64,
        });
        Debug.Log($"[BowlingBuild] {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalSize / 1048576} MB");
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
