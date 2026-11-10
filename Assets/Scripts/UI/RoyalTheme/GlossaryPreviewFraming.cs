using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Frames the glossary (bestiary) portraits automatically.
//
// Every portrait is a camera that renders into a "...PreviewPort" render texture. When a scene loads, each of
// those cameras finds the unit or building standing in front of it, measures the model as it really is now
// (so changing a model's scale never cuts it off again), and moves to show the whole model, centred, from
// slightly above. It also shows those models with flat, unlit colours and no shadows (in the game they stay
// lit). Nothing has to be set up in the scene.
public class GlossaryPreviewFraming : MonoBehaviour
{
    // Looking down a little shows the shape of a model better than a straight side view.
    const float PitchDegrees = 12f;
    // Empty space kept around the model (1 = touching the picture edges).
    const float Margin = 1.06f;
    // Wait for animators to take their first pose before measuring.
    const float MeasureDelay = .25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded += (scene, mode) => Begin();
        Begin();
    }

    static void Begin()
    {
        var runner = new GameObject("GlossaryPreviewFraming").AddComponent<GlossaryPreviewFraming>();
        runner.hideFlags = HideFlags.HideInHierarchy;
    }

    IEnumerator Start()
    {
        float end = Time.realtimeSinceStartup + MeasureDelay;
        while (Time.realtimeSinceStartup < end) yield return null;
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cam.targetTexture && cam.targetTexture.name.Contains("PreviewPort")) Frame(cam);
        Destroy(gameObject);
    }

    static void Frame(Camera cam)
    {
        Transform subject = FindSubject(cam);
        if (!subject || !TryGetBounds(subject, out Bounds b)) return;
        MakeFlat(subject);

        // Keep the camera's sideways direction, but tilt it down a little.
        Vector3 flatForward = cam.transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < .001f) flatForward = Vector3.back;
        Quaternion rotation = Quaternion.LookRotation(flatForward.normalized) * Quaternion.Euler(PitchDegrees, 0f, 0f);

        // Distance at which the whole box fits the picture, both in height and in width.
        float aspect = cam.targetTexture.width / (float)cam.targetTexture.height;
        float tanV = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
        float tanH = tanV * aspect;
        Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
        float halfWidth = 0f, halfHeight = 0f, halfDepth = 0f;
        foreach (var corner in Corners(b))
        {
            Vector3 d = corner - b.center;
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(d, right)));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(d, up)));
            halfDepth = Mathf.Max(halfDepth, Mathf.Abs(Vector3.Dot(d, forward)));
        }
        float distance = Mathf.Max(halfHeight * Margin / tanV, halfWidth * Margin / tanH) + halfDepth;

        cam.transform.SetPositionAndRotation(b.center - forward * distance, rotation);
        cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, .1f);
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, distance + halfDepth * 4f + 50f);
    }

    static readonly int GlossaryUnlit = Shader.PropertyToID("_GlossaryUnlit");

    // No light and no shadow on a glossary model: the building shaders read _GlossaryUnlit (0 = lit, as in the game).
    // Set per renderer, so the shared materials - and therefore the game - are not changed.
    static void MakeFlat(Transform subject)
    {
        var block = new MaterialPropertyBlock();
        foreach (var r in subject.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.GetPropertyBlock(block);
            block.SetFloat(GlossaryUnlit, 1f);
            r.SetPropertyBlock(block);
        }
    }

    // The unit or building closest to the line the camera looks along.
    static Transform FindSubject(Camera cam)
    {
        var candidates = new HashSet<Transform>();
        foreach (var t in FindObjectsByType<EnemyCombatTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)) candidates.Add(t.transform);
        foreach (var e in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)) candidates.Add(e.transform);

        Vector3 origin = cam.transform.position, look = cam.transform.forward;
        origin.y = 0f; look.y = 0f; look.Normalize();
        Transform best = null;
        float bestScore = float.PositiveInfinity;
        foreach (var t in candidates)
        {
            Vector3 to = t.position - origin;
            to.y = 0f;
            float along = Vector3.Dot(to, look);
            if (along <= 0f || along > 60f) continue;
            float sideways = (to - look * along).magnitude;
            if (sideways > 6f) continue;
            float score = sideways * 4f + along * .05f;
            if (score < bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    static bool TryGetBounds(Transform subject, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var r in subject.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (found) bounds.Encapsulate(r.bounds);
            else { bounds = r.bounds; found = true; }
        }
        return found;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return b.center + Vector3.Scale(b.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
    }
}
