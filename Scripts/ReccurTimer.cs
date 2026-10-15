using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ReccurTimer : MonoBehaviour
{
    //All time units in seconds
    float curTicks = 0f;
    float resetThreshold = 0f;

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

    public void OnTimeInvoke(float b, Action ac)
    {
        if(!tasks.TryAdd(b, new TimeNode(ac)))
        {
            tasks[b].tasks.Add(ac);
        }
    }

    // Update is called once per frame
    void Update()
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

            Debug.Log("Reset!");
        }

        // float cTime = offset + curTicks;

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

public class TimeNode
{
    public List<Action> tasks;
    public int nCalls;
    public float nextCall;

    public TimeNode(Action ac)
    {
        tasks = new List<Action>{ac};
        nCalls = 0;
        nextCall = 0f;
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
}