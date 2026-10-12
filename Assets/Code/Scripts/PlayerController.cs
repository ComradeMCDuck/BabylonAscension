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
    [SerializeField] private float coyoteTime = 0.1f;       // can still jump just after leaving a ledge
    [SerializeField] private float jumpBufferTime = 0.1f;   // jump pressed just before landing still counts

    [Header("Ledge grab")]
    [SerializeField] private Vector2 ledgeClimbOffset = new Vector2(0.5f, 1.2f); // forward, up
    [SerializeField] private float ledgeRegrabCooldown = 0.25f;


    [Header("Momentum and recoil")]
    [SerializeField] private float groundAcceleration = 60f;
    [SerializeField] private float groundDeceleration = 70f;
    [SerializeField] private float airAcceleration = 25f;
    [SerializeField] private float airDeceleration = 2f;

    [Tooltip("Maximum combined horizontal and vertical speed.")]
    [SerializeField] private float maxPlanarSpeed = 18f;


    private float recoilControlTimer;
    private float nextLedgeGrabTime;

    

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    private Rigidbody rb;
    private Collider col;
    private SpriteRenderer spriteRenderer;

    private Vector2 moveInput;

    private float nextDashTime;
    private float coyoteCounter;
    private float jumpBufferCounter;
    void Start()
    {
        playerVariables = GetComponentInChildren<PlayerVariables>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
        dashAction = InputSystem.actions.FindAction("Sprint");

        if (playerVariables == null) Debug.LogWarning("Can't find PlayerVariables in " + gameObject);
        if (rb == null) Debug.LogWarning("Can't find Rigidbody in " + gameObject);
        if (col == null) Debug.LogWarning("Can't find Collider in " + gameObject);
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

    // Update is called once per frame
    void FixedUpdate()
    {
        CheckGround();
        HandleTimers();

        Vector3 ledgeAngle = playerVariables.UpperPointOfRaycast - playerVariables.LowerPointOfRaycast;
        float angle = Mathf.Atan2(ledgeAngle.y, ledgeAngle.x) * Mathf.Rad2Deg;

        if (playerVariables.IsDashing)
        {
            playerVariables.DashRequested = false;
        }
        else if (Time.time >= nextLedgeGrabTime && playerVariables.FacingWall && playerVariables.FoundSpaceAboveWall && Mathf.Abs(angle) > 89f && Mathf.Abs(angle) < 91f)
        {
            HandleLedgeGrabbing();
        }
        else
        {
            rb.useGravity = true;
            MoveHorizontally();
            HandleJump();
            ApplyGravityModifiers();
            if (Mathf.Abs(rb.linearVelocity.x) > 0f) HandleDash();
        }

        FlipSprite();
    }

    private void HandleLedgeGrabbing()
    {
        rb.linearVelocity = Vector3.zero;
        rb.useGravity = false;

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter = 0f; // reset it, so it can't also trigger a jump

            float dir = playerVariables.FlipX ? -1f : 1f;
            rb.position += new Vector3(dir * ledgeClimbOffset.x, ledgeClimbOffset.y, 0f);

            rb.useGravity = true;
            nextLedgeGrabTime = Time.time + ledgeRegrabCooldown;
        }
    }

    private void CheckGround()
    {
        Bounds b = col.bounds;
        Vector3 feet = new Vector3(b.center.x, b.min.y, b.center.z);
        playerVariables.IsGrounded = Physics.CheckSphere(feet, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void HandleTimers()
    {
        coyoteCounter = playerVariables.IsGrounded ? coyoteTime : coyoteCounter - Time.fixedDeltaTime;
        jumpBufferCounter -= Time.fixedDeltaTime;
    }


    private void MoveHorizontally()
    {
        Vector3 velocity = rb.linearVelocity;

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
        rb.linearVelocity = velocity;
    }


    private void HandleJump()
    {
        Vector3 velocity = rb.linearVelocity;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            velocity.y = playerData.jumpSpeed;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f; // prevents double jumping
        }

        rb.linearVelocity = velocity;
    }

    private void ApplyGravityModifiers()
    {
        Vector3 velocity = rb.linearVelocity;
        float multiplier = velocity.y < 0f ? playerData.fallMultiplier : playerData.riseMultiplier;

        velocity.y += Physics.gravity.y * (multiplier - 1f) * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -playerData.maxFallSpeed);
        rb.linearVelocity = velocity;
    }

    private void HandleDash()
    {
        if (!playerVariables.DashRequested) return;
        playerVariables.DashRequested = false;

        if (Time.time < nextDashTime) return; // dash cooldown not finished
        StartCoroutine(DashCoroutine());
    }

    private IEnumerator DashCoroutine()
    {
        playerVariables.IsDashing = true;
        nextDashTime = Time.time + playerData.nextDashCooldown;

        // Dash where the player is pushing, or where they're facing if standing still
        float direction = Mathf.Abs(moveInput.x) > 0.01f ? Mathf.Sign(moveInput.x) : (spriteRenderer.flipX ? -1f : 1f);

        float endTime = Time.time + playerData.dashDuration;

        while (Time.time < endTime)
        {
            rb.linearVelocity = new Vector3(direction * playerData.dashSpeed, 0f, 0f); // y = 0 keeps it level
            yield return new WaitForFixedUpdate(); // stay in sync with physics
        }

        playerVariables.IsDashing = false; // normal movement takes over on the next physics step
    }

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
    private void SetFacing(bool facingLeft)
    {
        playerVariables.FlipX = facingLeft;

        Vector3 scale = spriteVisual.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingLeft ? -1f : 1f);
        spriteVisual.localScale = scale;
    }


    public void ApplyBowRecoil(Vector3 impulse)
    {
        if (rb == null) return;

        rb.AddForce(impulse, ForceMode.Impulse);

        // Limit total speed in the gameplay plane (X/Y).
        Vector3 velocity = rb.linearVelocity;
        Vector2 planarVelocity = new Vector2(velocity.x, velocity.y);

        if (planarVelocity.magnitude > maxPlanarSpeed)
        {
            planarVelocity = planarVelocity.normalized * maxPlanarSpeed;

            velocity.x = planarVelocity.x;
            velocity.y = planarVelocity.y;

            rb.linearVelocity = velocity;
        }
    }

}
