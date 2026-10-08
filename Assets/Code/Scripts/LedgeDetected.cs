using UnityEngine;

public class LedgeDetected : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;

    private PlayerVariables playerVariables;
    private bool isUpper;
    private int overlaps;

    private void Start()
    {
        playerVariables = FindAnyObjectByType<PlayerVariables>();
        isUpper = CompareTag("UpperLedgeCollider"); // differentiate between upper and lower ledge colliders
        CheckIfTouchingWall(); // set initial state
    }

    private void Update()
    {
        Vector3 position = transform.localPosition;
        position.x = Mathf.Abs(position.x) * (playerVariables.FlipX ? -1f : 1f);
        transform.localPosition = position;
    }

    private void FixedUpdate()
    {
        Vector3 direction = playerVariables.FlipX ? Vector3.left : Vector3.right;
        if (!Physics.Raycast(transform.position, direction, out RaycastHit hit, Mathf.Infinity, layerMask)) return;

        Debug.DrawRay(transform.position, direction * hit.distance, Color.yellow);
        if (isUpper) playerVariables.UpperPointOfRaycast = hit.point;
        else playerVariables.LowerPointOfRaycast = hit.point;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!InMask(other)) return;
        overlaps++;
        CheckIfTouchingWall();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!InMask(other)) return;
        overlaps--;
        CheckIfTouchingWall();
    }

    private bool InMask(Collider collider) => (layerMask.value & (1 << collider.gameObject.layer)) != 0;

    private void CheckIfTouchingWall()
    {
        bool touchingDesiredWall = overlaps > 0;
        if (isUpper) playerVariables.FoundSpaceAboveWall = !touchingDesiredWall;
        else playerVariables.FacingWall = touchingDesiredWall;
    }
}