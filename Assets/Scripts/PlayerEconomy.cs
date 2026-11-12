using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEconomy : MonoBehaviour
{
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

        catalog = BuildCatalog();
    }

    Dictionary<string, float> BuildCatalog()
    {
        return towerToCost == null
            ? new Dictionary<string, float>()
            : towerToCost.Where(kv => kv.Key).ToDictionary(kv => kv.Key.name, kv => kv.Value);
    }

    // Built on demand, so a lookup before Awake (or after a script reload in the editor) still works.
    Dictionary<string, float> Catalog => catalog ?? (catalog = BuildCatalog());

    void Start()
    {
        debug.Add(debugKey, () =>
        {
            Debug.Log(this);
        }, eventName: "Wallet");
    }

    public bool Spend(float buy)
    {
        bool spent = buy <= totalCash;
        if(!Monopoly)
        {
            if (spent)
            {
                totalCash -= buy;
            }
        }

        UpdateCash();

        return spent;
    }

    public bool Purchasable(float buy)
    {
        return buy <= totalCash || Monopoly;
    }

    public bool TryGetCost(string tower, out float cost)
    {
        return Catalog.TryGetValue(tower, out cost);
    }

    public bool Purchasable(string tower)
    {
        if(TryGetCost(tower, out float cost))
        {
            return Purchasable(cost);
        }

        Debug.LogError($"No item called '{tower}' in catalog");
        return false;
    }

    public bool Purchase(string tower)
    {
        if(TryGetCost(tower, out float cost))
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

    public void UpdateCatalog(string tower, Func<float,float> update)
    {
        if(catalog.ContainsKey(tower))
        {
            float temp = update(catalog[tower]);
            catalog[tower] = temp;

            Debug.Log($"Tower {tower} now costs {catalog[tower]}, calculated {temp}");
        } else
        {
            Debug.LogError($"No tower '{tower}' in catalog!");
        }
    }

    public void UpdateCatalog(GameObject tower, Func<float,float> update)
    {
        UpdateCatalog(tower.name, update);
    }

    public void AssignCatalog(string tower, float newPrice)
    {
        if(newPrice < 0)
        {
            Debug.LogWarning("Price cannot be negative!");
            return;
        }

        if (catalog.ContainsKey(tower))
        {
            catalog[tower] = newPrice;
        } else
        {
            Debug.LogError($"No tower '{tower}' in catalog!");
        }
    }

    public float Request(string tower)
    {
        return catalog[tower];
    }

    public float Request(GameObject tower)
    {
        return Request(tower.name);
    }

    public override string ToString()
    {
        string ot = $"Player wallet: {totalCash} / {maxCash} $\n";
        ot += $"(Started with {startCash} $; income only from kill rewards)";

        return ot;
    }

    public string PrintCatalog()
    {
        string ot = "Catalog:\n";

        foreach (KeyValuePair<string, float> kv in catalog)
        {
            ot += $"{kv.Key} : {kv.Value}\n";
        }

        return ot;
    }
}
