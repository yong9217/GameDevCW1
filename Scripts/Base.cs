using UnityEngine;

public class Base : MonoBehaviour
{
    float hp;
    public float startHP;

    //End game stuff
    public GameEnd end;

    public Debugger debug;
    public bool destructable;

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
        if (destructable)
        {
            hp += dmg;
        }

        if(hp <= 0)
        {
            //Do smtg
            Debug.Log("Base Defeated");

            end.CueDeath();
        }
    }

    public override string ToString()
    {
        return $"Base at {hp} / {startHP}";
    }
}
