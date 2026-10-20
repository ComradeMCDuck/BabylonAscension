
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ArrowProjectile : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float lifetime = 5f;

    [Header("Arrow Data")]
    [SerializeField] private ArrowData arrowData;

    private Rigidbody rb;
    private float damage;
    private float knockback;
    private GameObject owner;
    private bool launched;

    public ArrowData Data => arrowData;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
        float speed = Mathf.Lerp(arrowData.minArrowSpeed, arrowData.maxArrowSpeed, charge);
        knockback = Mathf.Lerp(arrowData.minKnockback, arrowData.maxKnockback, charge);

        owner = shooter;

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
