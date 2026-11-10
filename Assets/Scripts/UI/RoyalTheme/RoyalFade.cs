using UnityEngine.UIElements;

// Fade + slight scale for full-screen menus (styles in RoyalTheme.uss).
// Hidden overlays are removed from layout so they never catch clicks.
// UI Toolkit runs transitions and its scheduler in real time, so this also works while the game is paused.
public static class RoyalFade
{
    public const string HiddenClass = "royal-hidden";
    const long FadeMs = 220;

    public static bool IsShown(VisualElement e) => e != null && !e.ClassListContains(HiddenClass);

    public static void Show(VisualElement e)
    {
        if (e == null) return;
        e.style.display = DisplayStyle.Flex;
        e.AddToClassList(HiddenClass);
        // Remove the class on the next panel update so the transition actually plays.
        e.schedule.Execute(() => e.RemoveFromClassList(HiddenClass)).StartingIn(16);
    }

    public static void Hide(VisualElement e)
    {
        if (e == null) return;
        e.AddToClassList(HiddenClass);
        e.schedule.Execute(() =>
        {
            if (e.ClassListContains(HiddenClass)) e.style.display = DisplayStyle.None;
        }).StartingIn(FadeMs);
    }

    public static void HideNow(VisualElement e)
    {
        if (e == null) return;
        e.AddToClassList(HiddenClass);
        e.style.display = DisplayStyle.None;
    }
}
