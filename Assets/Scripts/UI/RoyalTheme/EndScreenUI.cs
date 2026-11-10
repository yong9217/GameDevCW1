using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Victory / defeat screen (RoyalEndScreen.uxml). GameEnd calls Show().
// The victory screen also lists the credits.
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

    void Awake()
    {
        GetComponent<PanelRenderer>().RegisterUIReloadCallback(Init);
    }

    void Init(PanelRenderer panel, VisualElement root, int version)
    {
        overlay = root.Q<VisualElement>("overlay");
        if (overlay == null)
        {
            Debug.LogError("EndScreenUI: the panel needs RoyalEndScreen.uxml.", this);
            return;
        }

        var titleLabel = root.Q<Label>("title");
        titleLabel.text = title;
        titleLabel.EnableInClassList("royal-title--defeat", kind == Kind.Defeat);
        root.Q<Label>("message").text = message;

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

        var play = root.Q<Button>("play");
        play.text = playButtonText;
        play.clicked += () =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        };
        root.Q<Button>("menu").clicked += () =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        };

        if (showWhenReady) RoyalFade.Show(overlay);
        else RoyalFade.HideNow(overlay);
    }

    public void Show()
    {
        if (overlay == null) { showWhenReady = true; return; }
        RoyalFade.Show(overlay);
    }
}
