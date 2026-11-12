using UnityEngine;
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
        var infinite = root.Q<Button>("infinite");
        if (infinite != null) infinite.clicked += LoadInfinite;
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
