using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

// Runs the whole match on one map: 3 tutorial waves, 2 normal waves, then the boss wave.
//  - Each tutorial wave opens with a parchment "paper" (TutorialPaper) that pauses the game until the player
//    closes it. The paper explains the rules; the rest is shown in play: the asked-for tower card blinks until it
//    is built, "Start now" blinks after the first tower, coins fly to the gold HUD, the castle bar blinks when hit.
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
        [Tooltip("Check if new attack angle should be relative to the previous")]
        public bool relativeToPrevWave = false;
        [Tooltip("Possbile angles of attack, relative to the castle if not relative to previous wave. x = starting legal angle, y = ending legal angle. Exp. (30,60) = Can choose any angle between 30 and 60. Negative values are allowed")]
        public Vector2 attackAngleDegrees = new Vector2(0f,30f);
        [Tooltip("Shown when the countdown to this wave begins.")]
        [TextArea] public string prepMessage;
        [Tooltip("Shown when this wave starts.")]
        [TextArea] public string startMessage;
        public List<SpawnGroup> groups = new List<SpawnGroup>();

        [Header("Tutorial paper (pauses the game when the countdown to this wave begins)")]
        [Tooltip("Small line above the title, e.g. 'Tutorial 1 of 3'.")]
        public string paperStep;
        [Tooltip("Leave empty for no paper.")]
        public string paperTitle;
        public TutorialPaper.Icon paperIcon;
        [Tooltip("One bullet point per line. {waves} = total number of waves. Rich text such as <b>bold</b> works.")]
        [TextArea(4, 12)] public string paperText;
        [Tooltip("Tower card that blinks after the paper until the player builds that tower: 0 = Arrow, 1 = Cannon, 2 = Magic, -1 = none.")]
        [Range(-1, 2)] public int highlightCard = -1;
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
    [Tooltip("Second of the boss intro music at which the boss appears.")]
    [Min(0f)] public float bossEntranceInIntro = 5f;

    [Header("Messages")]
    [TextArea] public string victoryMessage = "Victory! The castle is safe!";
    [Tooltip("Tutorial, first wave: shown once the first tower is built, while \"Start now\" blinks.")]
    [TextArea] public string firstTowerMessage = "Well done! Press Start now to call the enemies, or wait for the timer.";
    [Tooltip("Tutorial, first wave: shown when the first enemy is defeated.")]
    [TextArea] public string firstKillMessage = "Each defeated enemy gives you gold. Spend it on more towers!";
    [Tooltip("Shown once per game, the first time the castle loses health.")]
    [TextArea] public string castleHitMessage = "The castle is hit! If its health reaches 0, you lose.";

    [Header("Infinite Mode")]
    public bool infinite;
    int nWavesWon = 0;

    public List<Wave> waves = DefaultWaves();

    enum State { Countdown, Running, Won, Lost }
    State state = State.Countdown;
    int waveIndex;
    float countdown;
    float arcCentre;
    int groupsRunning;
    int toSpawn;
    float prevWaveAngle = 0f;

    // HUD (top centre): wave title + countdown, and the "Start now" button.
    VisualElement hudBox;
    Label hudLabel;
    Button startNowButton;

    // Wave track (right side): one segment per wave, filled when that wave starts; the last one is the boss.
    VisualElement waveTrack;
    Label waveTrackCount;
    readonly List<VisualElement> waveSegments = new List<VisualElement>();
    int wavesStarted;

    // Speech bubble above the castle.
    VisualElement bubble;
    Label bubbleText;
    Button bubbleClose;
    string pendingMessage;
    float bubbleHideAt = -1f, bubbleShownAt;

    // Tutorial
    TutorialPaper paper;
    int hintedCard = -1;
    bool startNowHinted, firstKillHinted, castleHitHinted;
    float lastCastleHP;

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

        // Tutorial helpers are added here so the scene needs no extra set-up.
        paper = GetComponent<TutorialPaper>();
        if (!paper) paper = gameObject.AddComponent<TutorialPaper>();
        if (!GetComponent<GoldPopups>()) gameObject.AddComponent<GoldPopups>();
        if (mouse) mouse.TowerPlaced += OnTowerPlaced;
        EnemyHealth.AnyDied += OnEnemyDied;
    }

    void OnDestroy()
    {
        if (mouse) mouse.TowerPlaced -= OnTowerPlaced;
        EnemyHealth.AnyDied -= OnEnemyDied;
    }

    void Start()
    {
        if (castle) lastCastleHP = castle.GetHP();
        waveIndex = 0;
        BeginCountdown(firstCountdown);
        GameAudio.SetMusic(MusicFor(0));
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
            StopHints();
            return;
        }

        // The first time the castle loses health, show it: its bar blinks and the rule is said once.
        if (castle)
        {
            float hp = castle.GetHP();
            if (!castleHitHinted && hp < lastCastleHP - .001f)
            {
                castleHitHinted = true;
                HealthBars.FlashBase(2.5f);
                Say(castleHitMessage);
            }
            lastCastleHP = hp;
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
        StopHints();
        Wave wave = waveIndex < waves.Count ? waves[waveIndex] : null;
        if (wave != null && paper && !string.IsNullOrWhiteSpace(wave.paperTitle))
        {
            // The game is paused while the paper is open; the countdown only starts once it is closed.
            string text = (wave.paperText ?? "").Replace("{waves}", waves.Count.ToString());
            paper.Show(wave.paperStep, wave.paperTitle, text, wave.paperIcon, () => AfterPaper(wave));
        }
        else
        {
            AfterPaper(wave);
        }
    }

    void AfterPaper(Wave wave)
    {
        if (wave == null || state != State.Countdown) return;
        Say(wave.prepMessage);
        if (wave.highlightCard >= 0 && hud)
        {
            hintedCard = wave.highlightCard;
            hud.SetCardHint(hintedCard, true);
        }
    }

    void OnTowerPlaced(GameObject prefab)
    {
        // The card the tutorial asked for stops blinking once that tower is built.
        if (hintedCard >= 0 && hud && hud.CardPrefab(hintedCard) == prefab)
        {
            hud.SetCardHint(hintedCard, false);
            hintedCard = -1;
        }
        // First tutorial wave: after the first tower, point at "Start now".
        if (waveIndex == 0 && state == State.Countdown && !startNowHinted)
        {
            startNowHinted = true;
            UIPulse.Start(startNowButton);
            Say(firstTowerMessage);
        }
    }

    void OnEnemyDied(EnemyHealth enemy)
    {
        if (waveIndex == 0 && !firstKillHinted && enemy && enemy.killReward > 0)
        {
            firstKillHinted = true;
            Say(firstKillMessage);
        }
    }

    void StopHints()
    {
        UIPulse.Stop(startNowButton);
        if (hud) hud.ClearCardHints();
        hintedCard = -1;
    }

    // Also called by the "Start now" button.
    public void StartWave()
    {
        if (state != State.Countdown || waveIndex >= waves.Count) return;
        // A click that reaches "Start now" through an open tutorial paper must not start the wave behind it.
        if (TutorialPaper.IsOpen) return;
        var wave = waves[waveIndex];
        state = State.Running;
        arcCentre = UnityEngine.Random.Range(wave.attackAngleDegrees.x, wave.attackAngleDegrees.y);

        if (wave.relativeToPrevWave)
        {
            arcCentre += prevWaveAngle;
        }
        prevWaveAngle = arcCentre;

        groupsRunning = 0;
        toSpawn = 0;
        foreach (var group in wave.groups)
        {
            if (group == null || group.count <= 0) continue;
            groupsRunning++;
            toSpawn += group.count;
            StartCoroutine(SpawnGroupRoutine(group, wave.arcDegrees));
        }
        UIPulse.Stop(startNowButton);
        wavesStarted = infinite ? nWavesWon + 1 : waveIndex + 1;
        UpdateWaveMeter();
        Say(wave.startMessage);
        GameAudio.Play("jingle_wave");
        GameAudio.SetMusic(MusicFor(waveIndex));
        if (MusicFor(waveIndex) == GameAudio.Music.Boss) StartCoroutine(BossIntro(wave));
    }

    // The boss wave has its own intro piece, played once from start to end. It is timed so that the boss
    // walks in exactly bossEntranceInIntro seconds into the piece.
    IEnumerator BossIntro(Wave wave)
    {
        float bossAt = -1f;
        foreach (var group in wave.groups)
            if (group != null && group.enemy == EnemyKind.Boss && group.count > 0) { bossAt = group.startDelay; break; }
        float wait = bossAt >= 0f ? Mathf.Max(0f, bossAt - bossEntranceInIntro) : 0f;
        if (wait > 0f) yield return new WaitForSeconds(wait);
        GameAudio.PlayStinger("stinger_boss");
    }

    // Calm music while the tutorial teaches, battle music for the real waves, the boss theme for the last one:
    // the music rises with the tension of the match.
    GameAudio.Music MusicFor(int index)
    {
        if (index < 0 || index >= waves.Count) return GameAudio.Music.Battle;
        string title = waves[index].title ?? "";
        if (title.IndexOf("tutorial", StringComparison.OrdinalIgnoreCase) >= 0) return GameAudio.Music.Calm;
        if (index == waves.Count - 1 || title.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0) return GameAudio.Music.Boss;
        return GameAudio.Music.Battle;
    }

    void WaveCleared()
    {
        if (infinite)
        {
            nWavesWon++;
            waveIndex = (waveIndex + 1) % waves.Count;
            waves[waveIndex] = NextWave(waves[waveIndex]);

            //Could also add an increasing function to append new waves

            //Implement cost increasing on towers
        } else
        {
            waveIndex++;
        }
        UpdateWaveMeter();
        if (waveIndex >= waves.Count)
        {
            state = State.Won;
            StopHints();
            Say(victoryMessage);
            RefreshHud();
            if (gameEnd) gameEnd.CueWin();
            return;
        }
        BeginCountdown(countdownBetweenWaves);
    }

    Wave NextWave(Wave curSetup)
    {
        curSetup.title = $"Wave {nWavesWon+1}";

        //Have some function to increase:
        //arcDegree - start from 30, slowly ramp to 360
        //spawnGroup - Each SpawnGroup has (EnemyType, count, interval between each spawn, start-delay)

        return curSetup;
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
        EnemySpawn.AtRing(PrefabFor(kind), castle ? castle.transform.position : Vector3.zero, arcCentre, arc,
            spawnDistance, spawnJitter, kind == EnemyKind.Boss, this);
    }

    GameObject PrefabFor(EnemyKind kind) => kind switch
    {
        EnemyKind.Fast => fastEnemy,
        EnemyKind.Heavy => heavyEnemy,
        EnemyKind.Boss => bossEnemy,
        _ => normalEnemy,
    };

    // Fills one segment per wave that has started: empty before the first wave, full when the boss wave starts.
    void UpdateWaveMeter()
    {
        if (waveSegments.Count == 0) return;
        int total = waveSegments.Count;
        int started = Mathf.Clamp(wavesStarted, 0, total);
        for (int i = 0; i < total; i++)
        {
            waveSegments[i].EnableInClassList("filled", i < started);
            waveSegments[i].EnableInClassList("current", i == started - 1 && state == State.Running);
        }
        if (waveTrackCount != null) waveTrackCount.text = $"{started} / {total}";
    }

    void BuildWaveTrack(VisualElement root)
    {
        waveTrack?.RemoveFromHierarchy();
        waveSegments.Clear();
        // The old rotated "Enemy Forces" bar from UIMenu.uxml is replaced by this track.
        var oldMeter = root.Q<VisualElement>("WaveHUD");
        if (oldMeter != null) oldMeter.style.display = DisplayStyle.None;

        // Look: RoyalHud.uss (#WaveTrack, .wave-seg ...).
        waveTrack = new VisualElement { name = "WaveTrack", pickingMode = PickingMode.Ignore };
        var title = new Label("Waves") { pickingMode = PickingMode.Ignore };
        title.AddToClassList("wave-track-title");
        waveTrackCount = new Label { pickingMode = PickingMode.Ignore };
        waveTrackCount.AddToClassList("wave-track-count");
        var column = new VisualElement { pickingMode = PickingMode.Ignore };
        column.AddToClassList("wave-track-column");
        for (int i = 0; i < waves.Count; i++)
        {
            bool boss = i == waves.Count - 1;
            var seg = new VisualElement { pickingMode = PickingMode.Ignore };
            seg.AddToClassList("wave-seg");
            if (boss) seg.AddToClassList("boss");
            var label = new Label(boss ? "Boss" : (i + 1).ToString()) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("wave-seg-label");
            seg.Add(label);
            column.Add(seg);   // column-reverse: wave 1 sits at the bottom, the boss on top
            waveSegments.Add(seg);
        }
        waveTrack.Add(title);
        waveTrack.Add(waveTrackCount);
        waveTrack.Add(column);
        root.Add(waveTrack);
        UpdateWaveMeter();
    }

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

        startNowButton = new Button(() => { GameAudio.Play("ui_click"); StartWave(); }) { name = "GameFlowStartNow", text = "Start now" };
        var b = startNowButton.style;
        b.fontSize = 40;
        b.paddingLeft = 24; b.paddingRight = 24; b.paddingTop = 6; b.paddingBottom = 6;

        hudBox.Add(hudLabel);
        hudBox.Add(startNowButton);
        root.Add(hudBox);
        BuildWaveTrack(root);
        if (startNowHinted && state == State.Countdown && waveIndex == 0) UIPulse.Start(startNowButton);
        RefreshHud();
    }

    void RefreshHud()
    {
        if (hudLabel == null || hudLabel.panel == null) return;
        string title = waveIndex < waves.Count ? waves[waveIndex].title : "";
        switch (state)
        {
            case State.Countdown:
                // The last wave is the boss wave: its small enemies come first, so the line talks about the battle, not the boss.
                string coming = waveIndex == waves.Count - 1 ? "The final battle begins" : "Enemies arrive";
                hudLabel.text = $"{title} - {coming} in {Mathf.CeilToInt(Mathf.Max(0f, countdown))}s";
                startNowButton.style.display = DisplayStyle.Flex;
                break;
            case State.Running:
                hudLabel.text = $"{title} - Enemies left: {toSpawn + EnemyHealth.Active.Count}";
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
            prepMessage = "Pick the blinking Arrow Tower card, then click on the ground near the castle.",
            startMessage = "Here they come! Your towers shoot by themselves.",
            paperStep = "Tutorial 1 of 3", paperTitle = "Defend the Castle!", paperIcon = TutorialPaper.Icon.Arrow, highlightCard = 0,
            paperText =
                "Enemies are marching on your castle. If its health drops to <b>0</b>, you lose.\n" +
                "Survive all <b>{waves}</b> waves to win. The first 3 waves teach you how to play.\n" +
                "To build, click a tower card at the bottom, then click on empty ground. <b>Right-click</b> cancels.\n" +
                "Towers cost gold. Your gold is shown at the top left.\n" +
                "Start with an <b>Arrow Tower</b>: it is cheap and fires fast at normal soldiers.",
            groups = { new SpawnGroup(EnemyKind.Normal, 4, 3f) },
        },
        new Wave
        {
            title = "Tutorial 2/3: Cannon Tower", arcDegrees = 90f,
            prepMessage = "Build a Cannon Tower before the Heavies arrive!",
            startMessage = "Heavies are slow but tough. Let the cannons do the work!",
            paperStep = "Tutorial 2 of 3", paperTitle = "Heavy Soldiers", paperIcon = TutorialPaper.Icon.Cannon, highlightCard = 1,
            paperText =
                "<b>Heavy</b> soldiers are slow, but tough enough to shrug off arrows.\n" +
                "The <b>Cannon Tower</b> fires slowly, but every shot hits very hard.\n" +
                "Build one where the Heavies will walk. Tougher enemies also give more gold.",
            groups = { new SpawnGroup(EnemyKind.Heavy, 3, 5f), new SpawnGroup(EnemyKind.Normal, 2, 3f, 2f) },
        },
        new Wave
        {
            title = "Tutorial 3/3: Magic Tower", arcDegrees = 30f,
            prepMessage = "Build a Magic Tower to catch the fast raiders!",
            startMessage = "Here they come, fast and all together!",
            paperStep = "Tutorial 3 of 3", paperTitle = "Fast Raiders", paperIcon = TutorialPaper.Icon.Magic, highlightCard = 2,
            paperText =
                "<b>Fast</b> raiders rush in as a pack. Each one is weak, but they are hard to stop one by one.\n" +
                "The <b>Magic Tower</b>'s lightning jumps between enemies standing close together: it can hit up to <b>3</b> at once.\n" +
                "Every enemy it hits is also <b>slowed down</b>, giving your other towers more time to shoot.\n" +
                "This is the last tutorial wave. After it, enemies come from more directions, so mix all three towers.",
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
            startMessage = "Their soldiers come first, and the leader is right behind them!",
            groups =
            {
                // All the small enemies first (the last one appears at 12.5 s) ...
                new SpawnGroup(EnemyKind.Normal, 6, 2.5f),
                new SpawnGroup(EnemyKind.Heavy, 1, 1f, 3f),
                new SpawnGroup(EnemyKind.Fast, 9, .6f, 4f),
                // ... then the boss, 3 seconds after the last of them.
                new SpawnGroup(EnemyKind.Boss, 1, 1f, 15.5f),
            },
        },
    };
}
