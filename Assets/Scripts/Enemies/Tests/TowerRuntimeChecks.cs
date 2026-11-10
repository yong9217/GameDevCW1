#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class TowerRuntimeChecks : MonoBehaviour
{
    int assertions;
    int editorSearchErrors;
    string gameplayError;
    GameObject actors;
    PlayerEconomy wallet;
    IEnumerator Start()
    {
        Application.logMessageReceived += CaptureError;
        var routine = Run();
        float deadline = Time.realtimeSinceStartup + 120;
        while (true)
        {
            object value = null;
            bool next = false;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Tower runtime checks exceeded 120 seconds.");
                next = routine.MoveNext(); if (next) value = routine.Current;
            }
            catch (Exception error)
            {
                File.WriteAllText("tower-result.txt", "FAIL after " + assertions + " assertions: " + error);
                Debug.LogError(error);
                EditorApplication.Exit(1);
                yield break;
            }
            if (!next) break;
            yield return value;
        }
        File.WriteAllText("tower-result.txt", "PASS: " + assertions + " tower runtime assertions. No gameplay errors. Separate UnityEditor.Search startup exceptions: " + editorSearchErrors);
        Debug.Log(File.ReadAllText("tower-result.txt"));
        EditorApplication.Exit(0);
    }

    void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
        { editorSearchErrors++; return; }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) gameplayError = message + "\n" + stack;
    }

    void ResetActors()
    {
        if (actors) { actors.SetActive(false); Destroy(actors); }
        foreach (var effect in FindObjectsByType<TowerPreviewEffects>(FindObjectsSortMode.None)) Destroy(effect.gameObject);
        actors = new GameObject("Tower test actors");
    }

    EnemyHealth Enemy(Vector3 position, float hp = 1000)
    {
        var root = new GameObject("Stationary enemy");
        root.SetActive(false);
        root.transform.SetParent(actors.transform);
        root.transform.position = position;
        var health = root.AddComponent<EnemyHealth>();
        health.maxHealth = hp;
        health.wallet = wallet;
        health.corpseLifetime = 3;
        root.SetActive(true);
        return health;
    }

    TowerPreviewWeapon Tower(TowerPreviewWeapon.WeaponKind kind, bool fire = true)
    {
        var root = new GameObject(kind + " tower");
        root.SetActive(false);
        root.transform.SetParent(actors.transform);
        root.AddComponent<EnemyCombatTarget>();
        var weapon = root.AddComponent<TowerPreviewWeapon>();
        weapon.kind = kind;
        weapon.muzzle = root.transform;
        weapon.range = 9;
        weapon.damage = 15;
        weapon.interval = 10;
        weapon.attacking = fire;
        root.SetActive(true);
        return weapon;
    }

    float Balance() => (float)typeof(PlayerEconomy).GetField("totalCash",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(wallet);

    void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
    }

    IEnumerator Run()
    {
        var walletRoot = new GameObject("Existing wallet");
        walletRoot.SetActive(false);
        wallet = walletRoot.AddComponent<PlayerEconomy>();
        wallet.startCash = 0;
        wallet.maxCash = 1000;
        wallet.enabled = false;
        walletRoot.SetActive(true);

        ResetActors();
        var enemy = Enemy(new Vector3(0, 0, 2), 100);
        var tower = Tower(TowerPreviewWeapon.WeaponKind.Crystal);
        yield return new WaitForSeconds(.15f);
        Require(enemy.CurrentHealth == 85, "Existing tower must damage EnemyHealth, not the demo-only TowerPreviewTarget.");

        // Verify actual prefab defaults and original mount references.
        string[] names = { "Tower_Arrow", "Tower_Cannon", "Tower_Magic" };
        float[] ranges = { 9, 13.5f, 9 }, damages = { 30, 50, 15 }, intervals = { .6f, 1.6f, 1 };
        for (int i = 0; i < names.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BaseDefenseArt/Prefabs/" + names[i] + ".prefab");
            var weapon = prefab.GetComponentInChildren<TowerPreviewWeapon>(true);
            Require(weapon && weapon.range == ranges[i] && weapon.damage == damages[i] && weapon.interval == intervals[i], names[i] + " approved actual stats");
            Require(weapon.muzzle && weapon.fixedBase, names[i] + " retains original firing/base mounts");
            Require(prefab.GetComponent<EnemyCombatTarget>(), names[i] + " accepts enemy damage");
        }

        ResetActors();
        var near = Enemy(new Vector3(0, 0, 2), 100);
        var far = Enemy(new Vector3(0, 0, 4), 1000);
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crossbow, false);
        yield return null;
        Require(tower.currentTarget == near, "Nearest wins over higher health outside tie tolerance");
        far.transform.position = new Vector3(0, 0, 1);
        yield return null;
        Require(tower.currentTarget == near, "Retain valid target even when a nearer enemy appears");
        near.gameObject.SetActive(false);
        yield return null;
        Require(tower.currentTarget == far, "Disabled target releases lock");
        far.transform.position = new Vector3(0, 0, 10);
        yield return null;
        Require(!tower.currentTarget, "Target leaving range releases lock");

        ResetActors();
        var outsideTie = Enemy(new Vector3(0, 0, 2.09f), 900);
        var tiedHigh = Enemy(new Vector3(0, 0, 2.03f), 500);
        near = Enemy(new Vector3(0, 0, 2), 100);
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crossbow, false);
        yield return null;
        Require(tower.currentTarget == tiedHigh, "Global nearest distance defines ties; no pairwise tolerance drift");
        tiedHigh.Damage(450);
        yield return null;
        Require(tower.currentTarget == tiedHigh, "Changing health does not break a valid lock");
        tiedHigh.enabled = false;
        yield return null;
        Require(tower.currentTarget == near, "Disabling health unregisters the enemy");
        near.Damage(100);
        yield return null;
        Require(tower.currentTarget == outsideTie, "Lethal damage releases lock and selects next enemy");

        ResetActors();
        var firstRegistered = Enemy(new Vector3(0, 0, 2.03f), 100);
        var laterRegistered = Enemy(new Vector3(0, 0, 2), 100);
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crossbow, false);
        yield return null;
        Require(tower.currentTarget == firstRegistered, "Equal HP inside tie band uses stable registration order");

        // Projectiles do not deal immediate damage, and cannon never damages neighbours.
        ResetActors();
        enemy = Enemy(new Vector3(0, 0, 2));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crossbow);
        tower.damage = 30;
        while (tower.shots == 0) yield return null;
        tower.attacking = false;
        Require(enemy.CurrentHealth == 1000, "Arrow damage waits for projectile impact");
        yield return new WaitForSeconds(.5f);
        Require(enemy.CurrentHealth == 970 && tower.hits == 1, "Arrow impact deals exactly one hit");

        ResetActors();
        enemy = Enemy(new Vector3(0, 0, 2));
        var neighbour = Enemy(new Vector3(.2f, 0, 2.1f));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Cannon);
        tower.damage = 50;
        while (tower.shots == 0) yield return null;
        tower.attacking = false;
        Require(enemy.CurrentHealth == 1000, "Cannon damage waits for impact");
        yield return new WaitForSeconds(.85f);
        Require(enemy.CurrentHealth == 950 && neighbour.CurrentHealth == 1000, "Cannon explosion is visual only, no area damage");

        ResetActors();
        enemy = Enemy(new Vector3(0, 0, 2));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crystal);
        tower.maxChainTargets = 1;
        tower.interval = .25f;
        tower.damage = 1;
        var timestamps = new List<float>();
        float stopAt = Time.time + 1;
        int observedShots = 0;
        while (Time.time < stopAt)
        {
            if (tower.shots > observedShots) { observedShots = tower.shots; timestamps.Add(Time.time); }
            yield return null;
        }
        tower.attacking = false;
        Require(timestamps.Count >= 3, "Repeated attacks follow configured interval");
        for (int i = 1; i < timestamps.Count; i++) Require(timestamps[i] - timestamps[i - 1] >= .24f, "No attacks faster than cooldown");

        ResetActors();
        near = Enemy(new Vector3(0, 0, 2));
        var second = Enemy(new Vector3(0, 0, 5));
        var third = Enemy(new Vector3(0, 0, 8));
        var fourth = Enemy(new Vector3(1, 0, 8));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crystal);
        tower.interval = 1;
        while (tower.shots == 0) yield return null;
        tower.attacking = false;
        Require(near.CurrentHealth == 985 && second.CurrentHealth == 985 && third.CurrentHealth == 985, "Sequential chain jumps beyond first target neighbourhood with equal damage");
        Require(fourth.CurrentHealth == 1000 && tower.hits == 3 && tower.chainHits == 2, "One cast hits at most three distinct enemies");
        Require(near.CurrentSpeedMultiplier == .5f && second.CurrentSpeedMultiplier == .5f && third.CurrentSpeedMultiplier == .5f, "All three victims slowed by 50 percent");
        yield return new WaitForSeconds(.25f);
        near.Damage(1, .5f);
        Require(near.CurrentSpeedMultiplier == .5f, "Repeated slow never stacks strength");
        yield return new WaitForSeconds(.35f);
        Require(second.CurrentSpeedMultiplier == 1 && third.CurrentSpeedMultiplier == 1, "One-second magic interval gives half-second slow");
        Require(near.CurrentSpeedMultiplier == .5f, "Repeated slow refreshes expiry");
        yield return new WaitForSeconds(.25f);
        Require(near.CurrentSpeedMultiplier == 1, "Refreshed slow eventually expires");

        ResetActors();
        near = Enemy(new Vector3(0, 0, 2));
        second = Enemy(new Vector3(0, 0, 5));
        var tooFar = Enemy(new Vector3(0, 0, 9.1f));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crystal);
        tower.interval = 2;
        while (tower.shots == 0) yield return null;
        tower.attacking = false;
        Require(tower.hits == 2 && tooFar.CurrentHealth == 1000, "No chain jump beyond 3.5 units");
        Require(Mathf.Approximately(tower.SlowDuration, 1), "Changing attack interval to two seconds gives one-second slow");
        yield return new WaitForSeconds(.65f);
        Require(near.CurrentSpeedMultiplier == .5f, "Two-second attack interval slow persists past half a second");
        yield return new WaitForSeconds(.45f);
        Require(near.CurrentSpeedMultiplier == 1, "Dynamic one-second slow expires");

        ResetActors();
        near = Enemy(new Vector3(0, 0, 8));
        outsideTie = Enemy(new Vector3(0, 0, 9.1f));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crystal);
        while (tower.shots == 0) yield return null;
        tower.attacking = false;
        Require(outsideTie.CurrentHealth == 1000 && tower.hits == 1, "Chain cannot extend beyond tower range even within jump distance");

        // A cast with only one target still damages/slows once and has no duplicate jumps.
        ResetActors();
        enemy = Enemy(new Vector3(0, 0, 2));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crystal);
        while (tower.shots == 0) yield return null;
        tower.attacking = false;
        Require(tower.hits == 1 && tower.chainHits == 0 && enemy.CurrentHealth == 985, "Fewer available targets remain a valid cast");

        // In-flight projectile lifecycle and death reward integration.
        ResetActors();
        enemy = Enemy(new Vector3(0, 0, 2));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crossbow);
        tower.damage = 30;
        while (tower.shots == 0) yield return null;
        Destroy(tower.gameObject);
        yield return new WaitForSeconds(.5f);
        Require(enemy.CurrentHealth == 970, "Already-fired projectile survives destroyed tower without owner reference error");

        ResetActors();
        enemy = Enemy(new Vector3(0, 0, 2), 10);
        enemy.killReward = 5;
        var otherTower = Tower(TowerPreviewWeapon.WeaponKind.Crossbow);
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crossbow);
        float balanceBefore = Balance();
        while (tower.shots == 0 || otherTower.shots == 0) yield return null;
        tower.attacking = otherTower.attacking = false;
        yield return new WaitForSeconds(.5f);
        Require(!enemy.IsAlive && Balance() == balanceBefore + 5, "Two simultaneous lethal projectiles pay once");
        Require(tower.hits + otherTower.hits == 1, "Second projectile cannot damage corpse");

        ResetActors();
        enemy = Enemy(new Vector3(0, 0, 2));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Cannon);
        balanceBefore = Balance();
        while (tower.shots == 0) yield return null;
        tower.attacking = false;
        enemy.gameObject.SetActive(false);
        yield return new WaitForSeconds(.85f);
        Require(enemy.CurrentHealth == 1000 && Balance() == balanceBefore, "Disabled enemy cancels incoming projectile without reward");

        ResetActors();
        enemy = Enemy(new Vector3(0, 0, 2));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crossbow);
        while (tower.shots == 0) yield return null;
        tower.attacking = false;
        Destroy(enemy.gameObject);
        yield return new WaitForSeconds(.5f);
        Require(tower.hits == 0, "Destroyed enemy cancels projectile safely");

        ResetActors();
        enemy = Enemy(new Vector3(0, 0, 2));
        tower = Tower(TowerPreviewWeapon.WeaponKind.Crystal);
        tower.interval = .1f;
        var towerHealth = tower.GetComponent<EnemyCombatTarget>();
        towerHealth.destroyTowerOnDeath = false;
        while (tower.shots == 0) yield return null;
        towerHealth.TakeDamage(1000);
        int shotsAtDeath = tower.shots;
        yield return new WaitForSeconds(.35f);
        Require(tower.shots == shotsAtDeath && !tower.currentTarget, "Destroyed health stops further firing even before object removal");

        for (int i = 0; i < names.Length; i++)
        {
            ResetActors();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BaseDefenseArt/Prefabs/" + names[i] + ".prefab");
            var actualTower = Instantiate(prefab, actors.transform);
            actualTower.transform.position = Vector3.zero;
            var actualWeapon = actualTower.GetComponentInChildren<TowerPreviewWeapon>();
            actualWeapon.interval = 10;
            enemy = Enemy(new Vector3(0, 0, 4), 1000);
            float fireDeadline = Time.time + 5;
            while (actualWeapon.shots == 0 && Time.time < fireDeadline) yield return null;
            actualWeapon.attacking = false;
            Require(actualWeapon.shots == 1, names[i] + " actual mount aims and fires");
            yield return new WaitForSeconds(i == 1 ? .85f : .5f);
            Require(enemy.CurrentHealth == 1000 - damages[i], names[i] + " actual prefab deals approved damage");
            Require(actualWeapon.maxBaseMovement < .001f, names[i] + " firing preserves tower pedestal position");
            if (i < 2) Require(actualWeapon.maxRecoil > .01f, names[i] + " original recoil remains functional");
            var actualHealth = actualTower.GetComponent<EnemyCombatTarget>();
            actualHealth.destroyTowerOnDeath = false;
            actualHealth.TakeDamage(1000);
            int before = actualWeapon.shots;
            actualWeapon.attacking = true;
            actualWeapon.interval = .05f;
            yield return new WaitForSeconds(.15f);
            Require(actualWeapon.shots == before && !actualWeapon.currentTarget, names[i] + " child weapon respects parent tower death");
        }
        Require(gameplayError == null, "Unexpected gameplay error: " + gameplayError);
    }
}
#endif
