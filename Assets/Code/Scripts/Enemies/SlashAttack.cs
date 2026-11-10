using System.Collections;
using UnityEngine;

/// <summary>
/// Melee basic attack: after the windup, a DamageHitbox is swung around an origin point toward the player,
/// from top to bottom, then disabled. Each swing damages the player at most once.
/// Setup: Enemy -> SlashPivot (empty child) -> SlashHitbox (child with a BoxCollider2D and DamageHitbox).
/// Place the hitbox to the right of the pivot (its distance from the pivot is the reach); the pivot's position
/// and rotation are set by this script. Settings live in EntityData ("Slash Attack").
/// </summary>
public class SlashAttack : EnemyAttack
{
    [Tooltip("Empty child the hitbox rotates around. Moved to EntityData.slashOriginOffset when the slash starts.")]
    [SerializeField] private Transform slashPivot;
    [Tooltip("Hitbox on a child of the pivot, placed to the right of it.")]
    [SerializeField] private DamageHitbox hitbox;

    private EnemyBehavior enemy;

    private void Awake()
    {
        enemy = GetComponent<EnemyBehavior>();

        if (slashPivot == null || hitbox == null)
            Debug.LogError($"{name}: SlashAttack needs a Slash Pivot and a Hitbox assigned.", this);
        else if (!hitbox.transform.IsChildOf(slashPivot))
            Debug.LogWarning($"{name}: The slash hitbox should be a child of the slash pivot, otherwise it won't swing.", this);
    }

    protected override IEnumerator PerformAttackRoutine(Vector2 targetPosition, EntityData data)
    {
        if (slashPivot == null || hitbox == null) yield break;

        // Use the way the enemy is facing (it doesn't turn during attacks), so the swing always matches the sprite.
        int facing = enemy != null ? enemy.FacingDirection : (targetPosition.x < transform.position.x ? -1 : 1);
        slashPivot.localPosition = new Vector3(data.slashOriginOffset.x * facing, data.slashOriginOffset.y, 0f);

        // Center the arc on the direction to the player and swing from above to below on the facing side.
        Vector2 toTarget = targetPosition - (Vector2)slashPivot.position;
        float aimAngle = toTarget.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg
            : (facing > 0 ? 0f : 180f);
        float halfArc = data.slashArcAngle * 0.5f;
        float startAngle = aimAngle + halfArc * facing;
        float endAngle = aimAngle - halfArc * facing;

        SetAngle(startAngle);
        hitbox.Activate(data.attackDamage);

        float elapsed = 0f;
        while (elapsed < data.slashDuration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(elapsed / data.slashDuration);
            SetAngle(Mathf.Lerp(startAngle, endAngle, Mathf.SmoothStep(0f, 1f, t)));
            hitbox.CheckHits();
        }

        hitbox.Deactivate();
    }

    private void SetAngle(float angle)
    {
        slashPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (hitbox != null) hitbox.Deactivate();
    }
}
