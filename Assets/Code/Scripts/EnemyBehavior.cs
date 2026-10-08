using System.Collections;
using UnityEngine;

public class EnemyBehavior : MonoBehaviour
{
    private enum EnemyState
    {
        Idle,
        Chasing,
        Attacking
    }

    [SerializeField] private EntityData enemyData;

    [SerializeField] private GameObject attackOrigin; // The point from which the attack originates (e.g., a weapon or hand)
    private GameObject attackProjectile; // The visualisation of the attack (e.g., a particle effect, animation or gameObject)
    private Vector3 lastKnownPlayerPosition;

    private EnemyState currentState = EnemyState.Idle;
    private Transform playerTransform;
    private Rigidbody rb;
    private float nextAttackTime = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        var player = GameObject.FindGameObjectWithTag("Player");
        playerTransform = player != null ? player.transform : null;
        if (playerTransform == null)
        {
            Debug.LogError("Player not found in the scene. Please ensure the player has the 'Player' tag.");
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (playerTransform == null) return;

        // Calculate the direction and distance to the player
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0f;
        if (enemyData.lockZAxis) directionToPlayer.z = 0f;
        float distanceToPlayer = directionToPlayer.magnitude;

        // Determine the current state based on the distance to the player
        if (distanceToPlayer <= enemyData.attackRange) currentState = EnemyState.Attacking;
        else if (distanceToPlayer <= enemyData.detectionRange) currentState = EnemyState.Chasing;
        else currentState = EnemyState.Idle;

        Vector3 velocity = rb.linearVelocity;

        // Update the enemy's velocity based on the current state
        switch (currentState)
        {
            case EnemyState.Idle:
                velocity = Vector3.zero;
                break;
            case EnemyState.Chasing:
                directionToPlayer.Normalize();
                velocity = directionToPlayer * enemyData.moveSpeed;
                break;
            case EnemyState.Attacking:
                velocity = Vector3.zero;
                TryAttackPlayer();
                break;
        }
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    /// <summary>
    /// Enemy attempts to attack the player if the attack cooldown has passed. 
    /// If the player implements the IDamageable interface, the enemy will call the Attack method to initiate the attack.
    /// </summary>
    private void TryAttackPlayer()
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + enemyData.attackCooldown;

        var target = playerTransform.GetComponentInChildren<IDamageable>();
        if (target != null) Attack(); //target.TakeDamage(enemyData.attackDamage);
        else
        {
            Debug.LogWarning("Player does not implement IDamageable interface.");
        }
    }
    
    /// <summary>
    /// Initiates the enemy's attack by instantiating the attack effect and launching it towards the player's last known position.
    /// </summary>
    private void Attack()
    {
        lastKnownPlayerPosition = playerTransform.position;
        attackProjectile = Instantiate(enemyData.attackEffectPrefab, attackOrigin.transform.position, Quaternion.identity);
        StartCoroutine(attackProjectile.GetComponent<Projectile>().Shoot(lastKnownPlayerPosition, enemyData.attackSpeed));
    }

    void OnDrawGizmosSelected()
    {
        if (enemyData == null) return;
        // Draw the detection range in yellow
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyData.detectionRange);
        // Draw the attack range in red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, enemyData.attackRange);
    }
}
