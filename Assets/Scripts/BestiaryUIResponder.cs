using UnityEngine;
using UnityEngine.UIElements;

public class BestiaryUIResponder : MonoBehaviour
{
    PanelRenderer pr;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(Init);
    }

    void Init(PanelRenderer p, VisualElement root, int ver)
    {
        GameAudio.AddButtonClicks(root);
        root.Q<Button>("exit").clicked += () =>
        {
            gameObject.SetActive(false);
        };
    }
}
