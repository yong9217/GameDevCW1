using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TowerCheckBootstrap
{
    public static void Begin()
    {
        try
        {
            EnemyBehaviourSetup.ConfigurePrefabs();
            const string arrowPath = "Assets/BaseDefenseArt/Prefabs/Tower_Arrow.prefab";
            var arrow = AssetDatabase.LoadAssetAtPath<GameObject>(arrowPath).GetComponentInChildren<TowerPreviewWeapon>();
            float approvedRange = arrow.range;
            arrow.range = approvedRange + 1;
            EditorUtility.SetDirty(arrow);
            AssetDatabase.SaveAssets();
            try
            {
                EnemyBehaviourSetup.ConfigurePrefabs();
                arrow = AssetDatabase.LoadAssetAtPath<GameObject>(arrowPath).GetComponentInChildren<TowerPreviewWeapon>();
                if (arrow.range != approvedRange + 1) throw new Exception("Repeat setup overwrote a teammate-tuned tower value.");
                File.WriteAllText("tower-setup-result.txt", "PASS: repeat setup preserves teammate-tuned tower values.");
            }
            finally
            {
                arrow.range = approvedRange;
                EditorUtility.SetDirty(arrow);
                AssetDatabase.SaveAssets();
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Tower checks").AddComponent<TowerRuntimeChecks>();
            Directory.CreateDirectory("Assets/Checks");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Checks/Towers.unity");
            EditorApplication.isPlaying = true;
        }
        catch (Exception error)
        {
            File.WriteAllText("tower-result.txt", "FAIL: " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
