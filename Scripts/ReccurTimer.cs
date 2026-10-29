using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ReccurTimer : MonoBehaviour
{
    //All time units in seconds
    float curTicks = 0f;
    float resetThreshold = 0f;

    int nCycles = 0;

    public bool paused;

    SortedList<float, TimeNode> tasks = new SortedList<float, TimeNode>();

    public void Init(float t)
    {
        SetThreshold(t);
    }

    public void SetThreshold(float t)
    {
        if(t <= 0)
        {
            Debug.LogError("Unable to have a 0 or negative reset threshold");
        } else
        {
            resetThreshold = t;
        }
    }

    public void OnTimeInvoke(float b, Action ac, float at = 0f)
    {
        if(!tasks.TryAdd(b, new TimeNode(ac, at)))
        {
            tasks[b].tasks.Add(ac);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!paused)
        {
            float dTime = Time.deltaTime;
            curTicks += dTime;

            if(curTicks >= resetThreshold)
            {
                curTicks -= resetThreshold * Mathf.Floor(curTicks/resetThreshold);

                foreach (KeyValuePair<float, TimeNode> kv in tasks)
                {
                    kv.Value.Reset(resetThreshold);
                }

                nCycles++;

                Debug.Log("Reset!");
            }

            foreach (float k in tasks.Keys.ToList())
            {
                TimeNode node = tasks[k];

                if(curTicks > node.nextCall)
                {                
                    node.nextCall += k;
                    node.Call();
                    foreach (Action t in node.tasks)
                    {
                        t.Invoke();
                    }
                }
            }
        }
    }

    public override string ToString()
    {
        string ot = $"Timer ({(paused ? "||" : "|>")}) at {curTicks} / {resetThreshold} secs [{nCycles} cycles :: Total elapsed: {nCycles*resetThreshold + curTicks} secs]\n";

        foreach (float k in tasks.Keys.ToList())
        {
            ot += $"Every {k} secs :: {tasks[k]}\n";
        }

        return ot;
    }
}

public class TimeNode
{
    public List<Action> tasks;
    public int nCalls;
    public float nextCall;

    public TimeNode(Action ac, float offset = 0f)
    {
        tasks = new List<Action>{ac};
        nCalls = 0;
        nextCall = offset;
    }

    public void Reset(float resT)
    {
        nCalls = 0;
        nextCall -= resT;
    }

    public void Call()
    {
        nCalls++;
    }

    public override string ToString()
    {
        string ot = $"TimeNode {tasks.Count} tasks | nCalls: {nCalls}, nextCall: {nextCall}";

        return ot;
    }
}