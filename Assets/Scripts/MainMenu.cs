using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenu : MonoBehaviour
{
    PanelRenderer pr;
    public PanelRenderer bestiary;
    public PanelRenderer credits;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(intialize);
    }

    void LoadGame()
    {
        SceneManager.LoadScene(1);
    }

    // Infinite mode has its own scene and map.
    void LoadInfinite()
    {
        SceneManager.LoadScene("Infinite");
    }

    void intialize(PanelRenderer p, VisualElement root, int ver)
    {
        GameAudio.AddButtonClicks(root);
        root.Q<Button>("start").clicked += () =>
        {
            LoadGame();
        };
        infiniteButton = root.Q<Button>("infinite");
        if (infiniteButton != null) infiniteButton.clicked += () =>
        {
            if (ModeUnlocks.InfiniteUnlocked) LoadInfinite();
            else if (lockedOverlay != null) { lockedOverlay.BringToFront(); RoyalFade.Show(lockedOverlay); }
        };
        root.Q<Button>("exit").clicked += () =>
        {
            Debug.Log("Exited");
            Application.Quit();
        };
        root.Q<Button>("bestiary").clicked += () =>
        {
            bestiary.gameObject.SetActive(true);
        };
        root.Q<Button>("credits").clicked += () =>
        {
            credits.gameObject.SetActive(true);
        };

        // Infinite mode top 5 of this computer (read-only here; times are added on the Infinite defeat screen).
        leaderboardOverlay = root.Q<VisualElement>("leaderboardOverlay");
        leaderboardRows = root.Q<VisualElement>("leaderboardRows");
        RoyalFade.HideNow(leaderboardOverlay);
        var leaderboard = root.Q<Button>("leaderboard");
        if (leaderboard != null) leaderboard.clicked += ShowLeaderboard;
        var close = root.Q<Button>("leaderboardClose");
        if (close != null) close.clicked += () => RoyalFade.Hide(leaderboardOverlay);

        // Infinite Mode is locked until the Campaign has been won once on this computer.
        lockedOverlay = root.Q<VisualElement>("lockedOverlay");
        unlockOverlay = root.Q<VisualElement>("unlockOverlay");
        if (lockedOverlay != null) RoyalFade.HideNow(lockedOverlay);
        if (unlockOverlay != null) RoyalFade.HideNow(unlockOverlay);
        var lockedOk = root.Q<Button>("lockedOk");
        if (lockedOk != null) lockedOk.clicked += () => RoyalFade.Hide(lockedOverlay);
        var unlockOk = root.Q<Button>("unlockOk");
        if (unlockOk != null) unlockOk.clicked += () => RoyalFade.Hide(unlockOverlay);
        RefreshInfiniteLock();

        // First visit to the menu after winning the Campaign (also after a restart of the game): congratulate once.
        if (ModeUnlocks.CelebrationPending && unlockOverlay != null)
        {
            ModeUnlocks.MarkCelebrated();
            unlockOverlay.BringToFront();
            RoyalFade.Show(unlockOverlay);
        }
    }

    VisualElement lockedOverlay, unlockOverlay;
    Button infiniteButton;

    void RefreshInfiniteLock()
    {
        if (infiniteButton == null) return;
        bool unlocked = ModeUnlocks.InfiniteUnlocked;
        infiniteButton.text = unlocked ? "Infinite Mode" : "Infinite Mode (Locked)";
        infiniteButton.EnableInClassList("royal-button--primary", unlocked);
        infiniteButton.EnableInClassList("royal-button--locked", !unlocked);
    }

    // Hidden shortcut for demos on a new computer: hold Shift and press B, L, G within 2 seconds.
    static readonly Key[] secretKeys = { Key.B, Key.L, Key.G };
    int secretStep;
    float secretStarted;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || ModeUnlocks.InfiniteUnlocked) return;
        if (!kb.shiftKey.isPressed || (secretStep > 0 && Time.unscaledTime - secretStarted > 2f))
        {
            secretStep = 0;
            if (!kb.shiftKey.isPressed) return;
        }
        if (kb[secretKeys[secretStep]].wasPressedThisFrame)
        {
            if (secretStep == 0) secretStarted = Time.unscaledTime;
            if (++secretStep < secretKeys.Length) return;
            secretStep = 0;
            ModeUnlocks.UnlockSilently();
            RefreshInfiniteLock();
            GameAudio.Play("ui_click");
        }
        else if (kb.anyKey.wasPressedThisFrame && !kb.leftShiftKey.wasPressedThisFrame && !kb.rightShiftKey.wasPressedThisFrame)
        {
            secretStep = 0;
        }
    }

    VisualElement leaderboardOverlay, leaderboardRows;

    void ShowLeaderboard()
    {
        if (leaderboardOverlay == null || leaderboardRows == null) return;
        leaderboardRows.Clear();
        var entries = InfiniteLeaderboard.Load();
        if (entries.Count == 0)
        {
            leaderboardRows.Add(MakeLabel("No records yet.", "lb-empty"));
        }
        for (int i = 0; i < entries.Count; i++)
        {
            var row = new VisualElement();
            row.AddToClassList("lb-row");
            row.Add(MakeLabel((i + 1) + ".", "lb-rank"));
            row.Add(MakeLabel(entries[i].name, "lb-player"));
            row.Add(MakeLabel(InfiniteFlow.FormatTime(entries[i].seconds), "lb-time"));
            leaderboardRows.Add(row);
        }
        leaderboardOverlay.BringToFront();
        RoyalFade.Show(leaderboardOverlay);
    }

    static Label MakeLabel(string text, string cls)
    {
        // Player names are shown as typed, never as rich text.
        var label = new Label(text) { enableRichText = false };
        label.AddToClassList(cls);
        return label;
    }
}
