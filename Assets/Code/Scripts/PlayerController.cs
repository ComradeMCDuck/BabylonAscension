using System.Collections;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f; // shorter jump if released early
    
    [Header("Ledge grab")]
    [SerializeField] private Vector2 ledgeClimbOffset = new Vector2(0.5f, 1.2f); // forward, up
    [SerializeField] private float ledgeRegrabCooldown = 0.25f;
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

        if (jumpAction.WasPressedThisFrame())
            jumpBufferCounter = jumpBufferTime;

        if (jumpAction.WasReleasedThisFrame())
            playerVariables.JumpCutRequested = true;

        if (dashAction.WasPressedThisFrame())
        {
            playerVariables.DashRequested = true;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        CheckGround();
        HandleTimers();

        if (playerVariables.IsDashing)
        {
            playerVariables.DashRequested = false;
        }
        else if (Time.time >= nextLedgeGrabTime && playerVariables.FacingWall && playerVariables.FoundSpaceAboveWall)
        {
            HandleLedgeGrabbing();
        }
        else
        {
            rb.useGravity = true;
            MoveHorizontally();
            HandleJump();
            HandleDash();
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
            playerVariables.JumpCutRequested = false; // releasing the button shouldn't cut anything

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
        velocity.x = moveInput.x * playerData.moveSpeed;
        velocity.z = 0f; // 2.5D: no depth movement
        rb.linearVelocity = velocity; // Y is untouched, so gravity still works
    }

    private void HandleJump()
    {
        Vector3 velocity = rb.linearVelocity;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            velocity.y = playerData.jumpSpeed;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f; // prevents double jumping
            playerVariables.JumpCutRequested = false;
        }
        else if (playerVariables.JumpCutRequested)
        {
            if (velocity.y > 0f) velocity.y *= jumpCutMultiplier; // released early: cut the jump short
            playerVariables.JumpCutRequested = false;
        }

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
        if (moveInput.x > 0.01f) SetFacing(false);
        else if (moveInput.x < -0.01f) SetFacing(true);
    }
    private void SetFacing(bool facingLeft)
    {
        playerVariables.FlipX = facingLeft;

        Vector3 scale = spriteVisual.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingLeft ? -1f : 1f);
        spriteVisual.localScale = scale;
    }
}
