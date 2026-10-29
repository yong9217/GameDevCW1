using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyBehaviourSetup
{
    [MenuItem("Tools/Central Base Defense/Configure enemy and target prefabs")]
    public static void ConfigurePrefabs()
    {
        ConfigureEnemy("Enemy_Normal", 100, 2, 10, 1, 1);
        ConfigureEnemy("Enemy_Fast", 60, 3.5f, 6, .7f, 2);
        ConfigureEnemy("Enemy_Heavy", 240, 1.2f, 20, 1.4f, 3);
        ConfigureEnemy("Enemy_Boss", 800, 1, 40, 1.8f, 5);
        ConfigureTarget("Tower_Arrow", EnemyCombatTarget.TargetKind.Tower);
        ConfigureTarget("Tower_Cannon", EnemyCombatTarget.TargetKind.Tower);
        ConfigureTarget("Tower_Magic", EnemyCombatTarget.TargetKind.Tower);
        ConfigureTarget("Base", EnemyCombatTarget.TargetKind.Base);
        AssetDatabase.SaveAssets();
        Debug.Log("Enemy prefabs configured. Rewards: Normal 1, Fast 2, Heavy 3, Boss 5. Existing components/stats are preserved on repeat runs.");
    }

    static string FindPrefab(string name)
    {
        string match = null;
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != name) continue;
            if (match != null) {
                throw new InvalidOperationException("Multiple " + name + " prefabs found; remove duplicate art imports first.");
            };
            match = path;
        }
        if (match == null) throw new InvalidOperationException("Missing existing art prefab: " + name);
        return match;
    }

    static void ConfigureEnemy(string name, float hp, float speed, float damage, float interval, int reward)
    {
        var path = FindPrefab(name);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var health = root.GetComponent<EnemyHealth>();
            if (!health)
            {
                health = root.AddComponent<EnemyHealth>();
                health.maxHealth = hp;
                health.killReward = reward;
            }
            var brain = root.GetComponent<EnemyBrain>();
            if (!brain)
            {
                brain = root.AddComponent<EnemyBrain>();
                brain.moveSpeed = speed;
                brain.attackDamage = damage;
                brain.attackInterval = interval;
            }
            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = name == "Enemy_Boss" ? .55f : .3f;
            agent.height = name == "Enemy_Boss" ? 2 : 1.5f;
            agent.baseOffset = 0;
            agent.speed = brain.moveSpeed;
            agent.acceleration = 12;
            agent.angularSpeed = 360;
            if (!root.GetComponent<Collider>())
            {
                var collider = root.AddComponent<CapsuleCollider>();
                collider.radius = agent.radius;
                collider.height = agent.height;
                collider.center = Vector3.up * agent.height * .5f;
                collider.isTrigger = true;
            }
            var animator = root.GetComponentInChildren<Animator>();
            if (animator) animator.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ConfigureTarget(string name, EnemyCombatTarget.TargetKind kind)
    {
        var path = FindPrefab(name);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ConfigureTargetObject(root, kind, false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ConfigureTargetObject(GameObject root, EnemyCombatTarget.TargetKind kind, bool undo)
    {
        var target = root.GetComponent<EnemyCombatTarget>();
        if (!target) target = undo ? Undo.AddComponent<EnemyCombatTarget>(root) : root.AddComponent<EnemyCombatTarget>();
        if (undo) Undo.RecordObject(target, "Configure enemy target");
        target.kind = kind;
        if (kind == EnemyCombatTarget.TargetKind.Base) target.baseHealth = root.GetComponent<Base>();
        if (target.attackBounds) return;

        // Dedicated trigger defines melee surface distance without becoming a movement obstacle.
        var shape = undo ? Undo.AddComponent<BoxCollider>(root) : root.AddComponent<BoxCollider>();
        var renderers = root.GetComponentsInChildren<Renderer>();
        var localBounds = new Bounds(Vector3.up * .5f, Vector3.one);
        if (renderers.Length > 0)
        {
            var world = renderers[0].bounds;
            foreach (var renderer in renderers) world.Encapsulate(renderer.bounds);
            localBounds = new Bounds(root.transform.InverseTransformPoint(world.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                localBounds.Encapsulate(root.transform.InverseTransformPoint(world.center +
                    Vector3.Scale(world.extents, new Vector3(x, y, z))));
        }
        shape.center = localBounds.center;
        shape.size = localBounds.size;
        shape.isTrigger = true;
        target.attackBounds = shape;
    }

    [MenuItem("Tools/Central Base Defense/Connect enemies in open scene")]
    public static void ConnectScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var wallet = UnityEngine.Object.FindFirstObjectByType<PlayerEconomy>();
        if (!wallet) Debug.LogWarning("No PlayerEconomy found. Assign EnemyHealth.wallet when the teammate creates the wallet.");
        foreach (var health in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (health.gameObject.scene != scene) continue;
            Undo.RecordObject(health, "Connect enemy wallet");
            health.wallet = wallet;
            PrefabUtility.RecordPrefabInstancePropertyModifications(health);
        }
        foreach (var baseHealth in UnityEngine.Object.FindObjectsByType<Base>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (baseHealth.gameObject.scene == scene)
                ConfigureTargetObject(baseHealth.gameObject, EnemyCombatTarget.TargetKind.Base, true);

        if (!UnityEngine.Object.FindFirstObjectByType<EnemyNavigationSurface>())
        {
            var root = new GameObject("Enemy Navigation");
            Undo.RegisterCreatedObjectUndo(root, "Create enemy navigation");
            var surface = Undo.AddComponent<EnemyNavigationSurface>(root);
            // Fit the level's static collision geometry; enemy/target sources are excluded at bake time.
            var bounds = new Bounds(Vector3.zero, new Vector3(60, 10, 60));
            foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (collider.gameObject.scene == scene && !collider.GetComponentInParent<EnemyHealth>() &&
                    !collider.GetComponentInParent<EnemyCombatTarget>() && !collider.isTrigger)
                    bounds.Encapsulate(collider.bounds);
            surface.center = bounds.center;
            surface.size = bounds.size + new Vector3(4, 10, 4);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Scene connected. Review navigation bounds, save the scene, and test in Play Mode. Existing economy rules were not changed.");
    }
}
