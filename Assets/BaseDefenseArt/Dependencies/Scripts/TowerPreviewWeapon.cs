using System.Collections.Generic;
using UnityEngine;

// Retains the original script GUID and prefab mounts; targets the production EnemyHealth system.
public sealed class TowerPreviewWeapon : MonoBehaviour
{
    public enum WeaponKind { Crossbow, Cannon, Crystal }
    public WeaponKind kind;
    [HideInInspector] public int lane; // Legacy serialized value; never filters production targets.
    [HideInInspector] public bool combatConfigured;
    public Transform yaw, muzzle, fixedBase, recoilPart;
    [Min(.1f)] public float range = 9;
    [Min(0)] public float damage = 30;
    [Min(.05f)] public float interval = .6f;
    [Range(1, 3)] public int maxChainTargets = 3;
    [Min(0)] public float chainRange = 3.5f;
    [Min(0)] public float distanceTieTolerance = .05f;
    public float SlowDuration => Mathf.Max(.05f, interval) * .5f;
    public bool attacking = true;
    public int shots, hits, chainHits;
    public float maxYawChange, maxBaseMovement, maxRecoil;
    public EnemyHealth currentTarget;
    public Vector3 lastMuzzle, lastAim;

    float fireAt, recoil;
    Vector3 recoilRest, baseStart;
    Quaternion yawStart;
    EnemyCombatTarget towerHealth;

    void OnEnable() => currentTarget = null;
    void OnDisable() => currentTarget = null;

    void Start()
    {
        towerHealth = GetComponentInParent<EnemyCombatTarget>();
        if (kind == WeaponKind.Crystal)
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.name == "Crystal" && !child.GetComponent<TowerPreviewCrystalSpin>())
                { child.gameObject.AddComponent<TowerPreviewCrystalSpin>(); break; }
        if (yaw) yawStart = yaw.rotation;
        if (fixedBase) baseStart = fixedBase.position;
        if (recoilPart) recoilRest = recoilPart.localPosition;
        fireAt = Time.time;
    }

    public void ResetWeapon()
    {
        shots = hits = chainHits = 0;
        maxYawChange = maxBaseMovement = maxRecoil = 0;
        recoil = 0;
        currentTarget = null;
        fireAt = Time.time;
        if (yaw) yaw.rotation = yawStart;
        if (recoilPart) recoilPart.localPosition = recoilRest;
    }

    static float Distance(Vector3 a, Vector3 b)
    {
        a.y = b.y;
        return Vector3.Distance(a, b);
    }

    Vector3 TowerPosition => towerHealth ? towerHealth.transform.position : transform.position;

    bool Valid(EnemyHealth enemy) => enemy && enemy.isActiveAndEnabled && enemy.IsAlive &&
        Distance(TowerPosition, enemy.transform.position) <= Mathf.Max(0, range);

    EnemyHealth FindTarget(Vector3 origin, float radius, HashSet<EnemyHealth> excluded = null)
    {
        float nearest = float.PositiveInfinity;
        foreach (var enemy in EnemyHealth.Active)
        {
            if (!Valid(enemy) || (excluded != null && excluded.Contains(enemy))) continue;
            float distance = Distance(origin, enemy.transform.position);
            if (distance <= radius) nearest = Mathf.Min(nearest, distance);
        }
        EnemyHealth best = null;
        foreach (var enemy in EnemyHealth.Active)
        {
            if (!Valid(enemy) || (excluded != null && excluded.Contains(enemy))) continue;
            float distance = Distance(origin, enemy.transform.position);
            if (distance > radius || distance > nearest + Mathf.Max(0, distanceTieTolerance)) continue;
            if (!best || enemy.CurrentHealth > best.CurrentHealth ||
                (enemy.CurrentHealth == best.CurrentHealth && enemy.TargetOrder < best.TargetOrder))
                best = enemy;
        }
        return best;
    }

    void Update()
    {
        if (fixedBase) maxBaseMovement = Mathf.Max(maxBaseMovement, Vector3.Distance(baseStart, fixedBase.position));
        if (recoilPart)
        {
            recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * .9f);
            recoilPart.localPosition = recoilRest + Vector3.back * recoil;
            maxRecoil = Mathf.Max(maxRecoil, recoil);
        }
        if (towerHealth && !towerHealth.IsAlive) { currentTarget = null; return; }
        if (!Valid(currentTarget)) currentTarget = FindTarget(TowerPosition, Mathf.Max(0, range));
        if (!currentTarget) return;
        Vector3 direction = currentTarget.AimPoint - transform.position;
        direction.y = 0;
        if (yaw && direction.sqrMagnitude > .001f)
        {
            yaw.rotation = Quaternion.RotateTowards(yaw.rotation, Quaternion.LookRotation(direction), 160 * Time.deltaTime);
            maxYawChange = Mathf.Max(maxYawChange, Quaternion.Angle(yawStart, yaw.rotation));
        }
        if (!attacking || Time.time < fireAt) return;
        if (yaw && direction.sqrMagnitude > .001f && Vector3.Angle(yaw.forward, direction) > 3) return;
        fireAt = Time.time + Mathf.Max(.05f, interval);
        shots++;
        GameAudio.PlayCombat(kind == WeaponKind.Cannon ? "shot_cannon" : kind == WeaponKind.Crystal ? "shot_magic" : "shot_arrow");
        var launch = muzzle ? muzzle : transform;
        lastMuzzle = launch.position;
        lastAim = currentTarget.AimPoint;
        if (kind == WeaponKind.Crystal) FireChain(launch.position);
        else
        {
            TowerPreviewEffects.Shot(launch, currentTarget, kind == WeaponKind.Cannon, damage, this);
            TowerPreviewEffects.Burst(launch.position, kind == WeaponKind.Cannon ? new Color(1, .55f, .15f) : new Color(1, .85f, .5f),
                kind == WeaponKind.Cannon ? .45f : .16f, kind == WeaponKind.Cannon ? 18 : 5);
            if (recoilPart) recoil = kind == WeaponKind.Cannon ? .16f : .065f;
        }
    }

    void FireChain(Vector3 origin)
    {
        // Select before applying lethal damage; registry removal cannot change this committed cast.
        var victims = new List<EnemyHealth>(3) { currentTarget };
        var excluded = new HashSet<EnemyHealth> { currentTarget };
        var previous = currentTarget;
        for (int i = 1; i < Mathf.Clamp(maxChainTargets, 1, 3); i++)
        {
            var next = FindTarget(previous.transform.position, Mathf.Max(0, chainRange), excluded);
            if (!next) break;
            victims.Add(next);
            excluded.Add(next);
            previous = next;
        }
        var color = new Color(.3f, .8f, 1);
        foreach (var victim in victims)
        {
            if (!victim || !victim.isActiveAndEnabled || !victim.IsAlive) continue;
            Vector3 impact = victim.AimPoint;
            TowerPreviewEffects.Bolt(origin, impact, color);
            TowerPreviewEffects.Burst(impact, color, origin == lastMuzzle ? .5f : .35f);
            victim.Damage(damage, SlowDuration);
            hits++;
            if (victim != victims[0]) chainHits++;
            origin = impact;
        }
    }
}
