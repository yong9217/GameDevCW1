using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class Stylist
{
    public List<ViewBounds> elements = new List<ViewBounds>();

    public void Add(VisualElement e, float hide = 1f, float show = 0f)
    {
        elements.Add(new ViewBounds(e,hide,show));
    }

    public bool IsVisible()
    {
        if (elements != null)
        {
            if(elements.Count > 0)
            {
                return elements.First().IsVisible();
            }
        }

        Debug.LogWarning("No elements to check style!");

        return false;
    }

    public void Show()
    {
        foreach (ViewBounds e in elements)
        {
            e.Show();
        }
    }

    public void Hide()
    {
        foreach (ViewBounds e in elements)
        {
            e.Hide();
        }
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

    public void ToggleVisible()
    {
        SetVisible(!IsVisible());
    }
}

public struct ViewBounds
{
    public VisualElement element;

    float hideBound;
    float showBound;

    public ViewBounds(VisualElement e, float hide = 0f, float show = 1f)
    {
        element = e;
        hideBound = hide;
        showBound = show;
    }

    public bool IsVisible()
    {
        return element.style.translate.value.y.value == showBound;
    }

    public void Show()
    {
        element.style.translate = ToTranslate(y: showBound);
    }

    public void Hide()
    {
        element.style.translate = ToTranslate(y: hideBound);
    }

    public static StyleTranslate ToTranslate(float x = 0f, float y = 0f)
    {
        return new StyleTranslate(new Translate(new Length(x*100f), Length.Percent(y*100f)));
    }
}