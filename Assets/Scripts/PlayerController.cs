using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private InputAction moveAction;

    public float groundDist = 1.0f;
    public float moveSpeed = 5.0f;

    public LayerMask groundLayer;
    private Rigidbody rb;
    private SpriteRenderer spriteRenderer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning("Can't find Rigidbody in " + gameObject);
        }
        spriteRenderer = gameObject.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning("Can't find sprite renderer in " + gameObject + " children");
        }
        moveAction = InputSystem.actions.FindAction("Move");
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // 1. Read input and map it to the ground plane (XZ)
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0f, input.y);

        // 2. Calculate the next position
        Vector3 newPos = rb.position + move * moveSpeed * Time.fixedDeltaTime;

        // 3. Snap to ground height at that new position
        Vector3 castPos = newPos + Vector3.up;
        if (Physics.Raycast(castPos, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundLayer))
        {
            newPos.y = hit.point.y + groundDist;
        }

        // 4. Move via the Rigidbody
        rb.MovePosition(newPos);
    }
}
