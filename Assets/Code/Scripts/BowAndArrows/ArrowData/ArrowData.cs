using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ArrowData", menuName = "Scriptable Objects/ArrowData")]
public class ArrowData : ScriptableObject
{
    [Header("Info")]
    [Tooltip("Name shown to the player, e.g. on the HUD. Empty = the arrow prefab's name is shown instead.")]
    public string arrowName = "";

    [Header("Shop")]
    [Tooltip("Genii needed to unlock this arrow at an altar. Once bought, switching back to it is free until death.")]
    [UnityEngine.Serialization.FormerlySerializedAs("soulCost")]
    [Min(0)] public int geniiCost = 1;

    [Header("Shot")]
    [Tooltip("Damage of a bow shot before the bow's charge multiplier (BowCombat's Damage Charge settings). " +
             "Explosion damage is multiplied by the same multiplier.")]
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
    [Tooltip("The arrow's damage is multiplied by this each time it passes through an enemy. " +
             "0.5 = halved after every enemy. 1 = every enemy takes full damage.")]
    [Range(0f, 1f)] public float penetrationDamageMultiplier = 1f;

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

    [Header("Meteor Shower")]
    [Tooltip("Above 0 makes this a meteor shower arrow: it flies to the point the player aimed at (set the gravity " +
             "above to 0 so it flies straight) and opens a portal there, which drops this many Meteor Prefab arrows. " +
             "It passes through enemies on the way; a wall stops it early and opens the portal there.")]
    [Min(0)] public int meteorCount = 0;
    [Tooltip("Arrow prefab (with ArrowProjectile) dropped by the portal, normally an explosive arrow. It is " +
             "launched at full charge with its own ArrowData, so set the low damage and big knockback there.")]
    public GameObject meteorPrefab;
    [Tooltip("Seconds over which the meteors are dropped, spread evenly.")]
    [Min(0f)] public float meteorDuration = 1.5f;
    [Tooltip("Width of the portal. Each meteor falls from a random point along it.")]
    [Min(0f)] public float portalWidth = 4f;
    [Tooltip("How far above the point the arrow reached the portal opens.")]
    public float portalHeightOffset = 0f;
    [Tooltip("Degrees a meteor can lean away from straight down, picked at random for each one.")]
    [Range(0f, 45f)] public float meteorAngleSpread = 8f;
    [Tooltip("Optional effect shown as the portal. Empty = a placeholder oval in Portal Color is drawn.")]
    public GameObject portalEffect;
    [Tooltip("Color of the placeholder portal.")]
    public Color portalColor = new Color(0.6f, 0.3f, 1f, 0.85f);

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
    public bool Splits => splitBounces > 0;
    public bool IsMeteorShower => meteorCount > 0 && meteorPrefab != null;
}
