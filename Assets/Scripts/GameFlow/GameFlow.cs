using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

// Runs the whole match on one map: 3 tutorial waves, 2 normal waves, then the boss wave.
//  - Tutorial text only informs. It never pauses the game and never waits for the player.
//  - Between waves there is a countdown; the player can press "Start now" to skip it.
//  - Enemies appear on a ring around the base, inside an arc that widens as the game goes on.
// Every wave is one entry in the "Waves" list below and can be edited in the Inspector.
// Replaces the old GameplayManager / WaveHandler / Spawner set-up (those objects are only disabled).
public class GameFlow : MonoBehaviour
{
    public enum EnemyKind { Normal, Fast, Heavy, Boss }

    [Serializable]
    public class SpawnGroup
    {
        public EnemyKind enemy;
        [Min(1)] public int count = 1;
        [Tooltip("Seconds between two enemies of this group.")]
        [Min(0.05f)] public float interval = 2f;
        [Tooltip("Seconds after the wave starts before this group begins.")]
        [Min(0f)] public float startDelay;

        public SpawnGroup() { }
        public SpawnGroup(EnemyKind enemy, int count, float interval, float startDelay = 0f)
        {
            this.enemy = enemy; this.count = count; this.interval = interval; this.startDelay = startDelay;
        }
    }

    [Serializable]
    public class Wave
    {
        public string title;
        [Tooltip("Width of the arc the enemies come from, in degrees. 360 = from every side.")]
        [Range(10f, 360f)] public float arcDegrees = 60f;
        [Tooltip("Shown when the countdown to this wave begins.")]
        [TextArea] public string prepMessage;
        [Tooltip("Shown when this wave starts.")]
        [TextArea] public string startMessage;
        public List<SpawnGroup> groups = new List<SpawnGroup>();
    }

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
    [Tooltip("The speech bubble above the castle (the object with SpeechEngine).")]
    public SpeechEngine speech;

    [Header("Spawning")]
    [Tooltip("Distance from the castle centre at which enemies appear.")]
    [Min(1f)] public float spawnDistance = 15f;
    [Tooltip("Random extra distance (+/-) so enemies do not stand on a perfect circle.")]
    [Min(0f)] public float spawnJitter = 2f;

    [Header("Timing (seconds)")]
    [Min(0f)] public float firstCountdown = 20f;
    [Min(0f)] public float countdownBetweenWaves = 15f;
    [Min(1f)] public float messageSeconds = 8f;

    [Header("Messages")]
    [TextArea] public string victoryMessage = "Victory! The castle is safe!";

    public List<Wave> waves = DefaultWaves();

    enum State { Countdown, Running, Won, Lost }
    State state = State.Countdown;
    int waveIndex;
    float countdown;
    float arcCentre;
    int groupsRunning;
    int toSpawn;

    // HUD (top centre): wave title + countdown, and the "Start now" button.
    VisualElement hudBox;
    Label hudLabel;
    Button startNowButton;

    // Speech bubble above the castle.
    VisualElement bubble;
    Label bubbleText;
    Button bubbleClose;
    string pendingMessage;
    float bubbleHideAt = -1f, bubbleShownAt;

    void Awake()
    {
        if (!castle) castle = FindAnyObjectByType<Base>();
        if (!gameEnd) gameEnd = FindAnyObjectByType<GameEnd>();
        if (!mouse) mouse = FindAnyObjectByType<MouseStateHandler>();
        if (!hud) hud = FindAnyObjectByType<UIResponder>();
        if (!speech) speech = FindAnyObjectByType<SpeechEngine>();

        if (hud && hud.TryGetComponent(out PanelRenderer hudPanel)) hudPanel.RegisterUIReloadCallback(BuildHud);
        if (speech && speech.TryGetComponent(out PanelRenderer speechPanel)) speechPanel.RegisterUIReloadCallback(HookBubble);

        // The old speech boxes come from the scene without an end action; their close button would log errors.
        if (speech && speech.textBoxes != null)
            foreach (var box in speech.textBoxes) box?.OnSpeechEnd(() => { });
    }

    void Start()
    {
        waveIndex = 0;
        BeginCountdown(firstCountdown);
        UpdateWaveMeter();
    }

    void Update()
    {
        if (state == State.Won || state == State.Lost) return;

        if ((castle && castle.GetHP() <= 0f) || (mouse && mouse.locked))
        {
            state = State.Lost;
            StopAllCoroutines();
            if (hudBox != null) hudBox.style.display = DisplayStyle.None;
            HideBubble();
            return;
        }

        if (state == State.Countdown)
        {
            countdown -= Time.deltaTime;
            if (countdown <= 0f) StartWave();
        }
        else if (state == State.Running && groupsRunning == 0 && EnemyHealth.Active.Count == 0)
        {
            WaveCleared();
        }

        if (bubbleHideAt > 0f && Time.time >= bubbleHideAt) HideBubble();
        // The bubble's own transition hides its X button, so show it again once the pop-in has finished.
        if (bubbleHideAt > 0f && bubbleClose != null && Time.time - bubbleShownAt > .45f) bubbleClose.visible = true;

        RefreshHud();
        // Cheap, and keeps the meter right even if its UI loads after Start.
        UpdateWaveMeter();
    }

    void BeginCountdown(float seconds)
    {
        state = State.Countdown;
        countdown = seconds;
        Say(waves.Count > waveIndex ? waves[waveIndex].prepMessage : null);
    }

    // Also called by the "Start now" button.
    public void StartWave()
    {
        if (state != State.Countdown || waveIndex >= waves.Count) return;
        var wave = waves[waveIndex];
        state = State.Running;
        arcCentre = UnityEngine.Random.Range(0f, 360f);
        groupsRunning = 0;
        toSpawn = 0;
        foreach (var group in wave.groups)
        {
            if (group == null || group.count <= 0) continue;
            groupsRunning++;
            toSpawn += group.count;
            StartCoroutine(SpawnGroupRoutine(group, wave.arcDegrees));
        }
        Say(wave.startMessage);
    }

    void WaveCleared()
    {
        waveIndex++;
        UpdateWaveMeter();
        if (waveIndex >= waves.Count)
        {
            state = State.Won;
            Say(victoryMessage);
            RefreshHud();
            if (gameEnd) gameEnd.CueWin();
            return;
        }
        BeginCountdown(countdownBetweenWaves);
    }

    IEnumerator SpawnGroupRoutine(SpawnGroup group, float arc)
    {
        if (group.startDelay > 0f) yield return new WaitForSeconds(group.startDelay);
        for (int i = 0; i < group.count; i++)
        {
            Spawn(group.enemy, arc);
            toSpawn--;
            if (i < group.count - 1) yield return new WaitForSeconds(group.interval);
        }
        groupsRunning--;
    }

    void Spawn(EnemyKind kind, float arc)
    {
        GameObject prefab = PrefabFor(kind);
        if (!prefab)
        {
            Debug.LogError($"GameFlow: no prefab assigned for {kind} enemies.", this);
            return;
        }
        Vector3 centre = castle ? castle.transform.position : Vector3.zero;
        centre.y = 0f;
        Vector3 position = centre;
        bool found = false;
        for (int attempt = 0; attempt < 12 && !found; attempt++)
        {
            float angle = (arcCentre + UnityEngine.Random.Range(-arc * .5f, arc * .5f)) * Mathf.Deg2Rad;
            float distance = spawnDistance + UnityEngine.Random.Range(-spawnJitter, spawnJitter);
            Vector3 candidate = centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                position = hit.position;
                found = true;
            }
        }
        if (!found)
        {
            Debug.LogWarning("GameFlow: no walkable ground found at the spawn ring; check spawnDistance.", this);
            float angle = arcCentre * Mathf.Deg2Rad;
            position = centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * spawnDistance;
        }
        Vector3 look = centre - position;
        look.y = 0f;
        Instantiate(prefab, position, look.sqrMagnitude > .01f ? Quaternion.LookRotation(look) : Quaternion.identity);
    }

    GameObject PrefabFor(EnemyKind kind) => kind switch
    {
        EnemyKind.Fast => fastEnemy,
        EnemyKind.Heavy => heavyEnemy,
        EnemyKind.Boss => bossEnemy,
        _ => normalEnemy,
    };

    void UpdateWaveMeter()
    {
        if (!hud || waves.Count == 0) return;
        float remaining = Mathf.Clamp(waves.Count - waveIndex, 0, waves.Count);
        hud.UpdateWaveMeter(100f * remaining / waves.Count, true);
    }

    // ---------------- HUD ----------------

    void BuildHud(PanelRenderer panel, VisualElement root, int version)
    {
        hudBox?.RemoveFromHierarchy();

        hudBox = new VisualElement { name = "GameFlowHUD" };
        var s = hudBox.style;
        s.position = Position.Absolute;
        s.top = 30;
        s.left = Length.Percent(50);
        s.translate = new Translate(Length.Percent(-50), 0);
        s.flexDirection = FlexDirection.Row;
        s.alignItems = Align.Center;
        s.paddingTop = 14; s.paddingBottom = 14; s.paddingLeft = 30; s.paddingRight = 20;
        // Colours, borders and fonts come from RoyalHud.uss (#GameFlowHUD, #GameFlowLabel, #GameFlowStartNow).

        hudLabel = new Label { name = "GameFlowLabel" };
        hudLabel.style.fontSize = 46;
        hudLabel.style.marginRight = 24;

        startNowButton = new Button(StartWave) { name = "GameFlowStartNow", text = "Start now" };
        var b = startNowButton.style;
        b.fontSize = 40;
        b.paddingLeft = 24; b.paddingRight = 24; b.paddingTop = 6; b.paddingBottom = 6;

        hudBox.Add(hudLabel);
        hudBox.Add(startNowButton);
        root.Add(hudBox);
        RefreshHud();
    }

    void RefreshHud()
    {
        if (hudLabel == null || hudLabel.panel == null) return;
        string title = waveIndex < waves.Count ? waves[waveIndex].title : "";
        switch (state)
        {
            case State.Countdown:
                hudLabel.text = $"{title} in {Mathf.CeilToInt(Mathf.Max(0f, countdown))}s";
                startNowButton.style.display = DisplayStyle.Flex;
                break;
            case State.Running:
                hudLabel.text = $"{title}  -  enemies left: {toSpawn + EnemyHealth.Active.Count}";
                startNowButton.style.display = DisplayStyle.None;
                break;
            case State.Won:
                hudLabel.text = "Victory!";
                startNowButton.style.display = DisplayStyle.None;
                break;
        }
    }

    // ---------------- Speech bubble ----------------

    void HookBubble(PanelRenderer panel, VisualElement root, int version)
    {
        bubble = root.Q<VisualElement>("Speech");
        bubbleText = root.Q<Label>("Desc");
        bubbleClose = root.Q<Button>("exit");
        if (bubbleClose != null) bubbleClose.clicked += HideBubble;
        // The bubble had a fixed height of 120; let it grow so three lines of text never touch the border.
        var box = root.Q<VisualElement>("Tutorial");
        if (box != null)
        {
            box.style.height = StyleKeyword.Auto;
            box.style.minHeight = 120;
            box.style.paddingBottom = 26;
        }

        if (!string.IsNullOrEmpty(pendingMessage)) Say(pendingMessage);
        else if (bubbleHideAt < 0f) HideBubble();
    }

    void Say(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (bubble == null || bubbleText == null)
        {
            pendingMessage = message;
            return;
        }
        pendingMessage = null;
        bubbleText.text = message;
        bubble.style.scale = new StyleScale(new Vector2(1f, 1f));
        bubbleShownAt = Time.time;
        bubbleHideAt = Time.time + messageSeconds;
    }

    void HideBubble()
    {
        bubbleHideAt = -1f;
        if (bubble != null) bubble.style.scale = new StyleScale(new Vector2(0f, 0f));
        if (bubbleClose != null) bubbleClose.visible = false;
    }

    // ---------------- Default wave table ----------------

    static List<Wave> DefaultWaves() => new List<Wave>
    {
        new Wave
        {
            title = "Tutorial 1/3: Arrow Tower", arcDegrees = 60f,
            prepMessage = "The castle is under attack! Pick the Arrow Tower card, then click the ground.",
            startMessage = "Arrow towers fire fast. Defeated enemies give you gold.",
            groups = { new SpawnGroup(EnemyKind.Normal, 4, 3f) },
        },
        new Wave
        {
            title = "Tutorial 2/3: Cannon Tower", arcDegrees = 90f,
            prepMessage = "Armoured Heavies spotted! Cannon towers hit hard - build one to break them.",
            startMessage = "Heavies are slow but tough. Let the cannons do the work!",
            groups = { new SpawnGroup(EnemyKind.Heavy, 3, 5f), new SpawnGroup(EnemyKind.Normal, 2, 3f, 2f) },
        },
        new Wave
        {
            title = "Tutorial 3/3: Magic Tower", arcDegrees = 30f,
            prepMessage = "Fast raiders incoming! Magic towers hit 3 enemies at once and slow them.",
            startMessage = "Here they come, fast and all together!",
            groups = { new SpawnGroup(EnemyKind.Fast, 8, .4f) },
        },
        new Wave
        {
            title = "Wave 1/2", arcDegrees = 180f,
            prepMessage = "Tutorial complete! Now enemies attack from wider directions.",
            groups = { new SpawnGroup(EnemyKind.Normal, 8, 2f), new SpawnGroup(EnemyKind.Fast, 4, 1f, 6f) },
        },
        new Wave
        {
            title = "Wave 2/2", arcDegrees = 360f,
            prepMessage = "They are surrounding the castle. Watch every side!",
            groups =
            {
                new SpawnGroup(EnemyKind.Normal, 6, 2f),
                new SpawnGroup(EnemyKind.Heavy, 3, 5f, 3f),
                new SpawnGroup(EnemyKind.Fast, 6, .5f, 10f),
            },
        },
        new Wave
        {
            title = "Boss Wave", arcDegrees = 360f,
            prepMessage = "Their leader is coming! Defeat the boss to save the castle!",
            startMessage = "The leader has arrived!",
            groups =
            {
                new SpawnGroup(EnemyKind.Boss, 1, 1f),
                new SpawnGroup(EnemyKind.Normal, 6, 2.5f, 2f),
                new SpawnGroup(EnemyKind.Fast, 4, .6f, 12f),
            },
        },
    };
}
