using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Makes a UI element blink to draw the player's eye (tutorial hints): it gets the class "pulse" while
// pulsing and "pulse-on" switches on and off. The look of each element is in the style sheets.
// UI Toolkit's scheduler runs in real time, so it keeps blinking while the game is paused.
public static class UIPulse
{
    const string Pulse = "pulse";
    const string On = "pulse-on";
    static readonly Dictionary<VisualElement, IVisualElementScheduledItem> running = new Dictionary<VisualElement, IVisualElementScheduledItem>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => running.Clear();

    public static void Start(VisualElement e, long periodMs = 450)
    {
        if (e == null) return;
        Prune();
        Stop(e);
        e.AddToClassList(Pulse);
        e.AddToClassList(On);
        running[e] = e.schedule.Execute(() => e.ToggleInClassList(On)).Every(periodMs);
    }

    // Drops entries whose element is no longer on screen (its scene was unloaded).
    static void Prune()
    {
        var dead = new List<VisualElement>();
        foreach (var kv in running)
            if (kv.Key.panel == null) dead.Add(kv.Key);
        foreach (var e in dead)
        {
            running[e].Pause();
            running.Remove(e);
        }
    }

    public static void Stop(VisualElement e)
    {
        if (e == null) return;
        if (running.TryGetValue(e, out var item))
        {
            item.Pause();
            running.Remove(e);
        }
        e.RemoveFromClassList(Pulse);
        e.RemoveFromClassList(On);
    }
}
