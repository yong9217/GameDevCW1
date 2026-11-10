#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Run through EnemyCheckBootstrap in a separate, disposable Unity project, never in the shared level.
public sealed class EnemyRuntimeChecks : MonoBehaviour
{
    int assertions;
    int editorSearchStartupErrors;
    string unexpectedError;
    GameObject actors;
    PlayerEconomy wallet;

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
                File.WriteAllText("result.txt", "FAIL after " + assertions + " assertions: " + error);
                Debug.LogError(error);
                EditorApplication.Exit(1);
                yield break;
            }
            if (!next) break;
            yield return current;
        }
        File.WriteAllText("result.txt", "PASS: " + assertions + " runtime assertions; dual circles, lock, navigation, melee, four animations, death and wallet rewards. No gameplay errors. Separate UnityEditor.Search startup exceptions: " + editorSearchStartupErrors);
        Debug.Log(File.ReadAllText("result.txt"));
        EditorApplication.Exit(0);
    }

    void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Exception && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
        {
            editorSearchStartupErrors++;
            return; // Known editor search-index startup failure, recorded separately from gameplay.
        }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            unexpectedError = message + "\n" + stack;
    }

    void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
    }

    void ResetActors()
    {
        if (actors) { actors.SetActive(false); Destroy(actors); }
        actors = new GameObject("Scenario actors");
    }

    PlayerEconomy CreateWallet(float cash, float cap)
    {
        var root = new GameObject("Wallet");
        root.SetActive(false);
        var value = root.AddComponent<PlayerEconomy>();
        value.startCash = cash;
        value.maxCash = cap;
        value.enabled = false; // Original Start needs teammate timer/debugger. Deposit/Awake are unmodified.
        root.SetActive(true);
        return value;
    }

    float Balance(PlayerEconomy economy) => (float)typeof(PlayerEconomy).GetField("totalCash",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(economy);

    EnemyCombatTarget Target(string name, Vector3 position, bool isBase = false, float hp = 1000)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        root.transform.SetParent(actors.transform);
        root.transform.position = position;
        var target = root.AddComponent<EnemyCombatTarget>();
        target.kind = isBase ? EnemyCombatTarget.TargetKind.Base : EnemyCombatTarget.TargetKind.Tower;
        target.maxHealth = hp;
        target.destroyTowerOnDeath = false;
        if (isBase)
        {
            target.baseHealth = root.AddComponent<Base>();
            target.baseHealth.startHP = hp;
            target.baseHealth.enabled = false;
        }
        root.SetActive(true);
        return target;
    }

    EnemyBrain Enemy(Vector3 position, string prefab = null)
    {
        GameObject root;
        if (prefab == null) root = new GameObject("Enemy");
        else
        {
            root = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BaseDefenseArt/Prefabs/" + prefab + ".prefab"), actors.transform);
            root.SetActive(false);
        }
        root.SetActive(false);
        root.transform.SetParent(actors.transform);
        root.transform.position = position;
        var health = root.GetComponent<EnemyHealth>() ?? root.AddComponent<EnemyHealth>();
        health.wallet = wallet;
        health.corpseLifetime = 5;
        var brain = root.GetComponent<EnemyBrain>() ?? root.AddComponent<EnemyBrain>();
        brain.moveSpeed = 3;
        brain.turnSpeed = 3600;
        brain.attackInterval = .4f;
        brain.hitFraction = .5f;
        brain.baseDetectionRadius = 2;
        brain.towerDetectionRadius = 6;
        root.SetActive(true);
        Require(root.GetComponent<NavMeshAgent>().isOnNavMesh, "Enemy must spawn on baked navigation");
        return brain;
    }

    IEnumerator Run()
    {
        wallet = CreateWallet(10, 1000);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -.25f, 0);
        ground.transform.localScale = new Vector3(40, .5f, 40);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Static wall";
        wall.transform.position = new Vector3(0, 1, 8);
        wall.transform.localScale = new Vector3(1, 2, 8);
        var navigation = new GameObject("Navigation").AddComponent<EnemyNavigationSurface>();
        Require(NavMesh.SamplePosition(Vector3.zero, out _, 1, NavMesh.AllAreas), "Runtime bake must produce walkable ground");

        // Actual presets and all four existing animation controllers, reward and repeated/re-entrant lethal hits.
        string[] names = { "Enemy_Normal", "Enemy_Fast", "Enemy_Heavy", "Enemy_Boss" };
        int[] rewards = { 1, 2, 3, 5 };
        for (int i = 0; i < names.Length; i++)
        {
            ResetActors();
            var enemy = Enemy(Vector3.zero, names[i]);
            var health = enemy.GetComponent<EnemyHealth>();
            var animator = enemy.GetComponentInChildren<Animator>();
            Require(health.killReward == rewards[i], names[i] + " reward preset");
            foreach (var state in new[] { "Idle", "Walk", "Attack", "Death" })
                Require(animator && animator.HasState(0, Animator.StringToHash(state)), names[i] + " existing state " + state);
            yield return new WaitForSeconds(.05f);
            yield return null; // Coroutine resumes before Animator evaluation; observe the following evaluated frame.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), names[i] + " plays existing Idle; brain=" + enemy.State + "; target=" + enemy.CurrentTarget + "; active targets=" + EnemyCombatTarget.Active.Count + "; animator initialized=" + animator.isInitialized + "; state hash=" + animator.GetCurrentAnimatorStateInfo(0).shortNameHash + "; cached animator=" + typeof(EnemyBrain).GetField("animator", flags).GetValue(enemy) + "; animation cache=" + typeof(EnemyBrain).GetField("animationState", flags).GetValue(enemy) + "; brain active=" + enemy.isActiveAndEnabled + "; alive=" + health.IsAlive);
            var animationTarget = Target("Base", new Vector3(0, 0, 15), true);
            yield return new WaitForSeconds(.15f);
            Require(enemy.State == EnemyBrain.BehaviourState.Moving, "Default goal must be base");
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), names[i] + " plays existing Walk");
            animationTarget.transform.position = enemy.transform.position + Vector3.forward * .4f;
            yield return new WaitForSeconds(.1f);
            yield return null;
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"), names[i] + " plays existing Attack");
            float before = Balance(wallet);
            health.Died += () => health.Damage(9999);
            health.Damage(9999);
            health.Damage(9999);
            Require(!health.IsAlive && !enemy.GetComponent<NavMeshAgent>().enabled, "Death immediately stops navigation");
            Require(Balance(wallet) == before + rewards[i], names[i] + " reward exactly once including reentrant hit");
            yield return null;
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), names[i] + " plays existing Death");
            Require(enemy.State == EnemyBrain.BehaviourState.Dead, "Dead brain must not attack");
        }

        ResetActors();
        var baseTarget = Target("Base", new Vector3(0, 0, 15), true);
        var farther = Target("Farther tower", new Vector3(5, 0, 0));
        var tower = Target("Nearest tower", new Vector3(3, 0, 0));
        var ai = Enemy(Vector3.zero);
        yield return null;
        Require(ai.CurrentTarget == tower, "Outside base circle, choose nearest tower inside large circle");
        baseTarget.transform.position = ai.transform.position + new Vector3(0, 0, 1.5f);
        yield return null;
        Require(ai.CurrentTarget == baseTarget, "Base entering small circle overrides tower pursuit");

        ResetActors();
        baseTarget = Target("Base", new Vector3(0, 0, 15), true);
        tower = Target("Locked tower", new Vector3(0, 0, .4f), false, 30);
        farther = Target("Next tower", new Vector3(4, 0, 0));
        ai = Enemy(Vector3.zero, "Enemy_Normal");
        ai.attackDamage = 10;
        yield return new WaitForSeconds(.08f);
        Require(ai.State == EnemyBrain.BehaviourState.Attacking && ai.CurrentTarget == tower, "Melee starts tower attack");
        Require(ai.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Attack"), "Existing Attack animation is played");
        Require(tower.CurrentHealth == 30, "No damage before swing hit time");
        baseTarget.transform.position = new Vector3(0, 0, 1.5f);
        yield return new WaitForSeconds(.2f);
        Require(ai.CurrentTarget == tower, "Started attack stays locked despite base entering small circle");
        Require(tower.CurrentHealth == 20, "One melee swing applies one hit");
        yield return new WaitForSeconds(.95f);
        Require(!tower.IsAlive && ai.CurrentTarget == baseTarget, "Destroyed tower unlocks and prioritises nearby base");
        float baseHitDeadline = Time.time + 3;
        while (baseTarget.baseHealth.GetHP() == 1000 && Time.time < baseHitDeadline) yield return null;
        Require(baseTarget.baseHealth.GetHP() < 1000, "Base adapter damages the actual existing Base component");

        ResetActors();
        baseTarget = Target("Base", new Vector3(0, 0, 15), true);
        tower = Target("Removed tower", new Vector3(3, 0, 0));
        farther = Target("Next tower", new Vector3(-3, 0, 0));
        ai = Enemy(Vector3.zero);
        yield return null;
        tower.gameObject.SetActive(false);
        yield return null;
        Require(ai.CurrentTarget == farther, "Disabled target is invalid and selects next live tower");
        farther.gameObject.SetActive(false);
        yield return null;
        Require(ai.CurrentTarget == baseTarget, "No towers left falls back to base");
        baseTarget.gameObject.SetActive(false);
        yield return null;
        Require(ai.State == EnemyBrain.BehaviourState.Idle, "No targets safely idles");

        ResetActors();
        tower = Target("Moving tower", new Vector3(4, 0, 0));
        ai = Enemy(Vector3.zero);
        yield return new WaitForSeconds(.3f);
        tower.transform.position = new Vector3(2, 0, 3);
        yield return new WaitForSeconds(.3f);
        Require(Vector3.Distance(ai.GetComponent<NavMeshAgent>().destination, tower.transform.position) < .1f, "Navigation tracks moved target");
        float hpBefore = tower.CurrentHealth;
        Require(hpBefore == 1000, "No damage while out of melee range");
        ai.GetComponent<EnemyHealth>().Damage(1, 1);
        yield return null;
        Require(Mathf.Abs(ai.GetComponent<NavMeshAgent>().speed - 1.5f) < .01f, "Slow affects navigation speed");
        float beforeDespawn = Balance(wallet);
        Destroy(ai.gameObject);
        yield return null;
        Require(Balance(wallet) == beforeDespawn, "Despawn without lethal damage does not reward");

        ResetActors();
        tower = Target("Moves during swing", new Vector3(0, 0, .4f));
        ai = Enemy(Vector3.zero);
        yield return new WaitForSeconds(.08f);
        tower.transform.position = new Vector3(0, 0, 4);
        yield return new WaitForSeconds(.25f);
        Require(tower.CurrentHealth == 1000, "Target leaving melee range cancels pending damage");

        ResetActors();
        // Force SamplePosition to choose the enemy side of a very thin wall.
        tower = Target("Near far side of wall", new Vector3(.6f, 0, 8));
        ai = Enemy(new Vector3(-1.2f, 0, 8));
        ai.attackRange = 3;
        ai.moveSpeed = .1f;
        yield return new WaitForSeconds(.6f);
        Require(tower.CurrentHealth == 1000, "Sampled navigation point cannot permit attacks through a nearby wall");

        ResetActors();
        tower = Target("Beyond wall", new Vector3(2, 0, 8));
        ai = Enemy(new Vector3(-2, 0, 8));
        ai.towerDetectionRadius = 20;
        ai.attackRange = 5; // In geometric range, but wall must prevent damage.
        ai.moveSpeed = 4;
        yield return new WaitForSeconds(.3f);
        Require(tower.CurrentHealth == 1000, "Wall blocks melee even inside attack radius");
        Require(ai.GetComponent<NavMeshAgent>().path.corners.Length > 2, "Path routes around wall");
        yield return new WaitForSeconds(4);
        Require(tower.CurrentHealth < 1000, "Enemy navigates around wall then attacks");

        ResetActors();
        var capped = CreateWallet(999, 1000);
        ai = Enemy(Vector3.zero);
        ai.GetComponent<EnemyHealth>().wallet = capped;
        ai.GetComponent<EnemyHealth>().killReward = 5;
        ai.GetComponent<EnemyHealth>().Damage(-100);
        Require(ai.GetComponent<EnemyHealth>().CurrentHealth == 100, "Negative damage does not heal or kill");
        ai.GetComponent<EnemyHealth>().Damage(100);
        Require(Balance(capped) == 1000, "Reward preserves existing wallet cap");
        Require(unexpectedError == null, "Unexpected Unity error: " + unexpectedError);
    }
}
#endif
