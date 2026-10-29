using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch verification ONLY in a disposable project containing the existing art and wallet scripts.
public static class EnemyCheckBootstrap
{
    public static void Begin()
    {
        try
        {
            EnemyBehaviourSetup.ConfigurePrefabs();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Enemy runtime checks").AddComponent<EnemyRuntimeChecks>();
            Directory.CreateDirectory("Assets/Checks");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Checks/Runtime.unity");
            EditorApplication.isPlaying = true;
        }
        catch (Exception error)
        {
            File.WriteAllText("result.txt", "FAIL: " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
