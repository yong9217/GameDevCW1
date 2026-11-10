using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public sealed class EnemyBrain : MonoBehaviour
{
    public enum BehaviourState { Idle, Moving, Chasing, Attacking, Dead }

    [Header("GDD dual circles (world units, measured from the enemy to target surface)")]
    [Min(.1f)] public float towerDetectionRadius = 10;
    [Min(.1f)] public float baseDetectionRadius = 2;
    [Header("Melee")]
    [Min(.05f)] public float attackRange = .8f;
    [Tooltip("Extra distance kept from the castle wall, so the swing is not hidden inside the castle model.")]
    [Min(0f)] public float baseStandOff = .5f;
    [Min(.1f)] public float attackDamage = 10;
    [Min(.05f)] public float attackInterval = 1;
    [Range(0, 1)] public float hitFraction = .45f;
    [Header("Movement")]
    [Min(.1f)] public float moveSpeed = 2;
    [Min(.05f)] public float repathInterval = .2f;
    public float turnSpeed = 360;
    public LayerMask meleeObstacleLayers = ~0;
    [Header("Hit feedback (leave empty / 0 for none; set on the boss only)")]
    [Tooltip("Spawned at the point where this enemy's blow lands on a tower or the castle.")]
    public GameObject hitEffect;
    [Tooltip("Show the effect and shake this many seconds BEFORE the damage lands (damage timing itself is unchanged).")]
    [Min(0f)] public float hitFeedbackLead = 0;
    [Min(.1f)] public float hitEffectScale = 1;
    [Min(.1f)] public float hitEffectLifetime = 1.5f;
    [Tooltip("Camera shake strength in world units when a blow lands. 0 = no shake.")]
    [Min(0f)] public float cameraShakeAmplitude = 0;
    [Min(.01f)] public float cameraShakeDuration = .2f;
    [Header("Unstuck (walking but going nowhere)")]
    [Tooltip("If the enemy walks for this long without getting anywhere, it attacks the nearest target within reach.")]
    [Min(.5f)] public float stuckSeconds = 2;
    [Tooltip("Moving less than this far (at normal speed) during that time counts as stuck.")]
    [Min(.05f)] public float stuckMoveDistance = .25f;
    [Tooltip("A target counts as within reach when it is this much farther than the normal melee range.")]
    [Min(0f)] public float stuckReachMargin = .5f;

    public EnemyCombatTarget CurrentTarget { get; private set; }
    public BehaviourState State { get; private set; }

    EnemyHealth health;
    NavMeshAgent agent;
    Animator animator;
    EnemyCombatTarget lockedTower, pendingVictim;
    float nextRepath, nextAttack, hitAt, attackEnds;
    bool swingPending;
    string animationState;
    float nextRejoin;
    float feedbackAt;
    bool feedbackPending;
    bool offMeshFallback;
    EnemyCombatTarget forcedTarget;
    Vector3 stuckAnchor;
    float stuckSince;

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
        CurrentTarget = lockedTower = pendingVictim = forcedTarget = null;
        ResetStuck();
        swingPending = false;
        nextRepath = nextAttack = attackEnds = nextRejoin = 0;
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
        CurrentTarget = lockedTower = pendingVictim = forcedTarget = null;
        swingPending = false;
    }

    void Update()
    {
        if (!health.IsAlive) { OnDeath(); return; }
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            // Off the NavMesh normal pathfinding cannot run. Only enemies GameFlow had to place without walkable
            // ground (EnableOffMeshFallback) walk straight at the castle; anything else (bestiary previews, ...) just stands.
            if (offMeshFallback) MarchStraightToBase();
            else
            {
                State = BehaviourState.Idle;
                Play("Idle");
            }
            return;
        }

        agent.speed = moveSpeed * health.CurrentSpeedMultiplier;
        if (lockedTower && !lockedTower.IsAlive) lockedTower = null;
        if (forcedTarget && !forcedTarget.IsAlive) forcedTarget = null;
        var selected = forcedTarget ? forcedTarget : lockedTower ? lockedTower : SelectTarget();
        if (selected != CurrentTarget)
        {
            if (swingPending) Diag("target changed " + Name(CurrentTarget) + " -> " + Name(selected));
            CurrentTarget = selected;
            swingPending = false;
            pendingVictim = null;
            attackEnds = 0;
            nextRepath = 0;
            ResetStuck();
        }
        if (!CurrentTarget || !CurrentTarget.IsAlive)
        {
            Stop();
            ResetStuck();
            State = BehaviourState.Idle;
            Play("Idle");
            return;
        }

        bool inRange = CurrentTarget.DistanceFrom(transform.position) <= RangeFor(CurrentTarget);
        bool canStrike = inRange && ClearMeleePath(CurrentTarget);
        if (!canStrike)
        {
            if (swingPending) Diag("lost strike: inRange=" + inRange + " dist=" + CurrentTarget.DistanceFrom(transform.position).ToString("F3") +
                " range=" + RangeFor(CurrentTarget).ToString("F3") + " path=" + diagPath + " vel=" + agent.velocity.magnitude.ToString("F3"));
            swingPending = false;
            attackEnds = 0;
            State = CurrentTarget.kind == EnemyCombatTarget.TargetKind.Base ? BehaviourState.Moving : BehaviourState.Chasing;
            agent.isStopped = false;
            TrackStuck();
            if (Time.time >= nextRepath)
            {
                // Towers are carved out of the NavMesh (solid), so aim for the edge of the tower facing this enemy.
                if (NavMesh.SamplePosition(ApproachPoint(CurrentTarget), out var destination, 2, agent.areaMask))
                    agent.SetDestination(destination.position);
                else agent.ResetPath();
                nextRepath = Time.time + Mathf.Max(.05f, repathInterval);
            }
            Face(agent.desiredVelocity);
            Play(agent.velocity.sqrMagnitude > .005f ? "Walk" : "Idle");
            return;
        }

        Strike();
    }

    // Stand still, face the current target and run the swing timer (used by both the NavMesh and the fallback path).
    void Strike()
    {
        Stop();
        ResetStuck();
        State = BehaviourState.Attacking;
        Vector3 direction = CurrentTarget.transform.position - transform.position;
        direction.y = 0;
        Face(direction);

        if (swingPending && feedbackPending && Time.time >= feedbackAt)
        {
            feedbackPending = false;
            if (pendingVictim && pendingVictim.IsAlive) HitFeedback(pendingVictim);
        }
        if (swingPending && Time.time >= hitAt)
        {
            swingPending = false;
            if (pendingVictim && pendingVictim == CurrentTarget && pendingVictim.IsAlive &&
                pendingVictim.DistanceFrom(transform.position) <= RangeFor(pendingVictim) && ClearMeleePath(pendingVictim))
                pendingVictim.TakeDamage(attackDamage);
            else Diag("hit dropped: victim=" + Name(pendingVictim) + " path=" + diagPath);
        }
        if (Time.time >= nextAttack && CurrentTarget && CurrentTarget.IsAlive &&
            (direction.sqrMagnitude < .0001f || Vector3.Angle(transform.forward, direction) <= 10))
        {
            if (CurrentTarget.kind == EnemyCombatTarget.TargetKind.Tower) lockedTower = CurrentTarget;
            float interval = Mathf.Max(.05f, attackInterval);
            nextAttack = attackEnds = Time.time + interval;
            pendingVictim = CurrentTarget;
            hitAt = Time.time + interval * Mathf.Clamp01(hitFraction);
            feedbackAt = Mathf.Max(Time.time, hitAt - hitFeedbackLead);
            feedbackPending = true;
            swingPending = true;
            Play("Attack", true, interval);
        }
        else if (!swingPending && Time.time >= attackEnds) Play("Idle");
    }

    // Effect + camera shake at the point of the victim facing this enemy. Only enemies with these fields set (the boss) use it.
    void HitFeedback(EnemyCombatTarget victim)
    {
        if (cameraShakeAmplitude > 0f) RTSCameraController.Shake(cameraShakeAmplitude, cameraShakeDuration);
        if (!hitEffect) return;
        Vector3 from = transform.position + Vector3.up;
        Vector3 point = victim.attackBounds && victim.attackBounds.enabled
            ? victim.attackBounds.ClosestPoint(from) : victim.transform.position + Vector3.up;
        var effect = Instantiate(hitEffect, point, Quaternion.identity);
        effect.transform.localScale *= hitEffectScale;
        Destroy(effect, hitEffectLifetime);
    }

    void ResetStuck()
    {
        stuckAnchor = transform.position;
        stuckSince = Time.time;
    }

    // Called every frame while the enemy is walking toward a target it cannot strike yet.
    // If it has gone nowhere for stuckSeconds (blocked by a tower, a crowd, ...), it attacks the nearest target in reach.
    void TrackStuck()
    {
        float needed = stuckMoveDistance * health.CurrentSpeedMultiplier;
        if ((transform.position - stuckAnchor).sqrMagnitude > needed * needed) { ResetStuck(); return; }
        if (Time.time - stuckSince < stuckSeconds) return;

        ResetStuck(); // measure a fresh window whatever happens below, so this cannot flip-flop
        var nearest = NearestTargetInReach();
        if (nearest && nearest != CurrentTarget)
        {
            Debug.Log("[Unstuck] " + name + " was stuck for " + stuckSeconds.ToString("F1") + "s, now attacking " + Name(nearest), this);
            forcedTarget = nearest;
        }
    }

    EnemyCombatTarget NearestTargetInReach()
    {
        EnemyCombatTarget best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (var target in EnemyCombatTarget.Active)
        {
            if (!target || !target.IsAlive) continue;
            float distance = target.DistanceFrom(transform.position);
            if (distance <= RangeFor(target) + stuckReachMargin && distance < bestDistance) { best = target; bestDistance = distance; }
        }
        return best;
    }

    // Called by GameFlow for an enemy it had to place off the NavMesh (no walkable ground at the spawn ring).
    public void EnableOffMeshFallback() => offMeshFallback = true;

    // Fallback for an enemy that is not on the NavMesh (e.g. no walkable ground at the spawn ring).
    // Never runs while normal pathfinding works. Ignores obstacles on purpose: it is the last resort.
    void MarchStraightToBase()
    {
        // If walkable ground is right here (e.g. the runtime bake finished after the spawn), rejoin normal pathfinding.
        if (agent.enabled && Time.time >= nextRejoin)
        {
            nextRejoin = Time.time + .5f;
            if (NavMesh.SamplePosition(transform.position, out var ground, 2f, agent.areaMask) && agent.Warp(ground.position))
            {
                CurrentTarget = null;
                swingPending = false;
                return;
            }
        }

        EnemyCombatTarget castle = NearestBase();
        if (!castle)
        {
            State = BehaviourState.Idle;
            Play("Idle");
            return;
        }
        if (castle != CurrentTarget)
        {
            CurrentTarget = castle;
            swingPending = false;
            pendingVictim = null;
            attackEnds = 0;
        }

        if (castle.DistanceFrom(transform.position) > RangeFor(castle))
        {
            swingPending = false;
            attackEnds = 0;
            State = BehaviourState.Moving;
            Vector3 toCastle = castle.transform.position - transform.position;
            toCastle.y = 0;
            Face(toCastle);
            float step = moveSpeed * health.CurrentSpeedMultiplier * Time.deltaTime;
            transform.position += toCastle.normalized * Mathf.Min(step, toCastle.magnitude);
            Play("Walk");
            return;
        }
        Strike();
    }

    // Where to walk to reach a target: the castle's centre as before; for a (solid) tower, the point of its footprint
    // nearest to this enemy, so the enemy stops at the side it arrives from instead of circling to the far edge.
    Vector3 ApproachPoint(EnemyCombatTarget target)
    {
        if (target.kind != EnemyCombatTarget.TargetKind.Tower || !target.attackBounds || !target.attackBounds.enabled)
            return target.transform.position;
        Vector3 point = target.attackBounds.ClosestPoint(transform.position);
        point.y = transform.position.y;
        return point;
    }

    EnemyCombatTarget NearestBase()
    {
        EnemyCombatTarget best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (var target in EnemyCombatTarget.Active)
        {
            if (!target || !target.IsAlive || target.kind != EnemyCombatTarget.TargetKind.Base) continue;
            float distance = target.DistanceFrom(transform.position);
            if (distance < bestDistance) { best = target; bestDistance = distance; }
        }
        return best;
    }

    // Melee reach for a target: the castle gets a little extra so enemies stop before the wall.
    float RangeFor(EnemyCombatTarget target) =>
        attackRange + (target && target.kind == EnemyCombatTarget.TargetKind.Base ? baseStandOff : 0f);

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
        // Off the NavMesh (fallback march) there is nothing to test against, so the strike is allowed.
        if (!agent.enabled || !agent.isOnNavMesh) { diagPath = "fallback (off NavMesh)"; return true; }
        if (!NavMesh.SamplePosition(ApproachPoint(target), out var end, 2, agent.areaMask) ||
            NavMesh.Raycast(agent.nextPosition, end.position, out _, agent.areaMask)) { diagPath = "navmesh blocked"; return false; }
        // Sampling can land on the near side of a wall. Also check the actual target position.
        Vector3 origin = transform.position + Vector3.up * .6f;
        Vector3 line = target.transform.position + Vector3.up * .6f - origin;
        foreach (var hit in Physics.RaycastAll(origin, line.normalized, line.magnitude,
                     meleeObstacleLayers, QueryTriggerInteraction.Ignore))
            if (!hit.collider.GetComponentInParent<EnemyHealth>() &&
                !hit.collider.GetComponentInParent<EnemyCombatTarget>()) { diagPath = "blocked by collider '" + hit.collider.name + "' root '" + hit.collider.transform.root.name + "'"; return false; }
        diagPath = "clear";
        return true;
    }

    // TEMP DIAGNOSTICS (SwingDiag) - remove after the half-swing bug is found.
    string diagPath = "-";
    static string Name(EnemyCombatTarget t) => t ? t.name + "(" + t.kind + ")" : "none";
    void Diag(string message) => Debug.Log("[SwingDiag] t=" + Time.time.ToString("F2") + " " + name +
        " target=" + Name(CurrentTarget) + " sinceSwing=" + (Time.time - (hitAt - Mathf.Max(.05f, attackInterval) * Mathf.Clamp01(hitFraction))).ToString("F2") + " | " + message);

    void Stop()
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;
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
