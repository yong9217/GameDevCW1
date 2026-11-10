using UnityEngine;

public class Base : MonoBehaviour
{
    float hp;
    bool defeated;
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
        // The decorative castle in the main menu has no Debugger.
        if (!debug) return;
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

        if(hp <= 0 && !defeated)
        {
            defeated = true;
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
