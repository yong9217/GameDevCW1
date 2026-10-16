using UnityEngine;

public class PlayerEconomy : MonoBehaviour
{
    public ReccurTimer timer;
    public float timeBetweenCashIns;
    public float CashInAmounts;
    public float startCash;
    public float maxCash;

    float totalCash;

    public Debugger debug;
    public string debugKey;

    void Awake()
    {
        if(startCash <= maxCash & startCash >= 0)
        {
            totalCash = startCash;
        } else
        {
            totalCash = 100f;
        }
    }

    void Start()
    {
        timer.OnTimeInvoke(timeBetweenCashIns, () =>
        {
            totalCash += CashInAmounts;
        });

        debug.Add(debugKey, () =>
        {
            Debug.Log(this);
        }, eventName: "Wallet");
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
}
