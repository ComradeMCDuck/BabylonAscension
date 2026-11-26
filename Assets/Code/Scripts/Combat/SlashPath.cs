using UnityEngine;

/// <summary>
/// The curved path a slash hitbox follows, shared by the enemies' SlashAttack and the boss's BossSlashAttack.
/// The path is a quadratic Bezier curve from start to end, given relative to the attacker as if facing right and
/// mirrored for facing left, so both sides match. Curvature is how far the middle of the curve bows out from the
/// straight line between start and end. Also places the hitbox on the path and draws the path as gizmos.
/// </summary>
public static class SlashPath
{
    // Line segments used to draw the curve gizmo.
    private const int GizmoSegments = 24;
    // Arrowheads along the gizmo curve, showing which way the swing travels.
    private const int GizmoArrows = 4;
    // Length of the arrowhead lines, in units.
    private const float GizmoArrowSize = 0.15f;
    // Radius of the start and end markers, in units.
    private const float GizmoPointRadius = 0.1f;

    /// <summary>
    /// Point on the path at t (0 = start, 1 = end) as an offset from the attacker, plus the direction the path
    /// travels there.
    /// </summary>
    public static void Evaluate(Vector2 start, Vector2 end, float curvature, int facing, float t,
        out Vector2 offset, out Vector2 direction)
    {
        Vector2 chord = end - start;
        // Left-hand normal of the chord: for a top-to-bottom swing in front of the attacker, this points away from it.
        Vector2 normal = chord.sqrMagnitude > 0.0001f ? new Vector2(-chord.y, chord.x).normalized : Vector2.zero;
        // A quadratic Bezier's midpoint sits halfway to its control point, so double the curvature to make the
        // middle of the swing bow out by exactly curvature.
        Vector2 control = (start + end) * 0.5f + normal * (curvature * 2f);

        float u = 1f - t;
        offset = u * u * start + 2f * u * t * control + t * t * end;
        direction = 2f * u * (control - start) + 2f * t * (end - control);

        offset.x *= facing;
        direction.x *= facing;
    }

    /// <summary>Rotation for a hitbox travelling in direction: along it, or unrotated.</summary>
    public static Quaternion HitboxRotation(Vector2 direction, bool rotateAlongPath)
    {
        if (!rotateAlongPath || direction.sqrMagnitude < 0.0001f) return Quaternion.identity;
        return Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    /// <summary>Moves and turns the hitbox to the point at t of the path, measured from origin (world space).</summary>
    public static void PlaceHitbox(Transform hitbox, Vector2 origin, Vector2 start, Vector2 end, float curvature,
        bool rotateAlongPath, int facing, float t)
    {
        Evaluate(start, end, curvature, facing, t, out Vector2 offset, out Vector2 direction);
        hitbox.position = new Vector3(origin.x + offset.x, origin.y + offset.y, hitbox.position.z);
        hitbox.rotation = HitboxRotation(direction, rotateAlongPath);
    }

    /// <summary>
    /// Draws the path from origin: the curve (yellow, with arrowheads toward the end), the start (green) and end (red)
    /// points, and the hitbox's BoxCollider2D outline at both ends so the reach is visible. Call from OnDrawGizmosSelected.
    /// </summary>
    public static void DrawGizmos(Vector2 origin, Vector2 start, Vector2 end, float curvature, bool rotateAlongPath,
        int facing, DamageHitbox hitbox)
    {
        Vector3 origin3 = origin;

        Gizmos.color = Color.yellow;
        Evaluate(start, end, curvature, facing, 0f, out Vector2 previous, out _);
        for (int i = 1; i <= GizmoSegments; i++)
        {
            Evaluate(start, end, curvature, facing, i / (float)GizmoSegments, out Vector2 point, out _);
            Gizmos.DrawLine(origin3 + (Vector3)previous, origin3 + (Vector3)point);
            previous = point;
        }

        for (int i = 1; i <= GizmoArrows; i++)
        {
            Evaluate(start, end, curvature, facing, i / (GizmoArrows + 1f), out Vector2 point, out Vector2 direction);
            DrawArrowHead(origin3 + (Vector3)point, direction);
        }

        Evaluate(start, end, curvature, facing, 0f, out Vector2 startPoint, out Vector2 startDirection);
        Evaluate(start, end, curvature, facing, 1f, out Vector2 endPoint, out Vector2 endDirection);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin3 + (Vector3)startPoint, GizmoPointRadius);
        DrawHitboxOutline(hitbox, origin3 + (Vector3)startPoint, HitboxRotation(startDirection, rotateAlongPath));

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin3 + (Vector3)endPoint, GizmoPointRadius);
        DrawHitboxOutline(hitbox, origin3 + (Vector3)endPoint, HitboxRotation(endDirection, rotateAlongPath));
    }

    private static void DrawArrowHead(Vector3 position, Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Vector3 back = -(Vector3)direction.normalized * GizmoArrowSize;
        Gizmos.DrawLine(position, position + Quaternion.Euler(0f, 0f, 30f) * back);
        Gizmos.DrawLine(position, position + Quaternion.Euler(0f, 0f, -30f) * back);
    }

    /// <summary>Outline of the hitbox's BoxCollider2D at a point of the path. Other collider shapes are skipped.</summary>
    private static void DrawHitboxOutline(DamageHitbox hitbox, Vector3 position, Quaternion rotation)
    {
        if (hitbox == null || !hitbox.TryGetComponent(out BoxCollider2D box)) return;

        Vector3 scale = hitbox.transform.lossyScale;
        Vector3 size = new Vector3(box.size.x * Mathf.Abs(scale.x), box.size.y * Mathf.Abs(scale.y), 0f);
        Vector3 offset = new Vector3(box.offset.x * scale.x, box.offset.y * scale.y, 0f);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
        Gizmos.DrawWireCube(offset, size);
        Gizmos.matrix = previousMatrix;
    }
}
