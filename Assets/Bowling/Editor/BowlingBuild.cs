using System;
using UnityEditor;
using UnityEditor.Build;
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

    // Outputs an Xcode project to Builds/iOS/ (Bowling > Build iOS). Also locks down the App
    // Store-facing Player Settings every time, so they can't silently drift back to the Unity
    // template's: real bundle id, iOS 15+, iPhone only, portrait only.
    [MenuItem("Bowling/Build iOS")]
    public static void iOS()
    {
        ApplyIOSPlayerSettings();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Bowling/Scenes/Bowling.unity" },
            locationPathName = "Builds/iOS",
            target = BuildTarget.iOS,
        });
        Debug.Log($"[BowlingBuild] {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalSize / 1048576} MB");
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public static void ApplyIOSPlayerSettings()
    {
        PlayerSettings.companyName = "btcwang";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.btcwang.neonbowling");
        PlayerSettings.iOS.appleDeveloperTeamID = "8R3YFLDYKM";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        ApplyIOSIcons();

        string buildNumber = ReadArg("-buildNumber");
        if (!string.IsNullOrEmpty(buildNumber)) PlayerSettings.iOS.buildNumber = buildNumber;

        AssetDatabase.SaveAssets();
    }

    // Every iOS icon slot (app, spotlight, settings, notification, 1024 marketing) gets the same
    // 1024 source; Unity downsizes it at build time. Alpha is stripped because App Store
    // rejects a marketing icon with transparency.
    const string IconPath = "Assets/Bowling/Icons/AppIcon.png";

    static void ApplyIOSIcons()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        if (importer.alphaSource != TextureImporterAlphaSource.None || importer.mipmapEnabled
            || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
            foreach (var slot in icons) slot.SetTexture(icon);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
        }
    }

    static string ReadArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
