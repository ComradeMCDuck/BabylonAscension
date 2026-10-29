using System.Collections.Generic;
using DYP;
using UnityEngine;

/// <summary>
/// 2D projectile shared by the player's bow and ranged enemies (just a sprite, no Rigidbody or Collider needed).
/// It moves itself and sweeps a circle along its path every physics step, so it can't tunnel
/// through thin colliders and also hits kinematic Rigidbody2Ds, which kinematic-vs-kinematic trigger callbacks would miss.
/// Gravity pulls it into an arc. The player launches it with a charge and its ArrowData (gravity scales with the charge);
/// enemies launch it with their EntityData and aim higher to make up for the drop.
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
    private float gravity;
    private float damage;
    private float knockback;
    private int remainingBounces;
    private int remainingPenetrations;
    // Characters already hit by this projectile, so a penetrating shot can't hit the same one twice.
    private readonly HashSet<Transform> hitCharacters = new HashSet<Transform>();
    private GameObject owner;
    private LayerMask activeHitLayers;
    private bool launched;

    // Reused buffers for explosions, to avoid allocating every time one goes off.
    private static readonly List<Collider2D> s_ExplosionOverlaps = new List<Collider2D>();
    private static readonly Dictionary<Transform, (Collider2D collider, float distance)> s_ExplosionTargets =
        new Dictionary<Transform, (Collider2D collider, float distance)>();

    // Reused buffer for every hit along one physics step.
    private static readonly List<RaycastHit2D> s_StepHits = new List<RaycastHit2D>();

    public ArrowData Data => arrowData;
    public int RemainingBounces => remainingBounces;
    public int RemainingPenetrations => remainingPenetrations;
    public bool IsExplosive => arrowData != null && arrowData.IsExplosive;

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
    /// Bow shot: damage, speed, knockback and gravity scale with the charge (0..1) between ArrowData's min and max.
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
        gravity = Mathf.Lerp(arrowData.minChargeGravity, arrowData.maxChargeGravity, charge);

        // Copy the count so the shared ArrowData asset is never modified.
        remainingBounces = Mathf.Max(0, arrowData.bounces);
        remainingPenetrations = Mathf.Max(0, arrowData.penetrations);
        activeHitLayers = hitLayers;

        Begin(direction, shooter, lifetime);
    }

    /// <summary>
    /// Enemy shot at a target position, using the enemy's attack stats. The speed is fixed, so the enemy
    /// aims higher to make up for projectileGravity.
    /// </summary>
    public void Launch(Vector2 targetPosition, EntityData data, GameObject shooter)
    {
        damage = data.attackDamage;
        speed = data.attackSpeed;
        gravity = data.projectileGravity;
        knockback = 0f;
        remainingBounces = arrowData != null ? Mathf.Max(0, arrowData.bounces) : 0;
        remainingPenetrations = arrowData != null ? Mathf.Max(0, arrowData.penetrations) : 0;
        activeHitLayers = enemyShotHitLayers;

        // Direction is fixed once, at launch.
        Begin(GetAimDirection(targetPosition - (Vector2)transform.position), shooter, data.projectileLifetime);
    }

    /// <summary>
    /// Launch direction that makes the arc pass through the target offset, using the flatter of the two angles that do.
    /// Out of reach, it shoots at 45 degrees (the farthest-reaching angle) toward the target, so the arrow lands short.
    /// </summary>
    private Vector2 GetAimDirection(Vector2 toTarget)
    {
        if (toTarget.sqrMagnitude < 0.0001f) return Vector2.right;

        float dx = Mathf.Abs(toTarget.x);
        float dy = toTarget.y;
        float side = toTarget.x < 0f ? -1f : 1f;

        // No drop, or straight up/down: aim right at it.
        if (gravity <= 0f || speed <= 0f || dx < 0.0001f) return toTarget;

        // Projectile motion: tan(angle) = (v^2 - sqrt(v^4 - g(g dx^2 + 2 dy v^2))) / (g dx). The minus sign is the flat shot.
        float v2 = speed * speed;
        float discriminant = v2 * v2 - gravity * (gravity * dx * dx + 2f * dy * v2);
        if (discriminant < 0f) return new Vector2(side, 1f);

        float tanAngle = (v2 - Mathf.Sqrt(discriminant)) / (gravity * dx);
        return new Vector2(side, tanAngle);
    }

    private void Begin(Vector2 launchDirection, GameObject shooter, float lifeSeconds)
    {
        owner = shooter;
        direction = launchDirection.normalized;
        hitCharacters.Clear();

        // Make the projectile's local X axis follow its travel direction.
        if (alignToDirection) transform.right = direction;

        launched = true;
        Destroy(gameObject, lifeSeconds);
    }

    private void FixedUpdate()
    {
        if (!launched)
            return;

        // Half the step's gravity before moving and half after, so the flight follows the exact arc enemies aim with.
        ApplyGravity(Time.fixedDeltaTime * 0.5f);

        Vector2 position = transform.position;
        float step = speed * Time.fixedDeltaTime;

        // Collect everything along this step (not just the first hit), so a penetrating
        // shot can pass through several targets and still find the wall behind them.
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(activeHitLayers);
        filter.useTriggers = Physics2D.queriesHitTriggers;

        s_StepHits.Clear();
        Physics2D.CircleCast(position, radius, direction, filter, s_StepHits, step);
        s_StepHits.Sort((a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D hit in s_StepHits)
        {
            if (IsOwner(hit.collider))
                continue;

            if (IsEnvironment(hit.collider))
            {
                HandleEnvironmentHit(hit);
                return;
            }

            // Already hit by this projectile (its other colliders, or still overlapping after penetrating it).
            if (hitCharacters.Contains(GetCharacter(hit.collider)))
                continue;

            if (HandleTargetHit(hit))
                return;
        }

        MoveTo(position + direction * step);
        ApplyGravity(Time.fixedDeltaTime * 0.5f);
    }

    private void ApplyGravity(float time)
    {
        if (gravity <= 0f) return;

        Vector2 velocity = direction * speed + Vector2.down * (gravity * time);
        speed = velocity.magnitude;
        if (speed > 0.0001f) direction = velocity / speed;
        if (alignToDirection) transform.right = direction;
    }

    // The character a collider belongs to: everything under the Rigidbody2D the collider is attached to.
    private static Transform GetCharacter(Collider2D other)
    {
        return other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform.root;
    }

    private static IDamageable FindDamageable(Collider2D other)
    {
        // Health can sit on a sibling child of the character (player and enemies both do this),
        // so search the whole character.
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null)
            target = GetCharacter(other).GetComponentInChildren<IDamageable>();

        return target;
    }

    /// <summary>
    /// Damages and knocks back every target within ArrowData.explosionRadius.
    /// Damage and knockback fall off linearly from the center values to the edge values,
    /// measured to the closest point of each target's collider. Knockback pushes away from the center.
    /// </summary>
    private void Explode(Vector2 center)
    {
        // Destroy is deferred to the end of the frame; stop here so a second physics step can't explode again.
        launched = false;

        float explosionRadius = arrowData.explosionRadius;

        if (arrowData.explosionEffect != null)
        {
            GameObject effect = Instantiate(
                arrowData.explosionEffect,
                new Vector3(center.x, center.y, transform.position.z),
                Quaternion.identity);

            if (arrowData.explosionEffectDuration > 0f)
                Destroy(effect, arrowData.explosionEffectDuration);
        }

        // Only targets get hurt, never the environment.
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(activeHitLayers.value & ~environmentLayers.value);
        filter.useTriggers = Physics2D.queriesHitTriggers;

        s_ExplosionOverlaps.Clear();
        Physics2D.OverlapCircle(center, explosionRadius, filter, s_ExplosionOverlaps);

        // A character can have several colliders; keep the one closest to the center.
        s_ExplosionTargets.Clear();
        foreach (Collider2D col in s_ExplosionOverlaps)
        {
            if (IsOwner(col))
                continue;

            // Walls shield targets behind them.
            if (IsBlockedFromExplosion(center, col))
                continue;

            float distance = Vector2.Distance(center, col.ClosestPoint(center));
            Transform character = GetCharacter(col);

            if (!s_ExplosionTargets.TryGetValue(character, out var closest) || distance < closest.distance)
                s_ExplosionTargets[character] = (col, distance);
        }

        foreach (var pair in s_ExplosionTargets)
        {
            Collider2D col = pair.Value.collider;
            float falloff = Mathf.Clamp01(pair.Value.distance / explosionRadius);

            IDamageable target = FindDamageable(col);
            if (target != null)
                target.TakeDamage(Mathf.Lerp(arrowData.explosionCenterDamage, arrowData.explosionEdgeDamage, falloff));

            float explosionKnockback = Mathf.Lerp(arrowData.explosionCenterKnockback, arrowData.explosionEdgeKnockback, falloff);

            if (explosionKnockback > 0f)
            {
                Vector2 away = (Vector2)pair.Key.position - center;
                Vector2 pushDirection = away.sqrMagnitude > 0.0001f ? away.normalized : direction;

                ApplyKnockback(col, pushDirection * explosionKnockback);
            }
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// True if environment is in the way of both the collider's closest point and its center,
    /// so a target peeking out from behind cover still gets hit.
    /// </summary>
    private bool IsBlockedFromExplosion(Vector2 center, Collider2D col)
    {
        return Physics2D.Linecast(center, col.ClosestPoint(center), environmentLayers).collider != null
            && Physics2D.Linecast(center, col.bounds.center, environmentLayers).collider != null;
    }

    /// <summary>
    /// Damages and knocks back the hit target. Returns true if the projectile stopped
    /// (exploded or out of penetrations), false if it penetrates and keeps flying.
    /// </summary>
    private bool HandleTargetHit(RaycastHit2D hit)
    {
        if (IsExplosive)
        {
            Explode(hit.centroid);
            return true;
        }

        Collider2D other = hit.collider;
        hitCharacters.Add(GetCharacter(other));

        IDamageable target = FindDamageable(other);

        if (target != null)
            target.TakeDamage(damage);

        if (knockback > 0f)
            ApplyKnockback(other, direction * knockback);

        // Damage over time; the ticks only deal damage, the knockback above happens once on the hit.
        if (target != null && arrowData != null && arrowData.IsPoisonous)
            PoisonEffect.Apply(target, arrowData.poisonDamage, arrowData.poisonTime, arrowData.poisonInterval);

        // Penetrate: use one and keep flying.
        if (remainingPenetrations > 0)
        {
            remainingPenetrations--;
            return false;
        }

        // Destroy is deferred to the end of the frame; stop here so a second physics step can't hit again.
        launched = false;
        Destroy(gameObject);
        return true;
    }

    /// <summary>
    /// Characters move with the same 2D controller as the player, so knockback normally goes through it.
    /// A flying enemy has that controller switched off, so its flight takes the push instead.
    /// </summary>
    private static void ApplyKnockback(Collider2D target, Vector2 impulse)
    {
        EnemyFlight flight = target.GetComponentInParent<EnemyFlight>();
        if (flight != null && flight.IsFlying)
        {
            flight.ApplyKnockback(impulse);
            return;
        }

        BasicMovementController2D targetMovement = target.GetComponentInParent<BasicMovementController2D>();
        if (targetMovement != null)
            targetMovement.ApplyKnockback(impulse);
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
            // Explode just off the surface, so the wall check doesn't start inside the wall that was hit.
            if (IsExplosive)
                Explode(hit.centroid + hit.normal * BounceSkin);
            else
                Destroy(gameObject);
            return;
        }

        remainingBounces--;

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

        if (IsExplosive)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f);
            Gizmos.DrawWireSphere(transform.position, arrowData.explosionRadius);
        }
    }
}
