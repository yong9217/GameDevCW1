using UnityEngine;
using UnityEngine.UIElements;

public class PauseAPI : MonoBehaviour
{
    PanelRenderer pr;

    public readonly Stylist stylist = new Stylist();
    public MouseStateHandler msh;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(Init);
    }

    void Init(PanelRenderer p, VisualElement root, int ver)
    {
        stylist.Add(root.Q<VisualElement>("mask"),-1f,0f);
        stylist.Add(root.Q<VisualElement>("main"),-1f,0f);
        root.Q<Button>("exit").clicked += () =>
        {
            TogglePause();
        };
        root.Q<Button>("resume").clicked += () =>
        {
            TogglePause();
        };

        stylist.ToggleVisible(); //Need to call once bcuz the translates read as 0 at the start but are actually -100%
    }

    public void TogglePause()
    {
        stylist.ToggleVisible();
        bool b = stylist.IsVisible();
        Time.timeScale = b ? 0f : 1f;
        msh.active = !b;
    }
}
