using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Melee basic attack: after the windup, a DamageHitbox is moved along a curved path in front of the enemy, from
/// slashStart to slashEnd, bowing out by slashCurvature, then disabled. Each swing damages the player at most once.
/// The path (see SlashPath) is set relative to the enemy as if facing right and mirrored when it faces left;
/// select the enemy to see it as gizmos. The swing doesn't aim: it always follows this path.
/// Setup: a child of the enemy with a Collider2D and DamageHitbox. This script sets its position and rotation
/// during the swing, so where it sits in the prefab doesn't matter. The path is set here (it depends on the
/// prefab's size and sprite); damage, duration and knockback live in EntityData ("Slash Attack").
/// </summary>
public class SlashAttack : EnemyAttack
{
    [Tooltip("Hitbox on a child of the enemy. Moved along the slash path during the swing.")]
    [SerializeField] private DamageHitbox hitbox;

    [Header("Slash Path (select the enemy to see it)")]
    [Tooltip("Where the hitbox's center starts the swing, relative to the enemy's position when facing right. Mirrored when facing left.")]
    [SerializeField] private Vector2 slashStart = new Vector2(0.9f, 1.9f);
    [Tooltip("Where the hitbox's center ends the swing, relative to the enemy's position when facing right. Mirrored when facing left.")]
    [SerializeField] private Vector2 slashEnd = new Vector2(0.9f, -0.5f);
    [Tooltip("How far the middle of the swing bows out from the straight line between Start and End, in units. Positive bows away from the enemy (for a top-to-bottom swing in front of it), negative toward it. 0 = straight line.")]
    [SerializeField] private float slashCurvature = 1f;
    [Tooltip("Turns the hitbox to follow the direction of the swing. Off keeps it unrotated.")]
    [SerializeField] private bool rotateAlongPath = true;

    [Header("Animation")]
    [Tooltip("Animator for the enemy to handle slash animation.")]
    [FormerlySerializedAs("animator")]
    [SerializeField] private Animator slasherAnimator;

    private EnemyBehavior enemy;

    public event Action OnSlashStarted;
    public event Action OnSlashEnded;

    public Animator Animator => slasherAnimator;

    private void Awake()
    {
        enemy = GetComponent<EnemyBehavior>();

        if (hitbox == null)
            Debug.LogError($"{name}: SlashAttack needs a Hitbox assigned.", this);
    }

    protected override IEnumerator PerformAttackRoutine(Vector2 targetPosition, EntityData data)
    {
        if (hitbox == null) yield break;

        // Use the way the enemy is facing (it doesn't turn during attacks), so the swing always matches the sprite.
        int facing = enemy != null ? enemy.FacingDirection : (targetPosition.x < transform.position.x ? -1 : 1);
        OnSlashStarted?.Invoke();
        if (slasherAnimator != null) slasherAnimator.SetTrigger("SlashStart");

        PlaceHitbox(facing, 0f);
        hitbox.Activate(data.attackDamage, data.slashKnockback, default);

        float elapsed = 0f;
        while (elapsed < data.slashDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(elapsed / data.slashDuration);
            PlaceHitbox(facing, Mathf.SmoothStep(0f, 1f, t));
            hitbox.CheckHits();
        }

        hitbox.Deactivate();
        OnSlashEnded?.Invoke();
        if (slasherAnimator != null) slasherAnimator.SetTrigger("SlashEnd");
    }

    private void PlaceHitbox(int facing, float t) =>
        SlashPath.PlaceHitbox(hitbox.transform, transform.position, slashStart, slashEnd, slashCurvature, rotateAlongPath, facing, t);

    protected override void OnDisable()
    {
        base.OnDisable();
        if (hitbox != null) hitbox.Deactivate();
    }

    /// <summary>Draws the slash path. Uses the enemy's current facing in play mode, facing right otherwise.</summary>
    private void OnDrawGizmosSelected()
    {
        EnemyBehavior owner = enemy != null ? enemy : GetComponent<EnemyBehavior>();
        int facing = Application.isPlaying && owner != null && owner.FacingDirection < 0 ? -1 : 1;
        SlashPath.DrawGizmos(transform.position, slashStart, slashEnd, slashCurvature, rotateAlongPath, facing, hitbox);
    }
}
