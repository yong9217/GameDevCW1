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

    void intialize(PanelRenderer p, VisualElement root, int ver)
    {
        GameAudio.AddButtonClicks(root);
        root.Q<Button>("start").clicked += () =>
        {
            LoadGame();
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
        root.Q<Button>("infinite").clicked += () => {
            SceneManager.LoadScene(2);
        };
    }
}
