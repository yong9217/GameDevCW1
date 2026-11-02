using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerEconomy : MonoBehaviour
{
    public ReccurTimer timer;
    public float timeBetweenCashIns;
    public float CashInAmounts;
    public float startCash;
    public float maxCash;
    [SerializeField] public Dictionary<GameObject, float> towerToCost;

    float totalCash;
    UIResponder uiR;
    Dictionary<string, float> catalog;

    public Debugger debug;
    public string debugKey;
    public bool Monopoly;

    void Awake()
    {
        if(startCash <= maxCash & startCash >= 0)
        {
            totalCash = startCash;
        } else
        {
            totalCash = 100f;
        }

        catalog = towerToCost.ToDictionary(kv => kv.Key.name, kv => kv.Value);
    }

    void Start()
    {
        timer.OnTimeInvoke(timeBetweenCashIns, () =>
        {
            Deposit(CashInAmounts, "Bank");
        }, "Cash-Ins", timeBetweenCashIns);

        debug.Add(debugKey, () =>
        {
            Debug.Log(this);
        }, eventName: "Wallet");

        debug.Add("l", () =>
        {
            Debug.Log(debug.GetCamSize());
        }, "Cam Size");
    }

    public bool Spend(float buy)
    {
        bool spent = buy <= totalCash;
        if(spent)
        {
            if (!Monopoly)
            {
                totalCash -= buy;
            }
        }

        UpdateCash();

        return spent;
    }

    public bool Purchase(string tower)
    {
        if(catalog.TryGetValue(tower, out float cost))
        {
            return Spend(cost);
        }

        Debug.LogError($"No item called '{tower}' in catalog");
        return false;
    }

    public bool Deposit(float bank, string source = "")
    {
        totalCash += bank;

        float lost = totalCash - maxCash;
        bool exceed = totalCash > maxCash;
        string totalDepo = "";

        if(exceed)
        {
            totalCash = maxCash;
            totalDepo = $"{bank - lost} Lost: {lost}";
        } else
        {
            totalDepo += bank;
        }

        UpdateCash();

        Debug.Log($"Deposited {totalDepo} from {(source.Length <= 0 ? "Unknown" : source)}");

        return totalCash < maxCash;
    }

    void UpdateCash()
    {
        if(uiR != null)
        {
            uiR.UpdateHUDTotal();
        }
    }

    public float GetTotal()
    {
        return totalCash;
    }

    public float GetProportion()
    {
        if(maxCash == 0f)
        {
            return Mathf.Infinity;
        }
        return totalCash/maxCash;
    }

    public float GetPercent()
    {
        return GetProportion()*100f;
    }

    public void RegUI(UIResponder r)
    {
        if(r == null)
        {
            Debug.LogWarning("Registered null UI! This removes the previous UI");
        }
        uiR = r;
    }

    public override string ToString()
    {
        string ot = $"Player wallet: {totalCash} / {maxCash} $\n";
        ot += $"Influx: {CashInAmounts} $ / {timeBetweenCashIns} secs (Started with {startCash} $)";

        return ot;
    }
}
