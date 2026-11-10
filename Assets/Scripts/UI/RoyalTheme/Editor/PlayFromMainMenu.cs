using UnityEditor;
using UnityEditor.SceneManagement;

// Pressing Play in the Editor always starts from the first scene in Build Settings (the main menu),
// exactly like the built game. Toggle with the menu "Kingdom Sentinels/Play From Main Menu".
[InitializeOnLoad]
static class PlayFromMainMenu
{
    const string PrefKey = "KingdomSentinels.PlayFromMainMenu";
    const string MenuPath = "Kingdom Sentinels/Play From Main Menu";

    static PlayFromMainMenu()
    {
        Apply();
        EditorApplication.delayCall += Apply;
        // Re-check right before every Play, so it also works if the Editor was in the background.
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Apply();
        };
    }

    static void Apply()
    {
        bool on = EditorPrefs.GetBool(PrefKey, true);
        SceneAsset first = null;
        if (EditorBuildSettings.scenes.Length > 0)
            first = AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorBuildSettings.scenes[0].path);
        EditorSceneManager.playModeStartScene = on ? first : null;
        EditorApplication.delayCall += () => Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, true));
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        EditorPrefs.SetBool(PrefKey, !EditorPrefs.GetBool(PrefKey, true));
        Apply();
    }
}
