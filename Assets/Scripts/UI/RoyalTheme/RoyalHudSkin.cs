using UnityEngine;
using UnityEngine.UIElements;

// Puts the in-game HUD into the parchment style (RoyalHud.uss) without editing the original UXML files.
// Inline styles written in the old UXML beat any style sheet, so those few values are set here in code.
public class RoyalHudSkin : MonoBehaviour
{
    static readonly Color Parchment = new Color32(239, 226, 192, 255);
    static readonly Color Wood = new Color32(107, 74, 36, 255);
    static readonly Color Ink = new Color32(138, 106, 58, 255);
    static readonly Color Maroon = new Color32(150, 62, 34, 255);
    static readonly Color DarkWash = new Color32(40, 26, 10, 178);

    public StyleSheet hudStyle;
    public PanelRenderer hudPanel;     // UIMenu (gold, wave meter, tower cards, wave bar)
    public PanelRenderer speechPanel;  // speech bubble above the castle

    void Awake()
    {
        if (!hudPanel)
        {
            var hud = FindAnyObjectByType<UIResponder>();
            if (hud) hudPanel = hud.GetComponent<PanelRenderer>();
        }
        if (!speechPanel)
        {
            var speech = FindAnyObjectByType<SpeechEngine>();
            if (speech) speechPanel = speech.GetComponent<PanelRenderer>();
        }
        if (hudPanel) hudPanel.RegisterUIReloadCallback(SkinHud);
        if (speechPanel) speechPanel.RegisterUIReloadCallback(SkinSpeech);
    }

    void AddSheet(VisualElement root)
    {
        if (hudStyle && !root.styleSheets.Contains(hudStyle)) root.styleSheets.Add(hudStyle);
    }

    void SkinHud(PanelRenderer panel, VisualElement root, int version)
    {
        AddSheet(root);
        var meter = root.Q<VisualElement>("WaveMeter");
        if (meter != null)
        {
            meter.style.backgroundColor = DarkWash;
            SetBorderColour(meter, Wood);
        }
        var bar = root.Q<VisualElement>("Bar");
        if (bar != null) bar.style.backgroundColor = Maroon;
        // Cinzel is wider than the old font; keep the heading inside the screen.
        var waveHud = root.Q<VisualElement>("WaveHUD");
        var heading = waveHud?.Q<Label>();
        if (heading != null) heading.style.fontSize = 58;
    }

    void SkinSpeech(PanelRenderer panel, VisualElement root, int version)
    {
        AddSheet(root);
        var box = root.Q<VisualElement>("Tutorial");
        if (box != null)
        {
            box.style.backgroundColor = Parchment;
            SetBorderColour(box, Wood);
            box.style.borderTopWidth = 3; box.style.borderBottomWidth = 3;
            box.style.borderLeftWidth = 3; box.style.borderRightWidth = 3;
            box.style.borderTopLeftRadius = 6; box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6; box.style.borderBottomRightRadius = 6;
        }
        var title = root.Q<Label>("Title");
        if (title != null) title.style.fontSize = 20;
        var desc = root.Q<Label>("Desc");
        if (desc != null) desc.style.fontSize = 20;
        var exit = root.Q<Button>("exit");
        if (exit != null)
        {
            exit.text = "×";
            exit.style.backgroundColor = Color.clear;
            exit.style.color = Ink;
            exit.style.fontSize = 26;
            SetBorderColour(exit, Color.clear);
        }
    }

    static void SetBorderColour(VisualElement e, Color c)
    {
        e.style.borderTopColor = c; e.style.borderBottomColor = c;
        e.style.borderLeftColor = c; e.style.borderRightColor = c;
    }
}
