using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIResponder : MonoBehaviour
{
    PanelRenderer pr;
    Label MoneyHUD;
    VisualElement MoneyMeter;
    [SerializeField] public Dictionary<int,GameObject> prefabsToButton;
    public MouseStateHandler msh;
    public PlayerEconomy bank;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(OnUIReload);
    }

    void OnUIReload(PanelRenderer pr, VisualElement root, int ver)
    {
        root.RegisterCallback<MouseOverEvent>(e =>
        {
            Debug.Log("Hovered!");
            msh.active = false;
        });
        root.RegisterCallback<MouseOutEvent>(e =>
        {
            Debug.Log("Left!");
            msh.Activate();
        });

        MoneyHUD = root.Q<Label>("MoneyHUDLabel");
        MoneyMeter = root.Q<VisualElement>("MoneyHUDMask");
        
        bank.RegUI(this);

        UpdateHUDTotal();

        List<Button> buts = root.Query<Button>().ToList();

        for (int i = 0; i < buts.Count; i++)
        {
            int k = i;

            buts[i].clicked += () =>
            {
                Debug.Log($"Clicked button {buts[k].name}");

                if(prefabsToButton.TryGetValue(k, out GameObject prefab))
                {
                    msh.towerPrefab = prefab;
                } else
                {
                    Debug.LogWarning($"No prefab assigned to button {buts[k].name}");
                }
            };
        }
    }

    public void UpdateHUDTotal()
    {
        if(MoneyHUD != null && MoneyMeter != null)
        {
            MoneyHUD.text = $"{bank.GetTotal()} / {bank.maxCash} $";
            MoneyMeter.style.width = Length.Percent(bank.GetPercent());
        } else
        {
            Debug.LogError("Have not initialized UI!");
        }
    }
}
