using System.Collections;
using UnityEngine;

/// <summary>
/// Close range basic attack: a DamageHitbox is moved along a curved path in front of the boss, from slashStart to
/// slashEnd, bowing out by slashCurvature, like the melee enemies' SlashAttack. Each swing damages the player at
/// most once. The path (see SlashPath) is set relative to the boss's feet (bottom center of its collider) as if
/// facing right and mirrored when it faces left; select the boss to see it as gizmos. The swing doesn't aim.
/// Setup: a child of the boss with a Collider2D and DamageHitbox. This script sets its position and rotation
/// during the swing, so where it sits in the prefab doesn't matter. Keep it out of the Visual Root.
/// The path is set here; damage, knockback and swing duration live in BossSlashData.
/// </summary>
public class BossSlashAttack : BossAttack<BossSlashData>
{
    [Tooltip("Hitbox on a child of the boss. Moved along the slash path during the swing.")]
    [SerializeField] private DamageHitbox hitbox;

    [Header("Slash Path (select the boss to see it)")]
    [Tooltip("Where the hitbox's center starts the swing, relative to the boss's feet when facing right. Mirrored when facing left.")]
    [SerializeField] private Vector2 slashStart = new Vector2(1.9f, 5f);
    [Tooltip("Where the hitbox's center ends the swing, relative to the boss's feet when facing right. Mirrored when facing left.")]
    [SerializeField] private Vector2 slashEnd = new Vector2(3f, 0.3f);
    [Tooltip("How far the middle of the swing bows out from the straight line between Start and End, in units. Positive bows away from the boss (for a top-to-bottom swing in front of it), negative toward it. 0 = straight line.")]
    [SerializeField] private float slashCurvature = 1.5f;
    [Tooltip("Turns the hitbox to follow the direction of the swing. Off keeps it unrotated.")]
    [SerializeField] private bool rotateAlongPath = true;

    protected override void Awake()
    {
        base.Awake();

        if (hitbox == null)
        {
            Debug.LogError($"{name}: BossSlashAttack needs a Hitbox assigned. Disabling.", this);
            enabled = false;
        }
    }

    protected override IEnumerator Perform(BossTargetInfo target)
    {
        int facing = Boss.FacingDirection;

        PlaceHitbox(facing, 0f);
        hitbox.Activate(data.damage, data.knockback, default);

        float elapsed = 0f;
        while (elapsed < data.swingDuration)
        {
            yield return NextFixedUpdate;
            elapsed += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(elapsed / data.swingDuration);
            PlaceHitbox(facing, Mathf.SmoothStep(0f, 1f, t));
            hitbox.CheckHits();
        }
    }

    // Measured from the boss's feet every step, so the swing stays with the boss if it gets pushed.
    private void PlaceHitbox(int facing, float t) =>
        SlashPath.PlaceHitbox(hitbox.transform, Boss.FeetCenter, slashStart, slashEnd, slashCurvature, rotateAlongPath, facing, t);

    protected override void Cleanup()
    {
        if (hitbox != null) hitbox.Deactivate();
    }

    /// <summary>Draws the slash path. Uses the boss's current facing in play mode, facing right otherwise.</summary>
    private void OnDrawGizmosSelected()
    {
        if (!TryGetGizmoFrame(out Vector2 feet, out int facing)) return;
        SlashPath.DrawGizmos(feet, slashStart, slashEnd, slashCurvature, rotateAlongPath, facing, hitbox);
    }
}
