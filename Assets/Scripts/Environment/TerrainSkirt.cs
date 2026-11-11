using UnityEngine;

// Plain ground around the map, so moving the camera never shows the empty grey background past the terrain's edge.
// It adds flat terrain tiles around the main terrain, painted with one of its own terrain layers (same look, same
// lighting) and with no trees, rocks or colliders: enemies, navigation and tower placement do not see them at all.
// The tiles are made when the scene opens (in the editor too) and are never saved into the scene.
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class TerrainSkirt : MonoBehaviour
{
    [Tooltip("Rings of tiles around the map. 1 = one tile on every side (3x3), 2 = 5x5.")]
    [Range(1, 3)] public int rings = 1;
    [Tooltip("Which of the main terrain's layers covers the extra ground (0 = the first layer).")]
    [Min(0)] public int fillLayer = 0;

    GameObject holder;
    TerrainData[] madeData = new TerrainData[0];

    void OnEnable() => Build();
    void OnDisable() => Clear();

    void OnValidate()
    {
        // Rebuild after an Inspector change. Not allowed to create objects inside OnValidate itself, so wait a frame.
        if (!isActiveAndEnabled) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this && isActiveAndEnabled) Build(); };
#endif
    }

    void Build()
    {
        Clear();
        var main = GetComponent<Terrain>();
        TerrainData source = main ? main.terrainData : null;
        if (!source) return;

        TerrainLayer[] layers = source.terrainLayers;
        if (layers == null || layers.Length == 0) return;
        int layer = Mathf.Clamp(fillLayer, 0, layers.Length - 1);
        Vector3 size = source.size;
        float edgeHeight = AverageEdgeHeight(source);

        holder = new GameObject("Ground skirt (made at runtime)") { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
        holder.transform.SetParent(transform, false);

        int n = (2 * rings + 1) * (2 * rings + 1) - 1;
        madeData = new TerrainData[n];
        int made = 0;
        for (int x = -rings; x <= rings; x++)
        {
            for (int z = -rings; z <= rings; z++)
            {
                if (x == 0 && z == 0) continue;

                var data = new TerrainData { name = "Ground skirt", hideFlags = HideFlags.DontSave };
                data.heightmapResolution = 33;
                data.alphamapResolution = 16;
                data.baseMapResolution = 16;
                data.size = size;
                data.terrainLayers = new[] { layers[layer] };
                if (size.y > 0f && edgeHeight > 0f)
                {
                    var heights = new float[33, 33];
                    float h = edgeHeight / size.y;
                    for (int i = 0; i < 33; i++)
                        for (int j = 0; j < 33; j++) heights[i, j] = h;
                    data.SetHeights(0, 0, heights);
                }
                madeData[made++] = data;

                var tile = new GameObject($"Ground skirt {x},{z}") { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
                tile.transform.SetParent(holder.transform, false);
                tile.transform.position = transform.position + new Vector3(x * size.x, 0f, z * size.z);
                tile.layer = gameObject.layer;
                var terrain = tile.AddComponent<Terrain>();
                terrain.terrainData = data;
                terrain.materialTemplate = main.materialTemplate;
                terrain.heightmapPixelError = main.heightmapPixelError;
                terrain.basemapDistance = main.basemapDistance;
                terrain.shadowCastingMode = main.shadowCastingMode;
                terrain.drawTreesAndFoliage = false;
                terrain.allowAutoConnect = false;
                terrain.groupingID = -1;
            }
        }
    }

    // The flat ground should meet the map's border at the same height.
    static float AverageEdgeHeight(TerrainData data)
    {
        int res = data.heightmapResolution;
        float[,] h = data.GetHeights(0, 0, res, res);
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < res; i++)
        {
            sum += h[0, i] + h[res - 1, i] + h[i, 0] + h[i, res - 1];
            count += 4;
        }
        return count > 0 ? sum / count * data.size.y : 0f;
    }

    void Clear()
    {
        if (holder)
        {
            if (Application.isPlaying) Destroy(holder);
            else DestroyImmediate(holder);
        }
        holder = null;
        foreach (var data in madeData)
        {
            if (!data) continue;
            if (Application.isPlaying) Destroy(data);
            else DestroyImmediate(data);
        }
        madeData = new TerrainData[0];
    }
}
