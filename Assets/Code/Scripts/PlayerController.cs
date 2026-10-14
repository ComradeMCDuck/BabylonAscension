using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private EntityData playerData;
    private PlayerVariables playerVariables;
    [SerializeField] private Transform spriteVisual; // the child holding the SpriteRenderer

    [Header("Ground check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("Jump feel")]
    [SerializeField] private float coyoteTime = 0.1f; // can still jump just after leaving a ledge
    [SerializeField] private float jumpBufferTime = 0.1f; // jump pressed just before landing should still count

    [Header("Momentum and recoil")]
    [SerializeField] private float groundAcceleration = 60f;
    [SerializeField] private float groundDeceleration = 70f;
    [SerializeField] private float airAcceleration = 25f;
    [SerializeField] private float airDeceleration = 2f;

    [Tooltip("Maximum combined horizontal and vertical speed.")]
    [SerializeField] private float maxPlanarSpeed = 18f;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    private Rigidbody playerRigidbody;
    private Collider playerCollider;
    private SpriteRenderer spriteRenderer;

    private Vector2 moveInput;

    private float nextDashTime;
    private float coyoteCounter;
    private float jumpBufferCounter;
    void Start()
    {
        playerVariables = GetComponentInChildren<PlayerVariables>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
        dashAction = InputSystem.actions.FindAction("Sprint");

        if (playerVariables == null) Debug.LogWarning("Can't find PlayerVariables in " + gameObject);
        if (playerRigidbody == null) Debug.LogWarning("Can't find Rigidbody in " + gameObject);
        if (playerCollider == null) Debug.LogWarning("Can't find Collider in " + gameObject);
        if (spriteRenderer == null) Debug.LogWarning("Can't find SpriteRenderer in children of " + gameObject);
        if (moveAction == null) Debug.LogWarning("Can't find 'Move' in InputActions");
        if (jumpAction == null) Debug.LogWarning("Can't find 'Jump' in InputActions");
    }
    private void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();

        if (jumpAction.WasPressedThisFrame()) jumpBufferCounter = jumpBufferTime;

        if (dashAction.WasPressedThisFrame()) playerVariables.DashRequested = true;

        if (dashAction.WasReleasedThisFrame()) playerVariables.DashRequested = false;
    }

    void FixedUpdate()
    {
        CheckGround();
        HandleTimers();

        if (playerVariables.IsDashing)
        {
            playerVariables.DashRequested = false;
        }
        else
        {
            playerRigidbody.useGravity = true;
            MoveHorizontally();
            HandleJump();
            ApplyGravityModifiers();
            if (Mathf.Abs(playerRigidbody.linearVelocity.x) > 0f) HandleDash();
        }

        FlipSprite();
    }
    private void CheckGround()
    {
        Bounds bound = playerCollider.bounds;
        Vector3 feet = new Vector3(bound.center.x, bound.min.y, bound.center.z);
        playerVariables.IsGrounded = Physics.CheckSphere(feet, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
    }
    #region code summary
    /// <summary>
    /// Handles the coyote time and jump buffer timers, allowing for more forgiving jump mechanics.
    /// </summary>
#endregion
    private void HandleTimers()
    {
        coyoteCounter = playerVariables.IsGrounded ? coyoteTime : coyoteCounter - Time.fixedDeltaTime;
        jumpBufferCounter -= Time.fixedDeltaTime;
    }

    private void MoveHorizontally()
    {
        Vector3 velocity = playerRigidbody.linearVelocity;

        float targetSpeed = moveInput.x * playerData.moveSpeed;

        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            // Accelerate toward the desired movement speed.
            float acceleration = playerVariables.IsGrounded
                ? groundAcceleration
                : airAcceleration;

            velocity.x = Mathf.MoveTowards(
                velocity.x,
                targetSpeed,
                acceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            // Preserve recoil momentum in the air.
            // On the ground, stop more quickly.
            float deceleration = playerVariables.IsGrounded
                ? groundDeceleration
                : airDeceleration;

            velocity.x = Mathf.MoveTowards(
                velocity.x,
                0f,
                deceleration * Time.fixedDeltaTime
            );
        }

        velocity.z = 0f;
        playerRigidbody.linearVelocity = velocity;
    }

    private void HandleJump()
    {
        Vector3 velocity = playerRigidbody.linearVelocity;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            velocity.y = playerData.jumpSpeed;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f; // prevents double jumping
        }

        playerRigidbody.linearVelocity = velocity;
    }
    #region code summary
    /// <summary>
    /// Applies gravity modifiers to the player's vertical velocity based on whether they are rising or falling.
    /// </summary>
#endregion
    private void ApplyGravityModifiers()
    {
        Vector3 velocity = playerRigidbody.linearVelocity;
        float multiplier = velocity.y < 0f ? playerData.fallMultiplier : playerData.riseMultiplier;

        velocity.y += Physics.gravity.y * (multiplier - 1f) * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -playerData.maxFallSpeed);
        playerRigidbody.linearVelocity = velocity;
    }
    #region code summary
    /// <summary>
    /// Checks if the player has requested a dash and initiates the dash if possible. 
    /// The dash can only be performed if the cooldown period has passed since the last dash. 
    /// If the player is currently dashing, the dash request is ignored. 
    /// The direction of the dash is determined by the player's input or facing direction.
    /// </summary>
#endregion
    private void HandleDash()
    {
        if (!playerVariables.DashRequested) return;
        playerVariables.DashRequested = false;

        if (Time.time < nextDashTime) return;
        StartCoroutine(DashCoroutine());
    }
    #region code summary
    /// <summary>
    /// This coroutine handles the dash mechanics. 
    /// It sets the player's dashing state, calculates the direction of the dash based on input or facing direction, and applies a high-speed movement for a short duration. 
    /// After the dash duration ends, it resets the dashing state.
    /// </summary>
    /// <returns></returns>
#endregion
    private IEnumerator DashCoroutine()
    {
        playerVariables.IsDashing = true;
        nextDashTime = Time.time + playerData.nextDashCooldown;

        // Dash where the player is pushing, or where they're facing if standing still
        float direction = Mathf.Abs(moveInput.x) > 0.01f ? Mathf.Sign(moveInput.x) : (spriteRenderer.flipX ? -1f : 1f);

        float endTime = Time.time + playerData.dashDuration;

        while (Time.time < endTime)
        {
            playerRigidbody.linearVelocity = new Vector3(direction * playerData.dashSpeed, 0f, 0f);
            yield return new WaitForFixedUpdate();
        }

        playerVariables.IsDashing = false;
    }
    #region code summary
    /// <summary>
    /// Flips the player's sprite based on their horizontal movement input.
    /// </summary>
#endregion
    private void FlipSprite()
    {
        // BowCombat owns the facing direction when attached.
        if (GetComponent<BowCombat>() != null)
            return;

        if (moveInput.x > 0.01f)
            SetFacing(false);
        else if (moveInput.x < -0.01f)
            SetFacing(true);
    }
    #region code summary
    /// <summary>
    /// Sets the player's facing direction and updates the sprite's scale accordingly.
    /// </summary>
    /// <param name="facingLeft">If true, the player will face left; otherwise, they will face right.</param>
#endregion
    private void SetFacing(bool facingLeft)
    {
        playerVariables.FlipX = facingLeft;

        Vector3 scale = spriteVisual.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingLeft ? -1f : 1f);
        spriteVisual.localScale = scale;
    }


    public void ApplyBowRecoil(Vector3 impulse)
    {
        if (playerRigidbody == null) return;

        playerRigidbody.AddForce(impulse, ForceMode.Impulse);

        // Limit total speed in the gameplay plane (X/Y).
        Vector3 velocity = playerRigidbody.linearVelocity;
        Vector2 planarVelocity = new Vector2(velocity.x, velocity.y);

        if (planarVelocity.magnitude > maxPlanarSpeed)
        {
            planarVelocity = planarVelocity.normalized * maxPlanarSpeed;

            velocity.x = planarVelocity.x;
            velocity.y = planarVelocity.y;

            playerRigidbody.linearVelocity = velocity;
        }
    }

}
