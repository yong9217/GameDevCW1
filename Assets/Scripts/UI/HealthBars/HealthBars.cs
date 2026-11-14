using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Overhead health bars for enemies, towers and the base. Display only: it never changes health or damage.
//  - Base: always shown.   Enemies and towers: shown only after they have lost health.
//  - Own side (towers, base) = green.   Enemies = red.
//  - Bars always face the camera and are drawn on top of models, trees and towers.
// It creates itself when a scene loads, so no prefab or scene setup is needed. New towers and newly
// spawned enemies get a bar automatically; a bar disappears as soon as its unit dies or is destroyed.
public sealed class HealthBars : MonoBehaviour
{
    [Header("Colours")]
    public Color friendlyColour = new Color(.25f, .85f, .3f);
    public Color enemyColour = new Color(.9f, .2f, .2f);
    [Tooltip("Enemy bar colour while the enemy is slowed by the magic tower.")]
    public Color slowedColour = new Color(.35f, .75f, 1f);
    public Color backgroundColour = new Color(0f, 0f, 0f, .65f);

    [Header("Size (world units)")]
    [Tooltip("Bar width as a multiple of the unit's model width, clamped between min and max below.")]
    public float widthPerModelWidth = 1f;
    public float minWidth = .8f;
    public float maxWidth = 3f;
    public float height = .14f;
    [Tooltip("Gap between the top of the model and the bar.")]
    public float gapAboveModel = .35f;
    [Tooltip("Thickness of the dark border around the coloured part.")]
    public float border = .03f;

    static HealthBars instance;
    // Until this (real) time the castle's bar blinks, to show the player it is being hit.
    static float baseFlashUntil;
    static Sprite pixel;
    static Material material;

    sealed class Bar
    {
        public Transform owner;
        public EnemyHealth enemy;
        public EnemyCombatTarget target;
        public float headHeight, width;
        public GameObject root;
        public Transform background, fill;
        public SpriteRenderer fillRenderer;
    }

    readonly Dictionary<Object, Bar> bars = new Dictionary<Object, Bar>();
    readonly HashSet<Object> seen = new HashSet<Object>();
    readonly List<Object> toRemove = new List<Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; baseFlashUntil = 0f; }

    // Makes the castle's health bar blink for a few seconds (tutorial: the first time the castle is hit).
    public static void FlashBase(float seconds) => baseFlashUntil = Time.unscaledTime + seconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance) return;
        var go = new GameObject("HealthBars");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<HealthBars>();
    }

    void Awake()
    {
        if (instance && instance != this) { Destroy(gameObject); return; }
        instance = this;
        if (!pixel)
        {
            var tex = new Texture2D(1, 1) { name = "HealthBarPixel" };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        }
        if (!material)
        {
            // UI/Default is always included in builds and lets us switch off depth testing,
            // so bars are never hidden behind trees, towers or the base.
            var shader = Shader.Find("UI/Default");
            if (shader)
            {
                material = new Material(shader) { name = "HealthBarMaterial" };
                material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            }
        }
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        seen.Clear();

        // Only during a match: menus may show a decorative castle that must not get a bar.
        if (!FindAnyObjectByType<GameFlow>() && !InfiniteFlow.Current)
        {
            foreach (var pair in bars) if (pair.Value.root) Destroy(pair.Value.root);
            bars.Clear();
            return;
        }

        foreach (var enemy in EnemyHealth.Active)
        {
            if (!enemy || !enemy.IsAlive) continue;
            seen.Add(enemy);
            var bar = GetOrCreate(enemy, enemy.transform, enemy, null);
            Show(bar, enemy.CurrentHealth, enemy.maxHealth, false, cam);
            if (bar.fillRenderer) bar.fillRenderer.color = enemy.CurrentSpeedMultiplier < 1f ? slowedColour : enemyColour;
        }

        foreach (var target in EnemyCombatTarget.Active)
        {
            if (!target || !target.IsAlive) continue;
            seen.Add(target);
            var bar = GetOrCreate(target, target.transform, null, target);
            bool isBase = target.kind == EnemyCombatTarget.TargetKind.Base;
            float max = isBase && target.baseHealth ? target.baseHealth.startHP : target.maxHealth;
            Show(bar, target.CurrentHealth, max, isBase, cam);
        }

        // Units that died, were destroyed, or left the scene lose their bar immediately.
        toRemove.Clear();
        foreach (var pair in bars)
            if (!pair.Key || !seen.Contains(pair.Key)) toRemove.Add(pair.Key);
        foreach (var key in toRemove)
        {
            if (bars[key].root) Destroy(bars[key].root);
            bars.Remove(key);
        }
    }

    Bar GetOrCreate(Object key, Transform owner, EnemyHealth enemy, EnemyCombatTarget target)
    {
        if (bars.TryGetValue(key, out var bar)) return bar;

        bar = new Bar { owner = owner, enemy = enemy, target = target };
        Measure(owner, out bar.headHeight, out bar.width);

        bar.root = new GameObject("HealthBar_" + owner.name);
        bar.root.transform.SetParent(transform, false);
        bar.background = MakeQuad("Background", bar.root.transform, backgroundColour, 0).transform;
        var fillRenderer = MakeQuad("Fill", bar.root.transform, enemy ? enemyColour : friendlyColour, 1);
        bar.fill = fillRenderer.transform;
        bar.fillRenderer = fillRenderer;

        bars.Add(key, bar);
        return bar;
    }

    void Show(Bar bar, float current, float max, bool alwaysShow, Camera cam)
    {
        float ratio = max > 0 ? Mathf.Clamp01(current / max) : 0f;
        bool visible = cam && (alwaysShow || ratio < .999f);
        if (bar.root.activeSelf != visible) bar.root.SetActive(visible);
        if (!visible) return;

        // Face the camera and sit just above the model.
        var t = bar.root.transform;
        t.SetPositionAndRotation(bar.owner.position + Vector3.up * bar.headHeight, cam.transform.rotation);

        float w = bar.width;
        bar.background.localScale = new Vector3(w + border * 2f, height + border * 2f, 1f);
        bar.background.localPosition = Vector3.zero;
        // Fill shrinks towards the left edge.
        bar.fill.localScale = new Vector3(w * ratio, height, 1f);
        bar.fill.localPosition = new Vector3(-w * (1f - ratio) * .5f, 0f, -.001f);

        if (bar.target && bar.target.kind == EnemyCombatTarget.TargetKind.Base)
        {
            bool blink = Time.unscaledTime < baseFlashUntil && Mathf.Repeat(Time.unscaledTime, .3f) < .15f;
            bar.fillRenderer.color = blink ? Color.white : friendlyColour;
            float grow = Time.unscaledTime < baseFlashUntil ? 1.6f : 1f;
            bar.background.localScale = new Vector3((w + border * 2f), (height + border * 2f) * grow, 1f);
            bar.fill.localScale = new Vector3(w * ratio, height * grow, 1f);
        }
    }

    SpriteRenderer MakeQuad(string name, Transform parent, Color colour, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pixel;
        sr.color = colour;
        sr.sortingOrder = 1000 + order;
        sr.shadowCastingMode = ShadowCastingMode.Off;
        sr.receiveShadows = false;
        if (material) sr.sharedMaterial = material;
        return sr;
    }

    // Height of the model's top above its pivot, and a bar width that fits the model's size.
    void Measure(Transform owner, out float headHeight, out float width)
    {
        bool found = false;
        Bounds b = default;
        foreach (var r in owner.GetComponentsInChildren<Renderer>())
        {
            // Effects (particles, trails, lines) would make the size unreliable.
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!r.enabled) continue;
            if (!found) { b = r.bounds; found = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!found)
        {
            headHeight = 2f + gapAboveModel;
            width = minWidth;
            return;
        }
        headHeight = Mathf.Max(.5f, b.max.y - owner.position.y) + gapAboveModel;
        float modelWidth = Mathf.Max(b.size.x, b.size.z);
        width = Mathf.Clamp(modelWidth * widthPerModelWidth, minWidth, maxWidth);
    }
}
