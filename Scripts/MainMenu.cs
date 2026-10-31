using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenu : MonoBehaviour
{
    PanelRenderer pr;
    public PanelRenderer bestiary;

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
            Debug.Log("Activated!");
            bestiary.gameObject.SetActive(true);
        };
    }
}
