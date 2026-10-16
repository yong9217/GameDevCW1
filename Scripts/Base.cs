using UnityEngine;

public class Base : MonoBehaviour
{
    float hp;
    public float startHP;

    void Awake()
    {
        hp = startHP;
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
}
