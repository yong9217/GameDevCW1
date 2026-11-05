using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class SpeechEngine : MonoBehaviour
{
    PanelRenderer pr;
    VisualElement root;
    Label desc;
    public List<string> textBoxes;
    int next = 0;

    public float showFor;

    public ReccurTimer timer;
    public Debugger debug;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(Init);

        debug.Add("j", () =>
        {
            Debug.Log("Updated!");
            NextSpeech();
        }, "TestUpdateLabel");
    }

    void Init(PanelRenderer p, VisualElement r, int ver)
    {
        root = r.Q<VisualElement>("Tutorial");
        desc = r.Q<Label>("Desc");
    }

    public void NextSpeech()
    {
        PlaySpeech(next);
        next = (next + 1)%textBoxes.Count;
    }

    public void PlaySpeech(int n)
    {
        desc.text = textBoxes[n];

        root.visible = true;

        timer.InvokeOnce(showFor, () =>
        {
            Debug.Log("Hide!");
            root.visible = false;
        }, "HideSpeech");
    }
}
