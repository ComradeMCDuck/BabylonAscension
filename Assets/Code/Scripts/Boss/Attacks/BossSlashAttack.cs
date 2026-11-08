using System.Collections;
using UnityEngine;

/// <summary>
/// Close range basic attack: a DamageHitbox swings around a pivot toward the player, from top to bottom, like the
/// melee enemies' SlashAttack. Each swing damages the player at most once.
/// Setup: Boss -> SlashPivot (empty child) -> SlashHitbox (child with a Collider2D and DamageHitbox). Place the
/// hitbox to the right of the pivot (its distance from the pivot is the reach); this script moves and rotates the pivot.
/// </summary>
public class BossSlashAttack : BossAttack<BossSlashData>
{
    [Tooltip("Empty child the hitbox rotates around. Moved to the data's Origin Offset when the slash starts.")]
    [SerializeField] private Transform slashPivot;
    [Tooltip("Hitbox on a child of the pivot, placed to the right of it.")]
    [SerializeField] private DamageHitbox hitbox;

    protected override void Awake()
    {
        base.Awake();

        if (slashPivot == null || hitbox == null)
        {
            Debug.LogError($"{name}: BossSlashAttack needs a Slash Pivot and a Hitbox assigned. Disabling.", this);
            enabled = false;
        }
        else if (!hitbox.transform.IsChildOf(slashPivot))
        {
            Debug.LogWarning($"{name}: The slash hitbox should be a child of the slash pivot, otherwise it won't swing.", this);
        }
    }

    protected override IEnumerator Perform(BossTargetInfo target)
    {
        int facing = Boss.FacingDirection;
        slashPivot.position = FromFeet(data.originOffset);

        // Center the arc on where the player was when the windup started, swinging from above to below.
        Vector2 toTarget = target.PlayerCenter - (Vector2)slashPivot.position;
        float aimAngle = toTarget.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg
            : (facing > 0 ? 0f : 180f);
        float halfArc = data.arcAngle * 0.5f;
        float startAngle = aimAngle + halfArc * facing;
        float endAngle = aimAngle - halfArc * facing;

        SetAngle(startAngle);
        hitbox.Activate(data.damage);

        float elapsed = 0f;
        while (elapsed < data.swingDuration)
        {
            yield return NextFixedUpdate;
            elapsed += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(elapsed / data.swingDuration);
            SetAngle(Mathf.Lerp(startAngle, endAngle, Mathf.SmoothStep(0f, 1f, t)));
            hitbox.CheckHits();
        }
    }

    protected override void Cleanup()
    {
        if (hitbox != null) hitbox.Deactivate();
    }

    private void SetAngle(float angle)
    {
        slashPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null || !TryGetGizmoFrame(out Vector2 feet, out int facing)) return;

        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(FromFeet(data.originOffset, feet, facing), 0.12f);
    }
}
