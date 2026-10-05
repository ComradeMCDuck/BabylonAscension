using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private EntityData playerData;

    [Header("Ground check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("Jump feel")]
    [SerializeField] private float coyoteTime = 0.1f;       // can still jump just after leaving a ledge
    [SerializeField] private float jumpBufferTime = 0.1f;   // jump pressed just before landing still counts
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f; // shorter jump if released early

    private InputAction moveAction;
    private InputAction jumpAction;
    private Rigidbody rb;
    private Collider col;
    private SpriteRenderer spriteRenderer;

    private Vector2 moveInput;
    private bool isGrounded;
    private bool jumpCutRequested;
    private float coyoteCounter;
    private float jumpBufferCounter;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");

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
            jumpCutRequested = true;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        CheckGround();
        HandleTimers();
        MoveHorizontally();
        HandleJump();
        FlipSprite();
    }

    private void CheckGround()
    {
        Bounds b = col.bounds;
        Vector3 feet = new Vector3(b.center.x, b.min.y, b.center.z);
        isGrounded = Physics.CheckSphere(feet, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void HandleTimers()
    {
        coyoteCounter = isGrounded ? coyoteTime : coyoteCounter - Time.fixedDeltaTime;
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
            jumpCutRequested = false;
        }
        else if (jumpCutRequested)
        {
            if (velocity.y > 0f) velocity.y *= jumpCutMultiplier; // released early: cut the jump short
            jumpCutRequested = false;
        }

        rb.linearVelocity = velocity;
    }

    private void FlipSprite()
    {
        if (moveInput.x > 0.01f) spriteRenderer.flipX = false;
        else if (moveInput.x < -0.01f) spriteRenderer.flipX = true;
    }
}
