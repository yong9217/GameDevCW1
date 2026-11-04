using UnityEngine;
using UnityEngine.AI;

// Building rules belong here, before instantiation, rather than in tower combat or the mouse handler.
// A new tower may not overlap: another tower, the base, or a LIVING enemy.
// Dead enemies never block: they leave EnemyHealth.Active and EnemyHealth switches their colliders off.
public static class TowerPlacement
{
    // Only used when an enemy has neither a CapsuleCollider nor a NavMeshAgent to size it from.
    const float FallbackEnemyRadius = .3f;

    // Checks every rule without creating anything. Call this BEFORE charging the player.
    public static bool CanPlace(GameObject prefab, Vector3 position, Quaternion rotation,
        float clearance, out string reason)
    {
        reason = null;
        if (!prefab)
        {
            reason = "Select a tower first.";
            return false;
        }

        var target = prefab.GetComponent<EnemyCombatTarget>();
        var box = target ? target.attackBounds as BoxCollider : null;
        if (!box || box.transform != prefab.transform || target.kind != EnemyCombatTarget.TargetKind.Tower ||
            !prefab.activeSelf || !target.enabled || !box.enabled || box.size.x <= 0 || box.size.z <= 0)
        {
            reason = "Tower prefab needs its configured root EnemyCombatTarget and BoxCollider.";
            return false;
        }
        if (!Finite(position) || !Finite(prefab.transform.localScale) ||
            float.IsNaN(clearance) || float.IsInfinity(clearance) || clearance < 0)
        {
            reason = "Invalid placement coordinates or clearance.";
            return false;
        }

        var matrix = Matrix4x4.TRS(position, rotation, prefab.transform.localScale);
        Bounds candidate = Footprint(box, matrix);

        // Towers and the base. Search scene objects instead of the combat registry: visible dead
        // towers still occupy ground until their GameObject is removed or deactivated.
        foreach (var existing in Object.FindObjectsByType<EnemyCombatTarget>(FindObjectsSortMode.None))
        {
            var existingBox = existing.attackBounds as BoxCollider;
            Bounds occupied;
            if (existingBox)
                occupied = Footprint(existingBox, existingBox.transform.localToWorldMatrix);
            else if (existing.attackBounds)
                occupied = existing.attackBounds.bounds;
            else
            {
                // Safe fallback for an incompletely configured scene target.
                var renderers = existing.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                occupied = renderers[0].bounds;
                foreach (var renderer in renderers) occupied.Encapsulate(renderer.bounds);
            }
            if (candidate.min.x < occupied.max.x + clearance && candidate.max.x > occupied.min.x - clearance &&
                candidate.min.z < occupied.max.z + clearance && candidate.max.z > occupied.min.z - clearance)
            {
                reason = "This position overlaps an existing tower or base.";
                return false;
            }
        }

        // Living enemies: each one is a circle on the ground (its real body radius) tested
        // against the tower footprint. Corpses are not in EnemyHealth.Active, so they never block.
        foreach (var enemy in EnemyHealth.Active)
        {
            if (!enemy || !enemy.IsAlive) continue;
            Vector3 center = enemy.transform.position;
            float reach = EnemyBodyRadius(enemy) + clearance;
            // Distance from the circle centre to the rectangle (zero when the centre is inside it).
            float dx = Mathf.Max(candidate.min.x - center.x, 0f, center.x - candidate.max.x);
            float dz = Mathf.Max(candidate.min.z - center.z, 0f, center.z - candidate.max.z);
            if (dx * dx + dz * dz < reach * reach)
            {
                reason = "This position overlaps a living enemy.";
                return false;
            }
        }
        return true;
    }

    // Checks, then instantiates. Use CanPlace + Instantiate instead when payment must happen in between.
    public static bool TryPlace(GameObject prefab, Vector3 position, Quaternion rotation,
        float clearance, out GameObject placed, out string reason)
    {
        placed = null;
        if (!CanPlace(prefab, position, rotation, clearance, out reason)) return false;
        placed = Object.Instantiate(prefab, position, rotation);
        return true;
    }

    // World-space body radius of an enemy: its capsule radius, else its agent radius, both scaled by the root.
    internal static float EnemyBodyRadius(EnemyHealth enemy)
    {
        Vector3 scale = enemy.transform.lossyScale;
        float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        var capsule = enemy.GetComponent<CapsuleCollider>();
        if (capsule) return capsule.radius * horizontalScale;
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent) return agent.radius * horizontalScale;
        return FallbackEnemyRadius;
    }

    // Conservative XZ bounds include rotation, offsets, nonuniform scale and
    // negative scale. Read transforms directly, so same-frame placement works
    // without waiting for a physics step or Physics.SyncTransforms.
    internal static Bounds Footprint(BoxCollider box, Matrix4x4 matrix)
    {
        var bounds = new Bounds(matrix.MultiplyPoint3x4(box.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            bounds.Encapsulate(matrix.MultiplyPoint3x4(box.center +
                Vector3.Scale(box.size * .5f, new Vector3(x, y, z))));
        return bounds;
    }

    static bool Finite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);
}
