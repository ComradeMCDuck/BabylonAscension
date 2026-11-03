using DYP;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns the player's body visual towards the mouse cursor and tells the
/// movement controller which way the player is facing (used for dashing).
/// </summary>
public class PlayerFacing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [Tooltip("Body visual that gets flipped. The bow pivot should not be a child of it.")]
    [SerializeField] private Transform spriteVisual;
    [SerializeField] private BasicMovementController2D movementController;

    private int facingDirection = 1;

    public int FacingDirection => facingDirection;
    public bool FacingLeft => facingDirection < 0;

    private void Awake()
    {
        if (movementController == null)
            movementController = GetComponentInParent<BasicMovementController2D>();

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (aimCamera == null)
            Debug.LogError("PlayerFacing needs an aim camera.", this);
    }

    private void OnDisable()
    {
        // Hand dash direction back to the controller's own logic.
        if (movementController != null)
            movementController.AimFacingDirection = 0;
    }

    private void Update()
    {
        // Paused or respawning (GameManager freezes time for both): keep the current facing.
        if (Time.timeScale == 0f)
            return;

        if (Mouse.current == null || aimCamera == null)
            return;

        // Intersect the camera ray with the player's XY movement plane.
        Plane aimPlane = new Plane(
            Vector3.forward,
            new Vector3(0f, 0f, transform.position.z)
        );

        Ray ray = aimCamera.ScreenPointToRay(
            Mouse.current.position.ReadValue()
        );

        if (!aimPlane.Raycast(ray, out float distance))
            return;

        float deltaX = ray.GetPoint(distance).x - transform.position.x;

        // Keep the current facing when the cursor is right above/below the player.
        if (Mathf.Abs(deltaX) > 0.01f)
            facingDirection = deltaX < 0f ? -1 : 1;

        ApplyFacing();
    }

    private void ApplyFacing()
    {
        if (spriteVisual != null)
        {
            Vector3 scale = spriteVisual.localScale;
            scale.x = Mathf.Abs(scale.x) * facingDirection;
            spriteVisual.localScale = scale;
        }

        if (movementController != null)
            movementController.AimFacingDirection = facingDirection;
    }
}
