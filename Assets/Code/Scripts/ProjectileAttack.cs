using UnityEngine;

/// <summary>
/// Attack that spawns EntityData.attackEffectPrefab (a 2D Projectile) toward the target.
/// Used by both melee enemies (short attackRange, projectile stops at the range)
/// and shooters (long attackRange, projectileKeepsFlying).
/// </summary>
public class ProjectileAttack : EnemyAttack
{
    [Tooltip("Where the projectile spawns, relative to the enemy when facing right. Mirrored when the target is on the left.")]
    [SerializeField] private Vector2 spawnOffset = new Vector2(0.5f, 0f);

    protected override void PerformAttack(Vector2 targetPosition, EntityData data)
    {
        if (data.attackEffectPrefab == null)
        {
            Debug.LogWarning($"{name}: No attackEffectPrefab assigned in {data.name}.", this);
            return;
        }

        GameObject projectileObject = Instantiate(data.attackEffectPrefab, GetSpawnPosition(targetPosition), Quaternion.identity);

        if (!projectileObject.TryGetComponent(out Projectile projectile))
        {
            Debug.LogError($"{data.attackEffectPrefab.name} has no Projectile component.", this);
            Destroy(projectileObject);
            return;
        }

        projectile.Launch(targetPosition, data, gameObject);
    }

    private Vector3 GetSpawnPosition(Vector2 targetPosition)
    {
        float side = targetPosition.x < transform.position.x ? -1f : 1f;
        return transform.position + new Vector3(spawnOffset.x * side, spawnOffset.y, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position + (Vector3)spawnOffset, 0.1f);
    }
}
