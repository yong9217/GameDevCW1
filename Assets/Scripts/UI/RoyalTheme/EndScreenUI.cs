using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Victory / defeat screen (RoyalEndScreen.uxml). GameEnd calls Show().
// The victory screen also lists the credits.
// In Infinite mode the defeat screen shows how long the castle held and the local top-5 leaderboard:
// a time good enough for the top 5 first asks for a name, with a "Don't record" choice to keep it off the board.
[RequireComponent(typeof(PanelRenderer))]
public class EndScreenUI : MonoBehaviour
{
    public enum Kind { Victory, Defeat }
    public Kind kind = Kind.Victory;

    public string title = "Victory";
    [TextArea] public string message = "The castle stands. The kingdom is safe.";
    public string playButtonText = "Play again";
    public bool showCredits = true;
    public string[] creditNames = { "Yong You Shen", "Xia Jin Qing" };

    VisualElement overlay;
    bool showWhenReady;

    // Infinite mode result
    Label messageLabel;
    VisualElement infiniteBox, entryBox, rows;
    TextField nameField;
    Button playButton, menuButton;
    bool infiniteShown, recordDecided;

    void Awake()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(Init);
    }

    void Init(PanelRenderer panel, VisualElement root, int version)
    {
        overlay = root.Q<VisualElement>("overlay");
        GameAudio.AddButtonClicks(root);
        if (overlay == null)
        {
            Debug.LogError("EndScreenUI: the panel needs RoyalEndScreen.uxml.", this);
            return;
        }

        var titleLabel = root.Q<Label>("title");
        titleLabel.text = title;
        titleLabel.EnableInClassList("royal-title--defeat", kind == Kind.Defeat);
        messageLabel = root.Q<Label>("message");
        messageLabel.text = message;

        var credits = root.Q<VisualElement>("credits");
        credits.style.display = showCredits ? DisplayStyle.Flex : DisplayStyle.None;
        var names = root.Q<VisualElement>("names");
        names.Clear();
        foreach (var n in creditNames)
        {
            var label = new Label(n);
            label.AddToClassList("royal-credits-name");
            names.Add(label);
        }

        playButton = root.Q<Button>("play");
        playButton.text = playButtonText;
        playButton.clicked += () =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        };
        menuButton = root.Q<Button>("menu");
        menuButton.clicked += () =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        };

        infiniteBox = root.Q<VisualElement>("infinite");
        entryBox = root.Q<VisualElement>("lbEntry");
        rows = root.Q<VisualElement>("lbRows");
        nameField = root.Q<TextField>("lbName");
        var save = root.Q<Button>("lbSave");
        var skip = root.Q<Button>("lbSkip");
        if (save != null) save.clicked += SaveRecord;
        if (skip != null) skip.clicked += SkipRecord;
        if (nameField != null)
        {
            nameField.maxLength = InfiniteLeaderboard.MaxNameLength;
            nameField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) SaveRecord();
            }, TrickleDown.TrickleDown);
        }
        if (infiniteBox != null) infiniteBox.style.display = DisplayStyle.None;

        if (showWhenReady)
        {
            if (IsInfiniteDefeat) SetUpInfinite();
            RoyalFade.Show(overlay);
        }
        else RoyalFade.HideNow(overlay);
    }

    public void Show()
    {
        if (overlay == null) { showWhenReady = true; return; }
        if (IsInfiniteDefeat) SetUpInfinite();
        RoyalFade.Show(overlay);
    }

    // ---------------- Infinite mode ----------------

    bool IsInfiniteDefeat => kind == Kind.Defeat && InfiniteFlow.Current;

    float RunSeconds => InfiniteFlow.Current ? InfiniteFlow.Current.SurvivedSeconds : 0f;

    void SetUpInfinite()
    {
        if (infiniteShown || infiniteBox == null) return;
        infiniteShown = true;
        // The camera moves with W/A/S/D: keep it still while a name is typed.
        var cam = FindAnyObjectByType<RTSCameraController>();
        if (cam) cam.enabled = false;

        if (messageLabel != null) messageLabel.text = "The castle held for " + InfiniteFlow.FormatTime(RunSeconds) + ".";
        infiniteBox.style.display = DisplayStyle.Flex;

        if (InfiniteLeaderboard.Qualifies(RunSeconds))
        {
            // Decide first (save or don't record), then the usual buttons appear.
            entryBox.style.display = DisplayStyle.Flex;
            SetEndButtons(false);
            ShowBoard(-1);
            if (nameField != null)
            {
                nameField.value = "";
                nameField.schedule.Execute(() => nameField.Focus()).StartingIn(300);
            }
        }
        else
        {
            recordDecided = true;
            entryBox.style.display = DisplayStyle.None;
            ShowBoard(-1);
        }
    }

    void SaveRecord()
    {
        if (recordDecided) return;
        recordDecided = true;
        int place = InfiniteLeaderboard.Add(nameField != null ? nameField.value : "", RunSeconds);
        entryBox.style.display = DisplayStyle.None;
        SetEndButtons(true);
        ShowBoard(place);
    }

    void SkipRecord()
    {
        if (recordDecided) return;
        recordDecided = true;
        entryBox.style.display = DisplayStyle.None;
        SetEndButtons(true);
        ShowBoard(-1);
    }

    void SetEndButtons(bool shown)
    {
        var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        if (playButton != null) playButton.style.display = display;
        if (menuButton != null) menuButton.style.display = display;
    }

    // highlight: place of the time just saved (0 = first), or -1.
    void ShowBoard(int highlight)
    {
        if (rows == null) return;
        rows.Clear();
        var entries = InfiniteLeaderboard.Load();
        if (entries.Count == 0)
        {
            var empty = new Label("No records yet.");
            empty.AddToClassList("lb-empty");
            rows.Add(empty);
            return;
        }
        for (int i = 0; i < entries.Count; i++)
        {
            var row = new VisualElement();
            row.AddToClassList("lb-row");
            row.EnableInClassList("lb-row--new", i == highlight);
            row.Add(MakeLabel((i + 1) + ".", "lb-rank"));
            row.Add(MakeLabel(entries[i].name, "lb-player"));
            row.Add(MakeLabel(InfiniteFlow.FormatTime(entries[i].seconds), "lb-time"));
            rows.Add(row);
        }
    }

    static Label MakeLabel(string text, string cls)
    {
        var label = new Label(text) { enableRichText = false };
        label.AddToClassList(cls);
        return label;
    }
}
