using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private LayerMask hitLayers;   // Environment + Player

    private Rigidbody rb;
    private Vector3 target;
    private float speed;
    private bool launched;

    void Awake() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        if (!launched) return;

        Vector3 next = Vector3.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if ((next - target).sqrMagnitude < 0.0025f) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Projectile hit: {other.gameObject.name} (Layer: {other.gameObject.layer})");
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0) return;

        if (other.CompareTag("Player"))
        {
            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                Debug.Log("Player hit! Dealing damage.");
                // damageable.TakeDamage(10);
            }
        }
        Destroy(gameObject);
    }

    public void Shoot(Vector3 targetPosition, float attackSpeed)
    {
        target = targetPosition + Vector3.up;
        speed = attackSpeed;
        launched = true;
    }
}
