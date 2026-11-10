using UnityEngine;
using UnityEngine.InputSystem;

// RTS-style camera: fixed oblique view (no zoom, no rotation), moved with W/A/S/D only.
// The view starts on the base and can move at most `maxOffset` units up, down, left or right on screen.
[RequireComponent(typeof(Camera))]
public class RTSCameraController : MonoBehaviour
{
    [Header("View angle (fixed during play)")]
    [Tooltip("Downward tilt in degrees. 90 = straight down.")]
    [Range(20f, 85f)] public float pitch = 50f;
    [Tooltip("Horizontal facing in degrees. 45 = diagonal (diamond-shaped map).")]
    public float yaw = 45f;
    [Range(15f, 90f)] public float fieldOfView = 35f;
    [Tooltip("Distance from the camera to the ground point it looks at.")]
    [Min(1f)] public float distance = 43.75f;

    [Header("Movement")]
    [Tooltip("Pan speed in world units per second (real time, so it also works while the tutorial pauses the game).")]
    [Min(0f)] public float panSpeed = 12f;
    [Tooltip("How far the view may move from the start point in each direction (up, down, left, right).")]
    [Min(0f)] public float maxOffset = 10f;

    [Tooltip("Camera starts centred on this (drag the Base here). Empty = world origin.")]
    public Transform startFocus;

    static RTSCameraController instance;

    Camera cam;
    Vector3 start;
    float shakeAmplitude, shakeDuration, shakeEnds;
    Vector2 offset;   // x = screen right, y = screen up, each kept within ±maxOffset

    // Short screen shake for impacts. Uses real time, so it also works while the game is paused.
    public static void Shake(float amplitude, float duration)
    {
        if (!instance || amplitude <= 0f || duration <= 0f) return;
        // A weaker shake never cuts a stronger one that is still running.
        float remaining = Mathf.Max(0f, instance.shakeEnds - Time.unscaledTime);
        float running = instance.shakeDuration > 0f ? instance.shakeAmplitude * remaining / instance.shakeDuration : 0f;
        if (amplitude < running) return;
        instance.shakeAmplitude = amplitude;
        instance.shakeDuration = duration;
        instance.shakeEnds = Time.unscaledTime + duration;
    }

    void Awake()
    {
        instance = this;
        cam = GetComponent<Camera>();
        start = startFocus ? new Vector3(startFocus.position.x, 0f, startFocus.position.z) : Vector3.zero;
        Apply();
    }

    void LateUpdate()
    {
        offset += ReadMoveInput() * panSpeed * Time.unscaledDeltaTime;
        offset.x = Mathf.Clamp(offset.x, -maxOffset, maxOffset);
        offset.y = Mathf.Clamp(offset.y, -maxOffset, maxOffset);
        Apply();
        ApplyShake();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Jitters the camera on screen axes; the strength fades out linearly. Apply() resets the position every frame.
    void ApplyShake()
    {
        float remaining = shakeEnds - Time.unscaledTime;
        if (remaining <= 0f || shakeDuration <= 0f) return;
        Vector2 jitter = Random.insideUnitCircle * (shakeAmplitude * remaining / shakeDuration);
        transform.position += transform.right * jitter.x + transform.up * jitter.y;
    }

    // W = screen up, S = screen down, A = screen left, D = screen right. The mouse never moves the camera.
    Vector2 ReadMoveInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;

        Vector2 dir = Vector2.zero;
        if (keyboard.wKey.isPressed) dir.y += 1f;
        if (keyboard.sKey.isPressed) dir.y -= 1f;
        if (keyboard.dKey.isPressed) dir.x += 1f;
        if (keyboard.aKey.isPressed) dir.x -= 1f;
        return Vector2.ClampMagnitude(dir, 1f);
    }

    void Apply()
    {
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        Vector3 focus = start + flat * new Vector3(offset.x, 0f, offset.y);
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
        if (cam)
        {
            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
        }
    }

    // Right-click the component header in the Inspector to preview the start view without pressing Play.
    [ContextMenu("Preview start view in editor")]
    void PreviewStartView()
    {
        cam = GetComponent<Camera>();
        start = startFocus ? new Vector3(startFocus.position.x, 0f, startFocus.position.z) : Vector3.zero;
        offset = Vector2.zero;
        Apply();
    }
}
