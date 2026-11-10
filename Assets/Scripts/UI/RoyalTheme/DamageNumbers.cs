using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Red floating "-N" numbers above towers and the castle whenever they lose health. Display only.
// Health is compared every frame, so every source of damage is shown without touching the combat code.
// Several hits on the same target in one frame are merged into one number.
public class DamageNumbers : MonoBehaviour
{
    public PanelRenderer hudPanel;
    [Min(.1f)] public float lifetime = 1f;
    [Tooltip("How far the number floats up, in HUD pixels.")]
    public float risePixels = 110f;
    public float fontSize = 64f;
    [Tooltip("Start height above the top of the model, in world units.")]
    public float heightAboveModel = .7f;
    public Color colour = new Color(.92f, .16f, .1f);

    class Popup
    {
        public Label label;
        public Vector3 world;
        public float age;
        public float jitter;
    }

    readonly Dictionary<EnemyCombatTarget, float> lastHealth = new Dictionary<EnemyCombatTarget, float>();
    readonly Dictionary<EnemyCombatTarget, float> modelTop = new Dictionary<EnemyCombatTarget, float>();
    readonly List<Popup> popups = new List<Popup>();
    readonly List<EnemyCombatTarget> gone = new List<EnemyCombatTarget>();
    VisualElement layer;

    void Awake()
    {
        if (!hudPanel)
        {
            var hud = FindAnyObjectByType<UIResponder>();
            if (hud) hudPanel = hud.GetComponent<PanelRenderer>();
        }
        if (hudPanel) hudPanel.RegisterUIReloadCallback(Build);
    }

    void Build(PanelRenderer panel, VisualElement root, int version)
    {
        layer?.RemoveFromHierarchy();
        popups.Clear();
        layer = new VisualElement { name = "DamageNumbers", pickingMode = PickingMode.Ignore };
        layer.style.position = Position.Absolute;
        layer.style.left = 0; layer.style.top = 0; layer.style.right = 0; layer.style.bottom = 0;
        root.Add(layer);
    }

    void LateUpdate()
    {
        foreach (var t in EnemyCombatTarget.Active)
        {
            if (!t) continue;
            float hp = t.CurrentHealth;
            if (lastHealth.TryGetValue(t, out float before) && hp < before - .001f) Spawn(t, before - hp);
            lastHealth[t] = hp;
        }

        // A tower leaves Active on the hit that destroys it; it still exists until the end of this frame.
        gone.Clear();
        foreach (var kv in lastHealth)
            if (!kv.Key || !EnemyCombatTarget.Active.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var t in gone)
        {
            if (t && t.CurrentHealth < lastHealth[t] - .001f) Spawn(t, lastHealth[t] - t.CurrentHealth);
            lastHealth.Remove(t);
            modelTop.Remove(t);
        }

        Animate();
    }

    void Spawn(EnemyCombatTarget target, float amount)
    {
        if (layer == null || layer.panel == null) return;
        var label = new Label("-" + Mathf.Max(1, Mathf.RoundToInt(amount))) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("royal-damage");
        var s = label.style;
        s.position = Position.Absolute;
        s.fontSize = fontSize;
        s.color = colour;
        s.unityFontStyleAndWeight = FontStyle.Bold;
        s.unityTextOutlineWidth = 3f;
        s.unityTextOutlineColor = new Color(.18f, 0f, 0f);
        s.translate = new Translate(Length.Percent(-50), Length.Percent(-100));
        layer.Add(label);
        popups.Add(new Popup
        {
            label = label,
            world = target.transform.position + Vector3.up * (TopOf(target) + heightAboveModel),
            jitter = Random.Range(-35f, 35f),
        });
        Animate();
    }

    void Animate()
    {
        var cam = Camera.main;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            float k = p.age / lifetime;
            if (k >= 1f || !cam || layer == null || layer.panel == null)
            {
                p.label.RemoveFromHierarchy();
                popups.RemoveAt(i);
                continue;
            }
            Vector2 at = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, p.world, cam);
            float rise = 1f - (1f - k) * (1f - k); // ease out
            p.label.style.left = at.x + p.jitter;
            p.label.style.top = at.y - risePixels * rise;
            p.label.style.opacity = k < .6f ? 1f : 1f - (k - .6f) / .4f;
            float pop = k < .12f ? 1.35f - (k / .12f) * .35f : 1f;
            p.label.style.scale = new Scale(new Vector2(pop, pop));
            p.age += Time.deltaTime;
        }
    }

    // Height of the model's top above its pivot (cached per target).
    float TopOf(EnemyCombatTarget t)
    {
        if (modelTop.TryGetValue(t, out float top)) return top;
        top = 2f;
        bool found = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled) continue;
            float y = r.bounds.max.y - t.transform.position.y;
            top = found ? Mathf.Max(top, y) : y;
            found = true;
        }
        modelTop[t] = top;
        return top;
    }
}
