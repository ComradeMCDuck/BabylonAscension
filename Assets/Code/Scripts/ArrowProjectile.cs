
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ArrowProjectile : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float lifetime = 5f;

    [Header("Arrow Data")]
    [SerializeField] private ArrowData arrowData;

    [Header("Bouncing")]
    [Tooltip("Layers the arrow bounces off. Defaults to \"Environment\" if left empty.")]
    [SerializeField] private LayerMask environmentLayers;

    private Rigidbody rb;
    private Collider arrowCollider;
    private float damage;
    private float knockback;
    private int remainingBounces;
    private GameObject owner;
    private bool launched;

    public ArrowData Data => arrowData;
    public int RemainingBounces => remainingBounces;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        arrowCollider = GetComponent<Collider>();

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
        float speed = Mathf.Lerp(arrowData.minArrowSpeed, arrowData.maxArrowSpeed, charge);
        knockback = Mathf.Lerp(arrowData.minKnockback, arrowData.maxKnockback, charge);

        // Copy the count so the shared ArrowData asset is never modified.
        remainingBounces = Mathf.Max(0, arrowData.bounces);

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

        if (IsEnvironment(other))
        {
            HandleEnvironmentHit(other);
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

    // Returns true if the collider is on a layer that the arrow should bounce off of.
    private bool IsEnvironment(Collider other)
    {
        return (environmentLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    private void HandleEnvironmentHit(Collider surface)
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.z = 0f;

        float speed = velocity.magnitude;

        if (speed < 0.0001f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 direction = velocity / speed;
        Vector3 normal = GetSurfaceNormal(surface, direction);

        // Already moving away from this surface (e.g. touching the next
        // tile right after a bounce): ignore it instead of bouncing twice.
        if (Vector3.Dot(direction, normal) >= 0f)
            return;

        if (remainingBounces <= 0)
        {
            Destroy(gameObject);
            return;
        }

        remainingBounces--;

        // Mirror the direction around the surface normal, like a pool ball:
        // Example: hitting the ground at 30 degrees leaves it at 150 degrees.
        Vector3 reflected = Vector3.Reflect(direction, normal);
        reflected.z = 0f;
        reflected.Normalize();

        rb.linearVelocity = reflected * speed;
        transform.right = reflected;
    }

    private Vector3 GetSurfaceNormal(Collider surface, Vector3 direction)
    {
        // Start the ray behind the arrow so it begins outside the surface.
        float backDistance =
            arrowCollider.bounds.extents.magnitude +
            rb.linearVelocity.magnitude * Time.fixedDeltaTime;

        Vector3 origin = transform.position - direction * backDistance;
        Ray ray = new Ray(origin, direction);

        Vector3 normal;

        if (surface.Raycast(ray, out RaycastHit hit, backDistance * 3f))
        {
            normal = hit.normal;
        }
        else
        {
            // Grazing hits can miss the ray; use the closest point instead.
            normal = origin - surface.ClosestPoint(origin);
        }

        normal.z = 0f;

        if (normal.sqrMagnitude < 0.0001f)
            return -direction;

        return normal.normalized;
    }

}
