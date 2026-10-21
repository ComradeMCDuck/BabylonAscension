using UnityEngine;

/// <summary>
/// 2D enemy projectile (just a sprite, no Rigidbody or Collider needed).
/// It moves itself and sweeps a circle along its path every physics step, so it can't tunnel
/// through thin colliders and also hits the player's kinematic Rigidbody2D, which normal
/// kinematic-vs-kinematic trigger callbacks would miss.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Tooltip("Layers the projectile stops on: the player's layer and the environment.")]
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private float radius = 0.2f;
    [Tooltip("Rotate the sprite so its local X axis points along the flight direction.")]
    [SerializeField] private bool alignToDirection = true;

    private GameObject owner;
    private Vector2 direction;
    private float speed;
    private float damage;
    private float remainingRange;
    private bool keepFlying;
    private bool launched;

    private void Reset()
    {
        hitLayers = LayerMask.GetMask("Player", "Triggers", "Environment");
    }

    public void Launch(Vector2 targetPosition, EntityData data, GameObject shooter)
    {
        owner = shooter;
        speed = data.attackSpeed;
        damage = data.attackDamage;
        remainingRange = data.projectileRange;
        keepFlying = data.projectileKeepsFlying;

        // Direction is fixed once, at launch.
        direction = targetPosition - (Vector2)transform.position;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        if (alignToDirection) transform.right = direction;

        launched = true;
        Destroy(gameObject, data.projectileLifetime);
    }

    private void FixedUpdate()
    {
        if (!launched) return;

        Vector2 position = transform.position;
        float step = speed * Time.fixedDeltaTime;
        if (!keepFlying) step = Mathf.Min(step, remainingRange);

        RaycastHit2D hit = Physics2D.CircleCast(position, radius, direction, step, hitLayers);
        if (hit.collider != null && !IsOwner(hit.collider))
        {
            OnHit(hit.collider);
            return;
        }

        Vector2 next = position + direction * step;
        transform.position = new Vector3(next.x, next.y, transform.position.z);

        remainingRange -= step;
        if (!keepFlying && remainingRange <= 0f) Destroy(gameObject);
    }

    private void OnHit(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // Health sits on a child of the player root, so search the whole player.
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null)
                damageable = other.transform.root.GetComponentInChildren<IDamageable>();

            if (damageable != null)
            {
                Debug.Log($"Projectile hit {other.name} and dealt {damage} damage.");
                damageable.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }

    private bool IsOwner(Collider2D other)
    {
        return owner != null && other.transform.IsChildOf(owner.transform);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
