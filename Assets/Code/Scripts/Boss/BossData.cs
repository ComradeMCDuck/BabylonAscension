using UnityEngine;

/// <summary>
/// Tuning for one boss's brain: health, how it moves between attacks and how it picks the next one.
/// Kept separate from EntityData on purpose. The boss still needs an EntityData on its BasicMovementController2D,
/// but only for walking and gravity. Each attack has its own data asset (see BossAttackData).
/// Never modify this asset at runtime; it can be shared by several bosses of the same kind.
/// </summary>
[CreateAssetMenu(fileName = "BossData", menuName = "Scriptable Objects/Boss/Boss Data")]
public class BossData : ScriptableObject
{
    [Header("Health")]
    [Min(1f)] public float maxHealth = 1500f;
    [Tooltip("Seconds the sprite takes to flash red and back when hit.")]
    [Min(0.01f)] public float damageIndicationDuration = 0.15f;
    [Tooltip("Genii orb dropped where the boss dies. Must have a Genii component. Empty = drops nothing.")]
    [UnityEngine.Serialization.FormerlySerializedAs("soulPrefab")]
    public GameObject geniiPrefab;
    [Tooltip("Seconds between dying and the boss being removed, for a death animation.")]
    [Min(0f)] public float deathRemoveDelay = 1.5f;

    [Header("Movement Between Attacks")]
    [Tooltip("Walking speed toward the player while waiting for the next attack, as a fraction of the controller's speed (EntityData.speed).")]
    [Range(0f, 2f)] public float approachSpeedMultiplier = 0.6f;
    [Tooltip("Walking speed away from the player when they are inside the boss, as a fraction of the controller's speed.")]
    [Range(0f, 2f)] public float retreatSpeedMultiplier = 0.5f;
    [Tooltip("Horizontal gap (between the boss's and the player's colliders) the boss walks up to while waiting. Keep it inside the close range attack's Max Range.")]
    [Min(0f)] public float preferredDistance = 1f;
    [Tooltip("The boss only starts walking again once the gap is this much bigger than Preferred Distance, so it doesn't stutter.")]
    [Min(0f)] public float distanceTolerance = 0.4f;

    [Header("Attack Selection")]
    [Tooltip("Seconds after the fight starts before the first attack.")]
    [Min(0f)] public float firstAttackDelay = 0.6f;
    [Tooltip("After every attack the boss waits a random time between these two before the next one, walking toward (or away from) the player.")]
    [Min(0f)] public float minTimeBetweenAttacks = 0.8f;
    [Min(0f)] public float maxTimeBetweenAttacks = 1.6f;
    [Tooltip("The weight of the attack used last is multiplied by this, so the boss mixes up its attacks. 1 = no effect, 0 = never the same attack twice in a row (unless nothing else fits).")]
    [Range(0f, 1f)] public float repeatWeightMultiplier = 0.35f;

    [Header("Knockback")]
    [Tooltip("How much of the knockback from arrows and explosions the boss takes. 0 = can't be pushed. It is never pushed during an attack that moves it (the charge).")]
    [Range(0f, 1f)] public float knockbackMultiplier = 0f;

    private void OnValidate()
    {
        if (maxTimeBetweenAttacks < minTimeBetweenAttacks) maxTimeBetweenAttacks = minTimeBetweenAttacks;
    }
}
