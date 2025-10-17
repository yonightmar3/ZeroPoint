using UnityEngine;
using UnityEngine.InputSystem;

public class RTSCameraController : MonoBehaviour
{
    [Header("Pan")]
    public float panSpeed = 20f;
    public float edgeSize = 12f;
    public float fastMult = 2f;

    [Header("Zoom (height-only)")]
    public float zoomStep = 3f;   // meters of height change per wheel notch
    public float minHeight = 10f;
    public float maxHeight = 60f;

    [Header("Bounds")]
    public Vector2 xBounds = new Vector2(-100, 100);
    public Vector2 zBounds = new Vector2(-100, 100);

    Camera cam;

    void Awake()
    {
        cam = Camera.main;
        if (cam == null) cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (cam == null) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        Vector3 pos = cam.transform.position;

        // keyboard + edge pan
        float mult = kb.leftShiftKey.isPressed ? fastMult : 1f;
        float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

        Vector2 mpos = mouse.position.ReadValue();
        if (mpos.x >= Screen.width - edgeSize) h = 1f;
        else if (mpos.x <= edgeSize) h = -1f;
        if (mpos.y >= Screen.height - edgeSize) v = 1f;
        else if (mpos.y <= edgeSize) v = -1f;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 rightFlat = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        pos += (forwardFlat * v + rightFlat * h) * (panSpeed * mult * Time.unscaledDeltaTime);

        // wheel zoom: change height only; ignore if already at cap in that direction
        float wheel = mouse.scroll.ReadValue().y;
        if (wheel > 0.01f && pos.y > minHeight)         // zoom in
            pos.y = Mathf.Max(minHeight, pos.y - zoomStep);
        else if (wheel < -0.01f && pos.y < maxHeight)   // zoom out
            pos.y = Mathf.Min(maxHeight, pos.y + zoomStep);

        // clamp x/z bounds
        pos.x = Mathf.Clamp(pos.x, xBounds.x, xBounds.y);
        pos.z = Mathf.Clamp(pos.z, zBounds.x, zBounds.y);

        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
    }
}
