using UnityEngine;
using UnityEngine.InputSystem;

public class TestTimer : MonoBehaviour
{
    public ReccurTimer timer;
    public string t;

    void Start()
    {
        InputAction ia = new InputAction("test2");
        ia.AddCompositeBinding("ButtonWithOneModifier").With("Modifier", "<Keyboard>/a").With("Button", "<Keyboard>/b");

        ia.performed += (InputAction.CallbackContext ct) =>
        {
            Debug.Log("hi");
        };

        ia.Enable();

        timer.OnTimeInvoke(5f, () => Debug.Log(t));
    }
}
