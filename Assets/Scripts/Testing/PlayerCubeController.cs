using Mirror;
using UnityEngine;

/// Attach to your Player prefab (the cube).
/// Movement is client-authoritative and synced via NetworkTransform/NetworkRigidbody.
[RequireComponent(typeof(Rigidbody))]
public class PlayerCubeController : NetworkBehaviour
{
    [Header("Movement")]
    [Tooltip("Base movement speed on the XZ plane.")]
    public float moveSpeed = 6f;

    [Tooltip("Max speed clamp on the XZ plane.")]
    public float maxSpeed = 8f;

    [Tooltip("How fast we accelerate toward desired velocity.")]
    public float acceleration = 25f;

    [Tooltip("Drag applied by the rigidbody (helps damp input).")]
    public float drag = 5f;

    private Rigidbody rb;
    private Vector3 input;  // XZ input vector

    public override void OnStartAuthority()
    {
        // Only the owning client should read input & drive movement.
        enabled = true;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Start disabled so non-owners don't run Update/FixedUpdate.
        enabled = false;

        // Recommended rigidbody defaults for a top-down plane:
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Update()
    {
        if (!hasAuthority) return;

        input.x = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
        input.z = Input.GetAxisRaw("Vertical");   // W/S or Up/Down
        input = Vector3.ClampMagnitude(input, 1f);
    }

    private void FixedUpdate()
    {
        if (!hasAuthority) return;

        // Desired planar velocity
        Vector3 desired = input * moveSpeed;
        Vector3 current = rb.linearVelocity;
        Vector3 planarCurrent = new Vector3(current.x, 0f, current.z);

        // Accelerate toward desired velocity (planar only)
        Vector3 velChange = desired - planarCurrent;
        float maxDelta = acceleration * Time.fixedDeltaTime;
        velChange = Vector3.ClampMagnitude(velChange, maxDelta);

        rb.AddForce(new Vector3(velChange.x, 0f, velChange.z), ForceMode.VelocityChange);

        // Clamp max planar speed
        Vector3 planar = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (planar.sqrMagnitude > maxSpeed * maxSpeed)
        {
            planar = planar.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(planar.x, rb.linearVelocity.y, planar.z);
        }

        rb.linearDamping = drag;
    }
}
