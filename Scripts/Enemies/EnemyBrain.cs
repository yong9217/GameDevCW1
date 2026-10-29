using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public sealed class EnemyBrain : MonoBehaviour
{
    public enum BehaviourState { Idle, Moving, Chasing, Attacking, Dead }

    [Header("GDD dual circles (world units, measured from the enemy to target surface)")]
    [Min(.1f)] public float towerDetectionRadius = 10;
    [Min(.1f)] public float baseDetectionRadius = 4;
    [Header("Melee")]
    [Min(.05f)] public float attackRange = .8f;
    [Min(.1f)] public float attackDamage = 10;
    [Min(.05f)] public float attackInterval = 1;
    [Range(0, 1)] public float hitFraction = .45f;
    [Header("Movement")]
    [Min(.1f)] public float moveSpeed = 2;
    [Min(.05f)] public float repathInterval = .2f;
    public float turnSpeed = 360;
    public LayerMask meleeObstacleLayers = ~0;

    public EnemyCombatTarget CurrentTarget { get; private set; }
    public BehaviourState State { get; private set; }

    EnemyHealth health;
    NavMeshAgent agent;
    Animator animator;
    EnemyCombatTarget lockedTower, pendingVictim;
    float nextRepath, nextAttack, hitAt, attackEnds;
    bool swingPending;
    string animationState;

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;
        agent.updateRotation = false;
        agent.stoppingDistance = 0;
        agent.autoBraking = true;
    }

    void OnEnable()
    {
        health.Died += OnDeath;
        CurrentTarget = lockedTower = pendingVictim = null;
        swingPending = false;
        nextRepath = nextAttack = attackEnds = 0;
        animationState = null;
    }

    void OnDisable()
    {
        health.Died -= OnDeath;
        swingPending = false;
        if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
    }

    void OnDeath()
    {
        State = BehaviourState.Dead;
        CurrentTarget = lockedTower = pendingVictim = null;
        swingPending = false;
    }

    void Update()
    {
        if (!health.IsAlive) { OnDeath(); return; }
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            State = BehaviourState.Idle;
            Play("Idle");
            return;
        }

        agent.speed = moveSpeed * health.CurrentSpeedMultiplier;
        if (lockedTower && !lockedTower.IsAlive) lockedTower = null;
        var selected = lockedTower ? lockedTower : SelectTarget();
        if (selected != CurrentTarget)
        {
            CurrentTarget = selected;
            swingPending = false;
            pendingVictim = null;
            attackEnds = 0;
            nextRepath = 0;
        }
        if (!CurrentTarget || !CurrentTarget.IsAlive)
        {
            Stop();
            State = BehaviourState.Idle;
            Play("Idle");
            return;
        }

        bool inRange = CurrentTarget.DistanceFrom(transform.position) <= attackRange;
        bool canStrike = inRange && ClearMeleePath(CurrentTarget);
        if (!canStrike)
        {
            swingPending = false;
            attackEnds = 0;
            State = CurrentTarget.kind == EnemyCombatTarget.TargetKind.Base ? BehaviourState.Moving : BehaviourState.Chasing;
            agent.isStopped = false;
            if (Time.time >= nextRepath)
            {
                // Target collider centres are walkable: towers are deliberately not navigation obstacles.
                if (NavMesh.SamplePosition(CurrentTarget.transform.position, out var destination, 2, agent.areaMask))
                    agent.SetDestination(destination.position);
                else agent.ResetPath();
                nextRepath = Time.time + Mathf.Max(.05f, repathInterval);
            }
            Face(agent.desiredVelocity);
            Play(agent.velocity.sqrMagnitude > .005f ? "Walk" : "Idle");
            return;
        }

        Stop();
        State = BehaviourState.Attacking;
        Vector3 direction = CurrentTarget.transform.position - transform.position;
        direction.y = 0;
        Face(direction);

        if (swingPending && Time.time >= hitAt)
        {
            swingPending = false;
            if (pendingVictim && pendingVictim == CurrentTarget && pendingVictim.IsAlive &&
                pendingVictim.DistanceFrom(transform.position) <= attackRange && ClearMeleePath(pendingVictim))
                pendingVictim.TakeDamage(attackDamage);
        }
        if (Time.time >= nextAttack && CurrentTarget && CurrentTarget.IsAlive &&
            (direction.sqrMagnitude < .0001f || Vector3.Angle(transform.forward, direction) <= 10))
        {
            if (CurrentTarget.kind == EnemyCombatTarget.TargetKind.Tower) lockedTower = CurrentTarget;
            float interval = Mathf.Max(.05f, attackInterval);
            nextAttack = attackEnds = Time.time + interval;
            pendingVictim = CurrentTarget;
            hitAt = Time.time + interval * Mathf.Clamp01(hitFraction);
            swingPending = true;
            Play("Attack", true, interval);
        }
        else if (!swingPending && Time.time >= attackEnds) Play("Idle");
    }

    EnemyCombatTarget SelectTarget()
    {
        EnemyCombatTarget nearestBase = null, nearestTower = null;
        float baseDistance = float.PositiveInfinity, towerDistance = float.PositiveInfinity;
        foreach (var target in EnemyCombatTarget.Active)
        {
            if (!target || !target.IsAlive) continue;
            float distance = target.DistanceFrom(transform.position);
            if (target.kind == EnemyCombatTarget.TargetKind.Base && distance < baseDistance)
            { nearestBase = target; baseDistance = distance; }
            else if (target.kind == EnemyCombatTarget.TargetKind.Tower && distance <= towerDetectionRadius && distance < towerDistance)
            { nearestTower = target; towerDistance = distance; }
        }
        if (nearestBase && baseDistance <= baseDetectionRadius) return nearestBase;
        return nearestTower ? nearestTower : nearestBase;
    }

    bool ClearMeleePath(EnemyCombatTarget target)
    {
        if (!NavMesh.SamplePosition(target.transform.position, out var end, 2, agent.areaMask) ||
            NavMesh.Raycast(agent.nextPosition, end.position, out _, agent.areaMask)) return false;
        // Sampling can land on the near side of a wall. Also check the actual target position.
        Vector3 origin = transform.position + Vector3.up * .6f;
        Vector3 line = target.transform.position + Vector3.up * .6f - origin;
        foreach (var hit in Physics.RaycastAll(origin, line.normalized, line.magnitude,
                     meleeObstacleLayers, QueryTriggerInteraction.Ignore))
            if (!hit.collider.GetComponentInParent<EnemyHealth>() &&
                !hit.collider.GetComponentInParent<EnemyCombatTarget>()) return false;
        return true;
    }

    void Stop()
    {
        agent.isStopped = true;
        if (agent.hasPath) agent.ResetPath();
    }

    void Face(Vector3 direction)
    {
        direction.y = 0;
        if (direction.sqrMagnitude > .0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
    }

    void Play(string state, bool restart = false, float duration = 1)
    {
        if (!animator || !animator.runtimeAnimatorController) return;
        if (!animator.HasState(0, Animator.StringToHash(state))) return;
        // Animator may reset to its default state when a prefab is re-enabled.
        if (state == animationState && !restart && animator.GetCurrentAnimatorStateInfo(0).IsName(state)) return;
        animator.speed = 1;
        if (state == "Attack")
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip && (clip.name == "Attack" || clip.name.EndsWith("_Attack")))
                { animator.speed = clip.length / Mathf.Max(.05f, duration); break; }
        animator.Play(state, 0, 0);
        animationState = state;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, towerDetectionRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, baseDetectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
