
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ArrowProjectile : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float lifetime = 5f;

    private Rigidbody rb;
    private float damage;
    private float knockback;
    private GameObject owner;
    private bool launched;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Launch(
        Vector3 direction,
        float speed,
        float damageAmount,
        float knockbackAmount,
        GameObject shooter)
    {
        owner = shooter;
        damage = damageAmount;
        knockback = knockbackAmount;

        direction.z = 0f;
        direction.Normalize();

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.linearVelocity = direction * speed;

        // Make the arrow's local X axis follow its travel direction.
        transform.right = direction;

        launched = true;
        Destroy(gameObject, lifetime);
    }


    private void OnTriggerEnter(Collider other)
    {
        if (!launched)
            return;

        // Ignore the shooter and its children.
        if (owner != null &&
            (other.gameObject == owner ||
             other.transform.IsChildOf(owner.transform)))
        {
            return;
        }

        IArrowDamageable target =
            other.GetComponentInParent<IArrowDamageable>();

        if (target != null)
        {
            target.TakeArrowDamage(damage);

            Rigidbody targetRb = other.attachedRigidbody;

            if (targetRb != null)
            {
                Vector3 direction = rb.linearVelocity.normalized;

                targetRb.AddForce(
                    direction * knockback,
                    ForceMode.Impulse
                );
            }
        }

        Destroy(gameObject);
    }

}
