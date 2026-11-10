using System;
using System.Collections.Generic;
using UnityEngine;

// Attach to the root of a tower or the base. EnemyBrain never owns tower combat or the wallet.
[DisallowMultipleComponent]
public sealed class EnemyCombatTarget : MonoBehaviour
{
    public enum TargetKind { Tower, Base }
    public TargetKind kind;
    [Min(1)] public float maxHealth = 100;
    public Base baseHealth;
    public Collider attackBounds;
    public bool destroyTowerOnDeath = true;

    public static readonly HashSet<EnemyCombatTarget> Active = new HashSet<EnemyCombatTarget>();
    public float CurrentHealth => kind == TargetKind.Base ? (baseHealth ? baseHealth.GetHP() : 0) : health;
    public bool IsAlive => isActiveAndEnabled && CurrentHealth > 0;
    public event Action Destroyed;
    float health;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() => Active.Clear();

    void Awake()
    {
        health = Mathf.Max(1, maxHealth);
        if (kind == TargetKind.Base && !baseHealth) baseHealth = GetComponent<Base>();
        if (!attackBounds) attackBounds = GetComponent<Collider>();
        if (kind == TargetKind.Tower) MakeSolid();
    }

    // Towers block enemies: a carving NavMeshObstacle with the same footprint as the tower's BoxCollider, so enemies
    // walk around a tower instead of through it. It disappears with the tower. The castle is not changed.
    void MakeSolid()
    {
        var box = attackBounds as BoxCollider;
        if (!box || GetComponent<UnityEngine.AI.NavMeshObstacle>()) return;
        var obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
        obstacle.center = box.center;
        obstacle.size = box.size;
        obstacle.carving = true;
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public float DistanceFrom(Vector3 point)
    {
        Vector3 closest = attackBounds && attackBounds.enabled ? attackBounds.ClosestPoint(point) : transform.position;
        closest.y = point.y;
        return Vector3.Distance(point, closest);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        if (kind == TargetKind.Base && baseHealth) baseHealth.Direct(-amount);
        else health = Mathf.Max(0, health - amount);
        if (CurrentHealth > 0) return;
        Active.Remove(this);
        if (kind == TargetKind.Tower && destroyTowerOnDeath) Destroy(gameObject);
        // Base defeat/game-over remains the teammate's responsibility.
        Destroyed?.Invoke();
    }
}
