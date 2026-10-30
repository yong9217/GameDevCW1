using UnityEngine;
using UnityEngine.InputSystem;

public class MouseStateHandler : MonoBehaviour
{
    public bool active;
    InputAction click;

    public PlayerEconomy bank;
    public GameObject towerPrefab;

    public bool debug;

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
        
        if (bank.Purchase(towerPrefab.name))
        {
            Instantiate(towerPrefab, new Vector3(wpos.x,0,wpos.z), Quaternion.identity);
        } else
        {
            Debug.Log("Insufficient funds to buy tower!");
        }
    }
}
