using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class EnemyHealth : MonoBehaviour
{
    public static readonly HashSet<EnemyHealth> Active = new HashSet<EnemyHealth>();
    static int nextTargetOrder;
    public int TargetOrder { get; private set; }
    [Min(1)] public float maxHealth = 100;
    [Min(0)] public int killReward = 1;
    public PlayerEconomy wallet;
    [Min(0)] public float corpseLifetime = 3;
    [Range(.05f, 1)] public float slowMultiplier = .5f;

    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0 && !dead;
    public Vector3 AimPoint => transform.position + Vector3.up * .65f;
    public float CurrentSpeedMultiplier => Time.time < slowedUntil ? slowMultiplier : 1;
    public event Action Died;
    // Raised for every enemy that dies (gold popups, tutorial hints).
    public static event Action<EnemyHealth> AnyDied;

    bool dead;
    float slowedUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() { Active.Clear(); nextTargetOrder = 0; AnyDied = null; }

    void OnEnable() { if (IsAlive) Active.Add(this); }
    void OnDisable() => Active.Remove(this);

    void Awake()
    {
        TargetOrder = nextTargetOrder++;
        CurrentHealth = Mathf.Max(1, maxHealth);
    }

    public void TakeDamage(float amount) => Damage(amount);

    // The optional slow argument matches the existing tower effect damage entry point.
    public void Damage(float amount, float slowDuration = 0)
    {
        if (!IsAlive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (slowDuration > 0 && !float.IsInfinity(slowDuration))
            slowedUntil = Mathf.Max(slowedUntil, Time.time + slowDuration);
        if (CurrentHealth > 0) return;

        // Commit death before calling other components: repeated/re-entrant hits cannot pay twice.
        dead = true;
        Active.Remove(this);
        var agent = GetComponent<NavMeshAgent>();
        if (agent && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        if (agent) agent.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;

        var animator = GetComponentInChildren<Animator>();
        if (animator && animator.runtimeAnimatorController && animator.HasState(0, Animator.StringToHash("Death")))
        {
            animator.speed = 1;
            animator.Play("Death", 0, 0);
        }

        if (!wallet) wallet = FindFirstObjectByType<PlayerEconomy>();
        if (wallet) wallet.Deposit(Mathf.Max(0, killReward),gameObject.name);
        else Debug.LogWarning("Enemy died without a PlayerEconomy wallet; assign EnemyHealth.wallet before combat.", this);

        // Schedule cleanup before notifying subscribers, so it does not depend on their behaviour.
        Destroy(gameObject, Mathf.Max(corpseLifetime, DeathClipLength(animator)));
        GameAudio.PlayCombat("enemy_death");
        if (killReward > 0) GameAudio.PlayCombat("coin");
        Died?.Invoke();
        AnyDied?.Invoke(this);
    }

    static float DeathClipLength(Animator animator)
    {
        if (!animator || !animator.runtimeAnimatorController) return 0;
        float length = 0;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip && (clip.name == "Death" || clip.name.EndsWith("_Death", StringComparison.OrdinalIgnoreCase)))
                length = Mathf.Max(length, clip.length);
        return length;
    }
}
