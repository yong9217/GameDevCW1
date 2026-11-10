using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// All music and sound effects of the game, in one place.
//
// Clips are found by FILE NAME in Assets/Resources/Audio (any sub-folder):
//   music_menu, music_calm, music_battle, music_boss           (looping background music)
//   jingle_wave, jingle_victory, jingle_defeat                 (short music cues)
//   stinger_boss                                               (played once, in place of the music)
//   ui_click, ui_deny, build, shot_arrow, shot_cannon, shot_magic, hit_arrow, hit_cannon,
//   enemy_death, coin, melee_hit, tower_destroyed, boss_arrive, boss_hit
// A missing clip is simply silent, so the game runs with any subset of these files.
// Variants of one sound can be added as shot_arrow_1, shot_arrow_2, ...: one is picked at random.
// Missing music falls back: music_boss -> music_battle -> music_calm -> music_menu, so there is always music.
//
// Volumes are tuned on Assets/Resources/GameAudio.prefab. The object is created automatically
// before the first scene loads and survives scene changes, so no scene needs to be edited.
public sealed class GameAudio : MonoBehaviour
{
    public enum Music { None, Menu, Calm, Battle, Boss }

    [Header("Volume")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = .45f;
    [Range(0f, 1f)] public float effectsVolume = .8f;
    [Tooltip("Music level while the pause menu is open (multiplies the music volume).")]
    [Range(0f, 1f)] public float pausedMusicLevel = .35f;

    [Header("Behaviour")]
    [Tooltip("Seconds for one music track to fade into the next.")]
    [Min(0f)] public float musicFadeSeconds = 1.5f;
    [Tooltip("Random pitch change per effect, so repeated sounds are less mechanical.")]
    [Range(0f, .3f)] public float pitchVariation = .06f;
    [Tooltip("Most effects that may play at the same time; extra ones are skipped.")]
    [Range(4, 32)] public int maxVoices = 16;

    // Tower attack sounds (shot_*, hit_*) sit at 70% of their first level so they never cover the music.
    // Per sound: volume, and the shortest time between two plays of it (many towers firing
    // together would otherwise turn into a wall of noise).
    static readonly Dictionary<string, (float volume, float minGap)> Tuning = new Dictionary<string, (float, float)>
    {
        ["ui_click"] = (.8f, .03f),
        ["ui_deny"] = (.7f, .15f),
        ["build"] = (.9f, .05f),
        ["shot_arrow"] = (.25f, .1f),
        ["shot_cannon"] = (.49f, .1f),
        ["shot_magic"] = (.38f, .1f),
        ["hit_arrow"] = (.28f, .06f),
        ["hit_cannon"] = (.56f, .1f),
        ["enemy_death"] = (.6f, .06f),
        ["coin"] = (.45f, .05f),
        ["melee_hit"] = (.45f, .1f),
        ["tower_destroyed"] = (.9f, .1f),
        ["boss_arrive"] = (1f, .5f),
        ["boss_hit"] = (1f, .1f),
        ["jingle_wave"] = (.8f, 1f),
        ["jingle_victory"] = (.6f, 1f),
        ["jingle_defeat"] = (.6f, 1f),
    };

    // Sounds that must sound exactly the same every time (no random pitch).
    static readonly HashSet<string> FixedPitch = new HashSet<string> { "build", "shot_arrow", "hit_arrow", "shot_magic", "boss_hit" };

    static readonly string[] MusicNames = { null, "music_menu", "music_calm", "music_battle", "music_boss" };

    static GameAudio instance;
    static bool quitting;
    static readonly HashSet<VisualElement> hookedRoots = new HashSet<VisualElement>();

    readonly Dictionary<string, List<AudioClip>> clips = new Dictionary<string, List<AudioClip>>();
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    readonly List<AudioSource> voices = new List<AudioSource>();
    AudioSource music, fadingMusic, stinger;
    Coroutine fade;
    bool paused;
    // Tower and enemy sounds stay silent in the main menu, where the bestiary shows units in action.
    bool combatSounds = true;

    // ---------------- Static entry points (safe to call from anywhere) ----------------

    // Plays a menu / interface / game-flow sound.
    public static void Play(string sound)
    {
        if (Ready()) instance.PlayClip(sound);
    }

    // Plays a battle sound (towers, enemies). Silent outside the game scene.
    public static void PlayCombat(string sound)
    {
        if (Ready() && instance.combatSounds) instance.PlayClip(sound);
    }

    public static void SetMusic(Music track)
    {
        if (Ready()) instance.SwitchMusic(track);
    }

    // Lowers the music while the pause menu is open.
    public static void SetPaused(bool isPaused)
    {
        if (!Ready()) return;
        instance.paused = isPaused;
        // The boss intro is timed against the game clock: hold it while the game is paused.
        if (isPaused) instance.stinger.Pause();
        else instance.stinger.UnPause();
    }

    // Plays a piece once from start to end on the music channel: the background music fades out for it
    // and comes back afterwards (used for the boss wave intro).
    public static void PlayStinger(string sound)
    {
        if (Ready()) instance.StartStinger(sound);
    }

    // Victory or defeat: the music fades out and the matching jingle plays (also while time is frozen).
    public static void EndGame(bool won)
    {
        if (!Ready()) return;
        instance.stinger.Stop();
        instance.stingerActive = false;
        // Fade the background music out quickly, so it does not play on top of the (short) end theme.
        instance.SwitchMusic(Music.None, .4f);
        instance.PlayClip(won ? "jingle_victory" : "jingle_defeat");
    }

    // Every button inside this UI root clicks when pressed. Safe to call again after a UI reload.
    public static void AddButtonClicks(VisualElement root)
    {
        if (root == null || !hookedRoots.Add(root)) return;
        root.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0 || !(e.target is VisualElement element)) return;
            if (element is Button || element.GetFirstAncestorOfType<Button>() != null) Play("ui_click");
        }, TrickleDown.TrickleDown);
    }

    // ---------------- Setup ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        quitting = false;
        hookedRoots.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() => Ready();

    static bool Ready()
    {
        if (instance) return true;
        if (quitting) return false;
        var prefab = Resources.Load<GameAudio>("GameAudio");
        var created = prefab ? Instantiate(prefab) : new GameObject().AddComponent<GameAudio>();
        created.name = "GameAudio";
        return instance;
    }

    void Awake()
    {
        if (instance && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var clip in Resources.LoadAll<AudioClip>("Audio"))
        {
            string key = BaseName(clip.name);
            if (!clips.TryGetValue(key, out var list)) clips[key] = list = new List<AudioClip>();
            list.Add(clip);
        }

        music = NewSource(true);
        fadingMusic = NewSource(true);
        stinger = NewSource(false);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    void OnApplicationQuit() => quitting = true;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        paused = false;
        combatSounds = scene.buildIndex != 0;
        // A long defeat / victory theme must not carry on into the next game or the menu.
        foreach (var voice in voices) voice.Stop();
        stinger.Stop();
        stingerActive = false;
        // The game scene picks its own music (GameFlow); the main menu always plays the menu theme.
        if (scene.buildIndex == 0) SwitchMusic(Music.Menu);
    }

    AudioSource NewSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D: the camera is far above the map, so every sound plays at full clarity
        return source;
    }

    static string BaseName(string name)
    {
        int cut = name.LastIndexOf('_');
        return cut > 0 && int.TryParse(name.Substring(cut + 1), out _) ? name.Substring(0, cut) : name;
    }

    // ---------------- Effects ----------------

    void PlayClip(string sound)
    {
        if (string.IsNullOrEmpty(sound) || !clips.TryGetValue(sound, out var list) || list.Count == 0) return;
        (float volume, float minGap) tune = Tuning.TryGetValue(sound, out var t) ? t : (1f, .05f);
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(sound, out float last) && now - last < tune.minGap) return;

        var source = FreeVoice();
        if (!source) return;
        lastPlayed[sound] = now;
        source.clip = list[Random.Range(0, list.Count)];
        source.volume = tune.volume * effectsVolume * masterVolume;
        source.pitch = sound.StartsWith("jingle_") || FixedPitch.Contains(sound) ? 1f : 1f + Random.Range(-pitchVariation, pitchVariation);
        source.Play();
    }

    AudioSource FreeVoice()
    {
        foreach (var voice in voices)
            if (!voice.isPlaying) return voice;
        if (voices.Count >= maxVoices) return null;
        var created = NewSource(false);
        voices.Add(created);
        return created;
    }

    // ---------------- Music ----------------

    float MusicLevel => musicVolume * masterVolume * (paused ? pausedMusicLevel : 1f);
    // True from the start of a stinger until it has finished (also while it is held by the pause menu).
    bool stingerActive;
    bool StingerPlaying => stingerActive;

    void StartStinger(string sound)
    {
        if (!clips.TryGetValue(sound, out var list) || list.Count == 0) return;
        stinger.clip = list[0];
        stinger.pitch = 1f;
        stinger.volume = 0f;
        stinger.Play();
        stingerActive = true;
    }

    AudioClip MusicClip(Music track)
    {
        for (var m = track; m != Music.None; m = Fallback(m))
            if (clips.TryGetValue(MusicNames[(int)m], out var list) && list.Count > 0) return list[0];
        return null;
    }

    static Music Fallback(Music track) => track switch
    {
        Music.Boss => Music.Battle,
        Music.Battle => Music.Calm,
        Music.Calm => Music.Menu,
        _ => Music.None,
    };

    void SwitchMusic(Music track, float fadeSeconds = -1f)
    {
        AudioClip clip = MusicClip(track);
        // Same piece already playing (e.g. a fallback shared by two tracks): keep it going without a restart.
        if (clip && music.clip == clip && music.isPlaying) return;

        (music, fadingMusic) = (fadingMusic, music);
        music.Stop();
        music.clip = clip;
        music.volume = 0f;
        if (clip) music.Play();
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Crossfade(fadeSeconds >= 0f ? fadeSeconds : musicFadeSeconds));
    }

    IEnumerator Crossfade(float seconds)
    {
        float oldStart = fadingMusic.volume;
        float duration = Mathf.Max(.01f, seconds);
        for (float t = 0f; t < 1f; )
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
            music.volume = StingerPlaying ? 0f : MusicLevel * t;
            fadingMusic.volume = oldStart * (1f - t);
            yield return null;
        }
        fadingMusic.Stop();
        fadingMusic.clip = null;
        fade = null;
    }

    void Update()
    {
        if (stingerActive)
        {
            if (stinger.isPlaying || paused)
                stinger.volume = Mathf.MoveTowards(stinger.volume, MusicLevel, Time.unscaledDeltaTime * 3f * Mathf.Max(.05f, MusicLevel));
            else
            {
                // The stinger has finished: bring the background music back from the start of its piece,
                // so it begins on a clean phrase instead of somewhere in the middle.
                stingerActive = false;
                if (music.clip) music.time = 0f;
            }
        }
        // Follows the volume sliders and smoothly lowers / restores the music around the pause menu,
        // and keeps the background music silent while a stinger plays (it fades back in afterwards).
        if (fade == null && music)
        {
            float target = music.clip && !StingerPlaying ? MusicLevel : 0f;
            float speed = StingerPlaying ? 2f : (target > music.volume ? .5f : 1.5f);
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * speed * Mathf.Max(.05f, musicVolume));
        }
    }
}
