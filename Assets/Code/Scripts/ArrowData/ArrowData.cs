using UnityEngine;

[CreateAssetMenu(fileName = "ArrowData", menuName = "Scriptable Objects/ArrowData")]
public class ArrowData : ScriptableObject
{
    [Header("Shop")]
    [Tooltip("Souls needed to unlock this arrow at an altar. Once bought, switching back to it is free until death.")]
    [Min(0)] public int soulCost = 1;

    [Header("Shot")]
    [SerializeField] public float minDamage = 5f;
    [SerializeField] public float maxDamage = 30f;
    [SerializeField] public float minArrowSpeed = 12f;
    [SerializeField] public float maxArrowSpeed = 24f;
    [Tooltip("Downward acceleration of the arrow at no charge. Higher = drops faster. 0 = flies straight.")]
    [Min(0f)] public float minChargeGravity = 20f;
    [Tooltip("Downward acceleration of the arrow at full charge. Lower than the no-charge value makes charged shots fly flatter.")]
    [Min(0f)] public float maxChargeGravity = 5f;
    [SerializeField] public float minKnockback = 2f;
    [SerializeField] public float maxKnockback = 12f;
    [Tooltip("Velocity added to the shooter opposite the shot at no charge. Capped by the shooter's Recoil Max Planar Speed.")]
    [SerializeField] public float minRecoil = 0f;
    [Tooltip("Velocity added to the shooter opposite the shot at full charge.")]
    [SerializeField] public float maxRecoil = 24f;
    [SerializeField] public int bounces = 0;
    [Tooltip("How many targets the arrow passes through. Each target hit uses one; the hit after reaching 0 stops the arrow. " +
             "Explosive arrows still explode on the first hit.")]
    [Min(0)] public int penetrations = 0;

    [Header("Explosion")]
    [Tooltip("Above 0 makes this an explosive arrow: it explodes on impact and uses the explosion values below " +
             "instead of the charge-based damage and knockback. Speed still scales with charge.")]
    [SerializeField] public float explosionRadius = 0f;
    [Tooltip("Damage to a target at the center of the explosion.")]
    [SerializeField] public float explosionCenterDamage = 40f;
    [Tooltip("Damage to a target at the edge of the explosion radius.")]
    [SerializeField] public float explosionEdgeDamage = 5f;
    [Tooltip("Knockback (pushed away from the center) to a target at the center of the explosion.")]
    [SerializeField] public float explosionCenterKnockback = 15f;
    [Tooltip("Knockback to a target at the edge of the explosion radius.")]
    [SerializeField] public float explosionEdgeKnockback = 3f;
    [Tooltip("Optional effect spawned at the explosion center.")]
    [SerializeField] public GameObject explosionEffect;
    [Tooltip("Seconds before the explosion effect is removed. 0 or less keeps it (use that if the effect removes itself).")]
    [SerializeField] public float explosionEffectDuration = 1f;

    [Header("Poison")]
    [Tooltip("Above 0 makes this a poisonous arrow: a hit target keeps taking Poison Damage every Poison Interval for this many seconds. " +
             "The hit itself still does normal damage and knockback; poison ticks never knock back. Hitting a poisoned target again restarts the timer.")]
    [Min(0f)] public float poisonTime = 0f;
    [Tooltip("Seconds between two poison damage ticks.")]
    [Min(0.05f)] public float poisonInterval = 0.5f;
    [Tooltip("Damage dealt on each poison tick.")]
    [Min(0f)] public float poisonDamage = 2f;

    public bool IsExplosive => explosionRadius > 0f;
    public bool IsPoisonous => poisonTime > 0f && poisonDamage > 0f;
}
