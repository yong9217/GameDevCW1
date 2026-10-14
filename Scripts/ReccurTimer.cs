using System;
using System.Collections.Generic;
using UnityEngine;

public class ReccurTimer : MonoBehaviour
{
    //All time units in seconds
    float curTicks = 0f;
    float resetThreshold = 0f;

    SortedList<float, List<Action>> tasks = new SortedList<float, List<Action>>();
    Dictionary<float, float> offsets = new Dictionary<float, float>();

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
        if(!tasks.TryAdd(b, new List<Action> { ac }))
        {
            tasks[b].Add(ac);
        } else
        {
            offsets.Add(b,0f);
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
            foreach (float k in offsets.Keys)
            {
                offsets[k] += curTicks;
                if(k > offsets[k])
                {
                    offsets[k] -= k;
                }
            }

            Debug.Log(curTicks);
        }

        foreach (KeyValuePair<float, List<Action>> kv in tasks)
        {
            float mod = (kv.Key - offsets[kv.Key])/curTicks;
            float nSecsBetweenMods = (mod - Mathf.Floor(mod))*kv.Key;
            float t = nSecsBetweenMods - dTime;

            if(t <= 0)
            {
                kv.Value.ForEach(a => a.Invoke());
            }
        }
    }
}
