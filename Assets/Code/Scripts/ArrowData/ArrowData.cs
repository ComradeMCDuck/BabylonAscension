using UnityEngine;

[CreateAssetMenu(fileName = "ArrowData", menuName = "Scriptable Objects/ArrowData")]
public class ArrowData : ScriptableObject
{
    [SerializeField] public float minDamage = 5f;
    [SerializeField] public float maxDamage = 30f;
    [SerializeField] public float minArrowSpeed = 12f;
    [SerializeField] public float maxArrowSpeed = 24f;
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

    public bool IsExplosive => explosionRadius > 0f;
}
