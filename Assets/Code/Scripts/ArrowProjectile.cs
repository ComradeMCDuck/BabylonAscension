
using DYP;
using UnityEngine;

/// <summary>
/// 2D arrow (just a sprite, no Rigidbody or Collider needed).
/// It moves itself and sweeps a circle along its path every physics step, so it can't tunnel
/// through thin colliders and also hits enemies with kinematic Rigidbody2Ds.
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    // Distance kept from a surface after bouncing, so the next sweep doesn't start inside it.
    private const float BounceSkin = 0.01f;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 5f;

    [Header("Arrow Data")]
    [SerializeField] private ArrowData arrowData;

    [Header("Collision")]
    [Tooltip("Layers the arrow can hit: enemies and the environment. Defaults to \"Enemy\" and \"Environment\" if left empty.")]
    [SerializeField] private LayerMask hitLayers;
    [Tooltip("Layers the arrow bounces off. Defaults to \"Environment\" if left empty.")]
    [SerializeField] private LayerMask environmentLayers;
    [Tooltip("Radius of the circle swept along the arrow's path.")]
    [SerializeField] private float radius = 0.1f;

    private Vector2 direction;
    private float speed;
    private float damage;
    private float knockback;
    private int remainingBounces;
    private GameObject owner;
    private bool launched;

    public ArrowData Data => arrowData;
    public int RemainingBounces => remainingBounces;

    private void Awake()
    {
        if (hitLayers.value == 0)
            hitLayers = LayerMask.GetMask("Enemy", "Environment");

        if (environmentLayers.value == 0)
            environmentLayers = LayerMask.GetMask("Environment");
    }

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

        owner = shooter;

        this.direction = ((Vector2)direction).normalized;

        // Make the arrow's local X axis follow its travel direction.
        transform.right = this.direction;

        launched = true;
        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (!launched)
            return;

        Vector2 position = transform.position;
        float step = speed * Time.fixedDeltaTime;

        RaycastHit2D hit = Physics2D.CircleCast(position, radius, direction, step, hitLayers);

        if (hit.collider == null || IsOwner(hit.collider))
        {
            MoveTo(position + direction * step);
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
        IArrowDamageable target =
            other.GetComponentInParent<IArrowDamageable>();

        if (target != null)
        {
            target.TakeArrowDamage(damage);

            // Enemies move with the same 2D controller as the player, so knockback goes through it.
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

    // Returns true if the collider is on a layer that the arrow should bounce off of.
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

        // Mirror the direction around the surface normal, like a pool ball:
        // Example: hitting the ground at 30 degrees leaves it at 150 degrees.
        direction = Vector2.Reflect(direction, hit.normal).normalized;
        transform.right = direction;

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
