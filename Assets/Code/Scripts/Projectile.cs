using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private LayerMask hitLayers;

    private Rigidbody rb;
    private Vector3 target;
    private Vector3 direction;
    private Vector3 origin;
    private float speed;
    private float range;
    private bool keepFlying;
    private bool launched;

    void Awake() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        if (!launched) return;

        if (keepFlying || Vector3.Distance(rb.position, origin) >= range)
        {
            // Straight line past the target, only a hit or the lifetime ends it
            rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
            return;
        }

        Vector3 next = Vector3.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if ((next - target).sqrMagnitude < 0.0025f || Vector3.Distance(next, origin) >= range)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0) return;

        if (other.CompareTag("Player"))
        {
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                // damageable.TakeDamage(10);
            }
        }
        Destroy(gameObject);
    }

    public void Shoot(Vector3 targetPosition, EntityData entity)
    {
        target = targetPosition;
        speed = entity.attackSpeed;
        range = entity.attackRange;
        keepFlying = entity.entityID == 2;
        origin = rb.position;
        direction = (target - origin).normalized;   // fixed once, at launch
        launched = true;
    }
}
