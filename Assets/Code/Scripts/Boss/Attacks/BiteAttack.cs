using System.Collections;
using UnityEngine;

/// <summary>
/// Mid range attack: the boss stretches its neck from BiteData's start point toward where the player was when the
/// windup started (or to the fixed end point with Aim At Player off), holds, then pulls back. The aim is fixed for
/// the whole bite, so moving during the windup dodges it. Only the head deals damage, so the player can dash back out of reach or
/// dash under the neck and past the boss. Damages the player at most once per bite.
/// Setup: a child of the boss with a Collider2D (e.g. a CircleCollider2D the size of the head) and a DamageHitbox.
/// Optionally a neck sprite (1x1 unit, pivot in the center) that is stretched between the start point and the head.
/// Keep both out of the Visual Root, because they position themselves.
/// </summary>
public class BiteAttack : BossAttack<BiteData>
{
    [Tooltip("The head: a child of the boss with a Collider2D and DamageHitbox. Moved by this script.")]
    [SerializeField] private DamageHitbox headHitbox;
    [Tooltip("Optional sprite stretched from the start point to the head while biting. Hidden otherwise. It deals no damage.")]
    [SerializeField] private SpriteRenderer neckVisual;

    protected override void Awake()
    {
        base.Awake();

        if (headHitbox == null)
        {
            Debug.LogError($"{name}: BiteAttack needs a Head Hitbox assigned. Disabling.", this);
            enabled = false;
            return;
        }

        if (neckVisual != null) neckVisual.enabled = false;
    }

    protected override IEnumerator Perform(BossTargetInfo target)
    {
        // Fixed when the bite starts, so moving the boss (e.g. knockback) can't drag the head around.
        Vector2 start = FromFeet(data.startOffset);
        Vector2 end = data.aimAtPlayer ? GetAimedEnd(start, target.PlayerCenter) : FromFeet(data.endOffset);

        SetHead(start, start);
        if (neckVisual != null) neckVisual.enabled = true;
        headHitbox.Activate(data.damage);

        // Snap out (ease out), hold, then pull back without damage.
        float elapsed = 0f;
        while (elapsed < data.extendTime)
        {
            yield return NextFixedUpdate;
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / data.extendTime);
            SetHead(start, Vector2.Lerp(start, end, 1f - (1f - t) * (1f - t)));
            headHitbox.CheckHits();
        }

        yield return WaitFixed(data.holdTime);
        headHitbox.Deactivate();

        elapsed = 0f;
        while (elapsed < data.retractTime)
        {
            yield return NextFixedUpdate;
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / data.retractTime);
            SetHead(start, Vector2.Lerp(end, start, Mathf.SmoothStep(0f, 1f, t)));
        }
    }

    /// <summary>
    /// End point toward the player's position at the start of the windup: a bit past them, at most maxReach from the
    /// start, and always forward (the way the boss faces) within the allowed angles.
    /// </summary>
    private Vector2 GetAimedEnd(Vector2 start, Vector2 playerPosition)
    {
        int facing = Boss.FacingDirection;
        Vector2 toPlayer = playerPosition - start;

        // Angle measured as if facing right, so the limits mean the same on both sides.
        float angle = Mathf.Atan2(toPlayer.y, Mathf.Max(toPlayer.x * facing, 0.01f)) * Mathf.Rad2Deg;
        angle = Mathf.Clamp(angle, data.minAimAngle, data.maxAimAngle);

        float length = Mathf.Min(toPlayer.magnitude + data.aimOvershoot, data.maxReach);
        float radians = angle * Mathf.Deg2Rad;
        return start + new Vector2(Mathf.Cos(radians) * facing, Mathf.Sin(radians)) * length;
    }

    private void SetHead(Vector2 start, Vector2 head)
    {
        Transform t = headHitbox.transform;
        t.position = new Vector3(head.x, head.y, t.position.z);

        if (neckVisual == null) return;

        Vector2 neck = head - start;
        Transform n = neckVisual.transform;
        n.position = new Vector3((start.x + head.x) * 0.5f, (start.y + head.y) * 0.5f, n.position.z);
        n.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(neck.y, neck.x) * Mathf.Rad2Deg);

        Vector3 parentScale = n.parent != null ? n.parent.lossyScale : Vector3.one;
        n.localScale = new Vector3(
            Mathf.Max(neck.magnitude, 0.01f) / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
            data.neckThickness / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
            1f);
    }

    protected override void Cleanup()
    {
        if (headHitbox != null) headHitbox.Deactivate();
        if (neckVisual != null) neckVisual.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null || !TryGetGizmoFrame(out Vector2 feet, out int facing)) return;

        Vector2 start = FromFeet(data.startOffset, feet, facing);
        Vector2 end = FromFeet(data.endOffset, feet, facing);

        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(start, 0.15f);

        if (data.aimAtPlayer)
        {
            // The range the head can reach: the two angle limits out to maxReach.
            foreach (float angle in new[] { data.minAimAngle, data.maxAimAngle })
            {
                float radians = angle * Mathf.Deg2Rad;
                Gizmos.DrawLine(start, start + new Vector2(Mathf.Cos(radians) * facing, Mathf.Sin(radians)) * data.maxReach);
            }
            return;
        }

        Gizmos.DrawLine(start, end);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(end, 0.25f);
    }
}
