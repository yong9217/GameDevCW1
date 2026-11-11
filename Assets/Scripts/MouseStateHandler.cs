using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class MouseStateHandler : MonoBehaviour
{
    // False while the pause menu is open.
    public bool active;
    // Set once the game is over; nothing can be built after that.
    [NonSerialized] public bool locked;

    public PlayerEconomy bank;
    public GameObject towerPrefab;
    public float towerScale;
    public UIResponder ui;

    public bool debug;
    Action onPlace = () => {};
    // Raised after a tower has been built and paid for, with the prefab it was built from (used by the tutorial).
    public event Action<GameObject> TowerPlaced;

    // The "cannot build here" popup shows only for the FIRST illegal click of a game.
    // This is an instance field on purpose: Start Game and Restart Game both reload the scene, which creates a
    // new handler, so the popup is armed again for the next game and for nothing else.
    bool placementWarningShown;

    [Tooltip("Extra empty space required around a new tower, in world units.")]
    [Min(0)] public float placementClearance = .05f;

    void Awake()
    {
        if (!ui) ui = FindAnyObjectByType<UIResponder>();
    }

    // Poll the button every frame: one press = one placement attempt,
    // whether or not the mouse moved while clicking.
    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!active || locked) return;

        // Right click cancels the selection: afterwards a left click on the map builds nothing
        // until a tower card is picked again, so stray clicks cannot spend gold by accident.
        if (mouse.rightButton.wasPressedThisFrame)
        {
            DeselectTower();
            return;
        }
        if (!mouse.leftButton.wasPressedThisFrame) return;

        Vector2 mPos = mouse.position.ReadValue();
        if (ui && ui.IsPointerOverUI(mPos)) return;

        Camera cam = Camera.main;
        if (!cam) return;

        // Works for any camera angle: cast a ray from the mouse and hit the ground plane (y = 0).
        Ray ray = cam.ScreenPointToRay(mPos);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float hitDistance))
        {
            ActiveState(ray.GetPoint(hitDistance), mPos);
        }
    }

    public void DeselectTower()
    {
        if (ui) ui.DeselectTower();   // also un-highlights the card
        else towerPrefab = null;
    }

    void ActiveState(Vector3 wpos, Vector2 pos)
    {
        if (debug)
        {
            Debug.Log($"Clicked at ({pos.x},{pos.y})");
            Debug.Log($"Goes to ({wpos.x},{wpos.y},{wpos.z})");
        }

        TryPlaceTower(wpos);
    }

    // Order matters: check the spot first, charge only when it is valid, then create the tower.
    // Returns true only when a tower was actually placed and paid for.
    public bool TryPlaceTower(Vector3 worldPosition)
    {
        Vector3 position = new Vector3(worldPosition.x, 0, worldPosition.z);

        if (!towerPrefab)
        {
            Debug.Log("Cannot place tower: select a tower first.");
            return false;
        }

        // Check with the size the tower will really have once placed.
        Vector3 scale = towerPrefab.transform.localScale * towerScale;
        if (!TowerPlacement.CanPlace(towerPrefab, position, Quaternion.identity, scale, placementClearance, out string reason))
        {
            Debug.Log("Cannot place tower: " + reason);
            GameAudio.Play("ui_deny");
            // Only a bad spot counts (not "no tower selected"); only once per game.
            if (!placementWarningShown && TowerPlacement.IsBlockedSpot(reason) && ui)
                placementWarningShown = ui.ShowPlacementWarning();
            return false;
        }

        if (!bank.Purchasable(towerPrefab.name))
        {
            Debug.Log("Insufficient funds to buy tower!");
            GameAudio.Play("ui_deny");
            if (ui) ui.FlashMoneyDenied();
            return false;
        }

        bank.Purchase(towerPrefab.name);

        GameObject tower = Instantiate(towerPrefab, position, Quaternion.identity);
        tower.transform.localScale = scale;
        GameAudio.Play("build");

        onPlace();
        TowerPlaced?.Invoke(towerPrefab);

        return true;
    }

    public void OnPlace(Action ac)
    {
        onPlace = ac ?? (() => {});
    }
}
