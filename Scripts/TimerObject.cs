using UnityEngine;

public class TimerObject : ReccurTimer
{
    public float resetTime;

    public Debugger debug;

    void Awake()
    {
        Init(resetTime);
    }

    void Start()
    {
        debug.Add("t", () => {
            Debug.Log(this);
        }, eventName: "Timer");
    }
}
