using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Debugger : MonoBehaviour
{
    public bool printDebug;
    Dictionary<string, DebugTask> callbacks = new Dictionary<string, DebugTask>();

    public bool useManualDist;
    public float manualDist;

    char toggleKey = 'q';
    string modifierKey = "leftCtrl";

    void Awake()
    {
        InputAction toggleDebug = new InputAction("Toggle Debug");
        toggleDebug.AddCompositeBinding("ButtonWithOneModifier")
            .With("Button", $"<Keyboard>/" + toggleKey)
            .With("Modifier", "<Keyboard>/" + modifierKey);

        toggleDebug.performed += (InputAction.CallbackContext ct) =>
        {
            SetDebug(!printDebug);
        };

        toggleDebug.Enable();
    }

    void Start()
    {
        Add("y", () =>
        {
            Debug.Log(this);
        }, eventName: "self");

        SetDebug(printDebug);
    }

    public void Add(string key, Action ac, string eventName = null, IType input = IType.Keyboard)
    {
        string callName = $"Debug {(eventName == null ? callbacks.Count : eventName)} on {key}";

        InputAction debugAction = new InputAction(callName, type: InputActionType.Button);

        debugAction.AddCompositeBinding("ButtonWithOneModifier")
            .With("Button", $"<Keyboard>/{key}")
            .With("Modifier", $"<{input}>/{modifierKey}");

        debugAction.canceled += (InputAction.CallbackContext ct) =>
        {
            callbacks[callName].active = true;
        };
        debugAction.performed += (InputAction.CallbackContext ct) =>
        {
            DebugTask db = callbacks[callName];
            if (db.active)
            {
                callbacks[callName].task();
                db.active = false;
            }
        };

        if(!callbacks.TryAdd(callName, new DebugTask(ac,debugAction)))
        {
            callbacks[callName].task = new Action(() =>
            {
                callbacks[callName].task = ac;
            });
        }

        if (printDebug)
        {
            debugAction.Enable();
        }
    }

    public void SetDebug(bool b)
    {
        printDebug = b;

        if (printDebug)
        {
            foreach (DebugTask db in callbacks.Values)
            {
                db.Enable();
            }
        } else
        {
            foreach (DebugTask db in callbacks.Values)
            {
                db.Disable();
            }
        }

        Debug.Log(printDebug ? "Enabled debug" : "Disabled debug");
    }

    public Vector3[] GetCamBoundaries()
    {
        float yV = useManualDist ? manualDist : Camera.main.transform.position.y;

        Vector3[] corners = new Vector3[4];
        corners[0] = Camera.main.ViewportToWorldPoint(new Vector3(0,0,yV)); //botleft
        corners[1] = Camera.main.ViewportToWorldPoint(new Vector3(1,0,yV)); //botright
        corners[2] = Camera.main.ViewportToWorldPoint(new Vector3(1,1,yV)); //topright
        corners[3] = Camera.main.ViewportToWorldPoint(new Vector3(0,1,yV)); //topleft

        return corners;
    }

    public Vector2 GetCamSize()
    {
        Vector3[] corners = GetCamBoundaries();

        return new Vector2(Mathf.Abs((corners[0]-corners[1]).magnitude), Mathf.Abs((corners[2]-corners[1]).magnitude));
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Vector3[] corners = GetCamBoundaries();
        for(int i = 0; i < corners.Length; i++)
        {
            int next = (i + 1)%corners.Length;
            Vector3 dir = corners[next] - corners[i];
            Gizmos.DrawRay(corners[i],dir);
        }
    }

    public override string ToString()
    {
        string ot = $"Debugger ({(printDebug ? "|>" : "||")}) {callbacks.Count} callbacks\n";

        foreach (string nm in callbacks.Keys)
        {
            ot += $"{nm}\n";
        }

        return ot;
    }
}

class DebugTask
{
    public bool active;
    public Action task;
    public InputAction inputEvent;

    public DebugTask(Action t, InputAction e)
    {
        active = true;
        task = t;
        inputEvent = e;
    }

    public void Enable()
    {
        inputEvent.Enable();
    }

    public void Disable()
    {
        inputEvent.Disable();
    }
}

public enum IType
{
    Keyboard,
    Mouse
}