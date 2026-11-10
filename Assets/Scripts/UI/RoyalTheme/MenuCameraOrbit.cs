using UnityEngine;

// Main menu background camera: looks down at the castle like the in-game view and slowly
// swings left and right. The castle is kept on the right so the menu panel never covers it.
// It only swings over the side of the map that has trees, so the edge of the map never shows.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class MenuCameraOrbit : MonoBehaviour
{
    public Transform target;
    [Tooltip("Horizontal distance from the castle.")]
    public float distance = 20f;
    public float height = 17f;
    [Tooltip("Direction the camera faces at the middle of the swing (degrees, 45 = like the game).")]
    public float centreYaw = 45f;
    [Tooltip("How far it swings to each side (degrees).")]
    public float swing = 25f;
    [Tooltip("Seconds for one full left-right-left swing.")]
    [Min(1f)] public float period = 60f;
    [Tooltip("Shifts the view so the castle sits on the right of the screen (world units).")]
    public float screenShift = 7f;
    public float lookHeight = 1.5f;
    [Range(20f, 70f)] public float fieldOfView = 40f;

    void OnEnable() => Apply(0f);
    void OnValidate() => Apply(0f);

    void LateUpdate()
    {
        Apply(Application.isPlaying ? Time.unscaledTime : 0f);
    }

    void Apply(float time)
    {
        if (!target) return;
        float yaw = centreYaw + swing * Mathf.Sin(time * 2f * Mathf.PI / period);
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = flat * Vector3.forward;
        Vector3 right = flat * Vector3.right;
        Vector3 centre = target.position - right * screenShift;
        Vector3 position = centre - forward * distance + Vector3.up * height;
        Vector3 look = centre + Vector3.up * lookHeight;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position));
        var cam = GetComponent<Camera>();
        if (cam) cam.fieldOfView = fieldOfView;
    }
}
