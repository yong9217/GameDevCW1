using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// The big parchment sheet of the tutorial (markup in RoyalPause.uxml, styles in RoyalTheme.uss).
// While it is open the whole game is paused: time stops, nothing can be built and Esc does nothing.
// Only its button (or Enter / Space) closes it; then the game carries on from where it stopped.
// GameFlow adds this component at runtime, so no scene set-up is needed.
public class TutorialPaper : MonoBehaviour
{
    public enum Icon { None, Arrow, Cannon, Magic }

    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsOpen = false;

    [Tooltip("The pause menu panel: the paper is part of the same UXML (RoyalPause.uxml).")]
    public PanelRenderer panel;
    public MouseStateHandler mouse;
    public PauseAPI pauseMenu;
    [Tooltip("Seconds (real time) before Enter / Space can close a paper, so a key that is already held down does not skip it.")]
    [Min(0f)] public float keyDelay = .5f;

    VisualElement overlay, lines, icon;
    Label step, title;
    Button close;

    // What is on the paper. Kept here so it can be shown before the UI has loaded, and again after a UI reload.
    string stepText, titleText;
    string[] bodyLines = new string[0];
    Icon iconKind;
    Action onClosed;

    float resumeTimeScale = 1f;
    bool mouseWasActive = true;
    float openedAt;

    void Awake()
    {
        if (!pauseMenu) pauseMenu = FindAnyObjectByType<PauseAPI>();
        if (!panel && pauseMenu) panel = pauseMenu.GetComponent<PanelRenderer>();
        if (!mouse) mouse = FindAnyObjectByType<MouseStateHandler>();
        if (panel) panel.RegisterUIReloadCallback(Hook);
    }

    void OnDestroy()
    {
        // Leaving the scene with a paper open must not leave the next scene thinking a paper is open.
        IsOpen = false;
    }

    void Hook(PanelRenderer p, VisualElement root, int version)
    {
        overlay = root.Q<VisualElement>("TutorialPaper");
        step = root.Q<Label>("PaperStep");
        title = root.Q<Label>("PaperTitle");
        lines = root.Q<VisualElement>("PaperLines");
        icon = root.Q<VisualElement>("PaperIcon");
        close = root.Q<Button>("PaperClose");
        if (overlay == null)
        {
            Debug.LogError("TutorialPaper: RoyalPause.uxml has no 'TutorialPaper' element.", this);
            if (IsOpen) Close();   // never leave the game frozen behind a paper that cannot be shown
            return;
        }
        if (close != null) close.clicked += Close;

        if (IsOpen) Fill();
        else RoyalFade.HideNow(overlay);
    }

    // Opens a paper and pauses the game. 'closed' runs right after the player closes it (time is running again by then).
    // Lines of 'body' are separated by '\n'; each one becomes a bullet point. Rich text such as <b>...</b> works.
    public void Show(string stepLine, string heading, string body, Icon picture, Action closed = null)
    {
        // Without its UI the paper could never be closed: skip it instead of freezing the game.
        if (!panel)
        {
            closed?.Invoke();
            return;
        }

        stepText = stepLine;
        titleText = heading;
        bodyLines = string.IsNullOrEmpty(body) ? new string[0] : body.Replace("\r", "").Split('\n');
        iconKind = picture;
        onClosed = IsOpen ? onClosed + closed : closed;

        if (!IsOpen)
        {
            IsOpen = true;
            resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (mouse)
            {
                mouseWasActive = mouse.active;
                mouse.active = false;
            }
            GameAudio.SetPaused(true);
        }
        openedAt = Time.realtimeSinceStartup;
        Fill();
    }

    void Fill()
    {
        // Not loaded yet: Hook shows the paper as soon as the UI is ready.
        if (overlay == null || overlay.panel == null) return;

        if (step != null)
        {
            step.text = stepText ?? "";
            step.style.display = string.IsNullOrEmpty(stepText) ? DisplayStyle.None : DisplayStyle.Flex;
        }
        if (title != null) title.text = titleText ?? "";

        if (lines != null)
        {
            lines.Clear();
            foreach (string raw in bodyLines)
            {
                string text = raw.Trim();
                if (text.Length == 0) continue;
                var row = new VisualElement();
                row.AddToClassList("paper-line-row");
                var bullet = new VisualElement();
                bullet.AddToClassList("paper-bullet");
                var label = new Label(text);
                label.AddToClassList("paper-line");
                row.Add(bullet);
                row.Add(label);
                lines.Add(row);
            }
        }

        if (icon != null)
        {
            foreach (Icon kind in Enum.GetValues(typeof(Icon))) icon.RemoveFromClassList(IconClass(kind));
            icon.AddToClassList(IconClass(iconKind));
        }

        overlay.BringToFront();
        RoyalFade.Show(overlay);
    }

    static string IconClass(Icon kind) => "paper-icon--" + kind.ToString().ToLowerInvariant();

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        RoyalFade.Hide(overlay);

        // Normally the game carries on. If the pause menu is also open, it stays paused: Resume restarts it.
        if (!(pauseMenu && pauseMenu.IsPaused))
        {
            Time.timeScale = resumeTimeScale;
            if (mouse && !mouse.locked) mouse.active = mouseWasActive;
            GameAudio.SetPaused(false);
        }

        var then = onClosed;
        onClosed = null;
        then?.Invoke();
    }

    // Time.timeScale is 0 while the paper is open, but Update still runs every frame.
    void Update()
    {
        if (!IsOpen || overlay == null || overlay.panel == null) return;
        if (Time.realtimeSinceStartup - openedAt < keyDelay) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
        {
            Close();
            GameAudio.Play("ui_click");
        }
    }
}
