using UnityEngine;

public class MeleeEnemy : MonoBehaviour
{
    private enum EnemyState
    {
        Idle,
        Chasing,
        Attacking
    }

    [SerializeField] private EntityData enemyData;

    private EnemyState currentState = EnemyState.Idle;
    private Transform playerTransform;
    private Rigidbody rb;

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
                // Implement attack logic here
                break;
        }
        rb.linearVelocity = velocity;
    }
}
