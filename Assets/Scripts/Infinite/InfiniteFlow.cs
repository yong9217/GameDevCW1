using System;
using UnityEngine;
using UnityEngine.UIElements;

// Infinite mode: hold the castle as long as possible on the wasteland map.
//  - No tutorial and no last wave: a run only ends when the castle falls.
//  - The survival timer is shown at the top centre (it stops while the game is paused).
//  - Spawn() puts enemies on the ring around the castle; the wave design (InfiniteWaveDirector) uses it.
//  - When the castle falls, the defeat screen (EndScreenUI) shows the time and the local top-5 leaderboard.
public class InfiniteFlow : MonoBehaviour
{
    // The running Infinite mode, or null in the campaign / main menu.
    public static InfiniteFlow Current { get; private set; }

    [Header("Enemy prefabs")]
    public GameObject normalEnemy;
    public GameObject fastEnemy;
    public GameObject heavyEnemy;
    public GameObject bossEnemy;

    [Header("Scene references")]
    public Base castle;
    public GameEnd gameEnd;
    public MouseStateHandler mouse;
    public UIResponder hud;
    [Tooltip("The campaign's speech bubble; it is switched off in this mode.")]
    public SpeechEngine speech;

    [Header("Spawning")]
    [Tooltip("Distance from the castle centre at which enemies appear.")]
    [Min(1f)] public float spawnDistance = 15f;
    [Tooltip("Random extra distance (+/-) so enemies do not stand on a perfect circle.")]
    [Min(0f)] public float spawnJitter = 2f;

    // Seconds the castle has held so far (game time: pauses do not count).
    public float SurvivedSeconds { get; private set; }
    public bool IsRunning => !over;
    // Raised once, when the castle falls, with the final time.
    public event Action<float> RunEnded;

    bool over;
    VisualElement hudBox;
    Label hudLabel;

    void Awake()
    {
        Current = this;
        if (!castle) castle = FindAnyObjectByType<Base>();
        if (!gameEnd) gameEnd = FindAnyObjectByType<GameEnd>();
        if (!mouse) mouse = FindAnyObjectByType<MouseStateHandler>();
        if (!hud) hud = FindAnyObjectByType<UIResponder>();
        if (!speech) speech = FindAnyObjectByType<SpeechEngine>();

        if (hud && hud.TryGetComponent(out PanelRenderer hudPanel)) hudPanel.RegisterUIReloadCallback(BuildHud);
        // The speech bubble only belongs to the campaign tutorial.
        if (speech) speech.gameObject.SetActive(false);
        if (!GetComponent<GoldPopups>()) gameObject.AddComponent<GoldPopups>();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        GameAudio.SetMusic(GameAudio.Music.Battle);
    }

    void Update()
    {
        if (over) return;
        if ((castle && castle.GetHP() <= 0f) || (mouse && mouse.locked))
        {
            over = true;
            RefreshHud();
            RunEnded?.Invoke(SurvivedSeconds);
            return;
        }
        SurvivedSeconds += Time.deltaTime;
        RefreshHud();
    }

    // ---------------- Spawning interface (used by the wave design) ----------------

    // One enemy on the ring around the castle. angleDegrees: direction from the castle (0 = +Z, 90 = +X);
    // arcDegrees: random spread around that direction (0 = exactly there, 360 = anywhere).
    public GameObject Spawn(GameFlow.EnemyKind kind, float angleDegrees, float arcDegrees = 0f)
    {
        if (over) return null;
        return EnemySpawn.AtRing(PrefabFor(kind), castle ? castle.transform.position : Vector3.zero, angleDegrees,
            arcDegrees, spawnDistance, spawnJitter, kind == GameFlow.EnemyKind.Boss, this);
    }

    // One enemy from a random direction.
    public GameObject SpawnAnywhere(GameFlow.EnemyKind kind) => Spawn(kind, UnityEngine.Random.Range(0f, 360f));

    // Enemies still alive on the map.
    public int EnemiesAlive => EnemyHealth.Active.Count;

    GameObject PrefabFor(GameFlow.EnemyKind kind) => kind switch
    {
        GameFlow.EnemyKind.Fast => fastEnemy,
        GameFlow.EnemyKind.Heavy => heavyEnemy,
        GameFlow.EnemyKind.Boss => bossEnemy,
        _ => normalEnemy,
    };

    // 83.4 -> "01:23", 3725 -> "1:02:05".
    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
    }

    // ---------------- HUD (top centre timer) ----------------

    void BuildHud(PanelRenderer panel, VisualElement root, int version)
    {
        hudBox?.RemoveFromHierarchy();
        // The campaign's "Enemy Forces" wave meter means nothing here.
        var waveHud = root.Q<VisualElement>("WaveHUD");
        if (waveHud != null) waveHud.style.display = DisplayStyle.None;

        // Same look as the campaign's wave box (RoyalHud.uss styles #GameFlowHUD and #GameFlowLabel).
        hudBox = new VisualElement { name = "GameFlowHUD", pickingMode = PickingMode.Ignore };
        var s = hudBox.style;
        s.position = Position.Absolute;
        s.top = 30;
        s.left = Length.Percent(50);
        s.translate = new Translate(Length.Percent(-50), 0);
        s.flexDirection = FlexDirection.Row;
        s.alignItems = Align.Center;
        s.paddingTop = 14; s.paddingBottom = 14; s.paddingLeft = 34; s.paddingRight = 34;

        hudLabel = new Label { name = "GameFlowLabel", pickingMode = PickingMode.Ignore };
        hudLabel.style.fontSize = 46;
        hudBox.Add(hudLabel);
        root.Add(hudBox);
        RefreshHud();
    }

    void RefreshHud()
    {
        if (hudLabel == null || hudLabel.panel == null) return;
        hudLabel.text = "Time  " + FormatTime(SurvivedSeconds);
    }
}
