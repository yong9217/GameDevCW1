using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class LostStylist : MonoBehaviour
{
    PanelRenderer pr;
    [System.NonSerialized] public Stylist stylist = new Stylist();

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(Init);
    }

    void Init(PanelRenderer p, VisualElement root, int ver)
    {
        stylist.Add(root.Q<VisualElement>("background"),-1f,0f);
        VisualElement bRoot = root.Q<VisualElement>("buttons");
        stylist.Add(bRoot,-2f,0f);

        bRoot.Q<Button>("play").clicked += () =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        };
        bRoot.Q<Button>("return").clicked += () =>
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(0);
        };
    }
}
