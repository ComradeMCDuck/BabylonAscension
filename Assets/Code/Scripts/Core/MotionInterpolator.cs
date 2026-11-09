using UnityEngine;

/// <summary>
/// Smooths the visuals of an object that is moved in FixedUpdate (like CharacterMotor2D, which sets the
/// transform directly, so Rigidbody2D interpolation can't help). The object itself still moves in physics
/// steps; every rendered frame, the listed children are placed between the previous and current physics
/// position instead, so they move smoothly at any frame rate. Visuals trail the real position by at most
/// one physics step.
///
/// Put it on the moving root and list the children that are only visual or follow targets
/// (sprite, bow, camera follow point). Don't list colliders or anything gameplay reads positions from.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class MotionInterpolator : MonoBehaviour
{
    [Tooltip("Children drawn at the interpolated position, e.g. Appearance, BowPivot and CameraFollow.")]
    [SerializeField] private Transform[] smoothedChildren;
    [Tooltip("Moves bigger than this in one physics step (respawns, teleports) snap instead of sliding across.")]
    [SerializeField, Min(0f)] private float snapDistance = 3f;

    private Vector3[] restLocalPositions;
    private Vector3 previousPosition;

    private void Awake()
    {
        restLocalPositions = new Vector3[smoothedChildren.Length];
        for (int i = 0; i < smoothedChildren.Length; i++)
        {
            if (smoothedChildren[i] != null) restLocalPositions[i] = smoothedChildren[i].localPosition;
        }
    }

    private void OnEnable()
    {
        Snap();
    }

    private void OnDisable()
    {
        RestoreChildren();
    }

    /// <summary>
    /// Call after teleporting the object so the visuals jump with it instead of sliding.
    /// </summary>
    public void Snap()
    {
        previousPosition = transform.position;
    }

    // Runs before every other FixedUpdate (execution order -1000), so the motor moves the root
    // from here and gameplay code always sees the children at their real offsets.
    private void FixedUpdate()
    {
        RestoreChildren();
        previousPosition = transform.position;
    }

    // Update runs after all of this frame's physics steps and before LateUpdate,
    // so Cinemachine (in LateUpdate) already follows the smoothed CameraFollow point.
    private void Update()
    {
        Vector3 current = transform.position;

        // Respawns and teleports snap instead of sliding across the level.
        if ((current - previousPosition).sqrMagnitude > snapDistance * snapDistance) previousPosition = current;

        // How far the current frame is between the last physics step and the next one.
        float t = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
        // The children have to sit (1 - t) of the last step behind the root.
        Vector3 worldOffset = (previousPosition - current) * (1f - t);
        Vector3 localOffset = transform.InverseTransformVector(worldOffset);

        for (int i = 0; i < smoothedChildren.Length; i++)
        {
            if (smoothedChildren[i] != null) smoothedChildren[i].localPosition = restLocalPositions[i] + localOffset;
        }
    }

    private void RestoreChildren()
    {
        if (restLocalPositions == null) return;

        for (int i = 0; i < smoothedChildren.Length; i++)
        {
            if (smoothedChildren[i] != null) smoothedChildren[i].localPosition = restLocalPositions[i];
        }
    }
}
