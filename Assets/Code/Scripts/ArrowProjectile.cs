using DYP;
using UnityEngine;

/// <summary>
/// 2D projectile shared by the player's bow and ranged/melee enemies (just a sprite, no Rigidbody or Collider needed).
/// It moves itself and sweeps a circle along its path every physics step, so it can't tunnel
/// through thin colliders and also hits kinematic Rigidbody2Ds, which kinematic-vs-kinematic trigger callbacks would miss.
/// The player launches it with a charge and its ArrowData; enemies launch it with their EntityData.
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    // Distance kept from a surface after bouncing, so the next sweep doesn't start inside it.
    private const float BounceSkin = 0.01f;

    [Header("Lifetime")]
    [Tooltip("Seconds before the projectile is destroyed when launched by the bow. Enemy launches use EntityData.projectileLifetime.")]
    [SerializeField] private float lifetime = 5f;

    [Header("Arrow Data")]
    [Tooltip("Damage, speed, knockback and bounces for bow shots. Enemy launches only take the bounce count from it (if assigned).")]
    [SerializeField] private ArrowData arrowData;

    [Header("Collision")]
    [Tooltip("Layers a bow shot can hit: its targets and the environment. Defaults to \"Enemy\" and \"Environment\" if left empty.")]
    [SerializeField] private LayerMask hitLayers;
    [Tooltip("Layers an enemy shot can hit, so the same prefab works for both sides. Defaults to \"Player\", \"Triggers\" and \"Environment\" if left empty.")]
    [SerializeField] private LayerMask enemyShotHitLayers;
    [Tooltip("Layers the projectile bounces off (or stops on when out of bounces). Defaults to \"Environment\" if left empty.")]
    [SerializeField] private LayerMask environmentLayers;
    [Tooltip("Radius of the circle swept along the projectile's path.")]
    [SerializeField] private float radius = 0.1f;
    [Tooltip("Rotate the sprite so its local X axis points along the flight direction.")]
    [SerializeField] private bool alignToDirection = true;

    private Vector2 direction;
    private float speed;
    private float damage;
    private float knockback;
    private int remainingBounces;
    private float remainingRange = float.PositiveInfinity;
    private GameObject owner;
    private LayerMask activeHitLayers;
    private bool launched;

    public ArrowData Data => arrowData;
    public int RemainingBounces => remainingBounces;

    private void Awake()
    {
        if (hitLayers.value == 0)
            hitLayers = LayerMask.GetMask("Enemy", "Environment");

        if (enemyShotHitLayers.value == 0)
            enemyShotHitLayers = LayerMask.GetMask("Player", "Triggers", "Environment");

        if (environmentLayers.value == 0)
            environmentLayers = LayerMask.GetMask("Environment");
    }

    /// <summary>
    /// Bow shot: damage, speed and knockback scale with the charge (0..1) between ArrowData's min and max.
    /// </summary>
    public void Launch(
        Vector3 direction,
        float charge,
        GameObject shooter)
    {
        if (arrowData == null)
        {
            Debug.LogError("ArrowProjectile needs an ArrowData asset assigned.", this);
            Destroy(gameObject);
            return;
        }

        damage = Mathf.Lerp(arrowData.minDamage, arrowData.maxDamage, charge);
        speed = Mathf.Lerp(arrowData.minArrowSpeed, arrowData.maxArrowSpeed, charge);
        knockback = Mathf.Lerp(arrowData.minKnockback, arrowData.maxKnockback, charge);

        // Copy the count so the shared ArrowData asset is never modified.
        remainingBounces = Mathf.Max(0, arrowData.bounces);
        remainingRange = float.PositiveInfinity;
        activeHitLayers = hitLayers;

        Begin(direction, shooter, lifetime);
    }

    /// <summary>
    /// Enemy shot toward a target position, using the enemy's attack stats.
    /// Unless projectileKeepsFlying is set, it disappears after projectileRange (that's how melee enemies use it).
    /// </summary>
    public void Launch(Vector2 targetPosition, EntityData data, GameObject shooter)
    {
        damage = data.attackDamage;
        speed = data.attackSpeed;
        knockback = 0f;
        remainingBounces = arrowData != null ? Mathf.Max(0, arrowData.bounces) : 0;
        remainingRange = data.projectileKeepsFlying ? float.PositiveInfinity : data.projectileRange;
        activeHitLayers = enemyShotHitLayers;

        // Direction is fixed once, at launch.
        Vector2 toTarget = targetPosition - (Vector2)transform.position;
        Begin(toTarget.sqrMagnitude > 0.0001f ? toTarget : Vector2.right, shooter, data.projectileLifetime);
    }

    private void Begin(Vector2 launchDirection, GameObject shooter, float lifeSeconds)
    {
        owner = shooter;
        direction = launchDirection.normalized;

        // Make the projectile's local X axis follow its travel direction.
        if (alignToDirection) transform.right = direction;

        launched = true;
        Destroy(gameObject, lifeSeconds);
    }

    private void FixedUpdate()
    {
        if (!launched)
            return;

        Vector2 position = transform.position;
        float step = Mathf.Min(speed * Time.fixedDeltaTime, remainingRange);

        RaycastHit2D hit = Physics2D.CircleCast(position, radius, direction, step, activeHitLayers);

        if (hit.collider == null || IsOwner(hit.collider))
        {
            MoveTo(position + direction * step);

            remainingRange -= step;
            if (remainingRange <= 0f) Destroy(gameObject);
            return;
        }

        if (IsEnvironment(hit.collider))
        {
            HandleEnvironmentHit(hit);
            return;
        }

        HandleTargetHit(hit.collider);
    }

    private void HandleTargetHit(Collider2D other)
    {
        // Health can sit on a sibling child of the character (player and enemies both do this),
        // so search the whole character: everything under the Rigidbody2D the hit collider belongs to.
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null)
        {
            Transform character = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform.root;
            target = character.GetComponentInChildren<IDamageable>();
        }

        if (target != null)
            target.TakeDamage(damage);

        // Characters move with the same 2D controller as the player, so knockback goes through it.
        if (knockback > 0f)
        {
            BasicMovementController2D targetMovement =
                other.GetComponentInParent<BasicMovementController2D>();

            if (targetMovement != null)
                targetMovement.AddImpulse(direction * knockback);
        }

        Destroy(gameObject);
    }

    private bool IsOwner(Collider2D other)
    {
        // Ignore the shooter and its children.
        return owner != null &&
            (other.gameObject == owner ||
             other.transform.IsChildOf(owner.transform));
    }

    // Returns true if the collider is on a layer that the projectile should bounce off of.
    private bool IsEnvironment(Collider2D other)
    {
        return (environmentLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void HandleEnvironmentHit(RaycastHit2D hit)
    {
        if (remainingBounces <= 0)
        {
            Destroy(gameObject);
            return;
        }

        remainingBounces--;

        // Range already travelled up to the surface.
        remainingRange -= hit.distance;

        // Mirror the direction around the surface normal, like a pool ball:
        // Example: hitting the ground at 30 degrees leaves it at 150 degrees.
        direction = Vector2.Reflect(direction, hit.normal).normalized;
        if (alignToDirection) transform.right = direction;

        // Stop just off the surface; the rest of the flight continues next step.
        MoveTo(hit.centroid + hit.normal * BounceSkin);
    }

    private void MoveTo(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
