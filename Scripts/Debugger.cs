using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Debugger : MonoBehaviour
{
    public bool printDebug;
    Dictionary<string, DebugTask> callbacks;

    char toggleKey = 'q';
    string modifierKey = "leftCtrl";

    void Awake()
    {
        callbacks = new Dictionary<string, DebugTask>();

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

    public void Add(string key, Action ac, string eventName = null)
    {
        string callName = $"Debug {(eventName == null ? callbacks.Count : eventName)} on {key}";

        InputAction debugAction = new InputAction(callName, type: InputActionType.Button);

        debugAction.AddCompositeBinding("ButtonWithOneModifier")
            .With("Button", $"<Keyboard>/{key}")
            .With("Modifier", $"<Keyboard>/{modifierKey}");

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
                callbacks[callName].task();
                ac();
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