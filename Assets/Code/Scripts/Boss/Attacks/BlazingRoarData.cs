using UnityEngine;

/// <summary>
/// Settings for BlazingRoarAttack: a roar that damages and pushes away everything around the boss, followed by dark
/// energy projectiles that hover above it and fire at the player one by one. The base Damage is the roar's damage.
/// Usable at any distance; give it a high Inside Weight so it is the answer to the player standing inside the boss.
/// The roar is a shockwave that spreads from its center out to roarRadius over roarExpandTime.
/// </summary>
[CreateAssetMenu(fileName = "BlazingRoarData", menuName = "Scriptable Objects/Boss/Attacks/Blazing Roar")]
public class BlazingRoarData : BossAttackData
{
    [Header("Roar (area damage)")]
    [Tooltip("Center of the roar, relative to the boss's feet when facing right.")]
    public Vector2 roarOriginOffset = new Vector2(0f, 1.2f);
    [Tooltip("Everything within this distance (to the closest point of its collider) is damaged and pushed away.")]
    [Min(0f)] public float roarRadius = 4f;
    [Tooltip("Seconds the roar's shockwave takes to spread from its center out to Roar Radius. Targets are hit when the wave reaches them. 0 = everything in the radius is hit at once.")]
    [Min(0f)] public float roarExpandTime = 0.35f;
    [Tooltip("Horizontal push away from the boss. Big enough to throw a player out of the boss's body.")]
    [Min(0f)] public float pushForce = 14f;
    [Tooltip("Upward push added to the knockback.")]
    [Min(0f)] public float pushUpForce = 6f;
    [Tooltip("Optional effect spawned at the roar's center (e.g. a shockwave sprite).")]
    public GameObject roarEffectPrefab;
    [Tooltip("Seconds before the roar effect is removed. 0 = it removes itself.")]
    [Min(0f)] public float roarEffectDuration = 1f;

    [Header("Dark Energy Projectiles")]
    [Tooltip("Prefab with an ArrowProjectile. It flies straight (no gravity) and only takes its bounce count from its ArrowData, if it has one.")]
    public GameObject projectilePrefab;
    [Tooltip("Where each projectile hovers, relative to the top center of the boss's collider. One projectile per entry, fired in this order.")]
    public Vector2[] hoverOffsets =
    {
        new Vector2(-1.2f, 0.8f),
        new Vector2(0f, 1.3f),
        new Vector2(1.2f, 0.8f)
    };
    [Tooltip("Seconds after the roar before the projectiles appear.")]
    [Min(0f)] public float conjureDelay = 0.2f;
    [Tooltip("Seconds the projectiles take to grow to full size. The first one fires after this.")]
    [Min(0f)] public float conjureTime = 0.7f;
    [Tooltip("Seconds between two shots.")]
    [Min(0f)] public float fireInterval = 0.5f;
    [Min(0.01f)] public float projectileSpeed = 11f;
    [Min(0f)] public float projectileDamage = 15f;
    [Min(0.1f)] public float projectileLifetime = 4f;

    private void Reset()
    {
        minRange = 0f;
        maxRange = 30f;
        maxHeightDifference = 30f;
        weight = 0.5f;
        insideWeight = 5f;
        windup = 0.8f;
        recovery = 0.6f;
        cooldown = 9f;
        damage = 20f;
    }
}
