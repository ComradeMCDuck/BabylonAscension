using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ArrowData", menuName = "Scriptable Objects/ArrowData")]
public class ArrowData : ScriptableObject
{
    [Header("Shop")]
    [Tooltip("Genii needed to unlock this arrow at an altar. Once bought, switching back to it is free until death.")]
    [UnityEngine.Serialization.FormerlySerializedAs("soulCost")]
    [Min(0)] public int geniiCost = 1;

    [Header("Special Arrow")]
    [Tooltip("Enemy kills needed to recharge this arrow after it was fired as the special arrow (armed with Q). " +
             "Not used by the basic arrow.")]
    [Min(1)] public int killsToCharge = 3;

    [Header("Shot")]
    [Tooltip("Damage of a bow shot before the bow's charge multiplier (BowCombat's Damage Charge settings). " +
             "Explosion and poison damage are multiplied by the same multiplier.")]
    [FormerlySerializedAs("maxDamage")]
    [Min(0f)] public float baseDamage = 30f;
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

    [Header("Split (Hydra)")]
    [Tooltip("Cloning bounces: hitting a wall with one of these left splits the arrow into two, and each copy keeps " +
             "one fewer. While any are left the arrow pierces every enemy it hits, and after each split the arrows can hit " +
             "the same enemies again. When they run out, each copy " +
             "flies on like a normal arrow, with the Bounces and Penetrations above.")]
    [Min(0)] public int splitBounces = 0;
    [Tooltip("How many arrows come out of each split (the arrow itself counts as one). 2 = it splits in two.")]
    [Range(2, 8)] public int splitCount = 2;
    [Tooltip("Degrees between the outermost arrows of a split. They are spread evenly around the mirrored direction.")]
    [Range(0f, 120f)] public float splitAngle = 30f;

    [Header("Laser")]
    [Tooltip("Above 0 makes this a laser arrow: instead of flying, it spawns a rectangle this long, starting where " +
             "the shot left the bow and pointing along the aim. It goes through walls and damages every enemy inside it " +
             "once, with the normal Base Damage, knockback and poison. Speed, gravity, bounces and splits aren't used.")]
    [Min(0f)] public float laserLength = 0f;
    [Tooltip("Width of the laser rectangle (its contact radius is half of this).")]
    [Min(0.01f)] public float laserWidth = 0.6f;
    [Tooltip("Seconds the rectangle stays. Enemies walking into it during this time are hit too, still once each.")]
    [Min(0.02f)] public float laserDuration = 0.15f;
    [Tooltip("Color of the placeholder beam drawn when the laser prefab has no beam sprite of its own.")]
    public Color laserColor = new Color(1f, 0.3f, 0.2f, 0.85f);

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
    public bool Splits => splitBounces > 0;
    public bool IsLaser => laserLength > 0f;
}
