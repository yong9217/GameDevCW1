using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class SpeechEngine : MonoBehaviour
{
    PanelRenderer pr;
    VisualElement root;
    Button close;
    Label desc;
    public List<SpeechBox> textBoxes;
    int next = 0;
    SpeechBox curBox;
    Action onGUILoad = () => {};
    bool started;

    public ReccurTimer timer;
    public Debugger debug;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(Init);
        curBox = textBoxes[0];

        debug.Add("j", () =>
        {
            Debug.Log("Updated!");
            NextSpeech();
        }, "TestUpdateLabel");
    }

    void Init(PanelRenderer p, VisualElement r, int ver)
    {
        root = r.Q<VisualElement>("Speech");
        desc = r.Q<Label>("Desc");
        close = r.Q<Button>("exit");

        root.RegisterCallback<TransitionEndEvent>(e =>
        {
            close.visible = false;
        });

        close.clicked += () =>
        {
            Hide();
        };

        // The UI reloads when its files change during Play; only start the game sequence once.
        if (!started)
        {
            started = true;
            onGUILoad();
        }
    }

    public void NextSpeech()
    {
        curBox = textBoxes[next];
        PlaySpeech(next);
        next = (next + 1)%textBoxes.Count;
    }

    void Hide()
    {
        root.style.scale = new StyleScale(new Vector2(0,0));
        if (curBox != null)
        {
            curBox.Invoke();
        }
    }

    void Show()
    {
        root.style.scale = new StyleScale(new Vector2(1,1));
    }

    public bool IsVisible()
    {
        return root.style.scale.value.value.x > 0;
    }

    public void PlaySpeech(int n)
    {
        if (IsVisible())
        {
            Debug.LogWarning("Going to next dialogue while current is still visible!");
        }

        SpeechBox speak = textBoxes[n];
        desc.text = speak.text;
        
        Show();

        if (speak.closable)
        {
            if (speak.overidden)
            {
                Debug.LogWarning($"Expecting speech {n} to have a scripted end!");
            } else
            {
                close.visible = true;
            }
            
        } else
        {
            timer.InvokeOnce(speak.viewTime, () =>
            {
                Hide();
            }, "HideSpeech");
        }
    }

    public int NextSpeechKey()
    {
        return next;
    }

    public void OnGUILoad(Action ac)
    {
        if(ac == null)
        {
            Debug.LogError("Unable to set OnGUILoad action to be null!");
        } else
        {
            onGUILoad = ac;
        }
    }
}

[Serializable]
public class SpeechBox
{
    public string text;
    public bool closable;
    public bool overidden;
    public float viewTime;
    Action onSpeechEnd = () => {};

    public SpeechBox(string t, bool c = true, bool o = false, float vt = 5f)
    {
        text = t;
        closable = c;
        viewTime = vt;
        overidden = o;
    }

    public void OnSpeechEnd(Action ac)
    {
        if(ac == null)
        {
            Debug.LogError("Unable to set a null action to speech end!");
        } else
        {
            onSpeechEnd = ac;
        }
    }

    public void Invoke()
    {
        if(onSpeechEnd == null)
        {
            Debug.LogError($"Box '{text}' has onSpeechEnd null");
        } else
        {
            onSpeechEnd.Invoke();
        }
    }
}