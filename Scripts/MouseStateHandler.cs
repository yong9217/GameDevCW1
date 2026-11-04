using UnityEngine;
using UnityEngine.InputSystem;

public class MouseStateHandler : MonoBehaviour
{
    public bool active;
    InputAction click;

    public PlayerEconomy bank;
    public GameObject towerPrefab;

    public bool debug;

    [Tooltip("Extra empty space required around a new tower, in world units.")]
    [Min(0)] public float placementClearance = .05f;

    void Awake()
    {
        click = new InputAction("Click", InputActionType.Value, expectedControlType: "Vector2");
        click.AddCompositeBinding("OneModifier")
            .With("Binding", "<Mouse>/position")
            .With("Modifier","<Mouse>/leftButton");

        click.performed += (InputAction.CallbackContext ct) =>
        {
            if (active)
            {
                Vector2 mPos = ct.ReadValue<Vector2>();
                Vector3 wPos = Camera.main.ScreenToWorldPoint(new Vector3(mPos.x, mPos.y, 20));
                ActiveState(wPos,mPos);
                active = false;
            }
        };
        click.canceled += (InputAction.CallbackContext ct) =>
        {
            Activate();
        };

        click.Enable();
    }

    public void Activate()
    {
        active = true;
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

        if (!TowerPlacement.CanPlace(towerPrefab, position, Quaternion.identity, placementClearance, out string reason))
        {
            Debug.Log("Cannot place tower: " + reason);
            return false;
        }

        if (!bank.Purchase(towerPrefab.name))
        {
            Debug.Log("Insufficient funds to buy tower!");
            return false;
        }

        Instantiate(towerPrefab, position, Quaternion.identity);
        return true;
    }
}
