using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// "+N" with a coin icon where an enemy dies. It rises for a moment, then flies to the gold amount at the
// top left, which gives a small bump when it lands: the player sees that defeated enemies pay gold.
// Display only (the gold itself is paid by EnemyHealth). GameFlow adds this component at runtime.
public class GoldPopups : MonoBehaviour
{
    public UIResponder hud;
    [Tooltip("Seconds the coin stays above the enemy before it flies off.")]
    [Min(0f)] public float holdSeconds = .55f;
    [Tooltip("Seconds of the flight to the gold HUD.")]
    [Min(.05f)] public float flySeconds = .55f;
    [Tooltip("How far the coin rises while it waits, in HUD pixels.")]
    public float risePixels = 70f;
    [Tooltip("Start height above the enemy, in world units.")]
    public float heightAboveEnemy = 1.6f;

    class Popup
    {
        public VisualElement box;
        public Vector3 world;
        public Vector2 flyFrom;
        public float age;
    }

    readonly List<Popup> popups = new List<Popup>();
    VisualElement layer;

    void Awake()
    {
        if (!hud) hud = FindAnyObjectByType<UIResponder>();
        if (hud && hud.TryGetComponent(out PanelRenderer panel)) panel.RegisterUIReloadCallback(Build);
    }

    void OnEnable() => EnemyHealth.AnyDied += OnEnemyDied;
    void OnDisable() => EnemyHealth.AnyDied -= OnEnemyDied;

    void Build(PanelRenderer panel, VisualElement root, int version)
    {
        layer?.RemoveFromHierarchy();
        popups.Clear();
        layer = new VisualElement { name = "GoldPopups", pickingMode = PickingMode.Ignore };
        layer.style.position = Position.Absolute;
        layer.style.left = 0; layer.style.top = 0; layer.style.right = 0; layer.style.bottom = 0;
        root.Add(layer);
    }

    void OnEnemyDied(EnemyHealth enemy)
    {
        if (!enemy || enemy.killReward <= 0 || layer == null || layer.panel == null) return;

        var box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.AddToClassList("gold-popup");
        box.style.position = Position.Absolute;
        box.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
        var label = new Label("+" + enemy.killReward) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("gold-popup-label");
        var coin = new VisualElement { pickingMode = PickingMode.Ignore };
        coin.AddToClassList("gold-popup-coin");
        box.Add(label);
        box.Add(coin);
        layer.Add(box);

        popups.Add(new Popup { box = box, world = enemy.transform.position + Vector3.up * heightAboveEnemy });
        Animate(0f);
    }

    // Real time, so coins already in the air still arrive when a tutorial paper pauses the game.
    void LateUpdate() => Animate(Time.unscaledDeltaTime);

    void Animate(float dt)
    {
        var cam = Camera.main;
        bool haveTarget = hud && hud.TryGetCoinCentre(out _);
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            p.age += dt;
            if (!cam || layer == null || layer.panel == null || p.age >= holdSeconds + flySeconds)
            {
                if (p.age >= holdSeconds + flySeconds && hud) hud.BumpMoney();
                p.box.RemoveFromHierarchy();
                popups.RemoveAt(i);
                continue;
            }

            Vector2 at;
            float scale, opacity = 1f;
            if (p.age < holdSeconds || !haveTarget)
            {
                // Pop up above the enemy and rise a little.
                float k = Mathf.Clamp01(p.age / Mathf.Max(.01f, holdSeconds));
                Vector2 ground = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, p.world, cam);
                at = ground + Vector2.down * (risePixels * (1f - (1f - k) * (1f - k)));
                p.flyFrom = at;
                scale = k < .2f ? Mathf.Lerp(.4f, 1.25f, k / .2f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - .2f) / .3f));
                if (!haveTarget) opacity = 1f - Mathf.Clamp01((p.age - holdSeconds) / flySeconds);
            }
            else
            {
                // Fly to the gold HUD, speeding up on the way.
                hud.TryGetCoinCentre(out Vector2 target);
                float k = Mathf.Clamp01((p.age - holdSeconds) / flySeconds);
                float e = k * k;
                at = Vector2.Lerp(p.flyFrom, target, e);
                scale = Mathf.Lerp(1f, .55f, e);
            }

            p.box.style.left = at.x;
            p.box.style.top = at.y;
            p.box.style.scale = new Scale(new Vector2(scale, scale));
            p.box.style.opacity = opacity;
        }
    }
}
