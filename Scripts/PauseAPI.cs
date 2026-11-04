using UnityEngine;
using UnityEngine.UIElements;

public class PauseAPI : MonoBehaviour
{
    PanelRenderer pr;

    VisualElement mask;
    VisualElement main;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(Init);
    }

    void Init(PanelRenderer p, VisualElement root, int ver)
    {
        mask = root.Q<VisualElement>("mask");
        main = root.Q<VisualElement>("main");
        root.Q<Button>("exit").clicked += () =>
        {
            ToggleVisible();
        };
        root.Q<Button>("resume").clicked += () =>
        {
            ToggleVisible();
        };

        ToggleVisible(); //Need to call once bcuz the translates read as 0 at the start but are actually -100%
    }

    StyleTranslate YTranslate(float y)
    {
        return new StyleTranslate(new Translate(new Length(0), Length.Percent(y*100f)));
    }

    void Show()
    {
        mask.style.translate = YTranslate(0f);
        main.style.translate = YTranslate(0f);
    }

    void Hide()
    {
        mask.style.translate = YTranslate(-1f);
        main.style.translate = YTranslate(-1f);
    }

    public void SetVisible(bool v)
    {
        if (v)
        {
            Show();
        } else
        {
            Hide();
        }
    }

    public bool IsVisible()
    {
        return mask.style.translate.value.y.value == 0f;
    }

    public void ToggleVisible()
    {
        bool b = IsVisible();
        SetVisible(!b);
    }
}
