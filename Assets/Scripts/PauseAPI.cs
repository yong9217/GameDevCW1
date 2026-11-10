using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Pause menu (RoyalPause.uxml). Esc (via UIResponder) and the Resume / x buttons toggle it.
// It fades in and out instead of dropping from the top of the screen.
public class PauseAPI : MonoBehaviour
{
    PanelRenderer pr;
    VisualElement overlay;
    bool paused;

    public MouseStateHandler msh;
    float resumeTimeScale = 1f;

    public bool IsPaused => paused;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(Init);
    }

    void Init(PanelRenderer p, VisualElement root, int ver)
    {
        overlay = root.Q<VisualElement>("overlay");
        root.Q<Button>("exit").clicked += TogglePause;
        root.Q<Button>("resume").clicked += TogglePause;
        root.Q<Button>("restart").clicked += () => LoadScene(SceneManager.GetActiveScene().name);
        root.Q<Button>("menu").clicked += () => LoadScene(null);

        if (paused) RoyalFade.Show(overlay);
        else RoyalFade.HideNow(overlay);
    }

    public void TogglePause()
    {
        paused = !paused;
        if (paused)
        {
            resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            RoyalFade.Show(overlay);
        }
        else
        {
            Time.timeScale = resumeTimeScale;
            RoyalFade.Hide(overlay);
        }
        if (msh) msh.active = !paused;
    }

    static void LoadScene(string name)
    {
        Time.timeScale = 1f;
        if (string.IsNullOrEmpty(name)) SceneManager.LoadScene(0);
        else SceneManager.LoadScene(name);
    }
}
