using UnityEngine;
using UnityEngine.AI;

// Puts one enemy on a ring around the castle. Shared by both modes (GameFlow and InfiniteFlow).
public static class EnemySpawn
{
    // angleDegrees: direction from the castle (0 = +Z, 90 = +X). arcDegrees: random spread around that direction.
    // distance / jitter: radius of the ring and random +/- change of it.
    public static GameObject AtRing(GameObject prefab, Vector3 castlePosition, float angleDegrees, float arcDegrees,
        float distance, float jitter, bool isBoss = false, Object context = null)
    {
        if (!prefab)
        {
            Debug.LogError("EnemySpawn: no enemy prefab assigned.", context);
            return null;
        }
        Vector3 centre = castlePosition;
        centre.y = 0f;
        Vector3 position = centre;
        bool found = false;
        for (int attempt = 0; attempt < 12 && !found; attempt++)
        {
            float angle = (angleDegrees + Random.Range(-arcDegrees * .5f, arcDegrees * .5f)) * Mathf.Deg2Rad;
            float d = distance + Random.Range(-jitter, jitter);
            Vector3 candidate = centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * d;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                position = hit.position;
                found = true;
            }
        }
        if (!found)
        {
            Debug.LogWarning("EnemySpawn: no walkable ground found at the spawn ring; check the spawn distance.", context);
            float angle = angleDegrees * Mathf.Deg2Rad;
            position = centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance;
        }
        Vector3 look = centre - position;
        look.y = 0f;
        if (isBoss) GameAudio.Play("boss_arrive");
        GameObject enemy = Object.Instantiate(prefab, position, look.sqrMagnitude > .01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        // No walkable ground was found, so this enemy starts off the NavMesh: let it walk straight at the castle instead of standing still.
        if (!found && enemy.TryGetComponent(out EnemyBrain brain)) brain.EnableOffMeshFallback();
        return enemy;
    }
}
