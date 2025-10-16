using Mirror;
using UnityEngine;
using UnityEngine.InputSystem; // NEW INPUT SYSTEM

/// Attach to your Player prefab (cube) that is assigned in NetworkManager.
/// - Uses the New Input System (no legacy Input calls).
/// - Only the local player instance reads input and moves the Rigidbody.
/// - Sync position/rotation with a NetworkTransform component.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerCubeController : NetworkBehaviour
{
    [Header("Movement")]
    [Tooltip("Base movement speed on the XZ plane.")]
    [SerializeField] private float moveSpeed = 6f;

    [Tooltip("Max speed clamp on the XZ plane.")]
    [SerializeField] private float maxSpeed = 8f;

    [Tooltip("How fast we accelerate toward desired velocity.")]
    [SerializeField] private float acceleration = 25f;

    [Tooltip("Rigidbody drag (helps damp input).")]
    [SerializeField] private float drag = 5f;

    private Rigidbody rb;

    // Input System action for movement (WASD/Arrows/Gamepad left stick)
    private InputAction moveAction;
    private Vector2 moveInput; // x = horizontal, y = vertical

    public override void OnStartLocalPlayer()
    {
        // Only enable logic on the local player's instance.
        enabled = true;

        // Create and configure the input action the first time we own a local player.
        if (moveAction == null)
            CreateMoveAction();

        moveAction.Enable();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Start disabled so remote (non-local) players don't run Update/FixedUpdate.
        enabled = false;

        // Stable upright cube on a plane.
        rb.useGravity = false; // turn on if you need gravity
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = drag;
    }

    private void OnDisable()
    {
        if (isLocalPlayer && moveAction != null)
            moveAction.Disable();
    }

    private void CreateMoveAction()
    {
        // 2D Vector composite for keyboard + an extra binding for gamepad
        moveAction = new InputAction("Move");

        // WASD + Arrow keys via 2DVector composite
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/s")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/a")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d")
            .With("Right", "<Keyboard>/rightArrow");

        // Optional: gamepad left stick
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        // Read input from the New Input System
        moveInput = moveAction.ReadValue<Vector2>();
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        // Convert 2D input to XZ plane
        Vector3 desired = new Vector3(moveInput.x, 0f, moveInput.y) * moveSpeed;

        // Accelerate toward desired planar velocity
        Vector3 planarCurrent = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 velChange = desired - planarCurrent;

        float maxDelta = acceleration * Time.fixedDeltaTime;
        if (velChange.sqrMagnitude > maxDelta * maxDelta)
            velChange = velChange.normalized * maxDelta;

        rb.AddForce(new Vector3(velChange.x, 0f, velChange.z), ForceMode.VelocityChange);

        // Clamp max planar speed
        Vector3 planar = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float maxSq = maxSpeed * maxSpeed;
        if (planar.sqrMagnitude > maxSq)
        {
            planar = planar.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(planar.x, rb.linearVelocity.y, planar.z);
        }

        // Keep drag in sync (tweakable in Inspector)
        rb.linearDamping = drag;
    }
}
