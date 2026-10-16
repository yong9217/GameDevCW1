using UnityEngine;

public class TimerObject : ReccurTimer
{
    public float resetTime;

    void Awake()
    {
        Init(resetTime);
    }
}
