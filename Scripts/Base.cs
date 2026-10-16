using UnityEngine;

public class Base : MonoBehaviour
{
    float hp;
    public float startHP;

    public Debugger debug;

    void Awake()
    {
        hp = startHP;
    }

    void Start()
    {
        debug.Add("e", () =>
        {
            Debug.Log(this);
        }, eventName: "base");
    }

    public float GetHP()
    {
        return hp;
    }

    public void Direct(float dmg)
    {
        hp += dmg;

        if(hp <= 0)
        {
            //Do smtg
        }
    }

    public override string ToString()
    {
        return $"Base at {hp} / {startHP}";
    }
}
