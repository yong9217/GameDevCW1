using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TowerPlacementCheckBootstrap
{
    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Placement checks").AddComponent<TowerPlacementRuntimeChecks>();
        Directory.CreateDirectory("Assets/Checks");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Checks/Placement.unity");
        EditorApplication.isPlaying = true;
    }

    public static void RestoreDemo()
    {
        const string scene = "Assets/Demo/Scenes/CombatDemo.unity";
        if (File.Exists(scene)) EditorSceneManager.OpenScene(scene);
        AssetDatabase.DeleteAsset("Assets/Checks/Placement.unity");
    }
}
