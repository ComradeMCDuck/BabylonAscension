using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hand-placed connection between two platforms that ground enemies use to chase the player, like a NavMesh Link.
/// Place it in the level and drag its Start and End points (Scene view handles) onto the platforms; each point snaps
/// to the ground below it. The direction is worked out from the heights: going up, or across at about the same height,
/// is a jump; going down is a fall (the enemy walks off the edge and steers onto End), unless Always Jump is on.
/// Jumps are launched with the exact arc from Start to End that peaks Arc Height above the higher point (the arc shown
/// in the Scene view), so a link works for any enemy that is allowed to use it, regardless of its own jump height.
/// EnemyBehavior plans routes over all links in the scene (EnemyLinkPlanner) to reach the player's platform.
/// </summary>
public class EnemyLink : MonoBehaviour
{
    // Endpoints look for the ground from this far above them...
    private const float GroundSearchUp = 0.5f;
    // ...down to this far below.
    private const float GroundSearchDown = 5f;
    // A link whose end is less than this much lower than its start is jumped across, not fallen down.
    private const float FallHeightThreshold = 0.5f;
    // Line segments per arc in the Scene view.
    private const int ArcSegments = 24;

    public enum Traversal
    {
        Jump,
        Fall
    }

    [Tooltip("Start point, relative to this object. Drag it in the Scene view; it snaps to the ground below.")]
    [SerializeField] private Vector2 start = new Vector2(-1f, 0f);
    [Tooltip("End point, relative to this object. Drag it in the Scene view; it snaps to the ground below.")]
    [SerializeField] private Vector2 end = new Vector2(1f, 2f);
    [Tooltip("Enemies can also use the link from End to Start (e.g. jump up, and fall back down the same way).")]
    [SerializeField] private bool bidirectional = true;
    [Tooltip("Jump even when going down, instead of walking off the edge (e.g. to hop down over an obstacle).")]
    [SerializeField] private bool alwaysJump;
    [Tooltip("How far above the higher of the two points the jump peaks. Raise it if the arc clips a platform's corner.")]
    [SerializeField, Min(0f)] private float arcHeight = 1f;
    [Tooltip("Only these enemies (by their EntityData) may use the link. Empty = every ground enemy.")]
    [SerializeField] private EntityData[] allowedEnemies = Array.Empty<EntityData>();
    [Tooltip("Layers the points snap to. Defaults to \"Environment\" if left empty.")]
    [SerializeField] private LayerMask groundLayers;

    private static readonly List<EnemyLink> s_Active = new List<EnemyLink>();

    /// <summary>Every enabled link in the loaded scenes.</summary>
    public static IReadOnlyList<EnemyLink> Active => s_Active;

    public bool Bidirectional => bidirectional;
    public float ArcHeight => arcHeight;

    private bool resolved;
    private Vector2 startPoint;
    private Vector2 endPoint;
    private Collider2D startGround;
    private Collider2D endGround;

    /// <summary>Start snapped to the ground (where the enemy's feet are), in world space.</summary>
    public Vector2 StartPoint { get { Resolve(); return startPoint; } }
    public Vector2 EndPoint { get { Resolve(); return endPoint; } }
    public Collider2D StartGround { get { Resolve(); return startGround; } }
    public Collider2D EndGround { get { Resolve(); return endGround; } }

    /// <summary>False if either point has no ground below it; such a link is never used.</summary>
    public bool IsValid => StartGround != null && EndGround != null;

    private void OnEnable()
    {
        resolved = false;
        s_Active.Add(this);
    }

    private void OnDisable()
    {
        s_Active.Remove(this);
    }

    /// <summary>
    /// Snaps both points to the ground, the first time an enemy needs them: while a scene loads, the platforms'
    /// colliders may not be enabled yet when OnEnable runs. Platforms don't move, so it is done once.
    /// </summary>
    private void Resolve()
    {
        if (resolved) return;
        resolved = true;

        startPoint = Snap(transform.TransformPoint(start), out startGround);
        endPoint = Snap(transform.TransformPoint(end), out endGround);

        if (startGround == null || endGround == null)
            Debug.LogWarning($"{name}: EnemyLink {(startGround == null ? "Start" : "End")} has no ground below it, so enemies won't use the link.", this);
    }

    public bool Allows(EntityData data) => allowedEnemies == null || allowedEnemies.Length == 0 || Array.IndexOf(allowedEnemies, data) >= 0;

    public Vector2 From(bool reverse) => reverse ? EndPoint : StartPoint;
    public Vector2 To(bool reverse) => reverse ? StartPoint : EndPoint;
    public Collider2D FromGround(bool reverse) => reverse ? EndGround : StartGround;
    public Collider2D ToGround(bool reverse) => reverse ? StartGround : EndGround;

    public Traversal GetTraversal(bool reverse) => GetTraversal(From(reverse), To(reverse));

    private Traversal GetTraversal(Vector2 from, Vector2 to) =>
        alwaysJump || to.y > from.y - FallHeightThreshold ? Traversal.Jump : Traversal.Fall;

    /// <summary>
    /// Launch velocity for the jump from one point to the other, for an enemy with the given gravity.
    /// It is launched as an impulse with no input afterwards (like the jump attack), so its horizontal speed only
    /// drops by airDeceleration in the air, plus one step of groundDeceleration while it is still touching the ground.
    /// </summary>
    public Vector2 GetJumpVelocity(bool reverse, float gravity, float airDeceleration, float groundDeceleration, out float airTime)
    {
        Vector2 from = From(reverse);
        Vector2 to = To(reverse);
        GetArc(from, to, out float riseSpeed, out airTime, gravity);

        float distance = Mathf.Abs(to.x - from.x);
        // Distance covered while slowing down: distance = v t - d t^2 / 2.
        float speedX = airTime > 0f ? (distance + 0.5f * airDeceleration * airTime * airTime) / airTime : 0f;
        // The momentum would run out before landing: it stops after v^2 / (2 d) instead.
        if (airDeceleration > 0f && speedX < airDeceleration * airTime) speedX = Mathf.Sqrt(2f * airDeceleration * distance);
        speedX += groundDeceleration * Time.fixedDeltaTime;

        return new Vector2(Mathf.Sign(to.x - from.x) * speedX, riseSpeed);
    }

    /// <summary>Rise speed and total air time of the arc from one point to the other that peaks arcHeight above the higher one.</summary>
    private void GetArc(Vector2 from, Vector2 to, out float riseSpeed, out float airTime, float gravity)
    {
        float apex = Mathf.Max(from.y, to.y) + arcHeight;
        riseSpeed = Mathf.Sqrt(2f * gravity * (apex - from.y));
        airTime = riseSpeed / gravity + Mathf.Sqrt(2f * (apex - to.y) / gravity);
    }

    private Vector2 Snap(Vector2 point, out Collider2D ground)
    {
        int mask = groundLayers.value != 0 ? groundLayers.value : LayerMask.GetMask("Environment");
        RaycastHit2D hit = Physics2D.Raycast(point + Vector2.up * GroundSearchUp, Vector2.down, GroundSearchUp + GroundSearchDown, mask);
        ground = hit.collider;
        return hit.collider != null ? hit.point : point;
    }

    private void OnDrawGizmos()
    {
        Vector2 from = Snap(transform.TransformPoint(start), out Collider2D startGround);
        Vector2 to = Snap(transform.TransformPoint(end), out Collider2D endGround);

        DrawPoint(from, startGround != null);
        DrawPoint(to, endGround != null);
        DrawTraversal(from, to);
        if (bidirectional) DrawTraversal(to, from);
    }

    private static void DrawPoint(Vector2 point, bool grounded)
    {
        Gizmos.color = grounded ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(point, 0.15f);
    }

    /// <summary>
    /// Jumps: the launch arc (its shape doesn't depend on gravity). Falls: the drop from the edge, assuming the enemy
    /// steers at a steady speed onto the end. An arrow at the end shows the direction.
    /// </summary>
    private void DrawTraversal(Vector2 from, Vector2 to)
    {
        bool jump = GetTraversal(from, to) == Traversal.Jump;
        Gizmos.color = jump ? new Color(1f, 0.5f, 0f) : Color.yellow;

        // Any gravity gives the same shape; 1 keeps the numbers simple.
        float riseSpeed = 0f;
        float airTime = Mathf.Sqrt(2f * Mathf.Max(from.y - to.y, 0.01f));
        if (jump) GetArc(from, to, out riseSpeed, out airTime, 1f);

        Vector2 previous = from;
        Vector2 beforeLast = from;
        for (int i = 1; i <= ArcSegments; i++)
        {
            float t = airTime * i / ArcSegments;
            var point = new Vector2(Mathf.Lerp(from.x, to.x, (float)i / ArcSegments), from.y + riseSpeed * t - 0.5f * t * t);
            Gizmos.DrawLine(previous, point);
            beforeLast = previous;
            previous = point;
        }

        // Arrow head pointing along the last segment.
        Vector2 direction = (to - beforeLast).sqrMagnitude > 0.0001f ? (to - beforeLast).normalized : Vector2.down;
        Vector2 back = -direction * 0.3f;
        Gizmos.DrawLine(to, to + (Vector2)(Quaternion.Euler(0f, 0f, 25f) * back));
        Gizmos.DrawLine(to, to + (Vector2)(Quaternion.Euler(0f, 0f, -25f) * back));
    }
}
