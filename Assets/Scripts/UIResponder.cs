using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class UIResponder : MonoBehaviour
{
    //Component to get assets from
    public List<PanelRenderer> panels = new List<PanelRenderer>();
    InputAction pauseKey;

    //Altered assets
    Label MoneyHUD;
    VisualElement MoneyMeter;
    VisualElement waveMeter;
    VisualElement placementWarning;
    IVisualElementScheduledItem placementWarningHide;
    VisualElement moneyBox, moneyCoin;
    IVisualElementScheduledItem moneyShake, moneyDenyEnd, moneyBumpEnd;
    // Card the tutorial is pointing at (-1 = none); kept so the hint survives a UI reload.
    int hintedCard = -1;

    //Tower cards (bottom bar). Card i builds prefabsToButton[i].
    [SerializeField] public Dictionary<int,GameObject> prefabsToButton;
    readonly List<Button> towerButtons = new List<Button>();
    readonly List<Label> towerCosts = new List<Label>();

    //Loaded UI roots, used to tell whether the mouse is over UI
    readonly Dictionary<PanelRenderer, VisualElement> screenRoots = new Dictionary<PanelRenderer, VisualElement>();
    bool overWorldUI;
    readonly List<VisualElement> worldRoots = new List<VisualElement>();

    //GameObjects to modify
    public MouseStateHandler msh;
    public PlayerEconomy bank;
    public PauseAPI pauseMenu;

    void Awake()
    {
        PanelRenderer pr = GetComponent<PanelRenderer>();
        pr.RegisterUIReloadCallback(OnUIReload);

        // Track every UI panel in the scene, not only the ones listed by hand.
        foreach (PanelRenderer p in FindObjectsByType<PanelRenderer>(FindObjectsInactive.Include))
        {
            if (!panels.Contains(p)) panels.Add(p);
        }
        foreach (PanelRenderer p in panels)
        {
            if (p) p.RegisterUIReloadCallback(TrackPanel);
        }

        pauseKey = new InputAction("Pause");
        pauseKey.AddBinding("<Keyboard>/escape");

        pauseKey.performed += (InputAction.CallbackContext ct) =>
        {
            if (msh && msh.locked) return;
            // A tutorial paper is a pause of its own: only its button closes it, so Esc does nothing meanwhile.
            if (TutorialPaper.IsOpen) return;
            pauseMenu.TogglePause();
        };

        pauseKey.Enable();
    }

    void OnDestroy()
    {
        pauseKey?.Disable();
        pauseKey?.Dispose();
    }

    public void AddPanel(PanelRenderer pr)
    {
        if (panels.Contains(pr)) return;
        panels.Add(pr);

        pr.RegisterUIReloadCallback(TrackPanel);
    }

    public void AddPanel(PanelRenderer pr, PanelRenderer.VersionedUIReloadCallback OnUILoad)
    {
        AddPanel(pr);

        if(OnUILoad == null)
        {
            Debug.LogError("Unable to add a callback that is null!");
        } else
        {
            pr.RegisterUIReloadCallback(OnUILoad);
        }
    }

    void TrackPanel(PanelRenderer pr, VisualElement root, int ver)
    {
        if (pr.panelSettings && pr.panelSettings.renderMode == PanelRenderMode.WorldSpace)
        {
            // World-space panels cannot be picked from screen coordinates; follow hover events instead.
            if (!worldRoots.Contains(root)) worldRoots.Add(root);
            root.RegisterCallback<MouseOverEvent>(e => overWorldUI = true);
            root.RegisterCallback<MouseOutEvent>(e => overWorldUI = false);
        }
        else
        {
            screenRoots[pr] = root;
        }
    }

    // True when the pointer is over a visible button, label or coloured box of any UI panel.
    // Empty layout containers do not count, so clicks pass through them to the map.
    public bool IsPointerOverUI(Vector2 screenPos)
    {
        // A world panel that shrank away under the pointer never sends MouseOut, so also check it is still shown.
        if (overWorldUI && worldRoots.Exists(WorldPanelShown)) return true;

        foreach (var kv in screenRoots)
        {
            VisualElement root = kv.Value;
            if (!kv.Key || !kv.Key.enabled || !kv.Key.gameObject.activeInHierarchy || root == null || root.panel == null) continue;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            for (VisualElement e = root.panel.Pick(panelPos); e != null && e != root; e = e.parent)
            {
                if (IsSolid(e)) return true;
            }
        }
        return false;
    }

    static bool WorldPanelShown(VisualElement root)
    {
        foreach (VisualElement child in root.Children())
        {
            if (child.resolvedStyle.display != DisplayStyle.None && child.resolvedStyle.visibility == Visibility.Visible &&
                child.resolvedStyle.scale.value.x > .01f) return true;
        }
        return false;
    }

    static bool IsSolid(VisualElement e)
    {
        if (e.resolvedStyle.visibility == Visibility.Hidden || e.resolvedStyle.opacity <= 0f) return false;
        if (e is Button) return true;
        if (e is TextElement t && !string.IsNullOrEmpty(t.text)) return true;
        if (e.resolvedStyle.backgroundColor.a > .01f) return true;
        return e.resolvedStyle.backgroundImage.texture != null || e.resolvedStyle.backgroundImage.sprite != null;
    }

    void OnUIReload(PanelRenderer pr, VisualElement root, int ver)
    {
        MoneyHUD = root.Q<Label>("MoneyHUDLabel");
        MoneyMeter = root.Q<VisualElement>("MoneyHUDMask");
        moneyBox = root.Q<VisualElement>("MoneyHUD");
        moneyCoin = root.Q<VisualElement>("MoneyCoin");

        bank.RegUI(this);

        UpdateHUDTotal();

        waveMeter = root.Q<VisualElement>("Bar");
        placementWarning = root.Q<VisualElement>("PlacementWarning");

        towerButtons.Clear();
        towerCosts.Clear();
        foreach (Button card in root.Query<Button>(className: "tower-card").ToList())
        {
            towerButtons.Add(card);
            towerCosts.Add(card.Q<Label>(className: "card-cost"));
        }

        for (int i = 0; i < towerButtons.Count; i++)
        {
            int k = i;
            towerButtons[i].clicked += () => OnCardClicked(k);
        }

        // Nothing is selected at the start: the player must pick a tower card before a map click can build.
        RefreshTowerCards();
        if (hintedCard >= 0) SetCardHint(hintedCard, true);
    }

    // A card the player cannot afford is greyed out and does nothing when clicked.
    void OnCardClicked(int k)
    {
        // While a tutorial paper is open the game is paused; the cards behind it do nothing.
        if (TutorialPaper.IsOpen) return;
        if (!CardAffordable(k))
        {
            GameAudio.Play("ui_deny");
            FlashMoneyDenied();
            return;
        }
        GameAudio.Play("ui_click");
        SelectTower(k);
    }

    bool TryGetCardPrefab(int k, out GameObject prefab)
    {
        prefab = null;
        return prefabsToButton != null && prefabsToButton.TryGetValue(k, out prefab) && prefab;
    }

    bool CardAffordable(int k)
    {
        return bank && TryGetCardPrefab(k, out GameObject prefab) && bank.Purchasable(prefab.name);
    }

    // The prefab that card k builds (0 = Arrow, 1 = Cannon, 2 = Magic), or null.
    public GameObject CardPrefab(int k) => TryGetCardPrefab(k, out GameObject prefab) ? prefab : null;

    // Tutorial hint: card k blinks orange until it is switched off again (GameFlow does that once the tower is built).
    public void SetCardHint(int k, bool on)
    {
        if (on) hintedCard = k;
        else if (hintedCard == k) hintedCard = -1;
        if (k < 0 || k >= towerButtons.Count || towerButtons[k] == null) return;
        if (on) UIPulse.Start(towerButtons[k]);
        else UIPulse.Stop(towerButtons[k]);
    }

    public void ClearCardHints()
    {
        hintedCard = -1;
        foreach (Button card in towerButtons) UIPulse.Stop(card);
    }

    // Screen point (HUD panel coordinates) of the coin next to the gold amount: flying gold aims here.
    public bool TryGetCoinCentre(out Vector2 centre)
    {
        VisualElement target = moneyCoin ?? moneyBox;
        centre = default;
        if (target == null || target.panel == null) return false;
        centre = target.worldBound.center;
        return true;
    }

    // Short "bump" of the gold HUD when coins arrive.
    public void BumpMoney()
    {
        if (moneyBox == null || moneyBox.panel == null) return;
        moneyBumpEnd?.Pause();
        moneyBox.AddToClassList("gain");
        moneyBumpEnd = moneyBox.schedule.Execute(() => moneyBox.RemoveFromClassList("gain")).StartingIn(130);
    }

    // Not enough gold: the amount turns red and the gold HUD shakes, so the player sees what is missing.
    public void FlashMoneyDenied()
    {
        if (moneyBox == null || moneyBox.panel == null) return;
        moneyShake?.Pause();
        moneyDenyEnd?.Pause();
        moneyBox.AddToClassList("deny");
        float[] offsets = { -16f, 14f, -11f, 8f, -5f, 2f, 0f };
        int i = 0;
        moneyShake = moneyBox.schedule.Execute(() =>
        {
            moneyBox.style.translate = new Translate(offsets[Mathf.Min(i, offsets.Length - 1)], 0);
            i++;
        }).Every(40).Until(() => i >= offsets.Length);
        moneyDenyEnd = moneyBox.schedule.Execute(() => moneyBox.RemoveFromClassList("deny")).StartingIn(600);
    }

    public void SelectTower(int k)
    {
        if(TryGetCardPrefab(k, out GameObject prefab))
        {
            msh.towerPrefab = prefab;
        } else
        {
            Debug.LogWarning($"No prefab assigned to tower button {k}");
        }
        RefreshTowerCards();
    }

    // Right click (MouseStateHandler): nothing selected, so no card has the yellow border and map clicks build nothing.
    public void DeselectTower()
    {
        if (msh) msh.towerPrefab = null;
        RefreshTowerCards();
    }

    // Card look depends only on money and on which tower is selected, never on the mouse:
    // ready = can afford, locked = cannot afford, selected = yellow border.
    void RefreshTowerCards()
    {
        for (int i = 0; i < towerButtons.Count; i++)
        {
            // A UI reload (editor hot reload) releases the old elements; skip them until OnUIReload rebuilds the list.
            if (towerButtons[i] == null || towerButtons[i].panel == null) continue;

            bool hasPrefab = TryGetCardPrefab(i, out GameObject prefab);
            bool affordable = hasPrefab && bank && bank.Purchasable(prefab.name);
            bool selected = hasPrefab && msh && prefab == msh.towerPrefab;

            towerButtons[i].EnableInClassList("ready", affordable);
            towerButtons[i].EnableInClassList("locked", !affordable);
            towerButtons[i].EnableInClassList("selected", selected);

            if (hasPrefab && bank && towerCosts[i] != null && towerCosts[i].panel != null && bank.TryGetCost(prefab.name, out float cost))
            {
                towerCosts[i].text = $"{cost:0.##}";   // the coin icon next to it is in UIMenu.uxml
            }
        }
    }

    public void UpdateHUDTotal()
    {
        RefreshTowerCards();

        if (MoneyHUD == null)
        {
            Debug.LogError("Have not initialized UI!");
            return;
        }

        MoneyHUD.text = $"{bank.GetTotal():0.##}";
        // The old fill bar is optional; the HUD is now just a coin icon and the amount.
        if (MoneyMeter != null) MoneyMeter.style.width = Length.Percent(bank.GetPercent());
    }

    // Fades in the "cannot build here" popup and fades it out again after a few seconds.
    // Returns false when the popup is not on screen (UI not loaded), so the caller can try again later.
    // It never blocks clicks, and UI Toolkit's scheduler runs in real time, so it also works while paused.
    public bool ShowPlacementWarning(float seconds = 5f)
    {
        if (placementWarning == null || placementWarning.panel == null) return false;

        placementWarningHide?.Pause();
        placementWarning.AddToClassList("shown");
        placementWarningHide = placementWarning.schedule
            .Execute(() => placementWarning.RemoveFromClassList("shown"))
            .StartingIn((long)(seconds * 1000f));
        return true;
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

        if (waveMeter != null) waveMeter.style.width = Length.Percent(percent);
    }
}
