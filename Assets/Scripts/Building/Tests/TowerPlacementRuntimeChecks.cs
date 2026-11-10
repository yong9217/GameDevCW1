#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Standalone validation only: run through TowerPlacementCheckBootstrap in a disposable Unity project.
public sealed class TowerPlacementRuntimeChecks : MonoBehaviour
{
    int assertions;
    MouseStateHandler handler;
    PlayerEconomy wallet;
    string gameplayError;
    GameObject arrow, cannon, magic;

    IEnumerator Start()
    {
        Application.logMessageReceived += CaptureError;
        var checks = Run();
        while (true)
        {
            object current = null;
            bool next = false;
            try { next = checks.MoveNext(); if (next) current = checks.Current; }
            catch (Exception error)
            {
                File.WriteAllText("placement-result.txt", "FAIL after " + assertions + " assertions: " + error);
                Debug.LogError(error);
                EditorApplication.Exit(1);
                yield break;
            }
            if (!next) break;
            yield return current;
        }
        yield return null;
        if (gameplayError != null)
        {
            File.WriteAllText("placement-result.txt", "FAIL: " + gameplayError);
            EditorApplication.Exit(1);
            yield break;
        }
        File.WriteAllText("placement-result.txt", "PASS: " + assertions + " placement runtime assertions using all three production tower prefabs and all four enemy prefabs.");
        EditorApplication.Exit(0);
    }

    static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BaseDefenseArt/Prefabs/" + name + ".prefab");

    PlayerEconomy CreateWallet(float cash)
    {
        var root = new GameObject("Wallet");
        root.SetActive(false);
        var value = root.AddComponent<PlayerEconomy>();
        value.startCash = cash;
        value.maxCash = 100000;
        value.towerToCost = new Dictionary<GameObject, float> { { arrow, 10 }, { cannon, 20 }, { magic, 30 } };
        value.enabled = false; // Awake builds the catalogue; Start (timer/debugger) is not needed here.
        root.SetActive(true);
        return value;
    }

    // Keeps the enemy still, so every scenario is about placement only.
    GameObject SpawnEnemy(string name, Vector3 position, bool still = true)
    {
        var go = Instantiate(Load(name), position, Quaternion.identity);
        go.GetComponent<EnemyHealth>().wallet = wallet;
        if (still)
        {
            go.GetComponent<EnemyBrain>().enabled = false;
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent) agent.enabled = false;
        }
        return go;
    }

    void Clear()
    {
        foreach (var target in FindObjectsByType<EnemyCombatTarget>(FindObjectsSortMode.None)) DestroyImmediate(target.gameObject);
        foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) DestroyImmediate(enemy.gameObject);
    }

    int Towers => EnemyCombatTarget.Active.Count;
    bool Click(Vector3 point) => handler.TryPlaceTower(point);

    // Footprint of the given tower prefab if it were placed at "site".
    Bounds FootprintAt(GameObject prefab, Vector3 site)
    {
        var box = (BoxCollider)prefab.GetComponent<EnemyCombatTarget>().attackBounds;
        return TowerPlacement.Footprint(box, Matrix4x4.TRS(site, Quaternion.identity, prefab.transform.localScale));
    }

    IEnumerator Run()
    {
        arrow = Load("Tower_Arrow");
        cannon = Load("Tower_Cannon");
        magic = Load("Tower_Magic");
        Require(arrow && cannon && magic, "Missing real tower prefabs");

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -.25f, 0);
        ground.transform.localScale = new Vector3(160, .5f, 160);
        new GameObject("Navigation").AddComponent<EnemyNavigationSurface>();

        wallet = CreateWallet(100000);
        handler = new GameObject("Placement input").AddComponent<MouseStateHandler>();
        handler.bank = wallet;

        // ---- Towers and base (rules carried over from the earlier tested version) ----
        foreach (var prefab in new[] { arrow, cannon, magic })
        {
            Clear();
            handler.towerPrefab = prefab;
            Require(Click(Vector3.zero) && Towers == 1, prefab.name + ": empty ground must accept a tower.");
            Require(!Click(Vector3.zero) && Towers == 1, prefab.name + ": duplicate placement must be rejected.");
            Require(!Click(new Vector3(.5f, 0, .5f)) && Towers == 1, prefab.name + ": partial overlap must be rejected.");
            Require(Click(new Vector3(40, 0, 0)) && Towers == 2, prefab.name + ": clear ground must remain usable.");
        }

        Clear();
        var existing = Instantiate(arrow, new Vector3(3, 8, 4), Quaternion.Euler(0, 45, 0));
        existing.transform.localScale = new Vector3(2, 1, 2);
        handler.towerPrefab = cannon;
        Require(!Click(new Vector3(3, 0, 4)) && Towers == 1, "Preplaced, scaled, rotated tower blocks another tower even at a different height.");
        Require(!Click(new Vector3(7, 0, 4)) && Towers == 1, "Scaled footprint blocks a neighbouring overlap.");
        existing.SetActive(false);
        Require(Click(new Vector3(3, 0, 4)) && Towers == 1, "Disabled tower releases its footprint.");
        DestroyImmediate(existing);

        Clear();
        existing = Instantiate(arrow, Vector3.zero, Quaternion.identity);
        DestroyImmediate(existing);
        Require(Click(Vector3.zero) && Towers == 1, "Destroyed tower releases its footprint.");

        Clear();
        existing = Instantiate(arrow, Vector3.zero, Quaternion.identity);
        var target = existing.GetComponent<EnemyCombatTarget>();
        target.destroyTowerOnDeath = false;
        target.TakeDamage(100000);
        Require(!Click(Vector3.zero) && Towers == 0, "Visible dead tower continues to block until removed.");
        DestroyImmediate(existing);

        Clear();
        existing = Instantiate(arrow, Vector3.zero, Quaternion.identity);
        existing.GetComponent<EnemyCombatTarget>().kind = EnemyCombatTarget.TargetKind.Base;
        Require(!Click(Vector3.zero) && Towers == 1, "Configured base footprint blocks tower placement.");
        handler.towerPrefab = existing;
        Require(!Click(Vector3.zero) && Towers == 1, "Base cannot be selected as a tower prefab.");
        existing.GetComponent<EnemyCombatTarget>().kind = EnemyCombatTarget.TargetKind.Tower;
        Require(!Click(Vector3.zero) && Towers == 1, "Scene tower used as a template must still block its own site.");
        var template = Instantiate(arrow, new Vector3(100, 0, 0), Quaternion.identity);
        template.transform.localScale = new Vector3(-2, 1, 2);
        Require(!TowerPlacement.TryPlace(template, new Vector3(4, 0, 0), Quaternion.Euler(0, 45, 0), .05f, out _, out _),
            "Candidate footprint includes rotation and negative/nonuniform scale.");
        Require(TowerPlacement.TryPlace(template, new Vector3(60, 0, 0), Quaternion.Euler(0, 45, 0), .05f, out var placed, out _),
            "Rotated scaled candidate can be placed on clear ground.");
        Require(placed.transform.localScale == template.transform.localScale, "Placed tower retains prefab scale.");

        Clear();
        var invalid = new GameObject("Unconfigured tower");
        handler.towerPrefab = invalid;
        Require(!Click(Vector3.zero) && Towers == 0, "Unconfigured prefab is rejected without creating an object.");
        DestroyImmediate(invalid);
        handler.towerPrefab = cannon;
        Require(!Click(new Vector3(float.NaN, 0, 0)) && Towers == 0, "Non-finite coordinates are rejected.");
        handler.towerPrefab = null;
        Require(!Click(Vector3.zero) && Towers == 0, "Missing selection must not create anything or throw.");

        // ---- Enemy body radii (the circle used for each enemy) ----
        Clear();
        string[] enemyNames = { "Enemy_Normal", "Enemy_Fast", "Enemy_Heavy", "Enemy_Boss" };
        float[] radii = { .3f, .3f, .6f, 1.65f };
        for (int i = 0; i < enemyNames.Length; i++)
        {
            var enemy = SpawnEnemy(enemyNames[i], new Vector3(-50, 0, 0));
            float radius = TowerPlacement.EnemyBodyRadius(enemy.GetComponent<EnemyHealth>());
            Require(Mathf.Abs(radius - radii[i]) < .01f, enemyNames[i] + " body radius is " + radii[i] + " (was " + radius + ")");
            Require(enemy.GetComponent<EnemyHealth>().corpseLifetime == 2, enemyNames[i] + " corpse lifetime is 2 seconds");
        }

        // ---- Living enemies block placement; the circle is tested against the footprint ----
        Vector3 site = new Vector3(40, 0, 0);
        handler.towerPrefab = arrow;
        Bounds fp = FootprintAt(arrow, site);

        Clear();
        SpawnEnemy("Enemy_Normal", site);
        float before = wallet.GetTotal();
        Require(!Click(site) && Towers == 0, "Enemy standing in the middle blocks placement.");
        Require(wallet.GetTotal() == before, "Rejected placement must not charge the player.");

        Clear();
        SpawnEnemy("Enemy_Normal", new Vector3(fp.max.x + .2f, 0, site.z));
        Require(!Click(site) && Towers == 0, "Normal enemy 0.2 outside the edge blocks (radius 0.3 + clearance).");

        Clear();
        SpawnEnemy("Enemy_Normal", new Vector3(fp.max.x + .4f, 0, site.z));
        Require(Click(site) && Towers == 1, "Normal enemy 0.4 outside the edge does not block.");

        Clear();
        SpawnEnemy("Enemy_Normal", new Vector3(fp.max.x + .2f, 0, fp.max.z + .2f));
        Require(!Click(site) && Towers == 0, "Enemy diagonally off a corner (distance 0.28) blocks.");

        Clear();
        SpawnEnemy("Enemy_Normal", new Vector3(fp.max.x + .3f, 0, fp.max.z + .3f));
        Require(Click(site) && Towers == 1, "Enemy diagonally off a corner (distance 0.42) does not block: the test is a circle, not a square.");

        Clear();
        SpawnEnemy("Enemy_Fast", new Vector3(fp.min.x - .2f, 0, site.z));
        Require(!Click(site), "Fast enemy blocks on the opposite side too.");

        Clear();
        SpawnEnemy("Enemy_Heavy", new Vector3(fp.max.x + .6f, 0, site.z));
        Require(!Click(site), "Heavy enemy (radius 0.6) 0.6 outside the edge blocks.");
        Clear();
        SpawnEnemy("Enemy_Heavy", new Vector3(fp.max.x + .8f, 0, site.z));
        Require(Click(site), "Heavy enemy 0.8 outside the edge does not block.");

        Clear();
        SpawnEnemy("Enemy_Boss", new Vector3(fp.max.x + 1.6f, 0, site.z));
        Require(!Click(site), "Boss (radius 1.65) 1.6 outside the edge blocks.");
        Clear();
        SpawnEnemy("Enemy_Boss", new Vector3(fp.max.x + 1.9f, 0, site.z));
        Require(Click(site), "Boss 1.9 outside the edge does not block.");

        Clear();
        SpawnEnemy("Enemy_Normal", new Vector3(site.x + 70, 0, 0));
        SpawnEnemy("Enemy_Normal", new Vector3(fp.max.x + .2f, 0, site.z));
        Require(!Click(site), "One near enemy blocks even when another is far away.");

        Clear();
        var disabled = SpawnEnemy("Enemy_Normal", site);
        disabled.SetActive(false);
        Require(Click(site) && Towers == 1, "A deactivated enemy does not block.");
        Clear();
        var removed = SpawnEnemy("Enemy_Normal", site);
        DestroyImmediate(removed);
        Require(Click(site) && Towers == 1, "A removed enemy does not block.");

        // ---- Corpses never block, never obstruct movement, and disappear after 2 seconds ----
        Clear();
        var corpse = SpawnEnemy("Enemy_Boss", site, still: false);
        var corpseHealth = corpse.GetComponent<EnemyHealth>();
        Require(!Click(site), "A living Boss on the site blocks.");
        corpseHealth.Damage(100000);
        Require(!corpseHealth.IsAlive, "Boss is dead.");
        Require(Click(site) && Towers == 1, "A dead enemy does not block placement (it can be built over).");
        foreach (var collider in corpse.GetComponentsInChildren<Collider>()) Require(!collider.enabled, "Corpse collider is off, so nothing collides with it.");
        Require(!corpse.GetComponent<NavMeshAgent>().enabled, "Corpse agent is off, so other enemies do not path around it.");
        Require(corpse.GetComponentInChildren<NavMeshObstacle>() == null, "Corpse is not a navigation obstacle.");
        float died = Time.time;
        yield return new WaitForSeconds(1.5f);
        Require(corpse != null, "Corpse is still visible after 1.5 seconds (death animation plays).");
        yield return new WaitForSeconds(.8f);
        Require(corpse == null, "Corpse is gone about 2 seconds after death (" + (Time.time - died).ToString("0.0") + "s).");

        // ---- Payment happens only after a valid spot, and exactly once ----
        Clear();
        foreach (var pair in new[] { (arrow, 10f), (cannon, 20f), (magic, 30f) })
        {
            handler.towerPrefab = pair.Item1;
            before = wallet.GetTotal();
            Require(Click(new Vector3(-40, 0, -40)), pair.Item1.name + " placed on free ground.");
            Require(Mathf.Approximately(wallet.GetTotal(), before - pair.Item2), pair.Item1.name + " costs exactly " + pair.Item2 + ".");
            Clear();
        }

        handler.towerPrefab = arrow;
        Click(Vector3.zero);
        before = wallet.GetTotal();
        Require(!Click(Vector3.zero), "Overlapping tower rejected.");
        Require(wallet.GetTotal() == before && Towers == 1, "Blocked by a tower: no charge, no extra tower.");

        Clear();
        SpawnEnemy("Enemy_Normal", Vector3.zero);
        before = wallet.GetTotal();
        Require(!Click(Vector3.zero), "Enemy-blocked placement rejected.");
        Require(wallet.GetTotal() == before && Towers == 0, "Blocked by an enemy: no charge, no tower.");

        Clear();
        var poor = CreateWallet(5);
        handler.bank = poor;
        Require(!Click(new Vector3(-40, 0, 40)) && Towers == 0, "Insufficient funds on a free spot: no tower.");
        Require(poor.GetTotal() == 5, "Insufficient funds: balance unchanged.");
        handler.bank = wallet;

        Require(gameplayError == null, "Unexpected Unity error: " + gameplayError);
        yield break;
    }

    void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
    }

    void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) gameplayError = message + "\n" + stack;
    }
}
#endif
