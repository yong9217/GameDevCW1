using System;
using UnityEngine;
using UnityEngine.UIElements;

// Infinite mode: hold the castle as long as possible on the wasteland map.
//  - No last wave: a run only ends when the castle falls.
//  - The first time the mode is entered in a launch of the game, a parchment paper (TutorialPaper) explains the rules.
//  - The survival timer is shown at the top centre (it stops while the game is paused), with the countdown to the
//    next wave (InfiniteWaveDirector.NextWaveIn) underneath.
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

    [Header("Intro paper")]
    [Tooltip("Shown once per launch of the game, the first time Infinite mode is entered. The game is paused while it is open.")]
    public string introTitle = "Hold the Line!";
    [Tooltip("One bullet point per line. {interval} becomes the seconds between waves.")]
    [TextArea(4, 12)] public string introText =
        "There is no last wave: hold the castle for as long as you can.\n" +
        "A new wave arrives every <b>{interval}</b> seconds, even if the last one is still fighting. Each wave is bigger and comes from more directions.\n" +
        "Bosses join in from time to time, and later on towers get more expensive, so spend your gold wisely.\n" +
        "The top of the screen shows your time and when the next wave comes.\n" +
        "Build as in the Campaign: click a tower card, then click on empty ground. <b>Right-click</b> cancels.\n" +
        "Your best times go on this computer's <b>Top 5</b> leaderboard (also on the main menu).";

    // Set once the intro paper has been shown in this launch of the game ("Play again" does not show it again).
    static bool introShown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => introShown = false;

    bool over;
    VisualElement hudBox;
    Label hudLabel, nextWaveLabel;
    InfiniteWaveDirector waves;
    TutorialPaper paper;

    void Awake()
    {
        Current = this;
        waves = GetComponent<InfiniteWaveDirector>();
        if (!castle) castle = FindAnyObjectByType<Base>();
        if (!gameEnd) gameEnd = FindAnyObjectByType<GameEnd>();
        if (!mouse) mouse = FindAnyObjectByType<MouseStateHandler>();
        if (!hud) hud = FindAnyObjectByType<UIResponder>();
        if (!speech) speech = FindAnyObjectByType<SpeechEngine>();

        if (hud && hud.TryGetComponent(out PanelRenderer hudPanel)) hudPanel.RegisterUIReloadCallback(BuildHud);
        // The speech bubble only belongs to the campaign tutorial.
        if (speech) speech.gameObject.SetActive(false);
        if (!GetComponent<GoldPopups>()) gameObject.AddComponent<GoldPopups>();
        // Added in Awake (as GameFlow does) so it hooks the pause panel's UI before it loads.
        paper = GetComponent<TutorialPaper>();
        if (!paper) paper = gameObject.AddComponent<TutorialPaper>();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        GameAudio.SetMusic(GameAudio.Music.Battle);
        ShowIntroPaper();
    }

    // The same parchment paper as the campaign tutorial: it pauses the whole game (including the wave countdown)
    // until the player closes it.
    void ShowIntroPaper()
    {
        if (introShown) return;
        introShown = true;
        if (!paper) return;
        string interval = waves ? Mathf.RoundToInt(waves.secondsBetweenWaves).ToString() : "a few";
        paper.Show("Infinite Mode", introTitle, (introText ?? "").Replace("{interval}", interval), TutorialPaper.Icon.None);
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
        s.flexDirection = FlexDirection.Column;
        s.alignItems = Align.Center;
        s.paddingTop = 14; s.paddingBottom = 14; s.paddingLeft = 34; s.paddingRight = 34;

        hudLabel = new Label { name = "GameFlowLabel", pickingMode = PickingMode.Ignore };
        hudLabel.style.fontSize = 46;
        hudBox.Add(hudLabel);

        // Second, smaller line: countdown to the next wave (same font as the line above, lighter ink).
        nextWaveLabel = new Label { name = "GameFlowLabel", pickingMode = PickingMode.Ignore };
        nextWaveLabel.style.fontSize = 26;
        nextWaveLabel.style.color = new Color(122f / 255f, 59f / 255f, 34f / 255f);
        nextWaveLabel.style.marginTop = 2;
        hudBox.Add(nextWaveLabel);

        root.Add(hudBox);
        RefreshHud();
    }

    void RefreshHud()
    {
        if (hudLabel == null || hudLabel.panel == null) return;
        hudLabel.text = "Time  " + FormatTime(SurvivedSeconds);

        if (nextWaveLabel == null) return;
        bool showCountdown = waves && waves.isActiveAndEnabled && !over;
        nextWaveLabel.style.display = showCountdown ? DisplayStyle.Flex : DisplayStyle.None;
        if (!showCountdown) return;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, waves.NextWaveIn));
        nextWaveLabel.text = (waves.WavesStarted == 0 ? "First wave in " : "Next wave in ") + seconds + "s";
    }
}
