using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerEconomy : MonoBehaviour
{
    public ReccurTimer timer;
    public float timeBetweenCashIns;
    public float CashInAmounts;
    public float startCash;
    public float maxCash;

    float totalCash;

    public bool suppressDebug;
    InputAction debug;
    bool b = true;

    void Awake()
    {
        if(startCash <= maxCash & startCash >= 0)
        {
            totalCash = startCash;
        } else
        {
            totalCash = 100f;
        }

        debug = InputSystem.actions.FindAction("Debug");

        if(debug == null)
        {
            Debug.LogWarning("Unable to find Debug action. Go to ");
        }
    }

    void Start()
    {
        timer.OnTimeInvoke(timeBetweenCashIns, () =>
        {
            totalCash += CashInAmounts;
        });
    }

    public bool Spend(float buy)
    {
        if(buy < totalCash)
        {
            totalCash -= buy;
            return true;
        }

        return false;
    }

    public bool Deposit(float bank)
    {
        totalCash += bank;
        if(totalCash > maxCash)
        {
            totalCash = maxCash;
            return true;
        }

        return false;
    }

    public override string ToString()
    {
        string ot = $"Player wallet: {totalCash} / {maxCash} $\n";
        ot += $"Influx: {CashInAmounts} $ / {timeBetweenCashIns} secs (Started with {startCash} $)";

        return ot;
    }

    void Update()
    {
        if (!suppressDebug)
        {
            if (debug.IsPressed())
            {
                if (b)
                {
                    Debug.Log(this);
                    b = false;
                }
            } else
            {
                b = true;
            }
        }
    }
}
