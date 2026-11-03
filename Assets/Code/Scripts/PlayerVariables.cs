using UnityEngine;

public class PlayerVariables : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isDashing;
    [SerializeField] private bool isGrabbing;
    [SerializeField] private bool dashRequested;
    [SerializeField] private bool foundSpaceAboveWall;
    [SerializeField] private bool facingWall;
    [SerializeField] private bool flipX;

    public bool IsGrounded { get => isGrounded; set => isGrounded = value; }
    public bool IsDashing { get => isDashing; set => isDashing = value; }
    public bool DashRequested { get => dashRequested; set => dashRequested = value; }
    public bool IsGrabbing { get => isGrabbing; set => isGrabbing = value; }
    public bool FoundSpaceAboveWall { get => foundSpaceAboveWall; set => foundSpaceAboveWall = value; }
    public bool FacingWall { get => facingWall; set => facingWall = value; }
    public bool FlipX { get => flipX; set => flipX = value; }
    private void Update()
    {
        if (animator != null)
        {
            animator.SetBool("isGrounded", isGrounded);
            animator.SetBool("isDashing", isDashing);
            animator.SetBool("isGrabbing", isGrabbing);
            animator.SetBool("dashRequested", dashRequested);
            animator.SetBool("foundSpaceAboveWall", foundSpaceAboveWall);
            animator.SetBool("facingWall", facingWall);
            animator.SetBool("flipX", flipX);
        }
    }
}