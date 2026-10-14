using UnityEngine;

public class TestTimer : MonoBehaviour
{
    public ReccurTimer timer;
    public string t;

    void Start()
    {
        timer.Init(60f);
        timer.OnTimeInvoke(5f, () => Debug.Log(t));
    }
}
