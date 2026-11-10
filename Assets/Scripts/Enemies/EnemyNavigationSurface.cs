using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Minimal runtime bake for this small prototype. No AI Navigation package is required.
[DefaultExecutionOrder(-200)]
public sealed class EnemyNavigationSurface : MonoBehaviour
{
    public Vector3 center;
    public Vector3 size = new Vector3(120, 30, 120);
    public LayerMask groundAndObstacles = ~0;
    public int agentTypeID;
    NavMeshData data;
    NavMeshDataInstance instance;

    void OnEnable() => Rebuild();
    void OnDisable() => Release();

    [ContextMenu("Rebuild enemy navigation")]
    public void Rebuild()
    {
        Release();
        var sources = new List<NavMeshBuildSource>();
        var bounds = new Bounds(transform.position + center, size);
        NavMeshBuilder.CollectSources(bounds, groundAndObstacles, NavMeshCollectGeometry.PhysicsColliders,
            0, new List<NavMeshBuildMarkup>(), sources);
        sources.RemoveAll(source => source.component &&
            (source.component.GetComponentInParent<EnemyCombatTarget>() ||
             source.component.GetComponentInParent<EnemyHealth>()));
        var settings = NavMesh.GetSettingsByID(agentTypeID);
        if (settings.agentTypeID < 0)
        {
            Debug.LogError("Enemy navigation agentTypeID is not configured in Navigation settings.", this);
            return;
        }
        data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        if (data) instance = NavMesh.AddNavMeshData(data);
        else Debug.LogError("Enemy navigation bake failed. Include ground colliders inside the surface bounds.", this);
    }

    void Release()
    {
        if (instance.valid) instance.Remove();
        if (data)
        {
            if (Application.isPlaying) Destroy(data);
            else DestroyImmediate(data);
        }
        data = null;
    }
}
