using UnityEngine;

public class TestTimer : MonoBehaviour
{
    public ReccurTimer timer;
    public string t;
    public float init;

    void Start()
    {
        timer.Init(init);
        timer.OnTimeInvoke(5f, () => Debug.Log(t));
    }
}
