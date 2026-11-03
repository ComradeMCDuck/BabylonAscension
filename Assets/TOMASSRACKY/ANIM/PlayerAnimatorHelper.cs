using UnityEngine;
using DYP;

// Attach to the same GameObject that has Animator, BowCombat.
[RequireComponent(typeof(Animator))]
public class PlayerAnimatorHelper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BasicMovementController2D movementController;
    [SerializeField] private Animator animator;

    private void Awake()
    {
        if (movementController == null)
            movementController = GetComponentInParent<BasicMovementController2D>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (movementController == null)
            Debug.LogError("PlayerAnimatorHelper: No BasicMovementController2D found.", this);
        if (animator == null)
            Debug.LogError("PlayerAnimatorHelper: No Animator found.", this);
    }

    private void Update()
    {
        if (movementController == null || animator == null) return;

        // Pass velocity floats
        Vector3 vel = movementController.InputVelocity;
        animator.SetFloat("MoveX", vel.x);
        animator.SetFloat("MoveY", vel.y);

        // Compute jump/fall booleans from the controller
        bool isJumping = !movementController.IsOnGround() && vel.y > 0f;
        bool isFalling = !movementController.IsOnGround() && vel.y < 0f;
        animator.SetBool("isJumping", isJumping);
        animator.SetBool("isFalling", isFalling);

        // Set grounded state directly from the controller
        bool grounded = movementController.IsOnGround();
        animator.SetBool("isGrounded", grounded);

    }
}
