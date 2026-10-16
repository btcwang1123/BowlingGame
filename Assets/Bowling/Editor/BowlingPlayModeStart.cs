using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Makes Play "just work":
//  - Pressing Play always starts the bowling scene, whichever scene is open in the Editor
//    (turn off with Bowling > Always Play Bowling Scene).
//  - If the bowling scene was built by an older version of BowlingSceneBuilder, it is rebuilt
//    automatically, after the scripts reload and again right before Play.
[InitializeOnLoad]
public static class BowlingPlayModeStart
{
    const string MenuPath = "Bowling/Always Play Bowling Scene";
    const string PrefKey = "Bowling.AlwaysPlayBowlingScene";

    static BowlingPlayModeStart()
    {
        EditorApplication.delayCall += OnScriptsLoaded;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static void Apply() =>
        EditorSceneManager.playModeStartScene = Enabled
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(BowlingSceneBuilder.ScenePath)
            : null;

    static void OnScriptsLoaded()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Rebuild quietly now, unless that would mean throwing away unsaved scene edits;
        // in that case it waits until Play, where the user is asked to save first.
        if (!BowlingSceneBuilder.SceneIsCurrent() && !AnySceneDirty()) Rebuild();
        Apply();
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode || BowlingSceneBuilder.SceneIsCurrent()) return;

        // Too late to swap scenes safely mid-transition: cancel, rebuild, then press Play again.
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () =>
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Rebuild();
            Apply();
            EditorApplication.isPlaying = true;
        };
    }

    static void Rebuild()
    {
        Debug.Log($"[Bowling] The bowling scene is out of date — rebuilding it (version {BowlingSceneBuilder.SceneVersion}).");
        BowlingSceneBuilder.Build();
    }

    static bool AnySceneDirty()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty) return true;
        return false;
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }
}
