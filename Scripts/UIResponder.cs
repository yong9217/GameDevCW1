using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class UIResponder : MonoBehaviour
{
    //Component to get assets from
    PanelRenderer pr;
    InputAction pauseKey;

    //Altered assets
    Label MoneyHUD;
    VisualElement MoneyMeter;
    VisualElement waveMeter;

    //Button actions
    [SerializeField] public Dictionary<int,GameObject> prefabsToButton;

    //GameObjects to modify
    public MouseStateHandler msh;
    public PlayerEconomy bank;
    public PauseAPI pauseMenu;

    void Awake()
    {
        pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(OnUIReload);

        pauseKey = new InputAction("Pause");
        pauseKey.AddBinding("<Keyboard>/escape");

        pauseKey.performed += (InputAction.CallbackContext ct) =>
        {
            Debug.Log("Paused!");
            pauseMenu.TogglePause();
        };

        pauseKey.Enable();
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
            msh.active = true;;
        });

        MoneyHUD = root.Q<Label>("MoneyHUDLabel");
        MoneyMeter = root.Q<VisualElement>("MoneyHUDMask");
        
        bank.RegUI(this);

        UpdateHUDTotal();

        waveMeter = root.Q<VisualElement>("Bar");

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

    public void UpdateWaveMeter(float percent, bool exact = false)
    {
        if(percent < 0f || percent > 100f)
        {
            Debug.LogError($"Wave meter is set using [0-100]! Received: {percent}");
            return;
        }

        if(percent >= 0f && percent <= 1f && !exact)
        {
            Debug.Log("Using input as %. If this was not intended, please set the 'exact' flag to true");
            percent *= 100f;
        }

        waveMeter.style.width = Length.Percent(percent);
    }
}
